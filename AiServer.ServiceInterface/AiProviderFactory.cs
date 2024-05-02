using AiServer.ServiceModel;

namespace AiServer.ServiceInterface;

public record OpenAiChatResult(OpenAiChatResponse Response, int DurationMs);

public interface IOpenAiProvider
{
    Task<bool> IsOnlineAsync(IApiProviderWorker apiProvider);

    Task<OpenAiChatResult> ChatAsync(IApiProviderWorker worker, OpenAiChat request);
}

public class AiProviderFactory(OpenAiProvider openAiProvider, GoogleOpenAiProvider googleProvider)
{
    public static AiProviderFactory Instance { get; set; }
    
    public IOpenAiProvider GetOpenAiProvider(string? type = null)
    {
        return type == nameof(GoogleOpenAiProvider)
            ? googleProvider
            : openAiProvider;
    }
}