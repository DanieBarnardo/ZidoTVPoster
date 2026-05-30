namespace ZidoTVPoster.Core.Configuration;

public sealed class ZidooOptions
{
    public string BaseUrl { get; set; } = "http://192.168.0.209:9529";
    public string StorageRoot { get; set; } = @"\\192.168.0.209\Share\Storage";
    public string[] MediaRootNames { get; set; } = ["Series"];
    public int RequestTimeoutSeconds { get; set; } = 10;
}
