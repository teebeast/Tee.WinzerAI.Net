using System.Text.Json.Serialization;

namespace Winzer.Engine;

// Meilensteine: Zwischenziele quer durchs Spiel, vor allem fürs Endlosspiel. Jeder Meilenstein bringt ein paar Punkte
// und etwas Bekanntheit und wird mit Datum in der Chronik des Weinguts festgehalten – unabhängig vom Logbuch.

public enum Tier
{
    [JsonStringEnumMemberName("bronze")] Bronze,
    [JsonStringEnumMemberName("silber")] Silber,
    [JsonStringEnumMemberName("gold")] Gold,
}

public sealed class MilestoneDef
{
    public required string Id { get; init; }
    public required string Icon { get; init; }
    public required string NameDe { get; init; }
    public required string NameEn { get; init; }
    public required string TextDe { get; init; }
    public required string TextEn { get; init; }
    public string Name => Lang.T(NameDe, NameEn);
    public string Text => Lang.T(TextDe, TextEn);
    public required Tier Tier { get; init; }
    /// <summary>Fortschritt als [erreicht, Ziel]</summary>
    public required Func<GameState, Player, (double Value, double Target)> Progress { get; init; }
}

public sealed record TierReward(double Points, double Reputation, string Icon, string NameDe, string NameEn)
{
    public string Name => Lang.T(NameDe, NameEn);
}

public static class Milestones
{
    public static readonly Dictionary<Tier, TierReward> TierRewards = new()
    {
        [Tier.Bronze] = new(1, 1, "🥉", "Bronze", "Bronze"),
        [Tier.Silber] = new(3, 2, "🥈", "Silber", "Silver"),
        [Tier.Gold] = new(5, 4, "🥇", "Gold", "Gold"),
    };

    public static PlayerStats EmptyStats() => new()
    {
        Harvests = 0, BestLevelRank = -1, Edelsuess = 0, Sekt = 0, Barrique = 0, BottlesSold = 0,
        Medals = 0, GoldMedals = 0, OrdersDelivered = 0, ExportsCompleted = 0, AuctionsWon = 0,
    };

    static Func<GameState, Player, (double Value, double Target)> Stat(Func<PlayerStats, double> key, double target) =>
        (_, p) => (key(p.Stats), target);

    static Func<GameState, Player, (double Value, double Target)> Flag(Func<GameState, Player, bool> test) =>
        (s, p) => (test(s, p) ? 1 : 0, 1);

