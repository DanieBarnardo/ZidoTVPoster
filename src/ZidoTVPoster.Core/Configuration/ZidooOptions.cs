namespace ZidoTVPoster.Core.Configuration;

public sealed class ZidooOptions
{
    public string BaseUrl { get; set; } = "http://192.168.0.209:9529";
    public string StorageRoot { get; set; } = @"\\192.168.0.209\Share\Storage";
    public string[] MediaRootNames { get; set; } = ["Series"];
    public int RequestTimeoutSeconds { get; set; } = 10;

    public static bool IsValid(ZidooOptions options)
    {
        return Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _)
            && options.RequestTimeoutSeconds > 0
            && !string.IsNullOrWhiteSpace(options.StorageRoot)
            && options.MediaRootNames is { Length: > 0 }
            && options.MediaRootNames.All(root => !string.IsNullOrWhiteSpace(root));
    }
}
