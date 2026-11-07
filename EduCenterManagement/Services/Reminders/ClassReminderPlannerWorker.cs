using Microsoft.Extensions.Options;

namespace EduCenterManagement.Services.Reminders;

public sealed class ClassReminderPlannerWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ClassReminderSettings _settings;
    private readonly ILogger<ClassReminderPlannerWorker> _logger;

    public ClassReminderPlannerWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ClassReminderSettings> options,
        ILogger<ClassReminderPlannerWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Class reminder planning is disabled.");
            return;
        }

        var interval = TimeSpan.FromMinutes(
            Math.Clamp(_settings.PlannerIntervalMinutes, 1, 1440));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var planner = scope.ServiceProvider
                    .GetRequiredService<ClassReminderPlannerService>();
                var created = await planner.PlanAsync(stoppingToken);

                if (created > 0)
                {
                    _logger.LogInformation(
                        "Planned {Count} class reminder email(s).",
                        created);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Planning class reminder emails failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
