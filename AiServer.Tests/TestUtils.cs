using AiServer.ServiceModel;
using ServiceStack;

namespace AiServer.Tests;

public static class TestUtils
{
    public static string GetHostDir() => "../../../../AiServer";
    public static string GetQuestionsDir() => Path.GetFullPath("../../../../../pvq/questions");

    public const string SystemPrompt = "You are a friendly AI Assistant that helps answer developer questions. Think step by step and assist the user with their question, ensuring that your answer is relevant, on topic and provides actionable advice with code examples as appropriate.";

    public static string AiServerBaseUrl = "https://localhost:5005";
    public static string PvqBaseUrl = "https://localhost:5001";
    
    public static JsonApiClient CreateSystemClient() => new(AiServerBaseUrl) {
        BearerToken = "ak-1359a079e98841a2a0c52419433d207f",
    };
    public static JsonApiClient CreateAdminClient() => new(AiServerBaseUrl) {
        BearerToken = "ak-4357089af5a446cab0fdc44830e03617",
    };
    public static JsonApiClient CreatePvqClient() => new(AiServerBaseUrl) {
        BearerToken = "ak-78A1B9B4CD684118B2EAFAB1F268E3DB",
    };

    public static JsonApiClient PvqApiClient() => new(PvqBaseUrl) {
        UserName = Environment.GetEnvironmentVariable("PVQ_USERNAME"),
    };
    
    public static string PvqUsername = Environment.GetEnvironmentVariable("PVQ_USERNAME") ?? "servicestack";
    public static string PvqPassword = Environment.GetEnvironmentVariable("PVQ_PASSWORD") ?? "p@55wOrd";

    public static Dictionary<string, string> ModerUserIds = new()
    {
        ["phi3"] = "1d8b07ba-b1de-420c-8b7c-7e767fab9dbc",
        ["llama3:8b"] = "059fceb9-9e5e-4603-93f7-d77972b8eb2f",
        ["codellama"] = "32576297-6242-4ceb-84eb-d8e76da30a37",
        ["mistral"] = "4f96e54c-54e8-48f1-aad3-bbb7e7805469",
        ["mixtral"] = "ed334220-8614-4846-81e8-e2e94a9104ac",
        ["gemma"] = "d56768fb-bfcc-4a86-bb48-b428266d3e7c",
        ["gemini-pro"] = "e972c92b-68a9-4374-a2c4-1fb819f19cb3",
    };
    
    public static ApiType OpenAiApiType = new()
    {
        Id = 1,
        Name = "ollama",
        TaskPaths = new() {
            [TaskType.OpenAiChat] = "/v1/chat/completions",
        },
    };
    public static ApiType GoogleApiType = new()
    {
        Id = 5,
        Name = "google",
        Website = "https://cloud.google.com",
        ApiBaseUrl = "https://generativelanguage.googleapis.com",
        OpenAiProvider = "GoogleOpenAiProvider",
        TaskPaths = new() {
            [TaskType.OpenAiChat] = "/v1beta/models/gemini-pro:generateContent",
        },
        ApiModels = new()
        {
            ["gemini-pro"] = "gemini-pro",
            ["gemini-pro-1.5"] = "gemini-pro-1.5",
            ["gemini-pro-vision"] = "gemini-pro-vision",
        }
    };
    
    public static ApiProvider MacbookApiProvider = new()
    {
        Name = "macbook",
        ApiTypeId = 1,
        ApiBaseUrl = "https://macbook.pvq.app",
        Concurrency = 1,
        Priority = 2,
        Enabled = true,
        Models = [
            new() { Model = "llama3:8b", },
            new() { Model = "phi3", },
            new() { Model = "gemma", },
            new() { Model = "codellama", },
            new() { Model = "mistral", }
        ],
        ApiType = OpenAiApiType
    };

    public static ApiProvider GoogleApiProvider = new()
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
        ],
        ApiType = GoogleApiType
    };
}
