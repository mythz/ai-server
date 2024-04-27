﻿using System.Data;
using AiServer.ServiceModel;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface.Commands;

public class CompleteOpenAiChatCommand(IDbConnection db) : IAsyncCommand<CompleteOpenAiChat>
{
    public async Task ExecuteAsync(CompleteOpenAiChat request)
    {
        await db.UpdateOnlyAsync(() => new OpenAiChatTask
        {
            Provider = request.Provider,
            DurationMs = request.DurationMs,
            Response = request.Response,
            CompletedDate = DateTime.UtcNow,
        }, where: x => x.Id == request.Id);
    }
}