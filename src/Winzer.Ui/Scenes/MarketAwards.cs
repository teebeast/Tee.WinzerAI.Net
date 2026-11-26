using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

// Reiter „Prämierungen“: nächste Prämierung, einreichbare Weine, Anmeldungen und Medaillen-Vitrine
public static partial class MarketScene
{
    static readonly Medal[] MedalOrder = [Medal.Gold, Medal.Silber, Medal.Bronze];
    /// <summary>Die Tagesform der Jury verschiebt die Wertung um bis zu so viele Punkte</summary>
    const double JurySpread = 5;

    static void BuildAwards(GameSession session, SkiaLayout scene)
    {
        var state = session.State;
        var player = session.Player;
        var competition = Market.UpcomingCompetition(state.Month);

        // Einreichbar sind geprüfte Weine; was noch auf die Prüfung wartet, steht mit Sperrgrund dabei
        var lots = player.Bottles
            .Where(l => l.Registered || (l.MonthsUntilReady == 0 && (l.Sparkling || l.Level != QualityLevel.DeutscherWein)))
            .OrderByDescending(l => player.CompetitionEntries.Contains(l.Id))
            .ThenByDescending(l => l.Registered)
            .ThenByDescending(l => Market.CompetitionBaseScore(l))
            .ToList();
        var wanted = session.Nav.Selected("markt.entry");
        var selected = lots.FirstOrDefault(l => l.Id == wanted) ?? lots.FirstOrDefault();
        const int perPage = 8;
        var pages = Math.Max(1, (int)Math.Ceiling(lots.Count / (double)perPage));
        var page = session.Nav.Page("markt.entries", pages);

        scene.With(CompetitionPanel(state, player, competition));

        var index = 0;
        foreach (var lot in lots.Skip(page * perPage).Take(perPage))
        {
            scene.With(EntryCard(session, lot, competition, 28 + index % 4 * (LotWidth + 16), 276 + index / 4 * 322, lot == selected));
            index++;
        }
        if (lots.Count == 0)
        {
            scene.With(EmptySlot(28, 276, 1202, 300,
                Lang.T("Noch kein Wein, der antreten könnte. Nur amtlich geprüfte Flaschen dürfen zur Prämierung – im Keller abfüllen und prüfen lassen.", "No wine that could compete yet. Only officially inspected bottles may enter the wine award – bottle and have them inspected in the cellar.")));
        }
        if (pages > 1) scene.With(GridPager(session, "markt.entries", page, pages, Lang.T("Seite", "Page"), 920));

        scene.With(AwardsInspector(session, selected, competition));
    }

    static int MonthsUntil(int month, int current) => (month - current + 12) % 12;

    static SkiaControl CompetitionPanel(GameState state, Player player, CompetitionInfo competition)
    {
        var panel = Kit.Glass(28, 88, 1202, 172);
        var wait = MonthsUntil(competition.Month, state.Month);
        var monthName = Game.MonthNames[competition.Month - 1];
        panel.With(Kit.Text(competition.Name, 28, 14, 560, 42, 29, Theme.Gold, bold: true, serif: true));
        panel.With(Kit.Text(Lang.En ? (wait == 0 ? $"Judging at the end of {monthName} – last chance to enter!" : $"Judging at the end of {monthName} – {wait} {(wait == 1 ? "month" : "months")} left to enter")
                : (wait == 0 ? $"Wertung Ende {monthName} – letzte Gelegenheit zum Anmelden!" : $"Wertung Ende {monthName} – noch {wait} {(wait == 1 ? "Monat" : "Monate")} zum Anmelden"),
            28, 58, 580, 28, 19, wait == 0 ? Theme.Gold : Theme.Cream, bold: wait == 0));
        panel.With(Kit.Text(Lang.En ? $"Fee {Util.FormatEuro(competition.Fee)} per wine · entered {player.CompetitionEntries.Count} / {competition.MaxEntries}" : $"Gebühr {Util.FormatEuro(competition.Fee)} je Wein · angemeldet {player.CompetitionEntries.Count} / {competition.MaxEntries}",
            28, 88, 580, 28, 19, Theme.Cream));
        var other = Economy.Competitions.Values.First(c => c.Id != competition.Id);
        panel.With(Kit.Text(Lang.En ? $"Score = quality + level bonus ± {Util.FormatNumber(JurySpread)} points of daily form. Next: {other.Name} in {Game.MonthNames[other.Month - 1]} ({Util.FormatEuro(other.Fee)})." : $"Wertung = Güte + Bonus der Stufe ± {Util.FormatNumber(JurySpread)} Punkte Tagesform. Danach: {other.Name} im {Game.MonthNames[other.Month - 1]} ({Util.FormatEuro(other.Fee)}).",
            28, 120, 580, 46, 16, Theme.CreamSoft, maxLines: 2));

        for (var i = 0; i < MedalOrder.Length; i++)
        {
            var medal = MedalOrder[i];
            var tile = Kit.Box(626 + i * 188, 16, 176, 140, Theme.GlassLight, 14);
            tile.With(Kit.Paint(0, 8, 64, 84, (c, w, h) => PaintMedal(c, 34, 56, 22, medal)));
            tile.With(Kit.Text(Economy.MedalNames[medal], 68, 14, 104, 30, 21, Theme.Cream, bold: true, serif: true));
            tile.With(Kit.Text(Lang.En ? $"from {Util.FormatNumber(competition.Thresholds[medal])}" : $"ab {Util.FormatNumber(competition.Thresholds[medal])}", 68, 44, 104, 34, 24, Theme.Gold, bold: true));
            tile.With(Kit.Text(Lang.En ? $"+{Util.FormatNumber(competition.Points[medal])} points" : $"+{Util.FormatNumber(competition.Points[medal])} Punkte", 14, 88, 150, 24, 16, Theme.Cream));
            tile.With(Kit.Text(Lang.En ? $"+{Util.FormatNumber(competition.Reputation[medal])} reputation" : $"+{Util.FormatNumber(competition.Reputation[medal])} Bekanntheit", 14, 110, 150, 24, 16, Theme.CreamSoft));
            panel.With(tile);
        }
        return panel;
    }

