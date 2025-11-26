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
        services.AddLogging();
    })
    .Build();

var organizer = builder.Services.GetRequiredService<IFileOrganizer>();
// Koble til System.CommandLine eller Spectre.Console for parsing og kall organizer.ExecuteAsync(...)