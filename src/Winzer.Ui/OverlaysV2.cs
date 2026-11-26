using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui;

/// <summary>Bildschirme der erweiterten Regeln: Gutsplanung (Ziele, Ausbau, Preise), Jahresrückblick, Prämierung.</summary>
public static class OverlaysV2
{
    static readonly Color Panel = Theme.C("#f21c1418");
    static readonly Color Tile = Theme.C("#1af4ead7");

    static SkiaShape Frame(GameSession session, string title, string subtitle, out Action close)
    {
        var closeAction = () => session.Nav.Show(Overlay.None);
        close = closeAction;
        var card = Kit.Box(210, 96, 1500, 888, Panel, 26, Theme.GlassLine, 2, shadow: true);
        card.Block();
        card.With(Kit.Text(title, 48, 30, 1000, 54, 40, Theme.Gold, bold: true, serif: true));
        card.With(Kit.Text(subtitle, 48, 86, 1300, 30, 20, Theme.CreamSoft));
        card.With(Kit.IconButton("✕", 1424, 28, 48, closeAction, tip: Lang.T("Schließen", "Close")));
        return card;
    }

    static SkiaControl Progress(double x, double y, double w, double value, double target, Color color)
    {
        var share = target <= 0 ? 0 : Math.Clamp(value / target, 0, 1);
        return Kit.Box(x, y, w, 12, Theme.GlassLine, 6).With(Kit.Box(0, 0, Math.Max(12, w * share), 12, color, 6));
    }

    static string Number(double value) => Util.FormatNumber(Math.Max(0, value), value % 1 == 0 ? 0 : 1);

    // ---- Gutsplanung ----

    public static SkiaControl Plan(GameSession session)
    {
        var back = Overlays.Backdrop(onTap: () => session.Nav.Show(Overlay.None));
        var card = Frame(session, Lang.T("Gutsplanung", "Estate planning"), Lang.T("Wohin soll das Weingut? Ziele für dieses Jahr, Fernziele, Ausbau und Preise.", "Where is the estate headed? Goals for this year, long-term goals, upgrades and prices."), out _);
        var tab = Math.Clamp(session.Nav.OverlayTab, 0, 2);
        var player = session.Player;
        var open = (player.Goal is null && player.GoalOffers.Count > 0 ? 1 : 0) + (player.Ambitions.Count < RulesV2.AmbitionCount ? 1 : 0);
        card.With(Kit.Tabs(48, 132, Lang.En ? ["Goals & ambitions", "Upgrades & foundation", "Prices"] : Lang.En ? ["Goals & ambitions", "Upgrades & foundation", "Prices"] : ["Ziele & Ambitionen", "Ausbau & Stiftung", "Preise"], tab, session.Nav.SetOverlayTab, [open, 0, 0]));
        card.With(tab switch
        {
            1 => Buildings(session),
            2 => Prices(session),
            _ => Goals(session),
        });
        return back.With(card);
    }

