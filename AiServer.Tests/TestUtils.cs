using ServiceStack;

namespace AiServer.Tests;

public static class TestUtils
{
    public static string GetHostDir() => "../../../../AiServer";
    public static string GetQuestionsDir() => Path.GetFullPath("../../../../../pvq/questions");

    public const string SystemPrompt = "You are a friendly AI Assistant that helps answer developer questions. Think step by step and assist the user with their question, ensuring that your answer is relevant, on topic and provides actionable advice with code examples as appropriate.";

    public static JsonApiClient CreateSystemClient()
    {
        var client = new JsonApiClient("https://localhost:5001")
        {
            BearerToken = "ak-1359a079e98841a2a0c52419433d207f",
        };
        return client;
    }

    public static JsonApiClient CreateAdminClient()
    {
        var client = new JsonApiClient("https://localhost:5001")
        {
            BearerToken = "ak-4357089af5a446cab0fdc44830e03617",
        };
        return client;
    }
}
