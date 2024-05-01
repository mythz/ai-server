using AiServer.ServiceInterface;
using AiServer.ServiceModel;
using NUnit.Framework;
using ServiceStack.Text;

namespace AiServer.Tests;

[Explicit]
public class OpenAiProviderTests
{
    [Test]
    public async Task Can_Send_Ollama_Phi3_Request()
    {
        var response = await OpenAiProvider.Instance.ChatAsync(new ApiProviderWorker(TestUtils.MacbookApiProvider), new OpenAiChat
        {
            Model = "phi3",
            Messages =
            [
                new()
                {
                    Role = "user",
                    Content = "What is the capital of France?",
                }
            ],
            MaxTokens = 100,
        });
        
        response.PrintDump();
    }

    [Test]
    public async Task Can_Send_Google_GeminiPro_Request()
    {
        var response = await GoogleOpenAiProvider.Instance.ChatAsync(new ApiProviderWorker(TestUtils.GoogleApiProvider), new OpenAiChat
        {
            Model = "gemini-pro",
            Messages =
            [
                new()
                {
                    Role = "user",
                    Content = "What is the capital of France?",
                }
            ],
            MaxTokens = 100,
        });
        
        response.PrintDump();
    }
}
