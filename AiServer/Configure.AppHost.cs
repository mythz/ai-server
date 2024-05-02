using System.Data;
using AiServer.ServiceInterface;
using AiServer.ServiceModel.Types;
using ServiceStack.Data;
using ServiceStack.OrmLite;

[assembly: HostingStartup(typeof(AiServer.AppHost))]

namespace AiServer;

public class AppHost() : AppHostBase("AiServer"), IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices((context,services) => {
            // Configure ASP.NET Core IOC Dependencies
            context.Configuration.GetSection(nameof(AppConfig)).Bind(AppConfig.Instance);
            services.AddSingleton(AppConfig.Instance);
            services.AddSingleton(AppData.Instance);
            
            services.AddSingleton<OpenAiProvider>();
            services.AddSingleton<GoogleOpenAiProvider>();
            services.AddSingleton<AiProviderFactory>();
        });

    public override IDbConnection GetDbConnection(string? namedConnection)
    {
        var dbFactory = Container.TryResolve<IDbConnectionFactory>();
        if (namedConnection == null) 
            return dbFactory.OpenDbConnection();
            
        return namedConnection.IndexOf('-') >= 0 && namedConnection.LeftPart('-').IsInt() 
            ? GetDbMonthConnection(dbFactory, HostingEnvironment.ContentRootPath, namedConnection) 
            : dbFactory.OpenDbConnection(namedConnection);
    }

    public static IDbConnection GetDbMonthConnection(IDbConnectionFactory dbFactory, string contentDir, string monthDb)
    {
        var dataSource = $"App_Data/{monthDb}/app.db";
        var monthDbPath = Path.Combine(contentDir, dataSource);

        if (!File.Exists(monthDbPath))
            Path.GetDirectoryName(monthDbPath).AssertDir();

        if (!OrmLiteConnectionFactory.NamedConnections.ContainsKey(monthDb))
            dbFactory.RegisterConnection(monthDb, $"DataSource={dataSource};Cache=Shared", SqliteDialect.Provider);

        var db = dbFactory.OpenDbConnection(monthDb);
        db.CreateTableIfNotExists<OpenAiChatCompleted>();
        db.CreateTableIfNotExists<OpenAiChatFailed>();

        return db;
    }

    public override void Configure()
    {
        // Configure ServiceStack, Run custom logic after ASP.NET Core Startup
        var authSecret = Environment.GetEnvironmentVariable("AUTH_SECRET") ?? AppConfig.Instance.AuthSecret;
        SetConfig(new HostConfig {
            AdminAuthSecret = authSecret,
        });
        
        AiProviderFactory.Instance = ApplicationServices.GetRequiredService<AiProviderFactory>();
        using var db = ApplicationServices.GetRequiredService<IDbConnectionFactory>().OpenDbConnection();
        AppData.Instance.Init(AiProviderFactory.Instance, db);

        // Increase timeout on all HttpClient requests
        var existingClientFactory = HttpUtils.CreateClient; 
        HttpUtils.CreateClient = () =>
        {
            var client = existingClientFactory();
            client.Timeout = TimeSpan.FromSeconds(180);
            return client;
        };

    }
}
