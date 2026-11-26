using Winzer.Engine;
using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;

namespace Winzer.Ui;

/// <summary>Farben und Schriften der Oberfläche</summary>
public static class Theme
{
    public const string Serif = "Georgia";
    public const string Sans = "Segoe UI";

    public static Color C(string hex) => Colors.ToColor(hex);

    public static readonly Color Ink = C("#3a1d24");
    public static readonly Color InkSoft = C("#6a4a2a");
    public static readonly Color Paper = C("#fbf4e4");
    public static readonly Color PaperDark = C("#eedfc2");
    public static readonly Color PaperLine = C("#d9c7a3");
    public static readonly Color Cream = C("#f4ead7");
    public static readonly Color CreamSoft = C("#d9c7a3");
    public static readonly Color Gold = C("#f6d477");
    public static readonly Color GoldDark = C("#c8962e");
    public static readonly Color Wine = C("#8e2440");
    public static readonly Color WineDark = C("#5c1429");
    public static readonly Color Alert = C("#b3261e");
    public static readonly Color Good = C("#4d7a2a");
    public static readonly Color GoodLight = C("#7dbb6a");
    public static readonly Color Warn = C("#e0884c");
    public static readonly Color Night = C("#1c1418");
    public static readonly Color Glass = C("#d91c1418");
    public static readonly Color GlassLight = C("#1ff4ead7");
    public static readonly Color GlassLine = C("#40f4ead7");
    public static readonly Color Clear = C("#00000000");

    public static readonly SKColor SkGold = SKColor.Parse("#f6d477");
    public static readonly SKColor SkGoldDark = SKColor.Parse("#c8962e");
    public static readonly SKColor SkCream = SKColor.Parse("#f4ead7");
    public static readonly SKColor SkCreamSoft = SKColor.Parse("#d9c7a3");
    public static readonly SKColor SkWine = SKColor.Parse("#8e2440");
    public static readonly SKColor SkWineDark = SKColor.Parse("#5c1429");
    public static readonly SKColor SkInk = SKColor.Parse("#3a1d24");
    public static readonly SKColor SkNight = SKColor.Parse("#1c1418");
}

public enum Align { Start, Center, End }

public enum ButtonKind { Primary, Secondary, Ghost, Gold, Dark }

/// <summary>Fläche, die frei mit SkiaSharp bemalt wird – für Panorama, Tanks, Symbole und Anzeigen.</summary>
public sealed class PaintBox : SkiaControl
{
    /// <summary>Entwurfsgröße: Der Maler zeichnet in Koordinaten 0…Width × 0…Height.</summary>
    public float DesignWidth { get; init; }
    public float DesignHeight { get; init; }
    public Action<SKCanvas, float, float>? Painter { get; init; }

    /// <summary>Wie oft frei bemalte Flächen tatsächlich gezeichnet wurden – Nachweis für Prüfläufe, dass das Fenster wirklich malt</summary>
    public static int Paints { get; private set; }

    protected override void Paint(DrawingContext ctx)
    {
        base.Paint(ctx);
        Paints++;
        if (Painter is null || DesignWidth <= 0 || DesignHeight <= 0) return;
        var canvas = ctx.Context.Canvas;
        var r = ctx.Destination;
        canvas.Save();
        canvas.Translate(r.Left, r.Top);
        canvas.Scale(r.Width / DesignWidth, r.Height / DesignHeight);
        Painter(canvas, DesignWidth, DesignHeight);
        canvas.Restore();
    }
}

/// <summary>
/// Bausteine der Oberfläche. Alle Maße sind Entwurfseinheiten einer Fläche von 1920 × 1080;
/// <see cref="S"/> rechnet sie auf die Fenstergröße um. Positionen gelten relativ zum Elternelement.
/// </summary>
public static class Kit
{
    public const double DesignWidth = 1920;
    public const double DesignHeight = 1080;

