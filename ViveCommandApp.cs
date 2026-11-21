using Spectre.Console.Cli;
using ViveDiagnostic.Commands;

namespace ViveDiagnostic;

internal static class ViveCommandApp
{
    public static CommandApp Create()
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.SetApplicationName("vive-diagnostic");
            config.AddCommand<OpenVrCommand>("openvr")
                .WithDescription("Query the headset directly through the OpenVR API.");
            config.AddCommand<MonitorCommand>("monitor")
                .WithDescription("Monitor HMD connection and activity changes.")
                .WithExample("monitor", "--duration", "60");
            config.AddCommand<LogsCommand>("logs")
                .WithDescription("Analyze recent SteamVR log files.");
            config.AddCommand<UsbCommand>("usb")
                .WithDescription("List connected HTC Vive and related PnP devices.");
            config.AddCommand<FullReportCommand>("full")
                .WithDescription("Run all diagnostics and optionally save a report.");
        });
        return app;
    }
}