    static SkiaControl Goals(GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var layer = Kit.Layer(48, 204, 1404, 660);

        // Links: Jahresziel
        layer.With(Kit.Text(Lang.En ? $"Goal for year {state.Year}" : $"Jahresziel {state.Year}", 0, 0, 560, 36, 26, Theme.Cream, bold: true, serif: true));
        if (player.Goal is null && player.GoalOffers.Count > 0)
        {
            layer.With(Kit.Text(Lang.T(Lang.T("Wähle eines. Erreichst du es bis Dezember, gibt es Punkte und Ansehen.", "Pick one. Reach it by December and you earn points and reputation."), "Pick one. Reach it by December and you earn points and reputation."), 0, 38, 560, 28, 17, Theme.CreamSoft));
            var y = 80.0;
            foreach (var id in player.GoalOffers)
            {
                if (RulesV2.Goal(id) is not { } goal) continue;
                var tile = Kit.Box(0, y, 560, 150, Tile, 16);
                tile.With(Kit.Text(goal.Title, 24, 16, 380, 34, 24, Theme.Gold, bold: true));
                tile.With(Kit.Text(Lang.En ? $"+{Util.JsNumber(goal.Points)} points" : Lang.En ? $"+{Util.JsNumber(goal.Points)} points" : $"+{Util.JsNumber(goal.Points)} Punkte", 400, 20, 140, 28, 19, Theme.Cream, bold: true, align: Align.End));
                tile.With(Kit.Text(goal.Text(state, player), 24, 54, 512, 50, 17, Theme.Cream, maxLines: 2));
                tile.With(Kit.Button(Lang.T(Lang.T("Dieses Ziel wählen", "Choose this goal"), "Choose this goal"), 24, 100, 240, 40, () => session.Run(new GameAction.ChooseGoal(goal.Id)), ButtonKind.Gold,
                    problem: session.Problem(new GameAction.ChooseGoal(goal.Id)), fontSize: 17, align: Align.Center));
                layer.With(tile);
                y += 166;
            }
        }
        else if (RulesV2.Goal(player.Goal) is { } goal)
        {
            var (value, target) = goal.Progress(state, player);
            var done = value >= target;
            var tile = Kit.Box(0, 56, 560, 220, Tile, 16);
            tile.With(Kit.Text(goal.Title, 24, 20, 380, 36, 26, Theme.Gold, bold: true));
            tile.With(Kit.Text(Lang.En ? $"+{Util.JsNumber(goal.Points)} points" : Lang.En ? $"+{Util.JsNumber(goal.Points)} points" : $"+{Util.JsNumber(goal.Points)} Punkte", 400, 24, 140, 28, 19, Theme.Cream, bold: true, align: Align.End));
            tile.With(Kit.Text(goal.Text(state, player), 24, 66, 512, 54, 18, Theme.Cream, maxLines: 2));
            tile.With(Progress(24, 136, 512, value, target, done ? Theme.GoodLight : Theme.Gold));
            tile.With(Kit.Text(done ? Lang.T("Erreicht – die Punkte gibt es im Dezember, wenn es so bleibt.", "Reached – you get the points in December if it holds.") : Lang.En ? $"Progress: {Number(value)} of {Number(target)}" : $"Stand: {Number(value)} von {Number(target)}",
                24, 160, 512, 28, 17, done ? Theme.GoodLight : Theme.CreamSoft));
            layer.With(tile);
            if (player.Review is { } review)
            {
                layer.With(Kit.Button(Lang.En ? $"View review of year {review.Year}" : Lang.En ? $"View review of year {review.Year}" : $"Rückblick auf Jahr {review.Year} ansehen", 0, 300, 360, 52, () => session.Nav.Show(Overlay.Review), ButtonKind.Dark, fontSize: 18, align: Align.Center));
            }
        }
        else
        {
            layer.With(Kit.Text(Lang.T(Lang.T("Im Januar stehen wieder drei Ziele zur Wahl.", "Three new goals to choose from in January."), "Three new goals to choose from in January."), 0, 56, 560, 28, 18, Theme.CreamSoft));
        }

        // Rechts: Ambitionen
        var choosing = player.Ambitions.Count < RulesV2.AmbitionCount;
        layer.With(Kit.Text(Lang.T("Ambitionen", "Ambitions"), 620, 0, 500, 36, 26, Theme.Cream, bold: true, serif: true));
        layer.With(Kit.Text(choosing
            ? Lang.En ? $"Choose {RulesV2.AmbitionCount} secret long-term goals ({player.Ambitions.Count} chosen). The harder, the more points – once, when reached."
                : $"Wähle {RulesV2.AmbitionCount} geheime Fernziele ({player.Ambitions.Count} gewählt). Je schwerer, desto mehr Punkte – einmalig beim Erreichen."
            : Lang.T("Deine geheimen Fernziele – die Mitspieler kennen sie nicht. Die Punkte gibt es einmalig beim Erreichen.", "Your secret long-term goals – hidden from the other players. Points come once, when reached."),
            620, 38, 784, 28, 17, Theme.CreamSoft));
        var shown = choosing ? RulesV2.Ambitions.ToList() : RulesV2.Ambitions.Where(a => player.Ambitions.Contains(a.Id)).ToList();
        for (var i = 0; i < shown.Count; i++)
        {
            var ambition = shown[i];
            var chosen = player.Ambitions.Contains(ambition.Id);
            var done = player.AmbitionsDone.Contains(ambition.Id);
            var x = 620 + (i % 2) * 400;
            var y = 80 + (i / 2) * (choosing ? 190 : 250);
            var height = choosing ? 174 : 230;
            var tile = Kit.Box(x, y, 384, height, chosen ? Theme.C("#33f6d477") : Tile, 16, chosen ? Theme.Gold : null, 2);
            tile.With(Kit.Text(ambition.Icon, 20, 16, 50, 44, 32, Theme.Cream));
            tile.With(Kit.Text(ambition.Name, 76, 20, 290, 32, 22, Theme.Gold, bold: true));
            var worth = $"{Util.JsNumber(ambition.Points)} {Lang.T("Punkte", "points")} · {ambition.Difficulty}";
            tile.With(Kit.Text(ambition.Text, 20, 66, 344, 50, 16, Theme.Cream, maxLines: 2));
            if (choosing)
            {
                tile.With(Kit.Text(worth, 196, 130, 170, 26, 17, Theme.Cream, bold: true, align: Align.End));
                if (chosen) tile.With(Kit.Text(Lang.T("gewählt", "chosen"), 20, 130, 170, 26, 17, Theme.Gold, bold: true));
                else tile.With(Kit.Button(Lang.T("Wählen", "Choose"), 20, 122, 160, 40, () => session.Run(new GameAction.ChooseAmbition(ambition.Id)), ButtonKind.Gold, fontSize: 17, align: Align.Center));
            }
            else
            {
                var (value, target) = ambition.Progress(session.State, player);
                tile.With(Kit.Text(worth, 20, 112, 344, 24, 16, Theme.CreamSoft));
                tile.With(Progress(20, 138, 344, done ? target : value, target, done ? Theme.GoodLight : Theme.Gold));
                tile.With(Kit.Text(done ? Lang.T("Erfüllt!", "Fulfilled!") : player.Tainted && ambition.Id == "saubermann" ? Lang.T("Verspielt.", "Forfeited.") : Lang.En ? $"Progress: {Number(value)} of {Number(target)}" : $"Stand: {Number(value)} von {Number(target)}",
                    20, 162, 344, 28, 17, done ? Theme.GoodLight : Theme.CreamSoft, bold: done));
            }
            layer.With(tile);
        }
        return layer;
    }

