using Winzer.Engine.Data;

namespace Winzer.Engine;

/// <summary>Weinberg: Reife, Krankheiten, Wetterereignisse, Eiswein und Pflanzenschutz (vineyard.ts).</summary>
public static class Vineyard
{
    /// <summary>Basis-Reifezuwachs in °Oe am Ende des jeweiligen Monats</summary>
    static readonly Dictionary<int, double> RipeningBase = new() { [8] = 20, [9] = 18, [10] = 12, [11] = 5 };
    public const double RipeningStartOechsle = 45;
    /// <summary>Mengenverlust durch Fäulnis am Ende des Monats</summary>
    static readonly Dictionary<int, double> RotLoss = new() { [10] = 0.05, [11] = 0.25 };
    /// <summary>Edelfäule: Chance bei wechselhaftem Herbstwetter (Morgennebel, Nachmittagssonne)</summary>
    const double BotrytisChance = 0.35;
    const double BotrytisOechsle = 30;

    static bool Bearing(GameState state, Plot plot) =>
        plot.Grape is not null && !plot.HarvestedThisYear && Rules.PlotAgeFactor(plot, state.Year) > 0;

    /// <summary>Reife, Fäulnis, Krankheiten und Edelfäule einer Parzelle am Monatsende</summary>
    /// <summary>
    /// Reifezuwachs dieses Monats in °Oechsle bei diesem Wetter – dieselbe Rechnung wie in <see cref="GrowPlot"/>,
    /// ohne die Parzelle zu verändern. Regelversion 2 teilt ihn auf zwei Monatshälften auf.
    /// </summary>
    public static double RipeningGain(GameState state, Player player, Plot plot, WeatherKind weather)
    {
        if (!Bearing(state, plot)) return 0;
        var grape = Grapes.All[plot.Grape!.Value];
        if (!RipeningBase.TryGetValue(state.Month, out var baseGain) || plot.Oechsle >= grape.MaxOechsle) return 0;
        var headroom = (grape.MaxOechsle - plot.Oechsle) / (grape.MaxOechsle - RipeningStartOechsle);
        var warmth = Regions.All[player.RegionId].Warmth + Rules.ClimateWarming(state);
        var gain = (baseGain * Weather.SunFactor[weather] * (0.8 + warmth * 0.04) * headroom) / grape.RipeningNeed;
        return Math.Min(grape.MaxOechsle, plot.Oechsle + gain) - plot.Oechsle;
    }

