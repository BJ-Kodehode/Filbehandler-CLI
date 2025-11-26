using Microsoft.Extensions.Logging;

/// <summary>
/// Default implementation of IDryRunService.
/// Logs dry-run file actions using the standard ILogger infrastructure.
/// </summary>
public class DryRunService : IDryRunService
{
    private readonly ILogger<DryRunService> _logger;

    /// <summary>
    /// Initializes a new DryRunService with dependency injection.
    /// </summary>
    /// <param name="logger">Logger instance for dry-run output.</param>
    public DryRunService(ILogger<DryRunService> logger) => _logger = logger;

    /// <summary>
    /// Logs a file action that would be executed.
    /// </summary>
    /// <param name="action">File action with source and target paths.</param>
    public void Log(FileAction action)
    {
        _logger.LogInformation("DRYRUN: {Source} -> {Target}", action.Source, action.Target);
    }
}
