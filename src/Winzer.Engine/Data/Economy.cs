namespace Winzer.Engine.Data;

public sealed record DifficultyInfo(string NameDe, string NameEn, string Icon, string DescriptionDe, string DescriptionEn, double StartMoney, double PriceFactor, double CostFactor)
{
    public string Name => Lang.T(NameDe, NameEn);
    public string Description => Lang.T(DescriptionDe, DescriptionEn);
}

public sealed record MarketingInfo(MarketingKind Kind, string NameDe, string NameEn, double Cost, double Reputation, int[] Months)
{
    public string Name => Lang.T(NameDe, NameEn);
}

public sealed record CompetitionInfo(
    CompetitionId Id, string NameDe, string NameEn, int Month, double Fee, int MaxEntries,
    Dictionary<Medal, double> Thresholds, Dictionary<Medal, double> Points, Dictionary<Medal, double> Reputation)
{
    public string Name => Lang.T(NameDe, NameEn);
}

public sealed record ProtectionInfo(string NameDe, string NameEn, double CostPerHa, double Protection)
{
    public string Name => Lang.T(NameDe, NameEn);
}

public sealed record AdulterationInfo(string NameDe, string NameEn, double VolumeFactor, double QualityDelta, int LevelSteps, double Detection)
{
    public string Name => Lang.T(NameDe, NameEn);
}

public sealed record SabotageInfo(string NameDe, string NameEn, double Cost)
{
    public string Name => Lang.T(NameDe, NameEn);
}

/// <summary>Startwerte fürs Balancing (economy.ts). Namen: GROSS_SCHREIBUNG des Originals in PascalCase.</summary>
public static class Economy
{
    public const double StartMoney = 80_000;
    public const double StartTankHl = 300;
    public const double StartReputation = 10;

    /// <summary>Schwierigkeitsgrad der menschlichen Spieler; Computergegner spielen immer mit Faktor 1.</summary>
    public static readonly Dictionary<Difficulty, DifficultyInfo> Difficulties = new()
    {
        [Difficulty.Leicht] = new("Leicht", "Easy", "🌱", "100.000 € Startkapital, 5 % höhere Erlöse, 5 % weniger Kosten", "€100,000 starting capital, 5% higher revenue, 5% lower costs", 100_000, 1.05, 0.95),
        [Difficulty.Normal] = new("Normal", "Normal", "🍷", "80.000 € Startkapital, 5 % niedrigere Erlöse", "€80,000 starting capital, 5% lower revenue", StartMoney, 0.95, 1),
        [Difficulty.Schwierig] = new("Schwierig", "Hard", "🔥", "70.000 € Startkapital, 10 % niedrigere Erlöse, 5 % höhere Kosten", "€70,000 starting capital, 10% lower revenue, 5% higher costs", 70_000, 0.9, 1.05),
    };

    public const double WorkerSalary = 2_500;
    /// <summary>Monatliche Kosten je ha: Brache bzw. Bewirtschaftung bestockter Fläche</summary>
    public const double UpkeepPerHa = 50;
    public const double VineyardCostPerHa = 350;
    public const double HarvestCostPerHa = 1_500;
    public const double OverflowPricePerLiter = 0.5;

    public const double BottlesPerHl = 133;
    public const double BottleCost = 0.6;
    public const double RegisterFee = 150;
    /// <summary>Mindestgröße einer Prüfcharge: 5 hl</summary>
    public const double RegisterMinBottles = 5 * BottlesPerHl;

    public const double TankCostPerHl = 200;
    public const double OnlineShopCost = 15_000;

    public const double WholesaleShare = 0.45;
    public const double DirectSalesCostShare = 0.2;
    public const double FarmShopBaseDemand = 150;
    public const double FarmShopDemandPerReputation = 30;
    public const double OnlineDemandPerReputation = 40;

    public const double ReputationDecayRate = 0.03;
    public const double ReputationSaturation = 120;

    public const double CellarMasterBaseSalary = 2_500;
    public const double CellarMasterSalaryPerTalent = 350;
    public const double CellarMasterSalaryPerSkill = 600;
    public const int CellarMasterYearsPerExperience = 4;
    public const int CellarMasterMaxExperience = 2;
    public const int CellarMasterCandidates = 3;

