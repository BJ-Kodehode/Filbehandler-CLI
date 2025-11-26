/// <summary>
/// Service for logging dry-run file operations.
/// Allows previewing what would happen without actually executing changes.
/// </summary>
public interface IDryRunService
{
    /// <summary>
    /// Logs a file action (move operation) as if it were executed.
    /// </summary>
    /// <param name="action">The file action to log (source and target paths).</param>
    void Log(FileAction action);
}
