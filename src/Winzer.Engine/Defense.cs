using Winzer.Engine.Data;

namespace Winzer.Engine;

/// <summary>Nachrüstbare Anlage einer Parzelle (<c>'hailNet' | 'irrigation'</c> im Original)</summary>
public enum Installation
{
    HailNet,
    Irrigation,
}

/// <summary>
/// Aktive Wetterabwehr: Frostkerzen, Hagelnetze, Bewässerung und Ernteversicherung.
/// Hier liegen auch die Regeln für Ernteschäden, damit die Versicherung überall gleich zahlt.
/// </summary>
public static class Defense
{
    /// <summary>Reben, die in diesem Jahr noch Trauben tragen</summary>
    public static List<Plot> BearingPlots(GameState state, Player player) =>
        player.Plots.Where(p => p.Grape is not null && !p.HarvestedThisYear && Rules.PlotAgeFactor(p, state.Year) > 0).ToList();

    static double Hectares(IEnumerable<Plot> plots)
    {
        var sum = 0.0;
        foreach (var p in plots) sum += p.Hectares;
        return sum;
    }

    static int ClimateYears(GameState state) => state.ClimateChange ? state.Year - 1 : 0;

    /// <summary>Spätfrost-Chance im laufenden Monat (nur April und Mai)</summary>
    public static double FrostChance(GameState state, Player player) =>
        Array.IndexOf(Economy.FrostMonths, state.Month) >= 0 ? Regions.All[player.RegionId].FrostRisk * Economy.FrostChancePerRisk : 0;

    public static double HailChance(GameState state) => Economy.HailChance + ClimateYears(state) * Economy.HailClimatePerYear;

    /// <summary>Dürre-Chance bei sonnigem Hochsommer – in nassen Gebieten seltener</summary>
    public static double DroughtChance(GameState state, Player player)
    {
        var baseChance = Economy.DroughtChance + ClimateYears(state) * Economy.DroughtClimatePerYear;
        return baseChance * Math.Max(0.2, 1 - Regions.All[player.RegionId].Wetness / 15);
    }

    public static double FrostCandlesCost(GameState state, Player player) =>
        Js.Round(Hectares(BearingPlots(state, player)) * Economy.FrostCandlesPerHa);

    public static double InsurancePremium(GameState state, Player player)
    {
        var perHa = Economy.InsuranceBasePremium + Regions.All[player.RegionId].FrostRisk * Economy.InsurancePremiumPerFrostRisk;
        return Js.Round(Hectares(BearingPlots(state, player)) * perHa / 10) * 10;
    }

    public static bool IsInsured(GameState state, Player player) => player.InsuredYear == state.Year;

    /// <summary>
    /// Ernteschaden auf den Parzellen: Der Mengenfaktor sinkt um <paramref name="factor"/>. Mit Ernteversicherung werden 80 % des
    /// verlorenen Werts erstattet. Liefert die Entschädigung.
    /// </summary>
    public static double CropLoss(GameState state, Player player, IEnumerable<Plot> plots, double factor, string cause)
    {
        var lostHa = 0.0;
        foreach (var plot in plots)
        {
            lostHa += plot.Hectares * plot.CropFactor * (1 - factor);
            plot.CropFactor *= factor;
        }
        if (!IsInsured(state, player) || lostHa <= 0) return 0;
        var payout = Js.Round(lostHa * Economy.InsuredValuePerHa * (1 - Economy.InsuranceDeductible) / 100) * 100;
        player.Money += payout;
        Util.AddLog(state, player.Id, Lang.En ? $"🛡️ The crop insurance pays {Util.FormatEuro(payout)} for the damage caused by {cause}." : $"🛡️ Die Ernteversicherung zahlt {Util.FormatEuro(payout)} für den Schaden durch {cause}.", LogTone.Good);
        return payout;
    }

    // ── Aktionen ──────────────────────────────────────────────────────────────────

