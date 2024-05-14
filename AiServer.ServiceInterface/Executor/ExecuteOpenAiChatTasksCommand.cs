using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.Messaging;

namespace AiServer.ServiceInterface.Executor;

public class ExecuteTasks {}
public class ExecuteOpenAiChatTasksCommand(ILogger<ExecuteOpenAiChatTasksCommand> log, AppData appData, 
    IDbConnectionFactory dbFactory, IMessageProducer mq) 
    : IAsyncCommand<ExecuteTasks>
{
    private static long running = 0;
    public static bool Running => Interlocked.Read(ref running) > 0;
    private static long counter = 0;

    public static bool ShouldContinueRunning => Running && AppData.Instance.HasAnyChatTasksQueued();
    
    public async Task ExecuteAsync(ExecuteTasks request)
    {
        if (Interlocked.CompareExchange(ref running, 1, 0) == 0)
        {
            try
            {
                while (true)
                {
                    if (appData.IsStopped)
                        return;
                
                    var pendingTasks = appData.ChatTasksQueuedCount();
                    log.LogInformation("[Chat] Executing {QueuedCount} queued tasks...", pendingTasks);

                    var runningTasks = new List<Task>();

                    foreach (var worker in appData.GetActiveWorkers())
                    {
                        if (appData.IsStopped)
                            return;
                
                        if (worker.IsOffline) 
                            continue;

                        log.LogInformation("[Chat][{Provider}] {Counter} Executing {Count} Tasks",
                            worker.Name, ++counter, worker.ChatQueueCount);
                        runningTasks.Add(worker.ExecuteTasksAsync(log, dbFactory, mq));
                    }

                    await Task.WhenAll(runningTasks);

                    if (!appData.HasAnyChatTasksQueued())
                    {
                        log.LogInformation("[Chat] No more queued tasks left to execute, exiting...");
                        break;
                    }
                }
            }
            catch (TaskCanceledException)
            {
                log.LogInformation("[Chat] Executing tasks was cancelled, exiting...");
            }
            catch (Exception ex)
            {
                log.LogError(ex, "[Chat] Error executing tasks, exiting...");
            }
            finally
            {
                Interlocked.Decrement(ref running);
            }
        }
    }
}

