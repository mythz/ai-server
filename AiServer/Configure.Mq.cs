using AiServer.ServiceInterface;
using ServiceStack.Messaging;

[assembly: HostingStartup(typeof(AiServer.ConfigureMq))]

namespace AiServer;

/**
  Register Services you want available via MQ in your AppHost, e.g:
    var mqServer = appHost.Resolve<IMessageService>();
    mqServer.RegisterHandler<MyRequest>(ExecuteMessage);
*/
public class ConfigureMq : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices(services => {
            services.AddSingleton<IMessageService>(c => new BackgroundMqService());
            services.AddPlugin(new CommandsFeature());
        })
        .ConfigureAppHost(afterAppHostInit: appHost => {
            var mqService = appHost.Resolve<IMessageService>();

            //Register ServiceStack APIs you want to be able to invoke via MQ
            mqService.RegisterHandler<AppDbWrites>(appHost.ExecuteMessage);
            mqService.RegisterHandler<NotificationTasks>(appHost.ExecuteMessage);

            mqService.Start();
        });
}
