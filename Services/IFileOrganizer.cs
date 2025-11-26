using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;

public interface IFileOrganizer
{
    Task<IEnumerable<FileAction>> PreviewAsync(FileOptions options, CancellationToken ct = default);
    Task ExecuteAsync(FileOptions options, CancellationToken ct = default);
}

public class FileOrganizer : IFileOrganizer
{
    private readonly ILogger<FileOrganizer> _logger;
    private readonly IDryRunService _dryRun;

    public FileOrganizer(ILogger<FileOrganizer> logger, IDryRunService dryRun)
    {
        _logger = logger;
        _dryRun = dryRun;
    }

    public async Task<IEnumerable<FileAction>> PreviewAsync(FileOptions options, CancellationToken ct = default)
    {
        // Finn filer, grupper etter options.By, bygg liste av FileAction (source, target)
        // Returner uten å flytte
        return Enumerable.Empty<FileAction>();
    }

    public async Task ExecuteAsync(FileOptions options, CancellationToken ct = default)
    {
        var actions = await PreviewAsync(options, ct);
        foreach (var a in actions)
        {
            if (options.DryRun) _dryRun.Log(a);
            else
            {
                // opprett kataloger, flytt fil, logg
            }
        }
    }
}