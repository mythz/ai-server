using Microsoft.Extensions.Logging;
using ServiceStack.Messaging;
using AiServer.ServiceInterface.AppDb;

namespace AiServer.ServiceInterface.Executor;

public class ExecutorPeriodicTasksCommand(ILogger<AppDbPeriodicTasksCommand> log, AppData appData, IMessageProducer mq) : IAsyncCommand<PeriodicTasks>
{
    public async Task ExecuteAsync(PeriodicTasks request)
    {
        log.LogInformation("Executing {Type} {PeriodicFrequency} PeriodicTasks...", 
            GetType().Name, request.PeriodicFrequency);

        if (request.PeriodicFrequency == PeriodicFrequency.Frequent)
        {
            var offlineApiProviders = appData.ApiProviders.Where(x => x is { Enabled: true, OfflineDate: not null }).ToList();
            if (offlineApiProviders.Count == 0)
                return;

            log.LogInformation("Rechecking {OfflineCount} offline providers", offlineApiProviders.Count);
            foreach (var apiProvider in offlineApiProviders)
            {
                var chatProvider = apiProvider.GetOpenAiProvider();
                if (await chatProvider.IsOnlineAsync(apiProvider))
                {
                    log.LogInformation("Provider {Provider} is back online", apiProvider.Name);
                    apiProvider.OfflineDate = null;
                    mq.Publish(new AppDbWrites {
                        RecordOfflineProvider = new()
                        {
                            Name = apiProvider.Name,
                            OfflineDate = null,
                        }
                    });
                }
            }
        }
    }
}
