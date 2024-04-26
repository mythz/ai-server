using ServiceStack;

namespace AiServer.ServiceModel;

public class ValidateApiKeyAttribute() : ValidateRequestAttribute("ApiKey()");

public static class Tag
{
    public const string Tasks = nameof(Tasks);
    public const string User = nameof(User);
}