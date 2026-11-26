using DrawnUi.Draw;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

// Reiter „Aufträge & Export“: Anfragen, angenommene Aufträge, Export und der Absatz ab Hof
public static partial class MarketScene
{
    const double SlotWidth = 390, SlotHeight = 260;

    static void BuildOrders(GameSession session, SkiaLayout scene)
    {
        var state = session.State;
        var player = session.Player;
        var now = Bio.GameMonth(state);
        var all = player.Orders.Concat(player.OrderOffers).ToList();
        var wanted = session.Nav.Selected("markt.order");
        var selected = all.FirstOrDefault(o => o.Id == wanted) ?? all.FirstOrDefault();

        scene.With(Kit.Text(Lang.T("Anfragen", "Requests"), 28, 86, 400, 34, 24, Theme.Gold, bold: true, serif: true));
        scene.With(Kit.Text(Lang.T("gelten zwei Monate – annehmen oder ablehnen", "valid for two months – accept or decline"), 430, 93, 800, 26, 17, Theme.CreamSoft, align: Align.End));
        scene.With(Kit.Text(Lang.En ? $"Accepted orders · {player.Orders.Count} / {Orders.MaxOrders}" : $"Angenommene Aufträge · {player.Orders.Count} / {Orders.MaxOrders}", 28, 396, 600, 34, 24, Theme.Gold, bold: true, serif: true));
        scene.With(Kit.Text(Lang.En ? $"Missed deadline: contractual penalty of {Js.Round(Orders.OrderPenaltyShare * 100)} % of the order value" : $"Verpasster Termin: {Js.Round(Orders.OrderPenaltyShare * 100)} % des Auftragswerts Vertragsstrafe", 530, 403, 700, 26, 17, Theme.CreamSoft, align: Align.End));

        for (var i = 0; i < 3; i++)
        {
            var x = 28 + i * (SlotWidth + 16);
            if (i < player.OrderOffers.Count) scene.With(OrderCard(session, player.OrderOffers[i], x, 124, false, now, selected));
            else
            {
                scene.With(EmptySlot(x, 124, SlotWidth, SlotHeight, i > 0 || player.OrderOffers.Count > 0 ? Lang.T("frei", "free")
                    : state.Year < 2 ? Lang.T("Ab dem zweiten Jahr fragen Kunden nach deinen Weinen.", "From the second year on, customers ask for your wines.")
                    : Lang.T("Gerade liegen keine Anfragen vor – neue kommen regelmäßig.", "No requests right now – new ones come in regularly.")));
            }
            if (i < player.Orders.Count) scene.With(OrderCard(session, player.Orders[i], x, 434, true, now, selected));
            else scene.With(EmptySlot(x, 434, SlotWidth, SlotHeight, i > 0 || player.Orders.Count > 0 ? Lang.T("frei", "free") : Lang.T("Keine laufenden Aufträge.", "No orders in progress.")));
        }

        scene.With(ExportPanel(session));
        scene.With(FarmGatePanel(session));
        scene.With(selected is null ? OrdersIntro(player) : OrderInspector(session, selected, player.Orders.Contains(selected), now));
    }

    static string WishText(CustomerOrder order)
    {
        var text = Lang.En ? $"{Util.FormatNumber(order.Bottles)} bottles of {Orders.DescribeWine(order)}" : $"{Util.FormatNumber(order.Bottles)} Flaschen {Orders.DescribeWine(order)}";
        if (order.BioLabel is { } label) text += Lang.En ? $" · with organic seal “{BioData.BioLabels[label].Name}” or higher" : $" · mit Bio-Siegel „{BioData.BioLabels[label].Name}“ oder höher";
        return text;
    }

    /// <summary>Ländername nach „aus“ und „mit“: „die USA“ → „den USA“, „die Niederlande“ → „den Niederlanden“</summary>
    static string Dative(string country)
    {
        if (!country.StartsWith("die ")) return country;
        var name = country[4..];
        return "den " + name + (name.EndsWith('e') ? "n" : "");
    }

    static (string Text, bool Urgent) TimeText(CustomerOrder order, int now)
    {
        var left = order.Deadline - now;
        return left <= 0 ? (Lang.T("Letzter Monat zum Liefern!", "Last month to deliver!"), true) : (Lang.En ? $"{left + 1} months left." : $"Noch {left + 1} Monate Zeit.", left <= 1);
    }

