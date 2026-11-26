using System.Globalization;
using Winzer.Engine.Data;

namespace Winzer.Engine;

public sealed record HarvestPreview(double Hectoliters, QualityLevel Level, double Quality, double Cost);

/// <summary>
/// Spielablauf (game.ts): neue Partie, Prüfen und Ausführen von Aktionen, Monatsabschluss.
/// Die bewährten Re-Exporte des Originals (currentPlayer, formatEuro, hasPointTarget …) liegen in Rules und Util.
/// </summary>
public static class Game
{
    /// <summary>Anteil der möglichen Menge je nach Lesemonat</summary>
    static readonly Dictionary<int, double> HarvestTiming = new() { [8] = 0.7, [9] = 0.9, [10] = 1, [11] = 1, [12] = 1 };

    static readonly string[] MonthNamesDe =
    [
        "Januar", "Februar", "März", "April", "Mai", "Juni",
        "Juli", "August", "September", "Oktober", "November", "Dezember",
    ];

    static readonly string[] MonthNamesEn =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    ];

    public static string[] MonthNames => Lang.En ? MonthNamesEn : MonthNamesDe;

    /// <summary>Number.isInteger</summary>
    static bool IsInteger(double value) => double.IsFinite(value) && Math.Floor(value) == value;

    public static GameState NewGame(NewGameOptions options)
    {
        var state = new GameState
        {
            Version = 17,
            Rng = new Rng { State = options.Seed },
            Year = 1,
            Month = 1,
            Players = [],
            CurrentPlayer = 0,
            Forecast = new WeatherOdds { Sonnig = 0, Wechselhaft = 0, Nass = 0 },
            LastWeather = null,
            TargetPoints = options.TargetPoints ?? 600,
            WinnerId = null,
            Finished = false,
            Auction = null,
            MonthsToAuction = 0,
            RandomEvents = options.RandomEvents ?? true,
            CellarMasterCandidates = [],
            Trends = [],
            BulkCrisisMonths = 0,
            ClimateChange = options.ClimateChange ?? true,
            YearLimit = options.YearLimit,
            Challenge = options.Challenge,
            Difficulty = options.Difficulty ?? Difficulty.Normal,
            NextId = 1,
            Log = [],
        };
        var start = options.Start ?? new StartSetup();
        var players = new List<Player>();
        for (var i = 0; i < options.Players.Count; i++)
        {
            var p = options.Players[i];
            var region = Regions.All[p.RegionId];
            var ai = p.Ai;
            Difficulty? difficulty = ai is not null ? null : state.Difficulty;
            // Szenarien geben das Startkapital vor, sonst gilt das des Schwierigkeitsgrads
            var money = start.Money ?? Economy.Difficulties[difficulty ?? Difficulty.Normal].StartMoney;
            var oldVines = start.Plots == StartPlots.Planted;
            var fallow = start.Plots == StartPlots.Fallow;
            players.Add(new Player
            {
                Id = $"p{i + 1}",
                Name = p.Name,
                RegionId = p.RegionId,
                Ai = ai,
                Difficulty = difficulty,
                Money = money,
                Points = 0,
                Reputation = start.Reputation ?? Economy.StartReputation,
                Loan = start.Loan ?? 0,
                TankCapacityHl = start.TankHl ?? Economy.StartTankHl,
                BarriqueCapacityHl = 0,
                HasOnlineShop = false,
                HasVinothek = false,
                WholesaleThisYear = 0,
                CellarMaster = null,
                Plots =
                [
                    MakePlot(state, "Hauslage", 4, fallow ? null : region.StartGrape, fallow ? null : -10, 0),
                    MakePlot(state, "Südhang", 3, oldVines ? region.StartGrape : null, oldVines ? -10 : null, 1),
                    MakePlot(state, "Talweg", 3, oldVines ? region.StartGrape : null, oldVines ? -10 : null, -1),
                ],
                Cellar = [],
                Bottles = [],
                MarketingThisMonth = [],
                CompetitionEntries = [],
                Protection = ProtectionStrategy.Conventional,
                BioSince = null,
                BioLabel = null,
                BioLabelSince = null,
                OrderOffers = [],
                Orders = [],
                Relations = [],
                Stats = Milestones.EmptyStats(),
                Milestones = [],
                FederweisserThisMonth = 0,
                StraussYear = null,
                AutoSkip = null,
                IntrigueThisMonth = false,
                ExportOffer = null,
                ExportContract = null,
                MonthsInsolvent = 0,
                Bankrupt = false,
                Effects = [],
                Decision = null,
                InsuredYear = null,
                FrostCandles = false,
            });
        }
        state.Players = players;
        Auction.ScheduleNextAuction(state);
        CellarMasters.RefreshCandidates(state);
        Trends.InitTrends(state);
        state.Forecast = Weather.MakeForecast(state.Rng, state.Month, 0.8);
        Util.AddLog(
            state,
            null,
            state.YearLimit is not (null or 0)
                ? (Lang.En ? $"Year 1 begins. Whoever has the most points after {state.YearLimit} years wins." : $"Jahr 1 beginnt. Wer nach {state.YearLimit} Jahren die meisten Punkte hat, gewinnt.")
                : Rules.IsEndless(state)
                    ? Lang.T("Jahr 1 beginnt. Endlosspiel: kein Punkteziel, kein Zeitlimit – baue dein Weingut in Ruhe aus.", "Year 1 begins. Endless game: no point goal, no time limit – build up your estate at your own pace.")
                    : (Lang.En ? $"Year 1 begins. Goal: {Util.JsNumber(state.TargetPoints)} points." : $"Jahr 1 beginnt. Ziel: {Util.JsNumber(state.TargetPoints)} Punkte."));
        state.RulesVersion = options.RulesVersion ?? 1;
        if (RulesV2.On(state)) RulesV2.StartGame(state);
        return state;
    }

    static Plot MakePlot(GameState state, string name, double hectares, GrapeId? grape, int? plantedYear, double site)
    {
        return new Plot
        {
            Id = Util.NewId(state, "plot"),
            Name = name,
            Hectares = hectares,
            Grape = grape,
            PlantedYear = plantedYear,
            Site = site,
            Oechsle = 0,
            Health = 1,
            CropFactor = 1,
            HarvestedThisYear = false,
            Botrytis = false,
            IceWineGamble = false,
        };
    }

    /// <summary>Erlös beim Verkauf eines Weinbergs</summary>
    public static double PlotSalePrice(Player player, Plot plot, int year)
    {
        var parcel = new AuctionLot
        {
            Hectares = plot.Hectares,
            Grape = plot.Grape,
            PlantedYear = plot.PlantedYear,
            Site = plot.Site,
            RegionId = player.RegionId,
        };
        return Js.Round(Auction.ParcelValue(parcel, year) * Economy.PlotSaleShare / 1000) * 1000;
    }

    public static HarvestPreview HarvestPreview(GameState state, Player player, Plot plot)
    {
        var grape = Grapes.All[plot.Grape!.Value];
        var hectoliters =
            plot.Hectares *
            grape.YieldPerHa *
            Regions.All[player.RegionId].Character.YieldFactor *
            Rules.PlotAgeFactor(plot, state.Year) *
            plot.CropFactor *
            (BioData.BioInfo(player)?.YieldFactor ?? 1) *
            (HarvestTiming.TryGetValue(state.Month, out var timing) ? timing : 0) *
            (0.6 + 0.4 * plot.Health);
        return new HarvestPreview(
            Hectoliters: Js.Round(hectoliters),
            Level: Quality.LevelForOechsle(plot.Oechsle).Level,
            Quality: Rules.ComputeQuality(player, plot, state.Year),
            // Steillagen werden von Hand gelesen
            Cost: Js.Round(plot.Hectares * Economy.HarvestCostPerHa * (1 + Regions.All[player.RegionId].Character.Slope * Economy.SlopeHarvestFactor)));
    }

    /// <summary>Liefert eine Fehlermeldung, wenn die Aktion gerade nicht erlaubt ist, sonst null.</summary>
    public static string? ValidateAction(GameState state, GameAction action)
    {
        if (!string.IsNullOrEmpty(state.WinnerId) || state.Finished) return Lang.T("Das Spiel ist beendet.", "The game is over.");
        var player = Rules.CurrentPlayer(state);
        WineBatch? FindBatch(string id) => player.Cellar.Find(b => b.Id == id);
        BottleLot? FindLot(string id) => player.Bottles.Find(l => l.Id == id);

        switch (action)
        {
            case GameAction.Plant a:
            {
                var plot = player.Plots.Find(p => p.Id == a.PlotId);
                if (plot is null) return Lang.T("Unbekannte Parzelle.", "Unknown plot.");
                if (plot.ForAuction) return RulesV2.OfferedText;
                if (!Rules.PlantMonths.Contains(state.Month)) return Lang.T("Gepflanzt wird nur im März und April.", "Planting is only possible in March and April.");
                if (plot.Grape is not null) return Lang.T("Die Parzelle ist bereits bestockt.", "The plot is already planted.");
                var cost = Grapes.All[a.Grape].PlantCostPerHa * plot.Hectares;
                if (player.Money < cost) return (Lang.En ? $"Not enough money (need {Util.FormatEuro(cost)})." : $"Nicht genug Geld (benötigt {Util.FormatEuro(cost)}).");
                return null;
            }
            case GameAction.Harvest a:
            {
                var plot = player.Plots.Find(p => p.Id == a.PlotId);
                if (plot is null) return Lang.T("Unbekannte Parzelle.", "Unknown plot.");
                if (!Rules.HarvestMonths.Contains(state.Month)) return Lang.T("Gelesen wird von August bis Dezember.", "Harvest runs from August to December.");
                if (plot.Grape is null) return Lang.T("Die Parzelle ist nicht bestockt.", "The plot is not planted.");
                if (plot.HarvestedThisYear) return Lang.T("Die Parzelle ist in diesem Jahr schon gelesen.", "The plot has already been harvested this year.");
                // Kein Geldcheck: Die Saisonkräfte werden aus dem Erlös bezahlt, notfalls im Dispo.
                if (Rules.PlotAgeFactor(plot, state.Year) == 0) return Lang.T("Die Reben sind noch zu jung.", "The vines are still too young.");
                if (a.Rose && Grapes.All[plot.Grape.Value].Color != GrapeColor.Rot) return Lang.T("Rosé gibt es nur aus roten Trauben.", "Rosé can only be made from red grapes.");
                return null;
            }
            case GameAction.SellBulk a:
            {
                var batch = FindBatch(a.BatchId);
                if (batch is null) return Lang.T("Unbekannte Charge.", "Unknown batch.");
                if (!(a.Hectoliters > 0) || a.Hectoliters > batch.Hectoliters) return Lang.T("Ungültige Menge.", "Invalid amount.");
                return null;
            }
            case GameAction.Bottle a:
                return Cellar.ValidateBottle(player, a.BatchId, a.Hectoliters);
            case GameAction.MakeSekt a:
                return Cellar.ValidateSekt(player, a.BatchId, a.Hectoliters);
            case GameAction.Register a:
            {
                var lot = FindLot(a.LotId);
                if (lot is null) return Lang.T("Unbekannter Flaschenposten.", "Unknown bottle lot.");
                if (lot.Registered) return Lang.T("Der Wein ist bereits geprüft.", "The wine has already been tested.");
                if (lot.MonthsUntilReady > 0) return Lang.T("Der Sekt reift noch.", "The sparkling wine is still maturing.");
                if (lot.Level == QualityLevel.DeutscherWein && !lot.Sparkling) return Lang.T("Deutscher Wein braucht keine Prüfung.", "German wine needs no testing.");
                if (lot.Bottles < Economy.RegisterMinBottles) return (Lang.En ? $"At least {Util.JsNumber(Economy.RegisterMinBottles)} bottles (5 hl) needed for testing." : $"Mindestens {Util.JsNumber(Economy.RegisterMinBottles)} Flaschen (5 hl) für eine Prüfung.");
                if (player.Money < Economy.RegisterFee) return Lang.T("Nicht genug Geld für die Prüfgebühr.", "Not enough money for the testing fee.");
                return null;
            }
            case GameAction.SellWholesale a:
            {
                var lot = FindLot(a.LotId);
                if (lot is null) return Lang.T("Unbekannter Flaschenposten.", "Unknown bottle lot.");
                if (!lot.Registered) return Lang.T("Der Großhandel nimmt nur amtlich geprüfte Weine.", "Wholesale only takes officially tested wines.");
                if (!IsInteger(a.Bottles) || a.Bottles <= 0 || a.Bottles > lot.Bottles)
                {
                    return Lang.T("Ungültige Menge.", "Invalid amount.");
                }
                return null;
            }
            case GameAction.EnterCompetition a:
            {
                var lot = FindLot(a.LotId);
                if (lot is null) return Lang.T("Unbekannter Flaschenposten.", "Unknown bottle lot.");
                return Market.CompetitionEntryProblem(player, lot, Market.UpcomingCompetition(state.Month));
            }
            case GameAction.Marketing a:
            {
                var info = Economy.Marketing[a.Kind];
                if (!info.Months.Contains(state.Month)) return (Lang.En ? $"{info.Name} is not possible this month." : $"{info.Name} ist in diesem Monat nicht möglich.");
                if (player.MarketingThisMonth.Contains(a.Kind)) return (Lang.En ? $"{info.Name} is already booked for this month." : $"{info.Name} ist für diesen Monat schon gebucht.");
                if (player.Money < info.Cost) return (Lang.En ? $"Not enough money (need {Util.FormatEuro(info.Cost)})." : $"Nicht genug Geld (benötigt {Util.FormatEuro(info.Cost)}).");
                return null;
            }
            case GameAction.ExpandTank a:
            {
                if (!IsInteger(a.Hectoliters) || a.Hectoliters <= 0) return Lang.T("Ungültige Menge.", "Invalid amount.");
                var cost = a.Hectoliters * Economy.TankCostPerHl;
                if (player.Money < cost) return (Lang.En ? $"Not enough money (need {Util.FormatEuro(cost)})." : $"Nicht genug Geld (benötigt {Util.FormatEuro(cost)}).");
                return null;
            }
            case GameAction.BuyOnlineShop:
                if (player.HasOnlineShop) return Lang.T("Der Online-Shop läuft bereits.", "The online shop is already running.");
                if (player.Money < Economy.OnlineShopCost) return (Lang.En ? $"Not enough money (need {Util.FormatEuro(Economy.OnlineShopCost)})." : $"Nicht genug Geld (benötigt {Util.FormatEuro(Economy.OnlineShopCost)}).");
                return null;
            case GameAction.HireCellarMaster a:
                return CellarMasters.ValidateHire(state, player, a.Candidate);
            case GameAction.FireCellarMaster:
                return player.CellarMaster is not null ? null : Lang.T("Es ist kein Kellermeister angestellt.", "No cellar master is employed.");
            case GameAction.Borrow a:
                if (!IsInteger(a.Amount) || a.Amount <= 0) return Lang.T("Ungültiger Betrag.", "Invalid amount.");
                if (a.Amount > Market.AvailableCredit(player)) return (Lang.En ? $"Credit line only covers {Util.FormatEuro(Market.AvailableCredit(player))}." : $"Kreditrahmen reicht nur für {Util.FormatEuro(Market.AvailableCredit(player))}.");
                return null;
            case GameAction.Repay a:
                if (!IsInteger(a.Amount) || a.Amount <= 0 || a.Amount > player.Loan) return Lang.T("Ungültiger Betrag.", "Invalid amount.");
                if (a.Amount > player.Money) return Lang.T("Nicht genug Geld auf dem Konto.", "Not enough money in the account.");
                return null;
            case GameAction.AuctionLimit a:
                return Auction.ValidateAuctionLimit(state, player, a.LotId, a.Amount);
            case GameAction.AuctionBid a:
                return Auction.ValidateAuctionBid(state, a.LotId, a.Amount);
            case GameAction.AuctionPass a:
                return Auction.ValidateAuctionPass(state, a.LotId);
            case GameAction.Decide a:
                return Decisions.ValidateDecide(player, a.Option);
            case GameAction.InstallHailNet a:
                return Defense.ValidateInstall(player, a.PlotId, Installation.HailNet);
            case GameAction.InstallIrrigation a:
                return Defense.ValidateInstall(player, a.PlotId, Installation.Irrigation);
            case GameAction.InsureHarvest:
                return Defense.ValidateInsurance(state, player);
            case GameAction.FrostCandles:
                return Defense.ValidateFrostCandles(state, player);
            case GameAction.ApplyBioLabel:
                return Bio.ValidateBioLabel(state, player);
            case GameAction.SellFederweisser a:
                return Products.ValidateFederweisser(state, player, a.BatchId, a.Hectoliters);
            case GameAction.OpenStrausswirtschaft:
                return Products.ValidateStrauss(state, player);
            case GameAction.AcceptOrder a:
                return Orders.ValidateAccept(player, a.OrderId);
            case GameAction.DeclineOrder a:
                return Orders.ValidateDecline(player, a.OrderId);
            case GameAction.DeliverOrder a:
                return Orders.ValidateDeliver(player, a.OrderId);
            case GameAction.SellPlot a:
            {
                var plot = player.Plots.Find(p => p.Id == a.PlotId);
                if (plot is null) return Lang.T("Unbekannte Parzelle.", "Unknown plot.");
                if (plot.ForAuction) return RulesV2.OfferedText;
                // Zur Versteigerung angemeldete Parzellen zählen schon als weg
                if (player.Plots.Count(p => !p.ForAuction) <= 1) return Lang.T("Die letzte Parzelle kann nicht verkauft werden.", "The last plot cannot be sold.");
                return null;
            }
            case GameAction.ClearPlot a:
            {
                var plot = player.Plots.Find(p => p.Id == a.PlotId);
                if (plot is null) return Lang.T("Unbekannte Parzelle.", "Unknown plot.");
                if (plot.ForAuction) return RulesV2.OfferedText;
                if (plot.Grape is null) return Lang.T("Die Parzelle liegt bereits brach.", "The plot is already fallow.");
                var cost = plot.Hectares * Economy.ClearingCostPerHa;
                if (player.Money < cost) return (Lang.En ? $"Not enough money (need {Util.FormatEuro(cost)})." : $"Nicht genug Geld (benötigt {Util.FormatEuro(cost)}).");
                return null;
            }
            case GameAction.SetProtection a:
                return Vineyard.ValidateProtection(player, a.Strategy);
            case GameAction.GambleIceWine a:
                return Vineyard.ValidateIceWineGamble(state, player, a.PlotId, a.Gamble);
            case GameAction.Adulterate a:
                return Intrigue.ValidateAdulterate(player, a.BatchId, a.Method);
            case GameAction.Sabotage a:
                return Intrigue.ValidateSabotage(state, player, a.TargetId, a.Kind);
            case GameAction.AnswerExport:
                return Exports.ValidateAnswerExport(player);
            case GameAction.ExpandBarrique a:
                return Cellar.ValidateExpandBarrique(player, a.Hectoliters);
            case GameAction.ToBarrique a:
                return Cellar.ValidateToBarrique(player, a.BatchId);
            case GameAction.BuildVinothek:
                if (player.HasVinothek) return Lang.T("Die Vinothek steht bereits.", "The wine room is already built.");
                if (player.Money < Economy.VinothekCost) return (Lang.En ? $"Not enough money (need {Util.FormatEuro(Economy.VinothekCost)})." : $"Nicht genug Geld (benötigt {Util.FormatEuro(Economy.VinothekCost)}).");
                return null;
            case GameAction.EndTurn:
                return null;
            case GameAction.AutoSkipTurn:
                return player.Ai is not null ? Lang.T("Computergegner spulen nicht vor.", "Computer winemakers don't fast-forward.") : null;
            case GameAction.SetPriceLevel or GameAction.ChooseGoal or GameAction.ChooseAmbition or GameAction.Construct or GameAction.Donate
                or GameAction.OfferPlot or GameAction.WithdrawPlot:
                return RulesV2.Validate(state, player, action);
            default:
                // Das Original kennt keinen weiteren Fall (liefert dann undefined)
                return null;
        }
    }

    /// <summary>Reine Funktion: liefert einen neuen Spielstand, der alte bleibt unverändert.</summary>
    public static GameState ApplyAction(GameState state, GameAction action)
    {
        var error = ValidateAction(state, action);
        if (!string.IsNullOrEmpty(error)) throw new RuleException(error);

        var next = state.Clone();
        var player = Rules.CurrentPlayer(next);
        WineBatch BatchOf(string id) => player.Cellar.Find(b => b.Id == id)!;
        BottleLot LotOf(string id) => player.Bottles.Find(l => l.Id == id)!;

        switch (action)
        {
            case GameAction.Plant a:
            {
                var plot = player.Plots.Find(p => p.Id == a.PlotId)!;
                var grape = Grapes.All[a.Grape];
                player.Money -= grape.PlantCostPerHa * plot.Hectares;
                plot.Grape = grape.Id;
                plot.PlantedYear = next.Year;
                Util.AddLog(next, player.Id, (Lang.En ? $"{plot.Name}: planted {Util.JsNumber(plot.Hectares)} ha of {grape.Name}." : $"{plot.Name}: {Util.JsNumber(plot.Hectares)} ha {grape.Name} gepflanzt."));
                break;
            }
            case GameAction.Harvest a:
                Harvest(next, player, player.Plots.Find(p => p.Id == a.PlotId)!, a.Rose);
                break;
            case GameAction.SellBulk a:
            {
                var batch = BatchOf(a.BatchId);
                var revenue = Js.Round(Market.BulkPricePerHl(batch, next, player) * a.Hectoliters);
                player.Money += revenue;
                Rules.RemoveBatchVolume(player, batch, a.Hectoliters);
                Util.AddLog(next, player.Id, (Lang.En ? $"Sold {Util.JsNumber(a.Hectoliters)} hl {Rules.WineLabel(batch)} as bulk wine: +{Util.FormatEuro(revenue)}." : $"{Util.JsNumber(a.Hectoliters)} hl {Rules.WineLabel(batch)} als Fasswein verkauft: +{Util.FormatEuro(revenue)}."));
                break;
            }
            case GameAction.Bottle a:
                Cellar.Bottle(next, player, a.BatchId, a.Hectoliters);
                break;
            case GameAction.MakeSekt a:
                Cellar.MakeSekt(next, player, a.BatchId, a.Hectoliters);
                break;
            case GameAction.Register a:
            {
                var lot = LotOf(a.LotId);
                player.Money -= Economy.RegisterFee;
                if (Intrigue.InspectAtRegistration(next, player, lot)) break;
                var points = Market.RegistrationPoints(lot);
                player.Points += points;
                lot.Registered = true;
                Util.AddLog(next, player.Id, (Lang.En ? $"{Rules.WineLabel(lot)} passed the official test: +{Util.JsNumber(points)} points." : $"{Rules.WineLabel(lot)} hat die amtliche Prüfung bestanden: +{Util.JsNumber(points)} Punkte."));
                CheckWinner(next);
                break;
            }
            case GameAction.SellWholesale a:
            {
                var lot = LotOf(a.LotId);
                var revenue = Js.Round(Market.WholesalePrice(lot, player, next) * a.Bottles);
                player.Money += revenue;
                player.WholesaleThisYear += a.Bottles;
                player.Stats.BottlesSold += a.Bottles;
                Rules.RemoveBottles(player, lot, a.Bottles);
                Util.AddLog(next, player.Id, (Lang.En ? $"{Util.JsNumber(a.Bottles)} btl. {Rules.WineLabel(lot)} sold to wholesale: +{Util.FormatEuro(revenue)}." : $"{Util.JsNumber(a.Bottles)} Fl. {Rules.WineLabel(lot)} an den Großhandel: +{Util.FormatEuro(revenue)}."));
                break;
            }
            case GameAction.EnterCompetition a:
            {
                var competition = Market.UpcomingCompetition(next.Month);
                player.Money -= competition.Fee;
                player.CompetitionEntries.Add(a.LotId);
                Util.AddLog(next, player.Id, (Lang.En ? $"{Rules.WineLabel(LotOf(a.LotId))} entered in the {competition.Name}." : $"{Rules.WineLabel(LotOf(a.LotId))} zur {competition.Name} angemeldet."));
                break;
            }
            case GameAction.Marketing a:
            {
                var info = Economy.Marketing[a.Kind];
                player.Money -= info.Cost;
                player.MarketingThisMonth.Add(a.Kind);
                var gain = Rules.ChangeReputation(player, info.Reputation);
                Util.AddLog(next, player.Id, (Lang.En ? $"{info.Name}: reputation +{gain.ToString("F1", CultureInfo.InvariantCulture)}." : $"{info.Name}: Bekanntheit +{gain.ToString("F1", CultureInfo.InvariantCulture)}."));
                break;
            }
            case GameAction.ExpandTank a:
                player.Money -= a.Hectoliters * Economy.TankCostPerHl;
                player.TankCapacityHl += a.Hectoliters;
                Util.AddLog(next, player.Id, (Lang.En ? $"Tank storage expanded by {Util.JsNumber(a.Hectoliters)} hl." : $"Tanklager um {Util.JsNumber(a.Hectoliters)} hl erweitert."));
                break;
            case GameAction.BuyOnlineShop:
                player.Money -= Economy.OnlineShopCost;
                player.HasOnlineShop = true;
                Util.AddLog(next, player.Id, Lang.T("Der Online-Shop ist eröffnet.", "The online shop is open."));
                break;
            case GameAction.HireCellarMaster a:
                CellarMasters.Hire(next, player, a.Candidate);
                break;
            case GameAction.FireCellarMaster:
                CellarMasters.Fire(next, player);
                break;
            case GameAction.Borrow a:
                player.Loan += a.Amount;
                player.Money += a.Amount;
                Util.AddLog(next, player.Id, (Lang.En ? $"Took out a loan of {Util.FormatEuro(a.Amount)}." : $"Kredit über {Util.FormatEuro(a.Amount)} aufgenommen."));
                break;
            case GameAction.Repay a:
                player.Loan -= a.Amount;
                player.Money -= a.Amount;
                Util.AddLog(next, player.Id, (Lang.En ? $"Repaid {Util.FormatEuro(a.Amount)} of the loan." : $"{Util.FormatEuro(a.Amount)} Kredit getilgt."));
                break;
            case GameAction.AuctionLimit a:
                Auction.SetAuctionLimit(next, player, a.LotId, a.Amount);
                break;
            // Live-Gebote gelten für den Menschen, der bei der Versteigerung gerade an der Reihe ist – nicht für den Spieler am Zug.
            case GameAction.AuctionBid a:
                Auction.AuctionBid(next, a.LotId, a.Amount);
                break;
            case GameAction.AuctionPass a:
                Auction.AuctionPass(next, a.LotId);
                break;
            case GameAction.Decide a:
                Decisions.Decide(next, player, a.Option);
                break;
            case GameAction.InstallHailNet a:
                Defense.Install(next, player, a.PlotId, Installation.HailNet);
                break;
            case GameAction.InstallIrrigation a:
                Defense.Install(next, player, a.PlotId, Installation.Irrigation);
                break;
            case GameAction.InsureHarvest:
                Defense.Insure(next, player);
                break;
            case GameAction.FrostCandles:
                Defense.SetFrostCandles(next, player);
                break;
            case GameAction.ApplyBioLabel:
                Bio.ApplyBioLabel(next, player);
                break;
            case GameAction.SellFederweisser a:
                Products.SellFederweisser(next, player, a.BatchId, a.Hectoliters);
                break;
            case GameAction.OpenStrausswirtschaft:
                Products.OpenStrauss(next, player);
                break;
            case GameAction.AcceptOrder a:
                Orders.AcceptOrder(next, player, a.OrderId);
                break;
            case GameAction.DeclineOrder a:
                Orders.DeclineOrder(next, player, a.OrderId);
                break;
            case GameAction.DeliverOrder a:
                Orders.DeliverOrder(next, player, a.OrderId);
                CheckWinner(next);
                break;
            case GameAction.SellPlot a:
            {
                var plot = player.Plots.Find(p => p.Id == a.PlotId)!;
                var price = PlotSalePrice(player, plot, next.Year);
                player.Money += price;
                player.Plots = player.Plots.Where(p => p.Id != plot.Id).ToList();
                Util.AddLog(next, player.Id, (Lang.En ? $"Sold {plot.Name} ({Util.JsNumber(plot.Hectares)} ha) for {Util.FormatEuro(price)}." : $"{plot.Name} ({Util.JsNumber(plot.Hectares)} ha) für {Util.FormatEuro(price)} verkauft."));
                break;
            }
            case GameAction.ClearPlot a:
            {
                var plot = player.Plots.Find(p => p.Id == a.PlotId)!;
                var cost = plot.Hectares * Economy.ClearingCostPerHa;
                var grape = Grapes.All[plot.Grape!.Value].Name;
                player.Money -= cost;
                plot.Grape = null;
                plot.PlantedYear = null;
                plot.Oechsle = 0;
                plot.Health = 1;
                plot.CropFactor = 1;
                plot.HarvestedThisYear = false;
                plot.Botrytis = false;
                plot.IceWineGamble = false;
                // Computergegner roden öffentlich – so sieht man, wohin der Markt läuft
                Util.AddLog(
                    next,
                    player.Ai is not null ? null : player.Id,
                    player.Ai is not null
                        ? (Lang.En ? $"{player.Name} is clearing {grape} in {plot.Name} and will replant in spring." : $"{player.Name} rodet {grape} in der Lage {plot.Name} und pflanzt im Frühjahr neu.")
                        : (Lang.En ? $"{plot.Name}: {grape} cleared ({Util.FormatEuro(cost)}). Replanting is possible in March or April." : $"{plot.Name}: {grape} gerodet ({Util.FormatEuro(cost)}). Neu gepflanzt wird im März oder April."));
                break;
            }
            case GameAction.SetProtection a:
                Vineyard.SetProtection(next, player, a.Strategy);
                break;
            case GameAction.GambleIceWine a:
            {
                var plot = player.Plots.Find(p => p.Id == a.PlotId)!;
                plot.IceWineGamble = a.Gamble;
                Util.AddLog(
                    next,
                    player.Id,
                    a.Gamble
                        ? (Lang.En ? $"{plot.Name}: The grapes stay on the vine for ice wine. Now it needs frost." : $"{plot.Name}: Die Trauben bleiben für Eiswein hängen. Jetzt braucht es Frost.")
                        : (Lang.En ? $"{plot.Name}: No ice wine after all – the grapes can be harvested normally." : $"{plot.Name}: Doch kein Eiswein – die Trauben können normal gelesen werden."));
                break;
            }
            case GameAction.Adulterate a:
                Intrigue.Adulterate(next, player, a.BatchId, a.Method);
                if (RulesV2.On(next)) player.Tainted = true;
                break;
            case GameAction.Sabotage a:
                Intrigue.Sabotage(next, player, a.TargetId, a.Kind);
                if (RulesV2.On(next)) player.Tainted = true;
                break;
            case GameAction.AnswerExport a:
                Exports.AnswerExport(next, player, a.Accept);
                break;
            case GameAction.ExpandBarrique a:
                Cellar.ExpandBarrique(next, player, a.Hectoliters);
                break;
            case GameAction.ToBarrique a:
                Cellar.ToBarrique(next, player, a.BatchId);
                break;
            case GameAction.BuildVinothek:
                player.Money -= Economy.VinothekCost;
                player.HasVinothek = true;
                Rules.ChangeReputation(player, Economy.VinothekReputation);
                Util.AddLog(next, null, (Lang.En ? $"{player.Name} opens a wine room for tastings." : $"{player.Name} eröffnet eine Vinothek mit Probierstube."));
                break;
            case GameAction.SetPriceLevel or GameAction.ChooseGoal or GameAction.ChooseAmbition or GameAction.Construct or GameAction.Donate
                or GameAction.OfferPlot or GameAction.WithdrawPlot:
                RulesV2.Apply(next, player, action);
                // Eine Spende bringt Punkte und kann das Punkteziel erreichen
                if (action is GameAction.Donate) CheckWinner(next);
                break;
            case GameAction.EndTurn:
                EndTurn(next);
                break;
            case GameAction.AutoSkipTurn:
                player.AutoSkip = new AutoSkip { Since = Bio.GameMonth(next), Until = Bio.GameMonth(next) + Economy.AutoSkipMonths };
                EndTurn(next);
                break;
        }
        return next;
    }

    static void Harvest(GameState state, Player player, Plot plot, bool rose = false)
    {
        var preview = HarvestPreview(state, player, plot);
        player.Money -= preview.Cost;
        player.Stats.Harvests++;
        player.Stats.BestLevelRank = Math.Max(player.Stats.BestLevelRank, Rules.LevelRank(preview.Level));
        if (preview.Level == QualityLevel.Beerenauslese || preview.Level == QualityLevel.Trockenbeerenauslese) player.Stats.Edelsuess++;
        plot.HarvestedThisYear = true;
        plot.IceWineGamble = false;
        Util.AddLog(
            state,
            player.Id,
            Lang.En
                ? $"{plot.Name}: harvested {Util.JsNumber(preview.Hectoliters)} hl {Grapes.All[plot.Grape!.Value].Name}{(rose ? " for Rosé" : "")}, {Util.JsNumber(Js.Round(plot.Oechsle))} °Oe, " +
                  $"{Quality.QualityInfo(preview.Level).Name}, quality {Util.JsNumber(preview.Quality)}."
                : $"{plot.Name}: {Util.JsNumber(preview.Hectoliters)} hl {Grapes.All[plot.Grape!.Value].Name}{(rose ? " für Rosé" : "")} gelesen, {Util.JsNumber(Js.Round(plot.Oechsle))} °Oe, " +
                  $"{Quality.QualityInfo(preview.Level).Name}, Güte {Util.JsNumber(preview.Quality)}.");
        Rules.StoreWine(state, player, new WineBatch
        {
            Grape = plot.Grape!.Value,
            Vintage = state.Year,
            Hectoliters = preview.Hectoliters,
            Oechsle = Js.Round(plot.Oechsle),
            Level = preview.Level,
            Quality = preview.Quality,
            Rose = rose,
        });
        if (RulesV2.On(state)) RulesV2.OnHarvest(state, player, plot, preview);
    }

    static List<int> ActiveIndices(GameState state)
    {
        var result = new List<int>();
        for (var i = 0; i < state.Players.Count; i++)
        {
            if (!state.Players[i].Bankrupt) result.Add(i);
        }
        return result;
    }

    static void EndTurn(GameState state)
    {
        var candidates = ActiveIndices(state);
        var laterIndex = candidates.FindIndex(i => i > state.CurrentPlayer);
        if (laterIndex >= 0)
        {
            state.CurrentPlayer = candidates[laterIndex];
            return;
        }
        if (RulesV2.On(state) && RulesV2.HasHalves(state.Month) && state.HalfMonth <= 1)
        {
            // Erste Hälfte eines Lesemonats: nur Wetter und Reife
            RulesV2.ResolveFirstHalf(state);
        }
        else if (RulesV2.On(state) && state.Month == 1)
        {
            // Winterzug: Januar und Februar in einem
            ResolveMonth(state, quiet: true);
            if (state.Month == 2 && !state.Finished) ResolveMonth(state);
        }
        else
        {
            ResolveMonth(state);
        }
        var active = ActiveIndices(state);
        if (active.Count == 0)
        {
            state.Finished = true;
            Util.AddLog(state, null, Lang.T("Alle Weingüter sind insolvent. Das Spiel ist vorbei.", "All estates are insolvent. The game is over."));
        }
        else
        {
            state.CurrentPlayer = active[0];
        }
    }

    /// <param name="quiet">Regelversion 2, Januar im Winterzug: keine neuen Angebote, die im übersprungenen Februar verfielen</param>
    static void ResolveMonth(GameState state, bool quiet = false)
    {
        var v2 = RulesV2.On(state);
        var secondHalf = v2 && state.HalfMonth == 2;
        var weather = Weather.DrawWeather(state.Rng, state.Forecast);
        state.LastWeather = weather;
        if (v2 && state.Month is >= 8 and <= 11) state.RipeningWeather.Add(weather);
        Util.AddLog(state, null, (Lang.En ? $"Weather in {MonthNames[state.Month - 1]}: {Weather.WeatherLabels[weather]}." : $"Wetter im {MonthNames[state.Month - 1]}: {Weather.WeatherLabels[weather]}."));

        var competition = Economy.Competitions.Values.FirstOrDefault(c => c.Month == state.Month);
        if (state.Auction?.Phase == AuctionPhase.Live) Auction.FinishAuction(state);
        if (v2 && competition is not null) state.LastShow = new CompetitionShow { Competition = competition.Id, Year = state.Year };
        Events.MarketEvents(state);
        Trends.UpdateTrends(state);

        // Eiswein-Frost wird je Anbaugebiet einmal ausgewürfelt
        var frost = new Dictionary<RegionId, bool>();
        if (state.Month == 12)
        {
            // Gebiete in der Reihenfolge ihres ersten Auftretens (wie new Set(...))
            var regionIds = new List<RegionId>();
            foreach (var p in state.Players)
            {
                if (!regionIds.Contains(p.RegionId)) regionIds.Add(p.RegionId);
            }
            foreach (var regionId in regionIds)
            {
                frost[regionId] = Rng.NextFloat(state.Rng) < Vineyard.IceWineChance(state, regionId);
            }
        }

        foreach (var player in state.Players)
        {
            if (player.Bankrupt) continue;
            foreach (var plot in player.Plots)
            {
                if (secondHalf) RulesV2.GrowHalf(state, player, plot, weather);
                else Vineyard.GrowPlot(state, player, plot, weather);
            }
            Vineyard.VineyardEvents(state, player, weather);
            if (state.Month == 12) Vineyard.ResolveIceWine(state, player, frost.TryGetValue(player.RegionId, out var hasFrost) ? hasFrost : false);
            Cellar.MatureCellar(state, player);
            Exports.DeliverExport(state, player);
            SellDirect(state, player);
            if (competition is not null) JudgeCompetition(state, player, competition);
            Intrigue.RandomControl(state, player);
            Events.TickEffects(state, player);
            Decisions.ExpireDecision(state, player);
            if (state.RandomEvents && !quiet) Decisions.MonthlyEvent(state, player);
            PayMonthlyCosts(state, player);
            if (v2) RulesV2.Monthly(state, player);
            Rules.ChangeReputation(player, -player.Reputation * Economy.ReputationDecayRate);
            player.MarketingThisMonth = [];
            player.FrostCandles = false;
            player.FederweisserThisMonth = 0;
            player.IntrigueThisMonth = false;
            if (!quiet) Exports.MaybeOfferExport(state, player);
            Orders.ExpireOrders(state, player);
            if (!quiet) Orders.MaybeOfferOrder(state, player);
            // Im Winterzug zählt die Zahlungsunfähigkeit nur einmal – der Spieler kann zwischen Januar und Februar nicht eingreifen
            if (!quiet) CheckInsolvency(state, player);
        }
        if (state.RandomEvents) Rivals.MonthlyRivalry(state);
        Milestones.CheckMilestones(state);
        CheckWinner(state);

        if (state.Month == 12)
        {
            if (v2)
            {
                // Jahresziel und Schatzkammer bringen Punkte – wer damit das Ziel erreicht, gewinnt jetzt und nicht erst,
                // wenn irgendwann jemand einen Wein prüfen lässt
                RulesV2.EndYear(state);
                CheckWinner(state);
            }
            EndOfYear(state);
            state.Month = 1;
            state.Year++;
            if (state.YearLimit is not (null or 0) && state.Year > state.YearLimit)
            {
                FinishByTime(state);
                return;
            }
            Util.AddLog(state, null, (Lang.En ? $"Year {state.Year} begins." : $"Jahr {state.Year} beginnt."));
            CellarMasters.RefreshCandidates(state);
            foreach (var player in state.Players) if (!player.Bankrupt) Bio.ChargeBioFees(state, player);
            if (v2) RulesV2.StartYear(state);
        }
        else
        {
            state.Month++;
        }
        if (!quiet)
        {
            if (state.Auction?.Phase == AuctionPhase.Announced) Auction.StartLiveAuction(state);
            else if (state.Auction is null && --state.MonthsToAuction <= 0) Auction.AnnounceAuction(state);
        }
        state.HalfMonth = v2 && RulesV2.HasHalves(state.Month) ? 1 : 0;
        state.Forecast = Weather.MakeForecast(state.Rng, state.Month, 0.8);
    }

    /// <summary>
    /// Hofladen und Online-Shop verkaufen automatisch geprüfte Weine und Deutschen Wein, älteste zuerst.
    /// Ungeprüfte Prädikatsweine und ungeprüfter Sekt warten auf die Prüfung – nur Restposten, die für eine
    /// Prüfung zu klein sind, gehen als einfacher Wein weg. Reifender Sekt und Prämierungsweine bleiben im Lager.
    /// </summary>
    static void SellDirect(GameState state, Player player)
    {
        bool WaitsForTest(BottleLot lot) =>
            !lot.Registered && (lot.Sparkling || lot.Level != QualityLevel.DeutscherWein) && lot.Bottles >= Economy.RegisterMinBottles;
        var forSale = player.Bottles
            .Where(lot => !player.CompetitionEntries.Contains(lot.Id) && lot.MonthsUntilReady == 0 && !WaitsForTest(lot))
            .ToList();
        var demand = RulesV2.DirectDemand(state, player);
        var priceFactor = RulesV2.DirectPriceFactor(state, player);
        var sold = 0.0;
        var revenue = 0.0;
        foreach (var lot in forSale)
        {
            if (demand <= 0) break;
            var count = Math.Min(demand, lot.Bottles);
            revenue += priceFactor == 1 ? count * Market.BottlePrice(lot, player, state) : count * Market.BottlePrice(lot, player, state) * priceFactor;
            sold += count;
            player.Stats.BottlesSold += count;
            demand -= count;
            Rules.RemoveBottles(player, lot, count);
        }
        if (sold > 0)
        {
            var net = Js.Round(revenue * (1 - Economy.DirectSalesCostShare));
            player.Money += net;
            Util.AddLog(state, player.Id, (Lang.En ? $"Cellar door{(player.HasOnlineShop ? " and online" : "")}: sold {Util.JsNumber(sold)} bottles: +{Util.FormatEuro(net)} (after distribution costs)." : $"Ab Hof{(player.HasOnlineShop ? " und online" : "")} {Util.JsNumber(sold)} Flaschen verkauft: +{Util.FormatEuro(net)} (nach Vertriebskosten)."));
        }
    }

    static readonly Medal[] MedalOrder = [Medal.Gold, Medal.Silber, Medal.Bronze];

    static void JudgeCompetition(GameState state, Player player, CompetitionInfo competition)
    {
        foreach (var lotId in player.CompetitionEntries)
        {
            var lot = player.Bottles.Find(l => l.Id == lotId);
            if (lot is null) continue;
            ShowEntry? entry = null;
            double score;
            if (RulesV2.On(state)) (score, entry) = RulesV2.Judge(state, player, lot);
            else score = Market.CompetitionBaseScore(lot) + Rng.NextRange(state.Rng, -5, 5);
            Medal? found = null;
            foreach (var m in MedalOrder)
            {
                if (score >= competition.Thresholds[m])
                {
                    found = m;
                    break;
                }
            }
            if (found is not { } medal)
            {
                Util.AddLog(state, player.Id, (Lang.En ? $"{competition.Name}: {Rules.WineLabel(lot)} goes home empty-handed." : $"{competition.Name}: {Rules.WineLabel(lot)} geht leer aus."));
                continue;
            }
            lot.Awards.Add(new Award { Competition = competition.Id, Year = state.Year, Medal = medal });
            player.Points += competition.Points[medal];
            player.Stats.Medals++;
            if (medal == Medal.Gold) player.Stats.GoldMedals++;
            if (entry is not null)
            {
                entry.Medal = medal;
                entry.Points = competition.Points[medal];
                var grape = Json.Name(lot.Grape);
                if (medal == Medal.Gold) player.GoldByGrape[grape] = player.GoldByGrape.GetValueOrDefault(grape) + 1;
            }
            Rules.ChangeReputation(player, competition.Reputation[medal]);
            Util.AddLog(
                state,
                null,
                (Lang.En ? $"{competition.Name}: {Economy.MedalNames[medal]} for {player.Name} – {Rules.WineLabel(lot)}! +{Util.JsNumber(competition.Points[medal])} points." : $"{competition.Name}: {Economy.MedalNames[medal]} für {player.Name} – {Rules.WineLabel(lot)}! +{Util.JsNumber(competition.Points[medal])} Punkte."));
        }
        player.CompetitionEntries = [];
    }

    static void PayMonthlyCosts(GameState state, Player player)
    {
        player.Money -= Market.MonthlyCosts(player, state.Month).Total;
        if (player.Money < 0) Util.AddLog(state, player.Id, (Lang.En ? $"Account overdrawn: {Util.FormatEuro(player.Money)}!" : $"Konto im Minus: {Util.FormatEuro(player.Money)}!"));
    }

    static void CheckInsolvency(GameState state, Player player)
    {
        var creditExhausted = player.Loan >= Market.CreditLimit(player);
        if (player.Money < 0 && creditExhausted)
        {
            player.MonthsInsolvent++;
            if (player.MonthsInsolvent >= Economy.InsolvencyMonths)
            {
                player.Bankrupt = true;
                player.CompetitionEntries = [];
                player.ExportContract = null;
                player.ExportOffer = null;
                Util.AddLog(state, null, (Lang.En ? $"{player.Name} is insolvent and drops out." : $"{player.Name} ist insolvent und scheidet aus."));
            }
            else
            {
                var left = Economy.InsolvencyMonths - player.MonthsInsolvent;
                Util.AddLog(state, player.Id, (Lang.En ? $"Warning: no credit line left! {left} month{(left == 1 ? "" : "s")} until insolvency." : $"Achtung: Kein Kreditrahmen mehr! Noch {left} Monat{(left == 1 ? "" : "e")} bis zur Insolvenz."));
            }
        }
        else
        {
            player.MonthsInsolvent = 0;
        }
    }

    /// <summary>Zeitlimit erreicht: Die meisten Punkte gewinnen, auch unterhalb des Punkteziels.</summary>
    static void FinishByTime(GameState state)
    {
        state.Finished = true;
        state.Year = state.YearLimit!.Value;
        state.Month = 12;
        var best = state.Players.Where(p => !p.Bankrupt).OrderByDescending(p => p.Points).FirstOrDefault();
        if (string.IsNullOrEmpty(state.WinnerId) && best is not null) state.WinnerId = best.Id;
        Util.AddLog(state, null, (Lang.En ? $"After {state.YearLimit} years it's over{(best is not null ? $": {best.Name} leads with {Util.JsNumber(best.Points)} points" : "")}." : $"Nach {state.YearLimit} Jahren ist Schluss{(best is not null ? $": {best.Name} liegt mit {Util.JsNumber(best.Points)} Punkten vorn" : "")}."));
    }

    static void EndOfYear(GameState state)
    {
        foreach (var player in state.Players)
        {
            player.WholesaleThisYear = 0;
            foreach (var plot in player.Plots)
            {
                var hadCrop = plot.Grape is not null && Rules.PlotAgeFactor(plot, state.Year) > 0;
                if (hadCrop && !plot.HarvestedThisYear && !player.Bankrupt)
                {
                    Util.AddLog(state, player.Id, (Lang.En ? $"{plot.Name}: grapes not harvested – the vintage is lost." : $"{plot.Name}: Trauben nicht gelesen – der Jahrgang ist verloren."));
                }
                plot.Oechsle = 0;
                plot.Health = 1;
                plot.CropFactor = 1;
                plot.HarvestedThisYear = false;
                plot.Botrytis = false;
                plot.IceWineGamble = false;
            }
        }
    }

    /// <summary>Erreichen mehrere Spieler das Ziel gleichzeitig, gewinnt die höchste Punktzahl.</summary>
    static void CheckWinner(GameState state)
    {
        if (!string.IsNullOrEmpty(state.WinnerId)) return;
        var best = state.Players
            .Where(p => !p.Bankrupt && p.Points >= state.TargetPoints)
            .OrderByDescending(p => p.Points)
            .FirstOrDefault();
        if (best is not null)
        {
            state.WinnerId = best.Id;
            Util.AddLog(state, null, (Lang.En ? $"{best.Name} reaches {Util.JsNumber(best.Points)} points and wins!" : $"{best.Name} erreicht {Util.JsNumber(best.Points)} Punkte und gewinnt!"));
        }
    }
}
