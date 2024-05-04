using AiServer.ServiceModel;
using AiServer.ServiceModel.Types;
using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.Messaging;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface.Notification;

public class SendPendingNotifications {}

public class SendPendingNotificationsCommand(ILogger<SendPendingNotificationsCommand> log, AppData appData, 
    IDbConnectionFactory dbFactory, IMessageProducer mq) 
    : IAsyncCommand<SendPendingNotifications>
{
    public static long running = 0;
    public static bool Running => Interlocked.Read(ref running) > 0;
    private static long counter = 0;
    
    public async Task ExecuteAsync(SendPendingNotifications request)
    {
        if (Interlocked.CompareExchange(ref running, 1, 0) == 0)
        {
            try
            {
                if (appData.IsStopped)
                    return;

                using var db = await dbFactory.OpenDbConnectionAsync();
                var pendingNotifications = await db.SelectAsync(db.From<OpenAiChatTask>()
                    .Where(x => x.CompletedDate != null && x.NotificationDate == null && x.Retries <= 3 && x.ReplyTo != null && x.Response != null));
                    
                foreach (var task in pendingNotifications)
                {
                    var json = task.Response.ToJson();
                    mq.Publish(new NotificationTasks
                    {
                        NotificationRequest = new()
                        {
                            Url = task.ReplyTo!,
                            ContentType = MimeTypes.Json,
                            Body = json,
                            CompleteNotification = new()
                            {
                                Type = TaskType.OpenAiChat,
                                Id = task.Id,
                            },
                        },
                    });
                }

                if (pendingNotifications.Count > 0)
                    log.LogInformation("[Chat] Delegated {PendingCount} pending notifications, exiting...", pendingNotifications.Count);
            }
            catch (TaskCanceledException)
            {
                log.LogInformation("[Chat] Notification tasks was cancelled, exiting...");
            }
            catch (Exception ex)
            {
                log.LogError(ex, "[Chat] Error sending task notifications, exiting...");
            }
            finally
            {
                Interlocked.Decrement(ref running);
            }
        }
    }
}

