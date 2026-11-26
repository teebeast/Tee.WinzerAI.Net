using Winzer.Engine.Data;

namespace Winzer.Engine;

/// <summary>Jahresziel: eines von drei Angeboten im Januar, Abrechnung im Dezember</summary>
public sealed record GoalDef(
    string Id, string TitleDe, string TitleEn, double Points,
    Func<GameState, Player, string> Text,
    Func<GameState, Player, bool> Available,
    Func<GameState, Player, (double Value, double Target)> Progress)
{
    public string Title => Lang.T(TitleDe, TitleEn);
}

/// <summary>
/// Ambition: geheimes Fernziel, das beim Erreichen einmalig Punkte bringt. Die Punkte richten sich danach, wie schwer
/// das Ziel zu erreichen ist – wer viel will, muss viel wagen.
/// </summary>
public sealed record AmbitionDef(
    string Id, string Icon, string NameDe, string NameEn, string TextDe, string TextEn, double Points,
    Func<GameState, Player, (double Value, double Target)> Progress)
{
    public string Name => Lang.T(NameDe, NameEn);
    public string Text => Lang.T(TextDe, TextEn);

    /// <summary>Einstufung für die Anzeige, abgeleitet aus den Punkten</summary>
    public string Difficulty => Points >= 50 ? Lang.T("schwer", "hard") : Points >= 25 ? Lang.T("mittel", "medium") : Lang.T("leicht", "easy");
}

/// <summary>Ausbau des Guts</summary>
public sealed record BuildingDef(string Id, string Icon, string NameDe, string NameEn, double Cost, double MonthlyCost, string TextDe, string TextEn)
{
    public string Name => Lang.T(NameDe, NameEn);
    public string Text => Lang.T(TextDe, TextEn);
}

/// <summary>
/// Regelversion 2: Erweiterungen gegenüber dem Original. Alles hier greift nur, wenn
/// <see cref="GameState.RulesVersion"/> mindestens 2 ist – Partien nach Originalregeln verlaufen unverändert.
/// <list type="bullet">
/// <item>Ab-Hof-Preise selbst setzen: Aufschlag gegen Nachfrage</item>
/// <item>Jahresziele und Jahresrückblick</item>
/// <item>Ambitionen als geheime Fernziele</item>
/// <item>Gutsausbau und Stiftung als Verwendung für Vermögen</item>
/// <item>Prämierung mit drei Juroren, deren Noten einzeln festgehalten werden</item>
/// <item>Lese in Halbmonaten (September bis November), Januar und Februar als ein Winterzug</item>
/// </list>
/// </summary>
public static class RulesV2
{
    public static bool On(GameState state) => state.RulesVersion >= 2;

    // ---- Zeit: Halbmonate und Winterzug ----

    public static bool HasHalves(int month) => month is 9 or 10 or 11;

    /// <summary>Bezeichnung des laufenden Zugs, z. B. „Oktober, 1. Hälfte“</summary>
    public static string TurnName(GameState state)
    {
        var month = Game.MonthNames[state.Month - 1];
        if (!On(state)) return month;
        if (state.Month == 1) return Lang.T("Winter (Januar/Februar)", "Winter (January/February)");
        return state.HalfMonth > 0 ? (Lang.En ? $"{month}, {(state.HalfMonth == 1 ? "1st" : "2nd")} half" : $"{month}, {state.HalfMonth}. Hälfte") : month;
    }

    /// <summary>Bezeichnung des nächsten Zugs für die Schaltfläche „weiter“</summary>
    public static string NextTurnName(GameState state)
    {
        if (!On(state)) return Game.MonthNames[state.Month % 12];
        if (state.HalfMonth == 1) return (Lang.En ? $"the 2nd half of {Game.MonthNames[state.Month - 1]}" : $"die 2. {Game.MonthNames[state.Month - 1]}hälfte");
        var next = state.Month == 1 ? 3 : state.Month % 12 + 1;
        if (next == 1) return "Winter";
        return HasHalves(next) ? (Lang.En ? $"{Game.MonthNames[next - 1]} (1st half)" : $"{Game.MonthNames[next - 1]} (1. Hälfte)") : Game.MonthNames[next - 1];
    }

