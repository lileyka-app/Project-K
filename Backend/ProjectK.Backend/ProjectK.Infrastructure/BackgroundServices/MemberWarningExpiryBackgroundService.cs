using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectK.Common.Contracts.MemberModule;

namespace ProjectK.Infrastructure.BackgroundServices;

public class MemberWarningExpiryBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MemberWarningExpiryBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);

    public MemberWarningExpiryBackgroundService(IServiceProvider serviceProvider, ILogger<MemberWarningExpiryBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Member Warning Expiry Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireWarningsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing Member Warning Expiry Service.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Member Warning Expiry Service is stopping.");
    }

    private async Task ExpireWarningsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IMemberUnitOfWork>();

        var now = DateTime.UtcNow;

        var expiredWarningsCount = await unitOfWork.MemberWarnings.ExpireActiveWarningsAsync(now, cancellationToken);

        if (expiredWarningsCount > 0)
        {
            _logger.LogInformation("Expired {Count} member warnings.", expiredWarningsCount);
        }
    }
}