    /// <summary>Faktor Entwurfseinheit → Bildschirmpunkt; wird vor jedem Aufbau gesetzt.</summary>
    public static double S { get; set; } = 1;

    public static T At<T>(this T control, double x, double y, double w, double h) where T : SkiaControl
    {
        control.HorizontalOptions = LayoutOptions.Start;
        control.VerticalOptions = LayoutOptions.Start;
        control.Margin = new Thickness(x * S, y * S, 0, 0);
        control.WidthRequest = w * S;
        control.HeightRequest = h * S;
        return control;
    }

    public static T With<T>(this T parent, params SkiaControl?[] children) where T : SkiaLayout
    {
        foreach (var child in children)
        {
            if (child is not null) parent.Children.Add(child);
        }
        return parent;
    }

    /// <summary>Wie viele Tippflächen gerade unter dem Mauszeiger liegen – das Fenster zeigt dann die Hand</summary>
    public static int Hovering { get; private set; }
    public static event Action? HoverChanged;

    /// <summary>Vor jedem Neuaufbau: Die alten Flächen gibt es nicht mehr, also liegt auch keine mehr unter dem Zeiger.</summary>
    public static void ResetHover()
    {
        if (Hovering == 0) return;
        Hovering = 0;
        HoverChanged?.Invoke();
    }

    /// <summary>
    /// Fängt Eingaben ab, ohne wie eine Schaltfläche auszusehen: für Dialogflächen und den abgedunkelten Hintergrund.
    /// Kein Aufhellen unter dem Mauszeiger – sonst leuchtete der ganze Dialog.
    /// </summary>
    public static T Block<T>(this T control, Action? action = null) where T : SkiaControl
    {
        control.OnTapped(_ => action?.Invoke());
        return control;
    }

    // ---- Kurzhinweis (Tooltip) ----

    static SkiaShape? tipBox;
    static SkiaLabel? tipLabel;
    static SkiaControl? tipOwner;
    static double tipHostWidth, tipHostHeight;

    /// <summary>Legt den Kurzhinweis für diesen Aufbau an; <paramref name="width"/> und <paramref name="height"/> sind die Fenstergröße in Punkten.</summary>
    public static SkiaControl TipLayer(double width, double height)
    {
        (tipHostWidth, tipHostHeight, tipOwner) = (width / S, height / S, null);
        tipLabel = Text("", 14, 7, 300, 26, 17, Theme.Cream, align: Align.Center);
        tipBox = Box(0, 0, 328, 40, Theme.C("#f51c1418"), 12, Theme.Gold, 1.5, shadow: true);
        tipBox.With(tipLabel);
        tipBox.IsVisible = false;
        tipBox.InputTransparent = true;
        tipBox.ZIndex = 6000;
        return tipBox;
    }

    /// <summary>
    /// Kurzhinweis unter dem Mauszeiger – für Schaltflächen, die nur ein Zeichen oder Bild tragen und deren Zweck
    /// sonst nirgends steht.
    /// </summary>
    public static T Tip<T>(this T control, string text) where T : SkiaControl
    {
        control.ReceivesHover = true;
        control.OnHovered((c, on) =>
        {
            if (tipBox is null || tipLabel is null) return;
            if (!on)
            {
                if (ReferenceEquals(tipOwner, c)) tipBox.IsVisible = false;
                return;
            }
            var rect = c.DrawingRect;
            var scale = c.RenderingScale;
            if (scale <= 0 || rect.Width <= 0) return;
            // Bildpunkte der Fläche → Entwurfseinheiten
            double Unit(float pixels) => pixels / scale / S;
            var width = Math.Min(420, text.Length * 9.4 + 34);
            const double height = 40;
            var x = Math.Clamp(Unit(rect.MidX) - width / 2, 6, Math.Max(6, tipHostWidth - width - 6));
            var below = Unit(rect.Bottom) + 8;
            var y = below + height > tipHostHeight - 4 ? Unit(rect.Top) - 8 - height : below;
            tipLabel.Text = text;
            tipLabel.WidthRequest = (width - 28) * S;
            tipBox.At(x, y, width, height);
            tipBox.IsVisible = true;
            tipOwner = c;
        });
        return control;
    }

