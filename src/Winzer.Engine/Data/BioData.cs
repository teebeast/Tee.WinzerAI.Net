namespace Winzer.Engine.Data;

public sealed record BioLabelInfo(
    string Name, string Icon,
    /* Vorstufe; null = Umstellungszeit ('conversion' im Original) */ BioLabel? Requires,
    /* Monate in der Vorstufe */ int WaitMonths,
    double Fee, double AnnualFee,
    /* Aufschlag auf alle Flaschenpreise */ double PriceBonus,
    /* Zusätzliche Ab-Hof-Nachfrage */ double DemandBonus,
    /* Bekanntheit bei Verleihung */ double Reputation,
    /* Güte-Zuschlag bei jeder Lese */ double Quality,
    double ProtectionCostFactor, double YieldFactor, string TextDe, string TextEn)
{
    public string Text => Lang.T(TextDe, TextEn);
}

/// <summary>data/bio.ts</summary>
public static class BioData
{
    public static readonly BioLabel[] BioOrder = [BioLabel.Basis, BioLabel.Verband, BioLabel.Biodynamisch];

    public static readonly Dictionary<BioLabel, BioLabelInfo> BioLabels = new()
    {
        [BioLabel.Basis] = new("Grünblatt", "🍃", null, Economy.BioConversionMonths, 1_500, 600, 0.1, 0, 2, 0, 1, 1,
            "Das gesetzliche Mindestmaß: kein chemisch-synthetischer Pflanzenschutz, regelmäßige Kontrollen.",
            "The legal minimum: no synthetic chemical crop protection, regular inspections."),
        [BioLabel.Verband] = new("Terravita", "🌿", BioLabel.Basis, 12, 3_000, 1_500, 0.18, 0.1, 4, 0, 1, 1,
            "Anbauverband mit strengeren Regeln als das Grünblatt-Siegel – bei Kunden bekannter und geschätzt.",
            "Growers' association with stricter rules than the Grünblatt seal – better known and valued by customers."),
        [BioLabel.Biodynamisch] = new("Lunaris", "🌙", BioLabel.Verband, 12, 5_000, 2_500, 0.28, 0.15, 8, 2, 1.25, 0.95,
            "Biodynamisch: Präparate, Mondkalender, viel Handarbeit. Etwas weniger Ertrag, dafür charaktervolle Weine.",
            "Biodynamic: preparations, lunar calendar, lots of manual work. Slightly lower yield, but wines with character."),
    };

    public static BioLabelInfo? BioInfo(Player player) => player.BioLabel is { } label ? BioLabels[label] : null;
}
