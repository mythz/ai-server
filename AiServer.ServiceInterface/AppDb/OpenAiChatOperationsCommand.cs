using System.Data;
using ServiceStack;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface.AppDb;

public class RequeueIncompleteTasks {}

public class RequeueIncompleteTasksCommand(IDbConnection db) : IAsyncCommand<RequeueIncompleteTasks>
{
    public long Requeued { get; set; }
    
    public async Task ExecuteAsync(RequeueIncompleteTasks request)
    {
        var threshold = DateTime.UtcNow.AddMinutes(-5);
        Requeued = await db.ExecuteSqlAsync(
            "UPDATE OpenAiChatTask SET RequestId = NULL, StartedDate = NULL WHERE CompletedDate IS NULL AND Retries < 3 AND StartedDate < @threshold",
            new { threshold });
    }
}
