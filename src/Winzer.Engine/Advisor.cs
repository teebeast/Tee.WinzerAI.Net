using System.Text.Json.Serialization;
using Winzer.Engine.Data;

namespace Winzer.Engine;

/// <summary>Bereich der Oberfläche, in dem sich ein Hinweis erledigen lässt</summary>
public enum HintArea
{
    [JsonStringEnumMemberName("hof")] Hof,
    [JsonStringEnumMemberName("weinberg")] Weinberg,
    [JsonStringEnumMemberName("keller")] Keller,
    [JsonStringEnumMemberName("auftraege")] Auftraege,
    [JsonStringEnumMemberName("betrieb")] Betrieb,
}

public sealed class Hint
{
    public string Text { get; set; } = "";
    /// <summary>Wichtige Hinweise unterbrechen das Vorspulen</summary>
    public bool Important { get; set; }
    public HintArea Area { get; set; }
}

/// <summary>advisor.ts</summary>
public static class Advisor
{
    /// <summary>Was steht diesen Monat an? Grundlage für die Aufgabenliste und das Vorspulen.</summary>
    public static List<Hint> MonthHints(GameState state, Player player)
    {
        var hints = new List<Hint>();
        void Add(HintArea area, string text, bool important = true) => hints.Add(new Hint { Text = text, Important = important, Area = area });
        var bearing = player.Plots.Where(p => p.Grape is not null && !p.HarvestedThisYear && Rules.PlotAgeFactor(p, state.Year) > 0).ToList();

        if (player.Money < 0) Add(HintArea.Betrieb, Lang.T("Konto im Minus – Kredit aufnehmen oder Wein verkaufen.", "Account overdrawn – take a loan or sell wine."));
        if (player.MonthsInsolvent > 0) Add(HintArea.Betrieb, Lang.T("Insolvenzgefahr: Es ist kein Kreditrahmen mehr frei!", "Risk of insolvency: no credit line left!"));
        if (player.Decision is not null)
        {
            var nextMonth = (player.Decision.Deadline ?? 0) > Bio.GameMonth(state);
            Add(HintArea.Hof, Lang.En ? $"Decision needed: {player.Decision.Title} – by {(nextMonth ? "the end of next month" : "month end")}." : $"Entscheidung gefragt: {player.Decision.Title} – bis {(nextMonth ? "Ende nächsten Monats" : "Monatsende")}.");
        }
        var deliverable = player.Orders.Where(o => Orders.EligibleBottles(player, o) >= o.Bottles).ToList();
        if (deliverable.Count > 0)
        {
            Add(HintArea.Auftraege, Lang.En ? $"📦 {deliverable.Count} order(s) can be delivered now." : $"📦 {deliverable.Count} Auftrag/Aufträge können jetzt geliefert werden.");
        }
        var urgent = player.Orders.Where(o => o.Deadline - Bio.GameMonth(state) <= 1 && !deliverable.Contains(o)).ToList();
        if (urgent.Count > 0)
        {
            Add(HintArea.Auftraege, Lang.En ? $"⏰ {urgent.Count} order(s) expire soon – matching bottles are missing. Otherwise a contract penalty looms." : $"⏰ {urgent.Count} Auftrag/Aufträge laufen bald ab – es fehlen passende Flaschen. Sonst droht Vertragsstrafe.");
        }
        if (player.OrderOffers.Count > 0)
        {
            var fulfillable = player.OrderOffers.Any(o => Orders.EligibleBottles(player, o) >= o.Bottles);
            Add(HintArea.Auftraege, Lang.En ? $"📦 {player.OrderOffers.Count} {(player.OrderOffers.Count == 1 ? "order request is" : "order requests are")} waiting{(fulfillable ? " – you can deliver one right away" : "")}." : $"📦 {player.OrderOffers.Count} Auftragsanfrage(n) liegen vor{(fulfillable ? " – eine davon kannst du sofort liefern" : "")}.", fulfillable);
        }
        if (player.ExportOffer is not null) Add(HintArea.Auftraege, Lang.En ? $"Export request from {Exports.CountryName(player.ExportOffer.Country)} – valid this month only." : $"Exportanfrage aus {player.ExportOffer.Country} – nur diesen Monat gültig.");
        var lots = state.Auction?.Lots.Where(l => l.RegionId == player.RegionId && l.Status != LotStatus.Sold && l.Status != LotStatus.Unsold).ToList() ?? new List<AuctionLot>();
        if (state.Auction?.Phase == AuctionPhase.Announced && lots.Count > 0)
        {
            Add(HintArea.Hof, Lang.En ? $"📣 Next month {lots.Count} vineyard(s) in your region go to auction – have money ready." : $"📣 Nächsten Monat werden {lots.Count} Weinberg(e) in deinem Gebiet versteigert – Geld bereithalten.");
        }
        if (state.Auction?.Phase == AuctionPhase.Live && lots.Any(l => l.Awaiting is not null))
        {
            Add(HintArea.Hof, Lang.T("🔨 Live-Versteigerung läuft – jetzt mitbieten!", "🔨 Live auction under way – bid now!"));
        }
        if (player.Cellar.Any(b => Products.IsFreshMust(state, b)) && Economy.FederweisserMonths.Contains(state.Month) && Products.FederweisserCapacity(player) > 0)
        {
            Add(HintArea.Keller, Lang.En ? $"🥂 Federweisser season: you can sell up to {Util.JsNumber(Products.FederweisserCapacity(player))} hl of fresh must directly this month." : $"🥂 Federweißer-Zeit: Bis zu {Util.JsNumber(Products.FederweisserCapacity(player))} hl frischen Most kannst du diesen Monat direkt verkaufen.", false);
        }
        var sellable = 0.0;
        foreach (var lot in player.Bottles)
        {
            if (lot.MonthsUntilReady == 0) sellable += lot.Bottles;
        }
        if (state.Month is 4 or 5 or 6 && player.StraussYear != state.Year && sellable >= 1000)
        {
            Add(HintArea.Betrieb, Lang.T("🌳 Ein guter Zeitpunkt für die Straußwirtschaft: vier Monate mehr Gäste ab Hof.", "🌳 A good time for the wine tavern: four months of more cellar-door guests."), false);
        }
        var nextLabel = Bio.NextBioLabel(player);
        if (nextLabel is not null && Bio.MonthsUntilEligible(state, player) == 0)
        {
            var info = BioData.BioLabels[nextLabel.Value];
            Add(HintArea.Weinberg, Lang.En ? $"{info.Icon} The {info.Name} seal can be applied for now ({Util.FormatEuro(info.Fee)})." : $"{info.Icon} Das {info.Name}-Siegel kann jetzt beantragt werden ({Util.FormatEuro(info.Fee)}).");
        }
        var frost = Defense.FrostChance(state, player);
        if (frost > 0 && !player.FrostCandles && bearing.Count > 0)
        {
            Add(
                HintArea.Weinberg,
                Lang.En ? $"🥶 Late frost risk this month: {Util.JsNumber(Js.Round(frost * 100))}% – set up frost candles for {Util.FormatEuro(Defense.FrostCandlesCost(state, player))}?" : $"🥶 Spätfrostgefahr diesen Monat: {Util.JsNumber(Js.Round(frost * 100))} % – Frostkerzen für {Util.FormatEuro(Defense.FrostCandlesCost(state, player))} bereitstellen?",
                frost >= 0.08);
        }
        if (Economy.InsuranceMonths.Contains(state.Month) && !Defense.IsInsured(state, player) && Defense.InsurancePremium(state, player) > 0)
        {
            Add(HintArea.Weinberg, Lang.En ? $"🛡️ This year's harvest is not insured yet (premium {Util.FormatEuro(Defense.InsurancePremium(state, player))})." : $"🛡️ Die Ernte ist für dieses Jahr noch nicht versichert (Prämie {Util.FormatEuro(Defense.InsurancePremium(state, player))}).", false);
        }
        // Zur Versteigerung angemeldete Brachen lassen sich nicht bepflanzen – kein Hinweis ohne mögliche Aktion
        if (Rules.PlantMonths.Contains(state.Month) && player.Plots.Any(p => p.Grape is null && !p.ForAuction))
        {
            Add(HintArea.Weinberg, Lang.T("Pflanzzeit: Brachflächen können bestockt werden.", "Planting season: fallow land can be planted."));
        }

        if (Rules.HarvestMonths.Contains(state.Month) && bearing.Count > 0)
        {
            var ripe = bearing.Where(p => !p.IceWineGamble).ToList();
            if (state.Month >= 11 && ripe.Count > 0)
            {
                Add(HintArea.Weinberg, Lang.En ? $"Last chance: {ripe.Count} plot(s) not harvested yet – unharvested grapes spoil at year end." : $"Letzte Chance: {ripe.Count} Parzelle(n) noch nicht gelesen – ungelesene Trauben verderben zum Jahresende.");
            }
            else if (ripe.Count > 0)
            {
                var best = ripe
                    .Select(p => Game.HarvestPreview(state, player, p))
                    .OrderByDescending(h => h.Quality)
                    .First();
                Add(
                    HintArea.Weinberg,
                    Lang.En ? $"Harvest time: up to {Quality.QualityInfo(best.Level).Name} possible. Harvest now or gamble on more ripeness?" : $"Lesezeit: bis zu {Quality.QualityInfo(best.Level).Name} möglich. Jetzt lesen oder auf mehr Reife pokern?",
                    best.Level != QualityLevel.DeutscherWein);
            }
            if (state.Month >= 11 && bearing.Any(p => !p.IceWineGamble && p.Oechsle >= Economy.IceWineMinOechsle))
            {
                Add(HintArea.Weinberg, Lang.T("Eiswein möglich: Trauben bis zum Frost hängen lassen?", "Eiswein possible: leave the grapes hanging until frost?"), false);
            }
        }

        var ready = player.Cellar.Where(b => b.MonthsUntilReady == 0).ToList();
        if (ready.Count > 0) Add(HintArea.Keller, Lang.En ? $"{ready.Count} wine(s) matured in the cellar – bottle, make sparkling wine or sell as bulk wine." : $"{ready.Count} Wein(e) im Keller ausgebaut – abfüllen, Sekt machen oder als Fasswein verkaufen.");
        var unregistered = player.Bottles.Where(
            l => !l.Registered && l.MonthsUntilReady == 0 && (l.Sparkling || l.Level != QualityLevel.DeutscherWein) && l.Bottles >= Economy.RegisterMinBottles).ToList();
        if (unregistered.Count > 0) Add(HintArea.Keller, Lang.En ? $"{unregistered.Count} lot(s) awaiting official testing." : $"{unregistered.Count} Posten warten auf die amtliche Prüfung.");

        var competition = Economy.Competitions.Values.FirstOrDefault(c => c.Month == state.Month);
        // Nur wenn sich wirklich noch ein Wein anmelden lässt – nicht, wenn alle Plätze belegt oder die Weine schon dabei sind
        if (competition is not null && player.Bottles.Any(l => Market.CompetitionEntryProblem(player, l, competition) is null))
        {
            Add(HintArea.Keller, Lang.En ? $"{competition.Name} at month end – enter wines?" : $"{competition.Name} am Monatsende – Weine anmelden?");
        }
        hints.AddRange(RulesV2.Hints(state, player));
        if (hints.Count == 0) Add(HintArea.Betrieb, Lang.En ? $"Quiet {Game.MonthNames[state.Month - 1]} – time for marketing or investments." : $"Ruhiger {Game.MonthNames[state.Month - 1]} – Zeit für Marketing oder Investitionen.", false);
        return hints;
    }

