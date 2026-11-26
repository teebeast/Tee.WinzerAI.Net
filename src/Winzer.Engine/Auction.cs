using Winzer.Engine.Data;

namespace Winzer.Engine;

/// <summary>
/// Weinbergversteigerung als Live-Event: Alle 8–16 Monate wird eine Versteigerung angekündigt und im
/// Folgemonat abgehalten. Menschen bieten Runde für Runde mit; Computergegner und auswärtige Investoren
/// bieten verdeckt bis zu ihrem Höchstgebot. Wer bereit ist, mehr als alle anderen zu zahlen, bekommt den Zuschlag.
/// (auction.ts)
/// </summary>
public static class Auction
{
    public const string Investor = "investor";

    static readonly string[] ParcelNames =
    [
        "Sonnenberg", "Kirchberg", "Schlossberg", "Herrenberg", "Rosengarten", "Mühlberg", "Steinacker",
        "Goldtröpfchen", "Himmelreich", "Fuchsloch", "Altenberg", "Kapellenberg", "Paradies", "Hasensprung",
        "Bienengarten", "Felsenkopf", "Abtsberg", "Klosterpfad",
    ];

    static readonly double[] Sites = [-1, 0, 0, 1, 1, 2];

    static readonly Dictionary<double, string> SiteLabelsDe = new()
    {
        [-1] = "einfache Lage",
        [0] = "gute Lage",
        [1] = "sehr gute Lage",
        [2] = "Spitzenlage",
    };

    static readonly Dictionary<double, string> SiteLabelsEn = new()
    {
        [-1] = "basic site",
        [0] = "good site",
        [1] = "very good site",
        [2] = "prime site",
    };

    public static Dictionary<double, string> SiteLabels => Lang.En ? SiteLabelsEn : SiteLabelsDe;

    /// <summary>Zieht ein Element; bei leerer Liste wird der Zufallsaufruf trotzdem verbraucht (wie im Original, dort <c>undefined</c>).</summary>
    static T? Pick<T>(Rng rng, IReadOnlyList<T> items)
    {
        var index = (int)Math.Floor(Rng.NextFloat(rng) * items.Count);
        return index < items.Count ? items[index] : default;
    }

    static double RoundThousand(double value) => Js.Round(value / 1000) * 1000;

    /// <summary>Monate bis zur nächsten Ankündigung – die Versteigerung selbst folgt einen Monat später.</summary>
    public static void ScheduleNextAuction(GameState state)
    {
        var (min, max) = Economy.AuctionInterval;
        state.MonthsToAuction = (int)(min - 1 + Math.Floor(Rng.NextFloat(state.Rng) * (max - min + 1)));
    }

    /// <summary>Schätzwert: Boden plus Reben, je nach Lage teurer oder billiger</summary>
    public static double ParcelValue(double hectares, GrapeId? grape, int? plantedYear, double site, RegionId regionId, int year)
    {
        // Der Boden kostet je nach Gebiet unterschiedlich viel, die Reben überall gleich
        var perHa = Economy.LandValuePerHa * Regions.All[regionId].Character.LandPrice;
        if (grape is not null && plantedYear is not null)
        {
            perHa += Economy.VinesValuePerHa * (year - plantedYear.Value >= 3 ? 1 : 0.5);
        }
        return hectares * perHa * (1 + 0.15 * site);
    }

    /// <summary>Schätzwert eines Loses</summary>
    public static double ParcelValue(AuctionLot parcel, int year) =>
        ParcelValue(parcel.Hectares, parcel.Grape, parcel.PlantedYear, parcel.Site, parcel.RegionId, year);

    /// <summary>Schätzwert einer Parzelle – entspricht <c>parcelValue({ ...plot, regionId }, year)</c></summary>
    public static double ParcelValue(Plot plot, RegionId regionId, int year) =>
        ParcelValue(plot.Hectares, plot.Grape, plot.PlantedYear, plot.Site, regionId, year);

