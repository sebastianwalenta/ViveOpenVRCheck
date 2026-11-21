using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Spectre.Console;
using Valve.VR;

namespace ViveDiagnostic;

internal static class ViveDiagnostics
{
    private static readonly Regex VivePattern = new(
        @"(?i)(HTC Vive|Vive|Valve.*VR|VR.*Valve|Lighthouse|VID_(0BB4|28DE|2D1F))",
        RegexOptions.Compiled);

    public static int RunOpenVr(bool monitor, int seconds)
    {
        return AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Querying SteamVR through OpenVR…", _ =>
                RunOpenVrCore(monitor, seconds));
    }

    private static int RunOpenVrCore(bool monitor, int seconds)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Item");
        table.AddColumn("Result");

        var initError = EVRInitError.None;
        CVRSystem? system;
        try
        {
            system = OpenVR.Init(ref initError, EVRApplicationType.VRApplication_Utility);
        }
        catch (Exception ex)
        {
            table.AddRow("Exception", Markup.Escape(ex.Message));
            AnsiConsole.Write(table);
            return 3;
        }

        if (initError != EVRInitError.None)
        {
            table.AddRow("INIT ERROR", Markup.Escape(initError.ToString()));
            table.AddRow("Description", Markup.Escape(OpenVR.GetStringForHmdError(initError)));
            AnsiConsole.Write(table);
            return 2;
        }

        if (system == null)
        {
            AnsiConsole.MarkupLine("[red]OpenVR returned a null IVRSystem.[/]");
            return 3;
        }

        table.AddRow("SteamVR server", "[green]available[/]");
        table.AddRow("Maximum device count", OpenVR.k_unMaxTrackedDeviceCount.ToString());

        var hmdIndexes = new List<uint>();
        for (uint index = 0; index < OpenVR.k_unMaxTrackedDeviceCount; index++)
        {
            if (system.GetTrackedDeviceClass(index) != ETrackedDeviceClass.HMD)
            {
                continue;
            }

            hmdIndexes.Add(index);
            var connected = system.IsTrackedDeviceConnected(index);
            table.AddRow("HMD index", index.ToString());
            table.AddRow("Connected", connected ? "[green]True[/]" : "[red]False[/]");
            table.AddRow("Activity", Markup.Escape(system.GetTrackedDeviceActivityLevel(index).ToString()));
            table.AddRow("Model", ReadProperty(system, index, ETrackedDeviceProperty.Prop_ModelNumber_String));
            table.AddRow("Serial", ReadProperty(system, index, ETrackedDeviceProperty.Prop_SerialNumber_String));
            table.AddRow("Manufacturer", ReadProperty(system, index, ETrackedDeviceProperty.Prop_ManufacturerName_String));
            table.AddRow("TrackingSystem", ReadProperty(system, index, ETrackedDeviceProperty.Prop_TrackingSystemName_String));
        }

        if (hmdIndexes.Count == 0)
        {
            table.AddRow("Result", "[red]SteamVR returned no HMD device[/]");
            AnsiConsole.Write(table);
            OpenVR.Shutdown();
            return 1;
        }

        AnsiConsole.Write(table);

        if (monitor)
        {
            MonitorHmd(system, hmdIndexes, Math.Clamp(seconds, 5, 3600));
        }

        OpenVR.Shutdown();
        return 0;
    }

    private static void MonitorHmd(CVRSystem system, List<uint> hmdIndexes, int seconds)
    {
        AnsiConsole.MarkupLine("[cyan1]Connection monitor started[/] for {0} seconds.", seconds);
        var states = hmdIndexes.ToDictionary(index => index,
            index => system.IsTrackedDeviceConnected(index));
        var activities = hmdIndexes.ToDictionary(index => index,
            index => system.GetTrackedDeviceActivityLevel(index));

        for (var second = 1; second <= seconds; second++)
        {
            Thread.Sleep(1000);
            foreach (var index in hmdIndexes)
            {
                var connected = system.IsTrackedDeviceConnected(index);
                var activity = system.GetTrackedDeviceActivityLevel(index);
                if (connected != states[index])
                {
                    AnsiConsole.MarkupLine("[yellow]{0}[/] HMD {1}: Connected [red]{2}[/] -> [green]{3}[/]",
                        DateTime.Now.ToString("HH:mm:ss"), index,
                        states[index], connected);
                    states[index] = connected;
                }

                if (activity != activities[index])
                {
                    AnsiConsole.MarkupLine("[yellow]{0}[/] HMD {1}: Activity [grey]{2}[/] -> [cyan1]{3}[/]",
                        DateTime.Now.ToString("HH:mm:ss"), index,
                        activities[index], activity);
                    activities[index] = activity;
                }
            }
        }
    }

    private static string ReadProperty(CVRSystem system, uint index,
        ETrackedDeviceProperty property)
    {
        var value = new StringBuilder(1024);
        var error = ETrackedPropertyError.TrackedProp_Success;
        system.GetStringTrackedDeviceProperty(index, property, value,
            (uint)value.Capacity, ref error);
        return string.IsNullOrWhiteSpace(value.ToString())
            ? "(not returned)"
            : Markup.Escape(value.ToString());
    }

    public static void RunUsbCheck()
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Stav");
        table.AddColumn("Device");
        table.AddColumn("Device ID");

        var count = 0;
        var problems = 0;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "pnputil.exe",
                Arguments = "/enum-devices /connected",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("PnPUtil could not be started.");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error) ? "PnP query failed." : error);
            }

            var blocks = Regex.Split(output, @"(?=^Instance ID:\s*)",
                RegexOptions.Multiline);
            foreach (var block in blocks)
            {
                var idMatch = Regex.Match(block, @"(?m)^Instance ID:\s*(.+)$");
                var nameMatch = Regex.Match(block,
                    @"(?m)^(?:Device Description|Name|Description):\s*(.+)$");
                var statusMatch = Regex.Match(block, @"(?m)^Status:\s*(.+)$");
                var id = idMatch.Success ? idMatch.Groups[1].Value.Trim() : "";
                var name = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : "";
                var status = statusMatch.Success ? statusMatch.Groups[1].Value.Trim() : "Unknown";

                if (!VivePattern.IsMatch(name + " " + id))
                {
                    continue;
                }

                count++;
                var isStarted = status.StartsWith("Started", StringComparison.OrdinalIgnoreCase);
                var isOk = string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase) ||
                           isStarted;
                if (!isOk)
                {
                    problems++;
                }

                var statusMarkup = isOk
                    ? "[green]OK[/]"
                    : "[red]" + Markup.Escape(status) + "[/]";
                table.AddRow(statusMarkup, Markup.Escape(name), Markup.Escape(id));
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]PnP query failed:[/] " +
                                   Markup.Escape(ex.Message));
            return;
        }

        if (count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No Vive/HTC/Valve VR device was found.[/]");
        }
        else
        {
            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("Found: [cyan1]{0}[/], abnormal states: [red]{1}[/]", count, problems);
        }
    }

    public static string FindSteamLogs()
    {
        var candidates = new List<string>();
        try
        {
            using var hkcu = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var steamPath = hkcu?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(steamPath))
            {
                candidates.Add(Path.Combine(steamPath, "logs"));
            }
        }
        catch { }

        candidates.Add(@"C:\Program Files (x86)\Steam\logs");
        candidates.Add(@"C:\Program Files\Steam\logs");

        return candidates.FirstOrDefault(path =>
            Directory.Exists(path)) ?? "";
    }

    public static void RunSteamLogAnalysis()
    {
        var logDirectory = FindSteamLogs();
        if (string.IsNullOrWhiteSpace(logDirectory))
        {
            logDirectory = AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan1]Path to the Steam/logs folder[/]")
                    .AllowEmpty());
        }

        if (!Directory.Exists(logDirectory))
        {
            AnsiConsole.MarkupLine("[red]Log folder was not found: [/]" +
                                   Markup.Escape(logDirectory));
            return;
        }

        var logFiles = Directory.EnumerateFiles(logDirectory, "*.txt")
            .Where(path =>
                Regex.IsMatch(Path.GetFileName(path),
                    @"(?i)^(vrserver|vrcompositor|vrmonitor|vrclient).*\.txt$"))
            .OrderByDescending(File.GetLastWriteTime)
            .Take(8)
            .ToList();

        if (logFiles.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Expected SteamVR logs were not found.[/]");
            return;
        }

        var hmdNotFound = 0;
        var lostServer = 0;
        var fatal = 0;
        var binding = 0;
        var bluetooth = 0;
        var findings = new List<string>();

        foreach (var file in logFiles)
        {
            foreach (var rawLine in ReadTail(file, 5000))
            {
                if (Regex.IsMatch(rawLine,
                    @"(?i)VR_Init failed.*Hmd Not Found|Hmd Not Found \(108\)|TrackedProp_InvalidDevice"))
                {
                    hmdNotFound++;
                }
                if (Regex.IsMatch(rawLine, @"(?i)Lost connection to VR Server"))
                {
                    lostServer++;
                }
                if (Regex.IsMatch(rawLine,
                    @"(?i)fatal assertion|Aborting due|ignore.*Quit event"))
                {
                    fatal++;
                }
                if (Regex.IsMatch(rawLine, @"(?i)Failed to load binding file"))
                {
                    binding++;
                }
                if (Regex.IsMatch(rawLine,
                    @"(?i)(Bluetooth|0x0080).*?(required|0x0080)"))
                {
                    bluetooth++;
                }
                if (Regex.IsMatch(rawLine,
                    @"(?i)\b(error|fatal|failed|failure|assert|disconnect|timeout|lost)\b"))
                {
                    findings.Add($"{Path.GetFileName(file)}: {rawLine.Trim()}");
                }
            }
        }

        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn("Finding");
        table.AddColumn("Count");
        table.AddRow("HMD Not Found / invalid HMD",
            hmdNotFound > 0 ? "[red]" + hmdNotFound + "[/]" : "0");
        table.AddRow("Lost VR server",
            lostServer > 0 ? "[red]" + lostServer + "[/]" : "0");
        table.AddRow("Fatal assertion / abort",
            fatal > 0 ? "[red]" + fatal + "[/]" : "0");
        table.AddRow("Binding file",
            binding > 0 ? "[yellow]" + binding + "[/]" : "0");
        table.AddRow("Bluetooth warning",
            bluetooth > 0 ? "[yellow]" + bluetooth + "[/]" : "0");
        table.AddRow("Suspicious error lines",
            findings.Count.ToString());
        AnsiConsole.Write(table);

        if (findings.Count > 0)
        {
            var lines = findings.Take(100).Select(Markup.Escape).ToArray();
            AnsiConsole.Write(new Markup("[cyan1]First 100 suspicious lines[/]"));
            AnsiConsole.WriteLine();
            foreach (var line in lines)
            {
                AnsiConsole.WriteLine(line);
            }
        }
    }

    private static IReadOnlyList<string> ReadTail(string file, int lineCount)
    {
        var lines = new Queue<string>(lineCount);
        try
        {
            foreach (var line in File.ReadLines(file))
            {
                lines.Enqueue(line);
                if (lines.Count > lineCount)
                {
                    lines.Dequeue();
                }
            }
        }
        catch (Exception ex)
        {
            return new[] { "READ ERROR: " + ex.Message };
        }

        return lines.ToArray();
    }

    public static void RunFullReport()
    {
        var report = new StringBuilder();
        var originalOut = Console.Out;
        using var reportWriter = new StringWriter(report);
        Console.SetOut(reportWriter);
        try
        {
            RunUsbCheck();
            Console.WriteLine();
            RunSteamLogAnalysis();
            Console.WriteLine();
            RunOpenVr(false, 0);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var defaultName = "vive-diagnostic-" +
                          DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
        var save = AnsiConsole.Confirm("Save the report to a file?", false);
        if (!save)
        {
            return;
        }

        var path = AnsiConsole.Prompt(
            new TextPrompt<string>("[cyan1]Output file[/]")
                .DefaultValue(Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.Desktop),
                    defaultName)));
        try
        {
            File.WriteAllText(path,
                "Diagnostic report created " +
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                Environment.NewLine +
                "============================================================" +
                Environment.NewLine +
                report.ToString(),
                Encoding.UTF8);
            AnsiConsole.MarkupLine("[green]Report saved:[/] " + Markup.Escape(path));
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Save failed:[/] " +
                                   Markup.Escape(ex.Message));
        }
    }
}
