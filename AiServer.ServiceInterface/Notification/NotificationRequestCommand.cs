using System.Net.Http.Headers;
using AiServer.ServiceInterface.AppDb;
using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Messaging;

namespace AiServer.ServiceInterface.Notification;

public class NotificationRequest
{
    public string Url { get; set; }
    public string Method { get; set; }
    public string? BearerToken { get; set; }
    public string? ContentType { get; set; }
    public string? Body { get; set; }
    public CompleteNotification CompleteNotification { get; set; }
}

public class NotificationRequestCommand(ILogger<NotificationRequestCommand> log, IMessageProducer mq) : IAsyncCommand<NotificationRequest>
{
    public async Task ExecuteAsync(NotificationRequest request)
    {
        var method = request.Method ?? HttpMethods.Post;
        Action<HttpRequestMessage>? requestFilter = request.BearerToken != null
            ? req => req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.BearerToken)
            : null;

        Exception? holdError = null;

        var retry = 0;
        while (retry++ < 5)
        {
            try
            {
                await request.Url.SendStringToUrlAsync(method, requestBody:request.Body, contentType:request.ContentType,
                    requestFilter:requestFilter);
                holdError = null;
                request.CompleteNotification.CompletedDate = DateTime.UtcNow;
                break;
            }
            catch (Exception e)
            {
                holdError ??= e;
                log.LogError(e, "Failed to send notification request {Url}: {Message} x{Retry}", request.Url, e.Message, retry);
            }
            await Task.Delay(retry * retry * 200);
        }
        
        if (holdError != null)
        {
            request.CompleteNotification.Error = holdError.ToResponseStatus();
        }

        mq.Publish(new AppDbWrites {
            CompleteNotification = request.CompleteNotification
        });
    }
}
