using Winzer.Engine.Data;

namespace Winzer.Engine;

/// <summary>Keller: Abfüllen, Sekt, Barrique und Reifung (cellar.ts).</summary>
public static class Cellar
{
    public const double SektCostPerBottle = Economy.BottleCost + Economy.SektExtraCostPerBottle;

    /// <summary>Kosten je Flasche – ein sparsamer Kellermeister drückt sie</summary>
    public static double BottleCost(Player player) => Economy.BottleCost * CellarMasters.BottlingCostFactor(player);
    public static double SektCost(Player player) => SektCostPerBottle * CellarMasters.BottlingCostFactor(player);

    /// <summary>Liefert die Fehlermeldung oder null; im Erfolgsfall steht die Charge in <paramref name="batch"/>.</summary>
    static string? ReadyBatch(Player player, string batchId, double hectoliters, out WineBatch? batch)
    {
        batch = player.Cellar.Find(b => b.Id == batchId);
        if (batch is null) return Lang.T("Unbekannte Charge.", "Unknown batch.");
        if (batch.MonthsUntilReady > 0) return Lang.T("Der Wein ist noch nicht ausgebaut.", "The wine has not finished maturing yet.");
        if (!(hectoliters > 0) || hectoliters > batch.Hectoliters) return Lang.T("Ungültige Menge.", "Invalid quantity.");
        return null;
    }

    public static string? ValidateBottle(Player player, string batchId, double hectoliters)
    {
        var problem = ReadyBatch(player, batchId, hectoliters, out _);
        if (problem is not null) return problem;
        var cost = Market.HlToBottles(hectoliters) * BottleCost(player);
        if (player.Money < cost) return Lang.En ? $"Not enough money for bottles (needs {Util.FormatEuro(cost)})." : $"Nicht genug Geld für Flaschen (benötigt {Util.FormatEuro(cost)}).";
        return null;
    }

    public static void Bottle(GameState state, Player player, string batchId, double hectoliters)
    {
        var batch = player.Cellar.Find(b => b.Id == batchId)!;
        var bottles = Market.HlToBottles(hectoliters);
        var cost = Js.Round(bottles * BottleCost(player));
        player.Money -= cost;
        Rules.RemoveBatchVolume(player, batch, hectoliters);

        // Nachfüllen in einen noch ungeprüften Posten desselben Weins, damit kleine Abfüllungen prüfbar bleiben.
        var existing = player.Bottles.Find(l =>
            !l.Registered &&
            !l.Sparkling &&
            l.Grape == batch.Grape &&
            l.Vintage == batch.Vintage &&
            l.Level == batch.Level &&
            l.Quality == batch.Quality &&
            l.Adulterated == batch.Adulterated &&
            l.Rose == batch.Rose);
        if (existing is not null) existing.Bottles += bottles;
        else player.Bottles.Add(NewLot(state, batch, bottles, false));
        Util.AddLog(state, player.Id, Lang.En ? $"{Util.JsNumber(bottles)} bottles of {Rules.WineLabel(batch)} bottled: −{Util.FormatEuro(cost)}." : $"{Util.JsNumber(bottles)} Flaschen {Rules.WineLabel(batch)} abgefüllt: −{Util.FormatEuro(cost)}.");
    }

    public static string? ValidateSekt(Player player, string batchId, double hectoliters)
    {
        var problem = ReadyBatch(player, batchId, hectoliters, out _);
        if (problem is not null) return problem;
        if (!CellarMasters.HasSkill(player, CellarSkill.Sekt)) return Lang.T("Für Sekt braucht es einen Kellermeister, der die Flaschengärung beherrscht.", "Sparkling wine needs a cellar master who masters bottle fermentation.");
        var cost = Market.HlToBottles(hectoliters) * SektCost(player);
        if (player.Money < cost) return Lang.En ? $"Not enough money to make sparkling wine (needs {Util.FormatEuro(cost)})." : $"Nicht genug Geld für die Sektherstellung (benötigt {Util.FormatEuro(cost)}).";
        return null;
    }

    /// <summary>Grundwein wird in der Flasche ein zweites Mal vergoren und reift neun Monate auf der Hefe.</summary>
    public static void MakeSekt(GameState state, Player player, string batchId, double hectoliters)
    {
        var batch = player.Cellar.Find(b => b.Id == batchId)!;
        var bottles = Market.HlToBottles(hectoliters);
        var cost = Js.Round(bottles * SektCost(player));
        player.Money -= cost;
        player.Stats.Sekt++;
        Rules.RemoveBatchVolume(player, batch, hectoliters);
        player.Bottles.Add(NewLot(state, batch, bottles, true));
        Util.AddLog(
            state,
            player.Id,
            Lang.En ? $"{Util.JsNumber(bottles)} bottles of sparkling wine started from {Rules.WineLabel(batch)} – ready in {Economy.SektMonths} months (−{Util.FormatEuro(cost)})." : $"{Util.JsNumber(bottles)} Flaschen Sekt aus {Rules.WineLabel(batch)} angesetzt – fertig in {Economy.SektMonths} Monaten (−{Util.FormatEuro(cost)}).");
    }

