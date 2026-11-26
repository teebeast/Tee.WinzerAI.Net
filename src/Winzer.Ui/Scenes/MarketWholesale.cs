using DrawnUi.Draw;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

// Reiter „Großhandel“: geprüfte Flaschenposten sofort, aber billiger verkaufen
public static partial class MarketScene
{
    const int LotsPerPage = 8;
    const double LotWidth = 288;

    static void BuildWholesale(GameSession session, SkiaLayout scene)
    {
        var state = session.State;
        var player = session.Player;
        var lots = player.Bottles.Where(l => l.Registered).ToList();
        var wanted = session.Nav.Selected("markt.lot");
        var selected = lots.FirstOrDefault(l => l.Id == wanted) ?? lots.FirstOrDefault();
        var pages = Math.Max(1, (int)Math.Ceiling(lots.Count / (double)LotsPerPage));
        var page = session.Nav.Page("markt.lots", pages);

        var index = 0;
        foreach (var lot in lots.Skip(page * LotsPerPage).Take(LotsPerPage))
        {
            scene.With(WholesaleCard(session, lot, 28 + index % 4 * (LotWidth + 16), 96 + index / 4 * 412, lot == selected));
            index++;
        }
        if (lots.Count == 0)
        {
            var empty = Kit.Glass(28, 96, 1202, 300);
            empty.With(Kit.Text(Lang.T("Noch keine geprüften Flaschen im Lager", "No inspected bottles in stock yet"), 40, 44, 1120, 46, 32, Theme.Cream, bold: true, serif: true));
            empty.With(Kit.Text(
                Lang.T("Der Großhandel nimmt nur amtlich geprüfte Weine – dafür sofort und in jeder Menge. Fülle im Keller ausgebauten Wein ab und lass ihn prüfen; dann steht jeder Posten hier zur Auswahl.", "Wholesale takes only officially inspected wines – but immediately and in any quantity. Bottle matured wine in the cellar and have it inspected; then every lot is available here."),
                40, 104, 1000, 90, 21, Theme.CreamSoft, maxLines: 3));
            empty.With(Kit.Button(Lang.T("Zum Keller ›", "To the cellar ›"), 40, 212, 240, 60, () => session.Nav.Go(Scene.Keller), ButtonKind.Dark, fontSize: 21, align: Align.Center));
            scene.With(empty);
        }
        else
        {
            var total = 0.0;
            foreach (var lot in lots) total += lot.Bottles;
            scene.With(Kit.Box(28, 920, 720, 44, Theme.C("#c71c1418"), 22)
                .With(Kit.Text(Lang.En ? $"{lots.Count} {(lots.Count == 1 ? "inspected lot" : "inspected lots")} · {Util.FormatNumber(total)} bottles · wholesale pays {Js.Round(Market.WholesaleSaturation(player) * Economy.WholesaleShare * 100)} % of the cellar-door price" : $"{lots.Count} {(lots.Count == 1 ? "geprüfter Posten" : "geprüfte Posten")} · {Util.FormatNumber(total)} Flaschen · Großhandel zahlt {Js.Round(Market.WholesaleSaturation(player) * Economy.WholesaleShare * 100)} % vom Hofpreis",
                    22, 9, 680, 26, 17, Theme.Cream)));
            if (pages > 1) scene.With(GridPager(session, "markt.lots", page, pages, Lang.T("Seite", "Page"), 920));
        }
        scene.With(selected is null ? WholesaleIntro(player) : WholesaleInspector(session, selected));
    }

    static SkiaControl AwardDots(BottleLot lot, double x, double y, double size = 26)
    {
        var count = Math.Min(4, lot.Awards.Count);
        var layer = Kit.Layer(x, y, size * 4.4, size);
        if (count == 0) return layer;
        var awards = lot.Awards.TakeLast(count).ToList();
        layer.With(Kit.Paint(0, 0, size * 4.4, size, (c, w, h) =>
        {
            for (var i = 0; i < awards.Count; i++) PaintMedal(c, h / 2 + i * h * 1.1f, h / 2, h / 2 - 1, awards[i].Medal, ribbon: false);
        }));
        return layer;
    }

