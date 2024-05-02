using AiServer.ServiceInterface;
using ServiceStack.Configuration;
using ServiceStack.DataAnnotations;
using ServiceStack.Model;
using ServiceStack.OrmLite;

namespace AiServer.Migrations;

public class Migration1000 : MigrationBase
{
    public class OpenAiChatTask : TaskBase
    {
        public OpenAiChat Request { get; set; }
        public OpenAiChatResponse? Response { get; set; }
    }

    public class OpenAiChat {}
    public class OpenAiChatResponse {}

    [EnumAsInt]
    public enum TaskType {}

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
        public string? RefId { get; set; }
    
        /// <summary>
        /// Optional Tag to group related Tasks
        /// </summary>
        public string? Tag { get; set; }
        
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
        public long DbId { get; set; }
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
        /// Optional Tag to group related Tasks
        /// </summary>
        public string? Tag { get; set; }
        
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

    public override void Up()
    {
        Db.CreateTable<OpenAiChatTask>();
        Db.CreateTable<TaskSummary>();
    }

    public override void Down()
    {
        Db.DropTable<TaskSummary>();
        Db.DropTable<OpenAiChatTask>();
    }
}