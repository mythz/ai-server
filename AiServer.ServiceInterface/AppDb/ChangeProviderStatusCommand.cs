using System.Data;
using AiServer.ServiceModel;
using ServiceStack;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface.AppDb;

public class ChangeProviderStatus
{
    public string Name { get; set; }
    public DateTime? OfflineDate { get; set; }
}

[Tag(Tags.Database)]
public class ChangeProviderStatusCommand(AppData appData, IDbConnection db) : IAsyncCommand<ChangeProviderStatus>
{
    public async Task ExecuteAsync(ChangeProviderStatus request)
    {
        await db.UpdateOnlyAsync(() => new ApiProvider {
            OfflineDate = request.OfflineDate,
        }, where:x => x.Name == request.Name);
        
        var apiProvider = appData.ApiProviderWorkers.FirstOrDefault(x => x.Name == request.Name);
        if (apiProvider != null)
            apiProvider.IsOffline = request.OfflineDate != null;
    }
}