    /// <summary>Erste Monatshälfte: die halbe Reife dieses Monats beim Wetter der ersten Hälfte, sonst nichts.</summary>
    public static void GrowFirstHalf(GameState state, Player player, Plot plot, WeatherKind weather)
    {
        plot.Oechsle += Vineyard.RipeningGain(state, player, plot, weather) / 2;
    }

    /// <summary>
    /// Zweite Monatshälfte: Krankheiten, Fäulnis und Edelfäule wie in einem ganzen Monat (einmal gewürfelt, mit dem
    /// Wetter der zweiten Hälfte), die Reife aber nur zur Hälfte – die andere Hälfte kam schon in der ersten.
    /// </summary>
    public static void GrowHalf(GameState state, Player player, Plot plot, WeatherKind weather)
    {
        var gain = Vineyard.RipeningGain(state, player, plot, weather);
        Vineyard.GrowPlot(state, player, plot, weather);
        plot.Oechsle -= gain / 2;
    }

    /// <summary>Ende der ersten Monatshälfte: Wetter und halber Reifeschritt, sonst nichts.</summary>
    public static void ResolveFirstHalf(GameState state)
    {
        var weather = Weather.DrawWeather(state.Rng, state.Forecast);
        state.LastWeather = weather;
        state.RipeningWeather.Add(weather);
        Util.AddLog(state, null, (Lang.En ? $"Weather in the first half of {Game.MonthNames[state.Month - 1]}: {Weather.WeatherLabels[weather]}." : $"Wetter in der ersten {Game.MonthNames[state.Month - 1]}hälfte: {Weather.WeatherLabels[weather]}."));
        foreach (var player in state.Players)
        {
            if (player.Bankrupt) continue;
            foreach (var plot in player.Plots) GrowFirstHalf(state, player, plot, weather);
        }
        state.HalfMonth = 2;
        state.Forecast = Weather.MakeForecast(state.Rng, state.Month, 0.8);
    }

    // ---- Preise ----

    public const double MinPriceLevel = -0.2;
    public const double MaxPriceLevel = 0.4;

    /// <summary>Wie die Ab-Hof-Nachfrage auf den eigenen Preis reagiert: Bekannte Güter können mehr verlangen.</summary>
    public static double PriceDemandFactor(Player player)
    {
        var level = player.PriceLevel;
        if (level >= 0)
        {
            var elasticity = Math.Max(1.2, 2.2 - player.Reputation / 100);
            return Math.Max(0.15, 1 - elasticity * level);
        }
        return 1 + 1.5 * -level;
    }

    /// <summary>Ab-Hof-Nachfrage dieses Monats in Flaschen</summary>
    public static double DirectDemand(GameState state, Player player)
    {
        var demand = Market.MonthlyDemand(player);
        if (!On(state)) return demand;
        if (player.Buildings.Contains("schaenke")) demand *= 1.25;
        return Js.Round(demand * PriceDemandFactor(player));
    }

    /// <summary>Faktor auf den Ab-Hof-Preis je Flasche</summary>
    public static double DirectPriceFactor(GameState state, Player player)
    {
        if (!On(state)) return 1;
        return (1 + player.PriceLevel) * (player.Buildings.Contains("schatzkammer") ? 1.1 : 1);
    }

    // ---- Jahresziele ----

    static double Start(Player p, string key) => p.YearStart.GetValueOrDefault(key);

    static double SalesTarget(Player p) => Math.Max(2000, Js.Round((1500 + 500 * Start(p, "planted")) / 500) * 500);

