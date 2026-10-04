using Ryzom.Engine.Ecology;

namespace Ryzom.Server.Services;

public class WorldTickHostedService : BackgroundService
{
    private readonly EcologySimulation _ecology;
    private readonly ILogger<WorldTickHostedService> _logger;
    private const int TICK_INTERVAL_MS = 50; // 20 Hz tick loop

    public WorldTickHostedService(EcologySimulation ecology, ILogger<WorldTickHostedService> logger)
    {
        _ecology = ecology;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ryzom Modern World Tick Loop started at 20 Hz (50ms ticks).");
        var periodicTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(TICK_INTERVAL_MS));

        var lastTickTime = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested && await periodicTimer.WaitForNextTickAsync(stoppingToken))
        {
            var now = DateTime.UtcNow;
            float deltaSeconds = (float)(now - lastTickTime).TotalSeconds;
            lastTickTime = now;

            try
            {
                _ecology.Tick(deltaSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in world tick iteration.");
            }
        }

        _logger.LogInformation("Ryzom Modern World Tick Loop stopped.");
    }
}
