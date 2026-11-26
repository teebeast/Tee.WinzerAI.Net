using Winzer.Engine.Data;

namespace Winzer.Engine;

public sealed record AiProfile
{
    public required AiProfileId Id { get; init; }
    public required string NameDe { get; init; }
    public required string NameEn { get; init; }
    public required string DescriptionDe { get; init; }
    public required string DescriptionEn { get; init; }
    public string Name => Lang.T(NameDe, NameEn);
    public string Description => Lang.T(DescriptionDe, DescriptionEn);
    /// <summary>Bevorzugte Rebsorten; gepflanzt wird die, die am besten zum Gebiet passt</summary>
    public required GrapeId[] Grapes { get; init; }
    /// <summary>Frühester Lesemonat</summary>
    public required int HarvestMonth { get; init; }
    /// <summary>Gelesen wird erst ab diesem Mostgewicht – spätestens aber im November</summary>
    public required double MinOechsle { get; init; }
    /// <summary>Anteil des neuen Jahrgangs, der direkt als Fasswein verkauft wird</summary>
    public required double BulkShare { get; init; }
    /// <summary>Geld, das die KI möglichst nicht unterschreitet</summary>
    public required double CashReserve { get; init; }
    /// <summary>So viele Monate Ab-Hof-Absatz bleiben im Lager, der Rest geht an den Großhandel</summary>
    public required double KeepMonthsOfDemand { get; init; }
    public required CompetitionId[] Competitions { get; init; }
    public required bool CellarMaster { get; init; }
    public required bool OnlineShop { get; init; }
    public required MarketingKind[] Marketing { get; init; }
    /// <summary>Höchstgebot bei Versteigerungen als Vielfaches des Schätzwerts</summary>
    public required double AuctionPremium { get; init; }
    public required double MaxHectares { get; init; }
    /// <summary>Anteil des Kreditrahmens, den die KI höchstens ausschöpft</summary>
    public required double MaxLoanShare { get; init; }
    public required ProtectionStrategy Protection { get; init; }
    /// <summary>Lässt in frostsicheren Gebieten eine kleine Parzelle für Eiswein hängen</summary>
    public required bool IceWine { get; init; }
    /// <summary>Setzt jedes Frühjahr etwas Sekt an</summary>
    public required bool Sekt { get; init; }
    /// <summary>Panscht ab und zu und streut Gerüchte über den Spitzenreiter</summary>
    public required bool Intrigue { get; init; }
    /// <summary>Baut Rotwein im Barrique aus</summary>
    public required bool Barrique { get; init; }
    /// <summary>Sichert sich gegen Wetter ab: Versicherung, Frostkerzen, Bewässerung</summary>
    public required bool Cautious { get; init; }
    /// <summary>Keltert bei gutem Markt eine kleine rote Lage als Rosé</summary>
    public required bool Rose { get; init; }
}

/// <summary>Computergegner (ai.ts)</summary>
public static class Ai
{
    /// <summary>Vorgeschlagene Namen der Computer-Weingüter – bewusst erfunden, damit kein echter Betrieb gemeint ist</summary>
    public static readonly Dictionary<AiProfileId, string> AiEstateNames = new()
    {
        [AiProfileId.Tradition] = "Weingut Rebenhain",
        [AiProfileId.Genossenschaft] = "Genossenschaft Talblick",
        [AiProfileId.Bio] = "Biohof Morgentau",
        [AiProfileId.Schlitzohr] = "Weinhaus Gaukler",
    };