    /// <summary>Kündigt für den Folgemonat 1–3 Weinberge je Anbaugebiet an, in dem noch jemand wirtschaftet.</summary>
    public static void AnnounceAuction(GameState state)
    {
        var regions = state.Players.Where(p => !p.Bankrupt).Select(p => p.RegionId).Distinct().ToList();
        var year = state.Month == 12 ? state.Year + 1 : state.Year;
        var month = state.Month % 12 + 1;
        var lots = new List<AuctionLot>();
        foreach (var regionId in regions)
        {
            var roll = Rng.NextFloat(state.Rng);
            var count = roll < 0.4 ? 1 : roll < 0.8 ? 2 : 3;
            for (var i = 0; i < count; i++) lots.Add(MakeLot(state, regionId, lots));
        }
        // Erweiterte Regeln: Von den Weingütern eingelieferte Parzellen kommen als eigene Lose dazu
        foreach (var seller in state.Players.Where(p => !p.Bankrupt).ToList())
        {
            foreach (var plot in seller.Plots.Where(p => p.ForAuction)) lots.Add(MakeOfferedLot(state, seller, plot, lots));
        }
        state.Auction = new AuctionState { Phase = AuctionPhase.Announced, Year = year, Month = month, Lots = lots };
        Util.AddLog(
            state,
            null,
            (Lang.En ? $"📣 Vineyard auction in {Game.MonthNames[month - 1]}: {lots.Count} sites go under the hammer. Bid live – the highest bidder wins." : $"📣 Weinbergversteigerung im {Game.MonthNames[month - 1]}: {lots.Count} Lagen kommen unter den Hammer. Live bieten – wer am meisten zahlt, bekommt den Zuschlag."));
    }

    /// <summary>Ausrufpreis, wenn man diese Parzelle selbst zur Versteigerung gibt</summary>
    public static double OfferStartPrice(Player player, Plot plot, int year) =>
        RoundThousand(RoundThousand(ParcelValue(plot, player.RegionId, year)) * Economy.PlotOfferStartShare);

    /// <summary>Los aus einer eingelieferten Parzelle: Ausruf bei 80 % des Schätzwerts, der Einlieferer bietet nicht mit.</summary>
    static AuctionLot MakeOfferedLot(GameState state, Player seller, Plot plot, List<AuctionLot> taken)
    {
        var rng = state.Rng;
        var estimate = RoundThousand(ParcelValue(plot, seller.RegionId, state.Year));
        var name = plot.Name;
        for (var n = 2; taken.Any(l => l.Name == name); n++) name = $"{plot.Name} {n}";
        var limits = new Dictionary<string, double>();
        if (Rng.NextFloat(rng) < Economy.AuctionInvestorChance)
        {
            limits[Investor] = RoundThousand(
                estimate * Rng.NextRange(rng, Economy.AuctionInvestorRange.Min, Economy.AuctionInvestorRange.Max));
        }
        return new AuctionLot
        {
            Id = Util.NewId(state, "lot"),
            RegionId = seller.RegionId,
            Name = name,
            Hectares = plot.Hectares,
            Grape = plot.Grape,
            PlantedYear = plot.PlantedYear,
            Site = plot.Site,
            Estimate = estimate,
            StartPrice = OfferStartPrice(seller, plot, state.Year),
            Increment = Math.Max(1000, RoundThousand(estimate * Economy.AuctionIncrementShare)),
            Status = LotStatus.Pending,
            Price = 0,
            Leader = null,
            HumansIn = [],
            Awaiting = null,
            Limits = limits,
            Bids = [],
            SellerId = seller.Id,
            PlotId = plot.Id,
        };
    }

