using ServiceStack;
using ServiceStack.DataAnnotations;
using ServiceStack.Model;

namespace AiServer.ServiceModel;

/// <summary>
///  An API Provider that can process tasks
/// </summary>
public class ApiProvider
{
    [AutoIncrement]
    public int Id { get; set; }
        
    /// <summary>
    /// The unique name for this API Provider
    /// </summary>
    [Index(Unique = true)]
    public string Name { get; set; }
        
    /// <summary>
    /// The behavior for this API Provider
    /// </summary>
    public int ApiTypeId { get; set; }
        
    /// <summary>
    /// The API Key to use for this Provider
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Send the API Key in the Header instead of Authorization Bearer
    /// </summary>
    public string? ApiKeyHeader { get; set; }
        
    /// <summary>
    /// Override Base URL for the API Provider
    /// </summary>
    public string? ApiBaseUrl { get; set; }
        
    /// <summary>
    /// Override API Paths for different AI Tasks
    /// </summary>
    public Dictionary<TaskType, string>? TaskPaths { get; set; }
        
    /// <summary>
    /// How many requests should be made concurrently
    /// </summary>
    public int Concurrency { get; set; }
        
    /// <summary>
    /// What priority to give this Provider to use for processing models 
    /// </summary>
    public int Priority { get; set; }
        
    /// <summary>
    /// Whether the Provider is enabled
    /// </summary>
    public bool Enabled { get; set; }
        
    /// <summary>
    /// When the Provider was created
    /// </summary>
    public DateTime CreatedDate { get; set; }
        
    [Reference]
    public ApiType ApiType { get; set; }
        
    [Reference]
    public List<ApiProviderModel> Models { get; set; }
}


/// <summary>
/// The models this API Provider can process 
/// </summary>
public class ApiProviderModel
{
    [AutoIncrement]
    public int Id { get; set; }

    public int ApiProviderId { get; set; }
    
    /// <summary>
    /// Ollama Model Id
    /// </summary>
    public string Model { get; set; }
    
    /// <summary>
    /// What Model to use for this API Provider
    /// </summary>
    public string ApiModel { get; set; }
}

/// <summary>
/// The behavior of the API Provider
/// </summary>
public class ApiType
{
    [AutoIncrement]
    public int Id { get; set; }
        
    /// <summary>
    /// Name for this API Provider Type
    /// </summary>
    public string Name { get; set; }
        
    /// <summary>
    /// The website for this provider
    /// </summary>
    public string Website { get; set; }

    /// <summary>
    /// The API Base Url
    /// </summary>
    public string ApiBaseUrl { get; set; }
        
    /// <summary>
    /// Uses a Custom IOpenAiProvider
    /// </summary>
    public string? OpenAiProvider { get; set; }
        
    /// <summary>
    /// API Paths for different AI Tasks
    /// </summary>
    public Dictionary<TaskType, string> TaskPaths { get; set; }

    /// <summary>
    /// Mapping of Ollama Models to API Models
    /// </summary>
    public Dictionary<string, string> ApiModels { get; set; } = new();
}

/// <summary>
/// Different Models available in AI Server 
/// </summary>
public class ApiModel
{
    [AutoIncrement]
    public int Id { get; set; }
        
    [Index(Unique = true)]
    public string Name { get; set; }

    public string? Website { get; set; }
    
    public int? Parameters { get; set; }

    public int? ContextSize { get; set; }

    public int? Experts { get; set; }
    
    public string? Notes { get; set; }
}

public class TaskSummary
{
    public long Id { get; set; }

    /// <summary>
    /// The type of Task
    /// </summary>
    public TaskType Type { get; set; }
    
    /// <summary>
    /// The model to use for the Task
    /// </summary>
    public string Model { get; set; }

    /// <summary>
    /// The specific provider used to complete the Task
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Unique External Reference for the Task
    /// </summary>
    [Index(Unique = true)]
    public string RefId { get; set; }
    
    /// <summary>
    /// Number of tokens in the prompt.
    /// </summary>
    public int PromptTokens { get; set; }
    
    /// <summary>
    /// Number of tokens in the generated completion.
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    /// The duration reported by the worker to complete the task
    /// </summary>
    public int DurationMs { get; set; }

    /// <summary>
    /// The Month DB the Task was created in
    /// </summary>
    public string Db { get; set; }
    
    /// <summary>
    /// The Primary Key for the Task in the Month Db
    /// </summary>
    public int DbId { get; set; }
}

public abstract class TaskBase : IHasLongId
{
    /// <summary>
    /// Primary Key for the Task
    /// </summary>
    public virtual long Id { get; set; }

    /// <summary>
    /// The model to use for the Task
    /// </summary>
    [Index]
    public virtual string Model { get; set; }

    /// <summary>
    /// The specific provider to use to complete the Task
    /// </summary>
    public virtual string? Provider { get; set; }
    
    /// <summary>
    /// Unique External Reference for the Task
    /// </summary>
    [Index(Unique = true)]
    public virtual string? RefId { get; set; }
    
    /// <summary>
    /// URL to publish the Task to
    /// </summary>
    public virtual string? ReplyTo { get; set; } 
    
    /// <summary>
    /// When the Task was created
    /// </summary>
    public virtual DateTime CreatedDate { get; set; }

    /// <summary>
    /// The API Key UserName which created the Task
    /// </summary>
    public virtual string CreatedBy { get; set; }

    /// <summary>
    /// The worker that is processing the Task
    /// </summary>
    public virtual string? Worker { get; set; }

    /// <summary>
    /// The Remote IP Address of the worker
    /// </summary>
    public virtual string? WorkerIp { get; set; }

    /// <summary>
    /// The HTTP Request Id reserving the task for the worker
    /// </summary>
    public virtual string? RequestId { get; set; }
    
    /// <summary>
    /// When the Task was started
    /// </summary>
    [Index]
    public virtual DateTime? StartedDate { get; set; }

    /// <summary>
    /// When the Task was completed
    /// </summary>
    public virtual DateTime? CompletedDate { get; set; }
    
    /// <summary>
    /// The duration reported by the worker to complete the task
    /// </summary>
    public virtual int DurationMs { get; set; }
    
    /// <summary>
    /// How many times to attempt to retry the task
    /// </summary>
    public virtual int? RetryLimit { get; set; }
    
    /// <summary>
    /// How many times the Task has been retried
    /// </summary>
    public virtual int Retries { get; set; }

    /// <summary>
    /// When the callback for the Task completed
    /// </summary>
    public virtual DateTime? NotificationDate { get; set; }

    /// <summary>
    /// The Exception Type or other Error Code for why the Task failed 
    /// </summary>
    public virtual string? ErrorCode { get; set; }

    /// <summary>
    /// Why the Task failed
    /// </summary>
    public virtual ResponseStatus? Error { get; set; }
}

[EnumAsInt]
public enum TaskType
{
    OpenAiChat = 1,
}