    /// <summary>
    /// Macht ein Element antippbar. Unter dem Mauszeiger hellt es sich mit einem durchscheinenden Schleier und
    /// goldenem Rand auf, damit erkennbar ist, was sich anklicken lässt.
    /// </summary>
    public static T Tap<T>(this T control, Action action) where T : SkiaControl
    {
        control.OnTapped(_ => action());
        if (control is SkiaLayout layout)
        {
            var veil = new SkiaShape
            {
                Type = ShapeType.Rectangle,
                BackgroundColor = Theme.C("#2effffff"),
                StrokeColor = Theme.C("#d9f6d477"),
                StrokeWidth = 2 * S,
                IsVisible = false,
                InputTransparent = true,
                ZIndex = 1000,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
            };
            veil.CornerRadius = layout is SkiaShape shape ? shape.CornerRadius : 10 * S;
            layout.Children.Add(veil);
            var over = false;
            control.ReceivesHover = true;
            control.OnHovered((_, on) =>
            {
                if (on == over) return;
                over = on;
                veil.IsVisible = on;
                Hovering = Math.Max(0, Hovering + (on ? 1 : -1));
                HoverChanged?.Invoke();
            });
        }
        return control;
    }

    /// <summary>
    /// Lässt ein Element beim ersten Erscheinen einblenden und dabei ein Stück aufsteigen. <paramref name="key"/> benennt
    /// den Anlass (alle Elemente eines Anlasses teilen eine Uhr), <paramref name="delay"/> staffelt sie in Sekunden.
    /// </summary>
    public static T Reveal<T>(this T control, string key, double delay, double rise = 14, double seconds = 0.32) where T : SkiaControl
    {
        Anim.Drive(() =>
        {
            var p = Math.Clamp((Anim.Since(key) - delay) / seconds, 0, 1);
            control.Opacity = Anim.EaseOut(p);
            control.TranslationY = (1 - Anim.EaseOut(p)) * rise * S;
            return p < 1;
        });
        return control;
    }

    /// <summary>Unsichtbarer Behälter mit eigenen Koordinaten</summary>
    public static SkiaLayout Layer(double x, double y, double w, double h) =>
        new SkiaLayout { Type = LayoutType.Absolute }.At(x, y, w, h);

    public static SkiaGradient Vertical(Color top, Color bottom) => new()
    {
        Type = GradientType.Linear,
        StartXRatio = 0.5f, StartYRatio = 0, EndXRatio = 0.5f, EndYRatio = 1,
        Colors = { top, bottom },
    };

    public static SkiaShape Box(double x, double y, double w, double h, Color? fill = null, double radius = 0,
        Color? stroke = null, double strokeWidth = 0, SkiaGradient? gradient = null, bool shadow = false)
    {
        var box = new SkiaShape { Type = ShapeType.Rectangle, CornerRadius = radius * S }.At(x, y, w, h);
        if (fill is not null) box.BackgroundColor = fill;
        if (gradient is not null) box.FillGradient = gradient;
        if (stroke is not null)
        {
            box.StrokeColor = stroke;
            box.StrokeWidth = strokeWidth * S;
        }
        if (shadow) box.Shadows.Add(new SkiaShadow { X = 0, Y = 6 * S, Blur = 10 * S, Color = Colors.Black, Opacity = 0.4 });
        return box;
    }

    /// <summary>Helle Karte auf Pergament</summary>
    public static SkiaShape Card(double x, double y, double w, double h, double radius = 18) =>
        Box(x, y, w, h, radius: radius, gradient: Vertical(Theme.Paper, Theme.PaperDark), shadow: true);

