using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

// Reiter „Marketing“: drei Maßnahmen, die Straußwirtschaft und die Bekanntheit mit ihren Wirkungen
public static partial class MarketScene
{
    const double PromoWidth = 593, PromoHeight = 427;
    static readonly string[] MonthLetters = ["J", "F", "M", "A", "M", "J", "J", "A", "S", "O", "N", "D"];

    /// <summary>Tatsächlicher Zuwachs an Bekanntheit – Zuwächse schrumpfen mit der Sättigung</summary>
    static double ReputationGain(Player player, double nominal) => Rules.ChangeReputation(new Player { Reputation = player.Reputation }, nominal);

    static string GainText(double gain, double nominal) =>
        Lang.En ? $"{Signed(gain)} reputation" + (Math.Abs(gain - nominal) >= 0.05 ? $" (instead of +{Util.FormatNumber(nominal)})" : "")
            : $"{Signed(gain)} Bekanntheit" + (Math.Abs(gain - nominal) >= 0.05 ? $" (statt +{Util.FormatNumber(nominal)})" : "");

    static void BuildMarketing(GameSession session, SkiaLayout scene)
    {
        var state = session.State;
        var player = session.Player;

        (MarketingKind Kind, string Verb, string Text)[] measures =
        [
            (MarketingKind.Social, Lang.T("Kampagne buchen", "Book campaign"), Lang.T("Beiträge und Anzeigen im Netz halten das Weingut im Gespräch – jeden Monat buchbar.", "Posts and ads online keep the estate in the conversation – bookable every month.")),
            (MarketingKind.Hoffest, Lang.T("Hoffest ausrichten", "Host a farm festival"), Lang.T("Weinprobe und Fest auf dem eigenen Hof – in der warmen Jahreszeit von Mai bis September.", "Wine tasting and festival on your own estate – in the warm season from May to September.")),
            (MarketingKind.Messe, Lang.T("Messestand buchen", "Book a trade fair stand"), Lang.T("Ein Stand auf der großen Weinmesse bringt Händler und Presse – nur im März.", "A stand at the big wine fair brings dealers and press – in March only.")),
        ];
        for (var i = 0; i < measures.Length; i++)
        {
            var (kind, verb, text) = measures[i];
            var info = Economy.Marketing[kind];
            var action = new GameAction.Marketing(kind);
            var booked = player.MarketingThisMonth.Contains(kind);
            var allowed = info.Months.Contains(state.Month);
            var gain = ReputationGain(player, info.Reputation);
            var status = booked ? (Lang.T("Für diesen Monat gebucht", "Booked for this month"), Theme.Good)
                : allowed ? (Lang.T("Diesen Monat möglich", "Possible this month"), Theme.Good)
                : (Lang.En ? $"Possible again in {Game.MonthNames[NextAllowed(info.Months, state.Month) - 1]}" : $"Wieder möglich im {Game.MonthNames[NextAllowed(info.Months, state.Month) - 1]}", Theme.InkSoft);
            scene.With(PromoCard(28 + i % 2 * (PromoWidth + 16), 90 + i / 2 * (PromoHeight + 16), kind, info.Name, text,
                Util.FormatEuro(info.Cost), GainText(gain, info.Reputation),
                info.Months, state.Month, status,
                Kit.Button(verb, 24, 321, PromoWidth - 48, 84, () => session.Run(action), ButtonKind.Primary,
                    Lang.En ? $"{Util.FormatEuro(info.Cost)} · reputation {Signed(gain)}" : $"{Util.FormatEuro(info.Cost)} · Bekanntheit {Signed(gain)}", session.Problem(action))));
        }

        // Straußwirtschaft
        var open = player.Effects.FirstOrDefault(e => e.Label == Products.StraussEffectLabel);
        var strauss = new GameAction.OpenStrausswirtschaft();
        var straussGain = ReputationGain(player, Economy.StraussReputation);
        var straussStatus = open is not null ? (Lang.En ? $"Open – {open.MonthsLeft} {(open.MonthsLeft == 1 ? "month" : "months")} left" : $"Geöffnet – noch {open.MonthsLeft} {(open.MonthsLeft == 1 ? "Monat" : "Monate")}", Theme.Good)
            : player.StraussYear == state.Year ? (Lang.T("Hatte dieses Jahr schon geöffnet", "Already opened this year"), Theme.InkSoft)
            : Economy.StraussStartMonths.Contains(state.Month) ? (Lang.T("Geschlossen – kann jetzt öffnen", "Closed – can open now"), Theme.Good)
            : (Lang.En ? $"Closed – opens again from {Game.MonthNames[NextAllowed(Economy.StraussStartMonths, state.Month) - 1]}" : $"Geschlossen – öffnet wieder ab {Game.MonthNames[NextAllowed(Economy.StraussStartMonths, state.Month) - 1]}", Theme.InkSoft);
        scene.With(PromoCard(28 + PromoWidth + 16, 90 + PromoHeight + 16, null, Lang.T("Straußwirtschaft", "Wine tavern"),
            Lang.En ? $"Four months of serving wine on the estate: +{Js.Round((Economy.StraussDemandFactor - 1) * 100)} % cellar-door customers, once a year (March–October)." : $"Vier Monate Ausschank auf dem Hof: +{Js.Round((Economy.StraussDemandFactor - 1) * 100)} % Ab-Hof-Kundschaft, einmal im Jahr (März–Oktober).",
            Util.FormatEuro(Economy.StraussCost),
            Lang.En ? $"+{Js.Round((Economy.StraussDemandFactor - 1) * 100)} % demand for {Economy.StraussMonths} months · {Signed(straussGain)} reputation" : $"+{Js.Round((Economy.StraussDemandFactor - 1) * 100)} % Nachfrage für {Economy.StraussMonths} Monate · {Signed(straussGain)} Bekanntheit",
            Economy.StraussStartMonths, state.Month, straussStatus,
            Kit.Button(Lang.T("Straußwirtschaft öffnen", "Open wine tavern"), 24, 321, PromoWidth - 48, 84, () => session.Run(strauss), ButtonKind.Primary,
                Lang.En ? $"{Util.FormatEuro(Economy.StraussCost)} · {Economy.StraussMonths} months of more guests" : $"{Util.FormatEuro(Economy.StraussCost)} · {Economy.StraussMonths} Monate mehr Gäste", session.Problem(strauss))));

        scene.With(ReputationInspector(session));
    }

