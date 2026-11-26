namespace Winzer.Engine;

public static class Weather
{
    public static readonly WeatherKind[] WeatherKinds = [WeatherKind.Sonnig, WeatherKind.Wechselhaft, WeatherKind.Nass];

    static readonly Dictionary<WeatherKind, string> WeatherLabelsDe = new()
    {
        [WeatherKind.Sonnig] = "Sonnig",
        [WeatherKind.Wechselhaft] = "Wechselhaft",
        [WeatherKind.Nass] = "Nass",
    };

    static readonly Dictionary<WeatherKind, string> WeatherLabelsEn = new()
    {
        [WeatherKind.Sonnig] = "Sunny",
        [WeatherKind.Wechselhaft] = "Changeable",
        [WeatherKind.Nass] = "Wet",
    };

    public static Dictionary<WeatherKind, string> WeatherLabels => Lang.En ? WeatherLabelsEn : WeatherLabelsDe;

    /// <summary>Langjähriges Mittel je Monat (Index 0 = Januar) als [sonnig, wechselhaft, nass].</summary>
    static readonly double[][] Climate =
    [
        [0.2, 0.4, 0.4], [0.25, 0.4, 0.35], [0.3, 0.4, 0.3], [0.35, 0.4, 0.25],
        [0.4, 0.35, 0.25], [0.45, 0.35, 0.2], [0.5, 0.3, 0.2], [0.5, 0.3, 0.2],
        [0.45, 0.35, 0.2], [0.35, 0.35, 0.3], [0.2, 0.4, 0.4], [0.15, 0.4, 0.45],
    ];

    /// <summary>Wirkung des Wetters auf den Reifezuwachs.</summary>
    public static readonly Dictionary<WeatherKind, double> SunFactor = new()
    {
        [WeatherKind.Sonnig] = 1.3,
        [WeatherKind.Wechselhaft] = 1.0,
        [WeatherKind.Nass] = 0.7,
    };

    /// <summary>
    /// Prognose für einen Monat: das Klimamittel, zufällig verschoben.
    /// <paramref name="certainty"/> (0–1) bestimmt, wie weit sich die Prognose vom Mittel entfernt.
    /// </summary>
    public static WeatherOdds MakeForecast(Rng rng, int month, double certainty)
    {
        var baseOdds = Climate[month - 1];
        var raw = new double[3];
        for (var i = 0; i < 3; i++) raw[i] = Math.Max(0.05, baseOdds[i] + Rng.NextRange(rng, -0.25, 0.25) * certainty);
        var sum = 0.0;
        foreach (var p in raw) sum += p;
        var rounded = new double[3];
        for (var i = 0; i < 3; i++) rounded[i] = Js.Round(raw[i] / sum * 100) / 100;
        // Rundungsrest auf „wechselhaft“ buchen, damit die Summe exakt 1 ist.
        rounded[1] = Js.Round((1 - rounded[0] - rounded[2]) * 100) / 100;
        return new WeatherOdds { Sonnig = rounded[0], Wechselhaft = rounded[1], Nass = rounded[2] };
    }

    public static WeatherKind DrawWeather(Rng rng, WeatherOdds odds)
    {
        var roll = Rng.NextFloat(rng);
        foreach (var kind in WeatherKinds)
        {
            roll -= odds[kind];
            if (roll < 0) return kind;
        }
        return WeatherKind.Nass;
    }
}