    public static readonly Dictionary<MarketingKind, MarketingInfo> Marketing = new()
    {
        [MarketingKind.Social] = new(MarketingKind.Social, "Social Media", "Social media", 1_000, 1, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]),
        [MarketingKind.Hoffest] = new(MarketingKind.Hoffest, "Weinprobe/Hoffest", "Tasting / estate festival", 5_000, 4, [5, 6, 7, 8, 9]),
        [MarketingKind.Messe] = new(MarketingKind.Messe, "Weinmesse", "Wine fair", 12_000, 8, [3]),
    };

    static Dictionary<Medal, double> Medals(double gold, double silber, double bronze) =>
        new() { [Medal.Gold] = gold, [Medal.Silber] = silber, [Medal.Bronze] = bronze };

    public static readonly Dictionary<CompetitionId, CompetitionInfo> Competitions = new()
    {
        [CompetitionId.Regional] = new(CompetitionId.Regional, "Regionale Weinprämierung", "Regional wine awards", 6, 100, 3,
            Medals(88, 80, 72), Medals(20, 10, 5), Medals(6, 3, 1)),
        [CompetitionId.Bund] = new(CompetitionId.Bund, "Große Rebenkrone", "Grand Vine Crown", 11, 300, 3,
            Medals(93, 86, 79), Medals(60, 30, 15), Medals(15, 8, 4)),
    };

    static readonly Dictionary<Medal, string> MedalNamesDe = new()
    {
        [Medal.Gold] = "Gold", [Medal.Silber] = "Silber", [Medal.Bronze] = "Bronze",
    };
    static readonly Dictionary<Medal, string> MedalNamesEn = new()
    {
        [Medal.Gold] = "Gold", [Medal.Silber] = "Silver", [Medal.Bronze] = "Bronze",
    };
    public static Dictionary<Medal, string> MedalNames => Lang.En ? MedalNamesEn : MedalNamesDe;

    public const double LoanInterestRate = 0.06;
    public const double OverdraftInterestRate = 0.12;
    public const double LoanToValue = 0.5;
    public const double LandValuePerHa = 25_000;
    public const double VinesValuePerHa = 15_000;
    public const double TankValuePerHl = 150;
    public const int InsolvencyMonths = 3;

    public const double SlopeUpkeepFactor = 0.25;
    public const double SlopeHarvestFactor = 0.35;
    public const double SlopeQuality = 4;
    public const double PrestigePricePerLevel = 0.05;
    public const double TourismDemandPerLevel = 0.1;

    /// <summary>Abstand zwischen zwei Versteigerungen in Monaten (zufällig)</summary>
    public static readonly (double Min, double Max) AuctionInterval = (8, 16);
    public const double AuctionStartShare = 0.7;
    public const double AuctionIncrementShare = 0.04;
    public static readonly (double Min, double Max) AuctionInvestorRange = (0.85, 1.35);
    public const double AuctionInvestorChance = 0.85;
    public const double PlotSaleShare = 0.9;
    /// <summary>Ausrufpreis einer selbst eingelieferten Parzelle als Anteil am Schätzwert – darunter wird nicht zugeschlagen</summary>
    public const double PlotOfferStartShare = 0.8;
    public const double ClearingCostPerHa = 2_000;
    public const double SiteQualityBonus = 4;

    public const double BarriqueCostPerHl = 600;
    public const double BarriqueValuePerHl = 300;
    public const int BarriqueMonths = 12;
    public const double BarriqueQualityRed = 6;
    public const double BarriqueQualityWhite = 2;

    public const double VinothekCost = 150_000;
    public const double VinothekMonthlyCost = 1_000;
    public const double VinothekDemandFactor = 1.3;
    public const double VinothekReputation = 5;

    public const double WholesaleSaturationBottles = 200_000;
    public const double WholesaleMinFactor = 0.5;

    public const int RoseMaturingMonths = 3;
    public static readonly int[] RoseSummerMonths = [5, 6, 7, 8];
    public const double RoseSummerBonus = 0.15;
    public static readonly int[] FederweisserMonths = [9, 10];
    public const double FederweisserPricePerL = 2.5;
    public const double FederweisserBaseHl = 8;
    public const double FederweisserHlPerReputation = 0.25;
    public static readonly int[] StraussStartMonths = [3, 4, 5, 6, 7, 8, 9, 10];
    public const int StraussMonths = 4;
    public const double StraussCost = 8_000;
    public const double StraussDemandFactor = 1.4;
    public const double StraussReputation = 3;

    public static readonly (double Min, double Max) TrendRange = (0.8, 1.3);
    public const double TrendSpeed = 0.08;
    public const double TrendShiftChance = 0.03;
    public const double TrendNewsThreshold = 0.15;

    public const double PiwiPriceFactor = 0.9;

    public const double ClimateWarmthPerYear = 0.1;
    public const double ClimateIceWineLossPerYear = 0.01;
    public const double ClimateHeatPerYear = 0.005;

    public const double HectaresPerWorker = 10;

    public static readonly Dictionary<ProtectionStrategy, ProtectionInfo> Protection = new()
    {
        [ProtectionStrategy.None] = new("Kein Pflanzenschutz", "No crop protection", 0, 0),
        [ProtectionStrategy.Conventional] = new("Konventionell", "Conventional", 600, 0.9),
        [ProtectionStrategy.Bio] = new("Bio", "Organic", 900, 0.7),
    };
    public static readonly int[] ProtectionMonths = [5, 6, 7, 8];
    public const int BioConversionMonths = 24;

    public static readonly int[] FrostMonths = [4, 5];
    public const double FrostChancePerRisk = 0.02;
    public const double FrostLoss = 0.4;
    public const double FrostCandlesPerHa = 400;
    public static readonly int[] HailMonths = [6, 7, 8];
    public const double HailChance = 0.05;
    public const double HailClimatePerYear = 0.002;
    public const double HailLoss = 0.5;
    public const double HailNetCostPerHa = 3_000;
    public const double HailNetProtection = 0.9;
    public static readonly int[] DroughtMonths = [7, 8];
    public const double DroughtChance = 0.08;
    public const double DroughtClimatePerYear = 0.008;
    public const double DroughtLoss = 0.15;
    public const double IrrigationCostPerHa = 2_500;
    public static readonly int[] InsuranceMonths = [1, 2, 3, 4];
    public const double InsuredValuePerHa = 8_000;
    public const double InsuranceDeductible = 0.2;
    public const double InsuranceBasePremium = 210;
    public const double InsurancePremiumPerFrostRisk = 85;

    public const double IceWineBaseChance = 0.2;
    public const double IceWineChancePerFrostRisk = 0.04;
    public const double IceWineYieldShare = 0.15;
    public const double IceWineMinOechsle = 80;

    public const double SektExtraCostPerBottle = 1.0;
    public const int SektMonths = 9;
    public const double SektQualityBonus = 3;
    public const double SektBottlePrice = 12;
    public const double SektCompetitionBonus = 6;
    public const double SektPointsHigh = 10;
    public const double SektPointsLow = 3;
    public const double SektHighQuality = 80;

    public static readonly Dictionary<AdulterationMethod, AdulterationInfo> Adulteration = new()
    {
        [AdulterationMethod.Wasser] = new("Strecken mit Wasser", "Watering down", 1.2, -15, 0, 0.15),
        [AdulterationMethod.Zucker] = new("Unerlaubte Aufzuckerung", "Illegal chaptalization", 1, 0, 1, 0.25),
        [AdulterationMethod.Glykol] = new("„Frostschutz-Süße“", "“Antifreeze sweetness”", 1, 0, 2, 0.4),
    };
    public const double ControlChance = 0.03;
    public const double ScandalFine = 20_000;
    public const double ScandalPoints = 50;
    public const double ScandalReputation = 40;

    public static readonly Dictionary<SabotageKind, SabotageInfo> Sabotage = new()
    {
        [SabotageKind.Geruecht] = new("Gerüchte streuen", "Spread rumors", 3_000),
        [SabotageKind.Weinberg] = new("Weinberg beschädigen", "Damage vineyard", 8_000),
    };
    public const double SabotageRumorReputation = 10;
    public const double SabotageVineyardLoss = 0.3;
    public const double SabotageDetection = 0.3;
    public const double SabotageFine = 25_000;
    public const double SabotageReputation = 30;
    public const int AutoSkipMonths = 12;
    public static readonly Dictionary<SabotageKind, double> SabotageDamages = new()
    {
        [SabotageKind.Geruecht] = 8_000, [SabotageKind.Weinberg] = 15_000,
    };
    public const double DetectiveCost = 4_000;
    public const double DetectiveSuccess = 0.6;
    public const double LawsuitCost = 2_000;
    public const double LawsuitSuccess = 0.8;
    public const double GuardCost = 3_000;
    public const int GuardMonths = 12;
    public const double GuardDetection = 0.9;

    public const double ExportMinReputation = 20;
    public const double ExportOfferChance = 0.08;
    public const double ExportCompletionPoints = 15;
    public const double ExportCompletionReputation = 5;
    public const double ExportPenaltyPerBottle = 2;
}
