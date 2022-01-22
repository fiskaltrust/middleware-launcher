using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.ProcessHost;
using fiskaltrust.storage.serialization.V0;
using Microsoft.AspNetCore.Mvc;

namespace fiskaltrust.Launcher.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MonarchsController : ControllerBase
{
    private readonly ILogger<MonarchsController> _logger;
    private readonly Dictionary<Guid, ProcessHostMonarch> _monarchs;

    public MonarchsController(ILogger<MonarchsController> logger, ProcessHostMonarcStartup monarchStartup)
    {
        _monarchs = monarchStartup.Hosts;
        _logger = logger;
    }

    [HttpGet]
    public IEnumerable<ProcessHostMonarch> GetMonarchs()
    {
        return _monarchs.Select(m => m.Value);
    }

        [HttpGet("{id}")]
    public ProcessHostMonarch GetMonarchs(Guid id)
    {
        return _monarchs[id];
    }
}
