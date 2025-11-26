/// <summary>
/// Configuration options for the file organizer.
/// Controls how files are organized and whether to perform a dry-run.
/// </summary>
public class FileOptions
{
    /// <summary>
    /// If true, simulate the organization without moving files.
    /// Useful for previewing what changes would be made.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Strategy or parameter for grouping files.
    /// Could represent: directory path, file extension pattern, date format, etc.
    /// </summary>
    public string? By { get; set; }
}