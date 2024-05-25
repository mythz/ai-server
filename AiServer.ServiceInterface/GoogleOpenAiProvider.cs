using System.Diagnostics;
using AiServer.ServiceModel;
using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Text;

namespace AiServer.ServiceInterface;

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

    public async Task<OpenAiChatResult> ChatAsync(IApiProviderWorker worker, OpenAiChat request, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(worker.ApiKey))
            throw new NotSupportedException("GoogleOpenAiProvider requires an ApiKey");

        var sw = Stopwatch.StartNew();
        var responseJson = await SendRequestAsync(worker, request, token);

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
    
    private async Task<string> SendRequestAsync(IApiProviderWorker worker, OpenAiChat request, CancellationToken token)
    {
        var baseUrl = worker.GetApiEndpointUrlFor(TaskType.OpenAiChat);
        request.Model = worker.GetApiModel(request.Model);
        var url = baseUrl.Replace("${MODEL}", request.Model)
            .AddQueryParam("key", worker.ApiKey);
        
        var generationConfig = new Dictionary<string, object>();
        if (request.Temperature != null)
            generationConfig["temperature"] = request.Temperature;
        if (request.MaxTokens != null)
            generationConfig["maxOutputTokens"] = request.MaxTokens;

        var systemMessage = request.Messages.FirstOrDefault(x => "system".EqualsIgnoreCase(x.Role));
        var messages = request.Messages
            .Where(x => !"system".EqualsIgnoreCase(x.Role))
            .Map(x => new Dictionary<string, object> {
                ["role"] = "user".EqualsIgnoreCase(x.Role) ? "user" : "model",
                ["parts"] = new List<object> {
                    new Dictionary<string,string> {
                        ["text"] = x.Content
                    }
                },
            });
        
        var googleRequest = new Dictionary<string, object>
        {
            ["contents"] = messages,
            ["safetySettings"] = SafetySettings.Map(x => new Dictionary<string, object> {
                ["category"] = x.Category,
                ["threshold"] = x.Threshold,
            }),
        };
        if (generationConfig.Count > 0)
        {
            googleRequest["generationConfig"] = generationConfig;
        }

        if (systemMessage != null)
        {
            // Old gemini-pro before gemini-1.0-pro-002	doesn't support system prompts
            if (request.Model.StartsWith("gemini-1.0"))
            {
                messages.Insert(0, new Dictionary<string, object> {
                    ["role"] = "model",
                    ["parts"] = new List<object> {
                        new Dictionary<string,string> {
                            ["text"] = "Understood."
                        }
                    }
                });
                messages.Insert(0, new Dictionary<string, object> {
                    ["role"] = "user",
                    ["parts"] = new List<object> {
                        new Dictionary<string,string> {
                            ["text"] = "System prompt: " + systemMessage.Content
                        }
                    }
                });
            }
            else
            {
                googleRequest["systemInstruction"] = new Dictionary<string, object> {
                    ["parts"] = new List<object> {
                        new Dictionary<string,string> {
                            ["text"] = systemMessage.Content
                        }
                    }
                };
            }
        }

        var json = JSON.stringify(googleRequest);
        Console.WriteLine(url);
        Console.WriteLine(json);
        var responseJson = await url.PostJsonToUrlAsync(json, token:token);
        return responseJson;
    }

    public async Task<bool> IsOnlineAsync(IApiProviderWorker worker, CancellationToken token = default)
    {
        try
        {
            var apiModel = worker.GetPreferredApiModel();
            var request = new OpenAiChat
            {
                Model = apiModel,
                Messages = [
                    new() { Role = "user", Content = "1+1=" },
                ],
                MaxTokens = 2,
                Stream = false,
            };
            await SendRequestAsync(worker, request, token);
            return true;
        }
        catch (Exception e)
        {
            if (e is TaskCanceledException)
                throw;
            return false;
        }
    }
}