    public static readonly Dictionary<AiProfileId, AiProfile> AiProfiles = new()
    {
        [AiProfileId.Tradition] = new AiProfile
        {
            Id = AiProfileId.Tradition, NameDe = "Traditionswinzer", NameEn = "Traditional winemaker",
            DescriptionDe = "Riesling und Spätburgunder, späte Lese, Kellermeister und Prämierungen.",
            DescriptionEn = "Riesling and Spätburgunder, late harvest, cellar master and wine awards.",
            Grapes = [GrapeId.Riesling, GrapeId.Spaetburgunder, GrapeId.Lemberger, GrapeId.Weissburgunder],
            HarvestMonth = 10, MinOechsle = 73, BulkShare = 0.1, CashReserve = 20_000,
            KeepMonthsOfDemand = 12, Competitions = [CompetitionId.Regional, CompetitionId.Bund], CellarMaster = true, OnlineShop = false,
            Marketing = [MarketingKind.Social, MarketingKind.Messe], AuctionPremium = 1.3, MaxHectares = 25, MaxLoanShare = 0.6,
            Protection = ProtectionStrategy.Conventional, IceWine = true, Sekt = true, Intrigue = false, Barrique = true, Cautious = true, Rose = false,
        },
        [AiProfileId.Genossenschaft] = new AiProfile
        {
            Id = AiProfileId.Genossenschaft, NameDe = "Genossenschafts-Typ", NameEn = "Cooperative type",
            DescriptionDe = "Masse statt Klasse: Müller-Thurgau und Dornfelder, viel Fasswein und Großhandel.",
            DescriptionEn = "Quantity over quality: Müller-Thurgau and Dornfelder, lots of bulk wine and wholesale.",
            Grapes = [GrapeId.MuellerThurgau, GrapeId.Dornfelder, GrapeId.Silvaner, GrapeId.Trollinger],
            HarvestMonth = 9, MinOechsle = 68, BulkShare = 0.3, CashReserve = 30_000,
            KeepMonthsOfDemand = 6, Competitions = [CompetitionId.Regional, CompetitionId.Bund], CellarMaster = false, OnlineShop = false,
            Marketing = [MarketingKind.Social], AuctionPremium = 1.15, MaxHectares = 40, MaxLoanShare = 0.5,
            Protection = ProtectionStrategy.Conventional, IceWine = false, Sekt = false, Intrigue = false, Barrique = false, Cautious = true, Rose = false,
        },
        [AiProfileId.Bio] = new AiProfile
        {
            Id = AiProfileId.Bio, NameDe = "Biopionier", NameEn = "Organic pioneer",
            DescriptionDe = "Bio-Weinbau mit Silvaner und Spätburgunder, alles in die Flasche, Hoffeste und Online-Shop.",
            DescriptionEn = "Organic viticulture with Silvaner and Spätburgunder, everything bottled, estate festivals and online shop.",
            Grapes = [GrapeId.Weissburgunder, GrapeId.Silvaner, GrapeId.Spaetburgunder, GrapeId.Riesling, GrapeId.Regent],
            HarvestMonth = 10, MinOechsle = 70, BulkShare = 0, CashReserve = 15_000,
            KeepMonthsOfDemand = 18, Competitions = [CompetitionId.Regional, CompetitionId.Bund], CellarMaster = true, OnlineShop = true,
            Marketing = [MarketingKind.Social, MarketingKind.Hoffest], AuctionPremium = 1.1, MaxHectares = 20, MaxLoanShare = 0.5,
            Protection = ProtectionStrategy.Bio, IceWine = false, Sekt = true, Intrigue = false, Barrique = false, Cautious = true, Rose = true,
        },
        [AiProfileId.Schlitzohr] = new AiProfile
        {
            Id = AiProfileId.Schlitzohr, NameDe = "Schlitzohr", NameEn = "Sly fox",
            DescriptionDe = "Riskant: kauft auf Pump, bietet hoch, panscht gelegentlich und streut Gerüchte.",
            DescriptionEn = "Risky: buys on credit, bids high, occasionally adulterates wine and spreads rumors.",
            Grapes = [GrapeId.Dornfelder, GrapeId.Riesling, GrapeId.Grauburgunder, GrapeId.Lemberger],
            HarvestMonth = 10, MinOechsle = 65, BulkShare = 0.3, CashReserve = 15_000,
            KeepMonthsOfDemand = 8, Competitions = [CompetitionId.Regional, CompetitionId.Bund], CellarMaster = true, OnlineShop = true,
            Marketing = [MarketingKind.Social, MarketingKind.Hoffest, MarketingKind.Messe], AuctionPremium = 1.35, MaxHectares = 35, MaxLoanShare = 0.7,
            Protection = ProtectionStrategy.Conventional, IceWine = true, Sekt = false, Intrigue = true, Barrique = true, Cautious = false, Rose = true,
        },
    };

