using AiServer.ServiceModel.Types;
using ServiceStack;

namespace AiServer.ServiceModel;

[ValidateAuthSecret]
public class StopWorkers : IPost, IReturn<EmptyResponse> {}
[ValidateAuthSecret]
public class StartWorkers : IPost, IReturn<EmptyResponse> {}
[ValidateAuthSecret]
public class RestartWorkers : IPost, IReturn<EmptyResponse> {}

[ValidateAuthSecret]
public class ResetActiveProviders : IGet, IReturn<GetActiveProvidersResponse> {}

[ValidateAuthSecret]
public class ChangeApiProviderStatus : IPost, IReturn<StringResponse>
{
    public string Provider { get; set; }
    public bool Online { get; set; }
}

[ValidateAuthSecret]
public class OpenAiChatOperations : IPost, IReturn<EmptyResponse>
{
    public bool? ResetTaskQueue { get; set; }
    public bool? RequeueIncompleteTasks { get; set; }
}

[ValidateAuthSecret]
public class OpenAiChatFailedTasks : IPost, IReturn<EmptyResponse>
{
    public bool? ResetErrorState { get; set; }
    [Input(Type = "tag")]
    public List<long>? RequeueFailedTaskIds { get; set; }
}

[ValidateAuthSecret]
public class FirePeriodicTask : IPost, IReturn<EmptyResponse>
{
    public PeriodicFrequency Frequency { get; set; }
}
