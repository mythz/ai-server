using System.Data;
using AiServer.ServiceModel;
using AiServer.ServiceModel.Types;
using ServiceStack;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface.AppDb;

public class FailOpenAiChatCommand(IDbConnection db) : IAsyncCommand<FailOpenAiChat>
{
    public async Task ExecuteAsync(FailOpenAiChat request)
    {
        await db.UpdateAddAsync(() => new OpenAiChatTask
        {
            Provider = request.Provider,
            ErrorCode = request.Error.ErrorCode,
            Error = request.Error,
            Retries = 1,
        }, where: x => x.Id == request.Id);
        
        var task = await db.SingleByIdAsync<OpenAiChatTask>(request.Id);
        if (task.Retries >= 3)
        {
            await db.InsertAsync(task.ToOpenAiChatFailed());
            await db.DeleteByIdAsync<OpenAiChatTask>(request.Id);
        }
    }
}