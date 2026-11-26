namespace Winzer.Engine;

/// <summary>
/// Sprache der sichtbaren Texte. Deutsch ist Standard; Englisch wählt man im Hauptmenü unter Einstellungen.
/// Texte ohne Platzhalter: <c>Lang.T("Weinberg", "Vineyard")</c>. Texte mit Platzhaltern:
/// <c>Lang.En ? $"{n} bottles" : $"{n} Flaschen"</c>, so entsteht nur die gebrauchte Fassung.
/// </summary>
public static class Lang
{
    public static bool En { get; set; }

    public static string T(string de, string en) => En ? en : de;
}
