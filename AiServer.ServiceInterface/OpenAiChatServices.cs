﻿using AiServer.ServiceInterface.Commands;
using AiServer.ServiceModel;
using Microsoft.AspNetCore.Http;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.Messaging;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface;

public class OpenAiChatServices(
    IDbConnectionFactory dbFactory, 
    IMessageProducer mq,
    IAutoQueryDb autoQuery,
    AppData appData) : Service
{
    public async Task<object> Any(QueryCompletedChatTasks query)
    {
        using var db = dbFactory.GetMonthDbConnection(query.Db);
        var q = autoQuery.CreateQuery(query, base.Request, db);
        return await autoQuery.ExecuteAsync(query, q, base.Request, db);    
    }

    public async Task<object> Any(QueryFailedChatTasks query)
    {
        using var db = dbFactory.GetMonthDbConnection(query.Db);
        var q = autoQuery.CreateQuery(query, base.Request, db);
        return await autoQuery.ExecuteAsync(query, q, base.Request, db);    
    }

    public async Task<object> Any(CreateOpenAiChat request)
    {
        request.RefId ??= Guid.NewGuid().ToString("N");
        var task = request.ConvertTo<OpenAiChatTask>();
        task.Id = appData.GetNextChatTaskId();
        task.CreatedBy = Request.GetAccessKeyUser() ?? "System";
        
        mq.Publish(new AppDbWrites {
            CreateOpenAiChatTask = task,
        });

        return new CreateOpenAiChatResponse
        {
            Id = task.Id,
            RefId = request.RefId,
        };
    }

    public async Task<object> Any(FetchOpenAiChatRequests request)
    {
        if (request.Models == null || request.Models.Length == 0)
            throw new ArgumentNullException(nameof(request.Models));
        
        var aspReq = (HttpRequest)Request!.OriginalRequest;
        var requestId = aspReq.HttpContext.TraceIdentifier;
        var provider = request.Provider;
        var take = request.Take ?? 1;
        var models = request.Models;

        using var db = dbFactory.OpenDbConnection();

        var q = db.From<OpenAiChatTask>()
            .Where(x => x.StartedDate == null && (x.Provider == null || x.Provider == request.Provider));
        if (request.Models.Length == 1)
            q.Where(x => x.Model == models[0]);
        else
            q.Where(x => models.Contains(x.Model));
        var startedAt = DateTime.UtcNow;

        var hasTasks = await db.ExistsAsync(q);
        if (!hasTasks)
            return new FetchOpenAiChatRequestsResponse { Results = Array.Empty<OpenAiChatRequest>() };

        var lastCounter = ReserveOpenAiChatTaskCommand.Counter;
        mq.Publish(new AppDbWrites {
            ReserveOpenAiChatTask = new ReserveOpenAiChatTask {
                RequestId = requestId,
                Models = models,
                Provider = provider,
                Take = take,
            },
        });

        while (true)
        {
            if (DateTime.UtcNow - startedAt > TimeSpan.FromSeconds(180))
                throw HttpError.Conflict("Unable to fetch next tasks");
            
            // Wait for writer thread to reserve requested tasks for our request
            var attempts = 0;
            while (attempts++ <= 20)
            {
                if (ReserveOpenAiChatTaskCommand.Counter == lastCounter)
                    await Task.Delay(attempts);

                var currentCounter = ReserveOpenAiChatTaskCommand.Counter;
                if (currentCounter != lastCounter)
                {
                    lastCounter = currentCounter;
                    var results = db.Select(Db.From<OpenAiChatTask>().Where(x => x.RequestId == requestId));
                    if (results.Count > 0)
                    {
                        return new FetchOpenAiChatRequestsResponse {
                            Results = results.Select(x => new OpenAiChatRequest {
                                Id = x.Id,
                                Request = x.Request,
                            }).ToArray()
                        };
                    }
                }
            }
            
            hasTasks = await db.ExistsAsync(q);
            if (!hasTasks)
                return new FetchOpenAiChatRequestsResponse { Results = Array.Empty<OpenAiChatRequest>() };
        }
    }

    public object Any(OpenAiChatOperations request)
    {
        mq.Publish(new AppDbWrites {
            RequeueIncompleteTasks = request.RequeueIncompleteTasks == true
                ? new RequeueIncompleteTasks()
                : null,
        });
        
        return new EmptyResponse();
    }
}
