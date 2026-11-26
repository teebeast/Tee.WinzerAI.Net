using System.Globalization;
using Winzer.Engine.Data;

namespace Winzer.Engine;

/// <summary>
/// Kundenaufträge: Hotels, Restaurants, Fluglinien und Clubs bestellen einmalig eine größere Menge eines bestimmten
/// Weins bis zu einem Termin. Sie zahlen besser als der Hofladen und bringen Punkte – wer den Termin verpasst,
/// zahlt Vertragsstrafe.
/// </summary>
public static class Orders
{
    public const int MaxOrders = 3;
    const int MaxOffers = 3;
    const double OfferChance = 0.3;
    /// <summary>Monate, die eine Anfrage gilt (einschließlich des laufenden)</summary>
    const int OfferMonths = 2;
    public const double OrderPenaltyShare = 0.15;
    const double OrderPenaltyReputation = 4;

    enum Demand { Grape, Color, Any, Sekt }

    sealed record Customer(
        string Name, string Icon, Demand Demand, QualityLevel[] Levels, (double Min, double Max) Quality,
        /* Menge je tragendem Hektar */ (double Min, double Max) BottlesPerHa,
        /* Preisaufschlag gegenüber dem Listenpreis */ (double Min, double Max) Premium,
        BioLabel? Bio = null);

    /// <summary>Frei erfundene Kunden</summary>
    static readonly Customer[] Customers =
    [
        new("Grandhotel Sonnenkamm", "🏨", Demand.Grape, [QualityLevel.Kabinett, QualityLevel.Spaetlese], (60, 75), (60, 120), (1.0, 1.2)),
        new("Sternerestaurant Lindgrün", "🍽️", Demand.Grape, [QualityLevel.Spaetlese, QualityLevel.Auslese], (72, 85), (20, 45), (1.15, 1.4)),
        new("Fluglinie Aerovita", "✈️", Demand.Color, [QualityLevel.Qualitaetswein, QualityLevel.Kabinett], (50, 65), (120, 220), (0.85, 1.0)),
        new("Reederei Wellenblick", "🚢", Demand.Color, [QualityLevel.Qualitaetswein, QualityLevel.Kabinett, QualityLevel.Spaetlese], (55, 70), (90, 160), (0.9, 1.1)),
        new("Feinkostkette Gaumenfreund", "🛍️", Demand.Any, [QualityLevel.Kabinett, QualityLevel.Spaetlese], (60, 72), (80, 140), (0.95, 1.15)),
        new("Hochzeitsagentur Glücksglas", "💍", Demand.Sekt, [QualityLevel.Qualitaetswein], (55, 70), (20, 50), (1.05, 1.3)),
        new("Weinclub Korkenzieher", "🌿", Demand.Any, [QualityLevel.Kabinett, QualityLevel.Spaetlese], (62, 75), (30, 70), (1.2, 1.45), BioLabel.Basis),
    ];

    static double RoundTo(double value, double step) => Js.Round(value / step) * step;

    /// <summary>Flaschen, die für den Auftrag passen – die einfachsten zuerst, damit die besten Weine für den Hofladen bleiben</summary>
    public static List<BottleLot> OrderEligible(Player player, CustomerOrder order)
    {
        if (order.BioLabel is { } wanted &&
            (player.BioLabel is not { } held || Array.IndexOf(BioData.BioOrder, held) < Array.IndexOf(BioData.BioOrder, wanted)))
            return [];
        return player.Bottles
            .Where(l =>
                l.MonthsUntilReady == 0 &&
                !l.Rose && // Kunden bestellen Weiß-, Rot- oder Sekt – Rosé ist kein Ersatz
                !player.CompetitionEntries.Contains(l.Id) &&
                l.Sparkling == order.Sparkling &&
                (l.Registered || (!order.Sparkling && order.MinLevel == QualityLevel.DeutscherWein)) &&
                (order.Grape is null || l.Grape == order.Grape) &&
                (order.Color is null || Grapes.All[l.Grape].Color == order.Color) &&
                Rules.LevelRank(l.Level) >= Rules.LevelRank(order.MinLevel) &&
                l.Quality >= order.MinQuality)
            .OrderBy(l => l.Quality)
            .ToList();
    }