    public static readonly GoalDef[] Goals =
    [
        new("medaillen", "Zwei Medaillen", "Two medals", 12,
            (s, p) => Lang.T("Gewinne in diesem Jahr zwei Medaillen.", "Win two medals this year."),
            (s, p) => s.Year >= 2,
            (s, p) => (p.Stats.Medals - Start(p, "medals"), 2)),
        new("absatz", "Flaschen verkaufen", "Sell bottles", 8,
            (s, p) => (Lang.En ? $"Sell {Util.FormatNumber(SalesTarget(p))} bottles this year." : $"Verkaufe in diesem Jahr {Util.FormatNumber(SalesTarget(p))} Flaschen."),
            (s, p) => s.Year >= 2,
            (s, p) => (p.Stats.BottlesSold - Start(p, "bottlesSold"), SalesTarget(p))),
        new("schulden", "Schulden halbieren", "Halve your debt", 8,
            (s, p) => (Lang.En ? $"Reduce your loan to at most {Util.FormatEuro(Math.Floor(Start(p, "loan") / 2))} by year end." : $"Senke den Kredit bis Jahresende auf höchstens {Util.FormatEuro(Math.Floor(Start(p, "loan") / 2))}."),
            (s, p) => p.Loan >= 20_000,
            (s, p) => (Math.Max(0, Start(p, "loan") - p.Loan), Math.Ceiling(Start(p, "loan") / 2))),
        new("bekanntheit", "Bekannter werden", "Become better known", 8,
            (s, p) => (Lang.En ? $"Reach a reputation of {Util.JsNumber(Js.Round(Start(p, "reputation")) + 6)} by year end." : $"Erreiche zum Jahresende eine Bekanntheit von {Util.JsNumber(Js.Round(Start(p, "reputation")) + 6)}."),
            (s, p) => p.Reputation < 80,
            (s, p) => (Js.Round(p.Reputation), Js.Round(Start(p, "reputation")) + 6)),
        new("flaeche", "Rebfläche vergrößern", "Expand vineyard area", 6,
            (s, p) => Lang.T("Pflanze eine Brache oder ersteigere einen bestockten Weinberg.", "Plant a fallow plot or win a planted vineyard at auction."),
            (s, p) => true,
            (s, p) => (Market.PlantedHectares(p) > Start(p, "planted") ? 1 : 0, 1)),
        new("praedikat", "Eine Auslese lesen", "Harvest an Auslese", 10,
            (s, p) => Lang.T("Lies in diesem Jahr eine Auslese oder höher.", "Harvest an Auslese or higher this year."),
            (s, p) => p.Plots.Any(plot => plot.Grape is not null && Rules.PlotAgeFactor(plot, s.Year) > 0),
            (s, p) => (Start(p, "bestThisYear") >= Rules.LevelRank(QualityLevel.Auslese) ? 1 : 0, 1)),
        new("vermoegen", "Vermögen aufbauen", "Build wealth", 6,
            (s, p) => Lang.En ? $"Increase your net worth by {Util.FormatEuro(40_000)} this year." : "Steigere das Reinvermögen in diesem Jahr um 40.000 €.",
            (s, p) => true,
            (s, p) => (Market.NetWorth(p) - Start(p, "netWorth"), 40_000)),
        new("auftraege", "Zwei Aufträge liefern", "Deliver two orders", 8,
            (s, p) => Lang.T("Liefere in diesem Jahr zwei Kundenaufträge.", "Deliver two customer orders this year."),
            (s, p) => s.Year >= 2,
            (s, p) => (p.Stats.OrdersDelivered - Start(p, "orders"), 2)),
    ];

    public static GoalDef? Goal(string? id) => id is null ? null : Array.Find(Goals, g => g.Id == id);

    public static bool GoalReached(GameState state, Player player, GoalDef goal)
    {
        var (value, target) = goal.Progress(state, player);
        return value >= target;
    }

    /// <summary>Jahresbeginn: Kennzahlen festhalten und drei Ziele anbieten.</summary>
    public static void StartYear(GameState state)
    {
        state.RipeningWeather = [];
        foreach (var player in state.Players)
        {
            if (player.Bankrupt) continue;
            player.YearStart = new Dictionary<string, double>
            {
                ["medals"] = player.Stats.Medals,
                ["bottlesSold"] = player.Stats.BottlesSold,
                ["netWorth"] = Market.NetWorth(player),
                ["loan"] = player.Loan,
                ["reputation"] = player.Reputation,
                ["planted"] = Market.PlantedHectares(player),
                ["orders"] = player.Stats.OrdersDelivered,
                ["points"] = player.Points,
                ["bestThisYear"] = -1,
            };
            var pool = Goals.Where(g => g.Available(state, player)).Select(g => g.Id).ToList();
            var offers = new List<string>();
            while (offers.Count < 3 && pool.Count > 0)
            {
                var index = (int)Math.Floor(Rng.NextFloat(state.Rng) * pool.Count);
                offers.Add(pool[index]);
                pool.RemoveAt(index);
            }
            player.GoalOffers = offers;
            // Computergegner nehmen das erste Angebot
            player.Goal = player.Ai is not null ? offers.FirstOrDefault() : null;
        }
    }

