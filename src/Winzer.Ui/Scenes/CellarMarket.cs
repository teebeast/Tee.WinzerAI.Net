using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

/// <summary>Reiter „Weinmarkt“: Tafel mit dem Preisfaktor je Sorte, Sekt und Rosé; rechts Erklärung, eigene Sorten und Fassweinmarkt.</summary>
internal static class CellarMarket
{
    const double RowHeight = 54;
    const double BarLeft = 286;
    const double BarWidth = 600;

    public static void Build(SkiaLayout scene, GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var own = OwnKeys(player);
        var rows = Trends.TrendKeys
            .Select(key => (Key: key, Trend: state.Trends[key]))
            .OrderByDescending(r => r.Trend.Value)
            .ToList();
        var (min, max) = Economy.TrendRange;
        double Share(double value) => Math.Clamp((value - min) / (max - min), 0, 1);

        var board = Kit.Glass(28, 90, 1202, 872, 20);
        board.With(Kit.Text(Lang.T("Markttrends", "Market trends"), 28, 16, 300, 38, 28, Theme.Gold, bold: true, serif: true));
        board.With(Kit.Text(Lang.T("Preisfaktor je Sorte, Sekt und Rosé – gold: deine Sorten", "Price factor per variety, sparkling wine and rosé – gold: your varieties"), 232, 25, 700, 26, 17, Theme.CreamSoft));
        // Skala
        for (var percent = (int)Js.Round(min * 100); percent <= (int)Js.Round(max * 100); percent += 10)
        {
            board.With(Kit.Text($"{percent} %", BarLeft + BarWidth * Share(percent / 100.0) - 40, 66, 80, 22, 15,
                percent == 100 ? Theme.Cream : Theme.CreamSoft, bold: percent == 100, align: Align.Center));
        }
        board.With(Kit.Text(Lang.T("Faktor", "Factor"), 906, 66, 90, 22, 15, Theme.CreamSoft, align: Align.End));
        board.With(Kit.Text(Lang.T("Richtung", "Direction"), 1030, 66, 150, 22, 15, Theme.CreamSoft));

        const double top = 96;
        board.With(Kit.Paint(BarLeft, top, BarWidth, rows.Count * RowHeight, (c, w, h) =>
        {
            using (var grid = Draw.Stroke(Theme.SkCream.WithAlpha(28), 1.5f))
            {
                for (var percent = min; percent <= max + 0.001; percent += 0.1)
                {
                    var x = (float)(w * Share(percent));
                    c.DrawLine(x, 0, x, h, grid);
                }
            }
            var mid = (float)(w * Share(1));
            for (var i = 0; i < rows.Count; i++)
            {
                var value = rows[i].Trend.Value;
                var mine = own.Contains(rows[i].Key);
                var y = (float)(i * RowHeight + 15);
                using (var track = Draw.Fill(Theme.SkCream.WithAlpha(20))) c.DrawRoundRect(0, y, w, 24, 12, 12, track);
                var x = (float)(w * Share(value));
                var color = SKColor.Parse(value >= 1 ? "#7dbb6a" : "#e0704c").WithAlpha((byte)(mine ? 255 : 150));
                using var bar = Draw.Fill(color);
                if (Math.Abs(x - mid) < 3) c.DrawRoundRect(mid - 3, y, 6, 24, 3, 3, bar);
                else c.DrawRoundRect(Math.Min(x, mid), y, Math.Abs(x - mid), 24, 6, 6, bar);
            }
            using var center = Draw.Stroke(Theme.SkCream.WithAlpha(170), 2.5f);
            c.DrawLine(mid, 0, mid, h, center);
        }));

        for (var i = 0; i < rows.Count; i++)
        {
            var (key, trend) = rows[i];
            var mine = own.Contains(key);
            var y = top + i * RowHeight;
            if (mine) board.With(Kit.Box(14, y + 3, 1174, RowHeight - 6, Theme.C("#16f6d477"), 12));
            board.With(Kit.Text(Trends.TrendName(key), 28, y + 12, 250, 30, 21, mine ? Theme.Gold : Theme.Cream, bold: mine));
            var percent = Js.Round(trend.Value * 100);
            board.With(Kit.Text($"{percent} %", 906, y + 12, 90, 30, 21, mine ? Theme.Gold : Theme.Cream, bold: true, align: Align.End));
            var direction = Trends.TrendDirection(trend);
            var (word, hex) = direction == TrendDirectionKind.Up ? (Lang.T("steigend", "rising"), "#7dbb6a") : direction == TrendDirectionKind.Down ? (Lang.T("fallend", "falling"), "#e0884c") : (Lang.T("stabil", "stable"), "#d9c7a3");
            board.With(Kit.Paint(1030, y + 13, 28, 28, (c, w, h) => PaintArrow(c, direction, SKColor.Parse(hex))));
            board.With(Kit.Text(word, 1068, y + 13, 120, 28, 19, Theme.C(hex), bold: direction != TrendDirectionKind.Flat));
        }
        board.With(Kit.Text(Lang.T("Über 100 % zahlt der Markt einen Aufschlag, darunter gibt es Abschläge. Die Richtung zeigt, wohin der Trend in den nächsten Monaten wandert.", "Above 100 % the market pays a premium, below it there are discounts. The direction shows where the trend is heading in the coming months."),
            28, top + rows.Count * RowHeight + 22, 1146, 26, 17, Theme.CreamSoft));
        scene.With(board);

        scene.With(Panel(session, rows, own));
    }

