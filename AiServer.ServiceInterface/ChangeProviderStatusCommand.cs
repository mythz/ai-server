using System.Data;
using ServiceStack;
using ServiceStack.OrmLite;
using AiServer.ServiceModel;

namespace AiServer.ServiceInterface;

public class ChangeProviderStatus
{
    public string Name { get; set; }
    public DateTime? OfflineDate { get; set; }
}

public class ChangeProviderStatusCommand(AppData appData, IDbConnection db) : IAsyncCommand<ChangeProviderStatus>
{
    public async Task ExecuteAsync(ChangeProviderStatus request)
    {
        await db.UpdateOnlyAsync(() => new ApiProvider {
            OfflineDate = request.OfflineDate,
        }, where:x => x.Name == request.Name);
        
        var apiProvider = appData.ApiProviders.FirstOrDefault(x => x.Name == request.Name);
        if (apiProvider != null)
            apiProvider.OfflineDate = request.OfflineDate;
    }
}