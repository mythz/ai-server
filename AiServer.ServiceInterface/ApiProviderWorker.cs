using System.Collections.Concurrent;
using AiServer.ServiceModel;
using AiServer.ServiceModel.Types;
using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.Messaging;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface;

public interface IApiProviderWorker : IDisposable
{
    string? ApiKey { get; }
    string? HeartbeatUrl { get; }
    string GetApiEndpointUrlFor(TaskType taskType);
    string GetPreferredApiModel();
    string GetApiModel(string model);
}

public class ApiProviderWorker(ApiProvider apiProvider) : IApiProviderWorker
{
    public string Name = apiProvider.Name;
    public string[] Models = apiProvider.Models.Select(x => x.Model).ToArray();
    public int Concurrency = apiProvider.Concurrency;
    public string? ApiKey { get; } = apiProvider.ApiKey;
    public string? HeartbeatUrl { get; } = GetHeartbeatUrl(apiProvider);
    public bool Enabled = apiProvider.Enabled;
    public int ChatQueueCount => ChatQueue.Count;
    private BlockingCollection<string> ChatQueue { get; } = new();

    private CancellationTokenSource cts = new();

    private bool isDisposed;
    private long received = 0;
    private long completed = 0;
    private long retries = 0;
    private long failed = 0;
    private long running = 0;

    public bool IsOffline
    {
        get => apiProvider.OfflineDate != null;
        set => apiProvider.OfflineDate = value ? DateTime.UtcNow : null;
    }
    
    private static string? GetHeartbeatUrl(ApiProvider apiProvider)
    {
        var heartbeatUrl = apiProvider.HeartbeatUrl;
        if (heartbeatUrl == null)
        {
            heartbeatUrl = apiProvider.ApiType?.HeartbeatUrl;
            if (heartbeatUrl != null && heartbeatUrl.StartsWith('/'))
                return apiProvider.ApiBaseUrl.CombineWith(heartbeatUrl);
        }
        return heartbeatUrl;
    }

    public void AddToChatQueue(string requestId)
    {
        ChatQueue.Add(requestId);
        Interlocked.Increment(ref received);
    }
    
    public IOpenAiProvider GetOpenAiProvider()
    {
        if (apiProvider.ApiType?.OpenAiProvider == nameof(GoogleOpenAiProvider))
            return GoogleOpenAiProvider.Instance;
        
        return OpenAiProvider.Instance;
    }
    
    public string GetApiEndpointUrlFor(TaskType taskType)
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
    
    public string GetApiModel(string model)
    {
        var apiModel = apiProvider.Models.Find(x => x.Model == model);
        if (apiModel?.ApiModel != null)
            return apiModel.ApiModel;
        
        return apiProvider.ApiType?.ApiModels.TryGetValue(model, out var apiModelAlias) == true
            ? apiModelAlias
            : model;
    }

    public string GetPreferredApiModel()
    {
        var apiProviderModel = apiProvider.Models.FirstOrDefault()
            ?? throw new ArgumentNullException(nameof(apiProvider.Models));
        var model = apiProviderModel.ApiModel ?? apiProviderModel.Model;
        return model ?? throw new ArgumentNullException(nameof(model));;
    }
    
    public WorkerStats GetStats() => new()
    {
        Name = Name,
        Received = Interlocked.Read(ref received),
        Completed = Interlocked.Read(ref completed),
        Retries = Interlocked.Read(ref retries),
        Failed = Interlocked.Read(ref failed),
        OfflineAt = apiProvider.OfflineDate,
        Running = Interlocked.Read(ref running) > 0,
    };

    public void Stop()
    {
        cts.Cancel();
    }
    
    bool ShouldStopRunning() => IsOffline || isDisposed || cts.IsCancellationRequested;