    static AuctionLot MakeLot(GameState state, RegionId regionId, List<AuctionLot> taken)
    {
        var rng = state.Rng;
        double hectares = 1 + Math.Floor(Rng.NextFloat(rng) * 5);
        var site = Pick(rng, Sites);
        GrapeId? grape = null;
        int? plantedYear = null;
        if (Rng.NextFloat(rng) < 0.6)
        {
            var suitable = Grapes.All.Keys.Where(g => Regions.All[regionId].Suitability[g] >= 0).ToList();
            grape = Pick(rng, suitable);
            plantedYear = state.Year - 3 - (int)Math.Floor(Rng.NextFloat(rng) * 25);
        }
        var estimate = RoundThousand(ParcelValue(hectares, grape, plantedYear, site, regionId, state.Year));
        var usedNames = new HashSet<string>(taken.Select(p => p.Name));
        // Sind alle Namen vergeben, liefert das Original undefined
        var name = Pick(rng, ParcelNames.Where(n => !usedNames.Contains(n)).ToList()) ?? "undefined";
        var limits = new Dictionary<string, double>();
        if (Rng.NextFloat(rng) < Economy.AuctionInvestorChance)
        {
            limits[Investor] = RoundThousand(
                estimate * Rng.NextRange(rng, Economy.AuctionInvestorRange.Min, Economy.AuctionInvestorRange.Max));
        }
        return new AuctionLot
        {
            Id = Util.NewId(state, "lot"),
            RegionId = regionId,
            Name = name,
            Hectares = hectares,
            Grape = grape,
            PlantedYear = plantedYear,
            Site = site,
            Estimate = estimate,
            StartPrice = RoundThousand(estimate * Economy.AuctionStartShare),
            Increment = Math.Max(1000, RoundThousand(estimate * Economy.AuctionIncrementShare)),
            Status = LotStatus.Pending,
            Price = 0,
            Leader = null,
            HumansIn = [],
            Awaiting = null,
            Limits = limits,
            Bids = [],
        };
    }

    /// <summary>Höchstes Gebot, das ein Spieler finanzieren kann: Konto plus freier Kreditrahmen</summary>
    public static double MaxAffordableBid(Player player) => Math.Max(0, player.Money) + Market.AvailableCredit(player);

    /// <summary>Name des Bieters; <paramref name="inSentence"/> schreibt die Investoren klein („… an auswärtige Investoren“)</summary>
    public static string BidderName(GameState state, string bidder, bool inSentence = false)
    {
        if (bidder == Investor) return inSentence ? Lang.T("auswärtige Investoren", "outside investors") : Lang.T("Auswärtige Investoren", "Outside investors");
        return state.Players.Find(p => p.Id == bidder)?.Name ?? "?";
    }

    /// <summary>Das nächste zulässige Gebot</summary>
    public static double NextMinimum(AuctionLot lot) =>
        !string.IsNullOrEmpty(lot.Leader) ? lot.Price + lot.Increment : lot.StartPrice;

    public static AuctionLot? FindLot(GameState state, string lotId) => state.Auction?.Lots.Find(l => l.Id == lotId);

    /// <summary>Das Los, bei dem gerade ein Mensch am Zug ist – Grundlage für die Live-Ansicht</summary>
    public static AuctionLot? AwaitingLot(GameState state)
    {
        if (state.Auction?.Phase != AuctionPhase.Live) return null;
        return state.Auction.Lots.Find(l => l.Status == LotStatus.Open && l.Awaiting is not null);
    }

    /// <summary>Computergegner hinterlegen im Ankündigungsmonat ihr Höchstgebot; 0 zieht es zurück.</summary>
    public static string? ValidateAuctionLimit(GameState state, Player player, string lotId, double amount)
    {
        var lot = FindLot(state, lotId);
        if (lot is null || lot.Status != LotStatus.Pending) return Lang.T("Dieser Weinberg wird nicht (mehr) versteigert.", "This vineyard is no longer up for auction.");
        if (player.Ai is null) return Lang.T("Menschen bieten live bei der Versteigerung mit.", "Humans bid live at the auction.");
        if (lot.RegionId != player.RegionId) return Lang.T("Der Weinberg liegt nicht in deinem Anbaugebiet.", "The vineyard is not in your wine region.");
        if (lot.SellerId == player.Id) return Lang.T("Auf den eigenen Weinberg bietet man nicht.", "You can't bid on your own vineyard.");
        if (!double.IsInteger(amount) || amount < 0) return Lang.T("Ungültiger Betrag.", "Invalid amount.");
        if (amount == 0) return null;
        if (amount < lot.StartPrice) return (Lang.En ? $"The starting price is {Util.FormatEuro(lot.StartPrice)}." : $"Der Ausrufpreis liegt bei {Util.FormatEuro(lot.StartPrice)}.");
        if (amount > MaxAffordableBid(player)) return Lang.T("Das Gebot ist nicht finanzierbar.", "You can't afford this bid.");
        return null;
    }

