using fiskaltrust.Launcher.Configuration;
using fiskaltrust.storage.serialization.V0;
using Microsoft.AspNetCore.Mvc;

namespace fiskaltrust.Launcher.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigurationController : ControllerBase
{
    private readonly ILogger<ConfigurationController> _logger;
    private readonly LauncherConfiguration _launcherConfiguration;
    private readonly ftCashBoxConfiguration _cashboxConfiguration;

    public ConfigurationController(ILogger<ConfigurationController> logger, LauncherConfiguration launcherConfiguration, ftCashBoxConfiguration cashboxConfiguration)
    {
        _logger = logger;
        _launcherConfiguration = launcherConfiguration;
        _cashboxConfiguration = cashboxConfiguration;
    }

    [HttpGet("launcher")]
    public LauncherConfiguration GetLauncherConfiguration()
    {
        return _launcherConfiguration;
    }

    [HttpGet("cashbox")]
    public ftCashBoxConfiguration GetCashboxConfiguration()
    {
        return _cashboxConfiguration;
    }
}
