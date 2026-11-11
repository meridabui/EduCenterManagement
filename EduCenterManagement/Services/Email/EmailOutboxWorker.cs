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
        var interval = TimeSpan.FromSeconds(
            Math.Clamp(_settings.SenderIntervalSeconds, 10, 3600));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var confirmationPlanner = scope.ServiceProvider
                    .GetRequiredService<EnrollmentConfirmationPlannerService>();
                var confirmationProcessor = scope.ServiceProvider
                    .GetRequiredService<EnrollmentConfirmationEmailProcessor>();

                var planned = await confirmationPlanner.PlanAsync(stoppingToken);
                var confirmations = await confirmationProcessor
                    .ProcessDueAsync(stoppingToken);
                var reminders = 0;

                if (_settings.Enabled)
                {
                    var reminderProcessor = scope.ServiceProvider
                        .GetRequiredService<EmailOutboxProcessor>();
                    reminders = await reminderProcessor.ProcessDueAsync(stoppingToken);
                }

                if (planned > 0 || confirmations > 0 || reminders > 0)
                {
                    _logger.LogInformation(
                        "Email outbox: planned {Planned} enrollment confirmation(s), " +
                        "processed {Confirmations} confirmation(s) and {Reminders} reminder(s).",
                        planned,
                        confirmations,
                        reminders);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Processing email outbox failed.");
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
