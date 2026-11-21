using Spectre.Console;
using ViveDiagnostic;

if (args.Length > 0)
{
    return await ViveCommandApp.Create().RunAsync(args);
}

while (true)
{
    AnsiConsole.Clear();
    AnsiConsole.Write(new FigletText("Vive Diagnostic").Centered().Color(Color.Cyan1));

    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[cyan1]Select a diagnostic operation[/]")
            .PageSize(12)
            .AddChoices(
                "Direct OpenVR query",
                "Monitor HMD connection",
                "Analyze SteamVR logs",
                "Check USB / PnP devices",
                "Create full report",
                "Exit"));

    string[] commandArgs;
    switch (choice)
    {
        case "Direct OpenVR query":
            commandArgs = ["openvr"];
            break;
        case "Monitor HMD connection":
            var seconds = AnsiConsole.Prompt(
                new TextPrompt<int>("[cyan1]Monitoring duration in seconds[/]")
                    .DefaultValue(60)
                    .Validate(value => value is >= 5 and <= 3600
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Enter a value from 5 to 3600 seconds.")));
            commandArgs = ["monitor", "--duration", seconds.ToString()];
            break;
        case "Analyze SteamVR logs":
            commandArgs = ["logs"];
            break;
        case "Check USB / PnP devices":
            commandArgs = ["usb"];
            break;
        case "Create full report":
            commandArgs = ["full"];
            break;
        case "Exit":
            return 0;
        default:
            continue;
    }

    await ViveCommandApp.Create().RunAsync(commandArgs);
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[grey]Press Enter to return to the menu…[/]");
    Console.ReadLine();
}
