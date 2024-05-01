﻿using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.Messaging;
using ServiceStack.OrmLite;
using AiServer.ServiceInterface.Executor;
using AiServer.ServiceModel.Types;

namespace AiServer.ServiceInterface.AppDb;

public class DelegateOpenAiChatTasks {}
public class DelegateOpenAiChatTasksCommand(ILogger<DelegateOpenAiChatTasksCommand> log, AppData appData, 
    IDbConnectionFactory dbFactory, IMessageProducer mq) : IAsyncCommand<DelegateOpenAiChatTasks>
{
    public long CheckIntervalSeconds { get; set; } = 10;
    
    private static long running = 0;
    public static bool Running => Interlocked.Read(ref running) > 0;
    
    private static long counter = 0;
    
    public int DelegatedCount { get; set; }
    
    public async Task ExecuteAsync(DelegateOpenAiChatTasks request)
    {
        if (Running)
            return;
        
        using var db = await dbFactory.OpenDbConnectionAsync();
        if (!await db.ExistsAsync(db.From<OpenAiChatTask>().Where(x => 
            x.RequestId == null && x.StartedDate == null && x.CompletedDate == null && appData.ActiveProviderModels.Contains(x.Model))))
        {
            return;
        }

        try
        {
            Interlocked.Increment(ref running);
            
            while (true)
            {
                foreach (var apiProvider in appData.ActiveProviders)
                {
                    // Don't assign more work to provider until their work queue is empty
                    var providerQueue = appData.GetProviderOpenAiChatQueue(apiProvider);
                    if (providerQueue.Count > 0)
                        continue;
                    
                    var requestId = Guid.NewGuid().ToString("N");
                    var models = apiProvider.Models.Select(x => x.Model).ToArray();
                    var pendingTasks = await db.ReserveNextTasksAsync(
                        requestId: requestId,
                        models: models,
                        provider: apiProvider.Name,
                        take: apiProvider.Concurrency);

                    DelegatedCount += pendingTasks;
                    if (pendingTasks > 0)
                    {
                        log.LogDebug("{Counter} Reserved and delegating {PendingTasks} to {Provider}",
                            ++counter, pendingTasks, apiProvider.Name);
                        providerQueue.Add(requestId);
                    }
                }

                if (!ExecuteOpenAiChatTasksCommand.Running)
                {
                    var hasWorkQueued = appData.OpenAiChatTasks.Any(x => x.Count > 0);
                    if (hasWorkQueued)
                    {
                        mq.Publish(new ExecutorTasks {
                            ExecuteOpenAiChatTasks = new()
                        });
                    }
                }
                
                var hasMoreTasksToDelegate = await db.ExistsAsync(db.From<OpenAiChatTask>()
                    .Where(x => x.RequestId == null && x.StartedDate == null && x.CompletedDate == null && appData.ActiveProviderModels.Contains(x.Model)));
                if (!hasMoreTasksToDelegate)
                {
                    log.LogInformation("All OpenAI Chat Tasks have been delegated, exiting...");
                    return;
                }
                
                // Give time for workers to complete their tasks before trying to delegate more work to them
                log.LogInformation("Waiting {WaitSeconds} seconds before delegating more tasks...", CheckIntervalSeconds);
                await Task.Delay(TimeSpan.FromSeconds(CheckIntervalSeconds));
            }
        }
        finally
        {
            Interlocked.Decrement(ref running);
        }
    }
}
