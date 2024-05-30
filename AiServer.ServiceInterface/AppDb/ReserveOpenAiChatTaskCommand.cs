using System.Data;
using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Messaging;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface.AppDb;

public class ReserveOpenAiChatTask
{
    public string RequestId { get; set; }
    public string[] Models { get; set; }
    public string? Provider { get; set; }
    public int Take { get; set; }
}

[Tag(Tags.OpenAiChat)]
public class ReserveOpenAiChatTaskCommand(ILogger<ReserveOpenAiChatTaskCommand> log, IDbConnection db, IMessageProducer mq) 
    : IAsyncCommand<ReserveOpenAiChatTask>
{
    private static long counter;
    public static long Counter => Interlocked.Read(ref counter);
    
    public async Task ExecuteAsync(ReserveOpenAiChatTask request)
    {
        var rowsUpdated = await db.ReserveNextTasksAsync(request.RequestId, request.Models, request.Provider, request.Take);
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

public static class ReserveOpenAiChatTaskCommandExtensions
{
    public static async Task<int> ReserveNextTasksAsync(this IDbConnection db, 
        string requestId, string[] models, string? provider=null, int take=1, string? workerIp = null)
    {
        var startedDate = DateTime.UtcNow;
        var sql = """
                  UPDATE OpenAiChatTask SET RequestId = @requestId, StartedDate = @startedDate, Worker = @provider, WorkerIp = @workerIp
                  WHERE Id IN
                      (SELECT Id
                        FROM OpenAiChatTask
                        WHERE RequestId IS NULL AND StartedDate IS NULL AND CompletedDate IS NULL
                        AND Model IN (@models)
                        AND (Provider IS NULL OR Provider = @provider)
                        ORDER BY Id
                        LIMIT @take)
                  """;

        var rowsUpdated = await db.ExecuteSqlAsync(sql,
            new { requestId, startedDate, models, provider, take, workerIp });
        return rowsUpdated;
    }
}
