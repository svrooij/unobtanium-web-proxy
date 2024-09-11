namespace Unobtanium.Web.Proxy.Examples.WorkerTemplate;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ProxyServer _proxyServer;

    public Worker ( ILogger<Worker> logger, ProxyServer proxyServer )
    {
        _logger = logger;
        _proxyServer = proxyServer;
    }

    public override async Task StartAsync ( CancellationToken cancellationToken )
    {
        //await base.StartAsync(cancellationToken);
        await _proxyServer.StartAsync(cancellationToken: cancellationToken);
    }

    protected override async Task ExecuteAsync ( CancellationToken stoppingToken )
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }
            await Task.Delay(5000, stoppingToken);
        }
    }

    public override Task StopAsync ( CancellationToken cancellationToken )
    {
        _proxyServer.Stop();
        return base.StopAsync(cancellationToken);
    }
    public override void Dispose ()
    {
        _proxyServer.Dispose();
        base.Dispose();
    }
}
