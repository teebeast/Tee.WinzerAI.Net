using System.Globalization;

namespace Winzer.Engine;

/// <summary>JavaScript-Verhalten, das die Regeln des Originals voraussetzen.</summary>
public static class Js
{
    /// <summary>Math.round: rundet .5 immer nach oben (auch bei negativen Zahlen).</summary>
    public static double Round(double value) => Math.Floor(value + 0.5);

    /// <summary>Number.MAX_SAFE_INTEGER</summary>
    public const double MaxSafeInteger = 9007199254740991;
}

public static class Util
{
    const int MaxLog = 400;

    static readonly NumberFormatInfo German = new()
    {
        NumberGroupSeparator = ".",
        NumberDecimalSeparator = ",",
        NegativeSign = "-",
    };

    static readonly NumberFormatInfo English = new()
    {
        NumberGroupSeparator = ",",
        NumberDecimalSeparator = ".",
        NegativeSign = "-",
    };

    public static string NewId(GameState state, string prefix) => $"{prefix}{state.NextId++}";

    /// <summary><paramref name="playerId"/> null = öffentliche Meldung, die alle Spieler sehen</summary>
    public static void AddLog(GameState state, string? playerId, string text, LogTone? tone = null)
    {
        state.Log.Add(new LogEntry { Year = state.Year, Month = state.Month, PlayerId = playerId, Text = text, Tone = tone });
        if (state.Log.Count > MaxLog) state.Log.RemoveRange(0, state.Log.Count - MaxLog);
    }

    /// <summary>Wie toLocaleString('de-DE', Währung EUR, ohne Nachkommastellen): „1.234 €“ mit geschütztem Leerzeichen.</summary>
    public static string FormatEuro(double value) =>
        Lang.En ? (Math.Round(value) < 0 ? "-€" + FormatNumber(-value) : "€" + FormatNumber(value)) : FormatNumber(value) + " €";

    /// <summary>Zahl wie in einem JavaScript-Template (<c>`${x}`</c>): kürzeste eindeutige Darstellung mit Punkt.</summary>
    public static string JsNumber(double value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Wie toLocaleString('de-DE'): Tausenderpunkte, höchstens <paramref name="maxDecimals"/> Nachkommastellen.</summary>
    public static string FormatNumber(double value, int maxDecimals = 0)
    {
        var rounded = Math.Round(value, maxDecimals, MidpointRounding.AwayFromZero);
        if (rounded == 0) rounded = 0; // kein „-0“
        var format = maxDecimals == 0 ? "#,##0" : "#,##0." + new string('#', maxDecimals);
        return rounded.ToString(format, Lang.En ? English : German);
    }
}
