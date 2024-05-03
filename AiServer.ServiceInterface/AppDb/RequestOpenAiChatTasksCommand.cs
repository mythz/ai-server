using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface.AppDb;

public class RequestOpenAiChatTasks
{
    public string Provider { get; set; }
    public int Count { get; set; }
}

public class RequestOpenAiChatTasksCommand(ILogger<RequestOpenAiChatTasksCommand> log, AppData appData, IDbConnectionFactory dbFactory) 
    : IAsyncCommand<RequestOpenAiChatTasks>
{
    public async Task ExecuteAsync(RequestOpenAiChatTasks request)
    {
        var worker = appData.ApiProviderWorkers.FirstOrDefault(x => x.Name == request.Provider) 
                     ?? throw new ArgumentNullException(nameof(request.Provider));

        using var db = await dbFactory.OpenDbConnectionAsync();

        var assigned = 0; 
        for (int i = 0; i < request.Count; i++)
        {
            var requestId = appData.CreateRequestId();
            var rowsUpdated = await db.ReserveNextTasksAsync(requestId, worker.Models, worker.Name, worker.Concurrency);
            if (rowsUpdated == 0)
            {
                log.LogDebug("[{Provider}] No tasks available to reserve, exiting...", worker.Name);
                return;
            }
            
            assigned += rowsUpdated;
            worker.AddToChatQueue(requestId);
        }

        log.LogInformation("[{Provider}] Assigned {Assigned} tasks", assigned, worker.Name);
    }
}