using AiServer.ServiceInterface.Commands;
using AiServer.ServiceModel;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace AiServer.ServiceInterface;

[Tag(Tag.Tasks)]
[Restrict(RequestAttributes.MessageQueue), ExcludeMetadata]
public class AppDbWrites : IGet, IReturn<EmptyResponse>
{
    [Command<CreateOpenAiChatTaskCommand>]
    public OpenAiChatTask? CreateOpenAiChatTask { get; set; }
    
    [Command<ReserveOpenAiChatTaskCommand>]
    public ReserveOpenAiChatTask? ReserveOpenAiChatTask { get; set; }
    
    [Command<RequeueIncompleteTasksCommand>]
    public RequeueIncompleteTasks? RequeueIncompleteTasks { get; set; }
}
