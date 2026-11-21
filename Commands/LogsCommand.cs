using Spectre.Console.Cli;
using System.Threading;
using ViveDiagnostic;

namespace ViveDiagnostic.Commands;

internal sealed class LogsCommand : Command<EmptyCommandSettings>
{
    public override int Execute(CommandContext context, EmptyCommandSettings settings, CancellationToken cancellationToken)
    {
        ViveDiagnostics.RunSteamLogAnalysis();
        return 0;
    }
}
