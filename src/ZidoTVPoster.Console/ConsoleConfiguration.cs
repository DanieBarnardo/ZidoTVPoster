using Microsoft.Extensions.Configuration;
using ZidoTVPoster.Core.Configuration;

namespace ZidoTVPoster.Console;

public static class ConsoleConfiguration
{
    public static ZidooOptions LoadZidooOptions(string? basePath = null)
    {
        var defaults = new ZidooOptions();
        var section = LoadConfiguration(basePath).GetSection("Zidoo");
        var options = new ZidooOptions
        {
            BaseUrl = section["BaseUrl"] ?? defaults.BaseUrl,
            StorageRoot = section["StorageRoot"] ?? defaults.StorageRoot,
            MediaRootNames = section.GetSection("MediaRootNames").Get<string[]>() ?? defaults.MediaRootNames,
            RequestTimeoutSeconds = section.GetValue<int?>("RequestTimeoutSeconds") ?? defaults.RequestTimeoutSeconds
        };

        if (!ZidooOptions.IsValid(options))
        {
            throw new InvalidOperationException(
                "Zidoo options must include an absolute BaseUrl, positive RequestTimeoutSeconds, StorageRoot, and non-empty MediaRootNames.");
        }

        return options;
    }

    private static IConfiguration LoadConfiguration(string? basePath)
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";
        var resolvedBasePath = string.IsNullOrWhiteSpace(basePath)
            ? AppContext.BaseDirectory
            : basePath;

        return new ConfigurationBuilder()
            .SetBasePath(resolvedBasePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .Build();
    }
}
