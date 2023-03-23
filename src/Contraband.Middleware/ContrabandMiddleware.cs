using Contraband.Core;
using FFmpeg.NET;
using MessagePack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Contraband.Middleware;

using Microsoft.AspNetCore.Http;

internal class ContrabandMiddleware
{
    private readonly RequestDelegate _next;

    public ContrabandMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IOptions<ContrabandConfiguration> configuration)
    {
        if (context.Request.Query.Any(q => q.Key == configuration.Value.QueryParameter && q.Value.ToString().ToLower() == "true"))
        {
            context.Response.Headers.Remove("Content-Type");
            context.Response.Headers.Add("Content-Type", "video/mp4");
        }

        await _next(context);
    }
}

internal class VideoOutputFormatter : OutputFormatter
{
    public VideoOutputFormatter()
    {
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("video/mp4"));
    }

    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context)
    {
        var httpContext = context.HttpContext;
        var serviceProvider = httpContext.RequestServices;
        var configuration = serviceProvider.GetRequiredService<IOptions<ContrabandConfiguration>>();

        using var ms = new MemoryStream();
        await MessagePackSerializer.SerializeAsync(context.ObjectType!, ms, context.Object, cancellationToken: httpContext.RequestAborted);
        var engine = new Engine(configuration.Value.FfmpegEnginePath);
        var encoded = await new ContrabandEncoder(engine).GenerateVideo(ms.ToArray(), httpContext.RequestAborted);
        await encoded.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted);
    }
}

public static class ContrabandExtensions
{
    public static IServiceCollection AddContraband(this IServiceCollection services)
    {
        return services.AddContraband(_ => { });
    }

    public static IServiceCollection AddContraband(this IServiceCollection services, Action<ContrabandConfiguration> configure)
    {
        return services.Configure(configure);
    }

    public static IApplicationBuilder UseContraband(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ContrabandMiddleware>();
    }

    public static MvcOptions AddContrabandFormatters(this MvcOptions options)
    {
        options.OutputFormatters.Add(new VideoOutputFormatter());
        return options;
    }
}

public class ContrabandConfiguration
{
    public string? FfmpegEnginePath { get; set; } = "C:\\ProgramData\\chocolatey\\bin\\ffmpeg.exe";
    public string? QueryParameter { get; set; } = "video";
}