    public static void SetAuctionLimit(GameState state, Player player, string lotId, double amount)
    {
        var lot = FindLot(state, lotId)!;
        if (amount == 0)
        {
            // Neues Wörterbuch statt Remove: nur so bleibt die Einfügereihenfolge bei späteren Einträgen wie im Original erhalten
            lot.Limits = new Dictionary<string, double>(lot.Limits.Where(entry => entry.Key != player.Id));
        }
        else lot.Limits[player.Id] = amount;
    }

    /// <summary>Live-Gebot eines Menschen; geprüft wird für den Bieter, der gerade an der Reihe ist.</summary>
    public static string? ValidateAuctionBid(GameState state, string lotId, double amount)
    {
        var lot = FindLot(state, lotId);
        if (lot is null || lot.Status != LotStatus.Open || string.IsNullOrEmpty(lot.Awaiting))
            return Lang.T("Bei diesem Weinberg kann gerade niemand bieten.", "Nobody can bid on this vineyard right now.");
        var bidder = state.Players.Find(p => p.Id == lot.Awaiting)!;
        if (!double.IsInteger(amount)) return Lang.T("Ungültiger Betrag.", "Invalid amount.");
        if (amount < NextMinimum(lot)) return (Lang.En ? $"The bid must be at least {Util.FormatEuro(NextMinimum(lot))}." : $"Das Gebot muss mindestens {Util.FormatEuro(NextMinimum(lot))} betragen.");
        if (amount > MaxAffordableBid(bidder))
        {
            return (Lang.En ? $"You can't afford this bid (at most {Util.FormatEuro(MaxAffordableBid(bidder))})." : $"Das Gebot ist nicht finanzierbar (höchstens {Util.FormatEuro(MaxAffordableBid(bidder))}).");
        }
        return null;
    }

    public static string? ValidateAuctionPass(GameState state, string lotId)
    {
        var lot = FindLot(state, lotId);
        if (lot is null || lot.Status != LotStatus.Open || string.IsNullOrEmpty(lot.Awaiting))
            return Lang.T("Bei diesem Weinberg kann gerade niemand aussteigen.", "Nobody can drop out of this vineyard right now.");
        return null;
    }

    public static void AuctionBid(GameState state, string lotId, double amount)
    {
        var lot = FindLot(state, lotId)!;
        lot.Price = amount;
        lot.Leader = lot.Awaiting;
        lot.Bids.Add(new Bid { Bidder = lot.Awaiting!, Amount = amount });
        RivalsRespond(state, lot);
        Advance(state, lot);
    }

    public static void AuctionPass(GameState state, string lotId)
    {
        var lot = FindLot(state, lotId)!;
        lot.HumansIn = lot.HumansIn.Where(id => id != lot.Awaiting).ToList();
        RivalsRespond(state, lot);
        Advance(state, lot);
    }

    /// <summary>Beginn des Versteigerungsmonats: In jedem Gebiet wird das erste Los aufgerufen.</summary>
    public static void StartLiveAuction(GameState state)
    {
        var auction = state.Auction!;
        auction.Phase = AuctionPhase.Live;
        foreach (var regionId in auction.Lots.Select(l => l.RegionId).Distinct().ToList()) OpenNextLot(state, regionId, true);
    }

    /// <summary>Ende des Versteigerungsmonats: Offene Lose werden ohne weitere Gebote der Menschen zugeschlagen.</summary>
    public static void FinishAuction(GameState state)
    {
        var auction = state.Auction!;
        foreach (var lot in auction.Lots.Where(l => l.Status == LotStatus.Open).ToList())
        {
            lot.HumansIn = [];
            lot.Awaiting = null;
            RivalsRespond(state, lot);
            CloseLot(state, lot);
        }
        foreach (var regionId in auction.Lots.Select(l => l.RegionId).Distinct().ToList()) OpenNextLot(state, regionId, false);
        state.Auction = null;
        ScheduleNextAuction(state);
    }

