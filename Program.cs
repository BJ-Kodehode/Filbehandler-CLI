using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Spectre.Console;

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        services.Configure<FileOptions>(ctx.Configuration.GetSection("FileOptions"));
        services.AddSingleton<IFileOrganizer, FileOrganizer>();
        services.AddSingleton<IDryRunService, DryRunService>();
        services.AddLogging(cfg => cfg.AddConsole());
    })
    .Build();

var organizer = host.Services.GetRequiredService<IFileOrganizer>();

AnsiConsole.MarkupLine("[bold cyan]File Organizer CLI[/] - organize files, analyze CSV and import JSON-stat");
AnsiConsole.MarkupLine("[dim]v1.0[/]\n");

if (args.Length == 0)
{
    ShowHelp();
    return;
}

var cmd = args[0].ToLowerInvariant();
try
{
    switch (cmd)
    {
        case "organize":
            await HandleOrganize(args, organizer);
            break;
        case "analyze-csv":
            await HandleAnalyzeCsv(args);
            break;
        case "import-jsonstat":
            await HandleImportJsonStat(args);
            break;
        case "--help":
        case "-h":
        case "help":
            ShowHelp();
            break;
        default:
            AnsiConsole.MarkupLine($"[red]Error:[/] Unknown command '{cmd}'");
            ShowHelp();
            break;
    }
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
    Environment.Exit(1);
}

void ShowHelp()
{
    AnsiConsole.MarkupLine("[bold]Commands:[/]");
    AnsiConsole.MarkupLine("  [green]organize[/]         Organize files in a directory");
    AnsiConsole.MarkupLine("    Options:");
    AnsiConsole.MarkupLine("      [cyan]--path, -p[/] <PATH>    Directory to organize (default: current directory)");
    AnsiConsole.MarkupLine("      [cyan]--dry-run, -d[/]        Log actions without moving files");
    AnsiConsole.MarkupLine("");
    AnsiConsole.MarkupLine("  [green]analyze-csv[/]      Analyze a gzipped CSV file");
    AnsiConsole.MarkupLine("    [cyan]FILE[/]                 Path to .gz CSV file");
    AnsiConsole.MarkupLine("");
    AnsiConsole.MarkupLine("  [green]import-jsonstat[/]  Convert JSON-stat to CSV");
    AnsiConsole.MarkupLine("    [cyan]INPUT[/]                Path to JSON-stat file (data.json)");
    AnsiConsole.MarkupLine("    [cyan]OUTPUT[/]               Path for resulting CSV file");
    AnsiConsole.MarkupLine("");
    AnsiConsole.MarkupLine("[bold]Examples:[/]");
    AnsiConsole.MarkupLine("  [dim]dotnet run -- organize --path . --dry-run[/]");
    AnsiConsole.MarkupLine("  [dim]dotnet run -- analyze-csv file.csv.gz[/]");
    AnsiConsole.MarkupLine("  [dim]dotnet run -- import-jsonstat data.json out.csv[/]");
}

async Task HandleOrganize(string[] args, IFileOrganizer organizer)
{
    var path = Environment.CurrentDirectory;
    var dry = false;

    for (int i = 1; i < args.Length; i++)
    {
        if (args[i] == "--path" || args[i] == "-p")
        {
            if (i + 1 < args.Length) path = args[++i];
        }
        else if (args[i] == "--dry-run" || args[i] == "-d")
        {
            dry = true;
        }
    }

    var options = new FileOptions { DryRun = dry, By = path };
    AnsiConsole.MarkupLine($"[bold green]Organizing:[/] {path}  (dry-run={dry})");
    await organizer.ExecuteAsync(options, CancellationToken.None);
    AnsiConsole.MarkupLine("[bold green]Done![/]");
}

async Task HandleAnalyzeCsv(string[] args)
{
    if (args.Length < 2)
    {
        AnsiConsole.MarkupLine("[red]Error:[/] analyze-csv requires a file path");
        AnsiConsole.MarkupLine("[dim]Usage: dotnet run -- analyze-csv <file.csv.gz>[/]");
        return;
    }

    var file = args[1];
    if (!File.Exists(file))
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {file}");
        return;
    }

    AnsiConsole.MarkupLine($"[bold]Analyzing[/] {file}...");
    var stats = await CsvAnalyzer.AnalyzeGzippedCsvAsync(file, CancellationToken.None);

    AnsiConsole.MarkupLine($"[bold green]Rows:[/] {stats.RowCount}");
    var table = new Table();
    table.AddColumn("Column Name");
    table.AddColumn("Type");
    table.AddColumn("Count");
    table.AddColumn("Mean");
    table.AddColumn("Min");
    table.AddColumn("Max");

    foreach (var c in stats.Columns)
    {
        var type = c.IsNumeric ? "Numeric" : "Text";
        table.AddRow(c.Name, type, c.Count.ToString(), c.Mean.ToString("F2"), c.Min.ToString("F2"), c.Max.ToString("F2"));
    }

    AnsiConsole.Write(table);
}

async Task HandleImportJsonStat(string[] args)
{
    if (args.Length < 3)
    {
        AnsiConsole.MarkupLine("[red]Error:[/] import-jsonstat requires input and output paths");
        AnsiConsole.MarkupLine("[dim]Usage: dotnet run -- import-jsonstat <data.json> <out.csv>[/]");
        return;
    }

    var input = args[1];
    var output = args[2];

    if (!File.Exists(input))
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {input}");
        return;
    }

    AnsiConsole.MarkupLine($"[bold]Converting[/] {input} [yellow]->[/] {output}...");
    await JsonStatImporter.ConvertJsonStatToCsvAsync(input, output);
    AnsiConsole.MarkupLine($"[bold green]Done![/]");
}
