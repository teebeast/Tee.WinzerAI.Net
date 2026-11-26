using System.Globalization;
using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

/// <summary>
/// Markt: Hofladen bzw. Vinothek als Bühne. Vier Reiter – Aufträge &amp; Export, Großhandel, Marketing, Prämierungen.
/// Links die Dinge zum Auswählen, rechts der Inspektor mit Werten und Aktionen.
/// </summary>
public static partial class MarketScene
{
    static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    // Als Eigenschaft, damit die Sprache beim Lesen gilt
    static string[] TabNames => [Lang.T("Aufträge & Export", "Orders & export"), Lang.T("Großhandel", "Wholesale"), "Marketing", Lang.T("Prämierungen", "Wine awards")];

    public static SkiaControl Build(GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var scene = Kit.Layer(0, 0, Shell.StageWidth, Shell.StageHeight);
        scene.With(Kit.Paint(0, 0, Shell.StageWidth, Shell.StageHeight, (c, w, h) => PaintShop(c, w, h, player)));

        var tab = Math.Clamp(session.Nav.Tab, 0, TabNames.Length - 1);
        int[] badges = [player.OrderOffers.Count + player.Orders.Count + (player.ExportOffer is null ? 0 : 1), 0, 0, 0];
        scene.With(Kit.Tabs(28, 20, TabNames, tab, session.Nav.SetTab, badges));

        switch (tab)
        {
            case 1: BuildWholesale(session, scene); break;
            case 2: BuildMarketing(session, scene); break;
            case 3: BuildAwards(session, scene); break;
            default: BuildOrders(session, scene); break;
        }
        return scene;
    }

    // ---- Gemeinsame Bausteine der Markt-Reiter ----

    /// <summary>Inspektor rechts: Pergamentkarte, Inhalt in eigenen Koordinaten (Rand 32, Breite 460)</summary>
    internal static SkiaShape Inspector() => Kit.Card(1260, 20, 524, 948, 22);

    internal static SkiaControl Rule(double y) => Kit.Box(32, y, 460, 2, Theme.PaperLine);

    /// <summary>Preis je Flasche mit Cent, z. B. „8,40 €“</summary>
    internal static string Price(double value) => Lang.En ? "€" + value.ToString("#,##0.00", CultureInfo.InvariantCulture) : value.ToString("#,##0.00", German) + " €";

    internal static string Signed(double value, int decimals = 1) =>
        (value < 0 ? "−" : "+") + Util.FormatNumber(Math.Abs(value), decimals);

    /// <summary>Füllstandsbalken</summary>
    internal static SkiaControl Meter(double x, double y, double w, double share, Color fill, Color? track = null, double h = 10)
    {
        var bar = Kit.Box(x, y, w, h, track ?? Theme.PaperLine, h / 2);
        var value = Math.Clamp(share, 0, 1);
        if (value > 0) bar.With(Kit.Box(0, 0, Math.Max(h, w * value), h, fill, h / 2));
        return bar;
    }

    /// <summary>Zeile „Bezeichnung … Wert“ auf dunkler Tafel</summary>
    internal static SkiaControl DarkRow(string label, string value, double x, double y, double w, Color? valueColor = null, double size = 18) =>
        Kit.Layer(x, y, w, size * 1.6).With(
            Kit.Text(label, 0, 0, w * 0.55, size * 1.5, size, Theme.CreamSoft),
            Kit.Text(value, w * 0.4, 0, w * 0.6, size * 1.5, size, valueColor ?? Theme.Cream, bold: true, align: Align.End));

    /// <summary>Kleines Schild, z. B. „+6 Punkte“</summary>
    internal static SkiaControl Chip(string text, double x, double y, double w, Color fill, Color color, double size = 15) =>
        Kit.Box(x, y, w, size * 2, fill, size).With(Kit.Centered(text, 0, w, size * 2, size, color, bold: true));

    /// <summary>Goldener Rahmen über einer gewählten Karte – als letztes Kind anhängen; null, wenn nicht gewählt</summary>
    internal static SkiaControl? SelectionFrame(bool selected, double w, double h, double radius = 16) =>
        selected ? Kit.Box(0, 0, w, h, Theme.Clear, radius, Theme.Gold, 6) : null;

    /// <summary>Leerer Platz in einem Raster</summary>
    internal static SkiaShape EmptySlot(double x, double y, double w, double h, string text)
    {
        var slot = Kit.Box(x, y, w, h, Theme.C("#8c1c1418"), 16, Theme.GlassLine, 1.5);
        slot.With(Kit.Text(text, 24, h / 2 - 40, w - 48, 84, 18, Theme.CreamSoft, align: Align.Center, maxLines: 3));
        return slot;
    }

    /// <summary>Blättern unter einem Raster, auf dunkler Pille</summary>
    internal static SkiaControl GridPager(GameSession session, string list, int page, int pages, string noun, double y)
    {
        var pill = Kit.Box(930, y, 300, 44, Theme.C("#c71c1418"), 22);
        pill.With(Kit.Pager(20, 4, 268, page, pages, p => session.Nav.SetPage(list, p), noun: noun));
        return pill;
    }