    static Medal? MedalAt(double score, CompetitionInfo competition)
    {
        foreach (var medal in MedalOrder)
        {
            if (score >= competition.Thresholds[medal]) return medal;
        }
        return null;
    }

    /// <summary>Aussicht in Worten: was sicher ist und was mit guter Tagesform möglich wäre</summary>
    static string Outlook(double score, CompetitionInfo competition)
    {
        var sure = MedalAt(score - JurySpread, competition);
        var best = MedalAt(score + JurySpread - 0.01, competition);
        if (best is not { } top) return Lang.T("Keine Medaille in Reichweite", "No medal within reach");
        if (sure == best) return Lang.En ? $"{Economy.MedalNames[top]} all but certain" : $"{Economy.MedalNames[top]} so gut wie sicher";
        return Lang.En ? (sure is { } floor ? $"{Economy.MedalNames[floor]} certain, {Economy.MedalNames[top]} possible" : $"{Economy.MedalNames[top]} possible") : (sure is { } floor2 ? $"{Economy.MedalNames[floor2]} sicher, {Economy.MedalNames[top]} möglich" : $"{Economy.MedalNames[top]} möglich");
    }

    /// <summary>Skala 60–100 mit den Medaillenschwellen und der Spanne, in der die Wertung landen kann</summary>
    static void PaintScoreBar(SKCanvas c, float w, float h, double score, CompetitionInfo competition)
    {
        const double from = 60, to = 100;
        float X(double value) => (float)(Math.Clamp((value - from) / (to - from), 0, 1) * w);
        var top = h / 2 - 6;
        using (var track = Draw.Fill("#d9c7a3")) c.DrawRoundRect(0, top, w, 12, 6, 6, track);
        c.Save();
        using (var clip = new SKPath())
        {
            clip.AddRoundRect(new SKRect(0, top, w, top + 12), 6, 6);
            c.ClipPath(clip, antialias: true);
        }
        var edges = new[] { competition.Thresholds[Medal.Bronze], competition.Thresholds[Medal.Silber], competition.Thresholds[Medal.Gold], to };
        Medal[] zones = [Medal.Bronze, Medal.Silber, Medal.Gold];
        for (var i = 0; i < zones.Length; i++)
        {
            using var zone = Draw.Fill(MedalColors(zones[i]).Bottom);
            c.DrawRect(X(edges[i]), top, X(edges[i + 1]) - X(edges[i]), 12, zone);
        }
        c.Restore();
        // Spanne der Tagesform und erwartete Wertung
        using (var span = Draw.Fill(Theme.SkWine.WithAlpha(70))) c.DrawRoundRect(X(score - JurySpread), top - 5, X(score + JurySpread) - X(score - JurySpread), 22, 6, 6, span);
        using (var edge = Draw.Stroke(Theme.SkWine, 2)) c.DrawRoundRect(X(score - JurySpread), top - 5, X(score + JurySpread) - X(score - JurySpread), 22, 6, 6, edge);
        using (var dot = Draw.Fill(Theme.SkWineDark)) c.DrawCircle(Math.Clamp(X(score), 9, w - 9), h / 2, 7, dot);
        using var ring = Draw.Stroke(SKColor.Parse("#fbf4e4"), 2.5f);
        c.DrawCircle(Math.Clamp(X(score), 9, w - 9), h / 2, 7, ring);
    }

