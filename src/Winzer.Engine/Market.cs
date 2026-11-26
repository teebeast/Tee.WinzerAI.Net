using Winzer.Engine.Data;

namespace Winzer.Engine;

public sealed record MonthlyCosts(double Salaries, double Upkeep, double Protection, double Interest, double Total);

/// <summary>Preise, Nachfrage, Betriebswert und laufende Kosten (market.ts).</summary>
public static class Market
{
    /// <summary>Auf- oder Abschlag auf Verkaufserlöse durch Schwierigkeitsgrad und laufende Ereignisse</summary>
    public static double PriceFactor(Player player) =>
        (player.Difficulty is { } difficulty ? Economy.Difficulties[difficulty].PriceFactor : 1) * Rules.EffectValue(player, EffectKind.Price);

    /// <summary>Auf- oder Abschlag auf die laufenden Kosten durch Schwierigkeitsgrad und laufende Ereignisse</summary>
    public static double CostFactor(Player player) =>
        (player.Difficulty is { } difficulty ? Economy.Difficulties[difficulty].CostFactor : 1) * Rules.EffectValue(player, EffectKind.Costs);

    /// <summary>Fasswein-Erlös je hl – in einer Krise der Großkellereien nur die Hälfte</summary>
    public static double BulkPricePerHl(WineBatch batch, GameState state, Player player)
    {
        var crisis = state.BulkCrisisMonths > 0 ? 0.5 : 1;
        return Quality.QualityInfo(batch.Level).BulkPricePerLiter * 100 * (0.7 + batch.Quality / 200) * crisis * PriceFactor(player) * Trends.TrendFactor(state, batch);
    }

    /// <summary>Trägt das Weingut ein Bio-Siegel?</summary>
    public static bool IsBioCertified(Player player) => player.BioLabel is not null;

    /// <summary>
    /// Ab-Hof-Preis je Flasche. Ungeprüfte Weine dürfen nur als „Deutscher Wein“ verkauft werden,
    /// ungeprüfter Sekt nur als einfacher Schaumwein zum halben Preis.
    /// </summary>
    public static double BottlePrice(BottleLot lot, Player player, GameState state)
    {
        double basePrice;
        if (lot.Sparkling) basePrice = Economy.SektBottlePrice * (lot.Registered ? 1 : 0.5);
        else basePrice = Quality.QualityInfo(lot.Registered ? lot.Level : QualityLevel.DeutscherWein).BottlePrice;
        var bio = 1 + (BioData.BioInfo(player)?.PriceBonus ?? 0);
        var prestige = 1 + Regions.All[player.RegionId].Character.Prestige * Economy.PrestigePricePerLevel;
        // Rosé geht im Sommer besonders gut
        var roseSeason = lot.Rose && !lot.Sparkling && Economy.RoseSummerMonths.Contains(state.Month) ? 1 + Economy.RoseSummerBonus : 1;
        var piwi = Grapes.All[lot.Grape].Piwi ? Economy.PiwiPriceFactor : 1;
        var price = basePrice * (0.5 + lot.Quality / 100) * (0.8 + player.Reputation / 250) * bio * prestige * piwi * PriceFactor(player) * Trends.TrendFactor(state, lot) * roseSeason;
        return Js.Round(price * 100) / 100;
    }

    /// <summary>Je mehr Flaschen ein Weingut im Jahr an den Großhandel drückt, desto weniger zahlt er.</summary>
    public static double WholesaleSaturation(Player player) =>
        Math.Max(Economy.WholesaleMinFactor, 1 - player.WholesaleThisYear / Economy.WholesaleSaturationBottles);

    public static double WholesalePrice(BottleLot lot, Player player, GameState state) =>
        Js.Round(BottlePrice(lot, player, state) * Economy.WholesaleShare * WholesaleSaturation(player) * 100) / 100;

    /// <summary>Siegpunkte für die amtliche Prüfung</summary>
    public static double RegistrationPoints(BottleLot lot)
    {
        if (lot.Sparkling) return lot.Quality >= Economy.SektHighQuality ? Economy.SektPointsHigh : Economy.SektPointsLow;
        return Quality.QualityInfo(lot.Level).ApPoints;
    }

    /// <summary>Flaschen, die Hofladen und Online-Shop pro Monat absetzen können</summary>
    public static double MonthlyDemand(Player player)
    {
        var demand = Economy.FarmShopBaseDemand + player.Reputation * Economy.FarmShopDemandPerReputation;
        if (player.HasOnlineShop) demand += player.Reputation * Economy.OnlineDemandPerReputation;
        if (player.HasVinothek) demand *= Economy.VinothekDemandFactor;
        demand *= 1 + (BioData.BioInfo(player)?.DemandBonus ?? 0);
        demand *= 1 + Regions.All[player.RegionId].Character.Tourism * Economy.TourismDemandPerLevel;
        return Js.Round(demand * Rules.EffectValue(player, EffectKind.Demand));
    }

