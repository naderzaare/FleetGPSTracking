using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GpsReceiver;

public static class MetricsServer
{
    public static WebApplication Create(ReceiverMetrics metrics, string url)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls(url);
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
        var app = builder.Build();
        app.MapGet("/metrics", () => Results.Text(metrics.Render(), "text/plain; version=0.0.4; charset=utf-8"));
        app.MapGet("/healthz", () => metrics.IsListening ? Results.Text("listening\n") : Results.StatusCode(503));
        return app;
    }
}
