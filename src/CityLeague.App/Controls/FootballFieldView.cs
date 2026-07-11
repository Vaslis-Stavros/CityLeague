using CityLeague.App.Helpers;
using CityLeague.Core.Dtos;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace CityLeague.App.Controls;

/// <summary>Draws a football pitch with claimable position slots, rendered with SkiaSharp.</summary>
public class FootballFieldView : SKCanvasView
{
    public static readonly BindableProperty PositionsProperty =
        BindableProperty.Create(nameof(Positions), typeof(IReadOnlyList<PositionDto>), typeof(FootballFieldView),
            null, propertyChanged: OnVisualChanged);

    public static readonly BindableProperty CurrentUserIdProperty =
        BindableProperty.Create(nameof(CurrentUserId), typeof(Guid?), typeof(FootballFieldView),
            null, propertyChanged: OnVisualChanged);

    public static readonly BindableProperty IsReadOnlyProperty =
        BindableProperty.Create(nameof(IsReadOnly), typeof(bool), typeof(FootballFieldView), false);

    public event EventHandler<string>? SlotTapped;

    public FootballFieldView()
    {
        EnableTouchEvents = true;
        HeightRequest = 260;
        PaintSurface += OnPaintSurface;
        Touch += OnTouch;
    }

    public IReadOnlyList<PositionDto>? Positions
    {
        get => (IReadOnlyList<PositionDto>?)GetValue(PositionsProperty);
        set => SetValue(PositionsProperty, value);
    }

    public Guid? CurrentUserId
    {
        get => (Guid?)GetValue(CurrentUserIdProperty);
        set => SetValue(CurrentUserIdProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    private static void OnVisualChanged(BindableObject bindable, object oldValue, object newValue)
        => ((FootballFieldView)bindable).InvalidateSurface();

    private float SlotRadius(SKImageInfo info) => Math.Min(info.Width, info.Height) * 0.052f;

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        canvas.Clear(SKColor.Parse("#0B6B2E"));

        DrawPitch(canvas, info);
        DrawSlots(canvas, info);
    }

    private static void DrawPitch(SKCanvas canvas, SKImageInfo info)
    {
        using var line = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(255, 255, 255, 180),
            StrokeWidth = Math.Max(2f, info.Width * 0.004f),
            IsAntialias = true,
        };

        var margin = info.Width * 0.02f;
        var rect = new SKRect(margin, margin, info.Width - margin, info.Height - margin);
        canvas.DrawRoundRect(rect, 12, 12, line);

        // Halfway line (home on left, away on right).
        var midX = info.Width / 2f;
        canvas.DrawLine(midX, rect.Top, midX, rect.Bottom, line);

        // Center circle + spot.
        var centerRadius = Math.Min(info.Width, info.Height) * 0.12f;
        canvas.DrawCircle(midX, info.Height / 2f, centerRadius, line);
        using var fill = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(255, 255, 255, 180), IsAntialias = true };
        canvas.DrawCircle(midX, info.Height / 2f, Math.Max(2f, info.Width * 0.006f), fill);

        // Penalty boxes.
        var boxH = rect.Height * 0.5f;
        var boxW = rect.Width * 0.12f;
        var topY = (info.Height - boxH) / 2f;
        canvas.DrawRect(new SKRect(rect.Left, topY, rect.Left + boxW, topY + boxH), line);
        canvas.DrawRect(new SKRect(rect.Right - boxW, topY, rect.Right, topY + boxH), line);
    }

    private void DrawSlots(SKCanvas canvas, SKImageInfo info)
    {
        var positions = Positions;
        if (positions is null || positions.Count == 0)
            return;

        var radius = SlotRadius(info);
        using var labelFont = new SKFont { Size = radius * 0.7f };
        using var initialsFont = new SKFont { Size = radius * 0.8f, Embolden = true };

        foreach (var p in positions)
        {
            var cx = (float)(p.X * info.Width);
            var cy = (float)(p.Y * info.Height);
            var claimed = p.UserId.HasValue;
            var isMine = CurrentUserId.HasValue && p.UserId == CurrentUserId;

            if (claimed)
            {
                var color = ToSk(AvatarFormatter.ColorFor(p.UserHandle ?? p.UserDisplayName ?? p.SlotId));
                using var fill = new SKPaint { Style = SKPaintStyle.Fill, Color = color, IsAntialias = true };
                canvas.DrawCircle(cx, cy, radius, fill);

                using var ring = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    Color = isMine ? SKColors.Gold : SKColors.White,
                    StrokeWidth = isMine ? radius * 0.22f : radius * 0.12f,
                    IsAntialias = true,
                };
                canvas.DrawCircle(cx, cy, radius, ring);

                using var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
                var initials = AvatarFormatter.Initials(p.UserDisplayName ?? p.UserHandle);
                DrawCenteredText(canvas, initials, cx, cy, initialsFont, textPaint);
            }
            else
            {
                using var fill = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(0, 0, 0, 60), IsAntialias = true };
                canvas.DrawCircle(cx, cy, radius, fill);

                using var dash = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    Color = SKColors.White,
                    StrokeWidth = radius * 0.1f,
                    IsAntialias = true,
                    PathEffect = SKPathEffect.CreateDash([radius * 0.4f, radius * 0.3f], 0),
                };
                canvas.DrawCircle(cx, cy, radius, dash);

                using var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
                DrawCenteredText(canvas, p.Label, cx, cy, labelFont, textPaint);
            }
        }
    }

    private static void DrawCenteredText(SKCanvas canvas, string text, float cx, float cy, SKFont font, SKPaint paint)
    {
        var metrics = font.Metrics;
        var baseline = cy - (metrics.Ascent + metrics.Descent) / 2f;
        canvas.DrawText(text, cx, baseline, SKTextAlign.Center, font, paint);
    }

    private static SKColor ToSk(Microsoft.Maui.Graphics.Color color)
        => new((byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255));

    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        if (IsReadOnly)
        {
            e.Handled = true;
            return;
        }

        if (e.ActionType != SKTouchAction.Pressed)
        {
            e.Handled = true;
            return;
        }

        var positions = Positions;
        var size = CanvasSize;
        if (positions is not null && size.Width > 0)
        {
            var radius = SlotRadius(new SKImageInfo((int)size.Width, (int)size.Height));
            var hitRadius = radius * 1.4f;

            PositionDto? best = null;
            var bestDist = float.MaxValue;
            foreach (var p in positions)
            {
                var cx = (float)(p.X * size.Width);
                var cy = (float)(p.Y * size.Height);
                var dist = (float)Math.Sqrt(Math.Pow(e.Location.X - cx, 2) + Math.Pow(e.Location.Y - cy, 2));
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = p;
                }
            }

            if (best is not null && bestDist <= hitRadius)
                SlotTapped?.Invoke(this, best.SlotId);
        }

        e.Handled = true;
    }
}
