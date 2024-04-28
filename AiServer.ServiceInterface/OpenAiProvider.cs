using System.Diagnostics;
using System.Net.Http.Headers;
using AiServer.ServiceModel;
using ServiceStack;
using ServiceStack.Text;

namespace AiServer.ServiceInterface;

public record OpenAiChatResult(OpenAiChatResponse Response, int DurationMs);

public interface IOpenAiProvider
{
    Task<OpenAiChatResult> ChatAsync(ApiProvider apiProvider, OpenAiChat request);
}

public class OpenAiProvider : IOpenAiProvider
{
    public static OpenAiProvider Instance = new();
    
    public async Task<OpenAiChatResult> ChatAsync(ApiProvider apiProvider, OpenAiChat request)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var openApiChatEndpoint = apiProvider.GetApiEndpointUrlFor(TaskType.OpenAiChat);

            Action<HttpRequestMessage>? requestFilter = apiProvider.ApiKey != null
                ? req => req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiProvider.ApiKey)
                : null;

            request.Model = apiProvider.GetApiModel(request.Model);

            var responseJson = await openApiChatEndpoint.PostJsonToUrlAsync(request, requestFilter:requestFilter);
            var durationMs = (int)sw.ElapsedMilliseconds;
            var response = responseJson.FromJson<OpenAiChatResponse>();
            return new(response, durationMs);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}

public class GoogleSafetySetting
{
    public string Category { get; set; }
    public string Threshold { get; set; }
}

public class GoogleOpenAiProvider : IOpenAiProvider
{
    public static GoogleOpenAiProvider Instance = new();

    public List<GoogleSafetySetting> SafetySettings { get; set; } =
    [
        new() { Category = "HARM_CATEGORY_DANGEROUS_CONTENT", Threshold = "BLOCK_ONLY_HIGH" },
        new() { Category = "HARM_CATEGORY_HATE_SPEECH", Threshold = "BLOCK_ONLY_HIGH" },
        new() { Category = "HARM_CATEGORY_HARASSMENT", Threshold = "BLOCK_ONLY_HIGH" },
        new() { Category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", Threshold = "BLOCK_ONLY_HIGH" },
    ];
    
    public async Task<OpenAiChatResult> ChatAsync(ApiProvider apiProvider, OpenAiChat request)
    {
        if (string.IsNullOrEmpty(apiProvider.ApiKey))
            throw new NotSupportedException("GoogleOpenAiProvider requires an ApiKey");

        var sw = Stopwatch.StartNew();
        var url = apiProvider.GetApiEndpointUrlFor(TaskType.OpenAiChat)
            .AddQueryParam("key", apiProvider.ApiKey);
        
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
}

public static class OpenAiProviderExtensions
{
    public static string GetApiEndpointUrlFor(this ApiProvider apiProvider, TaskType taskType)
    {
        var apiBaseUrl = apiProvider.ApiBaseUrl ?? apiProvider.ApiType?.ApiBaseUrl
            ?? throw new NotSupportedException("No ApiBaseUrl found in ApiProvider or ApiType");
        var chatPath = apiProvider.TaskPaths?.TryGetValue(taskType, out var path) == true ? path : null;
        if (chatPath == null)
            apiProvider.ApiType?.TaskPaths.TryGetValue(taskType, out chatPath);
        if (chatPath == null)
            throw new NotSupportedException("No TaskPath found for TaskType.OpenAiChat in ApiType or ApiProvider");
        
        return apiBaseUrl.CombineWith(chatPath);
    }

    public static IOpenAiProvider GetOpenAiProvider(this ApiProvider apiProvider)
    {
        if (apiProvider.ApiType?.OpenAiProvider == nameof(GoogleOpenAiProvider))
            return GoogleOpenAiProvider.Instance;
        
        return OpenAiProvider.Instance;
    }

    public static string GetApiModel(this ApiProvider apiProvider, string model)
    {
        var apiModel = apiProvider.Models.Find(x => x.Model == model);
        if (apiModel?.ApiModel != null)
            return apiModel.ApiModel;
        
        return apiProvider.ApiType?.ApiModels.TryGetValue(model, out var apiModelAlias) == true
            ? apiModelAlias
            : model;
    }
}