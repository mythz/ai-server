using Microsoft.Extensions.Logging;
using ServiceStack;

namespace AiServer.ServiceInterface.AppDb;

public class AppDbPeriodicTasksCommand(ILogger<AppDbPeriodicTasksCommand> log, AppData appData, ICommandExecutor executor) 
    : IAsyncCommand<PeriodicTasks>
{
    public async Task ExecuteAsync(PeriodicTasks request)
    {
        log.LogInformation("Executing {Type} {PeriodicFrequency} PeriodicTasks...", GetType().Name, request.PeriodicFrequency);

        if (request.PeriodicFrequency == PeriodicFrequency.Frequent)
        {
            await DoFrequentTasksAsync();
        }
    }

    async Task DoFrequentTasksAsync()
    {
        // Requeue incomplete tasks
        var requeueCommand = executor.Command<RequeueIncompleteTasksCommand>();
        await requeueCommand.ExecuteAsync(new RequeueIncompleteTasks());

        log.LogInformation("Requeued {Requeued} incomplete tasks", requeueCommand.Requeued);

        var delegateCommand = executor.Command<DelegateOpenAiChatTasksCommand>();
        await delegateCommand.ExecuteAsync(new DelegateOpenAiChatTasks());
        log.LogInformation("Delegated {Delegated} tasks", delegateCommand.DelegatedCount);
        
        // Check if any offline providers are back online
        var offlineApiProviders = appData.ApiProviders.Where(x => x is { Enabled: true, OfflineDate: not null }).ToList();
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
