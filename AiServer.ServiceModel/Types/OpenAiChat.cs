namespace AiServer.ServiceModel.Types;

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

public class OpenAiChatRequest
{
    public long Id { get; set; }
    public string Model { get; set; }
    public string Provider { get; set; }
    public OpenAiChat Request { get; set; }
}