    static SkiaControl Buildings(GameSession session)
    {
        var player = session.Player;
        var layer = Kit.Layer(48, 204, 1404, 660);
        layer.With(Kit.Text(Lang.T("Ausbau des Guts", "Estate upgrades"), 0, 0, 800, 36, 26, Theme.Cream, bold: true, serif: true));
        layer.With(Kit.Text(Lang.T(Lang.T("Große Vorhaben für volle Kassen. Jeder Ausbau wirkt dauerhaft.", "Big projects for full coffers. Every upgrade is permanent."), "Big projects for full coffers. Every upgrade is permanent."), 0, 38, 900, 28, 17, Theme.CreamSoft));
        for (var i = 0; i < RulesV2.Buildings.Length; i++)
        {
            var building = RulesV2.Buildings[i];
            var owned = player.Buildings.Contains(building.Id);
            var tile = Kit.Box((i % 2) * 468, 80 + (i / 2) * 250, 452, 234, owned ? Theme.C("#33f6d477") : Tile, 16, owned ? Theme.Gold : null, 2);
            tile.With(Kit.Text(building.Icon, 22, 18, 56, 48, 36, Theme.Cream));
            tile.With(Kit.Text(building.Name, 84, 22, 350, 34, 24, Theme.Gold, bold: true));
            tile.With(Kit.Text(building.Text, 22, 74, 408, 52, 17, Theme.Cream, maxLines: 2));
            var costs = Util.FormatEuro(building.Cost) + (building.MonthlyCost > 0 ? $" · {Util.FormatEuro(building.MonthlyCost)} {Lang.T("im Monat", "per month")}" : "");
            tile.With(Kit.Text(costs, 22, 132, 408, 28, 18, Theme.CreamSoft, bold: true));
            if (owned) tile.With(Kit.Text(Lang.T("steht", "built"), 22, 180, 408, 28, 19, Theme.Gold, bold: true));
            else
            {
                var action = new GameAction.Construct(building.Id);
                tile.With(Kit.Button(Lang.T("Bauen", "Build"), 22, 164, 408, 56, () => session.Run(action), ButtonKind.Gold, problem: session.Problem(action), fontSize: 21, align: Align.Center, quiet: true));
            }
            layer.With(tile);
        }

        // Stiftung
        var cost = RulesV2.DonationCost(player);
        var donate = new GameAction.Donate();
        var gift = Kit.Box(964, 80, 440, 484, Tile, 16);
        gift.With(Kit.Text("🏛️", 24, 20, 60, 52, 38, Theme.Cream));
        gift.With(Kit.Text(Lang.T("Stiftung Weinkultur", "Wine Culture Foundation"), 90, 26, 330, 36, 24, Theme.Gold, bold: true));
        gift.With(Kit.Text(Lang.T(Lang.T("Wer hat, der gibt: Eine Spende für Lehrpfad, Museum und Nachwuchs bringt Ansehen, das bleibt.", "Those who have, give: a donation for the educational trail, museum and young talent earns lasting reputation."), "Those who have, give: a donation for the educational trail, museum and young talent earns lasting reputation."), 24, 86, 392, 80, 17, Theme.Cream, maxLines: 3));
        gift.With(Kit.Row(Lang.T("Nächste Spende", "Next donation"), Util.FormatEuro(cost), 24, 190, 392, Theme.Cream, labelColor: Theme.CreamSoft));
        gift.With(Kit.Row(Lang.T("Bringt", "Earns"), Lang.En ? $"+{Util.JsNumber(RulesV2.DonationPoints)} points" : $"+{Util.JsNumber(RulesV2.DonationPoints)} Punkte", 24, 228, 392, Theme.Cream, labelColor: Theme.CreamSoft));
        gift.With(Kit.Row(Lang.T("Bisher gespendet", "Donated so far"), $"{player.Donations} ×", 24, 266, 392, Theme.Cream, labelColor: Theme.CreamSoft));
        gift.With(Kit.Text(Lang.T(Lang.T("Jede weitere Spende kostet ein Viertel mehr.", "Each further donation costs a quarter more."), "Each further donation costs a quarter more."), 24, 312, 392, 28, 16, Theme.CreamSoft));
        gift.With(Kit.Button(Lang.T("Spenden", "Donate"), 24, 380, 392, 56, () => session.Run(donate), ButtonKind.Gold, problem: session.Problem(donate), fontSize: 21, align: Align.Center, quiet: true));
        layer.With(gift);
        return layer;
    }

