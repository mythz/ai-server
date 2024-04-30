using AiServer.ServiceInterface.AppDb;
using AiServer.ServiceModel;
using AiServer.ServiceModel.Types;
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
        if (request.Request == null)
            throw new ArgumentNullException(nameof(request.Request));
        
        var model = request.Request.Model;
        if (!await Db.ExistsAsync<ApiModel>(x => x.Name == model))
            throw HttpError.NotFound($"Model {model} not found");
        
        request.RefId ??= Guid.NewGuid().ToString("N");
        var task = request.ConvertTo<OpenAiChatTask>();
        task.Id = appData.GetNextChatTaskId();
        task.Model = model;
        task.CreatedBy = Request.GetApiKeyUser() ?? "System";
        
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
                                Model = x.Model,
                                Provider = x.Provider,
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

    public async Task<object> Any(CompleteOpenAiChat request)
    {
        mq.Publish(new AppDbWrites {
            CompleteOpenAiChat = request,
        });

        var task = await Db.SingleByIdAsync<OpenAiChatTask>(request.Id);
        if (task?.ReplyTo != null)
        {
            var json = request.Response.ToJson();
            mq.Publish(new NotificationTasks {
                NotificationRequest = new() {
                    Url = task.ReplyTo,
                    ContentType = MimeTypes.Json,
                    Body = json,
                    CompleteNotification = new() {
                        Type = TaskType.OpenAiChat,
                        Id = task.Id,
                    },
                },
            });
        }
        return new EmptyResponse();
    }
    
    public object Any(GetActiveProviders request) => new GetActiveProvidersResponse
    {
        Results = appData.ActiveProviders
    };

    public object Any(ResetActiveProviders request)
    {
        appData.ResetApiProviders(Db);
        return new GetActiveProvidersResponse {
            Results = appData.ActiveProviders
        };
    }

    public async Task<object> Any(ChatApiProvider request)
    {
        var apiProvider = appData.ApiProviders.FirstOrDefault(x => x.Name == request.Provider)
            ?? throw HttpError.NotFound("ApiProvider not found");
        
        var chatProvider = apiProvider.GetOpenAiProvider();
        var response = await chatProvider.ChatAsync(apiProvider, request.Request);
        return response.Response;
    }

    public async Task<object> Any(ChangeApiProviderStatus request)
    {
        var apiProvider = appData.ApiProviders.FirstOrDefault(x => x.Name == request.Provider)
            ?? throw HttpError.NotFound("ApiProvider not found");
        
        DateTime? offlineDate = request.Online ? null : DateTime.UtcNow;
        apiProvider.OfflineDate = offlineDate;
        
        MessageProducer.Publish(new AppDbWrites
        {
            RecordOfflineProvider = new()
            {
                Name = apiProvider.Name,
                OfflineDate = offlineDate,
            }
        });
        return new StringResponse
        {
            Result = offlineDate == null 
                ? $"{apiProvider.Name} is back online" 
                : $"{apiProvider.Name} was taken offline"
        };
    }

    public async Task<object> Any(CreateApiKey request)
    {
        var feature = AssertPlugin<ApiKeysFeature>();
        var apiKey = request.ConvertTo<ApiKeysFeature.ApiKey>();
        await feature.InsertAllAsync(Db, [apiKey]);
        return apiKey.ConvertTo<CreateApiKeyResponse>();
    }
}
