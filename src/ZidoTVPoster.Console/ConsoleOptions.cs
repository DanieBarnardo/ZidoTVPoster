namespace ZidoTVPoster.Console;

public sealed record ConsoleOptions(
    string BaseUrl,
    string StorageRoot,
    string[] MediaRootNames,
    string? SeriesFilter,
    bool DryRun)
{
    public bool ApplyUpdates => !DryRun;

    public static ConsoleOptions Parse(string[] args)
    {
        var baseUrl = "http://192.168.0.209:9529";
        var storageRoot = @"\\192.168.0.209\Share\Storage";
        var mediaRootNames = new[] { "Series" };
        string? seriesFilter = null;
        var dryRun = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--series":
                    seriesFilter = ReadValue(args, ref index, "--series");
                    break;
                case "--base-url":
                    baseUrl = ReadValue(args, ref index, "--base-url");
                    break;
                case "--storage-root":
                    storageRoot = ReadValue(args, ref index, "--storage-root");
                    break;
                case "--media-roots":
                    mediaRootNames = ReadValue(args, ref index, "--media-roots")
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[index]}");
            }
        }

        return new ConsoleOptions(baseUrl, storageRoot, mediaRootNames, seriesFilter, dryRun);
    }

    private static string ReadValue(string[] args, ref int index, string name)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{name} requires a value.");
        }

        index++;
        return args[index];
    }
}
