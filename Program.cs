using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        services.Configure<FileOptions>(ctx.Configuration.GetSection("FileOptions"));
        services.AddSingleton<IFileOrganizer, FileOrganizer>();
        services.AddSingleton<IDryRunService, DryRunService>();
        services.AddLogging(cfg => cfg.AddConsole());
    })
    .Build();

var organizer = builder.Services.GetRequiredService<IFileOrganizer>();

// Simple argument-driven runner so you can call via `dotnet run -- <command> [args...]`
if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run -- <command> [args]\nCommands:\n  organize [path] [--dry-run]\n  analyze-csv <file.gz>\n  import-jsonstat <data.json> <out.csv>");
    return;
}

var cmd = args[0].ToLowerInvariant();
var ct = CancellationToken.None;

switch (cmd)
{
    case "organize":
    {
        var path = args.Length > 1 ? args[1] : Environment.CurrentDirectory;
        var dry = args.Contains("--dry-run");
        var options = new FileOptions { DryRun = dry, By = path };
        Console.WriteLine($"Running organizer (dry-run={dry}) with options.By={options.By}");
        await organizer.ExecuteAsync(options, ct);
        break;
    }
    case "analyze-csv":
    {
        if (args.Length < 2)
        {
            Console.WriteLine("analyze-csv requires a path to a .gz file");
            return;
        }
        var gz = args[1];
        Console.WriteLine($"Analyzing gzipped CSV: {gz}");
        var stats = await CsvAnalyzer.AnalyzeGzippedCsvAsync(gz, ct);
        Console.WriteLine($"Rows: {stats.RowCount}");
        foreach (var c in stats.Columns)
        {
            Console.WriteLine($"Column: {c.Name}  IsNumeric: {c.IsNumeric} Count: {c.Count} Mean: {c.Mean} Min: {c.Min} Max: {c.Max}");
        }
        break;
    }
    case "import-jsonstat":
    {
        if (args.Length < 3)
        {
            Console.WriteLine("import-jsonstat requires <data.json> <out.csv>");
            return;
        }
        var json = args[1];
        var outCsv = args[2];
        Console.WriteLine($"Converting JSON-stat {json} -> {outCsv}");
        await JsonStatImporter.ConvertJsonStatToCsvAsync(json, outCsv);
        Console.WriteLine("Conversion complete.");
        break;
    }
    default:
        Console.WriteLine($"Unknown command: {cmd}");
        break;
}
