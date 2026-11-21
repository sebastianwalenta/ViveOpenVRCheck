using Spectre.Console.Cli;
using System.Threading;
using ViveDiagnostic;

namespace ViveDiagnostic.Commands;

internal sealed class UsbCommand : Command<EmptyCommandSettings>
{
    public override int Execute(CommandContext context, EmptyCommandSettings settings, CancellationToken cancellationToken)
    {
        ViveDiagnostics.RunUsbCheck();
        return 0;
    }
}
