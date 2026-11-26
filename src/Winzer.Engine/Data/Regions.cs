namespace Winzer.Engine.Data;

/// <summary>Was ein Gebiet jenseits des Klimas ausmacht</summary>
public sealed record RegionCharacter(
    /* Anteil der Steillagen 0–1 */ double Slope,
    /* Renommee −1 bis +2 */ double Prestige,
    /* Weintourismus 0–2 */ double Tourism,
    /* Faktor auf die Erntemenge */ double YieldFactor,
    /* Faktor auf den Wert der Weinberge */ double LandPrice,
    string ProfileDe, string ProfileEn)
{
    public string Profile => Lang.T(ProfileDe, ProfileEn);
}

public sealed record Region(
    RegionId Id, string Name,
    /* 0–10: beschleunigt die Reife */ double Warmth,
    /* 0–10: Spätfrost, aber auch Chance auf Eiswein */ double FrostRisk,
    /* 0–10: Krankheitsdruck durch Nässe */ double Wetness,
    /* −2 bis +2 je Rebsorte */ Dictionary<GrapeId, double> Suitability,
    GrapeId StartGrape,
    RegionCharacter Character);

public static class Regions
{
    static Region Make(RegionId id, string name, double warmth, double frostRisk, double wetness, GrapeId startGrape,
        (GrapeId Grape, double Value)[] suitability, RegionCharacter character)
    {
        // Grundeignung: neutral, PIWIs gedeihen überall ordentlich
        var all = new Dictionary<GrapeId, double>
        {
            [GrapeId.MuellerThurgau] = 0, [GrapeId.Silvaner] = 0, [GrapeId.Riesling] = 0, [GrapeId.Weissburgunder] = 0,
            [GrapeId.Grauburgunder] = 0, [GrapeId.Spaetburgunder] = 0, [GrapeId.Dornfelder] = 0, [GrapeId.Trollinger] = -1,
            [GrapeId.Lemberger] = -1, [GrapeId.Regent] = 0, [GrapeId.Solaris] = 0,
        };
        foreach (var (grape, value) in suitability) all[grape] = value;
        return new Region(id, name, warmth, frostRisk, wetness, all, startGrape, character);
    }

    static RegionCharacter C(double slope, double prestige, double tourism, double yieldFactor, double landPrice, string profile, string profileEn) =>
        new(slope, prestige, tourism, yieldFactor, landPrice, profile, profileEn);

    const GrapeId MT = GrapeId.MuellerThurgau, Si = GrapeId.Silvaner, Ri = GrapeId.Riesling, WB = GrapeId.Weissburgunder,
        GB = GrapeId.Grauburgunder, SB = GrapeId.Spaetburgunder, Do = GrapeId.Dornfelder, Tr = GrapeId.Trollinger,
        Le = GrapeId.Lemberger, Re = GrapeId.Regent, So = GrapeId.Solaris;

