using Microsoft.Extensions.Logging;
using ServiceStack;
using AiServer.ServiceInterface.Executor;
using AiServer.ServiceModel;
using AiServer.ServiceModel.Types;
using ServiceStack.Messaging;

namespace AiServer.ServiceInterface.AppDb;

public class AppDbPeriodicTasksCommand(ILogger<AppDbPeriodicTasksCommand> log, AppData appData, IMessageProducer mq, ICommandExecutor executor) 
    : IAsyncCommand<PeriodicTasks>
{
    public async Task ExecuteAsync(PeriodicTasks request)
    {
        log.LogInformation("Executing {Type} {PeriodicFrequency} PeriodicTasks...", GetType().Name, request.PeriodicFrequency);

        if (request.PeriodicFrequency == PeriodicFrequency.Frequent)
        {
            var allStats = appData.ApiProviderWorkers.Select(x => x.GetStats()).ToList();
            var allStatsTable = Inspect.dumpTable(allStats, new TextDumpOptions {
                Caption = "Worker Stats",
                Headers = [
                    nameof(WorkerStats.Name),
                    nameof(WorkerStats.Received),
                    nameof(WorkerStats.Completed),
                    nameof(WorkerStats.Retries),
                    nameof(WorkerStats.Failed),
                    nameof(WorkerStats.OfflineAt),
                    nameof(WorkerStats.Running),
                ],
            }).Trim();
            log.LogInformation("ApiProvider:\n{Stats}\n", allStatsTable);
            log.LogInformation("DelegateOpenAiChatTasks: {Running}", DelegateOpenAiChatTasksCommand.Running);
            log.LogInformation("ExecuteOpenAiChatTasksCommand: {Running}", ExecuteOpenAiChatTasksCommand.Running);
            
            await DoFrequentTasksAsync();
        }
    }

    async Task DoFrequentTasksAsync()
    {
        // Requeue incomplete tasks
        var requeueCommand = executor.Command<RequeueIncompleteTasksCommand>();
        await requeueCommand.ExecuteAsync(new RequeueIncompleteTasks());

        log.LogInformation("Requeued {Requeued} incomplete tasks", requeueCommand.Requeued);

        mq.Publish(new QueueTasks {
            DelegateOpenAiChatTasks = new()
        });
        
        // Check if any offline providers are back online
        var offlineApiProviders = appData.ApiProviderWorkers.Where(x => x is { Enabled:true, IsOffline:true }).ToList();
        if (offlineApiProviders.Count > 0)
        {
            log.LogInformation("Rechecking {OfflineCount} offline providers", offlineApiProviders.Count);
            foreach (var apiProvider in offlineApiProviders)
            {
                var chatProvider = apiProvider.GetOpenAiProvider();
                if (await chatProvider.IsOnlineAsync(apiProvider))
                {
                    log.LogInformation("Provider {Provider} is back online", apiProvider.Name);
                    var changeStatusCommand = executor.Command<ChangeProviderStatusCommand>();
                    await changeStatusCommand.ExecuteAsync(new() {
                        Name = apiProvider.Name,
                        OfflineDate = null,
                    });
                }
            }
        }
    }
}
