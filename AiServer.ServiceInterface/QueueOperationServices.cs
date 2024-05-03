﻿using ServiceStack;
using ServiceStack.Data;
using AiServer.ServiceModel;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface;

public class QueueOperationServices(AppData appData, IDbConnectionFactory dbFactory, IAutoQueryDb autoQuery) : Service
{
    public object Any(StopWorkers request)
    {
        appData.StopWorkers();
        return new EmptyResponse();
    }
    
    public async Task<object> Any(StartWorkers request)
    {
        using var db = await dbFactory.OpenDbConnectionAsync();
        appData.StartWorkers(db);
        return new EmptyResponse();
    }
    
    public async Task<object> Any(RestartWorkers request)
    {
        using var db = await dbFactory.OpenDbConnectionAsync();
        appData.RestartWorkers(db);
        return new EmptyResponse();
    }
    
    public object Any(ResetActiveProviders request)
    {
        appData.RestartWorkers(Db);
        MessageProducer.Publish(new AppDbWrites {
            ResetTaskQueue = new()
        });
        return new GetActiveProvidersResponse {
            Results = appData.ApiProviders
        };
    }

    public object Any(ChangeApiProviderStatus request)
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

    public async Task<object> Any(UpdateApiProvider request)
    {
        var result = await autoQuery.PartialUpdateAsync<ApiProvider>(request, base.Request);
        var worker = appData.ApiProviderWorkers.FirstOrDefault(x => x.Id == request.Id);
        worker?.Update(request);

        if (request.Enabled == true || request.Concurrency > 0)
        {
            MessageProducer.Publish(new QueueTasks {
                DelegateOpenAiChatTasks = new()
            });
        }
        
        return result;
    }

    public object Any(FirePeriodicTask request)
    {
        MessageProducer.Publish(new AppDbWrites
        {
            PeriodicTasks = new()
            {
                PeriodicFrequency = request.Frequency
            }
        });
        return new EmptyResponse();
    }
}
