using Winzer.Engine.Data;

namespace Winzer.Engine;

/// <summary>Gemeinsame Spielregeln, die mehrere Module brauchen (rules.ts).</summary>
public static class Rules
{
    public static readonly int[] PlantMonths = [3, 4];
    public static readonly int[] HarvestMonths = [8, 9, 10, 11, 12];

    /// <summary>Punkteziel, das nie erreicht wird – für Endlosspiel und Partien mit Jahreslimit</summary>
    public const double NoPointTarget = Js.MaxSafeInteger;

    public static bool HasPointTarget(GameState state) => state.TargetPoints < NoPointTarget;

    /// <summary>Endlosspiel: weder Punkteziel noch Jahreslimit</summary>
    public static bool IsEndless(GameState state) => !HasPointTarget(state) && state.YearLimit is null or 0;

    public static Player CurrentPlayer(GameState state) => state.Players[state.CurrentPlayer];

    /// <summary>Ertragsfähigkeit nach Alter: Pflanzjahr und Folgejahr 0, 3. Jahr 50 %, danach voll.</summary>
    public static double PlotAgeFactor(Plot plot, int year)
    {
        if (plot.Grape is null || plot.PlantedYear is null) return 0;
        var age = year - plot.PlantedYear.Value;
        if (age <= 1) return 0;
        if (age == 2) return 0.5;
        return 1;
    }

    /// <summary>Belegter Edelstahltank – Wein im Barrique zählt nicht mit</summary>
    public static double UsedTankHl(Player player)
    {
        var sum = 0.0;
        foreach (var b in player.Cellar) if (!b.Barrique) sum += b.Hectoliters;
        return sum;
    }

    public static double UsedBarriqueHl(Player player)
    {
        var sum = 0.0;
        foreach (var b in player.Cellar) if (b.Barrique) sum += b.Hectoliters;
        return sum;
    }

    /// <summary>Zusätzliche Wärme durch den Klimawandel (Skala der Gebietswärme)</summary>
    public static double ClimateWarming(GameState state) =>
        state.ClimateChange ? (state.Year - 1) * Economy.ClimateWarmthPerYear : 0;

    /// <summary>Gemeinsamer Faktor aller laufenden Ereignis-Effekte einer Art; bei Quality die Summe der Zuschläge</summary>
    public static double EffectValue(Player player, EffectKind kind)
    {
        if (kind == EffectKind.Quality)
        {
            var sum = 0.0;
            foreach (var e in player.Effects) if (e.Kind == kind) sum += e.Value;
            return sum;
        }
        var product = 1.0;
        foreach (var e in player.Effects) if (e.Kind == kind) product *= e.Value;
        return product;
    }

    /// <summary>Güte 0–100 bei der Lese – <paramref name="oechsle"/> überschreibt das Mostgewicht (z. B. für Eiswein).</summary>
    public static double ComputeQuality(Player player, Plot plot, int year, double? oechsle = null)
    {
        var most = oechsle ?? plot.Oechsle;
        var grape = Grapes.All[plot.Grape!.Value];
        var region = Regions.All[player.RegionId];
        var suitability = region.Suitability[grape.Id];
        var bonus =
            CellarMasters.CellarMasterBonus(player, grape.Id, most, year) +
            plot.Site * Economy.SiteQualityBonus +
            region.Character.Slope * Economy.SlopeQuality +
            EffectValue(player, EffectKind.Quality) +
            (player.BioLabel is { } label ? BioData.BioLabels[label].Quality : 0);
        var raw =
            20 + grape.QualityPotential * 4 + suitability * 6 + (most - 70) * 0.6 + (plot.Health - 0.5) * 20 + bonus;
        return Math.Max(0, Math.Min(100, Js.Round(raw)));
    }