    /// <summary>Note des Jahrgangs aus dem Wetter der Reifemonate</summary>
    public static int VintageStars(GameState state)
    {
        var sunny = state.RipeningWeather.Count(w => w == WeatherKind.Sonnig);
        var wet = state.RipeningWeather.Count(w => w == WeatherKind.Nass);
        var total = Math.Max(1, state.RipeningWeather.Count);
        return Math.Clamp((int)Js.Round(3 + 3.0 * (sunny - wet) / total), 1, 5);
    }

    /// <summary>Jahresende: Ziel abrechnen und den Rückblick festhalten.</summary>
    public static void EndYear(GameState state)
    {
        var ranking = state.Players.OrderByDescending(p => p.Points).ThenByDescending(Market.NetWorth).ToList();
        var stars = VintageStars(state);
        foreach (var player in state.Players)
        {
            if (player.Bankrupt) continue;
            if (player.Buildings.Contains("schatzkammer"))
            {
                player.Points += 5;
                Util.AddLog(state, player.Id, Lang.T("Die Schatzkammer mehrt den Ruf des Hauses: +5 Punkte.", "The treasury adds to the estate's good name: +5 points."), LogTone.Good);
            }
            var goal = Goal(player.Goal);
            var reached = goal is not null && GoalReached(state, player, goal);
            if (goal is not null && reached)
            {
                player.Points += goal.Points;
                Rules.ChangeReputation(player, 2);
                Util.AddLog(state, player.Id, (Lang.En ? $"Yearly goal reached: {goal.Title}. +{Util.JsNumber(goal.Points)} points." : $"Jahresziel erreicht: {goal.Title}. +{Util.JsNumber(goal.Points)} Punkte."), LogTone.Good);
            }
            else if (goal is not null)
            {
                Util.AddLog(state, player.Id, (Lang.En ? $"Yearly goal missed: {goal.Title}." : $"Jahresziel verfehlt: {goal.Title}."), LogTone.Bad);
            }
            player.Review = new YearReview
            {
                Year = state.Year,
                GoalText = goal?.Title ?? Lang.T("kein Ziel gewählt", "no goal chosen"),
                GoalReached = reached,
                GoalPoints = reached ? goal!.Points : 0,
                PointsGained = player.Points - Start(player, "points"),
                NetWorthChange = Market.NetWorth(player) - Start(player, "netWorth"),
                BottlesSold = player.Stats.BottlesSold - Start(player, "bottlesSold"),
                Medals = (int)(player.Stats.Medals - Start(player, "medals")),
                Rank = ranking.IndexOf(player) + 1,
                VintageStars = stars,
            };
            player.Goal = null;
            player.GoalOffers = [];
        }
    }

    // ---- Ambitionen ----

    public const int AmbitionCount = 2;
    const int CleanYears = 7;
    /// <summary>Fläche für den Großgrundbesitzer</summary>
    public const double EstateHectares = 20;