    static SkiaControl EntryCard(GameSession session, BottleLot lot, CompetitionInfo competition, double x, double y, bool selected)
    {
        var player = session.Player;
        var entered = player.CompetitionEntries.Contains(lot.Id);
        var score = Market.CompetitionBaseScore(lot);
        var problem = entered ? null : Market.CompetitionEntryProblem(player, lot, competition);
        var card = Kit.Card(x, y, LotWidth, 306, 16);
        card.With(Bottle(lot, 18, 16, 58, 174));
        card.With(Kit.Text(GrapeName(lot), 90, 16, 186, 54, 19, Theme.Ink, bold: true, maxLines: 2));
        card.With(Kit.Text(KindName(lot).Length > 14 ? KindName(lot) : $"{KindName(lot)} · {Lang.T("Jg.", "Vint.")} {lot.Vintage}", 90, 70, 186, 24, 16, Theme.InkSoft));
        card.With(Kit.Text(Lang.T("Erwartete Wertung", "Expected score"), 90, 100, 186, 22, 15, Theme.InkSoft));
        card.With(Kit.Text(Util.FormatNumber(score), 90, 118, 80, 44, 32, Theme.Wine, bold: true, serif: true));
        card.With(Kit.Text($"± {Util.FormatNumber(JurySpread)}", 150, 132, 60, 24, 16, Theme.InkSoft));
        card.With(AwardDots(lot, 90, 164, 24));
        card.With(Kit.Paint(18, 200, LotWidth - 36, 28, (c, w, h) => PaintScoreBar(c, w, h, score, competition)));
        if (entered)
        {
            card.With(Chip(Lang.T("angemeldet", "entered"), 18, 238, LotWidth - 36, Theme.Good, Theme.Paper, 15));
            card.With(Kit.Text(Outlook(score, competition), 18, 274, LotWidth - 36, 24, 16, Theme.Ink, bold: true, align: Align.Center));
        }
        else
        {
            card.With(Kit.Text(Outlook(score, competition), 18, 234, LotWidth - 36, 24, 17, Theme.Ink, bold: true));
            card.With(Kit.Text(problem ?? (Lang.En ? $"Can compete · fee {Util.FormatEuro(competition.Fee)}" : $"Kann antreten · Gebühr {Util.FormatEuro(competition.Fee)}"), 18, 258, LotWidth - 36, 44, 15, problem is null ? Theme.Good : Theme.Alert, maxLines: 2));
        }
        card.With(SelectionFrame(selected, LotWidth, 306));
        var id = lot.Id;
        card.Tap(() => session.Nav.Select("markt.entry", id));
        return card;
    }

