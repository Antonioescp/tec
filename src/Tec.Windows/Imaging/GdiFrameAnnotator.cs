using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using Tec.Core.Abstractions;
using Tec.Core.Model;

namespace Tec.Windows.Imaging;

public sealed record AnnotationStyle
{
    public string FontFamily { get; init; } = "Segoe UI";
    public float TitleFontSize { get; init; } = 16f;
    public float DetailFontSize { get; init; } = 12f;
    public int HeaderPadding { get; init; } = 10;
    public int AccentStripeWidth { get; init; } = 8;
    public int MinimumWidth { get; init; } = 640;
    public float HighlightPenWidth { get; init; } = 3f;
    public int HighlightPadding { get; init; } = 3;
    public int ClickMarkerRadius { get; init; } = 12;
    public Color HeaderBackground { get; init; } = Color.FromArgb(30, 30, 30);
    public Color HeaderTitleColor { get; init; } = Color.White;
    public Color HeaderDetailColor { get; init; } = Color.FromArgb(200, 200, 200);
    public Color CanvasBackground { get; init; } = Color.FromArgb(240, 240, 240);
}

/// <summary>
/// Adds a header with the interaction description (without covering the capture),
/// draws the highlight box and, for clicks, a marker at the exact point.
/// </summary>
public sealed class GdiFrameAnnotator(AnnotationStyle? style = null) : IFrameAnnotator
{
    private readonly AnnotationStyle _style = style ?? new AnnotationStyle();

    public RasterImage Annotate(AnnotationRequest request)
    {
        var frame = request.Frame;
        var color = Color.FromArgb(request.Color.R, request.Color.G, request.Color.B);

        using var screenshot = BitmapConversions.ToBitmap(frame.Image);
        using var titleFont = new Font(_style.FontFamily, _style.TitleFontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var detailFont = new Font(_style.FontFamily, _style.DetailFontSize, FontStyle.Regular, GraphicsUnit.Pixel);

        var width = Math.Max(frame.Image.Width, _style.MinimumWidth);
        var textLeft = _style.AccentStripeWidth + _style.HeaderPadding;
        var textWidth = width - textLeft - _style.HeaderPadding;

        SizeF titleSize, detailSize;
        using (var measure = Graphics.FromImage(screenshot))
        {
            titleSize = measure.MeasureString(request.Caption.Title, titleFont, textWidth);
            detailSize = measure.MeasureString(request.Caption.Details, detailFont, textWidth);
        }

        var headerHeight = (int)Math.Ceiling(_style.HeaderPadding * 2 + titleSize.Height + 4 + detailSize.Height);

        using var canvas = new Bitmap(width, headerHeight + frame.Image.Height, PixelFormat.Format32bppRgb);
        using (var g = Graphics.FromImage(canvas))
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(_style.CanvasBackground);

            DrawHeader(g, request.Caption, color, width, headerHeight, textLeft, textWidth, titleFont, detailFont, titleSize.Height);

            var imageArea = new Rectangle(0, headerHeight, frame.Image.Width, frame.Image.Height);
            g.DrawImage(screenshot, imageArea);

            g.SetClip(imageArea);
            DrawHighlight(g, request.Highlight, frame.Bounds, imageArea, color);
            if (request.Interaction.Kind.Category == InteractionCategory.Pointer && request.Interaction.Point is { } p)
                DrawClickMarker(g, p, frame.Bounds, imageArea, color);
        }

        return BitmapConversions.ToRasterImage(canvas);
    }

    private void DrawHeader(Graphics g, InteractionCaption caption, Color accent, int width, int height,
        int textLeft, int textWidth, Font titleFont, Font detailFont, float titleHeight)
    {
        using var background = new SolidBrush(_style.HeaderBackground);
        using var accentBrush = new SolidBrush(accent);
        using var titleBrush = new SolidBrush(_style.HeaderTitleColor);
        using var detailBrush = new SolidBrush(_style.HeaderDetailColor);

        g.FillRectangle(background, 0, 0, width, height);
        g.FillRectangle(accentBrush, 0, 0, _style.AccentStripeWidth, height);

        var top = (float)_style.HeaderPadding;
        g.DrawString(caption.Title, titleFont, titleBrush, new RectangleF(textLeft, top, textWidth, titleHeight + 2));
        g.DrawString(caption.Details, detailFont, detailBrush, new RectangleF(textLeft, top + titleHeight + 4, textWidth, height - top - titleHeight));
    }

    private void DrawHighlight(Graphics g, ScreenRect highlight, ScreenRect frameBounds, Rectangle imageArea, Color color)
    {
        var rect = new Rectangle(
            highlight.Left - frameBounds.Left + imageArea.Left,
            highlight.Top - frameBounds.Top + imageArea.Top,
            highlight.Width, highlight.Height);
        rect.Inflate(_style.HighlightPadding, _style.HighlightPadding);

        // Keep the box fully visible inside the capture.
        var half = (int)Math.Ceiling(_style.HighlightPenWidth / 2);
        var visible = Rectangle.Inflate(imageArea, -half, -half);
        rect.Intersect(visible);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        using var pen = new Pen(color, _style.HighlightPenWidth) { Alignment = PenAlignment.Center };
        g.SmoothingMode = SmoothingMode.None;
        g.DrawRectangle(pen, rect);
    }

    private void DrawClickMarker(Graphics g, ScreenPoint point, ScreenRect frameBounds, Rectangle imageArea, Color color)
    {
        var x = point.X - frameBounds.Left + imageArea.Left;
        var y = point.Y - frameBounds.Top + imageArea.Top;
        var r = _style.ClickMarkerRadius;

        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var outline = new Pen(Color.White, 4f);
        using var pen = new Pen(color, 2f);
        using var dot = new SolidBrush(color);
        g.DrawEllipse(outline, x - r, y - r, r * 2, r * 2);
        g.DrawEllipse(pen, x - r, y - r, r * 2, r * 2);
        g.FillEllipse(dot, x - 3, y - 3, 6, 6);
    }
}