    static SkiaControl WholesaleCard(GameSession session, BottleLot lot, double x, double y, bool selected)
    {
        var state = session.State;
        var player = session.Player;
        var card = Kit.Card(x, y, LotWidth, 396, 16);
        card.With(Bottle(lot, 18, 18, 70, 210));
        card.With(Kit.Text(GrapeName(lot), 102, 20, 172, 56, 20, Theme.Ink, bold: true, maxLines: 2));
        card.With(Kit.Text(KindName(lot), 102, 78, 172, 26, 17, Theme.InkSoft));
        card.With(Kit.Text(Lang.En ? $"Vintage {lot.Vintage}" : $"Jahrgang {lot.Vintage}", 102, 102, 172, 24, 16, Theme.InkSoft));
        card.With(Kit.Text(Lang.T("Güte", "Quality"), 102, 136, 80, 22, 15, Theme.InkSoft));
        card.With(Kit.Text(Util.FormatNumber(lot.Quality), 102, 154, 90, 44, 32, Theme.Wine, bold: true, serif: true));
        card.With(AwardDots(lot, 102, 202));
        card.With(Kit.Box(18, 242, LotWidth - 36, 2, Theme.PaperLine));
        card.With(Kit.Row(Lang.T("Im Lager", "In stock"), $"{Util.FormatNumber(lot.Bottles)} {Lang.T("Fl.", "btl.")}", 18, 254, LotWidth - 36, size: 17));
        card.With(Kit.Row(Lang.T("Ab Hof", "Cellar door"), Price(Market.BottlePrice(lot, player, state)), 18, 284, LotWidth - 36, size: 17));
        card.With(Kit.Row(Lang.T("Großhandel", "Wholesale"), Price(Market.WholesalePrice(lot, player, state)), 18, 314, LotWidth - 36, Theme.Wine, 17));
        if (player.CompetitionEntries.Contains(lot.Id)) card.With(Chip(Lang.T("zur Prämierung angemeldet", "entered for the wine award"), 18, 352, LotWidth - 36, Theme.PaperLine, Theme.Ink, 14));
        else card.With(Kit.Text(Lang.En ? $"all: +{Util.FormatEuro(Js.Round(Market.WholesalePrice(lot, player, state) * lot.Bottles))}" : $"alles: +{Util.FormatEuro(Js.Round(Market.WholesalePrice(lot, player, state) * lot.Bottles))}", 18, 354, LotWidth - 36, 24, 16, Theme.InkSoft, align: Align.Center));
        card.With(SelectionFrame(selected, LotWidth, 396));
        var id = lot.Id;
        card.Tap(() =>
        {
            session.Nav.Select("markt.lot", id);
        });
        return card;
    }

    static SkiaControl SaturationBlock(Player player, double y)
    {
        var layer = Kit.Layer(32, y, 460, 150);
        var factor = Market.WholesaleSaturation(player);
        // Der Preis fällt bis zur Untergrenze; dort ist der Balken voll
        var limit = Economy.WholesaleSaturationBottles * (1 - Economy.WholesaleMinFactor);
        layer.With(Kit.Text(Lang.T("Großhandel dieses Jahr", "Wholesale this year"), 0, 0, 300, 30, 20, Theme.Ink, bold: true));
        layer.With(Kit.Text(Lang.En ? $"pays {Js.Round(factor * 100)} %" : $"zahlt {Js.Round(factor * 100)} %", 260, 2, 200, 28, 19, factor < 1 ? Theme.Alert : Theme.Good, bold: true, align: Align.End));
        layer.With(Meter(0, 40, 460, player.WholesaleThisYear / limit, factor <= Economy.WholesaleMinFactor ? Theme.Alert : Theme.Wine, h: 12));
        layer.With(Kit.Text(
            Lang.En ? $"{Util.FormatNumber(player.WholesaleThisYear)} bottles delivered so far. The more per year, the less wholesale pays – at least {Js.Round(Economy.WholesaleMinFactor * 100)} % of the usual price. It recovers at the turn of the year." : $"Bisher {Util.FormatNumber(player.WholesaleThisYear)} Flaschen geliefert. Je mehr im Jahr, desto weniger zahlt der Großhandel – mindestens {Js.Round(Economy.WholesaleMinFactor * 100)} % des üblichen Preises. Zum Jahreswechsel erholt er sich.",
            0, 62, 460, 76, 17, Theme.InkSoft, maxLines: 3));
        return layer;
    }

