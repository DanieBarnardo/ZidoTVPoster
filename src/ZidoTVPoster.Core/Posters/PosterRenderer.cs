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

        var tempPath = Path.Combine(
            string.IsNullOrWhiteSpace(outputDirectory) ? Directory.GetCurrentDirectory() : outputDirectory,
            $".{Path.GetFileName(generatedPosterPath)}.{Guid.NewGuid():N}.tmp.jpg");

        try
        {
            await image.SaveAsJpegAsync(
                tempPath,
                new JpegEncoder { Quality = 92 },
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            ReplaceGeneratedPoster(tempPath, generatedPosterPath);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void DrawBadge(Image<Rgba32> image, string text)
    {
        var shortestSide = Math.Min(image.Width, image.Height);
        var padding = Math.Clamp(shortestSide / 10, 2, 18);
        var margin = Math.Clamp(shortestSide / 12, 1, 24);
        var fontSize = Math.Clamp(shortestSide / 3f, 5f, 44f);
        var font = CreateFont(fontSize);
        var textSize = TextMeasurer.MeasureSize(text, new TextOptions(font));
        var maxBadgeWidth = Math.Max(1, image.Width - (margin * 2));
        var maxBadgeHeight = Math.Max(1, image.Height - (margin * 2));
        var badgeWidth = Math.Clamp((int)Math.Ceiling(textSize.Width + (padding * 2)), 1, maxBadgeWidth);
        var badgeHeight = Math.Clamp((int)Math.Ceiling(textSize.Height + padding), 1, maxBadgeHeight);
        var x = image.Width - margin - badgeWidth;
        var y = margin;
        var textX = Math.Max(0, x + ((badgeWidth - textSize.Width) / 2f));
        var textY = Math.Max(0, y + ((badgeHeight - textSize.Height) / 2f) - 1f);

        image.Mutate(context =>
        {
            context.Fill(Color.FromRgba(8, 10, 14, 230), new Rectangle(x, y, badgeWidth, badgeHeight));
            context.Draw(Color.White, Math.Clamp(shortestSide / 80f, 1f, 3f), new Rectangle(x, y, badgeWidth, badgeHeight));
            context.DrawText(text, font, Color.White, new PointF(textX, textY));
        });
    }

    private static void ReplaceGeneratedPoster(string tempPath, string generatedPosterPath)
    {
        if (File.Exists(generatedPosterPath))
        {
            File.Replace(tempPath, generatedPosterPath, destinationBackupFileName: null);
            return;
        }

        File.Move(tempPath, generatedPosterPath);
    }

    private static Font CreateFont(float size)
    {
        if (SystemFonts.TryGet("Arial", out var arial))
        {
            return arial.CreateFont(size, FontStyle.Bold);
        }

        using var families = SystemFonts.Families.GetEnumerator();
        if (!families.MoveNext())
        {
            throw new InvalidOperationException("No system fonts are available for poster badge rendering.");
        }

        return families.Current.CreateFont(size, FontStyle.Bold);
    }
}
