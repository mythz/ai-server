using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.Messaging;
using ServiceStack.OrmLite;
using AiServer.ServiceInterface.Executor;
using AiServer.ServiceModel.Types;

namespace AiServer.ServiceInterface.AppDb;

public class DelegateOpenAiChatTasks {}
public class DelegateOpenAiChatTasksCommand(ILogger<DelegateOpenAiChatTasksCommand> log, AppData appData, 
    IDbConnectionFactory dbFactory, IMessageProducer mq) : IAsyncCommand<DelegateOpenAiChatTasks>
{
    private static long running = 0;
    public static bool Running => Interlocked.Read(ref running) > 0;

    private static long counter = 0;
    
    public int DelegatedCount { get; set; }
    
    public async Task ExecuteAsync(DelegateOpenAiChatTasks request)
    {
        if (Running)
            return;
        
        using var db = await dbFactory.OpenDbConnectionAsync();
        if (!await db.ExistsAsync(db.From<OpenAiChatTask>().Where(x => 
            x.RequestId == null && x.StartedDate == null && x.CompletedDate == null && appData.ActiveProviderModels.Contains(x.Model))))
        {
            return;
        }

        try
        {
            Interlocked.Increment(ref running);
            
            while (true)
            {
                foreach (var apiProvider in appData.ActiveProviders)
                {
                    var requestId = Guid.NewGuid().ToString("N");
                    var models = apiProvider.Models.Select(x => x.Model).ToArray();
                    var pendingTasks = await db.ReserveNextTasksAsync(
                        requestId: requestId,
                        models: models,
                        provider: apiProvider.Name,
                        take: apiProvider.Concurrency);

                    DelegatedCount += pendingTasks;
                    if (pendingTasks > 0)
                    {
                        log.LogDebug("{Counter} Reserved and delegating {PendingTasks} to {Provider}",
                            ++counter, pendingTasks, apiProvider.Name);
                        appData.EnqueueOpenAiChatTasks(apiProvider, requestId);
                    }
                }

                var hasWork = appData.OpenAiChatTasks.Any(x => x.Count > 0);
                if (hasWork)
                {
                    if (!ExecuteOpenAiChatTasksCommand.Running)
                    {
                        mq.Publish(new ExecutorTasks {
                            ExecuteOpenAiChatTasks = new()
                        });
                    }
                }
                break;
            }
        }
        finally
        {
            Interlocked.Decrement(ref running);
        }
    }
}
