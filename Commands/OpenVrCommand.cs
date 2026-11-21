using Spectre.Console.Cli;
using System.Threading;

namespace ViveDiagnostic.Commands;

internal sealed class OpenVrCommand : Command<EmptyCommandSettings>
{
    public override int Execute(CommandContext context, EmptyCommandSettings settings, CancellationToken cancellationToken)
    {
        return ViveDiagnostics.RunOpenVr(monitor: false, seconds: 0);
    }
}
