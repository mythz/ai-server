using System.Data;
using AiServer.ServiceModel;
using Microsoft.Extensions.Logging;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface.Commands;

public class CreateOpenAiChatTaskCommand(ILogger<CreateOpenAiChatTaskCommand> log, IDbConnection db) : IAsyncCommand<OpenAiChatTask>
{
    public async Task ExecuteAsync(OpenAiChatTask request)
    {
        request.CreatedDate = DateTime.UtcNow;
        request.RefId ??= Guid.NewGuid().ToString("N");
        
        await db.InsertAsync(request);
    }
}