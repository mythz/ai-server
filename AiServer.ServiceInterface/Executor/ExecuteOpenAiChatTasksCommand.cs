using System.Collections.Concurrent;
using AiServer.ServiceModel;
using AiServer.ServiceModel.Types;
using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.Messaging;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface.Executor;

public class ExecuteTasks {}

public class ExecuteOpenAiChatTasksCommand(ILogger<ExecuteOpenAiChatTasksCommand> log, AppData appData, 
    IDbConnectionFactory dbFactory, IMessageProducer mq) 
    : IAsyncCommand<ExecuteTasks>
{
    public static long running = 0;
    public static bool Running => Interlocked.Read(ref running) > 0;
    private static long counter = 0;
    
    public async Task ExecuteAsync(ExecuteTasks request)
    {
        if (Interlocked.CompareExchange(ref running, 1, 0) == 0)
        {
            try
            {
                while (true)
                {
                    var runningTasks = new List<Task>();

                    for (int i = 0; i < appData.OpenAiChatTasks.Length; i++)
                    {
                        var apiProvider = appData.ApiProviders[i];
                        if (apiProvider.OfflineDate != null) continue;
                        
                        var queue = appData.OpenAiChatTasks[i];
                        if (queue.Count > 0)
                        {
                            log.LogDebug("{Counter} Executing {Count} OpenAI Chat Tasks for {Provider}", 
                                ++counter, queue.Count, apiProvider.Name);
                            runningTasks.Add(ExecuteTask(apiProvider, queue));
                        }
                    }

                    await Task.WhenAll(runningTasks);
                    
                    if (appData.OpenAiChatTasks.All(x => x.Count == 0))
                        break;
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error executing tasks");
            }
            finally
            {
                Interlocked.Decrement(ref running);
            }
        }
    }

    async Task ExecuteChatApiTaskAsync(ApiProvider apiProvider, OpenAiChatTask task)
    {
        var chatProvider = apiProvider.GetOpenAiProvider();

        var retry = 0;
        while (retry++ < 2)
        {
            try
            {
                var (response, durationMs) = await chatProvider.ChatAsync(apiProvider , task.Request);

                log.LogDebug("Completed {Provider} OpenAI Chat Task {Id} in {Duration}ms", apiProvider.Name, task.Id, durationMs);

                mq.Publish(new AppDbWrites {
                    CompleteOpenAiChat = new()
                    {
                        Id = task.Id,
                        Provider = apiProvider.Name,
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
                return;
            }
            catch (Exception e)
            {
                log.LogError(e, "{Retry}x Error executing {TaskId} OpenAI Chat Task for {Provider}: {Message}", 
                    retry, task.Id, apiProvider.Name, e.Message);
                await Task.Delay(200);
            }
        }

        if (!await chatProvider.IsOnlineAsync(apiProvider))
        {
            var offlineDate = DateTime.UtcNow;
            apiProvider.OfflineDate = offlineDate;
            log.LogError("Provider {Provider} has been taken offline", apiProvider.Name);
            mq.Publish(new AppDbWrites {
                RecordOfflineProvider = new() {
                    Name = apiProvider.Name,
                    OfflineDate = offlineDate,
                }
            });
        }
    }
        
    public async Task ExecuteTask(ApiProvider apiProvider, BlockingCollection<string> requestIds)
    {
        while (requestIds.Count > 0)
        {
            var completedRequestIds = new HashSet<string>();
            using var db = await dbFactory.OpenDbConnectionAsync();
            while (apiProvider.OfflineDate == null && requestIds.TryTake(out var requestId))
            {
                completedRequestIds.Add(requestId);
                var chatTasks = await db.SelectAsync(db.From<OpenAiChatTask>().Where(x => x.RequestId == requestId && x.CompletedDate == null && x.ErrorCode == null));
                var concurrentTasks = chatTasks.Select(x => ExecuteChatApiTaskAsync(apiProvider, x));
                await Task.WhenAll(concurrentTasks);
            }

            // See if there are any incomplete tasks from the list of processed request ids
            var incompleteRequestIds = await db.ColumnDistinctAsync<string>(db.From<OpenAiChatTask>()
                .Where(x => x.RequestId != null && x.CompletedDate == null && x.ErrorCode == null && 
                            (completedRequestIds.Contains(x.RequestId) || x.Worker == apiProvider.Name))
                .Select(x => x.RequestId));
            if (incompleteRequestIds.Count > 0)
            {
                log.LogWarning("Missed completing {Count} OpenAI Chat Tasks for {Provider}", incompleteRequestIds.Count, apiProvider.Name);
                foreach (var requestId in incompleteRequestIds)
                {
                    requestIds.Add(requestId);
                }
            }
        }
    }
}