    /// <summary>Rangfolge der Qualitätsstufen; Eiswein zählt wie eine Beerenauslese.</summary>
    public static int LevelRank(QualityLevel level)
    {
        var effective = level == QualityLevel.Eiswein ? QualityLevel.Beerenauslese : level;
        return Array.FindIndex(Quality.QualityLevels, q => q.Level == effective);
    }

    /// <summary>Stufe nach <paramref name="steps"/> Schritten nach oben (ohne Sonderstufen wie Eiswein)</summary>
    public static QualityLevel RaiseLevel(QualityLevel level, int steps)
    {
        var ordinary = Quality.QualityLevels.Where(q => !q.Special).ToArray();
        var index = Math.Min(ordinary.Length - 1, LevelRank(level) + steps);
        return ordinary[index].Level;
    }

    public static string WineLabel(WineInfo wine)
    {
        var kind = wine is BottleLot { Sparkling: true } ? Lang.T("Sekt", "sparkling") : Quality.QualityInfo(wine.Level).Name;
        return $"{Grapes.All[wine.Grape].Name} {(wine.Rose ? "Rosé " : "")}{kind} ({Lang.T("Jg.", "vintage")} {wine.Vintage})";
    }

    /// <summary>Ändert die Bekanntheit; Zuwächse schrumpfen, je bekannter das Weingut schon ist. Liefert die tatsächliche Änderung.</summary>
    public static double ChangeReputation(Player player, double delta)
    {
        var effective = delta > 0 ? delta * Math.Max(0, 1 - player.Reputation / Economy.ReputationSaturation) : delta;
        var before = player.Reputation;
        player.Reputation = Math.Max(0, Math.Min(100, player.Reputation + effective));
        return player.Reputation - before;
    }

    public static void RemoveBottles(Player player, BottleLot lot, double bottles)
    {
        lot.Bottles -= bottles;
        if (lot.Bottles <= 0)
        {
            player.Bottles = player.Bottles.Where(l => l.Id != lot.Id).ToList();
            player.CompetitionEntries = player.CompetitionEntries.Where(id => id != lot.Id).ToList();
        }
    }

    public static void RemoveBatchVolume(Player player, WineBatch batch, double hectoliters)
    {
        batch.Hectoliters -= hectoliters;
        if (batch.Hectoliters <= 0.001) player.Cellar = player.Cellar.Where(b => b.Id != batch.Id).ToList();
    }

    /// <summary>
    /// Legt frischen Most in den Tank; was nicht passt, geht billig an die Kellerei.
    /// Von <paramref name="wine"/> zählen Sorte, Jahrgang, Mostgewicht, Stufe, Güte, Rosé und die Menge.
    /// </summary>
    public static void StoreWine(GameState state, Player player, WineBatch wine)
    {
        var freeHl = Math.Max(0, player.TankCapacityHl - UsedTankHl(player));
        var storedHl = Math.Min(wine.Hectoliters, freeHl);
        var overflowHl = wine.Hectoliters - storedHl;
        if (storedHl > 0)
        {
            var maturing = Quality.QualityInfo(wine.Level).MaturingMonths;
            var batch = new WineBatch
            {
                Id = Util.NewId(state, "wine"),
                Hectoliters = storedHl,
                // Rosé ist schnell trinkfertig
                MonthsUntilReady = wine.Rose ? Math.Min(Economy.RoseMaturingMonths, maturing) : maturing,
                Barrique = false,
            };
            batch.CopyInfoFrom(wine);
            batch.Adulterated = null;
            player.Cellar.Add(batch);
        }
        if (overflowHl > 0)
        {
            var revenue = Js.Round(overflowHl * 100 * Economy.OverflowPricePerLiter);
            player.Money += revenue;
            Util.AddLog(state, player.Id, Lang.En ? $"Tanks full! {Util.JsNumber(overflowHl)} hl of must sold off cheaply: +{Util.FormatEuro(revenue)}." : $"Tanks voll! {Util.JsNumber(overflowHl)} hl Most billig abgegeben: +{Util.FormatEuro(revenue)}.");
        }
    }
}
