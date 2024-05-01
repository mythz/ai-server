using System.Data;
using AiServer.ServiceModel;
using ServiceStack;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface;

public class AppData
{
    public static AppData Instance { get; } = new();

    private long nextChatTaskId = -1;
    public void SetInitialChatTaskId(long initialValue) => this.nextChatTaskId = initialValue;
    public long LastChatTaskId => Interlocked.Read(ref nextChatTaskId);
    public long GetNextChatTaskId() => Interlocked.Increment(ref nextChatTaskId);

    public ApiProviderWorker[] ApiProviderWorkers { get; set; } = [];
    public ApiProvider[] ApiProviders { get; set; } = [];
    public IEnumerable<ApiProviderWorker> ActiveWorkers => ApiProviderWorkers.Where(x => x is { Enabled: true, Concurrency: > 0 });
    public HashSet<string> ActiveWorkerModels => ActiveWorkers.SelectMany(x => x.Models).ToSet();

    public void ResetInitialChatTaskId(IDbConnection db)
    {
        var maxId = db.Scalar<long>($"SELECT MAX(Id) FROM {nameof(TaskSummary)}");
        SetInitialChatTaskId(maxId);
    }

    public void ResetApiProviders(IDbConnection db)
    {
        foreach (var worker in ApiProviderWorkers)
        {
            worker.Dispose();
        }
        
        var apiProviders = db.LoadSelect<ApiProvider>().OrderByDescending(x => x.Priority).ThenBy(x => x.Id).ToArray();
        var workers = apiProviders.Select(x => new ApiProviderWorker(x)).ToArray();
        lock (Instance)
        {
            ApiProviders = apiProviders;
            ApiProviderWorkers = workers;
        }
    }

    public void Init(IDbConnection db)
    {
        ResetInitialChatTaskId(db);
        ResetApiProviders(db);
    }

    public bool HasAnyChatTasksQueued() => ActiveWorkers.Any(x => x.ChatQueueCount > 0);
    public int ChatTasksQueuedCount() => ActiveWorkers.Sum(x => x.ChatQueueCount);
}
