using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Messaging;
using AiServer.ServiceInterface.AppDb;

namespace AiServer.ServiceInterface.Executor;

public class ExecutorPeriodicTasksCommand(ILogger<AppDbPeriodicTasksCommand> log, AppData appData, IMessageProducer mq) 
    : IAsyncCommand<PeriodicTasks>
{
    public async Task ExecuteAsync(PeriodicTasks request)
    {
        log.LogInformation("Executing {Type} {PeriodicFrequency} PeriodicTasks...", 
            GetType().Name, request.PeriodicFrequency);
    }
}
