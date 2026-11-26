using System.Globalization;
using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

/// <summary>
/// Keller: Gewölbe mit vier Reitern – Tanks &amp; Fässer, Flaschenlager, Weinmarkt und Kellermeister.
/// Links die Dinge, rechts der Inspektor mit Werten und Aktionen.
/// </summary>
public static class CellarScene
{
    // Als Eigenschaft, damit die Sprache beim Lesen gilt
    static string[] TabLabels => [Lang.T("Tanks & Fässer", "Tanks & barrels"), Lang.T("Flaschenlager", "Bottle stock"), Lang.T("Weinmarkt", "Wine market"), Lang.T("Kellermeister", "Cellar master")];

    public static SkiaControl Build(GameSession session)
    {
        var player = session.Player;
        var tab = Math.Clamp(session.Nav.Tab, 0, TabLabels.Length - 1);
        var scene = Kit.Layer(0, 0, Shell.StageWidth, Shell.StageHeight);
        scene.With(Kit.Paint(0, 0, Shell.StageWidth, Shell.StageHeight, (c, w, h) => PaintVault(c, w, h, tab)));
        // Zähler: Posten, die jetzt zur amtlichen Prüfung können
        int[] badges = [0, session.Over ? 0 : player.Bottles.Count(l => session.Problem(new GameAction.Register(l.Id)) is null), 0, 0];
        scene.With(Kit.Tabs(28, 20, TabLabels, tab, session.Nav.SetTab, badges));
        switch (tab)
        {
            case 1: CellarBottles.Build(scene, session); break;
            case 2: CellarMarket.Build(scene, session); break;
            case 3: CellarMasterTab.Build(scene, session); break;
            default: CellarTanks.Build(scene, session); break;
        }
        return scene;
    }

    /// <summary>Gewölbekeller: Mauerwerk, Bögen, Lampenschein und Boden; je Reiter mit Podest oder Regal.</summary>
    static void PaintVault(SKCanvas c, float w, float h, int tab)
    {
        var floor = tab == 0 ? 790 : 730;
        using (var wall = Draw.VerticalGradient(0, h, (0, "#2e221e"), (1, "#14100f"))) c.DrawRect(0, 0, w, h, wall);
        using (var joint = Draw.Stroke(SKColors.Black.WithAlpha(40), 2))
        {
            for (var row = 0; row * 46 < floor; row++)
            {
                var y = row * 46f;
                c.DrawLine(0, y, w, y, joint);
                for (var x = row % 2 == 0 ? 0f : 60f; x < w; x += 120) c.DrawLine(x, y, x, y + 46, joint);
            }
        }
        using (var arch = Draw.Stroke(SKColor.Parse("#4a3830").WithAlpha(204), 26))
        {
            for (var i = 0; i < 3; i++) Draw.Path(c, $"M{i * 600} {floor} V268 A300 300 0 0 1 {i * 600 + 600} 268 V{floor}", arch);
        }
        using (var lamp = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(new SKPoint(620, -60), 900,
                [SKColor.Parse("#ffcf7a").WithAlpha(120), SKColor.Parse("#ffcf7a").WithAlpha(0)], [0, 1], SKShaderTileMode.Clamp),
        })
            c.DrawRect(0, 0, w, floor, lamp);
        using (var ground = Draw.VerticalGradient(floor, h, (0, "#2a201b"), (1, "#110d0c"))) c.DrawRect(0, floor, w, h - floor, ground);
        using (var edge = Draw.Stroke(SKColor.Parse("#6a574b").WithAlpha(150), 3)) c.DrawLine(0, floor, w, floor, edge);

