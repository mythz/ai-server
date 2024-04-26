using System.Data;
using Microsoft.Extensions.Logging;
using ServiceStack.Messaging;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface.Commands;

public class ReserveOpenAiChatTask
{
    public string RequestId { get; set; }
    public string[] Models { get; set; }
    public string? Provider { get; set; }
    public int Take { get; set; }
}

public class ReserveOpenAiChatTaskCommand(ILogger<ReserveOpenAiChatTaskCommand> log, IDbConnection db, IMessageProducer mq) 
    : IAsyncCommand<ReserveOpenAiChatTask>
{
    private static long counter;
    public static long Counter => Interlocked.Read(ref counter);
    
    public async Task ExecuteAsync(ReserveOpenAiChatTask request)
    {
        var sql = """
          UPDATE OpenAiChatTask SET RequestId = @RequestId, StartedDate = @StartedDate
          WHERE Id IN
              (SELECT Id
                FROM OpenAiChatTask
                WHERE RequestId is null AND StartedDate is null
                AND Model IN (@Models)
                AND (Provider IS NULL OR Provider = @Provider)
                ORDER BY Id
                LIMIT @Take)
          """;

        var rowsUpdated = await db.ExecuteSqlAsync(sql,
            new { request.RequestId, StartedDate = DateTime.UtcNow, request.Models, request.Provider, request.Take });
        if (rowsUpdated == 0)
        {
            log.LogWarning("Could not find {Take} available tasks for {Model} with {Provider} for Request {RequestId}", 
                request.Take, request.Models, request.Provider ?? "no provider", request.RequestId);
            return;
        }

        if (Interlocked.Increment(ref counter) % 10 == 0)
        {
            mq.Publish(new AppDbWrites {
                RequeueIncompleteTasks = new RequeueIncompleteTasks(),
            });
        }
    }
}
