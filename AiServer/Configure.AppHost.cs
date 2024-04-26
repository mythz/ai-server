using System.Data;
using AiServer.ServiceInterface;
using AiServer.ServiceModel;
using ServiceStack.Auth;
using ServiceStack.Data;
using ServiceStack.OrmLite;

[assembly: HostingStartup(typeof(AiServer.AppHost))]

namespace AiServer;

public class AppHost() : AppHostBase("AiServer"), IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices(services => {
            // Configure ASP.NET Core IOC Dependencies
            services.AddSingleton(AppData.Instance);
            InitOptions.ScriptContext.ScriptMethods.Add(new ValidationScriptMethods());
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
        SetConfig(new HostConfig {
            AdminAuthSecret = Environment.GetEnvironmentVariable("AUTH_SECRET") ?? "p@55wOrd",
        });
        
        using var db = ApplicationServices.GetRequiredService<IDbConnectionFactory>().OpenDbConnection();
        AppData.Instance.Init(db);
    }
}
