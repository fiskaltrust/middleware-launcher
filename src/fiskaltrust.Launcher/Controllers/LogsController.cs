using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.ProcessHost;
using fiskaltrust.storage.serialization.V0;
using Microsoft.AspNetCore.Mvc;

namespace fiskaltrust.Launcher.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly ILogger<LogsController> _logger;
    private readonly LauncherConfiguration _launcherConfiguration;
    private readonly ftCashBoxConfiguration _cashboxConfiguration;

    public LogsController(ILogger<LogsController> logger, LauncherConfiguration launcherConfiguration, ftCashBoxConfiguration cashboxConfiguration)
    {
        _logger = logger;
        _launcherConfiguration = launcherConfiguration;
        _cashboxConfiguration = cashboxConfiguration;
    }

    [HttpGet()]
    public IEnumerable<object> GetLogs()
    {
        var ids = new List<(Guid, string)> {
            (_launcherConfiguration.CashboxId!.Value, "fiskaltrust.Launcher"),
        };

        
        ids.AddRange(_cashboxConfiguration.ftQueues.Select(p => (p.Id, p.Package)));
        ids.AddRange(_cashboxConfiguration.ftSignaturCreationDevices.Select(p => (p.Id, p.Package)));
        ids.AddRange(_cashboxConfiguration.helpers.Select(p => (p.Id, p.Package)));

        return ids.Select(i => new { Id = i.Item1, Package = i.Item2 });
    }

    [HttpGet("{id}")]
    public string GetLog(Guid id)
    {
        var tmp = Directory.GetFiles(_launcherConfiguration.LogFolder!);
        var logFile = Directory.GetFiles(_launcherConfiguration.LogFolder!).Where(f => f.Contains(id.ToString())).OrderBy(f => f).Last();

        using var fileStream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var streamReader = new StreamReader(fileStream);
        return streamReader.ReadToEnd();
    }
}