    /// <summary>MILESTONES des Originals (der Name Milestones gehört der Klasse).</summary>
    public static readonly List<MilestoneDef> All =
    [
        // Weinberg und Keller
        new() { Id = "erste-lese", Icon = "✂️", NameDe = "Erste Lese", NameEn = "First Harvest", TextDe = "Den ersten eigenen Jahrgang gelesen.", TextEn = "Brought in your first own vintage.", Tier = Tier.Bronze, Progress = Stat(s => s.Harvests, 1) },
        new()
        {
            Id = "auslese", Icon = "🍇", NameDe = "Auslese", NameEn = "Auslese", TextDe = "Eine Lese mit mindestens Auslese-Mostgewicht eingebracht.", TextEn = "Brought in a harvest with at least Auslese must weight.", Tier = Tier.Silber,
            Progress = (_, p) => (Math.Min(p.Stats.BestLevelRank, Rules.LevelRank(QualityLevel.Auslese)), Rules.LevelRank(QualityLevel.Auslese)),
        },
        new() { Id = "edelsuess", Icon = "🍯", NameDe = "Edelsüß", NameEn = "Noble Sweet", TextDe = "Beerenauslese, Trockenbeerenauslese oder Eiswein gelesen.", TextEn = "Harvested Beerenauslese, Trockenbeerenauslese or Eiswein.", Tier = Tier.Gold, Progress = Stat(s => s.Edelsuess, 1) },
        new() { Id = "sekt", Icon = "🍾", NameDe = "Prickelnd", NameEn = "Sparkling", TextDe = "Den ersten Sekt angesetzt.", TextEn = "Started your first sparkling wine.", Tier = Tier.Bronze, Progress = Stat(s => s.Sekt, 1) },
        new() { Id = "barrique", Icon = "🛢️", NameDe = "Im Eichenfass", NameEn = "In the Oak Barrel", TextDe = "Den ersten Wein ins Barrique gelegt.", TextEn = "Put your first wine into a barrique.", Tier = Tier.Bronze, Progress = Stat(s => s.Barrique, 1) },
        new() { Id = "lesen-25", Icon = "🧺", NameDe = "Erfahrene Hand", NameEn = "Experienced Hand", TextDe = "25 Lesen eingebracht.", TextEn = "Brought in 25 harvests.", Tier = Tier.Silber, Progress = Stat(s => s.Harvests, 25) },

        // Wachstum
        new() { Id = "flaeche-20", Icon = "🗺️", NameDe = "Stattliches Gut", NameEn = "Stately Estate", TextDe = "20 Hektar Rebfläche.", TextEn = "20 hectares of vines.", Tier = Tier.Silber, Progress = (_, p) => (Market.TotalHectares(p), 20) },
        new() { Id = "flaeche-50", Icon = "🏞️", NameDe = "Großweingut", NameEn = "Large Estate", TextDe = "50 Hektar Rebfläche.", TextEn = "50 hectares of vines.", Tier = Tier.Gold, Progress = (_, p) => (Market.TotalHectares(p), 50) },
        new() { Id = "versteigerung", Icon = "🔨", NameDe = "Zuschlag!", NameEn = "Sold!", TextDe = "Einen Weinberg ersteigert.", TextEn = "Won a vineyard at auction.", Tier = Tier.Bronze, Progress = Stat(s => s.AuctionsWon, 1) },
        new()
        {
            Id = "vermoegen-1m", Icon = "💶", NameDe = "Millionär", NameEn = "Millionaire", TextDe = "Reinvermögen von 1 Mio. €.", TextEn = "Net worth of €1 million.", Tier = Tier.Silber,
            Progress = (_, p) => (Js.Round(Market.NetWorth(p) / 1000), 1000),
        },
        new()
        {
            Id = "vermoegen-5m", Icon = "💰", NameDe = "Weinbaron", NameEn = "Wine Baron", TextDe = "Reinvermögen von 5 Mio. €.", TextEn = "Net worth of €5 million.", Tier = Tier.Gold,
            Progress = (_, p) => (Js.Round(Market.NetWorth(p) / 1000), 5000),
        },
        new()
        {
            Id = "schuldenfrei", Icon = "🧾", NameDe = "Schuldenfrei", NameEn = "Debt-Free", TextDe = "Ab Jahr 3 ohne Kredit und mit mindestens 100.000 € auf dem Konto.", TextEn = "From year 3 on: no loan and at least €100,000 in the account.", Tier = Tier.Silber,
            Progress = Flag((s, p) => s.Year >= 3 && p.Loan == 0 && p.Money >= 100_000),
        },

        // Markt und Ansehen
        new() { Id = "flaschen-10k", Icon = "🍷", NameDe = "Gut im Geschäft", NameEn = "Doing Well", TextDe = "10.000 Flaschen verkauft.", TextEn = "Sold 10,000 bottles.", Tier = Tier.Bronze, Progress = Stat(s => s.BottlesSold, 10_000) },
        new() { Id = "flaschen-200k", Icon = "🚚", NameDe = "Marktgröße", NameEn = "Market Giant", TextDe = "200.000 Flaschen verkauft.", TextEn = "Sold 200,000 bottles.", Tier = Tier.Gold, Progress = Stat(s => s.BottlesSold, 200_000) },
        new() { Id = "medaille", Icon = "🏅", NameDe = "Prämiert", NameEn = "Award-Winning", TextDe = "Die erste Medaille gewonnen.", TextEn = "Won your first medal.", Tier = Tier.Bronze, Progress = Stat(s => s.Medals, 1) },
        new() { Id = "gold", Icon = "🥇", NameDe = "Goldrichtig", NameEn = "Pure Gold", TextDe = "Die erste Goldmedaille gewonnen.", TextEn = "Won your first gold medal.", Tier = Tier.Silber, Progress = Stat(s => s.GoldMedals, 1) },
        new() { Id = "medaillen-10", Icon = "🎖️", NameDe = "Medaillensammler", NameEn = "Medal Collector", TextDe = "10 Medaillen gewonnen.", TextEn = "Won 10 medals.", Tier = Tier.Gold, Progress = Stat(s => s.Medals, 10) },
        new() { Id = "auftraege-5", Icon = "📦", NameDe = "Verlässlicher Lieferant", NameEn = "Reliable Supplier", TextDe = "5 Kundenaufträge geliefert.", TextEn = "Delivered 5 customer orders.", Tier = Tier.Silber, Progress = Stat(s => s.OrdersDelivered, 5) },
        new() { Id = "export", Icon = "🌍", NameDe = "Weltweit", NameEn = "Worldwide", TextDe = "Einen Exportvertrag vollständig erfüllt.", TextEn = "Fully completed an export contract.", Tier = Tier.Silber, Progress = Stat(s => s.ExportsCompleted, 1) },
        new() { Id = "bekanntheit-50", Icon = "⭐", NameDe = "Gefragt", NameEn = "In Demand", TextDe = "Bekanntheit 50.", TextEn = "Reputation 50.", Tier = Tier.Silber, Progress = (_, p) => (Math.Floor(p.Reputation), 50) },
        new() { Id = "bekanntheit-80", Icon = "🌟", NameDe = "Kultweingut", NameEn = "Cult Estate", TextDe = "Bekanntheit 80.", TextEn = "Reputation 80.", Tier = Tier.Gold, Progress = (_, p) => (Math.Floor(p.Reputation), 80) },

        // Betrieb
        new() { Id = "vinothek", Icon = "🏠", NameDe = "Probierstube", NameEn = "Tasting Room", TextDe = "Eine Vinothek eröffnet.", TextEn = "Opened a wine room.", Tier = Tier.Bronze, Progress = Flag((_, p) => p.HasVinothek) },
        new() { Id = "onlineshop", Icon = "🛒", NameDe = "Online", NameEn = "Online", TextDe = "Einen Online-Shop gestartet.", TextEn = "Launched an online shop.", Tier = Tier.Bronze, Progress = Flag((_, p) => p.HasOnlineShop) },
        new() { Id = "bio", Icon = "🌿", NameDe = "Bio-zertifiziert", NameEn = "Certified Organic", TextDe = "Das erste Bio-Siegel erhalten.", TextEn = "Received your first organic seal.", Tier = Tier.Silber, Progress = Flag((_, p) => p.BioLabel is not null) },
        new() { Id = "lunaris", Icon = "🌙", NameDe = "Biodynamisch", NameEn = "Biodynamic", TextDe = "Das Lunaris-Siegel erhalten.", TextEn = "Received the Lunaris seal.", Tier = Tier.Gold, Progress = Flag((_, p) => p.BioLabel == BioLabel.Biodynamisch) },
        new()
        {
            Id = "zehn-jahre", Icon = "🎂", NameDe = "Zehn Jahre", NameEn = "Ten Years", TextDe = "Zehn Jahre lang ein Weingut geführt.", TextEn = "Ran an estate for ten years.", Tier = Tier.Silber,
            Progress = (s, _) => (Math.Min(10, s.Year - 1), 10),
        },
    ];