    /// <summary>Dunkle, halbtransparente Tafel</summary>
    public static SkiaShape Glass(double x, double y, double w, double h, double radius = 18, bool shadow = true) =>
        Box(x, y, w, h, Theme.Glass, radius, shadow: shadow);

    static readonly SKTypeface SansFace = SKTypeface.FromFamilyName(Theme.Sans);

    // Maße der Schrift je Punkt Schriftgröße: Grundlinie unter der Oberkante des Labels, Höhe der Großbuchstaben
    static readonly (double Ascent, double Cap) SansMetrics = MeasureSans();

    static (double, double) MeasureSans()
    {
        using var font = new SKFont(SansFace, 100);
        var metrics = font.Metrics;
        return (-metrics.Ascent / 100, metrics.CapHeight / 100);
    }

    /// <summary>
    /// Oberkante eines einzeiligen Labels, damit die Buchstaben (Großbuchstabenhöhe bis Grundlinie) senkrecht in der
    /// Mitte von <paramref name="center"/> sitzen. Die Zeilenbox selbst zu zentrieren setzt den Text zu tief, weil die
    /// Schrift oberhalb der Großbuchstaben mehr Raum lässt als unter der Grundlinie.
    /// </summary>
    static double CapTop(double center, double size) => center - (SansMetrics.Ascent - SansMetrics.Cap / 2) * size;

    /// <summary>Einzeiliger Text, dessen Buchstaben senkrecht mittig in einer Fläche der Höhe <paramref name="boxHeight"/> sitzen (Schilder, Reiter)</summary>
    public static SkiaLabel Centered(string text, double x, double w, double boxHeight, double size, Color color, bool bold = false, Align align = Align.Center) =>
        Text(text, x, CapTop(boxHeight / 2, size), w, size * 1.5, size, color, bold: bold, align: align);

    /// <summary>
    /// Gemessene Breite eines einzeiligen Textes in Entwurfseinheiten. Entscheidet, ob ein Text umbrechen muss –
    /// eine Schätzung über die Zeichenzahl lag oft daneben und reservierte dann Platz für eine Zeile, die es nicht gab.
    /// </summary>
    public static double TextWidth(string text, double size)
    {
        using var font = new SKFont(SansFace, (float)size);
        return font.MeasureText(text);
    }

    static bool NeedsRich(string text)
    {
        foreach (var ch in text)
        {
            if (char.IsSurrogate(ch) || ch is >= '←' and <= '⯿' || ch == '️') return true;
        }
        return false;
    }

    public static SkiaLabel Text(string text, double x, double y, double w, double h, double size, Color color,
        bool bold = false, bool serif = false, Align align = Align.Start, int maxLines = 1)
    {
        var label = NeedsRich(text) ? new SkiaRichLabel(text) : new SkiaLabel(text);
        label.FontSize = size * S;
        label.TextColor = color;
        label.FontFamily = serif ? Theme.Serif : Theme.Sans;
        if (bold)
        {
            label.FontAttributes = FontAttributes.Bold;
            label.FontWeight = 700;
        }
        label.HorizontalTextAlignment = align switch
        {
            Align.Center => DrawTextAlignment.Center,
            Align.End => DrawTextAlignment.End,
            _ => DrawTextAlignment.Start,
        };
        label.MaxLines = maxLines;
        label.LineBreakMode = LineBreakMode.TailTruncation;
        return label.At(x, y, w, h);
    }

    public static PaintBox Paint(double x, double y, double w, double h, Action<SKCanvas, float, float> painter) =>
        new PaintBox { DesignWidth = (float)w, DesignHeight = (float)h, Painter = painter }.At(x, y, w, h);

