using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Data;
using System.Net;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.Host;
using ServiceStack.OrmLite;
using ServiceStack.Script;
using ServiceStack.Web;

namespace AiServer.ServiceInterface;

public class AccessKey : IMeta
{
    /// <summary>
    /// The API Key
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Name for the API Key
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// User Primary Key
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Name of the User or Worker using the API Key
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// If supporting API Keys for multiple Environments
    /// </summary>
    public string? Environment { get; set; }

    /// <summary>
    /// What to show the User after they've created the API Key
    /// </summary>
    public string VisibleKey { get; set; }
    
    public DateTime CreatedDate { get; set; }
    
    public DateTime? ExpiryDate { get; set; }
    
    public DateTime? CancelledDate { get; set; }
    
    public List<string> Scopes { get; set; }
    
    public string? Notes { get; set; }

    //Custom Reference Data
    public int? RefId { get; set; }
    public string RefIdStr { get; set; }
    public Dictionary<string, string> Meta { get; set; }
}

public class ApiKeysFeature : IPlugin, IConfigureServices, IRequiresSchema
{
    public string? ApiKeyPrefix = "ak-";
    public string? HttpHeaderName = "x-api-key";
    public TimeSpan? CacheDuration = TimeSpan.FromMinutes(10);
    public Func<string>? ApiKeyGenerator { get; set; }
    public TimeSpan? DefaultExpiry { get; set; }

    private static ConcurrentDictionary<string, (AccessKey apiKey, DateTime dateTime)> Cache = new();

    public string GenerateApiKey() => ApiKeyGenerator != null 
        ? ApiKeyGenerator()
        : (ApiKeyPrefix ?? "") + Guid.NewGuid().ToString("N");

    public void Register(IAppHost appHost)
    {
        appHost.GlobalRequestFiltersAsync.Add(RequestFilterAsync);
    }

    public async Task RequestFilterAsync(IRequest req, IResponse res, object requestDto)
    {
        var apiKeyId = (HttpHeaderName != null ? req.GetHeader(HttpHeaderName) : null) ?? req.GetBearerToken();
        if (string.IsNullOrEmpty(apiKeyId)) 
            return;
        if (ApiKeyPrefix != null && !apiKeyId.StartsWith(ApiKeyPrefix))
            return;
        if (CacheDuration != null && Cache.TryGetValue(apiKeyId, out var entry))
        {
            if (entry.dateTime + CacheDuration > DateTime.UtcNow)
            {
                req.Items[Keywords.ApiKey] = entry.apiKey;
                return;
            }
            Cache.TryRemove(apiKeyId, out _);
        }

        using var db = await req.TryResolve<IDbConnectionFactory>().OpenDbConnectionAsync();
        var apiKey = await db.SingleByIdAsync<AccessKey>(apiKeyId);
        if (apiKey != null)
        {
            req.Items[Keywords.ApiKey] = apiKey;
            if (CacheDuration != null)
            {
                Cache[apiKeyId] = (apiKey, DateTime.UtcNow);
            }
        }
    }

    public void InitSchema()
    {
        using var db = HostContext.AppHost.GetDbConnection();
        InitSchema(db);
    }

    public void InitSchema(IDbConnection db)
    {
        db.CreateTableIfNotExists<AccessKey>();
    }
    
    public void InitKey(AccessKey to)
    {
        if (string.IsNullOrEmpty(to.Id))
            to.Id = GenerateApiKey();
        if (string.IsNullOrEmpty(to.VisibleKey))
            to.VisibleKey = to.Id[..(ApiKeyPrefix?.Length ?? 0)] + "***" + to.Id[^3..];
        if (string.IsNullOrEmpty(to.Name))
            to.Name = $"{DateTime.UtcNow.Year}-{DateTime.UtcNow.Month:00}";
        to.CreatedDate = DateTime.UtcNow;
        if (DefaultExpiry != null)
            to.ExpiryDate = DateTime.UtcNow.Add(DefaultExpiry.Value);
    }

    public void InsertAll(IDbConnection db, List<AccessKey> apiKeys)
    {
        apiKeys.ForEach(InitKey);
        db.InsertAll(apiKeys);
    }

    public async Task InsertAllAsync(IDbConnection db, List<AccessKey> apiKeys)
    {
        apiKeys.ForEach(InitKey);
        await db.InsertAllAsync(apiKeys);
    }

    public void Configure(IServiceCollection services)
    {
        ServiceStackHost.InitOptions.ScriptContext.ScriptMethods.Add(new ValidationScriptMethods());
    }
}

public static class ApiKeysExtensions
{
    public static AccessKey? GetAccessKey(this IRequest? req) => req.GetItem(Keywords.ApiKey) as AccessKey;
    public static string? GetAccessKeyUser(this IRequest? req) => X.Map(GetAccessKey(req), x => x.UserName ?? x.UserId);
}

public class ValidationScriptMethods : ScriptMethods
{
    public ITypeValidator ApiKey() => ApiKeyValidator.Instance;
}

public class ApiKeyValidator()
    : TypeValidator(nameof(HttpStatusCode.Unauthorized), DefaultErrorMessage, 401), IAuthTypeValidator
{
    public static string DefaultErrorMessage { get; set; } = ErrorMessages.NotAuthenticated;
    public static ApiKeyValidator Instance { get; } = new();
    ConcurrentDictionary<string, AccessKey> ValidApiKeys = new();

    public override async Task<bool> IsValidAsync(object dto, IRequest request)
    {
        var bearerToken = request.GetBearerToken();
        if (bearerToken != null)
        {
            if (ValidApiKeys.TryGetValue(bearerToken, out var apiKey))
            {
                request.Items[Keywords.ApiKey] = apiKey;
                return true;
            }
            
            using var db = request.TryResolve<IDbConnectionFactory>().OpenDbConnection();
            apiKey = await db.SingleByIdAsync<AccessKey>(bearerToken);
            if (apiKey != null)
            {
                ValidApiKeys[bearerToken] = apiKey;
                request.Items[Keywords.ApiKey] = apiKey;
                return true;
            }
        }
        return false;
    }
}