    public static bool IsReached(GameState state, Player player, MilestoneDef def)
    {
        var (value, target) = def.Progress(state, player);
        return value >= target;
    }

    /// <summary>
    /// Monatsende: neu erreichte Meilensteine eintragen und belohnen. <paramref name="silent"/> trägt nur ein – für alte
    /// Spielstände, damit dort nichts rückwirkend Punkte bringt.
    /// </summary>
    public static void CheckMilestones(GameState state, bool silent = false)
    {
        var now = Bio.GameMonth(state);
        foreach (var player in state.Players.Where(p => !p.Bankrupt).ToList())
        {
            foreach (var def in All)
            {
                if (player.Milestones.ContainsKey(def.Id) || !IsReached(state, player, def)) continue;
                player.Milestones[def.Id] = now;
                if (silent) continue;
                var reward = TierRewards[def.Tier];
                player.Points += reward.Points;
                Rules.ChangeReputation(player, reward.Reputation);
                Util.AddLog(
                    state,
                    def.Tier == Tier.Gold ? null : player.Id,
                    Lang.En
                        ? $"🏆 Milestone {reward.Icon} “{def.Name}”{(def.Tier == Tier.Gold ? $" for {player.Name}" : "")}: {def.Text} +{Util.JsNumber(reward.Points)} points."
                        : $"🏆 Meilenstein {reward.Icon} „{def.Name}“{(def.Tier == Tier.Gold ? $" für {player.Name}" : "")}: {def.Text} +{Util.JsNumber(reward.Points)} Punkte.",
                    LogTone.Good);
            }
        }
    }
}
