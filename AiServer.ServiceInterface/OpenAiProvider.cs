using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AiServer.ServiceModel;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace AiServer.ServiceInterface;

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
                        headers = res.Headers.Select(x => $"{x.Key}: {x.Value.FirstOrDefault()}").ToArray();
                        contentHeaders = res.Content.Headers.Select(x => $"{x.Key}: {x.Value.FirstOrDefault()}").ToArray();

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
                log.LogInformation("[{Name}] Headers:\n{Headers}", worker.Name, string.Join('\n', headers));
                log.LogInformation("[{Name}] Content Headers:\n{Headers}", worker.Name, string.Join('\n', contentHeaders));

                firstEx ??= e;
                if (e.StatusCode is null or HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError)
                {
                    if (retryAfter > 0)
                        sleepMs = retryAfter * 1000;
                    log.LogInformation("[{Name}] {Message} for {Url}, retrying after {SleepMs}ms", 
                        worker.Name, e.Message, openApiChatEndpoint, sleepMs);
                    await Task.Delay(sleepMs);
                }
                else throw;
            }
        }
        throw firstEx ?? new Exception($"[{worker.Name}] Failed to complete OpenAI Chat request after {retries} retries");
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
