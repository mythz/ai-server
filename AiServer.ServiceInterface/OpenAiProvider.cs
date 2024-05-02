using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AiServer.ServiceModel;
using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Text;

namespace AiServer.ServiceInterface;

public record OpenAiChatResult(OpenAiChatResponse Response, int DurationMs);

public interface IOpenAiProvider
{
    Task<bool> IsOnlineAsync(IApiProviderWorker apiProvider);

    Task<OpenAiChatResult> ChatAsync(IApiProviderWorker worker, OpenAiChat request);
}

public class AiProviderFactory(OpenAiProvider openAiProvider, GoogleOpenAiProvider googleProvider)
{
    public static AiProviderFactory Instance { get; set; }
    
    public IOpenAiProvider GetOpenAiProvider(string? type = null)
    {
        return type == nameof(GoogleOpenAiProvider)
            ? googleProvider
            : openAiProvider;
    }
}

public class OpenAiProvider(ILogger<OpenAiProvider> log) : IOpenAiProvider
{
    public async Task<OpenAiChatResult> ChatAsync(IApiProviderWorker worker, OpenAiChat request)
    {
        var sw = Stopwatch.StartNew();
        var openApiChatEndpoint = worker.GetApiEndpointUrlFor(TaskType.OpenAiChat);

        Action<HttpRequestMessage>? requestFilter = worker.ApiKey != null
            ? req => req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", worker.ApiKey)
            : null;

        request.Model = worker.GetApiModel(request.Model);
        Exception? firstEx = null;

        var retries = 0;
        while (retries++ < 10)
        {
            var headers = Array.Empty<string>();
            var contentHeaders = Array.Empty<string>();
                
            int retryAfter = 0;
            var sleepMs = 1000 * retries;
            try
            {
                var responseJson = await openApiChatEndpoint.PostJsonToUrlAsync(request, 
                    requestFilter:requestFilter,
                    responseFilter: res =>
                    {
                        headers = res.Headers.Select(x => $"{x.Key}: {x.Value}").ToArray();
                        contentHeaders = res.Content.Headers.Select(x => $"{x.Key}: {x.Value}").ToArray();

                        // GROQ
                        if (res.Headers.TryGetValues("retry-after", out var retryAfterValues))
                        {
                            var retryAfterStr = retryAfterValues.FirstOrDefault();
                            log.LogWarning("retry-after: {RetryAfter}", retryAfterStr ?? "null");
                            if (retryAfterStr != null) 
                                int.TryParse(retryAfterStr, out retryAfter);
                        }
                    });
                var durationMs = (int)sw.ElapsedMilliseconds;
                var response = responseJson.FromJson<OpenAiChatResponse>();
                return new(response, durationMs);
            }
            catch (HttpRequestException e)
            {
                log.LogInformation("Response Headers: {Headers}", string.Join("; ", headers));
                log.LogInformation("Response.Content Headers: {Headers}", string.Join("; ", contentHeaders));

                firstEx ??= e;
                if (e.StatusCode is null or HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError)
                {
                    if (retryAfter > 0)
                        sleepMs = retryAfter * 1000;
                    log.LogInformation("{Message}, retrying after {SleepMs}ms", e.Message, sleepMs);
                    await Task.Delay(sleepMs);
                }
                else throw;
            }
        }
        throw firstEx ?? new Exception($"Failed to complete OpenAI Chat request after {retries} retries");
    }