    static SkiaControl Prices(GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var layer = Kit.Layer(48, 204, 1404, 660);
        layer.With(Kit.Text(Lang.T("Preise ab Hof", "Cellar-door prices"), 0, 0, 800, 36, 26, Theme.Cream, bold: true, serif: true));
        layer.With(Kit.Text(Lang.T(Lang.T("Dein Aufschlag auf den Richtpreis für Hofladen und Online-Shop. Teurer heißt: mehr je Flasche, aber weniger Kundschaft.", "Your markup on the guide price for the farm shop and online shop. Pricier means more per bottle, but fewer customers."), "Your markup on the guide price for the farm shop and online shop. Pricier means more per bottle, but fewer customers."),
            0, 38, 1300, 28, 17, Theme.CreamSoft));

        // Stufen von −20 % bis +40 %
        var x = 0.0;
        for (var step = -4; step <= 8; step++)
        {
            var level = step * 0.05;
            var on = Math.Abs(player.PriceLevel - level) < 0.001;
            var label = step == 0 ? Lang.T("Richtpreis", "Guide price") : $"{(step > 0 ? "+" : "−")}{Math.Abs(step * 5)} %";
            var width = step == 0 ? 150.0 : 92;
            var chip = on
                ? Kit.Box(x, 96, width, 64, radius: 14, gradient: Kit.Vertical(Theme.Gold, Theme.GoldDark))
                : Kit.Box(x, 96, width, 64, Tile, 14);
            chip.With(Kit.Text(label, 0, 18, width, 30, 20, on ? Theme.Ink : Theme.Cream, bold: true, align: Align.Center));
            var action = new GameAction.SetPriceLevel(level);
            chip.Tap(() => session.Run(action));
            layer.With(chip);
            x += width + 10;
        }

        var baseDemand = Market.MonthlyDemand(player);
        var demand = RulesV2.DirectDemand(state, player);
        var factor = RulesV2.DirectPriceFactor(state, player);
        var stock = player.Bottles.Where(l => l.MonthsUntilReady == 0).Sum(l => l.Bottles);
        var tile = Kit.Box(0, 200, 680, 400, Tile, 16);
        tile.With(Kit.Text(Lang.T("Was das gerade bedeutet", "What this means right now"), 28, 22, 600, 34, 22, Theme.Gold, bold: true));
        tile.With(Kit.Row(Lang.T("Kundschaft beim Richtpreis", "Customers at guide price"), $"{Util.FormatNumber(baseDemand)} {Lang.T("Fl. im Monat", "btl. per month")}", 28, 76, 624, Theme.Cream, labelColor: Theme.CreamSoft));
        tile.With(Kit.Row(Lang.T("Kundschaft bei deinem Preis", "Customers at your price"), $"{Util.FormatNumber(demand)} {Lang.T("Fl. im Monat", "btl. per month")}", 28, 118, 624, demand < baseDemand ? Theme.Warn : Theme.GoodLight, labelColor: Theme.CreamSoft));
        tile.With(Kit.Row(Lang.T("Erlös je Flasche", "Revenue per bottle"), Lang.En ? $"{Js.Round(factor * 100)} % of guide price" : $"{Js.Round(factor * 100)} % des Richtpreises", 28, 160, 624, Theme.Cream, labelColor: Theme.CreamSoft));
        tile.With(Kit.Row(Lang.T("Umsatz gegenüber Richtpreis", "Sales vs. guide price"), $"{Js.Round(demand / Math.Max(1, baseDemand) * factor * 100)} %", 28, 202, 624, Theme.Cream, labelColor: Theme.CreamSoft));
        tile.With(Kit.Row(Lang.T("Verkaufsfertiger Vorrat", "Stock ready to sell"), $"{Util.FormatNumber(stock)} {Lang.T("Fl.", "btl.")}", 28, 244, 624, Theme.Cream, labelColor: Theme.CreamSoft));
        var months = demand > 0 ? stock / demand : 0;
        tile.With(Kit.Text(stock <= 0 ? Lang.T("Kein verkaufsfertiger Wein im Lager.", "No wine ready to sell in stock.")
            : months > 8 ? Lang.En ? $"Stock lasts {Util.FormatNumber(months, 0)} months – a lower price clears the cellar." : $"Der Vorrat reicht für {Util.FormatNumber(months, 0)} Monate – ein niedrigerer Preis räumt das Lager."
            : months < 2 ? Lang.T("Der Vorrat ist knapp – ein höherer Preis holt mehr aus jeder Flasche.", "Stock is running low – a higher price gets more from every bottle.")
            : Lang.En ? $"Stock lasts about {Util.FormatNumber(months, 0)} months." : $"Der Vorrat reicht für etwa {Util.FormatNumber(months, 0)} Monate.", 28, 300, 624, 60, 18, Theme.CreamSoft, maxLines: 2));
        layer.With(tile);

        var info = Kit.Box(720, 200, 684, 400, Tile, 16);
        info.With(Kit.Text(Lang.T("Gut zu wissen", "Good to know"), 28, 22, 600, 34, 22, Theme.Gold, bold: true));
        string[] lines = Lang.En ?
        [
            "Well-known estates lose fewer customers at the same markup – medals make prices possible.",
            "The markup only applies at the cellar door and online. Wholesale, export and orders pay their own prices.",
            "Whatever isn't sold in a month stays in stock and ties up money. Leave room before the harvest.",
            player.Buildings.Contains("schaenke") ? "Your estate tavern brings a quarter more customers." : "An estate tavern (upgrade) brings a quarter more customers.",
        ] :
        [
            "Bekannte Weingüter verlieren bei gleichem Aufschlag weniger Kundschaft – Medaillen machen Preise möglich.",
            "Der Aufschlag gilt nur ab Hof und online. Großhandel, Export und Aufträge zahlen ihre eigenen Preise.",
            "Was im Monat nicht verkauft wird, bleibt im Lager und bindet Geld. Vor der Lese sollte Platz sein.",
            player.Buildings.Contains("schaenke") ? "Deine Gutsschänke bringt ein Viertel mehr Kundschaft." : "Eine Gutsschänke (Ausbau) bringt ein Viertel mehr Kundschaft.",
        ];
        for (var i = 0; i < lines.Length; i++)
        {
            info.With(Kit.Box(28, 84 + i * 76, 10, 10, Theme.Gold, 5));
            info.With(Kit.Text(lines[i], 52, 72 + i * 76, 604, 64, 17, Theme.Cream, maxLines: 3));
        }
        layer.With(info);
        return layer;
    }

