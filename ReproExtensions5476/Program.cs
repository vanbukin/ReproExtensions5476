using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Serilog.Extensions.Logging;
using Serilog.Formatting.Elasticsearch;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ReproExtensions5476;

public static class Program
{
    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    [SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task")]
    public static async Task<int> Main(string[] args)
    {
        var exitCode = 0;
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddConfiguration(builder.Configuration.GetRequiredSection("Logging"));
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Verbose()
            .WriteTo.Console(new ExceptionAsObjectJsonFormatter(renderMessage: true, inlineFields: true))
            .CreateLogger();
        using var loggerProvider = new SerilogLoggerProvider(Log.Logger);
        try
        {
            builder.Logging.AddProvider(loggerProvider);
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddControllers();
            builder.Services.AddSingleton<IWeatherForecastService, DefaultWeatherForecastService>();
            var app = builder.Build();
            app.UseSwagger();
            app.UseSwaggerUI();
            app.MapControllers();
            await app.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
            exitCode = -1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }

        return exitCode;
    }
}

[ApiController]
[Route("api/weather")]
[Produces(MediaTypeNames.Application.Json)]
[Consumes(MediaTypeNames.Application.Json)]
public class WeatherController : ControllerBase
{
    private readonly IWeatherForecastService _weatherForecastService;

    public WeatherController(IWeatherForecastService weatherForecastService)
    {
        ArgumentNullException.ThrowIfNull(weatherForecastService);
        _weatherForecastService = weatherForecastService;
    }

    [HttpGet("")]
    public WeatherForecast[] GetForecast()
    {
        return _weatherForecastService.GetForecast();
    }
}

public interface IWeatherForecastService
{
    WeatherForecast[] GetForecast();
}

public class DefaultWeatherForecastService : IWeatherForecastService
{
    private ILogger<DefaultWeatherForecastService> _logger;

    public DefaultWeatherForecastService(ILogger<DefaultWeatherForecastService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    private static readonly string[] Summaries =
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    [SuppressMessage("Security", "CA5394:Do not use insecure randomness")]
    public WeatherForecast[] GetForecast()
    {
        _logger.StartGetWeatherForecast();
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                {
                    Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    TemperatureC = Random.Shared.Next(-20, 55),
                    Summary = Summaries[Random.Shared.Next(Summaries.Length)]
                })
            .ToArray();
        _logger.EndGetWeatherForecast(forecast.First(), forecast.Skip(1));
        return forecast;
    }
}

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public class WeatherForecast
{
    public DateOnly Date { get; set; }

    public int TemperatureC { get; set; }

    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    public string? Summary { get; set; }
}

public static partial class DefaultWeatherForecastServiceLoggingExtensions
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Start get weather forecast")]
    public static partial void StartGetWeatherForecast(this ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "End weather forecast. First {@First}, All: {@Remain}")]
    public static partial void EndGetWeatherForecast(this ILogger logger, WeatherForecast first, IEnumerable<WeatherForecast> remain);
}