    static SkiaControl OrderCard(GameSession session, CustomerOrder order, double x, double y, bool accepted, int now, CustomerOrder? selected)
    {
        var player = session.Player;
        var available = Orders.EligibleBottles(player, order);
        var full = available >= order.Bottles;
        var card = Kit.Card(x, y, SlotWidth, SlotHeight, 16);
        var (time, urgent) = TimeText(order, now);
        if (accepted && urgent) card.With(Kit.Box(0, 0, 10, SlotHeight, Theme.Alert, 5));
        card.With(Kit.Text($"{order.Icon} {order.Customer}", 22, 14, SlotWidth - 44, 30, 20, Theme.Ink, bold: true));
        card.With(Chip(Lang.En ? $"+{Util.FormatNumber(order.Points)} points" : $"+{Util.FormatNumber(order.Points)} Punkte", SlotWidth - 126, 147, 104, Theme.PaperLine, Theme.Ink, 14));
        card.With(Kit.Text(WishText(order), 22, 50, SlotWidth - 44, 70, 17, Theme.Ink, maxLines: 3));
        card.With(Kit.Text(Lang.En ? $"{Price(order.PricePerBottle)}/btl. · total {Util.FormatEuro(Orders.OrderValue(order))}" : $"{Price(order.PricePerBottle)}/Fl. · zusammen {Util.FormatEuro(Orders.OrderValue(order))}", 22, 124, SlotWidth - 44, 26, 17, Theme.Wine, bold: true));
        card.With(Kit.Text(Lang.En ? $"Due {Orders.DeadlineText(order)}" : $"Liefern bis {Orders.DeadlineText(order)}", 22, 150, SlotWidth - 150, 26, 16, Theme.InkSoft));
        card.With(Meter(22, 184, SlotWidth - 44, available / order.Bottles, full ? Theme.Good : Theme.Wine));
        card.With(Kit.Text(Lang.En ? $"Matching in stock: {Util.FormatNumber(available)} / {Util.FormatNumber(order.Bottles)} btl." : $"Passend im Lager: {Util.FormatNumber(available)} / {Util.FormatNumber(order.Bottles)} Fl.", 22, 199, SlotWidth - 44, 24, 16, full ? Theme.Good : Theme.InkSoft, bold: full));
        card.With(accepted
            ? Kit.Text(time, 22, 226, SlotWidth - 44, 24, 16, urgent ? Theme.Alert : Theme.InkSoft, bold: urgent)
            : Kit.Text(Lang.En ? $"The request is valid until the end of {Orders.MonthText(order.OfferExpires)}." : $"Die Anfrage gilt bis Ende {Orders.MonthText(order.OfferExpires)}.", 22, 226, SlotWidth - 44, 24, 16, Theme.InkSoft));
        card.With(SelectionFrame(order == selected, SlotWidth, SlotHeight));
        var id = order.Id;
        card.Tap(() => session.Nav.Select("markt.order", id));
        return card;
    }

