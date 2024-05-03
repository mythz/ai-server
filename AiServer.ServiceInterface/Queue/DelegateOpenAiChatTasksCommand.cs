﻿using AiServer.ServiceInterface.AppDb;
using AiServer.ServiceInterface.Executor;
using AiServer.ServiceModel.Types;
using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.Messaging;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface.Queue;

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
        
        var activeWorkerModels = appData.GetActiveWorkerModels();
        using var db = await dbFactory.OpenDbConnectionAsync();
        if (!await db.ExistsAsync(db.From<OpenAiChatTask>().Where(x => 
                x.RequestId == null && x.StartedDate == null && x.CompletedDate == null && activeWorkerModels.Contains(x.Model))))
        {
            return;
        }

        try
        {
            Interlocked.Increment(ref running);
            
            while (true)
            {
                foreach (var apiWorker in appData.GetActiveWorkers())
                {
                    // Don't assign more work to provider until their work queue is empty
                    if (apiWorker.ChatQueueCount > 0)
                        continue;
                    
                    var requestId = appData.CreateRequestId();
                    var models = apiWorker.Models;
                    var pendingTasks = await db.ReserveNextTasksAsync(
                        requestId: requestId,
                        models: models,
                        provider: apiWorker.Name,
                        take: apiWorker.Concurrency);

                    DelegatedCount += pendingTasks;
                    if (pendingTasks > 0)
                    {
                        log.LogDebug("[Chat][{Provider}] {Counter}: Reserved and delegating {PendingTasks} tasks",
                            ++counter, apiWorker.Name, pendingTasks);
                        apiWorker.AddToChatQueue(requestId);
                    }
                }

                if (!ExecuteOpenAiChatTasksCommand.Running)
                {
                    var hasWorkQueued = appData.HasAnyChatTasksQueued();
                    if (hasWorkQueued)
                    {
                        mq.Publish(new ExecutorTasks {
                            ExecuteOpenAiChatTasks = new()
                        });
                    }
                }
                
                var hasMoreTasksToDelegate = await db.ExistsAsync(db.From<OpenAiChatTask>()
                    .Where(x => x.RequestId == null && x.StartedDate == null && x.CompletedDate == null && activeWorkerModels.Contains(x.Model)));
                if (!hasMoreTasksToDelegate)
                {
                    log.LogInformation("[Chat] All tasks have been delegated, exiting...");
                    return;
                }
                
                // Give time for workers to complete their tasks before trying to delegate more work to them
                log.LogInformation("[Chat] Waiting {WaitSeconds} seconds before delegating more tasks...", CheckIntervalSeconds);
                await Task.Delay(TimeSpan.FromSeconds(CheckIntervalSeconds));
            }
        }
        catch (TaskCanceledException)
        {
            log.LogInformation("[Chat] Delegating tasks was cancelled, exiting...");
        }
        finally
        {
            Interlocked.Decrement(ref running);
        }
    }
}
