using AiServer.ServiceModel;
using AiServer.ServiceModel.Types;
using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface.AppDb;

public class CompleteNotification
{
    public TaskType Type { get; set; }
    public long Id { get; set; }
    public DateTime? CompletedDate { get; set; }
    public ResponseStatus? Error { get; set; }
}

public class CompleteNotificationCommand(ILogger<CompleteNotificationCommand> log, IDbConnectionFactory dbFactory) : IAsyncCommand<CompleteNotification>
{
    public async Task ExecuteAsync(CompleteNotification request)
    {
        var failed = request.Error != null;
        
        using var db = dbFactory.OpenDbConnection();
        if (request.Type == TaskType.OpenAiChat)
        {
            var task = await db.SingleByIdAsync<OpenAiChatTask>(request.Id);
            if (task == null)
            {
                log.LogWarning("Task {Id} does not exist", request.Id);
                return;
            }

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

            var monthDbName = dbFactory.GetNamedMonthDb(task.CreatedDate);
            using var dbMonth = HostContext.AppHost.GetDbConnection(monthDbName);

            if (!failed)
            {
                var completedTask = task.ToOpenAiChatCompleted();
                var openAiUsage = task.Response?.Usage;
                var promptTokens = openAiUsage?.PromptTokens ?? 0;
                var completionTokens = openAiUsage?.CompletionTokens ?? 0;
                await db.UpdateOnlyAsync(() => new TaskSummary {
                    Type = TaskType.OpenAiChat,
                    Model = task.Model,
                    PromptTokens = promptTokens, 
                    CompletionTokens = completionTokens,
                    Provider = task.Provider,
                    DurationMs = task.DurationMs,
                    Tag = task.Tag,
                    RefId = task.RefId,
                    CreatedDate = task.CreatedDate,
                }, x => x.Id == task.Id);
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