    public static double EligibleBottles(Player player, CustomerOrder order)
    {
        var sum = 0.0;
        foreach (var l in OrderEligible(player, order)) sum += l.Bottles;
        return sum;
    }

    /// <summary>Kurzbeschreibung des verlangten Weins, z. B. „Riesling Spätlese, Güte ab 70“</summary>
    public static string DescribeWine(CustomerOrder order)
    {
        if (Lang.En)
        {
            var whatEn = order.Sparkling
                ? "sparkling wine"
                : $"{(order.Grape is { } g ? Grapes.All[g].Name : order.Color == GrapeColor.Rot ? "red wine" : order.Color == GrapeColor.Weiss ? "white wine" : "wine")}, at least {Quality.QualityInfo(order.MinLevel).Name}";
            return $"{whatEn}, quality {Util.JsNumber(order.MinQuality)}+";
        }
        var what = order.Sparkling
            ? "Sekt"
            : $"{(order.Grape is { } grape ? Grapes.All[grape].Name : order.Color == GrapeColor.Rot ? "Rotwein" : order.Color == GrapeColor.Weiss ? "Weißwein" : "Wein")}, mindestens {Quality.QualityInfo(order.MinLevel).Name}";
        return $"{what}, Güte ab {Util.JsNumber(order.MinQuality)}";
    }

    public static double OrderValue(CustomerOrder order) => Js.Round(order.Bottles * order.PricePerBottle);

    /// <summary>Monatsende: gelegentlich eine neue Anfrage, ab dem zweiten Jahr</summary>
    public static void MaybeOfferOrder(GameState state, Player player)
    {
        if (state.Year < 2 || player.OrderOffers.Count >= MaxOffers || Rng.NextFloat(state.Rng) >= OfferChance) return;
        double Roll() => Rng.NextFloat(state.Rng);
        double Between((double Min, double Max) range) => range.Min + Roll() * (range.Max - range.Min);
        T Pick<T>(IReadOnlyList<T> items) => items[(int)Math.Floor(Roll() * items.Count)];

        var customers = Customers.Where(c => c.Bio is null || player.Protection == ProtectionStrategy.Bio).ToList();
        var customer = Pick(customers);
        var planted = new List<GrapeId>();
        foreach (var p in player.Plots)
        {
            if (p.Grape is { } g && !planted.Contains(g)) planted.Add(g);
        }
        GrapeId? grape = null;
        GrapeColor? color = null;
        if (customer.Demand == Demand.Grape)
        {
            // Meist eine Sorte, die das Weingut anbaut – manchmal eine Anregung für die nächste Pflanzung
            grape = planted.Count > 0 && Roll() < 0.75 ? Pick(planted) : Pick(Grapes.All.Keys.ToList());
        }
        else if (customer.Demand == Demand.Color)
        {
            var colors = planted.Select(g => Grapes.All[g].Color).ToList();
            color = colors.Count > 0 && Roll() < 0.75 ? Pick(colors) : Pick<GrapeColor>([GrapeColor.Rot, GrapeColor.Weiss]);
        }
        var sparkling = customer.Demand == Demand.Sekt;
        var minLevel = Pick(customer.Levels);
        var minQuality = RoundTo(Between(customer.Quality), 5);
        var plantedHa = 0.0;
        foreach (var p in player.Plots)
        {
            if (p.Grape is not null) plantedHa += p.Hectares;
        }
        var bearingHa = Math.Max(4, plantedHa);
        var bottles = Math.Max(200, RoundTo(bearingHa * Between(customer.BottlesPerHa), 100));
        var listPrice = sparkling ? Economy.SektBottlePrice : Quality.QualityInfo(minLevel).BottlePrice;
        var pricePerBottle = Js.Round(listPrice * (0.5 + minQuality / 100) * Between(customer.Premium) * 100) / 100;
        var levelBonus = Array.FindIndex(Quality.QualityLevels, q => q.Level == minLevel);
        var now = Bio.GameMonth(state);
        var order = new CustomerOrder
        {
            Id = Util.NewId(state, "order"),
            Customer = customer.Name,
            Icon = customer.Icon,
            Grape = grape,
            Color = color,
            Sparkling = sparkling,
            MinLevel = minLevel,
            MinQuality = minQuality,
            BioLabel = customer.Bio,
            Bottles = bottles,
            PricePerBottle = pricePerBottle,
            Points = Math.Min(12, Math.Max(2, Js.Round(bottles / 400 + levelBonus + (customer.Bio is not null ? 2 : 0)))),
            Reputation = 2 + Math.Floor(Roll() * 3),
            // Der Termin liegt 2 bis 8 Monate nach Ablauf der Anfrage
            Deadline = now + OfferMonths + 2 + (int)Math.Floor(Roll() * 7),
            OfferExpires = now + OfferMonths,
        };
        player.OrderOffers.Add(order);
        Util.AddLog(
            state,
            player.Id,
            Lang.En ? $"{order.Icon} Order request from {order.Customer}: {Util.JsNumber(order.Bottles)} bottles {DescribeWine(order)} – see Orders." : $"{order.Icon} Auftragsanfrage von {order.Customer}: {Util.JsNumber(order.Bottles)} Flaschen {DescribeWine(order)} – siehe Aufträge.",
            LogTone.Good);
    }

