using Winzer.Engine.Data;

namespace Winzer.Engine;

/// <summary>Richtung eines Trends für die Anzeige ('up' | 'down' | 'flat' im Original)</summary>
public enum TrendDirectionKind
{
    Up,
    Down,
    Flat,
}

/// <summary>
/// Markttrends: Jede Rebsorte und Sekt hat eine Beliebtheit, die als Faktor auf alle Verkaufspreise wirkt.
/// Ab und zu ändert sich das Ziel – dann wandert der Wert über viele Monate dorthin. Neue Reben tragen erst
/// im dritten Jahr: Wer pflanzt, wettet auf den Markt von übermorgen. (trends.ts)
/// </summary>
public static class Trends
{
    public static readonly TrendKey[] TrendKeys =
        [.. Grapes.All.Keys.Select(grape => (TrendKey)(int)grape), TrendKey.Sekt, TrendKey.Rose];

    public static string TrendName(TrendKey key) =>
        key == TrendKey.Sekt ? Lang.T("Sekt", "Sparkling wine") : key == TrendKey.Rose ? "Rosé" : Grapes.All[(GrapeId)(int)key].Name;

    static double Round(double value) => Js.Round(value * 100) / 100;
    static double Clamp(double value) => Math.Min(Economy.TrendRange.Max, Math.Max(Economy.TrendRange.Min, value));

    /// <summary>Neues Ziel: meist in der Nähe von 100 %, selten ein echter Boom oder Absturz</summary>
    static double DrawTarget(GameState state)
    {
        var first = Rng.NextFloat(state.Rng);
        var second = Rng.NextFloat(state.Rng);
        var spread = first + second - 1; // −1 … 1, Häufung um 0
        return Round(Clamp(1 + spread * 0.4));
    }

    public static void InitTrends(GameState state)
    {
        state.Trends = [];
        foreach (var key in TrendKeys)
        {
            var value = Round(Clamp(1 + (Rng.NextFloat(state.Rng) - 0.5) * 0.3));
            state.Trends[key] = new Trend { Value = value, Target = value };
        }
    }

    /// <summary>Monatsende: Werte wandern ans Ziel, gelegentlich dreht der Markt.</summary>
    public static void UpdateTrends(GameState state)
    {
        foreach (var key in TrendKeys)
        {
            var trend = state.Trends[key];
            if (Rng.NextFloat(state.Rng) < Economy.TrendShiftChance)
            {
                var target = DrawTarget(state);
                var change = target - trend.Value;
                trend.Target = target;
                if (Math.Abs(change) >= Economy.TrendNewsThreshold)
                {
                    Util.AddLog(
                        state,
                        null,
                        change > 0
                            ? Lang.En ? $"📈 Market trend: {TrendName(key)} is increasingly in demand – prices will rise over the coming months." : $"📈 Markttrend: {TrendName(key)} ist zunehmend gefragt – die Preise ziehen in den nächsten Monaten an."
                            : Lang.En ? $"📉 Market trend: {TrendName(key)} is going out of fashion – prices will ease over the coming months." : $"📉 Markttrend: {TrendName(key)} kommt aus der Mode – die Preise geben in den nächsten Monaten nach.",
                        change > 0 ? LogTone.Good : LogTone.Bad);
                }
            }
            trend.Value = Round(trend.Value + (trend.Target - trend.Value) * Economy.TrendSpeed);
        }
    }

    /// <summary>Preisfaktor für einen Wein: Sekt und Rosé folgen ihrem eigenen Trend, alles andere dem der Rebsorte</summary>
    public static double TrendFactor(GameState state, WineInfo wine)
    {
        var sparkling = wine is BottleLot { Sparkling: true };
        return state.Trends[sparkling ? TrendKey.Sekt : wine.Rose ? TrendKey.Rose : (TrendKey)(int)wine.Grape].Value;
    }

    /// <summary>Für ältere Spielstände: fehlende Trends nachtragen</summary>
    public static void EnsureTrends(GameState state)
    {
        foreach (var key in TrendKeys)
        {
            if (!state.Trends.TryGetValue(key, out var trend) || trend is null) state.Trends[key] = new Trend { Value = 1, Target = 1 };
        }
    }

    /// <summary>Richtung für die Anzeige: steigend, fallend oder stabil</summary>
    public static TrendDirectionKind TrendDirection(Trend trend)
    {
        var gap = trend.Target - trend.Value;
        return gap > 0.03 ? TrendDirectionKind.Up : gap < -0.03 ? TrendDirectionKind.Down : TrendDirectionKind.Flat;
    }
}