    /// <summary>REGIONS des Originals, in derselben Reihenfolge.</summary>
    public static readonly Dictionary<RegionId, Region> All = new()
    {
        [RegionId.Ahr] = Make(RegionId.Ahr, "Ahr", 5, 5, 4, SB,
            [(SB, 2), (Ri, 0), (Do, 0), (MT, -1), (Si, -1)],
            C(0.6, 1, 2, 0.95, 1.1, "Kleines Rotweintal mit steilen Schieferhängen und vielen Ausflüglern.",
                "Small red wine valley with steep slate slopes and plenty of day-trippers.")),
        [RegionId.Baden] = Make(RegionId.Baden, "Baden", 9, 2, 6, GB,
            [(GB, 2), (WB, 2), (SB, 2), (MT, 1), (Re, 1), (So, 1)],
            C(0.2, 1, 1, 1.0, 1.0, "Sonnigstes Gebiet: Burgunder in Hülle und Fülle, solide Nachfrage.",
                "Sunniest region: Burgundy varieties galore, solid demand.")),
        [RegionId.Franken] = Make(RegionId.Franken, "Franken", 5, 6, 3, Si,
            [(Si, 2), (MT, 2), (Ri, 1), (SB, 1), (So, 1)],
            C(0.3, 1, 1, 0.95, 0.9, "Silvaner im Bocksbeutel, eigenwillig und bodenständig – Frost ist ein Thema.",
                "Silvaner in the Bocksbeutel, quirky and down-to-earth – frost is a concern.")),
        [RegionId.HessischeBergstrasse] = Make(RegionId.HessischeBergstrasse, "Hessische Bergstraße", 7, 3, 5, Ri,
            [(Ri, 2), (GB, 1), (SB, 1), (WB, 1)],
            C(0.3, 0, 1, 0.95, 0.9, "Kleines, mildes Gebiet an der Bergstraße – wenig bekannt, gute Rieslinge.",
                "Small, mild region along the Bergstraße – little known, good Rieslings.")),
        [RegionId.Mittelrhein] = Make(RegionId.Mittelrhein, "Mittelrhein", 5, 5, 5, Ri,
            [(Ri, 2), (Si, -1), (Do, -1)],
            C(0.8, 1, 2, 0.9, 0.9, "Fast nur Steillagen im romantischen Welterbe-Rheintal: viele Touristen, viel Handarbeit.",
                "Almost all steep sites in the romantic World Heritage Rhine valley: many tourists, lots of manual work.")),
        [RegionId.Mosel] = Make(RegionId.Mosel, "Mosel", 4, 6, 5, Ri,
            [(Ri, 2), (Si, -1), (Do, -1), (GB, -1), (Tr, -2), (So, 1)],
            C(0.7, 2, 2, 0.95, 1.1, "Weltberühmter Riesling aus Steilstlagen – teuer zu bewirtschaften, aber begehrt.",
                "World-famous Riesling from the steepest sites – costly to farm, but sought after.")),
        [RegionId.Nahe] = Make(RegionId.Nahe, "Nahe", 6, 4, 4, Ri,
            [(Ri, 2), (MT, 1), (Si, 1), (Do, 1), (WB, 1), (GB, 1)],
            C(0.4, 1, 0, 0.95, 0.9, "Vielfältige Böden, feine Rieslinge – abseits der großen Touristenströme.",
                "Varied soils, fine Rieslings – away from the big tourist crowds.")),
        [RegionId.Pfalz] = Make(RegionId.Pfalz, "Pfalz", 9, 2, 3, Do,
            [(Do, 2), (Ri, 1), (SB, 1), (WB, 1), (GB, 1), (Tr, 0), (Le, 1), (Re, 1)],
            C(0.05, 1, 2, 1.1, 1.0, "Warm, flach, ertragreich – und die Weinstraße lockt die Gäste in Scharen.",
                "Warm, flat, high-yielding – and the Weinstraße draws guests in droves.")),
        [RegionId.Rheingau] = Make(RegionId.Rheingau, "Rheingau", 6, 4, 4, Ri,
            [(Ri, 2), (SB, 2), (MT, -1), (Do, -1)],
            C(0.3, 2, 1, 0.95, 1.3, "Adelige Rieslinglagen mit großem Namen – Boden ist hier besonders teuer.",
                "Noble Riesling sites with a great name – land is especially expensive here.")),
        [RegionId.Rheinhessen] = Make(RegionId.Rheinhessen, "Rheinhessen", 7, 3, 3, Si,
            [(MT, 2), (Si, 2), (Ri, 1), (Do, 1), (WB, 1), (GB, 1), (Tr, 0), (Le, 0), (Re, 1)],
            C(0.05, 0, 0, 1.15, 0.9, "Größtes Gebiet: sanfte Hügel, hohe Erträge, günstiges Land – aber wenig Glamour.",
                "Largest region: gentle hills, high yields, cheap land – but little glamour.")),
        [RegionId.SaaleUnstrut] = Make(RegionId.SaaleUnstrut, "Saale-Unstrut", 4, 8, 2, WB,
            [(MT, 2), (WB, 2), (Si, 1), (So, 2), (Re, 1), (SB, -1), (Tr, -2)],
            C(0.4, -1, 1, 0.9, 0.85, "Nördlichster Weinbau: kleine Erträge, günstiger Boden, ein echter Geheimtipp.",
                "Northernmost winegrowing: small yields, cheap land, a real insider tip.")),
        [RegionId.Sachsen] = Make(RegionId.Sachsen, "Sachsen", 4, 7, 3, Ri,
            [(MT, 2), (Ri, 1), (WB, 1), (GB, 1), (So, 2), (Re, 1), (Tr, -2)],
            C(0.5, 0, 1, 0.9, 0.9, "Terrassen an der Elbe: rare, gesuchte Weine, günstiges Land, harte Winter.",
                "Terraces on the Elbe: rare, sought-after wines, cheap land, harsh winters.")),
        [RegionId.Wuerttemberg] = Make(RegionId.Wuerttemberg, "Württemberg", 6, 4, 5, Le,
            [(Tr, 2), (Le, 2), (Ri, 1), (SB, 1), (Do, 1), (Re, 1)],
            C(0.4, 0, 1, 1.0, 1.0, "Trollinger und Lemberger, viel davon im eigenen Land getrunken.",
                "Trollinger and Lemberger, much of it drunk locally.")),
    };
}
