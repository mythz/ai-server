using System.Data;
using ServiceStack;
using ServiceStack.Messaging;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface.AppDb;

public class RequeueIncompleteTasks {}
public class RequeueIncompleteTasksCommand(IDbConnection db, IMessageProducer mq) : IAsyncCommand<RequeueIncompleteTasks>
{
    public long Requeued { get; set; }
    
    public async Task ExecuteAsync(RequeueIncompleteTasks request)
    {
        var threshold = DateTime.UtcNow.AddMinutes(-5);
        Requeued = await db.ExecuteSqlAsync(
            "UPDATE OpenAiChatTask SET RequestId = NULL, StartedDate = NULL, Worker = NULL, WorkerIp = NULL WHERE CompletedDate IS NULL AND Retries < 3 AND StartedDate < @threshold",
            new { threshold });
        
        mq.Publish(new AppDbWrites {
            DelegateOpenAiChatTasks = new()
        });
        mq.Publish(new ExecutorTasks {
            ExecuteOpenAiChatTasks = new()
        });
    }
}