    public async Task<bool> IsOnlineAsync(IApiProviderWorker apiProvider)
    {
        try
        {
            var heartbeatUrl = apiProvider.HeartbeatUrl;
            if (heartbeatUrl != null)
            {
                Action<HttpRequestMessage>? requestFilter = apiProvider.ApiKey != null
                    ? req => req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiProvider.ApiKey)
                    : null;
                await heartbeatUrl.GetStringFromUrlAsync(requestFilter:requestFilter);
            }

            var apiModel = apiProvider.GetPreferredApiModel();
            var request = new OpenAiChat
            {
                Model = apiModel,
                Messages = [
                    new() { Role = "user", Content = "1+1=" },
                ],
                MaxTokens = 2,
                Stream = false,
            };
            await ChatAsync(apiProvider, request);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

public class GoogleSafetySetting
{
    public string Category { get; set; }
    public string Threshold { get; set; }
}

public class GoogleOpenAiProvider(ILogger<GoogleOpenAiProvider> log) : IOpenAiProvider
{
    public List<GoogleSafetySetting> SafetySettings { get; set; } =
    [
        new() { Category = "HARM_CATEGORY_DANGEROUS_CONTENT", Threshold = "BLOCK_ONLY_HIGH" },
        new() { Category = "HARM_CATEGORY_HATE_SPEECH", Threshold = "BLOCK_ONLY_HIGH" },
        new() { Category = "HARM_CATEGORY_HARASSMENT", Threshold = "BLOCK_ONLY_HIGH" },
        new() { Category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", Threshold = "BLOCK_ONLY_HIGH" },
    ];

    public async Task<OpenAiChatResult> ChatAsync(IApiProviderWorker worker, OpenAiChat request)
    {
        if (string.IsNullOrEmpty(worker.ApiKey))
            throw new NotSupportedException("GoogleOpenAiProvider requires an ApiKey");

        var sw = Stopwatch.StartNew();
        var url = worker.GetApiEndpointUrlFor(TaskType.OpenAiChat)
            .AddQueryParam("key", worker.ApiKey);
        
        var generationConfig = new Dictionary<string, object> {};
        if (request.Temperature != null)
            generationConfig["temperature"] = request.Temperature;
        if (request.MaxTokens != null)
            generationConfig["maxOutputTokens"] = request.MaxTokens;

        var googleRequest = new Dictionary<string, object>
        {
            ["contents"] = new List<object> {
                new Dictionary<string, object>
                {
                    ["parts"] = new List<object> {
                        new Dictionary<string, object> {
                            ["text"] = request.Messages[0].Content,
                        }
                    }
                }
            },
            ["safetySettings"] = SafetySettings.Map(x => new Dictionary<string, object> {
                ["category"] = x.Category,
                ["threshold"] = x.Threshold,
            }),
            ["generationConfig"] = generationConfig,
        };

        var json = JSON.stringify(googleRequest);
        var responseJson = await url.PostJsonToUrlAsync(json);

        var res = (Dictionary<string, object>)JSON.parse(responseJson);
        var durationMs = (int)sw.ElapsedMilliseconds;
        var created = DateTime.UtcNow.ToUnixTime();

        var content = "";
        var finishReason = "stop";
        if (res.TryGetValue("candidates", out var oCandidates) && oCandidates is List<object> { Count: > 0 } candidates)
        {
            var candidate = (Dictionary<string, object>)candidates[0];
            if (candidate.TryGetValue("content", out var oContent) && oContent is Dictionary<string,object> contentObj)
            {
                if (contentObj.TryGetValue("parts", out var oParts) && oParts is List<object> { Count: > 0 } parts)
                {
                    if (parts[0] is Dictionary<string, object> part && part.TryGetValue("text", out var oText) && oText is string text)
                        content = text;
                }
            }
            if (candidate.TryGetValue("finishReason", out var oFinishReason))
                finishReason = (string)oFinishReason;
        }
            
        var to = new OpenAiChatResponse {
            Id = $"chatcmpl-{created}",
            Object = "chat.completion",
            Model = request.Model,
            Choices = [
                new() {
                    Index = 0,
                    Message = new() {
                        Role = "assistant",
                        Content = content,
                    },
                    FinishReason = finishReason,
                }
            ],
        };
        
        return new(to, durationMs);
    }

    public async Task<bool> IsOnlineAsync(IApiProviderWorker apiProvider)
    {
        try
        {
            var apiModel = apiProvider.GetPreferredApiModel();
            var request = new OpenAiChat
            {
                Model = apiModel,
                Messages = [
                    new() { Role = "user", Content = "1+1=" },
                ],
                MaxTokens = 2,
                Stream = false,
            };
            await ChatAsync(apiProvider, request);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