    static SkiaControl AwardsInspector(GameSession session, BottleLot? lot, CompetitionInfo competition)
    {
        var state = session.State;
        var player = session.Player;
        var card = Inspector();
        var monthName = Game.MonthNames[competition.Month - 1];

        if (lot is null)
        {
            card.With(Kit.Text(Lang.T("Prämierungen", "Wine awards"), 32, 28, 460, 46, 32, Theme.Ink, bold: true, serif: true));
            card.With(Kit.Text(Lang.T("Medaillen bringen Punkte und Bekanntheit", "Medals bring points and reputation"), 32, 74, 460, 28, 18, Theme.InkSoft));
            card.With(Kit.Text(
                Lang.En ? $"Twice a year a jury judges submitted wines: in {Game.MonthNames[Economy.Competitions[CompetitionId.Regional].Month - 1]} the {Economy.Competitions[CompetitionId.Regional].Name}, in {Game.MonthNames[Economy.Competitions[CompetitionId.Bund].Month - 1]} the {Economy.Competitions[CompetitionId.Bund].Name}. At most {competition.MaxEntries} inspected wines may enter each award; entered bottles stay in stock until judging."
                    : $"Zweimal im Jahr bewertet eine Jury eingereichte Weine: im {Game.MonthNames[Economy.Competitions[CompetitionId.Regional].Month - 1]} die {Economy.Competitions[CompetitionId.Regional].Name}, im {Game.MonthNames[Economy.Competitions[CompetitionId.Bund].Month - 1]} die {Economy.Competitions[CompetitionId.Bund].Name}. Je Prämierung dürfen höchstens {competition.MaxEntries} geprüfte Weine antreten; angemeldete Flaschen bleiben bis zur Wertung im Lager.",
                32, 124, 460, 190, 19, Theme.Ink, maxLines: 7));
        }
        else
        {
            var entered = player.CompetitionEntries.Contains(lot.Id);
            var score = Market.CompetitionBaseScore(lot);
            var bonus = score - lot.Quality;
            card.With(Bottle(lot, 426, 24, 50, 150));
            card.With(Kit.Text(GrapeName(lot), 32, 28, 384, 44, 32, Theme.Ink, bold: true, serif: true));
            card.With(Kit.Text(Lang.En ? $"{KindName(lot)} · vintage {lot.Vintage} · {Util.FormatNumber(lot.Bottles)} btl." : $"{KindName(lot)} · Jahrgang {lot.Vintage} · {Util.FormatNumber(lot.Bottles)} Fl.", 32, 74, 384, 28, 18, Theme.InkSoft));
            card.With(Kit.Row(Lang.T("Güte", "Quality"), Util.FormatNumber(lot.Quality), 32, 118, 380, size: 19));
            card.With(Kit.Row(lot.Sparkling ? Lang.T("Bonus für Sekt", "Sparkling wine bonus") : Lang.T("Bonus der Stufe", "Level bonus"), $"+{Util.FormatNumber(bonus)}", 32, 152, 380, size: 19));
            card.With(Kit.Row(Lang.T("Erwartete Wertung", "Expected score"), $"{Util.FormatNumber(score)} ± {Util.FormatNumber(JurySpread)}", 32, 186, 460, Theme.Wine, 19));
            card.With(Kit.Paint(32, 226, 460, 36, (c, w, h) => PaintScoreBar(c, w, h, score, competition)));
            foreach (var medal in MedalOrder)
            {
                var at = (competition.Thresholds[medal] - 60) / 40 * 460;
                card.With(Kit.Text($"{Economy.MedalNames[medal]} {Util.FormatNumber(competition.Thresholds[medal])}", 32 + at - 4, 262, 110, 22, 15, Theme.InkSoft));
            }
            card.With(Kit.Text(Outlook(score, competition), 32, 290, 460, 30, 20, Theme.Ink, bold: true));

            var action = new GameAction.EnterCompetition(lot.Id);
            if (entered)
            {
                card.With(Kit.Box(32, 334, 460, 84, Theme.C("#334d7a2a"), 16, Theme.Good, 3)
                    .With(Kit.Text(Lang.T("Angemeldet", "Entered"), 22, 12, 416, 32, 21, Theme.Good, bold: true))
                    .With(Kit.Text(Lang.En ? $"The jury judges at the end of {monthName}." : $"Die Jury wertet Ende {monthName}.", 22, 46, 416, 24, 16, Theme.InkSoft)));
            }
            else
            {
                card.With(Kit.Button(Lang.En ? $"Enter: {competition.Name}" : $"Anmelden: {competition.Name}", 32, 334, 460, 84, () => session.Run(action), ButtonKind.Primary,
                    Lang.En ? $"Fee {Util.FormatEuro(competition.Fee)} · judging at the end of {monthName}" : $"Gebühr {Util.FormatEuro(competition.Fee)} · Wertung Ende {monthName}", session.Problem(action)));
            }
        }

        // Angemeldete Weine
        card.With(Rule(438));
        card.With(Kit.Text(Lang.T("Angemeldet", "Entered"), 32, 452, 300, 30, 20, Theme.Ink, bold: true));
        card.With(Kit.Text($"{player.CompetitionEntries.Count} / {competition.MaxEntries}", 332, 454, 160, 28, 19, Theme.Wine, bold: true, align: Align.End));
        var y = 490.0;
        foreach (var id in player.CompetitionEntries.Take(3))
        {
            if (player.Bottles.Find(l => l.Id == id) is not { } entry) continue;
            var entryScore = Market.CompetitionBaseScore(entry);
            var row = Kit.Box(32, y, 460, 40, Theme.C("#33d9c7a3"), 10);
            if (MedalAt(entryScore, competition) is { } likely) row.With(Kit.Paint(8, 6, 28, 28, (c, w, h) => PaintMedal(c, 14, 14, 12, likely, ribbon: false)));
            row.With(Kit.Text(Lang.En ? $"{GrapeName(entry)} {KindName(entry)} (vintage {entry.Vintage})" : $"{GrapeName(entry)} {KindName(entry)} (Jg. {entry.Vintage})", 44, 8, 300, 26, 17, Theme.Ink, bold: true));
            row.With(Kit.Text(Lang.En ? $"Score {Util.FormatNumber(entryScore)}" : $"Wertung {Util.FormatNumber(entryScore)}", 330, 8, 120, 26, 17, Theme.InkSoft, align: Align.End));
            var entryId = id;
            row.Tap(() => session.Nav.Select("markt.entry", entryId));
            card.With(row);
            y += 46;
        }
        if (player.CompetitionEntries.Count == 0)
        {
            card.With(Kit.Text(Lang.En ? $"No wine entered yet. Up to {competition.MaxEntries} wines may compete – those with the highest expected score are at the front left." : $"Noch kein Wein angemeldet. Bis zu {competition.MaxEntries} Weine dürfen antreten – die mit der höchsten erwarteten Wertung stehen links vorn.",
                32, y, 460, 76, 17, Theme.InkSoft, maxLines: 3));
        }

        // Vitrine
        card.With(Rule(640));
        card.With(Kit.Text(Lang.T("Medaillen-Vitrine", "Medal cabinet"), 32, 654, 300, 30, 20, Theme.Ink, bold: true));
        card.With(Kit.Text(Lang.En ? $"{player.Stats.Medals} won · {player.Stats.GoldMedals} gold" : $"{player.Stats.Medals} gewonnen · {player.Stats.GoldMedals} Gold", 252, 656, 240, 28, 18, Theme.Wine, bold: true, align: Align.End));
        var trophies = player.Bottles.SelectMany(l => l.Awards.Select(a => (Award: a, Lot: l)))
            .OrderByDescending(t => t.Award.Year).ThenBy(t => Array.IndexOf(MedalOrder, t.Award.Medal)).Take(6).ToList();
        card.With(Kit.Paint(32, 694, 460, 176, (c, w, h) =>
        {
            using (var back = Draw.VerticalGradient(0, h, (0, "#4a2e22"), (1, "#2a1a14"))) c.DrawRoundRect(0, 0, w, h, 14, 14, back);
            using (var shelf = Draw.VerticalGradient(h - 34, h - 22, (0, "#b07a45"), (1, "#6e4524"))) c.DrawRect(0, h - 34, w, 12, shelf);
            using (var glass = Draw.Stroke(SKColor.Parse("#f6d477").WithAlpha(110), 3)) c.DrawRoundRect(1.5f, 1.5f, w - 3, h - 3, 14, 14, glass);
            for (var i = 0; i < trophies.Count; i++) PaintMedal(c, 46 + i * 74, 84, 26, trophies[i].Award.Medal);
        }));
        for (var i = 0; i < trophies.Count; i++)
        {
            var award = trophies[i].Award;
            card.With(Kit.Text(Lang.En ? $"{(award.Competition == CompetitionId.Bund ? "Crown" : "Regional")} yr {award.Year}" : $"{(award.Competition == CompetitionId.Bund ? "Krone" : "Regio")} J. {award.Year}", 32 + 10 + i * 74, 694 + 146, 74, 22, 14, Theme.CreamSoft, align: Align.Center));
        }
        if (trophies.Count == 0)
        {
            card.With(Kit.Text(player.Stats.Medals > 0 ? Lang.T("Die prämierten Weine sind verkauft – ihre Medaillen zählen weiter.", "The award-winning wines are sold – their medals still count.") : Lang.T("Noch leer – die erste Medaille wartet.", "Still empty – the first medal awaits."),
                52, 694 + 60, 420, 30, 19, Theme.CreamSoft, align: Align.Center));
        }
        card.With(Kit.Text(trophies.Count > 0 ? Lang.T("Medaillen der Weine, die noch im Lager liegen.", "Medals of the wines still in stock.") : Lang.T("Jeder Wein kann je Prämierung nur einmal eine Medaille holen.", "Each wine can win only one medal per wine award."),
            32, 880, 460, 24, 16, Theme.InkSoft));
        return card;
    }
}