    public static void GrowPlot(GameState state, Player player, Plot plot, WeatherKind weather)
    {
        if (!Bearing(state, plot)) return;
        var grape = Grapes.All[plot.Grape!.Value];
        var region = Regions.All[player.RegionId];
        var shield = 1 - Economy.Protection[player.Protection].Protection;
        double Roll() => Rng.NextFloat(state.Rng);

        if (state.Month == 7) plot.Oechsle = RipeningStartOechsle;

        if (RipeningBase.TryGetValue(state.Month, out var baseGain) && plot.Oechsle < grape.MaxOechsle)
        {
            var headroom = (grape.MaxOechsle - plot.Oechsle) / (grape.MaxOechsle - RipeningStartOechsle);
            var warmth = region.Warmth + Rules.ClimateWarming(state);
            var gain = (baseGain * Weather.SunFactor[weather] * (0.8 + warmth * 0.04) * headroom) / grape.RipeningNeed;
            plot.Oechsle = Math.Min(grape.MaxOechsle, plot.Oechsle + gain);
        }

        // Pilzkrankheiten und Schädlinge – Pflanzenschutz senkt das Risiko
        if (state.Month >= 5 && state.Month <= 8 && weather == WeatherKind.Nass)
        {
            if (Roll() < 0.4 * grape.DiseaseSusceptibility * (0.5 + region.Wetness / 10) * shield)
            {
                plot.CropFactor *= 0.7;
                Util.AddLog(state, player.Id, Lang.En ? $"{plot.Name}: Peronospora (downy mildew) – about 30% lower yield." : $"{plot.Name}: Peronospora (Falscher Mehltau) – rund 30 % weniger Ertrag.");
            }
        }
        if (state.Month >= 6 && state.Month <= 8 && weather == WeatherKind.Sonnig)
        {
            if (Roll() < 0.25 * grape.DiseaseSusceptibility * shield)
            {
                plot.Health = Math.Max(0, plot.Health - 0.2);
                Util.AddLog(state, player.Id, Lang.En ? $"{plot.Name}: Oidium (powdery mildew) – the grapes suffer." : $"{plot.Name}: Oidium (Echter Mehltau) – die Trauben leiden.");
            }
        }
        if (state.Month == 9 && grape.Color == GrapeColor.Rot && Roll() < 0.3 * shield)
        {
            plot.CropFactor *= 0.85;
            plot.Health = Math.Max(0, plot.Health - 0.1);
            Util.AddLog(state, player.Id, Lang.En ? $"{plot.Name}: The spotted wing drosophila has arrived – losses among the red grapes." : $"{plot.Name}: Die Kirschessigfliege ist da – Verluste bei den roten Trauben.");
        }

        if (state.Month >= 9 && weather == WeatherKind.Nass)
        {
            plot.Health = Math.Max(0, plot.Health - 0.15 * grape.RotSusceptibility);
        }
        if (RotLoss.TryGetValue(state.Month, out var rotLoss))
        {
            var wetBonus = weather == WeatherKind.Nass ? 0.1 * grape.RotSusceptibility : 0;
            plot.CropFactor *= 1 - rotLoss - wetBonus;
        }

        // Edelfäule: Fluch für die Menge, Segen fürs Mostgewicht
        if ((state.Month == 10 || state.Month == 11) && weather == WeatherKind.Wechselhaft && plot.Oechsle >= 80)
        {
            if (Roll() < BotrytisChance * (0.5 + grape.RotSusceptibility))
            {
                plot.Oechsle += BotrytisOechsle;
                plot.CropFactor *= plot.Botrytis ? 0.6 : 0.5;
                Util.AddLog(
                    state,
                    player.Id,
                    plot.Botrytis
                        ? Lang.En ? $"{plot.Name}: The berries shrivel into raisins – {Util.JsNumber(Js.Round(plot.Oechsle))} °Oe!" : $"{plot.Name}: Die Beeren schrumpfen zu Rosinen – {Util.JsNumber(Js.Round(plot.Oechsle))} °Oe!"
                        : Lang.En ? $"{plot.Name}: Noble rot (botrytis)! Less quantity, but {Util.JsNumber(Js.Round(plot.Oechsle))} °Oe." : $"{plot.Name}: Edelfäule (Botrytis)! Weniger Menge, aber {Util.JsNumber(Js.Round(plot.Oechsle))} °Oe.");
                plot.Botrytis = true;
            }
        }
    }