    /// <summary>Sorten, die im Weinberg stehen, im Tank liegen oder in Flaschen lagern</summary>
    static HashSet<TrendKey> OwnKeys(Player player)
    {
        var own = new HashSet<TrendKey>();
        foreach (var plot in player.Plots) if (plot.Grape is { } grape) own.Add((TrendKey)(int)grape);
        foreach (var batch in player.Cellar)
        {
            own.Add((TrendKey)(int)batch.Grape);
            if (batch.Rose) own.Add(TrendKey.Rose);
        }
        foreach (var lot in player.Bottles)
        {
            own.Add(lot.Sparkling ? TrendKey.Sekt : (TrendKey)(int)lot.Grape);
            if (lot.Rose && !lot.Sparkling) own.Add(TrendKey.Rose);
        }
        return own;
    }

    static void PaintArrow(SKCanvas c, TrendDirectionKind direction, SKColor color)
    {
        using var line = Draw.Stroke(color, 4);
        c.Save();
        c.RotateDegrees(direction == TrendDirectionKind.Up ? -40 : direction == TrendDirectionKind.Down ? 40 : 0, 14, 14);
        Draw.Path(c, "M3 14 H24 M16 6 L24 14 L16 22", line);
        c.Restore();
    }

    static SkiaControl Panel(GameSession session, List<(TrendKey Key, Trend Trend)> rows, HashSet<TrendKey> own)
    {
        var state = session.State;
        var player = session.Player;
        var card = CellarParts.Inspector();
        CellarParts.Head(card, Lang.T("Weinmarkt", "Wine market"), Lang.En ? $"{Game.MonthNames[state.Month - 1]} · year {state.Year}" : $"{Game.MonthNames[state.Month - 1]} · Jahr {state.Year}");
        card.With(Kit.Text(Lang.T("Der Preisfaktor wirkt auf Ab-Hof-, Großhandels- und Fassweinpreise. Trends wandern langsam; neue Reben tragen erst im dritten Jahr – wer pflanzt, wettet auf den Markt von übermorgen.", "The price factor affects cellar-door, wholesale and bulk wine prices. Trends move slowly; new vines bear fruit only in the third year – whoever plants is betting on the market of the day after tomorrow."),
            CellarParts.Pad, 108, CellarParts.Inner, 106, 18, Theme.Ink, maxLines: 4));

        // Fassweinmarkt
        var crisis = state.BulkCrisisMonths > 0;
        var box = Kit.Box(CellarParts.Pad, 226, CellarParts.Inner, 96, crisis ? Theme.C("#22b3261e") : Theme.C("#224d7a2a"), 14, crisis ? Theme.Alert : Theme.Good, 2.5);
        box.With(Kit.Text(crisis ? Lang.T("Krise der Großkellereien", "Large-winery crisis") : Lang.T("Fassweinmarkt stabil", "Bulk wine market stable"), 20, 12, 420, 30, 21, crisis ? Theme.Alert : Theme.Good, bold: true));
        box.With(Kit.Text(crisis
            ? (Lang.En ? $"Bulk wine fetches only half price for {CellarParts.Months(state.BulkCrisisMonths)}." : $"Fasswein bringt noch {CellarParts.Months(state.BulkCrisisMonths)} nur den halben Preis.")
            : Lang.T("Die Großkellereien zahlen für Fasswein den üblichen Preis.", "The large wineries pay the usual price for bulk wine."), 20, 42, 420, 50, 16, Theme.Ink, maxLines: 2));
        card.With(box);

        // Eigene Sorten
        card.With(Kit.Text(Lang.T("Deine Sorten", "Your varieties"), CellarParts.Pad, 340, 300, 30, 21, Theme.Wine, bold: true, serif: true));
        var mine = rows.Where(r => own.Contains(r.Key)).ToList();
        var y = 376.0;
        if (mine.Count == 0)
        {
            card.With(Kit.Text(Lang.T("Noch keine eigenen Sorten – im Weinberg pflanzen.", "No varieties of your own yet – plant in the vineyard."), CellarParts.Pad, y, CellarParts.Inner, 28, 18, Theme.InkSoft));
        }
        var pitch = mine.Count > 8 ? 30 : 34;
        foreach (var (key, trend) in mine.Take(10))
        {
            var value = CellarParts.TrendValue(trend);
            card.With(Kit.Row(Trends.TrendName(key), value.Text, CellarParts.Pad, y, CellarParts.Inner, value.Color, mine.Count > 8 ? 18 : 19));
            y += pitch;
        }

        card.With(CellarParts.Divider(694));
        var best = rows[0];
        var worst = rows[^1];
        card.With(Kit.Row(Lang.T("Am gefragtesten", "Most in demand"), $"{Trends.TrendName(best.Key)} · {Js.Round(best.Trend.Value * 100)} %", CellarParts.Pad, 708, CellarParts.Inner, Theme.Good, 19));
        card.With(Kit.Row(Lang.T("Am schwächsten", "Weakest"), $"{Trends.TrendName(worst.Key)} · {Js.Round(worst.Trend.Value * 100)} %", CellarParts.Pad, 744, CellarParts.Inner, Theme.Alert, 19));
        var saturation = Market.WholesaleSaturation(player);
        card.With(Kit.Row(Lang.T("Großhandel zahlt", "Wholesale pays"), Lang.En ? $"{Js.Round(saturation * 100)} % of the usual" : $"{Js.Round(saturation * 100)} % vom Üblichen", CellarParts.Pad, 780, CellarParts.Inner, saturation < 1 ? Theme.Alert : Theme.Ink, 19));
        var summer = Economy.RoseSummerMonths.Contains(state.Month);
        card.With(Kit.Row(Lang.T("Rosé ab Hof", "Rosé cellar door"), summer ? (Lang.En ? $"Summer: +{Js.Round(Economy.RoseSummerBonus * 100)} %" : $"Sommer: +{Js.Round(Economy.RoseSummerBonus * 100)} %") : Lang.T("ohne Sommerbonus", "no summer bonus"), CellarParts.Pad, 816, CellarParts.Inner, summer ? Theme.Good : Theme.Ink, 19));
        card.With(Kit.Button(Lang.T("Neue Reben pflanzen ›", "Plant new vines ›"), CellarParts.Pad, 864, CellarParts.Inner, 56, () => session.Nav.Go(Scene.Weinberg, 0), ButtonKind.Ghost, fontSize: 19, align: Align.Center));
        return card;
    }
}