    static void OpenNextLot(GameState state, RegionId regionId, bool withHumans)
    {
        var lot = state.Auction!.Lots.Find(l => l.RegionId == regionId && l.Status == LotStatus.Pending);
        if (lot is null) return;
        lot.Status = LotStatus.Open;
        lot.HumansIn = withHumans
            ? state.Players.Where(p => p.Ai is null && !p.Bankrupt && p.RegionId == regionId && p.Id != lot.SellerId).Select(p => p.Id).ToList()
            : [];
        if (lot.HumansIn.Count > 0)
        {
            lot.Awaiting = lot.HumansIn[0];
            return;
        }
        // Ohne Menschen entscheiden die verdeckten Höchstgebote sofort.
        RivalsRespond(state, lot);
        CloseLot(state, lot);
        OpenNextLot(state, regionId, withHumans);
    }

    /// <summary>Höchstgebote der Computergegner und Investoren – gedeckelt durch das, was ein Computergegner gerade finanzieren kann</summary>
    static List<(string Bidder, double Limit)> RivalLimits(GameState state, AuctionLot lot)
    {
        var result = new List<(string Bidder, double Limit)>();
        foreach (var (bidder, limit) in lot.Limits)
        {
            double capped;
            if (bidder == Investor) capped = limit;
            else
            {
                var player = state.Players.Find(p => p.Id == bidder);
                capped = player is not null && !player.Bankrupt ? Math.Min(limit, MaxAffordableBid(player)) : 0;
            }
            if (capped > 0) result.Add((bidder, capped));
        }
        return result;
    }

    /// <summary>
    /// Computergegner und Investoren reagieren wie bei einer echten Versteigerung: Wer noch mag, überbietet
    /// um einen Schritt; bieten mehrere, steigt der Preis bis knapp über das zweithöchste Limit.
    /// </summary>
    static void RivalsRespond(GameState state, AuctionLot lot)
    {
        var minimum = NextMinimum(lot);
        var rivals = RivalLimits(state, lot)
            .Where(r => r.Bidder == lot.Leader ? r.Limit >= lot.Price : r.Limit >= minimum)
            .OrderByDescending(r => r.Limit)
            .ToList();
        var challengers = rivals.Where(r => r.Bidder != lot.Leader).ToList();
        if (challengers.Count == 0) return;

        var top = rivals[0];
        (string Bidder, double Limit)? second = rivals.Count > 1 ? rivals[1] : null;
        var price = second is not null ? Math.Min(top.Limit, second.Value.Limit + lot.Increment) : minimum;
        if (top.Bidder != lot.Leader) price = Math.Max(price, minimum);
        if (second is not null && second.Value.Bidder != lot.Leader && second.Value.Limit > lot.Price)
        {
            lot.Bids.Add(new Bid { Bidder = second.Value.Bidder, Amount = second.Value.Limit });
        }
        if (top.Bidder == lot.Leader && price == lot.Price) return;
        lot.Price = price;
        lot.Leader = top.Bidder;
        lot.Bids.Add(new Bid { Bidder = top.Bidder, Amount = price });
    }

    /// <summary>Wer muss als Nächstes entscheiden? Wenn niemand mehr, fällt der Hammer.</summary>
    static void Advance(GameState state, AuctionLot lot)
    {
        var next = lot.HumansIn.Find(id => id != lot.Leader);
        if (!string.IsNullOrEmpty(next))
        {
            lot.Awaiting = next;
            return;
        }
        lot.Awaiting = null;
        CloseLot(state, lot);
        OpenNextLot(state, lot.RegionId, true);
    }

