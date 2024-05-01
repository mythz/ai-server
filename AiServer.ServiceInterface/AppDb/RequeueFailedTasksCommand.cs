using AiServer.ServiceModel.Types;
using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.Messaging;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface.AppDb;

public class RequeueFailedTasks
{
    public List<long> Ids { get; set; }
}

public class RequeueFailedTasksCommand(ILogger<RequeueFailedTasksCommand> log, 
    IDbConnectionFactory dbFactory, IMessageProducer mq) : IAsyncCommand<RequeueFailedTasks>
{
    public long Requeued { get; set; }
    
    public async Task ExecuteAsync(RequeueFailedTasks request)
    {
        using var monthDb = dbFactory.GetMonthDbConnection();
        var failedTasks = await monthDb.SelectAsync(monthDb.From<OpenAiChatFailed>().Where(x => request.Ids.Contains(x.Id)));
        if (failedTasks.Count == 0)
            return;
        
        var requeuedTasks = failedTasks.Map(x =>
        {
            var to = x.ConvertTo<OpenAiChatTask>();
            to.StartedDate = x.CompletedDate = null;
            to.Error = null;
            to.ErrorCode = null;
            to.Retries = 0;
            return to;
        });

        Requeued = requeuedTasks.Count;
        
        using var db = dbFactory.OpenDbConnection();
        await db.InsertAllAsync(requeuedTasks);

        await monthDb.DeleteAsync(monthDb.From<OpenAiChatFailed>().Where(x => request.Ids.Contains(x.Id)));
        
        log.LogInformation("Requeued {Requeued} failed tasks: {Ids}", Requeued, string.Join(", ", request.Ids));

        mq.Publish(new QueueTasks {
            DelegateOpenAiChatTasks = new()
        });
        mq.Publish(new ExecutorTasks {
            ExecuteOpenAiChatTasks = new()
        });
    }
}
