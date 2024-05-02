using System.Data;
using AiServer.ServiceInterface.Queue;
using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Messaging;
using ServiceStack.OrmLite;
using AiServer.ServiceModel;
using AiServer.ServiceModel.Types;

namespace AiServer.ServiceInterface.AppDb;

public class CreateOpenAiChatTaskCommand(ILogger<CreateOpenAiChatTaskCommand> log, IDbConnection db, IMessageProducer mq) 
    : IAsyncCommand<OpenAiChatTask>
{
    public async Task ExecuteAsync(OpenAiChatTask task)
    {
        task.CreatedDate = DateTime.UtcNow;
        task.RefId ??= Guid.NewGuid().ToString("N");

        using var dbTrans = db.OpenTransaction();
        await db.InsertAsync(task);
        await db.InsertAsync(new TaskSummary
        {
            Id = task.Id,
            Type = TaskType.OpenAiChat,
            Model = task.Model,
            Provider = task.Provider,
            RefId = task.RefId,
            Tag = task.Tag,
            CreatedDate = task.CreatedDate,
        });
        dbTrans.Commit();

        var running = DelegateOpenAiChatTasksCommand.Running;
        if (!running)
        {
            mq.Publish(new QueueTasks {
                DelegateOpenAiChatTasks = new()
            });
        }
    }
}
