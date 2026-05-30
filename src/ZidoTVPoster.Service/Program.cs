using Microsoft.Extensions.Options;
using ZidoTVPoster.Core.Configuration;
using ZidoTVPoster.Core.Posters;
using ZidoTVPoster.Core.Storage;
using ZidoTVPoster.Core.Zidoo;
using ZidoTVPoster.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<ZidooOptions>(builder.Configuration.GetSection("Zidoo"));
builder.Services.Configure<PosterUpdateOptions>(builder.Configuration.GetSection("PosterUpdates"));

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