    public static string? ValidateFrostCandles(GameState state, Player player)
    {
        if (Array.IndexOf(Economy.FrostMonths, state.Month) < 0) return Lang.T("Spätfrost droht nur im April und Mai.", "Late frost only threatens in April and May.");
        if (player.FrostCandles) return Lang.T("Die Frostkerzen stehen bereits.", "The frost candles are already in place.");
        if (BearingPlots(state, player).Count == 0) return Lang.T("Es gibt keine tragenden Reben zu schützen.", "There are no bearing vines to protect.");
        var cost = FrostCandlesCost(state, player);
        if (player.Money < cost) return Lang.En ? $"Not enough money (needs {Util.FormatEuro(cost)})." : $"Nicht genug Geld (benötigt {Util.FormatEuro(cost)}).";
        return null;
    }

    public static void SetFrostCandles(GameState state, Player player)
    {
        var cost = FrostCandlesCost(state, player);
        player.Money -= cost;
        player.FrostCandles = true;
        Util.AddLog(state, player.Id, Lang.En ? $"Frost candles are ready for all bearing sites ({Util.FormatEuro(cost)})." : $"Frostkerzen für alle tragenden Lagen stehen bereit ({Util.FormatEuro(cost)}).");
    }

    public static string? ValidateInsurance(GameState state, Player player)
    {
        if (Array.IndexOf(Economy.InsuranceMonths, state.Month) < 0) return Lang.T("Versicherungen werden nur von Januar bis April abgeschlossen.", "Insurance can only be taken out from January to April.");
        if (IsInsured(state, player)) return Lang.T("Die Ernte ist in diesem Jahr bereits versichert.", "The harvest is already insured this year.");
        var premium = InsurancePremium(state, player);
        if (premium == 0) return Lang.T("Es gibt noch keine tragenden Reben zu versichern.", "There are no bearing vines to insure yet.");
        if (player.Money < premium) return Lang.En ? $"Not enough money (premium {Util.FormatEuro(premium)})." : $"Nicht genug Geld (Prämie {Util.FormatEuro(premium)}).";
        return null;
    }

    public static void Insure(GameState state, Player player)
    {
        var premium = InsurancePremium(state, player);
        player.Money -= premium;
        player.InsuredYear = state.Year;
        Util.AddLog(state, player.Id, Lang.En ? $"Crop insurance for year {state.Year} taken out ({Util.FormatEuro(premium)})." : $"Ernteversicherung für Jahr {state.Year} abgeschlossen ({Util.FormatEuro(premium)}).");
    }

    static readonly Dictionary<Installation, (double Cost, string NameDe, string NameEn)> InstallData = new()
    {
        [Installation.HailNet] = (Economy.HailNetCostPerHa, "Hagelnetz", "Hail net"),
        [Installation.Irrigation] = (Economy.IrrigationCostPerHa, "Tröpfchenbewässerung", "Drip irrigation"),
    };

    static (double Cost, string Name) InstallInfoOf(Installation kind) => (InstallData[kind].Cost, Lang.T(InstallData[kind].NameDe, InstallData[kind].NameEn));

    static bool IsInstalled(Plot plot, Installation kind) => kind == Installation.HailNet ? plot.HailNet : plot.Irrigation;

    public static double InstallCost(Plot plot, Installation kind) => plot.Hectares * InstallData[kind].Cost;

    public static string? ValidateInstall(Player player, string plotId, Installation kind)
    {
        var plot = player.Plots.Find(p => p.Id == plotId);
        if (plot is null) return Lang.T("Unbekannte Parzelle.", "Unknown plot.");
        if (IsInstalled(plot, kind)) return Lang.En ? $"{InstallInfoOf(kind).Name} is already installed." : $"{InstallInfoOf(kind).Name} ist bereits installiert.";
        var cost = InstallCost(plot, kind);
        if (player.Money < cost) return Lang.En ? $"Not enough money (needs {Util.FormatEuro(cost)})." : $"Nicht genug Geld (benötigt {Util.FormatEuro(cost)}).";
        return null;
    }

    public static void Install(GameState state, Player player, string plotId, Installation kind)
    {
        var plot = player.Plots.Find(p => p.Id == plotId)!;
        var cost = InstallCost(plot, kind);
        player.Money -= cost;
        if (kind == Installation.HailNet) plot.HailNet = true;
        else plot.Irrigation = true;
        Util.AddLog(state, player.Id, Lang.En ? $"{plot.Name}: {InstallInfoOf(kind).Name} installed ({Util.FormatEuro(cost)})." : $"{plot.Name}: {InstallInfoOf(kind).Name} installiert ({Util.FormatEuro(cost)}).");
    }
}