    // ---- Jahresrückblick ----

    public static SkiaControl Review(GameSession session)
    {
        var review = session.Player.Review!;
        var back = Overlays.Backdrop(onTap: () => session.Nav.Show(Overlay.None));
        var card = Kit.Card(460, 130, 1000, 820, 26);
        card.Block();
        card.With(Kit.Box(0, 0, 1000, 14, Theme.Wine, 7));
        card.With(Kit.Text(Lang.En ? $"Review of year {review.Year}" : $"Rückblick auf Jahr {review.Year}", 56, 48, 888, 60, 44, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(session.Player.Name, 56, 110, 888, 30, 21, Theme.InkSoft));

        // Jahrgangsnote als Sterne
        card.With(Kit.Text(Lang.T("Der Jahrgang", "The vintage"), 56, 170, 400, 30, 20, Theme.InkSoft));
        card.With(Kit.Paint(56, 204, 300, 56, (c, w, h) =>
        {
            for (var i = 0; i < 5; i++)
            {
                using var p = Draw.Fill(i < review.VintageStars ? Theme.SkGoldDark : SKColor.Parse("#d9c7a3"));
                c.Save();
                c.Translate(i * 58, 0);
                c.Scale(1.75f);
                Draw.Path(c, "M15 1 l4.5 9.5 10.5 1.3 -7.7 7.2 2 10.3 -9.3 -5.1 -9.3 5.1 2 -10.3 -7.7 -7.2 10.5 -1.3 Z", p);
                c.Restore();
            }
        }));
        string[] notes = Lang.En ? ["", "a difficult year", "a modest year", "a decent year", "a very good year", "a vintage of the century"] : ["", "ein schwieriges Jahr", "ein mäßiges Jahr", "ein ordentliches Jahr", "ein sehr gutes Jahr", "ein Jahrhundertjahrgang"];
        card.With(Kit.Text(notes[Math.Clamp(review.VintageStars, 1, 5)], 380, 214, 560, 36, 26, Theme.Ink, serif: true));

        // Jahresziel
        var goal = Kit.Box(56, 290, 888, 110, review.GoalReached ? Theme.C("#337dbb6a") : Theme.C("#33d9c7a3"), 16);
        goal.With(Kit.Text(Lang.T("Jahresziel", "Yearly goal"), 28, 14, 400, 26, 18, Theme.InkSoft));
        goal.With(Kit.Text(review.GoalText, 28, 42, 560, 40, 28, Theme.Ink, bold: true, serif: true));
        goal.With(Kit.Text(review.GoalReached ? Lang.En ? $"reached · +{Util.JsNumber(review.GoalPoints)} points" : $"erreicht · +{Util.JsNumber(review.GoalPoints)} Punkte" : Lang.T("verfehlt", "missed"), 560, 40, 300, 40, 24,
            review.GoalReached ? Theme.Good : Theme.Alert, bold: true, align: Align.End));
        card.With(goal);

        var y = 440.0;
        void Line(string label, string value, Color? color = null)
        {
            card.With(Kit.Row(label, value, 56, y, 888, color, 24));
            y += 52;
        }
        Line(Lang.T("Punkte in diesem Jahr", "Points this year"), $"+{Util.FormatNumber(review.PointsGained)}");
        Line(Lang.T("Reinvermögen", "Net worth"), $"{(review.NetWorthChange >= 0 ? "+" : "−")}{Util.FormatEuro(Math.Abs(review.NetWorthChange))}", review.NetWorthChange >= 0 ? Theme.Good : Theme.Alert);
        Line(Lang.T("Verkaufte Flaschen", "Bottles sold"), Util.FormatNumber(review.BottlesSold));
        Line(Lang.T("Medaillen", "Medals"), review.Medals.ToString());
        if (session.State.Players.Count > 1) Line(Lang.T("Platz in der Rangliste", "Place in the standings"), Lang.En ? $"{review.Rank} of {session.State.Players.Count}" : $"{review.Rank} von {session.State.Players.Count}");

        card.With(Kit.Button(Lang.T("Auf ins neue Jahr", "On to the new year"), 660, 724, 284, 64, () => session.Nav.Show(Overlay.None), ButtonKind.Primary, fontSize: 24, align: Align.Center));
        return back.With(card);
    }

    // ---- Prämierung ----

    public static SkiaControl Show(GameSession session)
    {
        var show = session.State.LastShow!;
        var me = session.Player.Id;
        var competition = Economy.Competitions[show.Competition];
        var back = Overlays.Backdrop(onTap: () => session.Nav.Show(Overlay.None));
        var card = Frame(session, competition.Name, Lang.En ? $"Year {show.Year} · Three judges taste blind. Their average score counts." : Lang.En ? $"Year {show.Year} · Three judges taste blind. Their average score counts." : $"Jahr {show.Year} · Drei Juroren verkosten blind. Es zählt das Mittel ihrer Noten.", out var close);

        // Bühne mit Scheinwerfern
        card.With(Kit.Paint(0, 124, 1500, 80, (c, w, h) =>
        {
            using var light = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, h), [Theme.SkGold.WithAlpha(70), Theme.SkGold.WithAlpha(0)], SKShaderTileMode.Clamp),
            };
            Draw.Path(c, $"M250 0 L150 {h} L450 {h} Z M750 0 L600 {h} L900 {h} Z M1250 0 L1050 {h} L1350 {h} Z", light);
        }));

        // Kopfzeile der Wertungstafel
        var top = 214.0;
        string[] heads = Lang.En ? ["Estate", "Wine", "Judge 1", "Judge 2", "Judge 3", "Score", "Result"] : ["Weingut", "Wein", "Juror 1", "Juror 2", "Juror 3", "Wertung", "Ergebnis"];
        double[] cols = [48, 348, 800, 910, 1020, 1140, 1270];
        for (var i = 0; i < heads.Length; i++) card.With(Kit.Text(heads[i], cols[i], top, 200, 26, 17, Theme.CreamSoft, bold: true));

        // Eigene Weine zuerst, dann nach Wertung
        var entries = show.Entries.OrderByDescending(e => e.PlayerId == me).ThenByDescending(e => e.Score).ToList();
        const int perPage = 8;
        var pages = Math.Max(1, (int)Math.Ceiling(entries.Count / (double)perPage));
        var page = session.Nav.Page("show", pages);
        var y = top + 36;
        // Jede Zeile erscheint, dann nacheinander die drei Noten, die Wertung und zuletzt die Medaille
        var occasion = $"schau:{show.Competition}:{show.Year}:{page}";
        var index = 0;
        foreach (var entry in entries.Skip(page * perPage).Take(perPage))
        {
            var start = index++ * 0.2;
            var mine = entry.PlayerId == me;
            var row = Kit.Box(32, y, 1436, 64, mine ? Theme.C("#33f6d477") : Tile, 12, mine ? Theme.Gold : null, 1.5);
            row.With(Kit.Text(entry.PlayerName, 16, 18, 290, 30, 19, Theme.Cream, bold: mine));
            row.With(Kit.Text(entry.Wine, 316, 18, 440, 30, 19, Theme.Cream));
            for (var j = 0; j < entry.Jury.Count && j < 3; j++)
            {
                row.With(Kit.Text(Util.FormatNumber(entry.Jury[j], 1), 768 + j * 110, 16, 90, 32, 22, Theme.CreamSoft, align: Align.Start)
                    .Reveal(occasion, start + 0.2 + j * 0.14, rise: 8));
            }
            row.With(Kit.Text(Util.FormatNumber(entry.Score, 1), 1108, 12, 110, 38, 28, Theme.Gold, bold: true, serif: true).Reveal(occasion, start + 0.68, rise: 8));
            var medalAt = start + 0.9;
            if (entry.Medal is { } medal)
            {
                var color = medal == Medal.Gold ? "#f6d477" : medal == Medal.Silber ? "#d7dee4" : "#c98a52";
                row.With(Kit.Paint(1228, 2, 60, 60, (c, w, h) =>
                {
                    // Die Medaille fällt groß herein, landet und blitzt einmal auf
                    var e = (float)(Anim.Since(occasion, medalAt + 1.2) - medalAt);
                    if (e < 0) return;
                    var p = Math.Clamp(e / 0.28f, 0, 1);
                    var size = 1 + (1 - p) * (1 - p) * 1.5f;
                    var alpha = (byte)(255 * p);
                    c.Save();
                    c.Translate(10, 10);
                    c.Scale(size, size, 20, 18);
                    using (var ribbon = Draw.Fill(Theme.SkWine.WithAlpha(alpha))) Draw.Path(c, "M12 22 L8 40 L20 33 L32 40 L28 22 Z", ribbon);
                    using (var disc = Draw.Fill(SKColor.Parse(color).WithAlpha(alpha))) c.DrawCircle(20, 16, 14, disc);
                    using (var rim = Draw.Stroke(Theme.SkNight.WithAlpha((byte)(90 * p)), 1.5f)) c.DrawCircle(20, 16, 10, rim);
                    var gleam = (e - 0.28f) / 0.55f;
                    if (gleam is > 0 and < 1)
                    {
                        using var circle = new SKPath();
                        circle.AddCircle(20, 16, 14);
                        c.ClipPath(circle, antialias: true);
                        using var shine = Draw.Fill(SKColors.White.WithAlpha(170));
                        var x = -6 + gleam * 52;
                        Draw.Path(c, FormattableString.Invariant($"M{x} 0 L{x + 9} 0 L{x - 3} 32 L{x - 12} 32 Z"), shine);
                    }
                    c.Restore();
                }));
                row.With(Kit.Text($"{Economy.MedalNames[medal]} · +{Util.JsNumber(entry.Points)}", 1284, 18, 150, 30, 18, Theme.Cream, bold: true).Reveal(occasion, medalAt + 0.15, rise: 0));
            }
            else
            {
                row.With(Kit.Text(Lang.T("ohne Medaille", "no medal"), 1238, 18, 190, 30, 18, Theme.CreamSoft).Reveal(occasion, medalAt, rise: 0));
            }
            card.With(row.Reveal(occasion, start));
            y += 72;
        }
        var thresholds = competition.Thresholds;
        card.With(Kit.Text(Lang.En ? $"Gold from {Util.JsNumber(thresholds[Medal.Gold])} · Silver from {Util.JsNumber(thresholds[Medal.Silber])} · Bronze from {Util.JsNumber(thresholds[Medal.Bronze])}" : Lang.En ? $"Gold from {Util.JsNumber(thresholds[Medal.Gold])} · Silver from {Util.JsNumber(thresholds[Medal.Silber])} · Bronze from {Util.JsNumber(thresholds[Medal.Bronze])}" : $"Gold ab {Util.JsNumber(thresholds[Medal.Gold])} · Silber ab {Util.JsNumber(thresholds[Medal.Silber])} · Bronze ab {Util.JsNumber(thresholds[Medal.Bronze])}",
            48, 838, 700, 28, 18, Theme.CreamSoft));
        if (pages > 1) card.With(Kit.Pager(760, 830, 300, page, pages, p => session.Nav.SetPage("show", p)));
        card.With(Kit.Button(Lang.T("Weiter", "Continue"), 1216, 812, 236, 56, close, ButtonKind.Gold, fontSize: 22, align: Align.Center));
        return back.With(card);
    }
}