    public static string? ValidateAccept(Player player, string orderId)
    {
        if (!player.OrderOffers.Any(o => o.Id == orderId)) return Lang.T("Diese Anfrage liegt nicht (mehr) vor.", "This request is no longer available.");
        if (player.Orders.Count >= MaxOrders) return Lang.En ? $"At most {MaxOrders} orders at the same time." : $"Höchstens {MaxOrders} Aufträge gleichzeitig.";
        return null;
    }

    public static void AcceptOrder(GameState state, Player player, string orderId)
    {
        var order = player.OrderOffers.First(o => o.Id == orderId);
        player.OrderOffers = player.OrderOffers.Where(o => o.Id != orderId).ToList();
        player.Orders.Add(order);
        Util.AddLog(state, player.Id, Lang.En ? $"Order from {order.Customer} accepted: {Util.JsNumber(order.Bottles)} bottles by {DeadlineText(order)}." : $"Auftrag von {order.Customer} angenommen: {Util.JsNumber(order.Bottles)} Flaschen bis {DeadlineText(order)}.");
    }

    public static string? ValidateDecline(Player player, string orderId) =>
        player.OrderOffers.Any(o => o.Id == orderId) ? null : Lang.T("Diese Anfrage liegt nicht (mehr) vor.", "This request is no longer available.");

    public static void DeclineOrder(GameState state, Player player, string orderId)
    {
        var order = player.OrderOffers.First(o => o.Id == orderId);
        player.OrderOffers = player.OrderOffers.Where(o => o.Id != orderId).ToList();
        Util.AddLog(state, player.Id, Lang.En ? $"Request from {order.Customer} declined." : $"Anfrage von {order.Customer} abgelehnt.");
    }

    public static string? ValidateDeliver(Player player, string orderId)
    {
        var order = player.Orders.FirstOrDefault(o => o.Id == orderId);
        if (order is null) return Lang.T("Dieser Auftrag liegt nicht vor.", "This order does not exist.");
        var available = EligibleBottles(player, order);
        if (order.BioLabel is not null && available == 0) return Lang.T("Der Kunde verlangt ein Bio-Siegel.", "The customer requires an organic seal.");
        if (available < order.Bottles) return Lang.En ? $"Only {Util.JsNumber(available)} of {Util.JsNumber(order.Bottles)} matching, tested bottles in stock." : $"Nur {Util.JsNumber(available)} von {Util.JsNumber(order.Bottles)} passenden, geprüften Flaschen im Lager.";
        return null;
    }

