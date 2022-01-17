using Microsoft.AspNetCore.Mvc;

namespace fiskaltrust.Launcher.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private readonly ILogger<WeatherForecastController> _logger;

    public WeatherForecastController(ILogger<WeatherForecastController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IEnumerable<string> Get()
    {
        return Enumerable.Range(1, 5).Select(index => "kek")
        .ToArray();
    }
}
