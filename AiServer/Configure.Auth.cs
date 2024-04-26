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
        });
}
