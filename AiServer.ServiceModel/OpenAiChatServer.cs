using AiServer.ServiceModel.Types;
using ServiceStack;

namespace AiServer.ServiceModel;

[ValidateApiKey]
public class QueryOpenAiChat : QueryDb<OpenAiChatTask>
{
    public int? Id { get; set; }
    public string? RefId { get; set; }
}

[ValidateApiKey]
public class CreateOpenAiChat : ICreateDb<OpenAiChatTask>, IReturn<CreateOpenAiChatResponse>
{
    public string? RefId { get; set; }
    public string? Provider { get; set; }
    public string? ReplyTo { get; set; }
    public OpenAiChat Request { get; set; }
}
public class CreateOpenAiChatResponse
{
    public long Id { get; set; }
    public string RefId { get; set; }
    public ResponseStatus? ResponseStatus { get; set; }
}

[ValidateApiKey]
public class FetchOpenAiChatRequests : IPost, IReturn<FetchOpenAiChatRequestsResponse>
{
    [ValidateNotEmpty]
    public string[] Models { get; set; }

    [ValidateNotEmpty]
    public string Provider { get; set; }
    
    public string? Worker { get; set; }
    
    public int? Take { get; set; }
}
public class FetchOpenAiChatRequestsResponse
{
    public required OpenAiChatRequest[] Results { get; set; }
    public ResponseStatus? ResponseStatus { get; set; }
}

[ValidateApiKey]
public class CompleteOpenAiChat : IPost, IReturn<EmptyResponse>
{
    public long Id { get; set; }
    public string Provider { get; set; }
    public int DurationMs { get; set; }
    public OpenAiChatResponse Response { get; set; }
}

[ValidateApiKey]
public class FailOpenAiChat : IPost, IReturn<EmptyResponse>
{
    public long Id { get; set; }
    public string Provider { get; set; }
    public int DurationMs { get; set; }
    public ResponseStatus Error { get; set; }
}

[ValidateApiKey]
public class QueryCompletedChatTasks : QueryDb<OpenAiChatCompleted>
{
    public string? Db { get; set; }
    public int? Id { get; set; }
    public string? RefId { get; set; }
}

[ValidateApiKey]
public class QueryFailedChatTasks : QueryDb<OpenAiChatFailed>
{
    public string? Db { get; set; }
}

[ValidateApiKey]
public class GetActiveProviders : IGet, IReturn<GetActiveProvidersResponse> {}

[ValidateAuthSecret]
public class ResetActiveProviders : IGet, IReturn<GetActiveProvidersResponse> {}

public class GetActiveProvidersResponse
{
    public ApiProvider[] Results { get; set; }
    public ResponseStatus? ResponseStatus { get; set; }
}

[ValidateApiKey]
public class ChatApiProvider : IPost, IReturn<OpenAiChatResponse>
{
    public string Provider { get; set; }
    public string Model { get; set; }
    public OpenAiChat Request { get; set; }
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
public class ChangeApiProviderStatus : IPost, IReturn<StringResponse>
{
    public string Provider { get; set; }
    public bool Online { get; set; }
}

[ValidateAuthSecret]
public class CreateApiKey : IPost, IReturn<CreateApiKeyResponse>
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public List<string> Scopes { get; set; } = new();
    public string? Notes { get; set; }
    public int? RefId { get; set; }
    public string? RefIdStr { get; set; }
    public Dictionary<string, string>? Meta { get; set; }
}
public class CreateApiKeyResponse
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string VisibleKey { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? CancelledDate { get; set; }
    public string? Notes { get; set; }
}

[ValidateAuthSecret]
public class GetApiWorkerStats : IGet, IReturn<GetApiWorkerStatsResponse> { }
public class GetApiWorkerStatsResponse
{
    public List<WorkerStats> Results { get; set; }
    public ResponseStatus? ResponseStatus { get; set; }
}

public class WorkerStats
{
    public string Name { get; init; }
    public long Received { get; init; }
    public long Completed { get; init; }
    public long Retries { get; init; }
    public long Failed { get; init; }
    public DateTime? OfflineAt { get; init; }
    public bool Running { get; init; }
}

[ValidateAuthSecret]
public class QueryTaskSummary : QueryDb<TaskSummary> {}

[ValidateAuthSecret]
public class FirePeriodicTask : IPost, IReturn<EmptyResponse>
{
    public PeriodicFrequency Frequency { get; set; }
}
