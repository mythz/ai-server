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
    };
}
