using ServiceStack;

namespace AiServer.ServiceInterface;

public class BackgroundMqServices  : Service
{
    public Task Any(AppDbWrites request) => Request.ExecuteCommandsAsync(request);
}