    // Punkte nach Schwierigkeit, gemessen an Probepartien über zehn Jahre mit vier Gütern im selben Gebiet:
    // - Saubermann tritt von selbst ein, wenn man nichts Unrechtes tut (praktisch immer, in Jahr 8).
    // - Sekt ist eine Frage des Entschlusses, sobald ein Kellermeister da ist.
    // - Zwei Exportverträge brauchen Bekanntheit und zwei Jahre lückenlose Lieferung.
    // - Lunaris erreicht, wer konsequent umstellt, um Jahr 6 – bezahlt aber mit Ertrag und Kosten.
    // - Drei Goldmedaillen mit einer Sorte schafften gut zwei Drittel der soliden Güter, im Mittel in Jahr 6.
    // - Land ist knapp: Rund 45 ha kommen in zehn Jahren für alle zusammen unter den Hammer. 30 ha erreichte kein
    //   solide wirtschaftendes Gut; wer es erzwang, ging in zwei von drei Partien pleite. 20 ha schaffte etwa jedes
    //   neunte Gut nebenbei und etwa jedes vierte, das gezielt bis zum Schätzwert mitbot.
    public static readonly AmbitionDef[] Ambitions =
    [
        new("sortenkoenig", "👑", "Sortenkönig", "Variety King", "Gewinne drei Goldmedaillen mit derselben Rebsorte.", "Win three gold medals with the same grape variety.", 30,
            (s, p) => (p.GoldByGrape.Count == 0 ? 0 : p.GoldByGrape.Values.Max(), 3)),
        new("grossgrund", "🗺️", "Großgrundbesitzer", "Landowner", $"Bewirtschafte {Util.JsNumber(EstateHectares)} Hektar.", $"Farm {Util.JsNumber(EstateHectares)} hectares.", 60,
            (s, p) => (Market.TotalHectares(p), EstateHectares)),
        new("biovorreiter", "🌙", "Bio-Vorreiter", "Organic Pioneer", "Erreiche das Lunaris-Siegel.", "Earn the Lunaris seal.", 30,
            (s, p) => (p.BioLabel is { } label ? Array.IndexOf(BioData.BioOrder, label) + 1 : 0, 3)),
        new("exportmeister", "🌍", "Exportmeister", "Export Master", "Erfülle zwei Exportverträge vollständig.", "Fully complete two export contracts.", 25,
            (s, p) => (p.Stats.ExportsCompleted, 2)),
        new("sektbaron", "🍾", "Sektbaron", "Sparkling Baron", "Setze viermal Sekt an.", "Start sparkling wine four times.", 20,
            (s, p) => (p.Stats.Sekt, 4)),
        new("saubermann", "🕊️", "Saubermann", "Clean Hands", $"Wirtschafte {CleanYears} Jahre, ohne je zu panschen oder zu sabotieren.", $"Run your estate for {CleanYears} years without ever adulterating wine or sabotaging.", 15,
            (s, p) => (p.Tainted ? 0 : Math.Min(CleanYears, s.Year - 1), CleanYears)),
    ];

    public static AmbitionDef? Ambition(string id) => Array.Find(Ambitions, a => a.Id == id);

    static readonly Dictionary<AiProfileId, string[]> AiAmbitions = new()
    {
        [AiProfileId.Tradition] = ["sortenkoenig", "saubermann"],
        [AiProfileId.Genossenschaft] = ["grossgrund", "sektbaron"],
        [AiProfileId.Bio] = ["biovorreiter", "saubermann"],
        [AiProfileId.Schlitzohr] = ["grossgrund", "exportmeister"],
    };

    static void CheckAmbitions(GameState state, Player player)
    {
        foreach (var id in player.Ambitions)
        {
            if (player.AmbitionsDone.Contains(id) || Ambition(id) is not { } ambition) continue;
            var (value, target) = ambition.Progress(state, player);
            if (value < target) continue;
            player.AmbitionsDone.Add(id);
            player.Points += ambition.Points;
            // Nur der Spieler selbst erfährt davon – Ambitionen bleiben geheim
            Util.AddLog(state, player.Id, (Lang.En ? $"{ambition.Icon} Ambition fulfilled: {ambition.Name}! +{Util.JsNumber(ambition.Points)} points." : $"{ambition.Icon} Ambition erfüllt: {ambition.Name}! +{Util.JsNumber(ambition.Points)} Punkte."), LogTone.Good);
        }
    }

    // ---- Gutsausbau und Stiftung ----

    public static readonly BuildingDef[] Buildings =
    [
        new("schaenke", "🍽️", "Gutsschänke", "Estate Tavern", 120_000, 800, "Ganzjähriger Ausschank: +25 % Ab-Hof-Nachfrage.", "Year-round wine service: +25 % cellar-door demand."),
        new("gaestehaus", "🛏️", "Gästehaus", "Guest House", 180_000, 0, "Zimmer für Weinreisende: Einnahmen von Mai bis Oktober, einmalig +3 Bekanntheit.", "Rooms for wine travelers: income from May to October, one-time +3 reputation."),
        new("brennerei", "⚗️", "Brennerei", "Distillery", 100_000, 0, "Tresterbrand aus jeder Lese: +250 € je gelesenem Hektar.", "Pomace brandy from every harvest: +€250 per harvested hectare."),
        new("schatzkammer", "🗝️", "Schatzkammer", "Treasury", 300_000, 0, "Raritätenkeller: +10 % auf Ab-Hof-Preise und jedes Jahr +5 Punkte.", "Cellar of rarities: +10 % on cellar-door prices and +5 points every year."),
    ];

    public static BuildingDef? Building(string id) => Array.Find(Buildings, b => b.Id == id);

    public const double DonationPoints = 5;