        if (tab == 0)
        {
            // Podest, auf dem die Tanks stehen
            using (var shade = Draw.VerticalGradient(524, 560, (0, "#99000000"), (1, "#00000000"))) c.DrawRect(20, 524, 1222, 36, shade);
            using (var stone = Draw.VerticalGradient(508, 526, (0, "#7a665a"), (1, "#43352e"))) c.DrawRoundRect(20, 508, 1222, 18, 4, 4, stone);
        }
        else if (tab == 1)
        {
            // Regal: zwei Fächer mit Brettern
            foreach (var top in CellarBottles.RowTops)
            {
                using (var niche = Draw.Fill(SKColor.Parse("#0d0908").WithAlpha(150))) c.DrawRoundRect(20, top - 6, 1222, 300, 14, 14, niche);
                using (var shade = Draw.VerticalGradient(top + 300, top + 330, (0, "#88000000"), (1, "#00000000"))) c.DrawRect(14, top + 300, 1234, 30, shade);
                using (var board = Draw.VerticalGradient(top + 282, top + 304, (0, "#c08a52"), (1, "#6e4524"))) c.DrawRoundRect(14, top + 282, 1234, 22, 5, 5, board);
                using var grain = Draw.Stroke(SKColor.Parse("#5a3518").WithAlpha(120), 1.5f);
                c.DrawLine(24, top + 290, 1238, top + 290, grain);
                c.DrawLine(24, top + 297, 1238, top + 297, grain);
            }
        }
    }
}

/// <summary>Kleine Bausteine, die mehrere Reiter des Kellers brauchen</summary>
internal static class CellarParts
{
    /// <summary>Innenmaße des Inspektors</summary>
    public const double Pad = 32;
    public const double Inner = 460;

    static readonly NumberFormatInfo German = new() { NumberGroupSeparator = ".", NumberDecimalSeparator = "," };

    public static SkiaShape Inspector() => Kit.Card(1260, 20, 524, 948, 22);

    /// <summary>Preis je Flasche mit Cent</summary>
    public static string Cents(double value) => Lang.En ? "€" + value.ToString("#,##0.00", CultureInfo.InvariantCulture) : value.ToString("#,##0.00", German) + " €";

    public static string Hl(double value) => Util.FormatNumber(value, 1) + " hl";

    public static string Months(int months) => Lang.En ? (months == 1 ? "1 month" : $"{months} months") : (months == 1 ? "1 Monat" : $"{months} Monate");

    /// <summary>Bezeichnung aus <see cref="Rules.WineLabel"/>, geteilt in Namen und Jahrgang</summary>
    public static (string Name, string Vintage) NameOf(WineInfo wine)
    {
        var label = Rules.WineLabel(wine);
        var cut = label.LastIndexOf(" (", StringComparison.Ordinal);
        return (cut < 0 ? label : label[..cut], Lang.En ? $"Vintage, year {wine.Vintage}" : $"Jahrgang Jahr {wine.Vintage}");
    }

    public static string GrapeName(WineInfo wine) => Grapes.All[wine.Grape].Name.Replace(" (PIWI)", "");

    /// <summary>Farbverlauf der Weinart: rot, weiß oder rosé</summary>
    public static (string Top, string Bottom) WineColors(WineInfo wine) =>
        wine.Rose ? ("#f6b9ae", "#d27482")
        : Grapes.All[wine.Grape].Color == GrapeColor.Rot ? ("#a0304e", "#5c1429")
        : ("#f1dc7a", "#c9a93a");

