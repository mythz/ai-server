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
    public static long running = 0;
    public static bool Running => Interlocked.Read(ref running) > 0;
    private static long counter = 0;
    
    public async Task ExecuteAsync(ExecuteTasks request)
    {
        if (Interlocked.CompareExchange(ref running, 1, 0) == 0)
        {
            try
            {
                var pendingTasks = appData.ChatTasksQueuedCount();
                while (pendingTasks > 0)
                {
                    log.LogInformation("Executing {QueuedCount} queued OpenAI Chat Tasks...", pendingTasks);

                    var runningTasks = new List<Task>();

                    foreach (var worker in appData.ActiveWorkers)
                    {
                        if (worker.IsOffline) continue;
                        
                        if (worker.ChatQueueCount > 0)
                        {
                            log.LogInformation("{Counter} Executing {Count} OpenAI Chat Tasks for {Provider}", 
                                ++counter, worker.ChatQueueCount, worker.Name);
                            runningTasks.Add(worker.ExecuteTasksAsync(log, dbFactory, mq));
                        }
                    }

                    await Task.WhenAll(runningTasks);

                    pendingTasks = appData.ChatTasksQueuedCount();
                    if (pendingTasks == 0)
                    {
                        log.LogInformation("No more queued OpenAI Chat Tasks left to execute, exiting...");
                        break;
                    }
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
}

