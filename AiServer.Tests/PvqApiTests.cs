using AiServer.ServiceModel;
using NUnit.Framework;
using ServiceStack;

namespace AiServer.Tests;

public class PvqApiTests
{
    private static List<CreateApiProvider> ApiProviders =
    [
        new CreateApiProvider
        {
            Name = "macbook",
            ApiTypeId = 1,
            ApiBaseUrl = "https://macbook.pvq.app",
            Concurrency = 1,
            Priority = 4,
            Enabled = true,
            Models =
            [
                new() { Model = "phi3", },
                new() { Model = "mistral", },
                new() { Model = "llama3:8b", },
                new() { Model = "gemma", },
                new() { Model = "codellama", },
            ]
        },
        new CreateApiProvider
        {
            Name = "supermicro",
            ApiTypeId = 1,
            ApiBaseUrl = "https://supermicro.pvq.app",
            Concurrency = 1,
            Priority = 0,
            Enabled = true,
            Models = [
                new() { Model = "phi3", },
                new() { Model = "mistral", },
                new() { Model = "llama3:8b", },
                new() { Model = "gemma", },
                new() { Model = "codellama", },
                new() { Model = "mixtral", },
                new() { Model = "command-r", },
            ]
        },
        new CreateApiProvider
        {
            Name = "dell",
            ApiTypeId = 1,
            ApiBaseUrl = "https://dell.pvq.app",
            Concurrency = 1,
            Priority = 0,
            Enabled = true,
            Models = [
                new() { Model = "phi3", },
                new() { Model = "mistral", },
                new() { Model = "llama3:8b", },
                new() { Model = "llama3:70b", },
                new() { Model = "gemma", },
                new() { Model = "codellama", },
                new() { Model = "mixtral", },
                new() { Model = "command-r", },
            ]
        },
        new CreateApiProvider
        {
            Name = "openrouter-free",
            ApiTypeId = 2,
            ApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"),
            Concurrency = 3,
            Priority = 1,
            Enabled = true,
            Models =
            [
                new()
                {
                    Model = "mistral",
                    ApiModel = "mistralai/mistral-7b-instruct:free",
                },
                new()
                {
                    Model = "gemma",
                    ApiModel = "google/gemma-7b-it:free",
                },
            ]
        },
        new CreateApiProvider
        {
            Name = "groq",
            ApiTypeId = 3,
            ApiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY"),
            Concurrency = 1,
            Priority = 2,
            Enabled = true,
            Models =
            [
                new() { Model = "llama3:8b", },
                new() { Model = "llama3:70b", },
                new() { Model = "gemma", },
                new() { Model = "mixtral", },
            ]
        },
        new CreateApiProvider
        {
            Name = "openrouter",
            ApiTypeId = 2,
            ApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"),
            Concurrency = 1,
            Priority = 0,
            Enabled = true,
            Models =
            [
                new() { Model = "mixtral:8x22b", },
                new() { Model = "llama3:70b" },
                new() { Model = "wizardlm2:7b", },
                new() { Model = "wizardlm2:8x22b", },
                new() { Model = "mistral-small", },
                new() { Model = "mistral-large", },
                new() { Model = "dbrx", },
                new() { Model = "command-r", },
                new() { Model = "command-r-plus", },
                new() { Model = "claude-3-haiku", },
                new() { Model = "claude-3-sonnet", },
                new() { Model = "claude-3-opus", },
                new() { Model = "gemini-pro", },
                new() { Model = "gemini-pro-1.5", },
                new() { Model = "gemini-pro-vision", },
                new() { Model = "gpt-3.5-turbo", },
                new() { Model = "gpt-4", },
                new() { Model = "gpt-4-turbo", },
                new() { Model = "gpt-4-vision", },
            ]
        },
        new CreateApiProvider
        {
            Name = "google",
            ApiTypeId = 5,
            ApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY"),
            Concurrency = 1,
            Priority = 0,
            Enabled = true,
            Models =
            [
                new() { Model = "gemini-pro", },
                new() { Model = "gemini-pro-1.5", },
                new() { Model = "gemini-pro-vision", },
            ]
        },
        new CreateApiProvider
        {
            Name = "mistral",
            ApiTypeId = 4,
            ApiKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY"),
            Concurrency = 1,
            Priority = 0,
            Enabled = false,
            Models =
            [
                new() { Model = "mistral", },
                new() { Model = "mixtral", },
                new() { Model = "mixtral:8x22b", },
                new() { Model = "mistral-small", },
                new() { Model = "mistral-large", },
                new() { Model = "mistral-embed", },
            ]
        },
    ];
    
    [Test]
    [Ignore("Skip on CI")]
    public async Task Can_Add_ApiProviders()
    {
        ClientConfig.UseSystemJson = UseSystemJson.Always;
        
        var client = TestUtils.CreateAdminClient();
        foreach (var createApiProvider in ApiProviders)
        {
            var api = await client.ApiAsync(createApiProvider);
            api.ThrowIfError();
        }
    }
}