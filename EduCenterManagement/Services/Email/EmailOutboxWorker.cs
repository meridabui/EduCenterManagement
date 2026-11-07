using EduCenterManagement.Services.Reminders;
using Microsoft.Extensions.Options;

namespace EduCenterManagement.Services.Email;

public sealed class EmailOutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ClassReminderSettings _settings;
    private readonly ILogger<EmailOutboxWorker> _logger;

    public EmailOutboxWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ClassReminderSettings> options,
        ILogger<EmailOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Class reminder email sending is disabled.");
            return;
        }

        var interval = TimeSpan.FromSeconds(
            Math.Clamp(_settings.SenderIntervalSeconds, 10, 3600));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider
                    .GetRequiredService<EmailOutboxProcessor>();
                var processed = await processor.ProcessDueAsync(stoppingToken);

                if (processed > 0)
                {
                    _logger.LogInformation(
                        "Processed {Count} due class reminder email(s).",
                        processed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Processing class reminder emails failed.");
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
