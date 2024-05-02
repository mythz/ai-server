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
            var requestId = Guid.NewGuid().ToString("N");
            
            var rowsUpdated = await db.ReserveNextTasksAsync(requestId, worker.Models, worker.Name, worker.Concurrency);
            if (rowsUpdated == 0)
            {
                log.LogInformation("No tasks available to reserve for {Provider}, exiting...", worker.Name);
                return;
            }
            
            assigned += rowsUpdated;
            worker.AddToChatQueue(requestId);
        }

        log.LogInformation("Assigned {Assigned} tasks to {Provider}", assigned, worker.Name);
    }
}