    static SkiaControl WholesaleInspector(GameSession session, BottleLot lot)
    {
        var state = session.State;
        var player = session.Player;
        var farm = Market.BottlePrice(lot, player, state);
        var wholesale = Market.WholesalePrice(lot, player, state);
        var trend = Trends.TrendFactor(state, lot);
        var share = session.Nav.Amount("markt.amount", 1);
        var bottles = Math.Clamp(Js.Round(lot.Bottles * share), 1, lot.Bottles);

        var card = Inspector();
        card.With(Bottle(lot, 420, 24, 56, 168));
        card.With(Kit.Text(GrapeName(lot), 32, 28, 380, 44, 32, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(Lang.En ? $"{KindName(lot)} · vintage {lot.Vintage} · officially inspected" : $"{KindName(lot)} · Jahrgang {lot.Vintage} · amtlich geprüft", 32, 74, 380, 28, 18, Theme.InkSoft));
        card.With(AwardDots(lot, 32, 108, 30));

        var y = 150.0;
        void Row(string label, string text, Color? color = null)
        {
            card.With(Kit.Row(label, text, 32, y, y < 190 ? 372 : 460, color, 19));
            y += 34;
        }
        Row(Lang.T("Im Lager", "In stock"), Lang.En ? $"{Util.FormatNumber(lot.Bottles)} bottles" : $"{Util.FormatNumber(lot.Bottles)} Flaschen");
        Row(Lang.T("Güte", "Quality"), Util.FormatNumber(lot.Quality));
        Row(Lang.T("Markttrend", "Market trend"), $"{(trend > 1.005 ? "▲ " : trend < 0.995 ? "▼ " : "")}{Js.Round(trend * 100)} %", trend > 1.005 ? Theme.Good : trend < 0.995 ? Theme.Alert : null);
        Row(Lang.T("Preis ab Hof", "Cellar-door price"), Lang.En ? $"{Price(farm)} per bottle" : $"{Price(farm)} je Flasche");
        Row(Lang.T("Großhandelspreis", "Wholesale price"), Lang.En ? $"{Price(wholesale)} per bottle" : $"{Price(wholesale)} je Flasche", Theme.Wine);

        card.With(Rule(330));
        card.With(SaturationBlock(player, 346));
        card.With(Rule(496));

        card.With(Kit.Text(Lang.T("Menge für den Verkauf", "Amount to sell"), 32, 512, 460, 30, 20, Theme.Ink, bold: true));
        card.With(Kit.AmountSlider(32, 556, 460, share, Lang.En ? $"{Util.FormatNumber(bottles)} of {Util.FormatNumber(lot.Bottles)} btl." : $"{Util.FormatNumber(bottles)} von {Util.FormatNumber(lot.Bottles)} Fl.", v => session.Nav.SetAmount("markt.amount", v)));

        var action = new GameAction.SellWholesale(lot.Id, bottles);
        var revenue = Js.Round(wholesale * bottles);
        card.With(Kit.Button(Lang.T("An den Großhandel verkaufen", "Sell to wholesale"), 32, 656, 460, 84, () => session.Run(action), ButtonKind.Primary,
            Lang.En ? $"{Util.FormatNumber(bottles)} bottles · instantly +{Util.FormatEuro(revenue)}" : $"{Util.FormatNumber(bottles)} Flaschen · sofort +{Util.FormatEuro(revenue)}", session.Problem(action)));

        var direct = Js.Round(farm * RulesV2.DirectPriceFactor(state, player) * bottles * (1 - Economy.DirectSalesCostShare));
        card.With(Kit.Text(
            Lang.En ? $"For comparison: at the cellar door the same bottles would bring about {Util.FormatEuro(direct)} (after distribution costs) – but only around {Util.FormatNumber(Live.Demand(player))} bottles a month sell there." : $"Zum Vergleich: Ab Hof brächten dieselben Flaschen etwa {Util.FormatEuro(direct)} (nach Vertriebskosten) – dort gehen aber nur rund {Util.FormatNumber(Live.Demand(player))} Flaschen im Monat weg.",
            32, 760, 460, 76, 17, Theme.InkSoft, maxLines: 3));
        if (player.CompetitionEntries.Contains(lot.Id))
        {
            card.With(Kit.Text(Lang.T("Zur Prämierung angemeldet: Wird der Posten ganz verkauft, verfällt die Anmeldung.", "Entered for the wine award: if the lot is sold entirely, the entry lapses."), 32, 848, 460, 52, 17, Theme.Alert, bold: true, maxLines: 2));
        }
        else if (lot.Awards.Count > 0)
        {
            var last = lot.Awards[^1];
            card.With(Kit.Text(Lang.En ? $"Last awarded: {Economy.MedalNames[last.Medal]} – {Economy.Competitions[last.Competition].Name}, year {last.Year}" : $"Zuletzt prämiert: {Economy.MedalNames[last.Medal]} – {Economy.Competitions[last.Competition].Name}, Jahr {last.Year}", 32, 848, 460, 52, 17, Theme.InkSoft, maxLines: 2));
        }
        return card;
    }

    static SkiaControl WholesaleIntro(Player player)
    {
        var card = Inspector();
        card.With(Kit.Text(Lang.T("Großhandel", "Wholesale"), 32, 32, 460, 46, 32, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(Lang.T("Sofort Geld statt Warten", "Instant money instead of waiting"), 32, 78, 460, 28, 19, Theme.InkSoft));
        card.With(Kit.Text(
            Lang.En ? $"Wholesale buys inspected bottles in any quantity – for {Js.Round(Economy.WholesaleShare * 100)} % of the cellar-door price. Worth it when the stock overflows or the account is empty." : $"Der Großhandel kauft geprüfte Flaschen in jeder Menge – für {Js.Round(Economy.WholesaleShare * 100)} % des Preises ab Hof. Das lohnt sich, wenn das Lager überquillt oder das Konto leer ist.",
            32, 130, 460, 110, 19, Theme.Ink, maxLines: 4));
        card.With(Rule(256));
        card.With(SaturationBlock(player, 274));
        card.With(Rule(430));
        card.With(Kit.Row(Lang.T("Nachfrage ab Hof", "Cellar-door demand"), Lang.En ? $"{Util.FormatNumber(Live.Demand(player))} btl. per month" : $"{Util.FormatNumber(Live.Demand(player))} Fl. im Monat", 32, 448, 460, size: 19));
        return card;
    }
}
