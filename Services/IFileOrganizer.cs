using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for file organization operations.
/// Supports both preview (dry-run) and execution modes.
/// </summary>
public interface IFileOrganizer
{
    /// <summary>
    /// Preview files that would be organized without actually moving them.
    /// </summary>
    /// <param name="options">Organization options (path, grouping strategy, etc.).</param>
    /// <param name="ct">Cancellation token for long-running operation.</param>
    /// <returns>Enumerable of FileAction representing proposed file movements.</returns>
    Task<IEnumerable<FileAction>> PreviewAsync(FileOptions options, CancellationToken ct = default);

    /// <summary>
    /// Execute file organization (or dry-run if options.DryRun is true).
    /// </summary>
    /// <param name="options">Organization options.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ExecuteAsync(FileOptions options, CancellationToken ct = default);
}

/// <summary>
/// Default implementation of IFileOrganizer.
/// Organizes files in a directory into subdirectories based on type or custom grouping.
/// </summary>
public class FileOrganizer(ILogger<FileOrganizer> logger, IDryRunService dryRun) : IFileOrganizer
{
    /// <summary>
    /// Preview files that would be organized without moving them.
    /// </summary>
    public async Task<IEnumerable<FileAction>> PreviewAsync(FileOptions options, CancellationToken ct = default)
    {
        // Find all files in the directory
        // Group by extension or custom option.By strategy
        // Build list of FileAction (source → target) without executing
        // Return actions for user review
        return Enumerable.Empty<FileAction>();
    }

    /// <summary>
    /// Execute file organization: move files to subdirectories or log (if dry-run).
    /// </summary>
    public async Task ExecuteAsync(FileOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        
        // Get preview of actions
        var actions = await PreviewAsync(options, ct);

        foreach (var a in actions)
        {
            if (options.DryRun)
            {
                // Dry-run mode: log the action without moving the file
                dryRun.Log(a);
            }
            else
            {
                // Actual mode: create target directory if needed, move file, log
                logger.LogInformation("Moving: {Source} -> {Target}", a.Source, a.Target);
                // Implementation: Directory.CreateDirectory(Path.GetDirectoryName(a.Target))
                //                File.Move(a.Source, a.Target, overwrite: true)
            }
        }
    }
}