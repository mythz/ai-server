using ServiceStack;
using ServiceStack.DataAnnotations;

namespace AiServer.ServiceModel;

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

public class OpenAiChatRequest
{
    public long Id { get; set; }
    public string Model { get; set; }
    public string Provider { get; set; }
    public OpenAiChat Request { get; set; }
}

[ValidateApiKey]
public class OpenAiChatOperations : IPost, IReturn<EmptyResponse>
{
    public bool? RequeueIncompleteTasks { get; set; }
}

public class CompleteOpenAiChat : IPost, IReturn<EmptyResponse>
{
    public long Id { get; set; }
    public string Provider { get; set; }
    public int DurationMs { get; set; }
    public OpenAiChatResponse Response { get; set; }
}

public class FailOpenAiChat : IPost, IReturn<EmptyResponse>
{
    public int Id { get; set; }
    public string Provider { get; set; }
    public int DurationMs { get; set; }
    public ResponseStatus Error { get; set; }
}

public class OpenAiChatTask : TaskBase
{
    public OpenAiChat Request { get; set; }
    public OpenAiChatResponse? Response { get; set; }
}

public class OpenAiChatCompleted : OpenAiChatTask {}
public class OpenAiChatFailed : OpenAiChatTask
{
    /// <summary>
    /// When the Task was failed
    /// </summary>
    public DateTime FailedDate { get; set; }
}

public class QueryCompletedChatTasks : QueryDb<OpenAiChatCompleted>
{
    public string? Db { get; set; }
    public int? Id { get; set; }
    public string? RefId { get; set; }
}

public class QueryFailedChatTasks : QueryDb<OpenAiChatFailed>
{
    public string? Db { get; set; }
}

[ValidateApiKey]
public class GetActiveProviders : IGet, IReturn<GetActiveProvidersResponse> {}

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

[ExcludeMetadata]
public class RequeueTasks : IGet, IReturn<EmptyResponse> {}
