using MessagePack;

namespace Contraband.Examples.WebApi;

[MessagePackObject]
public class WeatherForecast
{
    [Key(0)] public DateOnly Date { get; set; }

    [Key(1)] public int TemperatureC { get; set; }

    [IgnoreMember] public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    [Key(2)] public string? Summary { get; set; }
}