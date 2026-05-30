using Microsoft.Extensions.Options;
using ZidoTVPoster.Core.Configuration;
using ZidoTVPoster.Core.Posters;
using ZidoTVPoster.Core.Storage;
using ZidoTVPoster.Core.Zidoo;
using ZidoTVPoster.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<ZidooOptions>()
    .Bind(builder.Configuration.GetSection("Zidoo"))
    .Validate(ZidooOptions.IsValid, "Zidoo options must include an absolute BaseUrl, positive RequestTimeoutSeconds, StorageRoot, and non-empty MediaRootNames.")
    .ValidateOnStart();

builder.Services.AddOptions<PosterUpdateOptions>()
    .Bind(builder.Configuration.GetSection("PosterUpdates"))
    .Validate(PosterUpdateOptions.IsValid, "PosterUpdates options must include positive PollIntervalSeconds and a valid BadgeTextFormat.")
    .ValidateOnStart();

builder.Services.AddHttpClient<ZidooApiClient>((provider, client) =>
{
    var options = provider.GetRequiredService<IOptions<ZidooOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
});

builder.Services.AddSingleton(provider =>
{
    var options = provider.GetRequiredService<IOptions<ZidooOptions>>().Value;
    return new ZidooPathMapper(options.StorageRoot, options.MediaRootNames);
});
builder.Services.AddSingleton<PosterStateStore>();
builder.Services.AddSingleton<PosterPlanner>();
builder.Services.AddSingleton<PosterRenderer>();
builder.Services.AddSingleton<PosterApplier>();
builder.Services.AddHostedService<PosterUpdateWorker>();
builder.Services.AddWindowsService();

await builder.Build().RunAsync();
