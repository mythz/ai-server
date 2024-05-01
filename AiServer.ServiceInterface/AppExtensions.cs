﻿using System.Data;
using AiServer.ServiceModel;
using AiServer.ServiceModel.Types;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.Messaging;
using ServiceStack.Text;

namespace AiServer;

public static class AppExtensions
{
    public static System.Text.Json.JsonSerializerOptions SystemJsonOptions = new(TextConfig.SystemJsonOptions)
    {
        WriteIndented = true
    };
    public static string ToSystemJson<T>(this T obj) => System.Text.Json.JsonSerializer.Serialize(obj, SystemJsonOptions); 

    public static string GetNamedMonthDb(this IDbConnectionFactory dbFactory) => dbFactory.GetNamedMonthDb(DateTime.UtcNow); 
    public static string GetNamedMonthDb(this IDbConnectionFactory dbFactory, DateTime createdDate) => 
        $"{createdDate.Year}-{createdDate.Month:00}";

    public static IDbConnection GetMonthDbConnection(this IDbConnectionFactory dbFactory, DateTime? createdDate = null) => 
        HostContext.AppHost.GetDbConnection(dbFactory.GetNamedMonthDb(createdDate ?? DateTime.UtcNow));

    public static EmptyResponse PublishAndReturn<T>(this IMessageProducer mq, T request)
    {
        mq.Publish(request);
        return new EmptyResponse();
    }

    public static string? GetBody(this OpenAiChatResponse? response)
    {
        return response?.Choices?.FirstOrDefault()?.Message?.Content;
    }
    
    public static string GetTableName(TaskType taskType) => taskType switch  {
        TaskType.OpenAiChat => nameof(OpenAiChatTask),
        _ => throw new NotSupportedException($"Unsupported TaskType: {taskType}")
    };

    public static OpenAiChatCompleted ToOpenAiChatCompleted(this OpenAiChatTask from)
    {
        var to = from.CreateNew<OpenAiChatCompleted>();
        to.Request = from.Request;
        to.Response = from.Response;
        return to;
    }

    public static OpenAiChatFailed ToOpenAiChatFailed(this OpenAiChatTask from)
    {
        var to = from.CreateNew<OpenAiChatFailed>();
        to.Request = from.Request;
        to.FailedDate = DateTime.UtcNow;
        return to;
    }

    public static T CreateNew<T>(this TaskBase x) where T : TaskBase, new()
    {
        var to = new T
        {
            Id = x.Id,
            Model = x.Model,
            Provider = x.Provider,
            RefId = x.RefId,
            ReplyTo = x.ReplyTo,
            CreatedDate = x.CreatedDate,
            CreatedBy = x.CreatedBy,
            Worker = x.Worker,
            WorkerIp = x.WorkerIp,
            RequestId = x.RequestId,
            StartedDate = x.StartedDate,
            CompletedDate = x.CompletedDate,
            DurationMs = x.DurationMs,
            RetryLimit = x.RetryLimit,
            NotificationDate = x.NotificationDate,
            ErrorCode = x.ErrorCode,
            Error = x.Error,
        };
        return to;
    }
}