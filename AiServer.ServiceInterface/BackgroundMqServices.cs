﻿using AiServer.ServiceInterface.AppDb;
using AiServer.ServiceInterface.Executor;
using AiServer.ServiceInterface.Notification;
using AiServer.ServiceInterface.Queue;
using AiServer.ServiceModel;
using AiServer.ServiceModel.Types;
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
    
    [Command<RequestOpenAiChatTasksCommand>]
    public RequestOpenAiChatTasks? RequestOpenAiChatTasks { get; set; }
    
    [Command<RequeueIncompleteTasksCommand>]
    public RequeueIncompleteTasks? RequeueIncompleteTasks { get; set; }
    
    [Command<ResetTaskQueueCommand>]
    public ResetTaskQueue? ResetTaskQueue { get; set; }
    
    [Command<ResetFailedTasksCommand>]
    public SelectedTasks? ResetFailedTasks { get; set; }
    
    [Command<RequeueFailedTasksCommand>]
    public SelectedTasks? RequeueFailedTasks { get; set; }
    
    [Command<CompleteOpenAiChatCommand>]
    public CompleteOpenAiChat? CompleteOpenAiChat { get; set; }
    
    [Command<CompleteNotificationCommand>]
    public CompleteNotification? CompleteNotification { get; set; }
    
    [Command<FailOpenAiChatCommand>]
    public FailOpenAiChat? FailOpenAiChat { get; set; }
    
    [Command<ChangeProviderStatusCommand>]
    public ChangeProviderStatus? RecordOfflineProvider { get; set; }
    
    [Command<AppDbPeriodicTasksCommand>]
    public PeriodicTasks? PeriodicTasks { get; set; } 
}

[Tag(Tag.Tasks)]
[Restrict(RequestAttributes.MessageQueue), ExcludeMetadata]
public class QueueTasks : IReturn<EmptyResponse>
{
    [Command<DelegateOpenAiChatTasksCommand>]
    public DelegateOpenAiChatTasks? DelegateOpenAiChatTasks { get; set; }
}

[Tag(Tag.Tasks)]
[Restrict(RequestAttributes.MessageQueue), ExcludeMetadata]
public class NotificationTasks : IReturn<EmptyResponse>
{
    [Command<NotificationRequestCommand>]
    public NotificationRequest? NotificationRequest { get; set; }

    [Command<SendPendingNotificationsCommand>]
    public SendPendingNotifications? SendPendingNotifications { get; set; }
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

public class SelectedTasks
{
    public List<long> Ids { get; set; }
}

public class BackgroundMqServices  : Service
{
    public Task Any(AppDbWrites request) => Request.ExecuteCommandsAsync(request);
    public Task Any(QueueTasks request) => Request.ExecuteCommandsAsync(request);
    public Task Any(NotificationTasks request) => Request.ExecuteCommandsAsync(request);
    public Task Any(ExecutorTasks request) => Request.ExecuteCommandsAsync(request);
}
