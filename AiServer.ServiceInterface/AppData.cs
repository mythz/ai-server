using System.Collections.Concurrent;
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

    public HashSet<string> ActiveProviderModels { get; set; } = new(); 
    public ApiProvider[] ActiveProviders { get; set; }
    public ApiProvider[] ApiProviders { get; set; }
    public BlockingCollection<string>[] OpenAiChatTasks { get; set; }

    public void ResetInitialChatTaskId(IDbConnection db)
    {
        var maxId = db.Scalar<long>($"SELECT MAX(Id) FROM {nameof(TaskSummary)}");
        SetInitialChatTaskId(maxId);
    }

    public void ResetApiProviders(IDbConnection db)
    {
        var apiProviders = db.LoadSelect<ApiProvider>().OrderByDescending(x => x.Priority).ThenBy(x => x.Id).ToArray();
        var activeProviders = apiProviders.Where(x => x is { Enabled: true, Concurrency: > 0 }).ToArray();
        var activeProviderModels = activeProviders.SelectMany(x => x.Models.Select(m => m.Model)).ToSet();
        var openAiChatTasks = apiProviders.Select(_ => new BlockingCollection<string>()).ToArray();
        lock (Instance)
        {
            ApiProviders = apiProviders;
            ActiveProviders = activeProviders;
            ActiveProviderModels = activeProviderModels;
            OpenAiChatTasks = openAiChatTasks;
        }
    }

    public void Init(IDbConnection db)
    {
        ResetInitialChatTaskId(db);
        ResetApiProviders(db);
    }

    public void EnqueueOpenAiChatTasks(ApiProvider apiProvider, string requestId)
    {
        var idx = Array.IndexOf(ApiProviders, apiProvider);
        OpenAiChatTasks[idx].Add(requestId);
    }
}
