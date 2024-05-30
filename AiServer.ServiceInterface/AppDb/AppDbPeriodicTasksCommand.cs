using Microsoft.Extensions.Logging;
using ServiceStack;
using AiServer.ServiceInterface.Executor;
using AiServer.ServiceInterface.Queue;
using AiServer.ServiceModel;
using AiServer.ServiceModel.Types;
using ServiceStack.Messaging;

namespace AiServer.ServiceInterface.AppDb;

[Tag(Tags.Database)]
public class AppDbPeriodicTasksCommand(ILogger<AppDbPeriodicTasksCommand> log, AppData appData, IMessageProducer mq, ICommandExecutor executor) 
    : IAsyncCommand<PeriodicTasks>
{
    public async Task ExecuteAsync(PeriodicTasks request)
    {
        log.LogInformation("Executing {Type} {PeriodicFrequency} PeriodicTasks...", GetType().Name, request.PeriodicFrequency);

        if (request.PeriodicFrequency == PeriodicFrequency.Minute)
        {
            var activeWorkers = appData.GetActiveWorkers().ToList();
            var allStats = activeWorkers.Select(x => x.GetStats()).ToList();
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
            var offlineWorkers = appData.ApiProviders.Where(x => x is { Enabled: true, OfflineDate: not null }).Map(x => x.Name);
            var disabledWorkers = appData.ApiProviders.Where(x => 
                    activeWorkers.All(a => a.Name != x.Name) && !offlineWorkers.Contains(x.Name))
                .Map(x => x.Name); 

            log.LogInformation("""
                               Workers:
                               {Stats}
                               
                               Offline:    {Offline}
                               Disabled:   {Disabled}
                               
                               Delegating: {Delegating}
                               Executing:  {Executing}
                               """, 
                appData.StoppedAt == null ? allStatsTable : $"Stopped at {appData.StoppedAt}",
                offlineWorkers.IsEmpty() ? "None" : offlineWorkers.Join(", "),
                disabledWorkers.IsEmpty() ? "None" : disabledWorkers.Join(", "),
                DelegateOpenAiChatTasksCommand.Running,
                ExecuteOpenAiChatTasksCommand.Running);
            
            if (request.PeriodicFrequency == PeriodicFrequency.Minute)
                await DoFrequentTasksAsync();
        }
    }

    async Task DoFrequentTasksAsync()
    {
        try
        {
            var frequency = PeriodicFrequency.Minute;
            var token = appData.Token;
            if (appData.IsStopped)
                return;

            // Requeue incomplete tasks
            var requeueCommand = executor.Command<RequeueIncompleteTasksCommand>();
            await requeueCommand.ExecuteAsync(new RequeueIncompleteTasks());

            if (appData.IsStopped)
                return;
            log.LogInformation("[{Frequency}] Requeued {Requeued} incomplete tasks", frequency, requeueCommand.Requeued);

            mq.Publish(new QueueTasks {
                DelegateOpenAiChatTasks = new()
            });
            
            mq.Publish(new NotificationTasks {
                SendPendingNotifications = new()
            });
        
            // Check if any offline providers are back online
            var offlineApiProviders = appData.ApiProviderWorkers.Where(x => x is { Enabled:true, IsOffline:true }).ToList();
            if (offlineApiProviders.Count > 0)
            {
                log.LogInformation("[{Frequency}] Rechecking {OfflineCount} offline providers", frequency, offlineApiProviders.Count);
                foreach (var apiProvider in offlineApiProviders)
                {
                    var chatProvider = apiProvider.GetOpenAiProvider();
                    if (await chatProvider.IsOnlineAsync(apiProvider, token))
                    {
                        if (appData.IsStopped)
                            return;

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
        catch (TaskCanceledException) {}
    }
}
