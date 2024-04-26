using System.Data;
using AiServer.ServiceModel;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface;

public class AppData
{
    public static AppData Instance { get; } = new();

    private long nextChatTaskId = -1;
    public void SetInitialChatTaskId(long initialValue) => this.nextChatTaskId = initialValue;
    public long LastChatTaskId => Interlocked.Read(ref nextChatTaskId);
    public long GetNextChatTaskId() => Interlocked.Increment(ref nextChatTaskId);

    public void ResetInitialChatTaskId(IDbConnection db)
    {
        var maxId = db.Scalar<long>($"SELECT MAX(Id) FROM {nameof(OpenAiChatTask)}");
        SetInitialChatTaskId(maxId);
    }

    public void Init(IDbConnection db)
    {
        ResetInitialChatTaskId(db);
    }
}