    /// <summary>Wetterereignisse, die den ganzen Betrieb treffen: Spätfrost, Hagel, Hitzewelle, Dürre – samt Wetterabwehr</summary>
    public static void VineyardEvents(GameState state, Player player, WeatherKind weather)
    {
        var plots = player.Plots.Where(p => Bearing(state, p)).ToList();
        if (plots.Count == 0) return;
        double Roll() => Rng.NextFloat(state.Rng);

        if (Roll() < Defense.FrostChance(state, player))
        {
            if (player.FrostCandles)
            {
                Util.AddLog(state, player.Id, Lang.T("🕯️ Spätfrost in der Nacht – deine Frostkerzen haben die jungen Triebe gerettet.", "🕯️ Late frost overnight – your frost candles saved the young shoots."), LogTone.Good);
            }
            else
            {
                Util.AddLog(state, player.Id, Lang.T("Spätfrost! Die jungen Triebe sind erfroren – rund 40 % weniger Ertrag in diesem Jahr.", "Late frost! The young shoots froze – about 40% lower yield this year."), LogTone.Bad);
                Defense.CropLoss(state, player, plots, 1 - Economy.FrostLoss, Lang.T("Spätfrost", "late frost"));
            }
        }

        if (Economy.HailMonths.Contains(state.Month) && Roll() < Defense.HailChance(state))
        {
            // Eine Parzelle, manchmal zwei – ohne Zurücklegen gezogen
            var pool = new List<Plot>(plots);
            var hitCount = Math.Min(pool.Count, Roll() < 0.3 ? 2 : 1);
            var hit = new List<Plot>();
            for (var i = 0; i < hitCount; i++)
            {
                var index = (int)Math.Floor(Roll() * pool.Count);
                hit.Add(pool[index]);
                pool.RemoveAt(index);
            }
            foreach (var plot in hit)
            {
                if (plot.HailNet)
                {
                    Util.AddLog(state, player.Id, Lang.En ? $"Hailstorm over {plot.Name} – the hail net stops most of it." : $"Hagelschlag über {plot.Name} – das Hagelnetz hält das Meiste ab.", LogTone.Good);
                    Defense.CropLoss(state, player, new List<Plot> { plot }, 1 - Economy.HailLoss * (1 - Economy.HailNetProtection), Lang.T("Hagel", "hail"));
                }
                else
                {
                    plot.Health = Math.Max(0, plot.Health - 0.1);
                    Util.AddLog(state, player.Id, Lang.En ? $"Hailstorm over {plot.Name} – half of the grapes are destroyed." : $"Hagelschlag über {plot.Name} – die Hälfte der Trauben ist zerstört.", LogTone.Bad);
                    Defense.CropLoss(state, player, new List<Plot> { plot }, 1 - Economy.HailLoss, Lang.T("Hagel", "hail"));
                }
            }
        }

        var heatChance = 0.15 + (state.ClimateChange ? (state.Year - 1) * Economy.ClimateHeatPerYear : 0);
        if ((state.Month == 7 || state.Month == 8) && weather == WeatherKind.Sonnig && Roll() < heatChance)
        {
            foreach (var plot in plots)
            {
                plot.Oechsle += 4;
                if (plot.Irrigation) continue;
                plot.CropFactor *= 0.9;
                plot.Health = Math.Max(0, plot.Health - 0.05);
            }
            var watered = plots.Count(p => p.Irrigation);
            Util.AddLog(
                state,
                player.Id,
                watered == plots.Count
                    ? Lang.T("Hitzewelle: Die Bewässerung hält die Reben frisch – nur die Süße legt zu.", "Heat wave: irrigation keeps the vines fresh – only the sweetness increases.")
                    : Lang.En ? $"Heat wave: sunburn on the grapes, slightly less quantity, but sweeter{(watered != 0 ? " (irrigated sites undamaged)" : "")}." : $"Hitzewelle: Sonnenbrand an den Trauben, etwas weniger Menge, dafür süßer{(watered != 0 ? " (bewässerte Lagen ohne Schaden)" : "")}.");
        }

        if (Economy.DroughtMonths.Contains(state.Month) && weather == WeatherKind.Sonnig && Roll() < Defense.DroughtChance(state, player))
        {
            var dry = plots.Where(p => !p.Irrigation).ToList();
            if (dry.Count == 0)
            {
                Util.AddLog(state, player.Id, Lang.T("💧 Dürre im Hochsommer – deine Bewässerung bringt alle Lagen gut durch.", "💧 Drought in high summer – your irrigation gets all sites through well."), LogTone.Good);
            }
            else
            {
                foreach (var plot in dry) plot.Health = Math.Max(0, plot.Health - 0.05);
                Util.AddLog(
                    state,
                    player.Id,
                    Lang.En ? $"Drought! Unirrigated vines suffer drought stress – about {Util.JsNumber(Js.Round(Economy.DroughtLoss * 100))}% lower yield there." : $"Dürre! Unbewässerte Reben leiden unter Trockenstress – rund {Util.JsNumber(Js.Round(Economy.DroughtLoss * 100))} % weniger Ertrag dort.",
                    LogTone.Bad);
                Defense.CropLoss(state, player, dry, 1 - Economy.DroughtLoss, Lang.T("Dürre", "drought"));
            }
        }
    }

    /// <summary>Chance auf starken Dezemberfrost – der Klimawandel macht Eiswein Jahr für Jahr seltener.</summary>
    public static double IceWineChance(GameState state, RegionId regionId)
    {
        var baseChance = Economy.IceWineBaseChance + Regions.All[regionId].FrostRisk * Economy.IceWineChancePerFrostRisk;
        var loss = state.ClimateChange ? (state.Year - 1) * Economy.ClimateIceWineLossPerYear : 0;
        return Math.Max(0.05, baseChance - loss);
    }

