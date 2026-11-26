using Winzer.Engine.Data;

namespace Winzer.Engine;

/// <summary>Exportverträge (exports.ts)</summary>
public static class Exports
{
    static readonly string[] Countries = ["Japan", "die USA", "Norwegen", "Kanada", "China", "Großbritannien", "Schweden", "die Niederlande"];
    static readonly Dictionary<string, string> CountriesEn = new()
    {
        ["Japan"] = "Japan", ["die USA"] = "the USA", ["Norwegen"] = "Norway", ["Kanada"] = "Canada", ["China"] = "China",
        ["Großbritannien"] = "Great Britain", ["Schweden"] = "Sweden", ["die Niederlande"] = "the Netherlands",
    };

    /// <summary>Anzeigename eines gespeicherten (deutschen) Landes; Englisch: „the USA“. Gespeichert bleibt das deutsche Land.</summary>
    public static string CountryName(string country) => Lang.En && CountriesEn.TryGetValue(country, out var en) ? en : country;

    static readonly QualityLevel[] Levels = [QualityLevel.Qualitaetswein, QualityLevel.Kabinett, QualityLevel.Spaetlese];
    const int ContractMonths = 12;

    /// <summary>Flaschen, die für den Exportvertrag in Frage kommen: geprüft, fertig, Stufe und Güte passen</summary>
    public static List<BottleLot> ExportEligible(Player player, ExportOffer offer)
    {
        return player.Bottles
            .Where(l =>
                l.Registered &&
                !l.Sparkling &&
                l.MonthsUntilReady == 0 &&
                !player.CompetitionEntries.Contains(l.Id) &&
                Rules.LevelRank(l.Level) >= Rules.LevelRank(offer.MinLevel) &&
                l.Quality >= offer.MinQuality)
            .OrderBy(l => l.Quality)
            .ToList();
    }

    /// <summary>Monatsende: Bekannte Weingüter bekommen gelegentlich eine Exportanfrage, die einen Monat gilt.</summary>
    public static void MaybeOfferExport(GameState state, Player player)
    {
        player.ExportOffer = null;
        if (player.ExportContract is not null || player.Reputation < Economy.ExportMinReputation) return;
        if (Rng.NextFloat(state.Rng) >= Economy.ExportOfferChance) return;
        T Pick<T>(T[] items) => items[(int)Math.Floor(Rng.NextFloat(state.Rng) * items.Length)];
        var minLevel = Pick(Levels);
        var minQuality = 60 + Math.Floor(Rng.NextFloat(state.Rng) * 5) * 5;
        var bottlesPerMonth = (3 + Math.Floor(Rng.NextFloat(state.Rng) * 13)) * 100;
        // Importeure zahlen deutlich mehr als der Großhandel, verlangen aber Liefertreue.
        var pricePerBottle =
            Js.Round(Quality.QualityInfo(minLevel).BottlePrice * (0.5 + minQuality / 100) * 0.75 * 100) / 100;
        var offer = new ExportOffer
        {
            Id = Util.NewId(state, "export"),
            Country = Pick(Countries),
            BottlesPerMonth = bottlesPerMonth,
            PricePerBottle = pricePerBottle,
            MinLevel = minLevel,
            MinQuality = minQuality,
            Months = ContractMonths,
        };
        player.ExportOffer = offer;
        Util.AddLog(
            state,
            player.Id,
            Lang.En ? $"Export request from {CountryName(offer.Country)}: {Util.JsNumber(bottlesPerMonth)} bottles per month for {Util.FormatEuro(pricePerBottle)}." : $"Exportanfrage aus {offer.Country}: {Util.JsNumber(bottlesPerMonth)} Flaschen pro Monat für {Util.FormatEuro(pricePerBottle)}.");
    }

    public static string? ValidateAnswerExport(Player player) =>
        player.ExportOffer is not null ? null : Lang.T("Es liegt keine Exportanfrage vor.", "There is no export request.");

    public static void AnswerExport(GameState state, Player player, bool accept)
    {
        var offer = player.ExportOffer!;
        player.ExportOffer = null;
        if (!accept)
        {
            Util.AddLog(state, player.Id, Lang.En ? $"Export request from {CountryName(offer.Country)} declined." : $"Exportanfrage aus {offer.Country} abgelehnt.");
            return;
        }
        player.ExportContract = new ExportContract
        {
            Id = offer.Id,
            Country = offer.Country,
            BottlesPerMonth = offer.BottlesPerMonth,
            PricePerBottle = offer.PricePerBottle,
            MinLevel = offer.MinLevel,
            MinQuality = offer.MinQuality,
            Months = offer.Months,
            MonthsLeft = offer.Months,
            MissedMonths = 0,
        };
        Util.AddLog(state, player.Id, Lang.En ? $"Export contract with {CountryName(offer.Country)} signed for {offer.Months} months." : $"Exportvertrag mit {offer.Country} über {offer.Months} Monate unterschrieben.");
    }

    /// <summary>Monatsende: Lieferung vor dem Hofverkauf. Fehlmengen kosten Vertragsstrafe.</summary>
    public static void DeliverExport(GameState state, Player player)
    {
        var contract = player.ExportContract;
        if (contract is null) return;
        var missing = contract.BottlesPerMonth;
        var revenue = 0.0;
        foreach (var lot in ExportEligible(player, contract))
        {
            if (missing <= 0) break;
            var count = Math.Min(missing, lot.Bottles);
            revenue += count * contract.PricePerBottle;
            missing -= count;
            Rules.RemoveBottles(player, lot, count);
        }
        player.Money += Js.Round(revenue);
        if (missing > 0)
        {
            var penalty = missing * Economy.ExportPenaltyPerBottle;
            player.Money -= penalty;
            contract.MissedMonths++;
            Util.AddLog(state, player.Id, Lang.En ? $"Export to {CountryName(contract.Country)}: {Util.JsNumber(missing)} bottles missing – {Util.FormatEuro(penalty)} contract penalty." : $"Export nach {contract.Country}: {Util.JsNumber(missing)} Flaschen fehlen – {Util.FormatEuro(penalty)} Vertragsstrafe.");
        }
        else
        {
            Util.AddLog(state, player.Id, Lang.En ? $"Export to {CountryName(contract.Country)}: {Util.JsNumber(contract.BottlesPerMonth)} bottles delivered (+{Util.FormatEuro(revenue)})." : $"Export nach {contract.Country}: {Util.JsNumber(contract.BottlesPerMonth)} Flaschen geliefert (+{Util.FormatEuro(revenue)}).");
        }

        if (--contract.MonthsLeft > 0) return;
        player.ExportContract = null;
        if (contract.MissedMonths == 0)
        {
            player.Points += Economy.ExportCompletionPoints;
            player.Stats.ExportsCompleted++;
            Rules.ChangeReputation(player, Economy.ExportCompletionReputation);
            Util.AddLog(state, null, Lang.En ? $"{player.Name} fully completed the export contract with {CountryName(contract.Country)}: +{Util.JsNumber(Economy.ExportCompletionPoints)} points." : $"{player.Name} hat den Exportvertrag mit {contract.Country} vollständig erfüllt: +{Util.JsNumber(Economy.ExportCompletionPoints)} Punkte.");
        }
        else
        {
            Util.AddLog(state, player.Id, Lang.En ? $"The export contract with {CountryName(contract.Country)} has expired ({contract.MissedMonths} months with shortfalls)." : $"Der Exportvertrag mit {contract.Country} ist ausgelaufen ({contract.MissedMonths} Monate mit Fehlmengen).");
        }
    }
}
