using AiServer.ServiceModel;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface.Commands;

public class CompleteNotification
{
    public TaskType Type { get; set; }
    public long Id { get; set; }
    public DateTime? CompletedDate { get; set; }
    public ResponseStatus? Error { get; set; }
}

public class CompleteNotificationCommand(IDbConnectionFactory dbFactory) : IAsyncCommand<CompleteNotification>
{
    public async Task ExecuteAsync(CompleteNotification request)
    {
        var failed = request.Error != null;
        
        using var db = dbFactory.OpenDbConnection();
        if (request.Type == TaskType.OpenAiChat)
        {
            if (!failed)
            {
                await db.UpdateOnlyAsync(() => new OpenAiChatTask
                {
                    NotificationDate = request.CompletedDate,
                }, where: x => x.Id == request.Id);
            }
            else
            {
                var errorCode = request.Error?.ErrorCode;
                await db.UpdateOnlyAsync(() => new OpenAiChatTask
                {
                    Error = request.Error,
                    ErrorCode = errorCode,
                }, where: x => x.Id == request.Id);
            }

            var task = await db.SingleByIdAsync<OpenAiChatTask>(request.Id);
            
            using var dbMonth = dbFactory.GetMonthDbConnection(task.CreatedDate);

            if (!failed)
            {
                var completedTask = task.ToOpenAiChatCompleted();
                await dbMonth.InsertAsync(completedTask);
            }
            else
            {
                var failedTask = task.ToOpenAiChatFailed();
                await dbMonth.InsertAsync(failedTask);
            }
            
            await db.DeleteByIdAsync<OpenAiChatTask>(request.Id);
        }
    }
}