    public static string? ValidateIceWineGamble(GameState state, Player player, string plotId, bool gamble)
    {
        var plot = player.Plots.Find(p => p.Id == plotId);
        if (plot is null) return Lang.T("Unbekannte Parzelle.", "Unknown plot.");
        if (!gamble) return plot.IceWineGamble ? null : Lang.T("Die Parzelle ist nicht für Eiswein vorgesehen.", "The plot is not set aside for Eiswein.");
        if (state.Month != 11 && state.Month != 12) return Lang.T("Für Eiswein entscheidet man im November oder Dezember.", "You decide on Eiswein in November or December.");
        if (!Bearing(state, plot)) return Lang.T("Hier hängen keine Trauben mehr.", "No grapes hang here any more.");
        if (plot.IceWineGamble) return Lang.T("Die Trauben hängen bereits für Eiswein.", "The grapes are already hanging for Eiswein.");
        if (plot.Oechsle < Economy.IceWineMinOechsle) return Lang.En ? $"Eiswein needs at least {Util.JsNumber(Economy.IceWineMinOechsle)} °Oe." : $"Für Eiswein braucht es mindestens {Util.JsNumber(Economy.IceWineMinOechsle)} °Oe.";
        return null;
    }

    /// <summary>
    /// Ende Dezember: Bei starkem Frost werden die gefrorenen Trauben zu Eiswein gelesen,
    /// sonst sind sie verloren. <paramref name="frost"/> wird je Anbaugebiet einmal ausgewürfelt.
    /// </summary>
    public static void ResolveIceWine(GameState state, Player player, bool frost)
    {
        foreach (var plot in player.Plots.Where(p => p.IceWineGamble && Bearing(state, p)).ToList())
        {
            plot.HarvestedThisYear = true;
            if (!frost)
            {
                Util.AddLog(state, player.Id, Lang.En ? $"{plot.Name}: No frost – the grapes for the Eiswein are lost." : $"{plot.Name}: Kein Frost – die Trauben für den Eiswein sind verloren.");
                continue;
            }
            var grape = Grapes.All[plot.Grape!.Value];
            var oechsle = Math.Max(125, plot.Oechsle + 40);
            var hectoliters = Js.Round(
                plot.Hectares * grape.YieldPerHa * Rules.PlotAgeFactor(plot, state.Year) * Economy.IceWineYieldShare * plot.CropFactor * (0.6 + 0.4 * plot.Health));
            var cost = Js.Round(plot.Hectares * Economy.HarvestCostPerHa * 1.5);
            player.Money -= cost;
            player.Stats.Harvests++;
            player.Stats.Edelsuess++;
            player.Stats.BestLevelRank = Math.Max(player.Stats.BestLevelRank, Rules.LevelRank(QualityLevel.Eiswein));
            Rules.StoreWine(state, player, new WineBatch
            {
                Grape = grape.Id,
                Vintage = state.Year,
                Hectoliters = hectoliters,
                Oechsle = Js.Round(oechsle),
                Level = QualityLevel.Eiswein,
                Quality = Rules.ComputeQuality(player, plot, state.Year, oechsle),
            });
            Util.AddLog(
                state,
                player.Id,
                Lang.En ? $"Bitter frost! {plot.Name}: {Util.JsNumber(hectoliters)} hl of Eiswein harvested at {Util.JsNumber(Js.Round(oechsle))} °Oe ({Util.FormatEuro(cost)})." : $"Klirrender Frost! {plot.Name}: {Util.JsNumber(hectoliters)} hl Eiswein mit {Util.JsNumber(Js.Round(oechsle))} °Oe gelesen ({Util.FormatEuro(cost)}).");
        }
    }

    public static string? ValidateProtection(Player player, ProtectionStrategy strategy) =>
        player.Protection == strategy ? Lang.T("Diese Strategie ist bereits gewählt.", "This strategy is already selected.") : null;

    public static void SetProtection(GameState state, Player player, ProtectionStrategy strategy)
    {
        if (player.Protection == ProtectionStrategy.Bio) Bio.LoseBio(state, player, Lang.T("kein Bio-Pflanzenschutz mehr", "no longer using organic crop protection"));
        player.Protection = strategy;
        player.BioSince = strategy == ProtectionStrategy.Bio ? Bio.GameMonth(state) : null;
        Util.AddLog(
            state,
            player.Id,
            strategy == ProtectionStrategy.Bio
                ? Lang.T("Umstellung auf Bio-Weinbau begonnen – nach zwei Jahren kann das Grünblatt-Siegel beantragt werden.", "Conversion to organic viticulture started – after two years the Grünblatt seal can be applied for.")
                : Lang.En ? $"Crop protection: {Economy.Protection[strategy].Name}." : $"Pflanzenschutz: {Economy.Protection[strategy].Name}.");
    }
}