    /// <summary>
    /// Schaltfläche mit Titel und optionaler Unterzeile. Ist <paramref name="problem"/> gesetzt, ist sie gesperrt
    /// und zeigt den Grund statt der Unterzeile – außer mit <paramref name="quiet"/>, dann bleibt es beim Titel.
    /// </summary>
    public static SkiaShape Button(string title, double x, double y, double w, double h, Action onTap,
        ButtonKind kind = ButtonKind.Secondary, string? sub = null, string? problem = null, double fontSize = 21, Align align = Align.Start, bool quiet = false)
    {
        var disabled = problem is not null;
        SkiaShape box;
        Color titleColor, subColor;
        switch (kind)
        {
            case ButtonKind.Primary:
                box = Box(x, y, w, h, radius: 16, gradient: Vertical(Theme.Wine, Theme.WineDark));
                (titleColor, subColor) = (Theme.C("#fff6e6"), Theme.C("#f0cfd6"));
                break;
            case ButtonKind.Gold:
                box = Box(x, y, w, h, radius: 16, gradient: Vertical(Theme.Gold, Theme.GoldDark));
                (titleColor, subColor) = (Theme.Ink, Theme.Ink);
                break;
            case ButtonKind.Dark:
                box = Box(x, y, w, h, Theme.GlassLight, 16, Theme.GlassLine, 1.5);
                (titleColor, subColor) = (Theme.Cream, Theme.CreamSoft);
                break;
            case ButtonKind.Ghost:
                box = Box(x, y, w, h, Theme.PaperLine, 16);
                (titleColor, subColor) = (Theme.Ink, Theme.InkSoft);
                break;
            default:
                box = Box(x, y, w, h, Theme.Clear, 16, Theme.Wine, 3);
                (titleColor, subColor) = (Theme.Ink, Theme.InkSoft);
                break;
        }
        // quiet: nur der Titel – gesperrt wird die Schaltfläche blass, der Grund steht schon daneben
        var line = quiet ? null : sub ?? problem;
        var pad = align == Align.Center ? 8 : 22;
        if (line is null)
        {
            box.With(Text(title, pad, CapTop(h / 2, fontSize), w - 2 * pad, fontSize * 1.5, fontSize, titleColor, bold: true, align: align));
        }
        else
        {
            var text = disabled ? problem! : line;
            // Lange Unterzeilen (vor allem Sperrgründe der Engine) brechen auf zwei Zeilen um, wenn die Höhe reicht
            // Zwei Zeilen nur, wenn der Text wirklich nicht in eine passt; sonst säße der Block zu hoch
            // Passt er knapp nicht, wird er erst etwas kleiner gesetzt, bevor er umbricht
            var room = (w - 2 * pad) * 0.97;
            var subSize = TextWidth(text, 16) <= room ? 16 : 14.5;
            var twoLines = TextWidth(text, subSize) > room && h >= fontSize * 1.4 + 50;
            if (twoLines) subSize = 15;
            var subHeight = twoLines ? 46 : 24;
            // Titel und Unterzeile als ein Block, senkrecht in der Schaltfläche zentriert: von der Oberkante der
            // Großbuchstaben im Titel bis zur Grundlinie der letzten Unterzeile
            var (ascent, cap) = SansMetrics;
            var glyphs = fontSize * 1.4 - (ascent - cap) * fontSize + ascent * subSize + (twoLines ? subHeight - subSize * 1.4 : 0);
            var top = Math.Max(4, (h - glyphs) / 2 - (ascent - cap) * fontSize);
            box.With(
                Text(title, pad, top, w - 2 * pad, fontSize * 1.5, fontSize, titleColor, bold: true, align: align),
                Text(text, pad, top + fontSize * 1.4, w - 2 * pad, subHeight, subSize, subColor, align: align, maxLines: twoLines ? 2 : 1));
        }
        if (disabled) box.Opacity = 0.5;
        else box.Tap(onTap);
        return box;
    }