    static void CloseLot(GameState state, AuctionLot lot)
    {
        var description =
            (Lang.En ? $"{lot.Name} ({Util.JsNumber(lot.Hectares)} ha {(lot.Grape is { } grapeEn ? Grapes.All[grapeEn].Name : "fallow")}, {Regions.All[lot.RegionId].Name})" : $"{lot.Name} ({Util.JsNumber(lot.Hectares)} ha {(lot.Grape is { } grape ? Grapes.All[grape].Name : "Brache")}, {Regions.All[lot.RegionId].Name})");
        lot.Awaiting = null;
        if (string.IsNullOrEmpty(lot.Leader))
        {
            lot.Status = LotStatus.Unsold;
            Util.AddLog(state, null, (Lang.En ? $"🔨 {description}: no bid – the vineyard stays unsold." : $"🔨 {description}: kein Gebot – der Weinberg bleibt unverkauft."));
            if (SellerPlot(state, lot) is var (owner, kept) && kept is not null)
            {
                kept.ForAuction = false;
                Util.AddLog(state, owner!.Id, (Lang.En ? $"No bidder found for {kept.Name} – the plot stays with you." : $"Für {kept.Name} fand sich kein Bieter – die Parzelle bleibt bei dir."), LogTone.Bad);
            }
            return;
        }
        lot.Status = LotStatus.Sold;
        var ratio = Js.Round(lot.Price / lot.Estimate * 100);
        Util.AddLog(
            state,
            null,
            (Lang.En ? $"🔨 Sold: {description} for {Util.FormatEuro(lot.Price)} ({Util.JsNumber(ratio)} % of the estimate) to {BidderName(state, lot.Leader, true)}." : $"🔨 Zuschlag: {description} geht für {Util.FormatEuro(lot.Price)} ({Util.JsNumber(ratio)} % des Schätzwerts) an {BidderName(state, lot.Leader, true)}."));
        // Eingelieferte Parzelle: Sie verlässt das Gut des Einlieferers, der Erlös geht an ihn
        if (SellerPlot(state, lot) is var (seller, sold) && seller is not null)
        {
            if (sold is not null) seller.Plots = seller.Plots.Where(p => p.Id != sold.Id).ToList();
            seller.Money += lot.Price;
            Util.AddLog(state, seller.Id,
                (Lang.En ? $"{lot.Name} auctioned: +{Util.FormatEuro(lot.Price)} ({Util.JsNumber(ratio)} % of the estimate)." : $"{lot.Name} versteigert: +{Util.FormatEuro(lot.Price)} ({Util.JsNumber(ratio)} % des Schätzwerts)."), ratio >= Economy.PlotSaleShare * 100 ? LogTone.Good : LogTone.Bad);
        }
        var winner = state.Players.Find(p => p.Id == lot.Leader);
        if (winner is not null) AwardParcel(state, winner, lot);
        // Wer mitgeboten und verloren hat, ist verstimmt
        foreach (var loser in state.Players.Where(p => p.Ai is not null && p.Id != lot.Leader && lot.Limits.ContainsKey(p.Id)).ToList())
        {
            if (winner is not null) Rivals.AdjustRelation(loser, winner.Id, -10);
        }
    }

    /// <summary>Einlieferer und seine Parzelle zu einem Los – beide <c>null</c> bei Losen aus dem Umland</summary>
    static (Player? Seller, Plot? Plot) SellerPlot(GameState state, AuctionLot lot)
    {
        if (lot.SellerId is null) return (null, null);
        var seller = state.Players.Find(p => p.Id == lot.SellerId);
        return (seller, seller?.Plots.Find(p => p.Id == lot.PlotId));
    }

    static void AwardParcel(GameState state, Player player, AuctionLot lot)
    {
        player.Stats.AuctionsWon++;
        var taken = new HashSet<string>(player.Plots.Select(p => p.Name));
        var name = lot.Name;
        for (var n = 2; taken.Contains(name); n++) name = $"{lot.Name} {n}";
        player.Plots.Add(new Plot
        {
            Id = Util.NewId(state, "plot"),
            Name = name,
            Hectares = lot.Hectares,
            Grape = lot.Grape,
            PlantedYear = lot.PlantedYear,
            Site = lot.Site,
            Oechsle = 0,
            Health = 1,
            CropFactor = 1,
            HarvestedThisYear = false,
            Botrytis = false,
            IceWineGamble = false,
        });

        // Fehlbetrag automatisch über den Kredit decken – der neue Weinberg erhöht bereits den Rahmen.
        player.Money -= lot.Price;
        if (player.Money < 0)
        {
            var loan = Math.Min(-player.Money, Market.AvailableCredit(player));
            player.Loan += loan;
            player.Money += loan;
            if (loan > 0) Util.AddLog(state, player.Id, (Lang.En ? $"A loan of {Util.FormatEuro(loan)} was taken out for the purchase." : $"Für den Kauf wurden {Util.FormatEuro(loan)} Kredit aufgenommen."));
        }
    }
}
