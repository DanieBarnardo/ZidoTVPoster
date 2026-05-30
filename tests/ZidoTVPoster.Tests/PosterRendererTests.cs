using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using ZidoTVPoster.Core.Configuration;
using ZidoTVPoster.Core.Posters;

namespace ZidoTVPoster.Tests;

public sealed class PosterRendererTests
{
    [Fact]
    public async Task RenderAsync_WithZeroCountAndHiddenBadge_CreatesPosterWithOriginalDimensions()
    {
        var source = CreatePoster(Color.CornflowerBlue, 320, 480);
        var output = Path.Combine(CreateTempFolder(), "nested", "poster.jpg");

        await new PosterRenderer().RenderAsync(
            source,
            output,
            unwatchedCount: 0,
            new PosterUpdateOptions { HideBadgeWhenZero = true },
            CancellationToken.None);

        using var image = await Image.LoadAsync<Rgba32>(output);
        Assert.Equal(320, image.Width);
        Assert.Equal(480, image.Height);
    }

    [Fact]
    public async Task RenderAsync_WithNonZeroCount_ChangesTopRightBadgeArea()
    {
        var source = CreatePoster(Color.CornflowerBlue, 320, 480);
        var output = Path.Combine(CreateTempFolder(), "poster.jpg");

        await new PosterRenderer().RenderAsync(
            source,
            output,
            unwatchedCount: 7,
            new PosterUpdateOptions { BadgeTextFormat = "{0}" },
            CancellationToken.None);

        using var original = await Image.LoadAsync<Rgba32>(source);
        using var generated = await Image.LoadAsync<Rgba32>(output);

        Assert.NotEqual(
            AverageColor(original, new Rectangle(220, 20, 80, 60)),
            AverageColor(generated, new Rectangle(220, 20, 80, 60)));
    }

    [Fact]
    public async Task RenderAsync_CreatesParentDirectoryAndSavesJpeg()
    {
        var source = CreatePoster(Color.CornflowerBlue, 320, 480);
        var output = Path.Combine(CreateTempFolder(), "created", "poster.jpg");

        await new PosterRenderer().RenderAsync(
            source,
            output,
            unwatchedCount: 3,
            new PosterUpdateOptions(),
            CancellationToken.None);

        Assert.True(Directory.Exists(Path.GetDirectoryName(output)));

        await using var stream = File.OpenRead(output);
        IImageFormat format = await Image.DetectFormatAsync(stream);
        Assert.Equal("JPEG", format.Name);
    }

    private static string CreatePoster(Color color, int width, int height)
    {
        var path = Path.Combine(CreateTempFolder(), "source.png");
        using var image = new Image<Rgba32>(width, height, color);
        image.SaveAsPng(path);
        return path;
    }

    private static string CreateTempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ZidoTVPosterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static Rgba32 AverageColor(Image<Rgba32> image, Rectangle area)
    {
        long red = 0;
        long green = 0;
        long blue = 0;
        long alpha = 0;
        var pixels = 0;

        for (var y = area.Top; y < area.Bottom; y++)
        {
            for (var x = area.Left; x < area.Right; x++)
            {
                var pixel = image[x, y];
                red += pixel.R;
                green += pixel.G;
                blue += pixel.B;
                alpha += pixel.A;
                pixels++;
            }
        }

        return new Rgba32(
            (byte)(red / pixels),
            (byte)(green / pixels),
            (byte)(blue / pixels),
            (byte)(alpha / pixels));
    }
}
