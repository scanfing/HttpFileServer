using Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Core.Service;

public class StartupService
{
    private readonly IAutoStartService _autoStartService;
    private readonly ILogger<StartupService> _logger;

    public StartupService(IAutoStartService autoStartService, ILogger<StartupService> logger)
    {
        _autoStartService = autoStartService;
        _logger = logger;
    }

    public void ApplyUserSetting(bool enable)
    {
        if (enable && !_autoStartService.IsEnabled())
        {
            _autoStartService.Enable();
        }
        else if (!enable && _autoStartService.IsEnabled())
        {
            _autoStartService.Disable();
        }
    }
}