using AiServer.ServiceModel;
using AiServer.ServiceModel.Types;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface.AppDb;

public class FailOpenAiChatCommand(IDbConnectionFactory dbFactory) : IAsyncCommand<FailOpenAiChat>
{
    public async Task ExecuteAsync(FailOpenAiChat request)
    {
        using var db = dbFactory.OpenDbConnection(); 
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
            using var dbMonth = dbFactory.GetMonthDbConnection(task.CreatedDate);
            await dbMonth.InsertAsync(task.ToOpenAiChatFailed());
            
            await db.DeleteByIdAsync<OpenAiChatTask>(request.Id);
        }
    }
}