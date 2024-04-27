using ServiceStack;

namespace AiServer.ServiceInterface;

public class BackgroundMqServices  : Service
{
    public Task Any(AppDbWrites request) => Request.ExecuteCommandsAsync(request);
    public Task Any(NotificationTasks request) => Request.ExecuteCommandsAsync(request);
}
