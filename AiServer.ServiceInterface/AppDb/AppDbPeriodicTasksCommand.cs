using Microsoft.Extensions.Logging;
using ServiceStack;
using AiServer.ServiceInterface.Executor;
using AiServer.ServiceInterface.Queue;
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

        if (request.PeriodicFrequency == PeriodicFrequency.Minute)
        {
            var allStats = appData.ApiProviderWorkers.Select(x => x.GetStats()).ToList();
            var allStatsTable = Inspect.dumpTable(allStats, new TextDumpOptions {
                Caption = "Worker Stats",
                Headers = [
                    nameof(WorkerStats.Name),
                    nameof(WorkerStats.Queued),
                    nameof(WorkerStats.Received),
                    nameof(WorkerStats.Completed),
                    nameof(WorkerStats.Retries),
                    nameof(WorkerStats.Failed),
                    nameof(WorkerStats.Offline),
                    nameof(WorkerStats.Running),
                ],
            }).Trim();

            log.LogInformation("""
                               ApiProvider:
                               {Stats}

                               Workers: {WorkerStatus} 
                               Delegating: {Delegating}
                               Executing: {Executing}
                               """, 
                allStatsTable,
                appData.StoppedAt == null ? "Running" : $"Stopped at {appData.StoppedAt}",
                DelegateOpenAiChatTasksCommand.Running,
                ExecuteOpenAiChatTasksCommand.Running);
            
            await DoFrequentTasksAsync(request.PeriodicFrequency);
        }
    }

    async Task DoFrequentTasksAsync(PeriodicFrequency frequency)
    {
        // Requeue incomplete tasks
        var requeueCommand = executor.Command<RequeueIncompleteTasksCommand>();
        await requeueCommand.ExecuteAsync(new RequeueIncompleteTasks());

        log.LogInformation("[{Frequency}] Requeued {Requeued} incomplete tasks", frequency, requeueCommand.Requeued);

        mq.Publish(new QueueTasks {
            DelegateOpenAiChatTasks = new()
        });
        
        // Check if any offline providers are back online
        var offlineApiProviders = appData.ApiProviderWorkers.Where(x => x is { Enabled:true, IsOffline:true }).ToList();
        if (offlineApiProviders.Count > 0)
        {
            log.LogInformation("[{Frequency}] Rechecking {OfflineCount} offline providers", frequency, offlineApiProviders.Count);
            foreach (var apiProvider in offlineApiProviders)
            {
                var chatProvider = apiProvider.GetOpenAiProvider();
                if (await chatProvider.IsOnlineAsync(apiProvider))
                {
                    log.LogInformation("[{Frequency}] Provider {Provider} is back online", frequency, apiProvider.Name);
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
