using System.Collections.Concurrent;
using System.Diagnostics;
using AiServer.ServiceModel;
using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.Messaging;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface.Executor;

public class ExecuteOpenAiChatTasksCommand(ILogger<ExecuteOpenAiChatTasksCommand> log, AppData appData, IDbConnectionFactory dbFactory, IMessageProducer mq) 
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
                        var queue = appData.OpenAiChatTasks[i];
                        if (queue.Count > 0)
                        {
                            var apiProvider = appData.ApiProviders[i];
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
        try
        {
            var chatProvider = apiProvider.GetOpenAiProvider();
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
        }
        catch (Exception e)
        {
            log.LogError(e, "Error executing {TaskId} OpenAI Chat Task for {Provider}: {Message}", 
                task.Id, apiProvider.Name, e.Message);
        }
    }
        
    public async Task ExecuteTask(ApiProvider apiProvider, BlockingCollection<string> requestIds)
    {
        using var db = await dbFactory.OpenDbConnectionAsync();
        while (requestIds.TryTake(out var requestId))
        {
            var chatTasks = await db.SelectAsync(db.From<OpenAiChatTask>().Where(x => x.RequestId == requestId));
            var concurrentTasks = chatTasks.Select(x => ExecuteChatApiTaskAsync(apiProvider, x));
            await Task.WhenAll(concurrentTasks);
        }
    }
}