    /// <summary>Zusätzliches Polster über der Reserve, ab dem Marketing gebucht wird</summary>
    static readonly Dictionary<MarketingKind, double> MarketingBuffer = new()
    {
        [MarketingKind.Social] = 0, [MarketingKind.Hoffest] = 15_000, [MarketingKind.Messe] = 30_000,
    };
    const double TankStep = 100;
    /// <summary>Ab dieser Frostchance lässt die KI Trauben für Eiswein hängen</summary>
    const double IceWineMinChance = 0.35;
    /// <summary>Höchstens dieser Anteil der tragenden Fläche wird für Eiswein riskiert</summary>
    const double IceWineMaxShare = 0.25;
    const double SektHectoliters = 15;
    const double BarriqueTargetHl = 200;
    /// <summary>Ab diesem Anteil des Kreditrahmens spart die KI: kein Kellermeister, kein teures Marketing, keine Fässer</summary>
    const double AusterityLoanShare = 0.75;
    /// <summary>Gepanscht wird nur, wenn ein Skandal (Strafe, beschlagnahmter Jahrgang) verkraftbar wäre</summary>
    const double SchemeMinCredit = 150_000;
    const double SchemeMaxHl = 120;

    /// <summary>Summe von links nach rechts – wie <c>reduce((sum, x) => sum + f(x), 0)</c></summary>
    static double Sum<T>(IEnumerable<T> items, Func<T, double> value)
    {
        var sum = 0.0;
        foreach (var item in items) sum += value(item);
        return sum;
    }

