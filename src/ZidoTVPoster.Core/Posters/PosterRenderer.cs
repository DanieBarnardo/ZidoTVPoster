using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ZidoTVPoster.Core.Configuration;

namespace ZidoTVPoster.Core.Posters;

public sealed class PosterRenderer
{
    public async Task RenderAsync(
        string originalPosterPath,
        string generatedPosterPath,
        int unwatchedCount,
        PosterUpdateOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalPosterPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(generatedPosterPath);
        ArgumentNullException.ThrowIfNull(options);

        var outputDirectory = Path.GetDirectoryName(generatedPosterPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using var image = await Image.LoadAsync<Rgba32>(originalPosterPath, cancellationToken);

        if (!options.HideBadgeWhenZero || unwatchedCount != 0)
        {
            DrawBadge(image, string.Format(options.BadgeTextFormat, unwatchedCount));
        }

        await image.SaveAsJpegAsync(
            generatedPosterPath,
            new JpegEncoder { Quality = 92 },
            cancellationToken);
    }

    private static void DrawBadge(Image<Rgba32> image, string text)
    {
        var padding = Math.Max(10, image.Width / 40);
        var margin = Math.Max(12, image.Width / 35);
        var fontSize = Math.Clamp(image.Width / 13f, 20f, 44f);
        var font = CreateFont(fontSize);
        var textSize = TextMeasurer.MeasureSize(text, new TextOptions(font));
        var badgeWidth = Math.Min(image.Width - (margin * 2), (int)Math.Ceiling(textSize.Width + (padding * 2)));
        var badgeHeight = Math.Max((int)Math.Ceiling(textSize.Height + padding), padding * 3);
        var x = image.Width - margin - badgeWidth;
        var y = margin;
        var textX = x + ((badgeWidth - textSize.Width) / 2f);
        var textY = y + ((badgeHeight - textSize.Height) / 2f) - 1f;

        image.Mutate(context =>
        {
            context.Fill(Color.FromRgba(8, 10, 14, 230), new Rectangle(x, y, badgeWidth, badgeHeight));
            context.Draw(Color.White, Math.Max(2, image.Width / 160f), new Rectangle(x, y, badgeWidth, badgeHeight));
            context.DrawText(text, font, Color.White, new PointF(textX, textY));
        });
    }

    private static Font CreateFont(float size)
    {
        if (SystemFonts.TryGet("Arial", out var arial))
        {
            return arial.CreateFont(size, FontStyle.Bold);
        }

        return SystemFonts.Families.First().CreateFont(size, FontStyle.Bold);
    }
}