    internal static string GrapeName(WineInfo wine) => Grapes.All[wine.Grape].Name.Replace(" (PIWI)", "") + (wine.Rose ? " Rosé" : "");

    internal static string KindName(BottleLot lot) => lot.Sparkling ? Lang.T("Sekt", "Sparkling") : Quality.QualityInfo(lot.Level).Name;

    internal static string ShortLevel(BottleLot lot) => lot.Sparkling ? "SEKT" : lot.Level switch
    {
        QualityLevel.DeutscherWein => "DW",
        QualityLevel.Qualitaetswein => "QbA",
        QualityLevel.Kabinett => "KAB",
        QualityLevel.Spaetlese => "SPL",
        QualityLevel.Auslese => "ASL",
        QualityLevel.Beerenauslese => "BA",
        QualityLevel.Trockenbeerenauslese => "TBA",
        _ => "EIS",
    };

    /// <summary>Flasche mit Etikett und Kürzel der Stufe</summary>
    internal static SkiaControl Bottle(BottleLot lot, double x, double y, double w, double h)
    {
        var layer = Kit.Layer(x, y, w, h);
        layer.With(Kit.Paint(0, 0, w, h, (c, pw, ph) => PaintBottle(c, pw, ph, lot)));
        var size = w * 0.2;
        layer.With(Kit.Text(ShortLevel(lot), w * 0.17, h * 0.615, w * 0.66, size * 1.5, size, Theme.Ink, bold: true, align: Align.Center));
        return layer;
    }

    static void PaintBottle(SKCanvas c, float w, float h, BottleLot lot)
    {
        var red = Grapes.All[lot.Grape].Color == GrapeColor.Rot;
        var (top, bottom) = lot.Rose ? ("#f2b6bd", "#d98591") : lot.Sparkling ? ("#3f5a2c", "#1f3016") : red ? ("#4a2330", "#1f0d14") : ("#8fa64a", "#55702a");
        c.Save();
        c.Scale(w / 60f, h / 180f);
        using (var shadow = Draw.Fill(SKColors.Black.WithAlpha(40))) c.DrawOval(30, 176, 24, 4, shadow);
        const string shape = "M24 6 H36 V44 C36 58 50 60 50 78 V168 Q50 176 42 176 H18 Q10 176 10 168 V78 C10 60 24 58 24 44 Z";
        using (var glass = Draw.VerticalGradient(0, 180, (0, top), (1, bottom))) Draw.Path(c, shape, glass);
        using (var shine = Draw.Stroke(SKColors.White.WithAlpha(70), 3)) Draw.Path(c, "M17 82 V160", shine);
        // Kapsel – Sekt trägt Goldfolie bis auf die Schulter
        if (lot.Sparkling)
        {
            using var foil = Draw.VerticalGradient(0, 56, (0, "#f6d477"), (1, "#c8962e"));
            Draw.Path(c, "M23 4 H37 V46 C37 52 40 55 43 58 H17 C20 55 23 52 23 46 Z", foil);
        }
        else
        {
            using var cap = Draw.Fill(red && !lot.Rose ? "#5c1429" : lot.Rose ? "#b3566a" : "#c8962e");
            c.DrawRoundRect(23, 4, 14, 22, 3, 3, cap);
        }
        using (var label = Draw.Fill("#fbf4e4")) c.DrawRoundRect(12, 100, 36, 48, 4, 4, label);
        using (var band = Draw.Fill(lot.Sparkling ? "#c8962e" : "#8e2440")) c.DrawRect(12, 104, 36, 4, band);
        c.Restore();
    }

    internal static (string Top, string Bottom) MedalColors(Medal medal) => medal switch
    {
        Medal.Gold => ("#f9df8f", "#c8962e"),
        Medal.Silber => ("#eef1f4", "#9aa3ad"),
        _ => ("#e2ae80", "#9a5f30"),
    };

