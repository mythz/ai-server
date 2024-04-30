using Microsoft.Extensions.Logging;
using ServiceStack.Messaging;

namespace AiServer.ServiceInterface.AppDb;

public class AppDbPeriodicTasksCommand(ILogger<AppDbPeriodicTasksCommand> log, IMessageProducer mq, ICommandExecutor executor) : IAsyncCommand<PeriodicTasks>
{
    public async Task ExecuteAsync(PeriodicTasks request)
    {
        log.LogInformation("Executing {Type} {PeriodicFrequency} PeriodicTasks...", GetType().Name, request.PeriodicFrequency);

        if (request.PeriodicFrequency == PeriodicFrequency.Frequent)
        {
            var requeueCommand = executor.Command<RequeueIncompleteTasksCommand>();
            await requeueCommand.ExecuteAsync(new RequeueIncompleteTasks());

            log.LogInformation("Requeued {Requeued} incomplete tasks", requeueCommand.Requeued);
            if (requeueCommand.Requeued > 0)
            {
                var delegateCommand = executor.Command<DelegateOpenAiChatTasksCommand>();
                await delegateCommand.ExecuteAsync(new DelegateOpenAiChatTasks());
                log.LogInformation("Delegated {Delegated} tasks", delegateCommand.DelegatedCount);
            }
        }
    }
}