    /// <summary>Kleine runde Schaltfläche mit einem Zeichen, z. B. zum Blättern</summary>
    public static SkiaShape IconButton(string glyph, double x, double y, double size, Action onTap, bool dark = true, bool enabled = true, string? tip = null)
    {
        var box = Box(x, y, size, size, dark ? Theme.GlassLight : Theme.PaperLine, size / 4);
        box.With(Text(glyph, 0, size * 0.14, size, size * 0.7, size * 0.48, dark ? Theme.Cream : Theme.Ink, bold: true, align: Align.Center));
        if (enabled) box.Tap(onTap);
        else box.Opacity = 0.35;
        if (tip is not null && enabled) box.Tip(tip);
        return box;
    }

    /// <summary>Roter Zähler für offene Aufgaben</summary>
    public static SkiaControl Badge(int count, double cx, double cy, double r = 14) =>
        Box(cx - r, cy - r, 2 * r, 2 * r, Theme.Alert, r)
            .With(Text(count > 9 ? "9+" : count.ToString(), 0, r * 0.28, 2 * r, r * 1.5, r * 1.15, Colors.White, bold: true, align: Align.Center));

    /// <summary>„Seite 1 von 3“ mit Pfeilen. <paramref name="setPage"/> bekommt die neue Seite (ab 0).</summary>
    public static SkiaControl Pager(double x, double y, double w, int page, int pageCount, Action<int> setPage, bool dark = true, string noun = "Seite")
    {
        var layer = Layer(x, y, w, 36);
        var color = dark ? Theme.CreamSoft : Theme.InkSoft;
        layer.With(Text(Lang.En ? $"{(noun == "Seite" ? "Page" : noun)} {page + 1} of {Math.Max(1, pageCount)}" : $"{noun} {page + 1} von {Math.Max(1, pageCount)}", 0, 7, w - 90, 24, 16, color));
        layer.With(IconButton("‹", w - 80, 0, 36, () => setPage(page - 1), dark, page > 0, Lang.T("Zurückblättern", "Previous page")));
        layer.With(IconButton("›", w - 38, 0, 36, () => setPage(page + 1), dark, page < pageCount - 1, Lang.T("Weiterblättern", "Next page")));
        return layer;
    }

    /// <summary>Reiter innerhalb einer Szene</summary>
    public static SkiaControl Tabs(double x, double y, string[] labels, int active, Action<int> select, int[]? badges = null)
    {
        var layer = Layer(x, y, 1200, 56);
        var left = 0.0;
        for (var i = 0; i < labels.Length; i++)
        {
            var index = i;
            var width = 44 + labels[i].Length * 11.5;
            var on = i == active;
            var tab = on
                ? Box(left, 4, width, 46, radius: 23, gradient: Vertical(Theme.Gold, Theme.GoldDark))
                : Box(left, 4, width, 46, Theme.GlassLight, 23);
            tab.With(Centered(labels[i], 0, width, 46, 20, on ? Theme.Ink : Theme.Cream, bold: true));
            tab.Tap(() => select(index));
            layer.With(tab);
            if (badges is not null && badges[i] > 0) layer.With(Badge(badges[i], left + width - 6, 8, 12));
            left += width + 12;
        }
        return layer;
    }

    /// <summary>Zeile „Bezeichnung … Wert“ im Inspektor</summary>
    public static SkiaControl Row(string label, string value, double x, double y, double w, Color? valueColor = null, double size = 20, Color? labelColor = null) =>
        Layer(x, y, w, size * 1.6).With(
            // Auf dunklem Grund braucht die Beschriftung eine helle Farbe (labelColor)
            Text(label, 0, 0, w * 0.5, size * 1.5, size, labelColor ?? Theme.InkSoft),
            Text(value, w * 0.35, 0, w * 0.65, size * 1.5, size, valueColor ?? Theme.Ink, bold: true, align: Align.End));

