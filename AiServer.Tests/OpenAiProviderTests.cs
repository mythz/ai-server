using AiServer.ServiceInterface;
using AiServer.ServiceModel;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ServiceStack.Text;

namespace AiServer.Tests;

[Explicit]
public class OpenAiProviderTests
{
    private AiProviderFactory factory;

    public OpenAiProviderTests()
    {
        factory = new AiProviderFactory(
            new OpenAiProvider(new NullLogger<OpenAiProvider>()),
            new GoogleOpenAiProvider(new NullLogger<GoogleOpenAiProvider>()));
    }

    [Test]
    public async Task Can_Send_Ollama_Phi3_Request()
    {
        var openAi = factory.GetOpenAiProvider();
        var response = await openAi.ChatAsync(new ApiProviderWorker(TestUtils.MacbookApiProvider, factory), new OpenAiChat
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
        var openAi = factory.GetOpenAiProvider(nameof(GoogleOpenAiProvider));
        var response = await openAi.ChatAsync(new ApiProviderWorker(TestUtils.GoogleApiProvider, factory), new OpenAiChat
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