    public static double BarriqueBonus(WineBatch batch) =>
        Grapes.All[batch.Grape].Color == GrapeColor.Rot ? Economy.BarriqueQualityRed : Economy.BarriqueQualityWhite;

    public static string? ValidateExpandBarrique(Player player, double hectoliters)
    {
        if (!double.IsInteger(hectoliters) || hectoliters <= 0) return Lang.T("Ungültige Menge.", "Invalid quantity.");
        var cost = hectoliters * Economy.BarriqueCostPerHl;
        if (player.Money < cost) return Lang.En ? $"Not enough money (needs {Util.FormatEuro(cost)})." : $"Nicht genug Geld (benötigt {Util.FormatEuro(cost)}).";
        return null;
    }

    public static void ExpandBarrique(GameState state, Player player, double hectoliters)
    {
        player.Money -= hectoliters * Economy.BarriqueCostPerHl;
        player.BarriqueCapacityHl += hectoliters;
        Util.AddLog(state, player.Id, Lang.En ? $"{Util.JsNumber(hectoliters)} hl of barrique barrels bought." : $"{Util.JsNumber(hectoliters)} hl Barriquefässer gekauft.");
    }

    public static string? ValidateToBarrique(Player player, string batchId)
    {
        var batch = player.Cellar.Find(b => b.Id == batchId);
        if (batch is null) return Lang.T("Unbekannte Charge.", "Unknown batch.");
        if (batch.Barrique) return Lang.T("Der Wein liegt bereits im Barrique.", "The wine is already in barrique.");
        if (batch.Rose) return Lang.T("Rosé lebt von Frische – ins Barrique gehört er nicht.", "Rosé lives on freshness – it does not belong in barrique.");
        if (!CellarMasters.HasSkill(player, CellarSkill.Barrique)) return Lang.T("Für den Barrique-Ausbau braucht es einen Kellermeister mit Erfahrung im Eichenfass.", "Barrique aging needs a cellar master experienced with oak barrels.");
        if (batch.Level == QualityLevel.Eiswein || batch.Level == QualityLevel.Trockenbeerenauslese) return Lang.T("Edelsüße Weine gehören nicht ins Barrique.", "Noble sweet wines do not belong in barrique.");
        var free = player.BarriqueCapacityHl - Rules.UsedBarriqueHl(player);
        if (batch.Hectoliters > free) return Lang.En ? $"The barrique only has room for {Util.JsNumber(free)} hl." : $"Im Barrique ist nur Platz für {Util.JsNumber(free)} hl.";
        return null;
    }

    /// <summary>Umlagern ins Eichenfass: längere Reife, dafür mehr Güte (vor allem bei Rotwein) – und Platz im Tank.</summary>
    public static void ToBarrique(GameState state, Player player, string batchId)
    {
        var batch = player.Cellar.Find(b => b.Id == batchId)!;
        var bonus = BarriqueBonus(batch);
        player.Stats.Barrique++;
        batch.Barrique = true;
        batch.Quality = Math.Min(100, batch.Quality + bonus);
        batch.MonthsUntilReady = Math.Max(batch.MonthsUntilReady, Economy.BarriqueMonths);
        Util.AddLog(state, player.Id, Lang.En ? $"{Rules.WineLabel(batch)} now ages {Economy.BarriqueMonths} months in barrique (+{Util.JsNumber(bonus)} quality)." : $"{Rules.WineLabel(batch)} reift jetzt {Economy.BarriqueMonths} Monate im Barrique (+{Util.JsNumber(bonus)} Güte).");
    }

    static BottleLot NewLot(GameState state, WineBatch batch, double bottles, bool sparkling) => new()
    {
        Id = Util.NewId(state, "lot"),
        Grape = batch.Grape,
        Vintage = batch.Vintage,
        Oechsle = batch.Oechsle,
        Level = batch.Level,
        Quality = sparkling ? Math.Min(100, batch.Quality + Economy.SektQualityBonus) : batch.Quality,
        Adulterated = batch.Adulterated,
        Rose = batch.Rose,
        Bottles = bottles,
        Sparkling = sparkling,
        MonthsUntilReady = sparkling ? Economy.SektMonths : 0,
        Registered = false,
        Awards = [],
    };

    /// <summary>Monatsende: Wein im Tank und Sekt in der Flasche reifen weiter.</summary>
    public static void MatureCellar(GameState state, Player player)
    {
        foreach (var batch in player.Cellar)
        {
            if (batch.MonthsUntilReady > 0 && --batch.MonthsUntilReady == 0)
            {
                Util.AddLog(state, player.Id, Lang.En ? $"{Rules.WineLabel(batch)} has finished maturing." : $"{Rules.WineLabel(batch)} ist ausgebaut.");
            }
        }
        foreach (var lot in player.Bottles)
        {
            if (lot.MonthsUntilReady > 0 && --lot.MonthsUntilReady == 0)
            {
                Util.AddLog(state, player.Id, Lang.En ? $"{Rules.WineLabel(lot)} has matured and can be tested." : $"{Rules.WineLabel(lot)} hat ausgereift und kann geprüft werden.");
            }
        }
    }
}