    public static void DeliverOrder(GameState state, Player player, string orderId)
    {
        var order = player.Orders.First(o => o.Id == orderId);
        var left = order.Bottles;
        foreach (var lot in OrderEligible(player, order))
        {
            if (left <= 0) break;
            var n = Math.Min(left, lot.Bottles);
            Rules.RemoveBottles(player, lot, n);
            left -= n;
        }
        var revenue = OrderValue(order);
        player.Money += revenue;
        player.Points += order.Points;
        player.Stats.OrdersDelivered++;
        player.Stats.BottlesSold += order.Bottles;
        var gain = Rules.ChangeReputation(player, order.Reputation);
        player.Orders = player.Orders.Where(o => o.Id != orderId).ToList();
        Util.AddLog(
            state,
            player.Id,
            Lang.En ? $"{order.Icon} {Util.JsNumber(order.Bottles)} bottles delivered to {order.Customer}: +{Util.FormatEuro(revenue)}, +{Util.JsNumber(order.Points)} points, reputation +{gain.ToString("F1", CultureInfo.InvariantCulture)}." : $"{order.Icon} {Util.JsNumber(order.Bottles)} Flaschen an {order.Customer} geliefert: +{Util.FormatEuro(revenue)}, +{Util.JsNumber(order.Points)} Punkte, Bekanntheit +{gain.ToString("F1", CultureInfo.InvariantCulture)}.",
            LogTone.Good);
    }

    /// <summary>Monatsende: abgelaufene Anfragen verschwinden, verpasste Termine kosten Vertragsstrafe.</summary>
    public static void ExpireOrders(GameState state, Player player)
    {
        var now = Bio.GameMonth(state);
        player.OrderOffers = player.OrderOffers.Where(o => o.OfferExpires > now).ToList();
        foreach (var order in player.Orders.Where(o => o.Deadline <= now).ToList())
        {
            var penalty = Js.Round(OrderValue(order) * OrderPenaltyShare);
            player.Money -= penalty;
            Rules.ChangeReputation(player, -OrderPenaltyReputation);
            Util.AddLog(
                state,
                player.Id,
                Lang.En ? $"{order.Customer} waits in vain for {Util.JsNumber(order.Bottles)} bottles: contract penalty {Util.FormatEuro(penalty)}, reputation −{Util.JsNumber(OrderPenaltyReputation)}." : $"{order.Customer} wartet vergeblich auf {Util.JsNumber(order.Bottles)} Flaschen: Vertragsstrafe {Util.FormatEuro(penalty)}, Bekanntheit −{Util.JsNumber(OrderPenaltyReputation)}.",
                LogTone.Bad);
        }
        player.Orders = player.Orders.Where(o => o.Deadline > now).ToList();
    }

    static readonly string[] Months = ["Januar", "Februar", "März", "April", "Mai", "Juni", "Juli", "August", "September", "Oktober", "November", "Dezember"];

    static readonly string[] MonthsEn = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

    /// <summary>„Oktober, Jahr 4“</summary>
    public static string MonthText(int month) =>
        Lang.En
            ? $"{MonthsEn[(month - 1) % 12]}, year {(int)Math.Floor((month - 1) / 12.0) + 1}"
            : $"{Months[(month - 1) % 12]}, Jahr {(int)Math.Floor((month - 1) / 12.0) + 1}";

    public static string DeadlineText(CustomerOrder order) => Lang.En ? $"end of {MonthText(order.Deadline)}" : $"Ende {MonthText(order.Deadline)}";
}
