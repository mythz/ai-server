using AiServer.ServiceInterface.AppDb;
using AiServer.ServiceInterface.Executor;
using AiServer.ServiceInterface.Notification;
using AiServer.ServiceModel;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace AiServer.ServiceInterface;

[Tag(Tag.Tasks)]
[Restrict(RequestAttributes.MessageQueue), ExcludeMetadata]
public class AppDbWrites : IReturn<EmptyResponse>
{
    [Command<CreateOpenAiChatTaskCommand>]
    public OpenAiChatTask? CreateOpenAiChatTask { get; set; }
    
    [Command<ReserveOpenAiChatTaskCommand>]
    public ReserveOpenAiChatTask? ReserveOpenAiChatTask { get; set; }

    [Command<DelegateOpenAiChatTasksCommand>]
    public DelegateOpenAiChatTasks? DelegateOpenAiChatTasks{ get; set; }
    
    [Command<RequeueIncompleteTasksCommand>]
    public RequeueIncompleteTasks? RequeueIncompleteTasks { get; set; }
    
    [Command<CompleteOpenAiChatCommand>]
    public CompleteOpenAiChat? CompleteOpenAiChat { get; set; }
    
    [Command<CompleteNotificationCommand>]
    public CompleteNotification? CompleteNotification { get; set; }
}

[Tag(Tag.Tasks)]
[Restrict(RequestAttributes.MessageQueue), ExcludeMetadata]
public class NotificationTasks : IReturn<EmptyResponse>
{
    [Command<NotificationRequestCommand>]
    public NotificationRequest? NotificationRequest { get; set; }
}

[Tag(Tag.Tasks)]
[Restrict(RequestAttributes.MessageQueue), ExcludeMetadata]
public class ExecutorTasks : IReturn<EmptyResponse>
{
    [Command<ExecuteOpenAiChatTasksCommand>]
    public ExecuteTasks? ExecuteOpenAiChatTasks { get; set; }
}

public class ExecuteTasks {}

public class BackgroundMqServices  : Service
{
    public Task Any(AppDbWrites request) => Request.ExecuteCommandsAsync(request);
    public Task Any(NotificationTasks request) => Request.ExecuteCommandsAsync(request);
    public Task Any(ExecutorTasks request) => Request.ExecuteCommandsAsync(request);
}