    /// <summary>Preis der nächsten Spende – jede weitere kostet ein Viertel mehr.</summary>
    public static double DonationCost(Player player) => Js.Round(40_000 * Math.Pow(1.25, player.Donations) / 1000) * 1000;

    /// <summary>Monatliche Einnahmen des Gästehauses</summary>
    public static double GuestHouseIncome(Player player, int month) =>
        month is >= 5 and <= 10 ? Js.Round(1_500 * (1 + 0.5 * Regions.All[player.RegionId].Character.Tourism)) : 0;

    // ---- Aktionen ----

    public static string? Validate(GameState state, Player player, GameAction action)
    {
        if (!On(state)) return Lang.T("Das gibt es nur mit den erweiterten Regeln.", "This is only available with the extended rules.");
        switch (action)
        {
            case GameAction.SetPriceLevel a:
                if (!double.IsFinite(a.Level) || a.Level < MinPriceLevel - 1e-9 || a.Level > MaxPriceLevel + 1e-9) return Lang.T("Der Preisaufschlag muss zwischen −20 % und +40 % liegen.", "The price markup must be between −20 % and +40 %.");
                return null;
            case GameAction.ChooseGoal a:
                if (player.Goal is not null) return Lang.T("Das Jahresziel steht schon fest.", "The yearly goal is already set.");
                if (!player.GoalOffers.Contains(a.GoalId)) return Lang.T("Dieses Ziel steht nicht zur Wahl.", "This goal is not on offer.");
                return null;
            case GameAction.OfferPlot a:
            {
                var plot = player.Plots.Find(p => p.Id == a.PlotId);
                if (plot is null) return Lang.T("Unbekannte Parzelle.", "Unknown plot.");
                if (plot.ForAuction) return OfferedText;
                // Mindestens eine Parzelle bleibt beim Gut
                if (player.Plots.Count(p => !p.ForAuction) <= 1) return Lang.T("Die letzte Parzelle kann nicht versteigert werden.", "The last plot cannot be auctioned.");
                return null;
            }
            case GameAction.WithdrawPlot a:
            {
                var plot = player.Plots.Find(p => p.Id == a.PlotId);
                if (plot is null) return Lang.T("Unbekannte Parzelle.", "Unknown plot.");
                if (!plot.ForAuction) return Lang.T("Die Parzelle ist nicht zur Versteigerung angemeldet.", "The plot is not registered for the auction.");
                if (OfferAnnounced(state, player, plot)) return Lang.T("Das Los ist schon angekündigt – jetzt entscheidet die Versteigerung.", "The lot has already been announced – the auction decides now.");
                return null;
            }
            case GameAction.ChooseAmbition a:
                if (Ambition(a.AmbitionId) is null) return Lang.T("Diese Ambition gibt es nicht.", "This ambition doesn't exist.");
                if (player.Ambitions.Contains(a.AmbitionId)) return Lang.T("Diese Ambition ist schon gewählt.", "This ambition is already chosen.");
                if (player.Ambitions.Count >= AmbitionCount) return (Lang.En ? $"You can't have more than {AmbitionCount} ambitions." : $"Mehr als {AmbitionCount} Ambitionen gibt es nicht.");
                return null;
            case GameAction.Construct a:
                if (Building(a.BuildingId) is not { } building) return Lang.T("Diesen Ausbau gibt es nicht.", "This upgrade doesn't exist.");
                if (player.Buildings.Contains(a.BuildingId)) return (Lang.En ? $"{building.Name} is already built." : $"{building.Name} steht bereits.");
                if (player.Money < building.Cost) return (Lang.En ? $"Not enough money (need {Util.FormatEuro(building.Cost)})." : $"Nicht genug Geld (benötigt {Util.FormatEuro(building.Cost)}).");
                return null;
            case GameAction.Donate:
                if (player.Money < DonationCost(player)) return (Lang.En ? $"Not enough money (need {Util.FormatEuro(DonationCost(player))})." : $"Nicht genug Geld (benötigt {Util.FormatEuro(DonationCost(player))}).");
                return null;
            default:
                return null;
        }
    }

    public static string OfferedText => Lang.T("Die Parzelle ist zur Versteigerung angemeldet.", "The plot is registered for the auction.");