    static int NextAllowed(int[] months, int current)
    {
        for (var step = 1; step <= 12; step++)
        {
            var month = (current - 1 + step) % 12 + 1;
            if (months.Contains(month)) return month;
        }
        return current;
    }

    static SkiaControl PromoCard(double x, double y, MarketingKind? kind, string name, string text, string cost, string effect,
        int[] months, int current, (string Text, Color Color) status, SkiaControl button)
    {
        var card = Kit.Card(x, y, PromoWidth, PromoHeight, 18);
        card.With(Kit.Paint(24, 22, 92, 92, (c, w, h) => PaintPromoIcon(c, kind)));
        card.With(Kit.Text(name, 136, 20, PromoWidth - 160, 42, 28, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(text, 136, 62, PromoWidth - 160, 52, 17, Theme.InkSoft, maxLines: 2));
        card.With(Kit.Row(Lang.T("Kosten", "Cost"), cost, 24, 130, PromoWidth - 48, size: 19));
        card.With(Kit.Layer(24, 164, PromoWidth - 48, 32).With(
            Kit.Text(Lang.T("Wirkung", "Effect"), 0, 0, 120, 28, 19, Theme.InkSoft),
            Kit.Text(effect, 100, 0, PromoWidth - 148, 28, 19, Theme.Wine, bold: true, align: Align.End)));

        // Monatsleiste: wann die Maßnahme möglich ist
        card.With(Kit.Text(Lang.T("Möglich in", "Possible in"), 24, 212, 110, 26, 17, Theme.InkSoft));
        for (var m = 1; m <= 12; m++)
        {
            var on = months.Contains(m);
            var now = m == current;
            var cell = Kit.Box(136 + (m - 1) * 36, 208, 32, 32, on ? Theme.Wine : Theme.PaperLine, 8, now ? Theme.GoldDark : null, 4);
            cell.With(Kit.Text(MonthLetters[m - 1], 0, 5, 32, 22, 15, on ? Theme.Paper : Theme.InkSoft, bold: true, align: Align.Center));
            card.With(cell);
        }
        card.With(Kit.Text(status.Text, 24, 262, PromoWidth - 48, 28, 18, status.Color, bold: true));
        card.With(button);
        return card;
    }

    static void PaintPromoIcon(SKCanvas c, MarketingKind? kind)
    {
        using (var back = Draw.VerticalGradient(0, 92, (0, "#8e2440"), (1, "#5c1429"))) c.DrawRoundRect(0, 0, 92, 92, 22, 22, back);
        using var cream = Draw.Fill("#fbf4e4");
        using var gold = Draw.Fill(Theme.SkGold);
        using var line = Draw.Stroke(SKColor.Parse("#fbf4e4"), 4);
        switch (kind)
        {
            case MarketingKind.Social:
                // Telefon mit Herz
                c.DrawRoundRect(28, 14, 36, 64, 7, 7, line);
                c.DrawLine(41, 70, 51, 70, line);
                Draw.Path(c, "M46 52 C34 42 36 32 42 32 C45 32 46 35 46 36 C46 35 47 32 50 32 C56 32 58 42 46 52 Z", gold);
                break;
            case MarketingKind.Hoffest:
                // Wimpelkette und zwei Gläser
                Draw.Path(c, "M12 20 Q46 36 80 20", line);
                Draw.Path(c, "M22 25 l7 12 5 -9 Z M40 30 l6 12 6 -12 Z M58 28 l5 9 7 -12 Z", gold);
                Draw.Path(c, "M24 46 h18 a9 12 0 0 1 -18 0 Z M54 46 h18 a9 12 0 0 1 -18 0 Z", cream);
                Draw.Path(c, "M33 58 V74 M26 76 H40 M63 58 V74 M56 76 H70", line);
                break;
            case MarketingKind.Messe:
                // Messestand mit Banner
                Draw.Path(c, "M16 30 H76 V44 H16 Z", gold);
                Draw.Path(c, "M20 44 V78 M72 44 V78 M14 78 H78", line);
                c.DrawRoundRect(30, 56, 32, 22, 3, 3, cream);
                Draw.Path(c, "M46 14 l4 8 9 1 -7 6 2 9 -8 -5 -8 5 2 -9 -7 -6 9 -1 Z", cream);
                break;
            default:
                // Strauß über der Tür: Besen aus Zweigen mit Schleife
                Draw.Path(c, "M46 80 V44", line);
                using (var leaf = Draw.Fill("#7dbb6a"))
                {
                    c.DrawOval(46, 28, 20, 18, leaf);
                    c.DrawOval(30, 36, 12, 10, leaf);
                    c.DrawOval(62, 36, 12, 10, leaf);
                }
                using (var dark = Draw.Fill("#4d7a2a"))
                {
                    c.DrawCircle(40, 26, 5, dark);
                    c.DrawCircle(54, 32, 5, dark);
                    c.DrawCircle(46, 18, 4, dark);
                }
                Draw.Path(c, "M46 50 l-14 10 4 -12 Z M46 50 l14 10 -4 -12 Z", gold);
                break;
        }
    }

    static SkiaControl ReputationInspector(GameSession session)
    {
        var player = session.Player;
        var reputation = player.Reputation;
        var card = Inspector();
        card.With(Kit.Text(Lang.T("Bekanntheit", "Reputation"), 32, 28, 460, 46, 32, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(Lang.T("Wie viele Kunden das Weingut kennen", "How many customers know the estate"), 32, 74, 460, 28, 18, Theme.InkSoft));

        card.With(Kit.Paint(32, 118, 150, 150, (c, w, h) =>
            Draw.Ring(c, 75, 75, 60, 16, reputation / 100, SKColor.Parse("#d9c7a3"), Theme.SkWine)));
        card.With(Kit.Text(Util.FormatNumber(Js.Round(reputation)), 32, 164, 150, 52, 40, Theme.Ink, bold: true, serif: true, align: Align.Center));
        card.With(Kit.Text(Lang.T("von 100", "of 100"), 32, 208, 150, 22, 15, Theme.InkSoft, align: Align.Center));
        card.With(Kit.Row(Lang.T("Ruf", "Standing"), Shell.ReputationWord(reputation), 206, 126, 286, size: 19));
        card.With(Kit.Row(Lang.T("Ab Hof", "Cellar door"), $"{Util.FormatNumber(Live.Demand(player))} {Lang.T("Fl./Monat", "btl./month")}", 206, 162, 286, size: 19));
        card.With(Kit.Row(Lang.T("Verfall", "Decay"), Lang.En ? $"−{Util.FormatNumber(reputation * Economy.ReputationDecayRate, 1)} per month" : $"−{Util.FormatNumber(reputation * Economy.ReputationDecayRate, 1)} im Monat", 206, 198, 286, Theme.Alert, 19));
        var saturation = Math.Max(0, 1 - reputation / Economy.ReputationSaturation);
        card.With(Kit.Row(Lang.T("Werbewirkung", "Ad effect"), $"{Js.Round(saturation * 100)} %", 206, 234, 286, size: 19));

        card.With(Rule(290));
        card.With(Kit.Text(
            Lang.En ? $"Well-known estates sell more bottles at the cellar door and fetch higher prices; from {Util.FormatNumber(Economy.ExportMinReputation)} importers get in touch." : $"Bekannte Güter verkaufen mehr Flaschen ab Hof und erzielen höhere Preise; ab {Util.FormatNumber(Economy.ExportMinReputation)} melden sich Importeure.",
            32, 304, 460, 52, 17, Theme.Ink, maxLines: 2));
        card.With(Kit.Text(
            Lang.En ? $"Decay: every month {Js.Round(Economy.ReputationDecayRate * 100)} % of reputation is lost – whoever does nothing is forgotten." : $"Verfall: Jeden Monat gehen {Js.Round(Economy.ReputationDecayRate * 100)} % der Bekanntheit verloren – wer nichts tut, gerät in Vergessenheit.",
            32, 362, 460, 52, 17, Theme.InkSoft, maxLines: 2));
        card.With(Kit.Text(
            Lang.T("Sättigung: Je bekannter das Gut schon ist, desto weniger bringt jede weitere Maßnahme, Medaille oder Lieferung.", "Saturation: the better known the estate already is, the less each further measure, medal or delivery brings."),
            32, 420, 460, 52, 17, Theme.InkSoft, maxLines: 2));

        card.With(Rule(488));
        card.With(Kit.Text(Lang.T("Laufende Effekte", "Active effects"), 32, 502, 300, 30, 20, Theme.Ink, bold: true));
        var effects = player.Effects.ToList();
        const int perPage = 5;
        var pages = Math.Max(1, (int)Math.Ceiling(effects.Count / (double)perPage));
        var page = session.Nav.Page("markt.effects", pages);
        if (pages > 1) card.With(Kit.Pager(252, 498, 240, page, pages, p => session.Nav.SetPage("markt.effects", p), dark: false));
        var y = 544.0;
        if (effects.Count == 0)
        {
            card.With(Kit.Text(Lang.T("Zurzeit wirkt nichts Besonderes auf Nachfrage oder Preise. Ereignisse, Hoffeste und die Straußwirtschaft erscheinen hier.", "Nothing special is currently affecting demand or prices. Events, farm festivals and the wine tavern appear here."),
                32, y, 460, 76, 17, Theme.InkSoft, maxLines: 3));
        }
        foreach (var effect in effects.Skip(page * perPage).Take(perPage))
        {
            var good = effect.Kind switch
            {
                EffectKind.Costs => effect.Value < 1,
                EffectKind.Quality => effect.Value > 0,
                EffectKind.Guard => true,
                _ => effect.Value > 1,
            };
            card.With(Kit.Box(32, y + 8, 12, 12, good ? Theme.Good : Theme.Alert, 6));
            card.With(Kit.Text(effect.Label, 54, y, 438, 26, 17, Theme.Ink, bold: true));
            card.With(Kit.Text(Lang.En ? $"{EstateScene.EffectText(effect)} · {effect.MonthsLeft} {(effect.MonthsLeft == 1 ? "month" : "months")} left" : $"{EstateScene.EffectText(effect)} · noch {effect.MonthsLeft} {(effect.MonthsLeft == 1 ? "Monat" : "Monate")}", 54, y + 24, 438, 24, 16, good ? Theme.Good : Theme.Alert));
            y += 54;
        }

        card.With(Rule(826));
        var demand = Rules.EffectValue(player, EffectKind.Demand);
        var price = Rules.EffectValue(player, EffectKind.Price);
        card.With(Kit.Row(Lang.T("Nachfrage durch Effekte", "Demand from effects"), Percent(demand), 32, 842, 460, demand > 1 ? Theme.Good : demand < 1 ? Theme.Alert : null, 19));
        card.With(Kit.Row(Lang.T("Preise durch Effekte", "Prices from effects"), Percent(price), 32, 876, 460, price > 1 ? Theme.Good : price < 1 ? Theme.Alert : null, 19));
        return card;
    }

    static string Percent(double factor)
    {
        var percent = Js.Round((factor - 1) * 100);
        return percent == 0 ? Lang.T("unverändert", "unchanged") : $"{(percent > 0 ? "+" : "−")}{Math.Abs(percent)} %";
    }
}
