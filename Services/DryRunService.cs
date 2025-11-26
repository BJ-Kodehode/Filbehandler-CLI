using Microsoft.Extensions.Logging;

public class DryRunService : IDryRunService
{
    private readonly ILogger<DryRunService> _logger;
    public DryRunService(ILogger<DryRunService> logger) => _logger = logger;

    public void Log(FileAction action)
    {
        _logger.LogInformation("DRYRUN: {Source} -> {Target}", action.Source, action.Target);
    }
}
