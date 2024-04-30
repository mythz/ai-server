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
    
    [Command<RequeueIncompleteTasksCommand>]
    public RequeueIncompleteTasks? RequeueIncompleteTasks { get; set; }

    [Command<DelegateOpenAiChatTasksCommand>]
    public DelegateOpenAiChatTasks? DelegateOpenAiChatTasks{ get; set; }
    
    [Command<CompleteOpenAiChatCommand>]
    public CompleteOpenAiChat? CompleteOpenAiChat { get; set; }
    
    [Command<CompleteNotificationCommand>]
    public CompleteNotification? CompleteNotification { get; set; }
    
    [Command<RecordOfflineProviderCommand>]
    public RecordOfflineProvider? RecordOfflineProvider { get; set; }
    
    [Command<AppDbPeriodicTasksCommand>]
    public PeriodicTasks? PeriodicTasks { get; set; } 
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
    
    [Command<ExecutorPeriodicTasksCommand>]
    public PeriodicTasks? PeriodicTasks { get; set; } 
}

public class PeriodicTasks
{
    public PeriodicFrequency PeriodicFrequency { get; set; }
}
public enum PeriodicFrequency
{
    Frequent,
    Hourly,
    Daily,
    Monthly,
}

public class BackgroundMqServices  : Service
{
    public Task Any(AppDbWrites request) => Request.ExecuteCommandsAsync(request);
    public Task Any(NotificationTasks request) => Request.ExecuteCommandsAsync(request);
    public Task Any(ExecutorTasks request) => Request.ExecuteCommandsAsync(request);
}