    static SkiaControl OrderInspector(GameSession session, CustomerOrder order, bool accepted, int now)
    {
        var player = session.Player;
        var value = Orders.OrderValue(order);
        var penalty = Js.Round(value * Orders.OrderPenaltyShare);
        var eligible = Orders.OrderEligible(player, order);
        var available = Orders.EligibleBottles(player, order);
        var full = available >= order.Bottles;
        var (time, urgent) = TimeText(order, now);

        var card = Inspector();
        card.With(Kit.Text(accepted ? Lang.T("ANGENOMMENER AUFTRAG", "ACCEPTED ORDER") : Lang.T("ANFRAGE", "REQUEST"), 32, 28, 460, 24, 16, Theme.Wine, bold: true));
        card.With(Kit.Text($"{order.Icon} {order.Customer}", 32, 52, 460, 44, 30, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(WishText(order), 32, 100, 460, 78, 19, Theme.Ink, maxLines: 3));

        var y = 190.0;
        void Row(string label, string text, Color? color = null)
        {
            card.With(Kit.Row(label, text, 32, y, 460, color, 19));
            y += 34;
        }
        Row(Lang.T("Menge", "Amount"), Lang.En ? $"{Util.FormatNumber(order.Bottles)} bottles" : $"{Util.FormatNumber(order.Bottles)} Flaschen");
        Row(Lang.T("Preis je Flasche", "Price per bottle"), Price(order.PricePerBottle));
        Row(Lang.T("Auftragswert", "Order value"), Util.FormatEuro(value), Theme.Wine);
        Row(Lang.T("Lohn", "Reward"), Lang.En ? $"+{Util.FormatNumber(order.Points)} points · +{Util.FormatNumber(order.Reputation)} reputation" : $"+{Util.FormatNumber(order.Points)} Punkte · +{Util.FormatNumber(order.Reputation)} Bekanntheit");
        Row(Lang.T("Liefertermin", "Delivery date"), Orders.DeadlineText(order));
        if (accepted) Row(Lang.T("Zeit", "Time"), time, urgent ? Theme.Alert : null);
        else Row(Lang.T("Anfrage gilt bis", "Request valid until"), Lang.En ? $"end of {Orders.MonthText(order.OfferExpires)}" : $"Ende {Orders.MonthText(order.OfferExpires)}");
        Row(Lang.T("Strafe bei Verzug", "Penalty if late"), Util.FormatEuro(penalty), Theme.Alert);

        card.With(Rule(436));
        card.With(Kit.Text(Lang.T("Passender Vorrat", "Matching stock"), 32, 450, 300, 30, 20, Theme.Ink, bold: true));
        card.With(Kit.Text($"{Util.FormatNumber(available)} / {Util.FormatNumber(order.Bottles)} {Lang.T("Fl.", "btl.")}", 232, 452, 260, 28, 19, full ? Theme.Good : Theme.Wine, bold: true, align: Align.End));
        card.With(Meter(32, 490, 460, available / order.Bottles, full ? Theme.Good : Theme.Wine, h: 12));
        var ly = 514.0;
        if (eligible.Count == 0)
        {
            var why = order.BioLabel is not null && player.BioLabel is null
                ? Lang.T("Der Kunde verlangt ein Bio-Siegel – das Weingut trägt keins.", "The customer demands an organic seal – the estate has none.")
                : Lang.T("Kein geprüfter, fertiger Wein im Lager erfüllt den Wunsch. Rosé zählt nicht als Ersatz.", "No inspected, finished wine in stock meets the request. Rosé is no substitute.");
            card.With(Kit.Text(why, 32, ly, 460, 50, 17, Theme.InkSoft, maxLines: 2));
        }
        foreach (var lot in eligible.Take(3))
        {
            card.With(Kit.Box(32, ly + 9, 10, 10, Theme.Wine, 5));
            card.With(Kit.Text(Lang.En ? $"{GrapeName(lot)} {KindName(lot)} (vintage {lot.Vintage})" : $"{GrapeName(lot)} {KindName(lot)} (Jg. {lot.Vintage})", 52, ly, 280, 26, 17, Theme.Ink));
            card.With(Kit.Text(Lang.En ? $"{Util.FormatNumber(lot.Bottles)} btl. · quality {Util.FormatNumber(lot.Quality)}" : $"{Util.FormatNumber(lot.Bottles)} Fl. · Güte {Util.FormatNumber(lot.Quality)}", 300, ly, 192, 26, 17, Theme.InkSoft, align: Align.End));
            ly += 30;
        }
        if (eligible.Count > 3) card.With(Kit.Text(Lang.En ? $"… and {eligible.Count - 3} more lots" : $"… und {eligible.Count - 3} weitere Posten", 52, ly, 440, 26, 16, Theme.InkSoft));
        card.With(Kit.Text(Lang.T("Geliefert werden die einfachsten passenden Weine zuerst – die besten bleiben für den Hofladen.", "The simplest matching wines are delivered first – the best stay for the farm shop."),
            32, 640, 460, 50, 16, Theme.InkSoft, maxLines: 2));

        var id = order.Id;
        if (accepted)
        {
            var deliver = new GameAction.DeliverOrder(id);
            card.With(Kit.Button(Lang.T("Jetzt liefern", "Deliver now"), 32, 832, 460, 84, () => session.Run(deliver), ButtonKind.Primary,
                Lang.En ? $"+{Util.FormatEuro(value)} · +{Util.FormatNumber(order.Points)} points · +{Util.FormatNumber(order.Reputation)} reputation" : $"+{Util.FormatEuro(value)} · +{Util.FormatNumber(order.Points)} Punkte · +{Util.FormatNumber(order.Reputation)} Bekanntheit", session.Problem(deliver)));
            card.With(Kit.Text(full ? Lang.T("Alles da – die Lieferung kann sofort hinaus.", "Everything is here – the delivery can go out right away.") : (Lang.En ? $"{Util.FormatNumber(order.Bottles - available)} matching bottles are still missing: bottle them and have them inspected." : $"Es fehlen noch {Util.FormatNumber(order.Bottles - available)} passende Flaschen: abfüllen und prüfen lassen."),
                32, 772, 460, 50, 17, full ? Theme.Good : Theme.InkSoft, bold: full, maxLines: 2));
        }
        else
        {
            var accept = new GameAction.AcceptOrder(id);
            var decline = new GameAction.DeclineOrder(id);
            card.With(Kit.Button(Lang.T("Annehmen", "Accept"), 32, 736, 460, 84, () => session.Run(accept), ButtonKind.Primary,
                Lang.En ? $"Deliver by {Orders.DeadlineText(order)} · otherwise {Util.FormatEuro(penalty)} penalty" : $"Liefern bis {Orders.DeadlineText(order)} · sonst {Util.FormatEuro(penalty)} Strafe", session.Problem(accept)));
            card.With(Kit.Button(Lang.T("Ablehnen", "Decline"), 32, 832, 460, 84, () => session.Run(decline), ButtonKind.Secondary,
                Lang.T("Die Anfrage verfällt ohne Folgen", "The request lapses without consequences"), session.Problem(decline)));
        }
        return card;
    }

    /// <summary>Inspektor ohne Auftrag: Worum es hier geht</summary>
    static SkiaControl OrdersIntro(Player player)
    {
        var card = Inspector();
        card.With(Kit.Text(Lang.T("Aufträge", "Orders"), 32, 32, 460, 46, 32, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(Lang.En ? $"{player.Orders.Count} / {Orders.MaxOrders} orders accepted" : $"{player.Orders.Count} / {Orders.MaxOrders} Aufträge angenommen", 32, 78, 460, 28, 19, Theme.InkSoft));
        card.With(Kit.Text(
            Lang.En ? $"Customers order larger quantities of a particular wine by a deadline – better paid than in the farm shop, plus victory points. Miss the deadline and you pay a contractual penalty of {Js.Round(Orders.OrderPenaltyShare * 100)} % of the order value." : $"Kunden bestellen größere Mengen eines bestimmten Weins bis zu einem Termin – besser bezahlt als im Hofladen, dazu Siegpunkte. Wer den Termin verpasst, zahlt {Js.Round(Orders.OrderPenaltyShare * 100)} % des Auftragswerts Vertragsstrafe.",
            32, 130, 460, 160, 19, Theme.Ink, maxLines: 6));
        card.With(Rule(306));
        card.With(Kit.Row(Lang.T("Bisher geliefert", "Delivered so far"), Lang.En ? $"{player.Stats.OrdersDelivered} {(player.Stats.OrdersDelivered == 1 ? "order" : "orders")}" : $"{player.Stats.OrdersDelivered} {(player.Stats.OrdersDelivered == 1 ? "Auftrag" : "Aufträge")}", 32, 324, 460, size: 19));
        card.With(Kit.Row(Lang.T("Exportverträge erfüllt", "Export contracts fulfilled"), player.Stats.ExportsCompleted.ToString(), 32, 358, 460, size: 19));
        card.With(Kit.Row(Lang.T("Flaschen verkauft", "Bottles sold"), Util.FormatNumber(player.Stats.BottlesSold), 32, 392, 460, size: 19));
        card.With(Rule(440));
        card.With(Kit.Text(Lang.T("Sobald eine Anfrage vorliegt, steht sie links. Antippen zeigt hier die Einzelheiten – annehmen, ablehnen oder liefern.", "As soon as a request comes in, it appears on the left. Tapping it shows the details here – accept, decline or deliver."),
            32, 458, 460, 110, 18, Theme.InkSoft, maxLines: 4));
        return card;
    }

    static double ExportStock(Player player, ExportOffer offer)
    {
        var sum = 0.0;
        foreach (var lot in Exports.ExportEligible(player, offer)) sum += lot.Bottles;
        return sum;
    }

    static SkiaControl ExportPanel(GameSession session)
    {
        var player = session.Player;
        var panel = Kit.Glass(28, 716, 790, 244);
        panel.With(Kit.Text(Lang.T("Export", "Export"), 24, 14, 300, 32, 22, Theme.Gold, bold: true, serif: true));
        var terms = player.ExportOffer ?? player.ExportContract;
        if (terms is null)
        {
            panel.With(Kit.Text(Lang.T("Keine Anfrage aus dem Ausland", "No request from abroad"), 24, 52, 740, 32, 21, Theme.Cream, bold: true));
            var known = player.Reputation >= Economy.ExportMinReputation;
            panel.With(Kit.Text(
                Lang.En
                    ? $"Importers occasionally contact estates with a reputation of {Util.FormatNumber(Economy.ExportMinReputation)} or more (currently {Util.FormatNumber(Js.Round(player.Reputation))}{(known ? "" : " – still too unknown")}). " +
                      $"A contract runs twelve months: a fixed quantity goes out automatically every month. Missing bottles cost a {Util.FormatEuro(Economy.ExportPenaltyPerBottle)} penalty per bottle; deliver in full and you get +{Util.FormatNumber(Economy.ExportCompletionPoints)} points."
                    : $"Importeure melden sich gelegentlich bei Weingütern ab Bekanntheit {Util.FormatNumber(Economy.ExportMinReputation)} (aktuell {Util.FormatNumber(Js.Round(player.Reputation))}{(known ? "" : " – noch zu unbekannt")}). " +
                      $"Ein Vertrag läuft zwölf Monate: Jeden Monat geht eine feste Menge automatisch hinaus. Fehlende Flaschen kosten {Util.FormatEuro(Economy.ExportPenaltyPerBottle)} Strafe je Flasche; wer vollständig liefert, bekommt +{Util.FormatNumber(Economy.ExportCompletionPoints)} Punkte.",
                24, 92, 742, 130, 18, Theme.CreamSoft, maxLines: 5));
            return panel;
        }

        var stock = ExportStock(player, terms);
        var enough = stock >= terms.BottlesPerMonth;
        var contract = player.ExportContract;
        panel.With(Kit.Text(Lang.En ? (contract is null ? $"Request from {Exports.CountryName(terms.Country)} · {terms.Months} months" : $"Contract with {Exports.CountryName(terms.Country)}") : (contract is null ? $"Anfrage aus {Dative(terms.Country)} · {terms.Months} Monate" : $"Vertrag mit {Dative(terms.Country)}"), 24, 48, 520, 32, 22, Theme.Cream, bold: true));
        panel.With(Kit.Text(Lang.En ? $"{Util.FormatNumber(terms.BottlesPerMonth)} bottles per month at {Price(terms.PricePerBottle)} · at least {Quality.QualityInfo(terms.MinLevel).Name}, quality from {Util.FormatNumber(terms.MinQuality)}" : $"{Util.FormatNumber(terms.BottlesPerMonth)} Flaschen pro Monat zu {Price(terms.PricePerBottle)} · mindestens {Quality.QualityInfo(terms.MinLevel).Name}, Güte ab {Util.FormatNumber(terms.MinQuality)}",
            24, 84, 520, 52, 18, Theme.Cream, maxLines: 2));
        panel.With(Kit.Text(Lang.En ? $"Matching stock: {Util.FormatNumber(stock)} btl. – {(enough ? $"lasts for {Util.FormatNumber(Math.Floor(stock / terms.BottlesPerMonth))} {(Math.Floor(stock / terms.BottlesPerMonth) == 1 ? "month" : "months")}" : "too little for a monthly delivery")}" : $"Passender Vorrat: {Util.FormatNumber(stock)} Fl. – {(enough ? $"reicht für {Util.FormatNumber(Math.Floor(stock / terms.BottlesPerMonth))} {(Math.Floor(stock / terms.BottlesPerMonth) == 1 ? "Monat" : "Monate")}" : "zu wenig für eine Monatslieferung")}",
            24, 138, 520, 26, 18, enough ? Theme.GoodLight : Theme.Warn, bold: true));

        if (contract is null)
        {
            panel.With(Kit.Text(
                Lang.En ? $"Delivery is automatic at the end of the month. Missing bottles cost a {Util.FormatEuro(Economy.ExportPenaltyPerBottle)} penalty per bottle; deliver in full for a whole year and you get +{Util.FormatNumber(Economy.ExportCompletionPoints)} points. The request is valid this month only." : $"Geliefert wird automatisch am Monatsende. Fehlende Flaschen kosten {Util.FormatEuro(Economy.ExportPenaltyPerBottle)} Strafe je Flasche; wer ein Jahr lang vollständig liefert, bekommt +{Util.FormatNumber(Economy.ExportCompletionPoints)} Punkte. Die Anfrage gilt nur diesen Monat.",
                24, 170, 742, 66, 16, Theme.CreamSoft, maxLines: 3));
            var accept = new GameAction.AnswerExport(true);
            var decline = new GameAction.AnswerExport(false);
            panel.With(Kit.Button(Lang.T("Annehmen", "Accept"), 566, 16, 200, 68, () => session.Run(accept), ButtonKind.Gold,
                Lang.En ? $"{Util.FormatEuro(Js.Round(terms.BottlesPerMonth * terms.PricePerBottle))} per month" : $"{Util.FormatEuro(Js.Round(terms.BottlesPerMonth * terms.PricePerBottle))} im Monat", session.Problem(accept), 20, Align.Center));
            panel.With(Kit.Button(Lang.T("Ablehnen", "Decline"), 566, 94, 200, 68, () => session.Run(decline), ButtonKind.Dark,
                Lang.T("ohne Folgen", "no consequences"), session.Problem(decline), 20, Align.Center));
        }
        else
        {
            var done = contract.Months - contract.MonthsLeft;
            panel.With(Kit.Paint(606, 20, 130, 130, (c, w, h) =>
                Draw.Ring(c, 65, 65, 52, 13, done / (double)Math.Max(1, contract.Months), Theme.SkCream.WithAlpha(50), Theme.SkGold)));
            panel.With(Kit.Text(contract.MonthsLeft.ToString(), 606, 52, 130, 44, 34, Theme.Cream, bold: true, serif: true, align: Align.Center));
            panel.With(Kit.Text(contract.MonthsLeft == 1 ? Lang.T("Monat noch", "month left") : Lang.T("Monate noch", "months left"), 586, 94, 170, 22, 14, Theme.CreamSoft, align: Align.Center));
            panel.With(Kit.Text(Lang.En ? $"brings {Util.FormatEuro(Js.Round(terms.BottlesPerMonth * terms.PricePerBottle))} per month" : $"bringt {Util.FormatEuro(Js.Round(terms.BottlesPerMonth * terms.PricePerBottle))} im Monat", 566, 150, 210, 24, 16, Theme.CreamSoft, align: Align.Center));
            panel.With(contract.MissedMonths > 0
                ? Kit.Text(Lang.En ? $"{contract.MissedMonths} {(contract.MissedMonths == 1 ? "month" : "months")} with shortfalls – the points bonus is lost. Missing bottles cost a {Util.FormatEuro(Economy.ExportPenaltyPerBottle)} penalty per bottle." : $"{contract.MissedMonths} {(contract.MissedMonths == 1 ? "Monat" : "Monate")} mit Fehlmengen – der Punktebonus ist verloren. Fehlende Flaschen kosten {Util.FormatEuro(Economy.ExportPenaltyPerBottle)} Strafe je Flasche.",
                    24, 176, 742, 50, 17, Theme.Warn, bold: true, maxLines: 2)
                : Kit.Text(Lang.En ? $"Delivery is automatic at the end of the month, before the farm-gate sales. Complete so far – +{Util.FormatNumber(Economy.ExportCompletionPoints)} points await at the end; missing bottles cost {Util.FormatEuro(Economy.ExportPenaltyPerBottle)} per bottle." : $"Geliefert wird automatisch am Monatsende, vor dem Hofverkauf. Bisher vollständig – am Ende winken +{Util.FormatNumber(Economy.ExportCompletionPoints)} Punkte; fehlende Flaschen kosten {Util.FormatEuro(Economy.ExportPenaltyPerBottle)} je Flasche.",
                    24, 176, 742, 50, 17, Theme.CreamSoft, maxLines: 2));
        }
        return panel;
    }

    /// <summary>
    /// Vorschau auf den Hofverkauf am Monatsende – dieselbe Auswahl wie beim Monatsabschluss: fertige Weine,
    /// die nicht auf die Prüfung warten und nicht zur Prämierung angemeldet sind, älteste zuerst.
    /// </summary>
    static (double Ready, double Sold, double Net) FarmGate(GameState state, Player player)
    {
        var demand = Live.Demand(player);
        double ready = 0, sold = 0, revenue = 0;
        foreach (var lot in player.Bottles)
        {
            var waits = !lot.Registered && (lot.Sparkling || lot.Level != QualityLevel.DeutscherWein) && lot.Bottles >= Economy.RegisterMinBottles;
            if (player.CompetitionEntries.Contains(lot.Id) || lot.MonthsUntilReady > 0 || waits) continue;
            ready += lot.Bottles;
            var count = Math.Min(Math.Max(0, demand - sold), lot.Bottles);
            sold += count;
            revenue += count * Market.BottlePrice(lot, player, state) * RulesV2.DirectPriceFactor(state, player);
        }
        return (ready, sold, Js.Round(revenue * (1 - Economy.DirectSalesCostShare)));
    }

    static SkiaControl FarmGatePanel(GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var panel = Kit.Glass(834, 716, 396, 244);
        var demand = Live.Demand(player);
        var (ready, sold, net) = FarmGate(state, player);
        var channels = Lang.T("Hofladen", "Farm shop") + (player.HasOnlineShop ? Lang.T(", Online-Shop", ", online shop") : "") + (player.HasVinothek ? Lang.T(", Vinothek", ", wine room") : "");
        panel.With(Kit.Text(Lang.T("Ab Hof diesen Monat", "Cellar door this month"), 24, 14, 348, 32, 22, Theme.Gold, bold: true, serif: true));
        panel.With(Kit.Text(Lang.En ? $"≈ {Util.FormatNumber(demand)} bottles" : $"≈ {Util.FormatNumber(demand)} Flaschen", 24, 48, 348, 44, 32, Theme.Cream, bold: true, serif: true));
        panel.With(Kit.Text(Lang.En ? $"Demand via {channels}" : $"Nachfrage über {channels}", 24, 92, 348, 24, 16, Theme.CreamSoft));
        panel.With(Meter(24, 128, 348, demand > 0 ? ready / demand : 0, ready >= demand ? Theme.GoodLight : Theme.Warn, Theme.GlassLine));
        panel.With(DarkRow(Lang.T("Verkaufsbereit", "Ready to sell"), $"{Util.FormatNumber(ready)} {Lang.T("Fl.", "btl.")}", 24, 146, 348, ready >= demand ? Theme.GoodLight : Theme.Warn, 17));
        panel.With(DarkRow(Lang.T("Voraussichtlich", "Expected"), sold > 0 ? $"{Util.FormatNumber(sold)} {Lang.T("Fl.", "btl.")} · +{Util.FormatEuro(net)}" : Lang.T("kein Verkauf", "no sales"), 24, 174, 348, size: 17));
        if (RulesV2.On(state))
        {
            var level = Js.Round(player.PriceLevel * 100);
            panel.With(Kit.Button(Lang.En ? $"Cellar-door prices: {(level == 0 ? "list price" : $"{(level > 0 ? "+" : "−")}{Math.Abs(level)} %")} ›" : $"Preise ab Hof: {(level == 0 ? "Listenpreis" : $"{(level > 0 ? "+" : "−")}{Math.Abs(level)} %")} ›", 24, 202, 348, 36,
                () => { session.Nav.Show(Overlay.Plan); session.Nav.SetOverlayTab(2); }, ButtonKind.Dark, fontSize: 16, align: Align.Center));
        }
        else panel.With(Kit.Text(Lang.T("Verkauf am Monatsende, nach Vertriebskosten.", "Sold at the end of the month, after distribution costs."), 24, 206, 348, 24, 15, Theme.CreamSoft));
        return panel;
    }
}
