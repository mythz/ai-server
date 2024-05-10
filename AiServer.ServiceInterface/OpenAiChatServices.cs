using AiServer.ServiceInterface.AppDb;
using AiServer.ServiceModel;
using AiServer.ServiceModel.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.Messaging;
using ServiceStack.OrmLite;
using OpenAiChatCompleted = AiServer.ServiceModel.Types.OpenAiChatCompleted;

namespace AiServer.ServiceInterface;

public class OpenAiChatServices(
    ILogger<OpenAiChatServices> log,
    IDbConnectionFactory dbFactory, 
    IMessageProducer mq,
    IAutoQueryDb autoQuery,
    AppData appData) : Service
{
    public async Task<object> Any(GetOpenAiChat request)
    {
        var q = Db.From<TaskSummary>();
        if (request.Id != null)
            q.Where(x => x.Id == request.Id);
        else if (request.RefId != null)
            q.Where(x => x.RefId == request.RefId);
        else
            throw new ArgumentNullException(nameof(request.Id));
        
        var summary = await Db.SingleAsync(q);
        if (summary != null)
        {
            var activeTask = await Db.SingleByIdAsync<OpenAiChatTask>(summary.Id);
            if (activeTask != null)
                return new GetOpenAiChatResponse { Result = activeTask };

            using var monthDb = dbFactory.GetMonthDbConnection(summary.CreatedDate);
            var completedTask = await monthDb.SingleByIdAsync<OpenAiChatCompleted>(summary.Id);
            if (completedTask != null)
                return new GetOpenAiChatResponse { Result = completedTask.ConvertTo<OpenAiChatTask>() };

            var failedTask = await monthDb.SingleByIdAsync<OpenAiChatFailed>(summary.Id);
            if (failedTask != null)
                return new GetOpenAiChatResponse { Result = failedTask.ConvertTo<OpenAiChatTask>() };
        }
        throw HttpError.NotFound("Task not found");
    }
    
    public async Task<object> Any(QueryCompletedChatTasks query)
    {
        using var dbMonth = HostContext.AppHost.GetDbConnection(query.Db ?? dbFactory.GetNamedMonthDb(DateTime.UtcNow));
        var q = autoQuery.CreateQuery(query, base.Request, dbMonth);
        return await autoQuery.ExecuteAsync(query, q, base.Request, dbMonth);    
    }

    public async Task<object> Any(QueryFailedChatTasks query)
    {
        using var dbMonth = HostContext.AppHost.GetDbConnection(query.Db ?? dbFactory.GetNamedMonthDb(DateTime.UtcNow));
        var q = autoQuery.CreateQuery(query, base.Request, dbMonth);
        return await autoQuery.ExecuteAsync(query, q, base.Request, dbMonth);    
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

    public object Any(ChatOperations request)
    {
        mq.Publish(new AppDbWrites {
            RequeueIncompleteTasks = request.RequeueIncompleteTasks == true
                ? new()
                : null,
            ResetTaskQueue = request.ResetTaskQueue == true
                ? new()
                : null,
        });
        
        return new EmptyResponse();
    }

    public object Any(ChatFailedTasks request)
    {
        mq.Publish(new AppDbWrites {
            ResetFailedTasks = request.ResetErrorState == true
                ? new()
                : null,
            RequeueFailedTasks = request.RequeueFailedTaskIds is { Count: > 0 }
                ? new() { Ids = request.RequeueFailedTaskIds }
                : null,
        });
        
        return new EmptyResponse();
    }
    
    public async Task<object>  Any(ChatNotifyCompletedTasks request)
    {
        var taskSummary = await Db.SelectByIdsAsync<TaskSummary>(request.Ids);
        var monthDbs = taskSummary.GroupBy(x => dbFactory.GetNamedMonthDb(x.CreatedDate.Date));
        var to = new ChatNotifyCompletedTasksResponse();

        foreach (var entry in monthDbs)
        {
            using var monthDb = await dbFactory.OpenDbConnectionAsync(entry.Key);
            var taskIds = entry.Map(x => x.Id);
            var tasks = await monthDb.SelectByIdsAsync<OpenAiChatCompleted>(taskIds);
            foreach (var task in tasks)
            {
                try
                {
                    if (task.Response == null)
                    {
                        to.Errors[task.Id] = "Response Missing";
                        continue;
                    }
                    var json = task.Response.ToJson();
                    mq.Publish(new NotificationTasks
                    {
                        NotificationRequest = new()
                        {
                            Url = task.ReplyTo!,
                            ContentType = MimeTypes.Json,
                            Body = json,
                        },
                    });
                    to.Results.Add(task.Id);
                }
                catch (Exception e)
                {
                    to.Errors[task.Id] = e.Message;
                    log.LogError(e, "Error sending notification for {TaskId}: {Message}", task.Id, e.Message);
                }
            }
        }
        return to;
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
        Results = appData.ApiProviders
    };

    public async Task<object> Any(ChatApiProvider request)
    {
        var worker = appData.ApiProviderWorkers.FirstOrDefault(x => x.Name == request.Provider)
            ?? throw HttpError.NotFound("ApiProvider not found");

        var openAiRequest = request.Request;
        if (openAiRequest == null)
        {
            if (string.IsNullOrEmpty(request.Prompt))
                throw new ArgumentNullException(nameof(request.Prompt));
            
            openAiRequest = new OpenAiChat
            {
                Model = request.Model,
                Messages = [
                    new() { Role = "user", Content = request.Prompt },
                ],
                MaxTokens = 512,
                Stream = false,
            };
        }
        
        var chatProvider = worker.GetOpenAiProvider();
        var response = await chatProvider.ChatAsync(worker, openAiRequest);
        return response.Response;
    }

    public async Task<object> Any(CreateApiKey request)
    {
        var feature = AssertPlugin<ApiKeysFeature>();
        var apiKey = request.ConvertTo<ApiKeysFeature.ApiKey>();
        await feature.InsertAllAsync(Db, [apiKey]);
        return apiKey.ConvertTo<CreateApiKeyResponse>();
    }

    public object Any(GetApiWorkerStats request) => new GetApiWorkerStatsResponse
    {
        Results = appData.ApiProviderWorkers.Select(x => x.GetStats()).ToList()
    };
}