    public static double HlToBottles(double hectoliters) => Js.Round(hectoliters * Economy.BottlesPerHl);

    public static double TotalHectares(Player player)
    {
        var sum = 0.0;
        foreach (var p in player.Plots) sum += p.Hectares;
        return sum;
    }

    public static double PlantedHectares(Player player)
    {
        var sum = 0.0;
        foreach (var p in player.Plots) if (p.Grape is not null) sum += p.Hectares;
        return sum;
    }

    public static double AssetValue(Player player) =>
        TotalHectares(player) * Economy.LandValuePerHa * Regions.All[player.RegionId].Character.LandPrice +
        PlantedHectares(player) * Economy.VinesValuePerHa +
        player.TankCapacityHl * Economy.TankValuePerHl +
        player.BarriqueCapacityHl * Economy.BarriqueValuePerHl +
        (player.HasVinothek ? Economy.VinothekCost / 2 : 0);

    public static double CreditLimit(Player player) => Math.Floor((AssetValue(player) * Economy.LoanToValue) / 1000) * 1000;

    public static double AvailableCredit(Player player) => Math.Max(0, CreditLimit(player) - player.Loan);

    /// <summary>Die nächste Prämierung, für die man im aktuellen Monat einreichen kann</summary>
    public static CompetitionInfo UpcomingCompetition(int month)
    {
        if (month <= Economy.Competitions[CompetitionId.Regional].Month || month > Economy.Competitions[CompetitionId.Bund].Month)
            return Economy.Competitions[CompetitionId.Regional];
        return Economy.Competitions[CompetitionId.Bund];
    }

    /// <summary>Warum ein Wein nicht zur Prämierung angemeldet werden kann – null, wenn es geht</summary>
    public static string? CompetitionEntryProblem(Player player, BottleLot lot, CompetitionInfo competition)
    {
        if (!lot.Registered) return Lang.T("Nur amtlich geprüfte Weine dürfen teilnehmen.", "Only officially tested wines may take part.");
        if (player.CompetitionEntries.Contains(lot.Id)) return Lang.T("Der Wein ist bereits angemeldet.", "The wine is already entered.");
        if (lot.Awards.Any(a => a.Competition == competition.Id)) return Lang.T("Der Wein wurde hier schon prämiert.", "The wine has already been awarded here.");
        if (player.CompetitionEntries.Count >= competition.MaxEntries) return Lang.En ? $"At most {competition.MaxEntries} wines per awards." : $"Höchstens {competition.MaxEntries} Weine pro Prämierung.";
        if (player.Money < competition.Fee) return Lang.T("Nicht genug Geld für die Teilnahmegebühr.", "Not enough money for the entry fee.");
        return null;
    }

    public static double CompetitionBaseScore(BottleLot lot) =>
        lot.Quality + (lot.Sparkling ? Economy.SektCompetitionBonus : Quality.QualityInfo(lot.Level).CompetitionBonus);

    /// <summary>Personal in Vollzeitkräften: eine je 10 ha, mindestens eine – dazwischen anteilig (Teilzeit)</summary>
    public static double StaffFte(Player player) => Math.Max(1, TotalHectares(player) / Economy.HectaresPerWorker);

    /// <summary>Laufende Kosten, die am Ende von <paramref name="month"/> abgebucht werden</summary>
    public static MonthlyCosts MonthlyCosts(Player player, int month)
    {
        // Steillagen brauchen viel mehr Handarbeit
        var slopeUpkeep = 1 + Regions.All[player.RegionId].Character.Slope * Economy.SlopeUpkeepFactor;
        var salaries =
            StaffFte(player) * Economy.WorkerSalary +
            (player.CellarMaster?.Salary ?? 0) +
            (player.HasVinothek ? Economy.VinothekMonthlyCost : 0);
        var upkeep = 0.0;
        foreach (var p in player.Plots)
            upkeep += p.Hectares * (p.Grape is not null ? Economy.VineyardCostPerHa * slopeUpkeep : Economy.UpkeepPerHa);
        var protection = Economy.ProtectionMonths.Contains(month)
            ? (PlantedHectares(player) * Economy.Protection[player.Protection].CostPerHa * (BioData.BioInfo(player)?.ProtectionCostFactor ?? 1)) /
              Economy.ProtectionMonths.Length
            : 0;
        var overdraft = player.Money < 0 ? -player.Money * Economy.OverdraftInterestRate : 0;
        var interest = (player.Loan * Economy.LoanInterestRate + overdraft) / 12;
        var factor = CostFactor(player);
        return new MonthlyCosts(
            Salaries: salaries * factor,
            Upkeep: upkeep * factor,
            Protection: protection * factor,
            Interest: interest,
            Total: Js.Round((salaries + upkeep + protection) * factor + interest));
    }

    /// <summary>Reinvermögen: Betriebswert plus Konto minus Kredit</summary>
    public static double NetWorth(Player player) => AssetValue(player) + player.Money - player.Loan;
}
