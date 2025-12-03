using Microsoft.Extensions.Logging;

/// <summary>
/// Default implementation of IDryRunService.
/// Logs dry-run file actions using the standard ILogger infrastructure.
/// </summary>
public class DryRunService(ILogger<DryRunService> logger) : IDryRunService
{
    /// <summary>
    /// Logs a file action that would be executed.
    /// </summary>
    /// <param name="action">File action with source and target paths.</param>
    public void Log(FileAction action)
    {
        logger.LogInformation("DRYRUN: {Source} -> {Target}", action.Source, action.Target);
    }
}
