using System.Data;
using ServiceStack;
using ServiceStack.OrmLite;
using AiServer.ServiceModel;
using AiServer.ServiceModel.Types;

namespace AiServer.ServiceInterface.AppDb;

public class CompleteOpenAiChatCommand(IDbConnection db) : IAsyncCommand<CompleteOpenAiChat>
{
    public async Task ExecuteAsync(CompleteOpenAiChat request)
    {
        await db.UpdateOnlyAsync(() => new OpenAiChatTask
        {
            Provider = request.Provider,
            DurationMs = request.DurationMs,
            Response = request.Response,
            CompletedDate = DateTime.UtcNow,
        }, where: x => x.Id == request.Id);
    }
}