    public async Task ExecuteTasksAsync(ILogger log, IDbConnectionFactory dbFactory, IMessageProducer mq)
    {
        if (ShouldStopRunning())
            return;

        if (Interlocked.CompareExchange(ref running, 1, 0) != 0)
        {
            log.LogInformation("{Provider} is already running...", Name);
            return;
        }

        try
        {
            while (ChatQueue.Count > 0)
            {
                if (isDisposed || cts.IsCancellationRequested)
                    return;

                var completedTaskIds = new List<long>();
                using var db = await dbFactory.OpenDbConnectionAsync();
                while (!IsOffline && ChatQueue.TryTake(out var requestId))
                {
                    if (ShouldStopRunning())
                        return;

                    var chatTasks = await db.SelectAsync(db.From<OpenAiChatTask>().Where(x => x.RequestId == requestId && x.CompletedDate == null && x.ErrorCode == null));
                    var concurrentTasks = chatTasks.Select(x => ExecuteChatApiTaskAsync(log, mq, x));
                
                    completedTaskIds.AddRange((await Task.WhenAll(concurrentTasks)).Where(x => x.HasValue).Select(x => x!.Value));
                }
            
                if (ShouldStopRunning())
                    return;

                // See if there are any incomplete tasks for this provider
                var incompleteRequestIds = await db.ColumnDistinctAsync<string>(db.From<OpenAiChatTask>()
                    .Where(x => x.RequestId != null && x.CompletedDate == null && x.ErrorCode == null && x.Worker == Name
                                && !completedTaskIds.Contains(x.Id))
                    .Select(x => x.RequestId));
                if (incompleteRequestIds.Count > 0)
                {
                    log.LogWarning("Missed completing {Count} OpenAI Chat Tasks for {Provider}", incompleteRequestIds.Count, Name);
                    foreach (var requestId in incompleteRequestIds)
                    {
                        ChatQueue.Add(requestId);
                    }
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref running);
        }
    }
    
    async Task<long?> ExecuteChatApiTaskAsync(ILogger log, IMessageProducer mq, OpenAiChatTask task)
    {
        var chatProvider = GetOpenAiProvider();

            try
            {
                if (ShouldStopRunning())
                    return null;
                
                var (response, durationMs) = await chatProvider.ChatAsync(this, task.Request);

                Interlocked.Increment(ref completed);
                log.LogInformation("Completed {Provider} OpenAI Chat Task {Id} from {Request} in {Duration}ms", 
                    Name, task.Id, task.RequestId, durationMs);

                mq.Publish(new AppDbWrites {
                    CompleteOpenAiChat = new()
                    {
                        Id = task.Id,
                        Provider = Name,
                        DurationMs = durationMs,
                        Response = response,
                    },
                });

                if (task.ReplyTo != null)
                {
                    var json = response.ToJson();
                    mq.Publish(new NotificationTasks {
                        NotificationRequest = new() {
                            Url = task.ReplyTo,
                            ContentType = MimeTypes.Json,
                            Body = json,
                            CompleteNotification = new() {
                                Type = TaskType.OpenAiChat,
                                Id = task.Id,
                            },
                        },
                    });
                }
                return task.Id;
            }
            catch (Exception e)
            {
                Interlocked.Increment(ref failed);
                
                log.LogError(e, "Error executing {TaskId} OpenAI Chat Task for {Provider}: {Message}", 
                    task.Id, Name, e.Message);

                if (!await chatProvider.IsOnlineAsync(this))
                {
                    var offlineDate = DateTime.UtcNow;
                    IsOffline = true;
                    log.LogError("Provider {Name} has been taken offline", Name);
                    mq.Publish(new AppDbWrites {
                        RecordOfflineProvider = new() {
                            Name = Name,
                            OfflineDate = offlineDate,
                        }
                    });
                }
                else
                {
                    mq.Publish(new AppDbWrites {
                        FailOpenAiChat = new() {
                            Id = task.Id,
                            Provider = Name,
                            Error = e.ToResponseStatus(),
                        },
                    });
                }
                return null;
            }
    }

    public void Dispose()
    {
        isDisposed = true;
        cts.Cancel();
        cts.Dispose();
        ChatQueue.Dispose();
    }
}