    /// <summary>Ist das Los zu dieser Parzelle schon angekündigt? Dann lässt sich die Anmeldung nicht mehr zurückziehen.</summary>
    public static bool OfferAnnounced(GameState state, Player player, Plot plot) =>
        state.Auction?.Lots.Any(l => l.SellerId == player.Id && l.PlotId == plot.Id) == true;

    public static void Apply(GameState state, Player player, GameAction action)
    {
        switch (action)
        {
            case GameAction.OfferPlot a:
            {
                var plot = player.Plots.Find(p => p.Id == a.PlotId)!;
                plot.ForAuction = true;
                Util.AddLog(state, player.Id, (Lang.En ? $"{plot.Name} ({Util.JsNumber(plot.Hectares)} ha) is registered for the next auction – starting price {Util.FormatEuro(Auction.OfferStartPrice(player, plot, state.Year))}." : $"{plot.Name} ({Util.JsNumber(plot.Hectares)} ha) ist zur nächsten Versteigerung angemeldet – Ausruf bei {Util.FormatEuro(Auction.OfferStartPrice(player, plot, state.Year))}."));
                break;
            }
            case GameAction.WithdrawPlot a:
            {
                var plot = player.Plots.Find(p => p.Id == a.PlotId)!;
                plot.ForAuction = false;
                Util.AddLog(state, player.Id, (Lang.En ? $"{plot.Name} will not be auctioned after all." : $"{plot.Name} wird doch nicht versteigert."));
                break;
            }
            case GameAction.SetPriceLevel a:
                player.PriceLevel = Math.Clamp(Js.Round(a.Level * 100) / 100, MinPriceLevel, MaxPriceLevel);
                break;
            case GameAction.ChooseGoal a:
                player.Goal = a.GoalId;
                Util.AddLog(state, player.Id, (Lang.En ? $"Yearly goal: {Goal(a.GoalId)!.Title}." : $"Jahresziel: {Goal(a.GoalId)!.Title}."));
                break;
            case GameAction.ChooseAmbition a:
                player.Ambitions.Add(a.AmbitionId);
                break;
            case GameAction.Construct a:
            {
                var building = Building(a.BuildingId)!;
                player.Money -= building.Cost;
                player.Buildings.Add(building.Id);
                if (building.Id == "gaestehaus") Rules.ChangeReputation(player, 3);
                Util.AddLog(state, null, (Lang.En ? $"{player.Name} builds: {building.Name}." : $"{player.Name} baut: {building.Name}."));
                break;
            }
            case GameAction.Donate:
            {
                var cost = DonationCost(player);
                player.Money -= cost;
                player.Donations++;
                player.Points += DonationPoints;
                Util.AddLog(state, null, (Lang.En ? $"{player.Name} donates {Util.FormatEuro(cost)} to wine culture: +{Util.JsNumber(DonationPoints)} points." : $"{player.Name} stiftet {Util.FormatEuro(cost)} für die Weinkultur: +{Util.JsNumber(DonationPoints)} Punkte."));
                break;
            }
        }
    }

    // ---- Einhängepunkte im Spielablauf ----

    /// <summary>Neue Partie: Ambitionen der Computergegner, erstes Jahr.</summary>
    public static void StartGame(GameState state)
    {
        foreach (var player in state.Players)
        {
            if (player.Ai is { } profile) player.Ambitions = [.. AiAmbitions[profile]];
        }
        StartYear(state);
    }

    public static void OnHarvest(GameState state, Player player, Plot plot, HarvestPreview preview)
    {
        player.YearStart["bestThisYear"] = Math.Max(Start(player, "bestThisYear"), Rules.LevelRank(preview.Level));
        if (player.Buildings.Contains("brennerei"))
        {
            var income = Js.Round(250 * plot.Hectares);
            player.Money += income;
            Util.AddLog(state, player.Id, (Lang.En ? $"The distillery turns the pomace into brandy: +{Util.FormatEuro(income)}." : $"Die Brennerei macht aus dem Trester Brand: +{Util.FormatEuro(income)}."));
        }
    }

