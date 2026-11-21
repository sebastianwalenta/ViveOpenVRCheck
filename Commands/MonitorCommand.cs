using Spectre.Console;
using Spectre.Console.Cli;
using System.Threading;
using ViveDiagnostic;

namespace ViveDiagnostic.Commands;

internal sealed class MonitorSettings : CommandSettings
{
    [CommandOption("-d|--duration <SECONDS>")]
    public int Duration { get; set; } = 60;

    public override ValidationResult Validate()
    {
        return Duration is >= 5 and <= 3600
            ? ValidationResult.Success()
            : ValidationResult.Error("Duration must be between 5 and 3600 seconds.");
    }
}

internal sealed class MonitorCommand : Command<MonitorSettings>
{
    public override int Execute(CommandContext context, MonitorSettings settings, CancellationToken cancellationToken)
    {
        return ViveDiagnostics.RunOpenVr(monitor: true, settings.Duration);
    }
}