    /// <summary>
    /// Spielt den Zug des aktuellen Computergegners und beendet ihn.
    /// Die KI nutzt ausschließlich reguläre Aktionen – ungültige werden still übersprungen.
    /// </summary>
    public static GameState PlayAiTurn(GameState state)
    {
        var profile = AiProfiles[Rules.CurrentPlayer(state).Ai ?? AiProfileId.Tradition];
        var s = state;
        if (RulesV2.On(s)) s = RulesV2.AiTurn(s);
        Player Me() => Rules.CurrentPlayer(s);
        bool Act(GameAction action)
        {
            if (Game.ValidateAction(s, action) != null) return false;
            s = Game.ApplyAction(s, action);
            return true;
        }
        // Kreditrahmen, den die KI noch nutzen mag: für Expansion nur den Anteil laut Profil,
        // für Betriebsnotwendiges (Abfüllen, Tanks, Liquidität) den vollen Rahmen.
        double UsableCredit(bool full = false) =>
            full
                ? Market.AvailableCredit(Me())
                : Math.Max(0, Math.Min(Market.AvailableCredit(Me()), Math.Floor(Market.CreditLimit(Me()) * profile.MaxLoanShare) - Me().Loan));
        // Sorgt per Kredit dafür, dass `amount` ausgegeben werden kann, ohne die Reserve zu unterschreiten.
        bool EnsureCash(double amount, double? reserve = null, bool full = false)
        {
            var missingCash = amount + (reserve ?? profile.CashReserve) - Me().Money;
            if (missingCash <= 0) return true;
            var borrow = Math.Min(UsableCredit(full), Math.Ceiling(missingCash / 1000) * 1000);
            if (borrow > 0) Act(new GameAction.Borrow(borrow));
            return Me().Money >= amount;
        }
        // Expansion (Pflanzen, Ersteigern) nur, wenn danach Reserve plus zwölf Monate laufende Kosten
        // finanzierbar bleiben – sonst fehlt später das Geld fürs Abfüllen.
        bool CanExpand(double expansionCost)
        {
            var yearOfCosts = 12 * Market.MonthlyCosts(Me(), s.Month).Total;
            var cash = Math.Max(0, Me().Money);
            var fullHeadroom = cash + Market.AvailableCredit(Me()) - expansionCost;
            return cash + UsableCredit() - expansionCost >= profile.CashReserve && fullHeadroom >= profile.CashReserve + yearOfCosts;
        }

        // Sparmodus bei hoher Verschuldung. Wichtig, weil Geld auf dem Konto oft nur geliehen ist
        // (etwa fürs Abfüllen) und sonst in Gehälter und Werbung fließen würde.
        bool Strained() => Me().Loan > Market.CreditLimit(Me()) * AusterityLoanShare;

        // Bewerber nach Profil bewerten: Talent zählt, benötigte Fähigkeiten (Sekt, Barrique) noch mehr, das Gehalt dagegen.
        // Gewechselt wird nur im Januar, wenn ein Bewerber deutlich besser passt.
        void ChooseCellarMaster()
        {
            var needs = new List<CellarSkill>();
            if (profile.Sekt) needs.Add(CellarSkill.Sekt);
            if (profile.Barrique) needs.Add(CellarSkill.Barrique);
            double Score(CellarMaster m) => m.Talent * 2 + needs.Count(n => m.Skills.Contains(n)) * 6 - m.Salary / 1000;
            var ranked = s.CellarMasterCandidates
                .Select((m, i) => (Index: i, Score: Score(m)))
                .OrderByDescending(r => r.Score)
                .ToList();
            if (ranked.Count == 0) return;
            var best = ranked[0];
            var current = Me().CellarMaster;
            if (current is null || (s.Month == 1 && best.Score > Score(current) + 4)) Act(new GameAction.HireCellarMaster(best.Index));
        }

        void HarvestAll(string? reservedPlotId)
        {
            var plots = Me()
                .Plots.Where(p => Game.ValidateAction(s, new GameAction.Harvest(p.Id)) == null)
                .Where(p => !p.IceWineGamble && p.Id != reservedPlotId)
                .Where(p => p.Oechsle >= profile.MinOechsle || s.Month >= 11)
                .Select(p => new { Plot = p, Preview = Game.HarvestPreview(s, Me(), p) })
                .ToList()
                .OrderByDescending(p => p.Preview.Quality)
                .ToList();
            if (plots.Count == 0) return;

            var needed = Sum(plots, p => p.Preview.Hectoliters);
            var missingHl = needed - (Me().TankCapacityHl - Rules.UsedTankHl(Me()));
            if (missingHl > 0)
            {
                var tankHl = Math.Ceiling(missingHl / TankStep) * TankStep;
                if (EnsureCash(tankHl * Economy.TankCostPerHl, 0, true)) Act(new GameAction.ExpandTank(tankHl));
            }
            // Rosé aus der kleinsten roten Lage, wenn der Markt ihn mag
            var rosePlot =
                profile.Rose && s.Trends[TrendKey.Rose].Value >= 1
                    ? plots.Where(p => Grapes.All[p.Plot.Grape!.Value].Color == GrapeColor.Rot).OrderBy(p => p.Plot.Hectares).FirstOrDefault()?.Plot
                    : null;
            foreach (var entry in plots) Act(new GameAction.Harvest(entry.Plot.Id, rosePlot is not null && entry.Plot.Id == rosePlot.Id));

            // Massenorientierte Profile zapfen im Herbst Federweißer – besser bezahlt als Fasswein
            if (profile.BulkShare >= 0.2)
            {
                foreach (var must in Me().Cellar.Where(b => Products.IsFreshMust(s, b)).OrderBy(b => b.Quality).ToList())
                {
                    var mustHl = Math.Min(must.Hectoliters, Products.FederweisserCapacity(Me()));
                    if (mustHl <= 0) break;
                    Act(new GameAction.SellFederweisser(must.Id, mustHl));
                }
            }

            // Ein Teil des neuen Jahrgangs geht sofort als Fasswein weg – die schwächsten Weine zuerst.
            var fresh = Me()
                .Cellar.Where(b => b.Vintage == s.Year)
                .OrderBy(b => b.Quality)
                .ToList();
            var toSell = Js.Round(Sum(fresh, b => b.Hectoliters) * profile.BulkShare);
            foreach (var freshBatch in fresh)
            {
                if (toSell <= 0) break;
                var bulkHl = Math.Min(freshBatch.Hectoliters, toSell);
                if (Act(new GameAction.SellBulk(freshBatch.Id, bulkHl))) toSell -= bulkHl;
            }
        }

        void SellSurplusWholesale()
        {
            var contract = Me().ExportContract;
            var keep =
                Market.MonthlyDemand(Me()) * (Strained() ? Math.Min(3, profile.KeepMonthsOfDemand) : profile.KeepMonthsOfDemand) +
                (contract is not null ? contract.BottlesPerMonth * Math.Min(3, contract.MonthsLeft) : 0);
            foreach (var bottleLot in Me().Bottles.OrderByDescending(l => l.Quality).ToList())
            {
                if (Me().CompetitionEntries.Contains(bottleLot.Id)) continue;
                var lotSurplus = bottleLot.Bottles - Math.Max(0, keep);
                keep -= bottleLot.Bottles;
                if (lotSurplus > 0 && bottleLot.Registered) Act(new GameAction.SellWholesale(bottleLot.Id, lotSurplus));
            }
        }

        // Schlitzohr: zuckert alle drei Jahre nach der Lese heimlich einen Wein auf und
        // streut im Mai Gerüchte über den Spitzenreiter, wenn es sich das leisten kann.
        void Scheme()
        {
            if (s.Month == 10 && s.Year % 3 == 0 && Market.AvailableCredit(Me()) >= SchemeMinCredit)
            {
                // Nur eine kleine Charge riskieren – fliegt sie auf, wird nicht der ganze Jahrgang beschlagnahmt.
                var target = Me()
                    .Cellar.Where(b => b.Vintage == s.Year && (b.Level == QualityLevel.Qualitaetswein || b.Level == QualityLevel.Kabinett))
                    .Where(b => b.Hectoliters <= SchemeMaxHl)
                    .OrderBy(b => b.Hectoliters)
                    .FirstOrDefault();
                if (target is not null) Act(new GameAction.Adulterate(target.Id, AdulterationMethod.Zucker));
            }
            // Rache: Wer das Schlitzohr bei einer Sabotage erwischt hat, bekommt im Sommer Besuch im Weinberg.
            var enemy = Rivals.ArchEnemy(s, Me());
            if (enemy is not null && (s.Month == 6 || s.Month == 7) &&
                Me().Money >= profile.CashReserve + Economy.Sabotage[SabotageKind.Weinberg].Cost + Economy.SabotageFine)
            {
                Act(new GameAction.Sabotage(enemy.Id, SabotageKind.Weinberg));
            }
            if (s.Month == 5)
            {
                var leader = s.Players
                    .Where(p => p.Id != Me().Id && !p.Bankrupt && p.Points > Me().Points)
                    .OrderByDescending(p => p.Points)
                    .FirstOrDefault();
                if (leader is not null && Me().Money >= profile.CashReserve + Economy.Sabotage[SabotageKind.Geruecht].Cost + Economy.SabotageFine)
                {
                    Act(new GameAction.Sabotage(leader.Id, SabotageKind.Geruecht));
                }
            }
        }

        // Notverkäufe: erst Fasswein, dann Großhandel, schwächste Weine zuerst – nur so viel wie nötig
        void RaiseCash(double amount)
        {
            var targetMoney = Me().Money + amount;
            foreach (var weakBatch in Me().Cellar.OrderBy(b => b.Quality).ToList())
            {
                var gap = targetMoney - Me().Money;
                if (gap <= 0) return;
                var sellHl = Math.Min(weakBatch.Hectoliters, Math.Ceiling(gap / Math.Max(1, Market.BulkPricePerHl(weakBatch, s, Me()))));
                Act(new GameAction.SellBulk(weakBatch.Id, sellHl));
            }
            foreach (var weakLot in Me().Bottles.OrderBy(l => l.Quality).ToList())
            {
                var gap = targetMoney - Me().Money;
                if (gap <= 0) return;
                if (!weakLot.Registered || Me().CompetitionEntries.Contains(weakLot.Id)) continue;
                var sellBottles = Math.Min(weakLot.Bottles, Math.Ceiling(gap / Math.Max(0.01, Market.WholesalePrice(weakLot, Me(), s))));
                Act(new GameAction.SellWholesale(weakLot.Id, sellBottles));
            }
        }

        // 1. Liquidität sichern: lieber Kredit als jungen Wein verramschen
        if (Me().Money < 0) EnsureCash(0, 0, true);
        if (Me().Money < 0) RaiseCash(-Me().Money);

        if (Me().Protection != profile.Protection) Act(new GameAction.SetProtection(profile.Protection));

        // Bio-Siegel beantragen, sobald es geht und die Gebühr die Reserve nicht angreift
        var nextLabel = Bio.NextBioLabel(Me());
        if (nextLabel is not null && Bio.MonthsUntilEligible(s, Me()) == 0 &&
            Me().Money - BioData.BioLabels[nextLabel.Value].Fee >= profile.CashReserve + 20_000)
        {
            Act(new GameAction.ApplyBioLabel());
        }

        // Exportanfrage nur annehmen, wenn der Vorrat für mehrere Monate reicht
        var exportOffer = Me().ExportOffer;
        if (exportOffer is not null)
        {
            var stock = Sum(Exports.ExportEligible(Me(), exportOffer), l => l.Bottles);
            Act(new GameAction.AnswerExport(stock >= exportOffer.BottlesPerMonth * 4));
        }

        // Gestaffeltes Wachstum: erst pflanzen, wenn die letzte Junganlage trägt; erst Land kaufen, wenn alles trägt.
        var hasYoungVines = Me().Plots.Any(p => p.Grape is not null && Rules.PlotAgeFactor(p, s.Year) == 0);
        var hasFallow = Me().Plots.Any(p => p.Grape is null);

        // 2. Versteigerung: Im Ankündigungsmonat ein verdecktes Höchstgebot hinterlegen
        if (s.Auction?.Phase == AuctionPhase.Announced && !hasYoungVines && !hasFallow)
        {
            double committed = 0;
            var lots = s.Auction.Lots
                .Where(l => l.RegionId == Me().RegionId && l.Status == LotStatus.Pending && !l.Limits.ContainsKey(Me().Id))
                .OrderByDescending(l => l.Estimate)
                .ToList();
            var hectares = Market.TotalHectares(Me());
            foreach (var lot in lots)
            {
                if (hectares + lot.Hectares > profile.MaxHectares) continue;
                var limit = Js.Round(lot.Estimate * profile.AuctionPremium / 1000) * 1000;
                if (limit < lot.StartPrice || !CanExpand(committed + limit) || limit > Auction.MaxAffordableBid(Me())) continue;
                if (Act(new GameAction.AuctionLimit(lot.Id, limit)))
                {
                    committed += limit;
                    hectares += lot.Hectares;
                }
            }
        }

        // Wetterabwehr: vorsichtige Profile versichern die Ernte, stellen bei hoher Frostgefahr Kerzen auf
        // und bewässern mit dem Klimawandel nach und nach ihre größte Lage.
        if (profile.Cautious && !Strained())
        {
            if (Economy.InsuranceMonths.Contains(s.Month) && Me().Money - Defense.InsurancePremium(s, Me()) >= profile.CashReserve)
            {
                Act(new GameAction.InsureHarvest());
            }
            if (Defense.FrostChance(s, Me()) >= 0.1 && Me().Money - Defense.FrostCandlesCost(s, Me()) >= profile.CashReserve)
            {
                Act(new GameAction.FrostCandles());
            }
            if (s.Month == 1 && Defense.DroughtChance(s, Me()) >= 0.08)
            {
                var dry = Me().Plots.Where(p => p.Grape is not null && !p.Irrigation).OrderByDescending(p => p.Hectares).FirstOrDefault();
                // installCost(dry, 'irrigation') des Originals: Fläche mal Preis je Hektar
                if (dry is not null && Me().Money - dry.Hectares * Economy.IrrigationCostPerHa >= profile.CashReserve + 40_000)
                {
                    Act(new GameAction.InstallIrrigation(dry.Id));
                }
            }
        }

        // Marktgespür: Kommt die eigene Sorte aus der Mode und ist eine andere gefragt, wird im Januar eine Parzelle gerodet.
        // Gepflanzt wird im Frühjahr wie gewohnt – nach Eignung und Marktrichtung.
        if (s.Month == 1 && !Strained() && !hasYoungVines && !hasFallow)
        {
            var region = Regions.All[Me().RegionId];
            var rising = profile.Grapes.Any(g => region.Suitability[g] >= 0 && s.Trends[(TrendKey)(int)g].Target >= 1.1);
            var fading = Me()
                .Plots.Where(p => p.Grape is not null && s.Trends[(TrendKey)(int)p.Grape.Value].Target <= 0.88 && Rules.PlotAgeFactor(p, s.Year) == 1)
                .OrderBy(p => p.Hectares)
                .FirstOrDefault();
            if (rising && fading is not null)
            {
                var cost = (Grapes.All[profile.Grapes[0]].PlantCostPerHa + Economy.ClearingCostPerHa) * fading.Hectares;
                if (Me().Money - cost >= profile.CashReserve + 30_000) Act(new GameAction.ClearPlot(fading.Id));
            }
        }

        // 3. Pflanzen: höchstens eine Parzelle pro Monat, beste Lage zuerst
        if (Rules.PlantMonths.Contains(s.Month) && !hasYoungVines)
        {
            var plot = Me().Plots.Where(p => p.Grape is null).OrderByDescending(p => p.Site).FirstOrDefault();
            if (plot is not null)
            {
                var region = Regions.All[Me().RegionId];
                // Eignung zählt am meisten; wohin der Markt läuft, gibt den Ausschlag (die Reben tragen erst in drei Jahren)
                double Score(GrapeId g) => region.Suitability[g] + (s.Trends[(TrendKey)(int)g].Target - 1) * 5;
                var grape = profile.Grapes.OrderByDescending(Score).First();
                var cost = Grapes.All[grape].PlantCostPerHa * plot.Hectares;
                if (CanExpand(cost) && EnsureCash(cost, 0))
                {
                    Act(new GameAction.Plant(plot.Id, grape));
                }
            }
        }

        // 4. Lese – eine kleine Nebenfläche bleibt in frostsicheren Gebieten für Eiswein hängen
        var bearingHa = Sum(Me().Plots.Where(p => p.Grape is not null && Rules.PlotAgeFactor(p, s.Year) > 0), p => p.Hectares);
        var iceWinePlot =
            profile.IceWine && Vineyard.IceWineChance(s, Me().RegionId) >= IceWineMinChance
                ? Me()
                    .Plots.Where(p => Game.ValidateAction(s, new GameAction.Harvest(p.Id)) == null && p.Oechsle >= 80)
                    .Where(p => p.IceWineGamble || p.Hectares <= bearingHa * IceWineMaxShare)
                    .OrderBy(p => p.Hectares)
                    .FirstOrDefault()
                : null;
        if (iceWinePlot is not null && s.Month == 11) Act(new GameAction.GambleIceWine(iceWinePlot.Id, true));
        if (Rules.HarvestMonths.Contains(s.Month) && s.Month >= profile.HarvestMonth) HarvestAll(iceWinePlot?.Id);

        // Sekt: im Frühjahr aus einem einfachen, ausgebauten Grundwein
        if (profile.Sekt && CellarMasters.HasSkill(Me(), CellarSkill.Sekt) && s.Month == 4 &&
            !Me().Bottles.Any(l => l.Sparkling && l.MonthsUntilReady > 0))
        {
            var baseWine = Me()
                .Cellar.Where(b => b.MonthsUntilReady == 0 && (b.Level == QualityLevel.Qualitaetswein || b.Level == QualityLevel.Kabinett))
                .OrderByDescending(b => b.Quality)
                .FirstOrDefault();
            if (baseWine is not null)
            {
                var hectoliters = Math.Min(SektHectoliters, baseWine.Hectoliters);
                if (EnsureCash(Market.HlToBottles(hectoliters) * Cellar.SektCost(Me()), 0, true))
                {
                    Act(new GameAction.MakeSekt(baseWine.Id, hectoliters));
                }
            }
        }

        // Barrique: den besten frischen Rotwein ins Eichenfass legen – nur mit einem Kellermeister, der das kann
        if (profile.Barrique && CellarMasters.HasSkill(Me(), CellarSkill.Barrique))
        {
            var hasRedVines = Me().Plots.Any(p => p.Grape is not null && Grapes.All[p.Grape.Value].Color == GrapeColor.Rot);
            if (hasRedVines && s.Year >= 3 && Me().BarriqueCapacityHl < BarriqueTargetHl && !Strained())
            {
                if (Me().Money >= profile.CashReserve + 80_000) Act(new GameAction.ExpandBarrique(100));
            }
            var free = Me().BarriqueCapacityHl - Rules.UsedBarriqueHl(Me());
            var candidate = Me()
                .Cellar.Where(b => !b.Barrique && b.Vintage == s.Year && b.Hectoliters <= free && Cellar.BarriqueBonus(b) > 2)
                .OrderByDescending(b => b.Quality)
                .FirstOrDefault();
            if (candidate is not null) Act(new GameAction.ToBarrique(candidate.Id));
        }

        // 5. Ausgebauten Wein abfüllen, sonst als Fasswein verkaufen
        foreach (var batch in Me().Cellar.Where(b => b.MonthsUntilReady == 0).ToList())
        {
            var cost = Market.HlToBottles(batch.Hectoliters) * Cellar.BottleCost(Me());
            if (!(EnsureCash(cost, 0, true) && Act(new GameAction.Bottle(batch.Id, batch.Hectoliters))))
            {
                Act(new GameAction.SellBulk(batch.Id, batch.Hectoliters));
            }
        }

        // 6. Amtliche Prüfung
        foreach (var lot in Me().Bottles.ToList())
        {
            if (EnsureCash(0, 0)) Act(new GameAction.Register(lot.Id));
        }

        // 7. Prämierungen: nur im Monat der Bewertung anmelden, damit der Wein nicht lange im Lager blockiert
        var competition = Economy.Competitions.Values.FirstOrDefault(c => c.Month == s.Month);
        if (competition is not null && profile.Competitions.Contains(competition.Id))
        {
            var candidates = Me()
                .Bottles.Where(l => l.Registered && !l.Awards.Any(a => a.Competition == competition.Id))
                .Where(l => Market.CompetitionBaseScore(l) + 2 >= competition.Thresholds[Medal.Bronze])
                .OrderByDescending(l => Market.CompetitionBaseScore(l))
                .ToList();
            foreach (var lot in candidates)
            {
                if (Me().Money - competition.Fee < profile.CashReserve / 2) break;
                Act(new GameAction.EnterCompetition(lot.Id));
            }
        }

        // Kundenaufträge: nur annehmen, was sofort geliefert werden kann – dann gleich liefern
        foreach (var orderOffer in Me().OrderOffers.ToList())
        {
            if (Me().Orders.Count >= Orders.MaxOrders) break;
            if (Orders.EligibleBottles(Me(), orderOffer) >= orderOffer.Bottles && Act(new GameAction.AcceptOrder(orderOffer.Id)))
            {
                Act(new GameAction.DeliverOrder(orderOffer.Id));
            }
        }

        // 8. Großhandel: Vorrat für Hofladen und Export behalten, die besten Weine zuerst
        SellSurplusWholesale();

        if (profile.Intrigue) Scheme();
        if (Me().Money < 0) RaiseCash(-Me().Money);

        // 9. Investitionen und Marketing
        var productiveHa = Sum(Me().Plots.Where(p => Rules.PlotAgeFactor(p, s.Year) > 0), p => p.Hectares);
        if (Strained())
        {
            if (Me().CellarMaster is not null) Act(new GameAction.FireCellarMaster());
            if (profile.Marketing.Contains(MarketingKind.Social) && Me().Money - Economy.Marketing[MarketingKind.Social].Cost >= profile.CashReserve)
            {
                Act(new GameAction.Marketing(MarketingKind.Social));
            }
        }
        else
        {
            if (profile.CellarMaster && s.Year >= 2 && productiveHa >= 6 && Me().Money >= profile.CashReserve + 30_000)
            {
                ChooseCellarMaster();
            }
            if (profile.OnlineShop && !Me().HasOnlineShop && Me().Reputation >= 25)
            {
                if (Me().Money >= profile.CashReserve + Economy.OnlineShopCost + 20_000) Act(new GameAction.BuyOnlineShop());
            }
            if (!Me().HasVinothek && Me().Money >= profile.CashReserve + Economy.VinothekCost + 100_000) Act(new GameAction.BuildVinothek());
            // Straußwirtschaft im Mai, wenn der Vorrat für eine gut besuchte Saison reicht
            var stock = Sum(Me().Bottles, l => l.Bottles);
            if (profile.KeepMonthsOfDemand >= 12 && s.Month == 5 && stock >= 4 * Market.MonthlyDemand(Me()))
            {
                if (Me().Money >= profile.CashReserve + Economy.StraussCost + 20_000) Act(new GameAction.OpenStrausswirtschaft());
            }
            foreach (var kind in profile.Marketing)
            {
                if (Me().Money - Economy.Marketing[kind].Cost >= profile.CashReserve + MarketingBuffer[kind]) Act(new GameAction.Marketing(kind));
            }
        }

        // 10. Kredit tilgen, wenn genug Polster da ist
        var surplus = Me().Money - profile.CashReserve - 20_000;
        if (Me().Loan > 0 && surplus >= 20_000)
        {
            Act(new GameAction.Repay(Math.Min(Me().Loan, Math.Floor(surplus / 1000) * 1000)));
        }

        // Abweichung vom Original (dort ein Fehler): Die Weinprüfung oder ein gelieferter Auftrag kann mitten im Zug
        // einen Sieger feststellen. Dann gibt es keinen Zug mehr zu beenden – das Original scheiterte hier mit
        // „Das Spiel ist beendet“ und die Partie blieb stehen.
        if (!string.IsNullOrEmpty(s.WinnerId) || s.Finished) return s;
        return Game.ApplyAction(s, new GameAction.EndTurn());
    }

    /// <summary>Lässt alle Computergegner ziehen, bis ein Mensch am Zug ist oder das Spiel endet.</summary>
    public static GameState RunAiTurns(GameState state)
    {
        var s = state;
        for (var guard = 0; guard < 10_000; guard++)
        {
            if (!string.IsNullOrEmpty(s.WinnerId) || s.Finished || Rules.CurrentPlayer(s).Ai is null) return s;
            if (ActiveHumans(s).Count == 0) return s; // ohne Menschen nicht endlos weiterspielen
            s = PlayAiTurn(s);
        }
        return s;
    }

    /// <summary>Spieler, die noch mitspielen und von Menschen gesteuert werden</summary>
    public static List<Player> ActiveHumans(GameState state) => state.Players.Where(p => p.Ai is null && !p.Bankrupt).ToList();
}
