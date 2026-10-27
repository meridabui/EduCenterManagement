namespace EduCenterManagement.Services
{
    // BR16: định kỳ hủy Enrollment Pending quá 15 phút chưa thanh toán.
    public class EnrollmentExpiryWorker : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EnrollmentExpiryWorker> _logger;

        public EnrollmentExpiryWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<EnrollmentExpiryWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var service = scope.ServiceProvider
                        .GetRequiredService<IEnrollmentService>();

                    var count = await service.ExpireOverdueAsync(stoppingToken);

                    if (count > 0)
                    {
                        _logger.LogInformation(
                            "Released {Count} expired pending enrollment(s).", count);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Expire pending enrollments failed.");
                }

                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
