using System.Data;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface;

public class RequeueIncompleteTasks {}

public class RequeueIncompleteTasksCommand(IDbConnection db) : IAsyncCommand<RequeueIncompleteTasks>
{
    public async Task ExecuteAsync(RequeueIncompleteTasks request)
    {
        var threshold = DateTime.UtcNow.AddMinutes(-5);
        await db.ExecuteSqlAsync(
            "UPDATE OpenAiChatTask SET RequestId = NULL, StartedDate = NULL WHERE CompletedDate IS NULL AND StartedDate < @threshold",
            new { threshold });
    }
}
