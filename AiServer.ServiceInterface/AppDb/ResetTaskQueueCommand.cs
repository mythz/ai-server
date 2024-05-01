using System.Data;
using ServiceStack;
using ServiceStack.Messaging;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface.AppDb;

public class ResetTaskQueue {}
public class ResetTaskQueueCommand(IDbConnection db, IMessageProducer mq) : IAsyncCommand<ResetTaskQueue>
{
    public long Reset { get; set; }
    
    public async Task ExecuteAsync(ResetTaskQueue request)
    {
        Reset = await db.ExecuteSqlAsync(
            "UPDATE OpenAiChatTask SET RequestId = NULL, StartedDate = NULL, Worker = NULL, WorkerIp = NULL WHERE CompletedDate IS NULL");
        
        mq.Publish(new AppDbWrites {
            DelegateOpenAiChatTasks = new()
        });
        mq.Publish(new ExecutorTasks {
            ExecuteOpenAiChatTasks = new()
        });
    }
}
