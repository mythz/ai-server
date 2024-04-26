using System.Data;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.Messaging;

namespace AiServer;

public static class AppExtensions
{
    public static string GetNamedMonthDb(string? monthDb = null) => 
        monthDb ?? $"{DateTime.UtcNow.Year}-{DateTime.UtcNow.Month:00}";
    
    public static string GetNamedMonthDb(this IDbConnectionFactory dbFactory, string? monthDb = null) => 
        GetNamedMonthDb(monthDb);

    public static IDbConnection GetMonthDbConnection(this IDbConnectionFactory dbFactory, string? monthDb = null) => 
        HostContext.AppHost.GetDbConnection(GetNamedMonthDb(monthDb));

    public static EmptyResponse PublishAndReturn<T>(this IMessageProducer mq, T request)
    {
        mq.Publish(request);
        return new EmptyResponse();
    }
}