    /// <summary>Titel und Unterzeile oben im Inspektor</summary>
    public static void Head(SkiaShape card, string title, string sub, Color? subColor = null)
    {
        card.With(Kit.Text(title, Pad, 26, Inner, 46, title.Length > 27 ? 24 : 31, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(sub, Pad, 72, Inner, 28, 18, subColor ?? Theme.InkSoft));
    }

    public static SkiaControl Divider(double y) => Kit.Box(Pad, y, Inner, 2, Theme.PaperLine, 1);

    /// <summary>
    /// Aktion mit Vorschau und Sperrgrund. Ein langer Sperrgrund bekommt hier sicher zwei Zeilen
    /// (die Zeilenhöhe von <see cref="Kit.Button"/> reicht dafür im Inspektor nicht immer).
    /// </summary>
    public static SkiaShape Action(string title, double x, double y, double w, double h, Action onTap, ButtonKind kind,
        string? sub, string? problem, double fontSize = 21)
    {
        if (problem is null || Kit.TextWidth(problem, 16) <= (w - 44) * 0.97) return Kit.Button(title, x, y, w, h, onTap, kind, sub, problem, fontSize);
        var primary = kind == ButtonKind.Primary;
        var box = primary
            ? Kit.Box(x, y, w, h, radius: 16, gradient: Kit.Vertical(Theme.Wine, Theme.WineDark))
            : Kit.Box(x, y, w, h, Theme.Clear, 16, Theme.Wine, 3);
        var small = h < 80;
        box.With(
            Kit.Text(title, 22, small ? 4 : 7, w - 44, fontSize * 1.5, small ? fontSize - 2 : fontSize, primary ? Theme.C("#fff6e6") : Theme.Ink, bold: true),
            Kit.Text(problem, 22, (small ? 4 : 7) + fontSize * 1.3, w - 44, 42, 14.5, primary ? Theme.C("#f0cfd6") : Theme.InkSoft, maxLines: 2));
        box.Opacity = 0.5;
        return box;
    }

    /// <summary>Güte 0–100 als Ring mit Zahl</summary>
    public static SkiaControl QualityRing(double x, double y, double quality) =>
        Kit.Layer(x, y, 130, 160).With(
            Kit.Paint(0, 0, 130, 130, (c, w, h) => Draw.Ring(c, 65, 65, 52, 13, quality / 100, SKColor.Parse("#d9c7a3"), Theme.SkWine)),
            Kit.Text(Util.FormatNumber(quality), 0, 42, 130, 46, 35, Theme.Ink, bold: true, serif: true, align: Align.Center),
            Kit.Text(Lang.T("Güte", "Quality"), 0, 132, 130, 26, 17, Theme.InkSoft, align: Align.Center));

    public static string Arrow(TrendDirectionKind direction) =>
        direction == TrendDirectionKind.Up ? "↗" : direction == TrendDirectionKind.Down ? "↘" : "→";

    /// <summary>Markttrend als „118 % ↗“ mit Farbe</summary>
    public static (string Text, Color Color) TrendValue(Trend trend)
    {
        var percent = Js.Round(trend.Value * 100);
        return ($"{percent} % {Arrow(Trends.TrendDirection(trend))}", percent > 100 ? Theme.Good : percent < 100 ? Theme.Alert : Theme.Ink);
    }

    public static Trend TrendOf(GameState state, WineInfo wine) =>
        state.Trends[wine is BottleLot { Sparkling: true } ? TrendKey.Sekt : wine.Rose ? TrendKey.Rose : (TrendKey)(int)wine.Grape];

    /// <summary>Auswahl aus wenigen festen Werten als Reihe von Knöpfen</summary>
    public static SkiaControl Choice(double x, double y, double w, string[] labels, int active, Action<int> pick)
    {
        var layer = Kit.Layer(x, y, w, 46);
        var gap = 8.0;
        var width = (w - gap * (labels.Length - 1)) / labels.Length;
        for (var i = 0; i < labels.Length; i++)
        {
            var index = i;
            var on = i == active;
            layer.With(Kit.Box(i * (width + gap), 0, width, 46, on ? Theme.Wine : Theme.PaperLine, 23)
                .With(Kit.Text(labels[i], 0, 10, width, 28, 19, on ? Theme.Paper : Theme.Ink, bold: true, align: Align.Center))
                .Tap(() => pick(index)));
        }
        return layer;
    }

    /// <summary>Anzeige „Tank 214 / 300 hl“ mit Balken, für dunklen Grund</summary>
    public static SkiaControl Gauge(double x, double y, double w, string label, string value, double share, Color color)
    {
        var layer = Kit.Layer(x, y, w, 52);
        layer.With(Kit.Text(label, 0, 0, w * 0.4, 26, 17, Theme.CreamSoft));
        layer.With(Kit.Text(value, w * 0.3, 0, w * 0.7, 26, 17, Theme.Cream, bold: true, align: Align.End));
        layer.With(Kit.Box(0, 32, w, 10, Theme.GlassLine, 5));
        if (share > 0) layer.With(Kit.Box(0, 32, Math.Max(10, w * Math.Clamp(share, 0, 1)), 10, color, 5));
        return layer;
    }

    public static void PaintMedal(SKCanvas c, float cx, float cy, float r, Medal medal)
    {
        var (light, dark) = medal == Medal.Gold ? ("#ffe9a0", "#c8962e") : medal == Medal.Silber ? ("#f2f4f6", "#8d98a2") : ("#e9b98a", "#9a5a2c");
        using (var ribbon = Draw.Fill("#8e2440"))
            Draw.Path(c, $"M{F(cx - r * 0.7f)} {F(cy)} l{F(-r * 0.25f)} {F(r * 1.7f)} l{F(r * 0.6f)} {F(-r * 0.45f)} l{F(r * 0.35f)} {F(r * 0.45f)} l{F(r * 0.35f)} {F(-r * 0.45f)} l{F(r * 0.6f)} {F(r * 0.45f)} l{F(-r * 0.25f)} {F(-r * 1.7f)} Z", ribbon);
        using (var disc = Draw.VerticalGradient(cy - r, cy + r, (0, light), (1, dark))) c.DrawCircle(cx, cy, r, disc);
        using (var rim = Draw.Stroke(SKColor.Parse(dark), r * 0.16f)) c.DrawCircle(cx, cy, r * 0.92f, rim);
        using (var shine = Draw.Stroke(SKColors.White.WithAlpha(170), r * 0.14f)) c.DrawCircle(cx, cy, r * 0.55f, shine);
    }

    /// <summary>Zahl für SVG-Pfade – immer mit Punkt</summary>
    public static string F(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Fünf Sterne, davon <paramref name="filled"/> gefüllt; Breite 5 × 30, Höhe 30</summary>
    public static void PaintStars(SKCanvas c, int filled)
    {
        using var on = Draw.VerticalGradient(0, 30, (0, "#f6d477"), (1, "#c8962e"));
        using var off = Draw.Fill(SKColor.Parse("#6a4a2a").WithAlpha(60));
        for (var i = 0; i < 5; i++)
        {
            c.Save();
            c.Translate(i * 30, 0);
            c.Scale(0.9f);
            Draw.Path(c, "M15 1 l4.5 9.5 10.5 1.3 -7.7 7.2 2 10.3 -9.3 -5.1 -9.3 5.1 2 -10.3 -7.7 -7.2 10.5 -1.3 Z", i < filled ? on : off);
            c.Restore();
        }
    }

    /// <summary>Warum Sekt oder Barrique gesperrt sind – null, wenn der Kellermeister beides kann</summary>
    public static string? MasterHint(Player player)
    {
        if (player.CellarMaster is not { } master) return Lang.T("Ohne Kellermeister gibt es weder Sekt noch Barriqueweine.", "Without a cellar master there is neither sparkling wine nor barrique wine.");
        var missing = new List<string>();
        if (!CellarMasters.HasSkill(player, CellarSkill.Sekt)) missing.Add(Lang.T("Sekt", "sparkling wine"));
        if (!CellarMasters.HasSkill(player, CellarSkill.Barrique)) missing.Add(Lang.T("den Barrique-Ausbau", "barrique aging"));
        return missing.Count > 0 ? (Lang.En ? $"{master.Name} cannot do {string.Join(" or ", missing)}." : $"{master.Name} beherrscht {string.Join(" und ", missing)} nicht.") : null;
    }

    /// <summary>Wo der Wein weggeht: „Ab Hof &amp; online gehen etwa … Flaschen im Monat weg.“</summary>
    public static string DemandText(Player player) =>
        Lang.En ? $"About {Util.FormatNumber(Live.Demand(player))} bottles a month sell at the cellar door{(player.HasOnlineShop ? " & online" : "")}{(player.HasVinothek ? " & wine room" : "")}."
            : $"Ab Hof{(player.HasOnlineShop ? " & online" : "")}{(player.HasVinothek ? " & Vinothek" : "")} gehen etwa {Util.FormatNumber(Live.Demand(player))} Flaschen im Monat weg.";
}
