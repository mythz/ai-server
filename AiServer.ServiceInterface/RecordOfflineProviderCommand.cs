using System.Data;
using AiServer.ServiceModel;
using ServiceStack.OrmLite;

namespace AiServer.ServiceInterface;

public class RecordOfflineProvider
{
    public string Name { get; set; }
    public DateTime? OfflineDate { get; set; }
}

public class RecordOfflineProviderCommand(AppData appData, IDbConnection db) : IAsyncCommand<RecordOfflineProvider>
{
    public async Task ExecuteAsync(RecordOfflineProvider request)
    {
        await db.UpdateOnlyAsync(() => new ApiProvider {
            OfflineDate = request.OfflineDate,
        }, where:x => x.Name == request.Name);
        
        var apiProvider = appData.ApiProviders.FirstOrDefault(x => x.Name == request.Name);
        if (apiProvider != null)
            apiProvider.OfflineDate = request.OfflineDate;
    }
}