    /// <summary>Medaille am Band, Mittelpunkt der Scheibe bei (cx, cy)</summary>
    internal static void PaintMedal(SKCanvas c, float cx, float cy, float r, Medal medal, bool ribbon = true)
    {
        var (top, bottom) = MedalColors(medal);
        if (ribbon)
        {
            using var left = Draw.Fill("#8e2440");
            using var right = Draw.Fill("#5c1429");
            Draw.Path(c, $"M{F(cx - r * 0.7f)} {F(cy - r * 2.1f)} L{F(cx - r * 0.1f)} {F(cy - r * 2.1f)} L{F(cx + r * 0.3f)} {F(cy - r * 0.6f)} L{F(cx - r * 0.3f)} {F(cy - r * 0.6f)} Z", left);
            Draw.Path(c, $"M{F(cx + r * 0.7f)} {F(cy - r * 2.1f)} L{F(cx + r * 0.1f)} {F(cy - r * 2.1f)} L{F(cx - r * 0.3f)} {F(cy - r * 0.6f)} L{F(cx + r * 0.3f)} {F(cy - r * 0.6f)} Z", right);
        }
        using (var disc = Draw.VerticalGradient(cy - r, cy + r, (0, top), (1, bottom))) c.DrawCircle(cx, cy, r, disc);
        using (var rim = Draw.Stroke(SKColor.Parse(bottom), r * 0.12f)) c.DrawCircle(cx, cy, r * 0.72f, rim);
        using var star = Draw.Fill(SKColor.Parse(bottom));
        using var path = new SKPath();
        for (var i = 0; i < 10; i++)
        {
            var a = -Math.PI / 2 + i * Math.PI / 5;
            var radius = (i % 2 == 0 ? 0.48f : 0.2f) * r;
            var point = new SKPoint(cx + (float)Math.Cos(a) * radius, cy + (float)Math.Sin(a) * radius);
            if (i == 0) path.MoveTo(point);
            else path.LineTo(point);
        }
        path.Close();
        c.DrawPath(path, star);
    }

    static string F(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    // ---- Hintergrund: Verkaufsraum mit Weinregal, Lampen und Theke ----

    static void PaintShop(SKCanvas c, float w, float h, Player player)
    {
        var fine = player.HasVinothek;
        using (var wall = Draw.VerticalGradient(0, h, (0, fine ? "#5a4030" : "#4c3526"), (0.55f, "#33231a"), (1, "#1c1310")))
            c.DrawRect(0, 0, w, h, wall);

        // Regalwand
        using (var back = Draw.Fill(fine ? "#2b1c15" : "#261912")) c.DrawRect(0, 84, w, 800, back);
        string[] glassColors = ["#3d4f25", "#5c1429", "#2c3a1c", "#6f8a3c", "#4a2330", "#7a5a2a"];
        using var board = Draw.VerticalGradient(0, 14, (0, fine ? "#9a6a3c" : "#7e5530"), (1, "#4a2e18"));
        using var post = Draw.Fill("#3a2416");
        using var labelPaint = Draw.Fill(SKColor.Parse("#f4ead7").WithAlpha(150));
        for (var row = 0; row < 5; row++)
        {
            var shelfY = 236f + row * 160f;
            for (var i = 0; i < 46; i++)
            {
                var seed = row * 53 + i * 17;
                if (seed % 11 == 0) continue; // Lücken im Regal
                var x = 22f + i * 39f;
                var color = SKColor.Parse(glassColors[seed % glassColors.Length]);
                using var glass = Draw.Fill(color);
                c.DrawRoundRect(x, shelfY - 86, 26, 86, 5, 5, glass);
                c.DrawRect(x + 9, shelfY - 118, 8, 36, glass);
                c.DrawRoundRect(x + 3, shelfY - 58, 20, 30, 2, 2, labelPaint);
            }
            c.Save();
            c.Translate(0, shelfY);
            c.DrawRect(0, 0, w, 14, board);
            c.Restore();
        }
        for (var x = 0f; x < w; x += 452) c.DrawRect(x - 7, 84, 14, 800, post);
        using (var crown = Draw.VerticalGradient(70, 96, (0, fine ? "#b07a45" : "#8a5d34"), (1, "#4a2e18"))) c.DrawRect(0, 70, w, 26, crown);
        if (fine)
        {
            using var gilt = Draw.Fill(Theme.SkGoldDark.WithAlpha(200));
            c.DrawRect(0, 94, w, 3, gilt);
        }

        // Das Regal tritt zurück, damit Karten und Schrift vorn stehen
        using (var veil = Draw.Fill(SKColor.Parse("#1c1418").WithAlpha(120))) c.DrawRect(0, 0, w, h, veil);

        // Lampen
        foreach (var x in new[] { 300f, 900f, 1500f })
        {
            using var glow = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateRadialGradient(new SKPoint(x, 40), 520,
                    [SKColor.Parse("#ffcf7a").WithAlpha(105), SKColor.Parse("#ffcf7a").WithAlpha(34), SKColor.Parse("#ffcf7a").WithAlpha(0)], [0, 0.45f, 1], SKShaderTileMode.Clamp),
            };
            c.DrawCircle(x, 40, 520, glow);
        }

        // Theke
        using (var counter = Draw.VerticalGradient(896, h, (0, fine ? "#8a5a30" : "#74492a"), (1, "#3a2414"))) c.DrawRect(0, 896, w, h - 896, counter);
        using (var edge = Draw.Fill(SKColor.Parse("#e0b070").WithAlpha(150))) c.DrawRect(0, 896, w, 5, edge);
        using var grain = Draw.Stroke(SKColors.Black.WithAlpha(30), 2);
        for (var x = 60f; x < w; x += 226) c.DrawLine(x, 904, x, h, grain);
    }
}
