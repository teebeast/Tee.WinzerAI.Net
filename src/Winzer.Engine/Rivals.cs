namespace Winzer.Engine;

/// <summary>
/// Lebendige Gegner: Computergegner merken sich, wie andere Weingüter mit ihnen umgehen. Freundliche Nachbarn
/// bieten Kooperationen an (siehe Decisions), verstimmte starten Rabattaktionen oder beschweren sich öffentlich,
/// und das Schlitzohr sabotiert seinen Erzfeind. Mit der Zeit beruhigen sich die Gemüter.
/// </summary>
public static class Rivals
{
    /// <summary>Anteil des Verhältnisses, der jeden Monat erhalten bleibt – der Rest verblasst</summary>
    const double RelationMemory = 0.95;
    const double PriceWarRelation = -30;
    const double PriceWarChance = 0.05;
    const double ComplaintRelation = -60;
    const double ComplaintChance = 0.04;
    const double ComplaintReputation = 3;
    public const double RevengeRelation = -40;

    public static double Relation(Player ai, string otherId) => ai.Relations.TryGetValue(otherId, out var value) ? value : 0;

    /// <summary>Ändert das Verhältnis eines Computergegners zu einem anderen Weingut. Menschen führen keine Liste.</summary>
    public static void AdjustRelation(Player ai, string otherId, double delta)
    {
        if (ai.Ai is null || ai.Id == otherId) return;
        ai.Relations[otherId] = Math.Max(-100, Math.Min(100, Js.Round((Relation(ai, otherId) + delta) * 10) / 10));
    }

    /// <summary>Alle Computergegner eines Gebiets reagieren auf dasselbe Ereignis</summary>
    public static void AdjustRegionRelations(GameState state, RegionId regionId, string otherId, double delta)
    {
        foreach (var ai in state.Players)
        {
            if (ai.Ai is not null && !ai.Bankrupt && ai.RegionId == regionId) AdjustRelation(ai, otherId, delta);
        }
    }

    public static (string Icon, string Text) RelationLabel(double value)
    {
        if (value >= 40) return ("😊", Lang.T("befreundet", "friends"));
        if (value >= 10) return ("🙂", Lang.T("freundlich", "friendly"));
        if (value > -10) return ("😐", Lang.T("neutral", "neutral"));
        if (value > PriceWarRelation - 10) return ("😒", Lang.T("verstimmt", "annoyed"));
        return ("😠", Lang.T("verfeindet", "hostile"));
    }

    /// <summary>Der Gegner, den ein Computergegner am wenigsten leiden kann – sofern es so schlimm ist</summary>
    public static Player? ArchEnemy(GameState state, Player ai, double threshold = RevengeRelation)
    {
        return state.Players
            .Where(p => p.Id != ai.Id && !p.Bankrupt && Relation(ai, p.Id) <= threshold)
            .OrderBy(p => Relation(ai, p.Id))
            .FirstOrDefault();
    }

    /// <summary>Monatsende: Groll verblasst, verstimmte Nachbarn werden aktiv.</summary>
    public static void MonthlyRivalry(GameState state)
    {
        foreach (var ai in state.Players.Where(p => p.Ai is not null && !p.Bankrupt).ToList())
        {
            foreach (var other in state.Players.Where(p => p.Id != ai.Id && !p.Bankrupt).ToList())
            {
                var value = Relation(ai, other.Id);
                if (value == 0) continue;
                ai.Relations[other.Id] = Math.Abs(value) < 1 ? 0 : Js.Round(value * RelationMemory * 10) / 10;

                var sameRegion = other.RegionId == ai.RegionId;
                var priceWarRunning = other.Effects.Any(e => e.Label == PriceWarLabel(ai, false) || e.Label == PriceWarLabel(ai, true));
                if (sameRegion && value <= PriceWarRelation && !priceWarRunning && Rng.NextFloat(state.Rng) < PriceWarChance)
                {
                    Events.AddEffect(other, EffectKind.Demand, 0.85, 3, PriceWarLabel(ai, Lang.En));
                    Util.AddLog(state, null, Lang.En ? $"{ai.Name} launches a discount campaign against {other.Name}: three months of 15% fewer cellar-door customers." : $"{ai.Name} startet eine Rabattaktion gegen {other.Name}: drei Monate 15 % weniger Kundschaft ab Hof.", LogTone.Bad);
                }
                if (value <= ComplaintRelation && Rng.NextFloat(state.Rng) < ComplaintChance)
                {
                    Rules.ChangeReputation(other, -ComplaintReputation);
                    Util.AddLog(state, null, Lang.En ? $"{ai.Name} publicly complains about {other.Name} (reputation −{Util.JsNumber(ComplaintReputation)})." : $"{ai.Name} beschwert sich öffentlich über {other.Name} (Bekanntheit −{Util.JsNumber(ComplaintReputation)}).", LogTone.Bad);
                }
            }
        }
    }

    static string PriceWarLabel(Player ai, bool en) => en ? $"Discount campaign by {ai.Name}" : $"Rabattaktion von {ai.Name}";
}
