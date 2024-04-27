﻿using System.Data;
using AiServer.ServiceModel;
using Microsoft.Extensions.Logging;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface.Commands;

public class CreateOpenAiChatTaskCommand(ILogger<CreateOpenAiChatTaskCommand> log, IDbConnection db) : IAsyncCommand<OpenAiChatTask>
{
    public async Task ExecuteAsync(OpenAiChatTask request)
    {
        request.CreatedDate = DateTime.UtcNow;
        request.RefId ??= Guid.NewGuid().ToString("N");

        using var dbTrans = db.OpenTransaction();
        await db.InsertAsync(request);
        await db.InsertAsync(new TaskSummary
        {
            Id = request.Id,
            Type = TaskType.OpenAiChat,
            Model = request.Model,
            Provider = request.Provider,
            RefId = request.RefId,
        });
        dbTrans.Commit();
    }
}