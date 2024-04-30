using AiServer.ServiceInterface;

[assembly: HostingStartup(typeof(ConfigureAuth))]

namespace AiServer;

public class ConfigureAuth : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices(services => {
            services.AddPlugin(new AuthFeature(() => new AuthUserSession(), [
                new AuthSecretAuthProvider(),
            ]));
            
            services.AddPlugin(new ApiKeysFeature());
        })
        .ConfigureAppHost(appHost =>
        {
            using var db = HostContext.AppHost.GetDbConnection();
            appHost.GetPlugin<ApiKeysFeature>().InitSchema(db);
        });
}
