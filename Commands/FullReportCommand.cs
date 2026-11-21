using Spectre.Console.Cli;
using System.Threading;
using ViveDiagnostic;

namespace ViveDiagnostic.Commands;

internal sealed class FullReportCommand : Command<EmptyCommandSettings>
{
    public override int Execute(CommandContext context, EmptyCommandSettings settings, CancellationToken cancellationToken)
    {
        ViveDiagnostics.RunFullReport();
        return 0;
    }
}
