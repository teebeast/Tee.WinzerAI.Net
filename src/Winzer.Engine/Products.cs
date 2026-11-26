using System.Globalization;
using Winzer.Engine.Data;

namespace Winzer.Engine;

/// <summary>
/// Saisonale Produkte: Federweißer (frischer, gärender Most im Herbst) und die Straußwirtschaft (Ausschank auf dem
/// eigenen Hof für vier Monate im Jahr). Rosé ist ein Merkmal des Weins und steckt in Lese, Keller und Preisen.
/// (products.ts)
/// </summary>
public static class Products
{
    /// <summary>Name des Effekts, an dem man eine geöffnete Straußwirtschaft erkennt</summary>
    public const string StraussEffectLabel = "Straußwirtschaft geöffnet";

    /// <summary>Federweißer aus roten Trauben heißt Roter Rauscher.</summary>
    public static string FederweisserName(WineInfo batch) =>
        Grapes.All[batch.Grape].Color == GrapeColor.Rot ? "Roter Rauscher" : Lang.T("Federweißer", "Federweisser");

    /// <summary>Hektoliter, die die Kundschaft in diesem Monat noch abnimmt</summary>
    public static double FederweisserCapacity(Player player)
    {
        var total = Js.Round(Economy.FederweisserBaseHl + player.Reputation * Economy.FederweisserHlPerReputation);
        return Math.Max(0, total - player.FederweisserThisMonth);
    }

    public static double FederweisserPricePerHl(Player player) => Economy.FederweisserPricePerL * 100 * Market.PriceFactor(player);

    /// <summary>Frischer Most des laufenden Jahrgangs, der noch gärt</summary>
    public static bool IsFreshMust(GameState state, WineBatch batch) =>
        batch.Vintage == state.Year && batch.MonthsUntilReady > 0 && !batch.Barrique;

    public static string? ValidateFederweisser(GameState state, Player player, string batchId, double hectoliters)
    {
        if (!Economy.FederweisserMonths.Contains(state.Month)) return Lang.T("Federweißer gibt es nur im September und Oktober.", "Federweisser is only available in September and October.");
        var batch = player.Cellar.Find(b => b.Id == batchId);
        if (batch is null) return Lang.T("Unbekannte Charge.", "Unknown batch.");
        if (!IsFreshMust(state, batch)) return Lang.T("Federweißer wird nur aus frischem Most des neuen Jahrgangs gemacht.", "Federweisser is only made from fresh must of the new vintage.");
        if (!(hectoliters > 0) || hectoliters > batch.Hectoliters) return Lang.T("Ungültige Menge.", "Invalid quantity.");
        var capacity = FederweisserCapacity(player);
        if (capacity <= 0) return Lang.T("Diesen Monat trinkt niemand mehr Federweißer.", "Nobody drinks any more Federweisser this month.");
        if (hectoliters > capacity) return Lang.En ? $"This month customers only take {Util.JsNumber(capacity)} hl more." : $"Diesen Monat nimmt die Kundschaft nur noch {Util.JsNumber(capacity)} hl ab.";
        return null;
    }

    public static void SellFederweisser(GameState state, Player player, string batchId, double hectoliters)
    {
        var batch = player.Cellar.Find(b => b.Id == batchId)!;
        var revenue = Js.Round(FederweisserPricePerHl(player) * hectoliters);
        var name = FederweisserName(batch);
        player.Money += revenue;
        player.FederweisserThisMonth += hectoliters;
        Rules.RemoveBatchVolume(player, batch, hectoliters);
        Util.AddLog(state, player.Id, Lang.En ? $"{Util.JsNumber(hectoliters)} hl of {name} from {Grapes.All[batch.Grape].Name} sold: +{Util.FormatEuro(revenue)}." : $"{Util.JsNumber(hectoliters)} hl {name} aus {Grapes.All[batch.Grape].Name} verkauft: +{Util.FormatEuro(revenue)}.");
    }

    public static string? ValidateStrauss(GameState state, Player player)
    {
        if (!Economy.StraussStartMonths.Contains(state.Month)) return Lang.T("Die Straußwirtschaft öffnet zwischen März und Oktober.", "The wine tavern opens between March and October.");
        if (player.StraussYear == state.Year) return Lang.T("Die Straußwirtschaft hatte dieses Jahr schon geöffnet.", "The wine tavern has already been open this year.");
        if (player.Money < Economy.StraussCost) return Lang.En ? $"Not enough money (needs {Util.FormatEuro(Economy.StraussCost)})." : $"Nicht genug Geld (benötigt {Util.FormatEuro(Economy.StraussCost)}).";
        return null;
    }

    public static void OpenStrauss(GameState state, Player player)
    {
        player.Money -= Economy.StraussCost;
        player.StraussYear = state.Year;
        Events.AddEffect(player, EffectKind.Demand, Economy.StraussDemandFactor, Economy.StraussMonths, StraussEffectLabel);
        var gain = Rules.ChangeReputation(player, Economy.StraussReputation);
        Util.AddLog(
            state,
            player.Id,
            Lang.En ? $"🌳 The wine tavern has opened ({Util.FormatEuro(Economy.StraussCost)}): four months of more guests, reputation +{gain.ToString("F1", CultureInfo.InvariantCulture)}." : $"🌳 Die Straußwirtschaft hat geöffnet ({Util.FormatEuro(Economy.StraussCost)}): vier Monate mehr Gäste, Bekanntheit +{gain.ToString("F1", CultureInfo.InvariantCulture)}.");
    }
}