    /// <summary>Wichtige Hinweise des Spielers am Zug – sie beenden sein Vorspulen</summary>
    public static List<Hint> ImportantHints(GameState state)
    {
        return MonthHints(state, Rules.CurrentPlayer(state)).Where(h => h.Important).ToList();
    }

    /// <summary>
    /// Nach einem Zugende: Die KI zieht, und wer vorspult, dessen Züge laufen von selbst durch – bis bei ihm etwas
    /// Wichtiges ansteht (auch eine Sabotage, auf die er reagieren kann), ein Jahr um ist oder das Spiel endet.
    /// Jeder Mensch spult für sich: Wer nicht vorspult, kommt wie gewohnt jeden Monat dran.
    /// </summary>
    public static GameState Advance(GameState state)
    {
        var s = Ai.RunAiTurns(state);
        for (var guard = 0; guard < 10_000; guard++)
        {
            if (!string.IsNullOrEmpty(s.WinnerId) || s.Finished || Ai.ActiveHumans(s).Count() == 0) return s;
            var skip = Rules.CurrentPlayer(s).AutoSkip;
            if (skip is null) return s;
            if (Bio.GameMonth(s) >= skip.Until || ImportantHints(s).Count > 0)
            {
                s = s.Clone();
                Rules.CurrentPlayer(s).AutoSkip = null;
                return s;
            }
            s = Ai.RunAiTurns(Game.ApplyAction(s, new GameAction.EndTurn()));
        }
        return s;
    }
}