    /// <summary>Schieber für einen Anteil 0–1 mit Schnellwahl ¼, ½, alles.</summary>
    public static SkiaControl AmountSlider(double x, double y, double w, double share, string caption, Action<double> set)
    {
        var layer = Layer(x, y, w, 76);
        var track = Box(0, 8, w, 12, Theme.PaperLine, 6);
        track.With(Box(0, 0, Math.Max(12, w * share), 12, Theme.Wine, 6));
        layer.With(track);
        // Zehn Tippzonen über der Spur: grob einstellen ohne Ziehen
        for (var i = 1; i <= 10; i++)
        {
            var value = i / 10.0;
            layer.With(Box((i - 1) * w / 10, 0, w / 10, 28, Theme.Clear).Tap(() => set(value)));
        }
        layer.With(Box(Math.Clamp(w * share - 17, 0, w - 34), -3, 34, 34, Theme.Wine, 17, Theme.Paper, 4));
        layer.With(Text(caption, 0, 42, w - 230, 26, 18, Theme.InkSoft));
        string[] names = ["¼", "½", Lang.T("alles", "all")];
        double[] values = [0.25, 0.5, 1];
        for (var i = 0; i < 3; i++)
        {
            var value = values[i];
            var on = Math.Abs(share - value) < 0.001;
            layer.With(Box(w - 228 + i * 78, 40, 72, 34, on ? Theme.Wine : Theme.PaperLine, 17)
                .With(Text(names[i], 0, 6, 72, 24, 17, on ? Theme.Paper : Theme.Ink, bold: true, align: Align.Center))
                .Tap(() => set(value)));
        }
        return layer;
    }
}

/// <summary>Zeichenhilfen für <see cref="PaintBox"/></summary>
public static class Draw
{
    public static SKPaint Fill(SKColor color) => new() { IsAntialias = true, Color = color, Style = SKPaintStyle.Fill };

    public static SKPaint Fill(string hex) => Fill(SKColor.Parse(hex));

    public static SKPaint Stroke(SKColor color, float width) =>
        new() { IsAntialias = true, Color = color, Style = SKPaintStyle.Stroke, StrokeWidth = width, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };

    public static SKPaint Stroke(string hex, float width) => Stroke(SKColor.Parse(hex), width);

    public static SKPaint VerticalGradient(float top, float bottom, params (float At, string Hex)[] stops) => new()
    {
        IsAntialias = true,
        Shader = SKShader.CreateLinearGradient(new SKPoint(0, top), new SKPoint(0, bottom),
            stops.Select(s => SKColor.Parse(s.Hex)).ToArray(), stops.Select(s => s.At).ToArray(), SKShaderTileMode.Clamp),
    };

    public static SKPaint HorizontalGradient(float left, float right, params (float At, string Hex)[] stops) => new()
    {
        IsAntialias = true,
        Shader = SKShader.CreateLinearGradient(new SKPoint(left, 0), new SKPoint(right, 0),
            stops.Select(s => SKColor.Parse(s.Hex)).ToArray(), stops.Select(s => s.At).ToArray(), SKShaderTileMode.Clamp),
    };

    public static void Path(SKCanvas canvas, string svgPath, SKPaint paint)
    {
        using var path = SKPath.ParseSvgPathData(svgPath);
        canvas.DrawPath(path, paint);
    }

    /// <summary>Ring mit Füllstand 0–1, beginnend oben</summary>
    public static void Ring(SKCanvas canvas, float cx, float cy, float radius, float width, double value, SKColor track, SKColor color)
    {
        using var back = Stroke(track, width);
        canvas.DrawCircle(cx, cy, radius, back);
        var sweep = (float)(Math.Clamp(value, 0, 1) * 360);
        if (sweep <= 0) return;
        using var front = Stroke(color, width);
        if (sweep >= 359.9f) canvas.DrawCircle(cx, cy, radius, front);
        else canvas.DrawArc(new SKRect(cx - radius, cy - radius, cx + radius, cy + radius), -90, sweep, false, front);
    }
}
