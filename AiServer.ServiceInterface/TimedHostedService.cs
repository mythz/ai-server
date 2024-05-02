using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceStack.Messaging;
using AiServer.ServiceModel.Types;

namespace AiServer.ServiceInterface;

public class TimedHostedService(ILogger<TimedHostedService> logger, IMessageService mqServer) : IHostedService, IDisposable
{
    private int EverySecs = 60;
    private int executionCount = 0;
    private Timer? timer = null;

    public Task StartAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Timed Hosted Service running.");

        timer = new Timer(DoWork, null, TimeSpan.Zero,
            TimeSpan.FromSeconds(EverySecs));

        return Task.CompletedTask;
    }

    private void DoWork(object? state)
    {
        var count = Interlocked.Increment(ref executionCount);
        logger.LogInformation("Timed Hosted Service is working. Count: {Count}", count);
        
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogInformation("MQ Worker running at: {Stats}\n", mqServer.GetStatsDescription());
        
        var frequentTasks = new PeriodicTasks { PeriodicFrequency = PeriodicFrequency.Frequent };
        using var mq = mqServer.MessageFactory.CreateMessageProducer();
        mq.Publish(new AppDbWrites { PeriodicTasks = frequentTasks });
        // mq.Publish(new ExecutorTasks { PeriodicTasks = frequentTasks });

        if (count % 60 == 0)
        {
            var hourlyTasks = new PeriodicTasks { PeriodicFrequency = PeriodicFrequency.Hourly };
            mq.Publish(new AppDbWrites { PeriodicTasks = hourlyTasks });
            // mq.Publish(new ExecutorTasks { PeriodicTasks = hourlyTasks });
        }

        if (count % (24 * 60) == 0)
        {
            var dailyTasks = new PeriodicTasks { PeriodicFrequency = PeriodicFrequency.Daily };
            mq.Publish(new AppDbWrites { PeriodicTasks = dailyTasks });
            // mq.Publish(new ExecutorTasks { PeriodicTasks = dailyTasks });
        }

        if (count % (30 * 24 * 60) == 0)
        {
            var monthlyTasks = new PeriodicTasks { PeriodicFrequency = PeriodicFrequency.Monthly };
            mq.Publish(new AppDbWrites { PeriodicTasks = monthlyTasks });
            // mq.Publish(new ExecutorTasks { PeriodicTasks = monthlyTasks });
        }
    }

    public Task StopAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Timed Hosted Service is stopping.");

        timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        timer?.Dispose();
    }
}