    /// <summary>Monatsabschluss je Weingut: Ausbauten, Ambitionen, Standard-Jahresziel.</summary>
    public static void Monthly(GameState state, Player player)
    {
        var income = player.Buildings.Contains("gaestehaus") ? GuestHouseIncome(player, state.Month) : 0;
        var costs = player.Buildings.Sum(id => Building(id)?.MonthlyCost ?? 0);
        if (income > 0)
        {
            player.Money += income;
            Util.AddLog(state, player.Id, (Lang.En ? $"Guest house: +{Util.FormatEuro(income)}." : $"Gästehaus: +{Util.FormatEuro(income)}."));
        }
        player.Money -= costs;
        // Wer bis Ende Februar kein Ziel wählt, bekommt das erste Angebot
        if (state.Month == 2 && player.Goal is null && player.GoalOffers.Count > 0) player.Goal = player.GoalOffers[0];
        CheckAmbitions(state, player);
    }

    /// <summary>Prämierung mit drei Juroren; die Wertung ist ihr Mittel.</summary>
    public static (double Score, ShowEntry Entry) Judge(GameState state, Player player, BottleLot lot)
    {
        var baseScore = Market.CompetitionBaseScore(lot);
        var jury = new List<double>();
        for (var i = 0; i < 3; i++) jury.Add(Js.Round((baseScore + Rng.NextRange(state.Rng, -5, 5)) * 10) / 10);
        var score = Js.Round(jury.Sum() / 3 * 10) / 10;
        var entry = new ShowEntry { PlayerId = player.Id, PlayerName = player.Name, Wine = Rules.WineLabel(lot), BaseScore = baseScore, Jury = jury, Score = score };
        state.LastShow?.Entries.Add(entry);
        return (score, entry);
    }

    /// <summary>Zusätzliche Hinweise für die Aufgabenliste</summary>
    public static IEnumerable<Hint> Hints(GameState state, Player player)
    {
        if (!On(state)) yield break;
        if (player.Ambitions.Count < AmbitionCount)
        {
            yield return new Hint { Area = HintArea.Hof, Important = true, Text = Lang.T("Wähle deine zwei Ambitionen – geheime Fernziele. Leichte bringen wenige Punkte, schwere viele.", "Choose your two ambitions – secret long-term goals. Easy ones give few points, hard ones many.") };
        }
        if (player.Goal is null && player.GoalOffers.Count > 0)
        {
            yield return new Hint { Area = HintArea.Hof, Important = true, Text = Lang.T("Wähle dein Jahresziel aus drei Angeboten.", "Choose your yearly goal from three offers.") };
        }
    }

    // ---- Computergegner ----

    /// <summary>Züge der Computergegner nach den erweiterten Regeln, vor ihrem gewohnten Ablauf.</summary>
    public static GameState AiTurn(GameState state)
    {
        var s = state;
        Player Me() => Rules.CurrentPlayer(s);
        void Act(GameAction action)
        {
            if (Game.ValidateAction(s, action) is null) s = Game.ApplyAction(s, action);
        }

        if (Me().Goal is null && Me().GoalOffers.Count > 0) Act(new GameAction.ChooseGoal(Me().GoalOffers[0]));

        // Preis nach Lagerstand: volles Lager billiger, knappes teurer; bekannte Güter und Bio verlangen mehr
        var me = Me();
        var stock = me.Bottles.Where(l => l.MonthsUntilReady == 0).Sum(l => l.Bottles);
        var demand = Math.Max(1, Market.MonthlyDemand(me));
        var months = stock / demand;
        var level = me.Ai switch
        {
            AiProfileId.Genossenschaft => 0,
            AiProfileId.Bio => me.BioLabel is not null ? 0.15 : 0.05,
            AiProfileId.Tradition => me.Reputation >= 40 ? 0.1 : 0,
            _ => 0.0,
        };
        if (months > 8) level -= 0.1;
        else if (months < 2) level += 0.1;
        level = Math.Clamp(Js.Round(level * 100) / 100, MinPriceLevel, MaxPriceLevel);
        if (Math.Abs(level - me.PriceLevel) > 0.001) Act(new GameAction.SetPriceLevel(level));

        // Ausbau nur mit dickem Polster, Spenden erst mit sehr dickem
        foreach (var building in Buildings.OrderBy(b => b.Cost))
        {
            if (!Me().Buildings.Contains(building.Id) && Me().Loan <= 0 && Me().Money >= building.Cost + 150_000)
            {
                Act(new GameAction.Construct(building.Id));
                break;
            }
        }
        if (Me().Loan <= 0 && Me().Money >= DonationCost(Me()) + 400_000) Act(new GameAction.Donate());
        return s;
    }
}
