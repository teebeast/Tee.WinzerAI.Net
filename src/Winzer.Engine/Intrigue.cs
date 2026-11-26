using Winzer.Engine.Data;

namespace Winzer.Engine;

/// <summary>Kellertricks und Sabotage (intrigue.ts)</summary>
public static class Intrigue
{
    public static string? ValidateAdulterate(Player player, string batchId, AdulterationMethod method)
    {
        var batch = player.Cellar.Find(b => b.Id == batchId);
        if (batch is null) return Lang.T("Unbekannte Charge.", "Unknown batch.");
        if (player.IntrigueThisMonth) return Lang.T("Für diesen Monat hast du schon genug riskiert.", "You have risked enough this month.");
        if (batch.Adulterated is not null) return Lang.T("An diesem Wein wurde schon gedreht.", "This wine has already been tampered with.");
        if (Economy.Adulteration[method].LevelSteps > 0 && Rules.RaiseLevel(batch.Level, 1) == batch.Level)
        {
            return Lang.T("Höher geht es mit dieser Stufe nicht.", "This level cannot go any higher.");
        }
        if (batch.Level == QualityLevel.Eiswein && Economy.Adulteration[method].LevelSteps > 0) return Lang.T("Eiswein lässt sich nicht weiter aufwerten.", "Eiswein cannot be upgraded further.");
        return null;
    }

    /// <summary>Heimliche Kellertricks: mehr Menge oder eine höhere Stufe – mit Entdeckungsrisiko</summary>
    public static void Adulterate(GameState state, Player player, string batchId, AdulterationMethod method)
    {
        var batch = player.Cellar.Find(b => b.Id == batchId)!;
        var info = Economy.Adulteration[method];
        var before = Rules.WineLabel(batch);
        batch.Hectoliters = Js.Round(batch.Hectoliters * info.VolumeFactor);
        batch.Quality = Math.Max(0, batch.Quality + info.QualityDelta);
        batch.Level = Rules.RaiseLevel(batch.Level, info.LevelSteps);
        batch.Adulterated = method;
        player.IntrigueThisMonth = true;
        Util.AddLog(state, player.Id, Lang.En ? $"Secretly in the cellar: {info.Name} on {before}. Hopefully nobody notices …" : $"Heimlich im Keller: {info.Name} bei {before}. Hoffentlich merkt es niemand …");
    }

    /// <summary>Bei der amtlichen Prüfung fliegt Gepanschtes womöglich auf. Liefert true bei Entdeckung.</summary>
    public static bool InspectAtRegistration(GameState state, Player player, BottleLot lot)
    {
        if (lot.Adulterated is not { } method) return false;
        if (Rng.NextFloat(state.Rng) >= Economy.Adulteration[method].Detection) return false;
        Scandal(state, player, Lang.T("bei der amtlichen Prüfung", "during the official testing"));
        return true;
    }

    /// <summary>Unangemeldete Kontrolle der Weinaufsicht, solange gepanschter Wein im Betrieb lagert</summary>
    public static void RandomControl(GameState state, Player player)
    {
        var hasFake = player.Cellar.Any(b => b.Adulterated is not null) || player.Bottles.Any(l => l.Adulterated is not null);
        if (hasFake && Rng.NextFloat(state.Rng) < Economy.ControlChance) Scandal(state, player, Lang.T("bei einer Kontrolle der Weinaufsicht", "during a wine inspectorate check"));
    }

    /// <summary>Weinskandal: Alle gepanschten Weine werden beschlagnahmt, dazu Strafe, Punktabzug und Imageschaden.</summary>
    static void Scandal(GameState state, Player player, string where)
    {
        var seizedHl = 0.0;
        foreach (var b in player.Cellar) if (b.Adulterated is not null) seizedHl += b.Hectoliters;
        var seizedBottlesHl = 0.0;
        foreach (var l in player.Bottles) if (l.Adulterated is not null) seizedBottlesHl += l.Bottles / 133;
        var seized = seizedHl + seizedBottlesHl;
        player.Cellar = player.Cellar.Where(b => b.Adulterated is null).ToList();
        var seizedLots = new HashSet<string>(player.Bottles.Where(l => l.Adulterated is not null).Select(l => l.Id));
        player.Bottles = player.Bottles.Where(l => !seizedLots.Contains(l.Id)).ToList();
        player.CompetitionEntries = player.CompetitionEntries.Where(id => !seizedLots.Contains(id)).ToList();
        player.Money -= Economy.ScandalFine;
        player.Points = Math.Max(0, player.Points - Economy.ScandalPoints);
        Rules.ChangeReputation(player, -Economy.ScandalReputation);
        Bio.LoseBio(state, player, Lang.T("Weinskandal", "wine scandal"));
        // Ein Skandal schadet dem Ruf des ganzen Gebiets – die Nachbarn sind sauer
        Rivals.AdjustRegionRelations(state, player.RegionId, player.Id, -10);
        Util.AddLog(
            state,
            null,
            Lang.En
                ? $"Wine scandal! {player.Name} was caught adulterating wine {where}: {Util.JsNumber(Js.Round(seized))} hl seized, " +
                  $"{Util.FormatEuro(Economy.ScandalFine)} fine, −{Util.JsNumber(Economy.ScandalPoints)} points."
                : $"Weinskandal! {player.Name} wurde {where} beim Panschen erwischt: {Util.JsNumber(Js.Round(seized))} hl beschlagnahmt, " +
                  $"{Util.FormatEuro(Economy.ScandalFine)} Strafe, −{Util.JsNumber(Economy.ScandalPoints)} Punkte.");
    }

    public static string? ValidateSabotage(GameState state, Player player, string targetId, SabotageKind kind)
    {
        var target = state.Players.Find(p => p.Id == targetId);
        if (target is null || target.Id == player.Id) return Lang.T("Ungültiges Ziel.", "Invalid target.");
        if (target.Bankrupt) return Lang.T("Dieses Weingut gibt es nicht mehr.", "This estate no longer exists.");
        if (player.IntrigueThisMonth) return Lang.T("Für diesen Monat hast du schon genug riskiert.", "You have risked enough this month.");
        if (player.Money < Economy.Sabotage[kind].Cost) return Lang.En ? $"Not enough money (needs {Util.FormatEuro(Economy.Sabotage[kind].Cost)})." : $"Nicht genug Geld (benötigt {Util.FormatEuro(Economy.Sabotage[kind].Cost)}).";
        if (kind == SabotageKind.Weinberg && SabotageablePlots(state, target).Count == 0) return Lang.T("Dort hängen gerade keine Trauben.", "There are no grapes hanging there right now.");
        return null;
    }

    static List<Plot> SabotageablePlots(GameState state, Player target) =>
        target.Plots.Where(p => p.Grape is not null && !p.HarvestedThisYear && Rules.PlotAgeFactor(p, state.Year) > 0).ToList();

    public static void Sabotage(GameState state, Player player, string targetId, SabotageKind kind)
    {
        var target = state.Players.Find(p => p.Id == targetId)!;
        player.Money -= Economy.Sabotage[kind].Cost;
        player.IntrigueThisMonth = true;

        // Eine Nachtwache erwischt Saboteure fast immer
        var guarded = target.Effects.Any(e => e.Kind == EffectKind.Guard);
        if (Rng.NextFloat(state.Rng) < (guarded ? Economy.GuardDetection : Economy.SabotageDetection))
        {
            player.Money -= Economy.SabotageFine;
            Rules.ChangeReputation(player, -Economy.SabotageReputation);
            Util.AddLog(
                state,
                null,
                Lang.En ? $"{player.Name} was caught {(guarded ? "by the night watch " : "")}sabotaging {target.Name}: {Util.FormatEuro(Economy.SabotageFine)} fine." : $"{player.Name} wurde {(guarded ? "von der Nachtwache " : "")}bei einer Sabotage gegen {target.Name} erwischt: {Util.FormatEuro(Economy.SabotageFine)} Strafe.");
            Rivals.AdjustRelation(target, player.Id, -60); // das vergisst so schnell niemand
            Decisions.OfferSabotageReaction(state, target, player, kind, true);
            return;
        }
        if (kind == SabotageKind.Geruecht)
        {
            Rules.ChangeReputation(target, -Economy.SabotageRumorReputation);
            Util.AddLog(state, target.Id, Lang.En ? $"Nasty rumors about your estate are going around (−{Util.JsNumber(Economy.SabotageRumorReputation)} reputation)." : $"Üble Gerüchte über dein Weingut machen die Runde (−{Util.JsNumber(Economy.SabotageRumorReputation)} Bekanntheit).");
        }
        else
        {
            var plots = SabotageablePlots(state, target);
            var plot = plots[(int)Math.Floor(Rng.NextFloat(state.Rng) * plots.Count)];
            plot.CropFactor *= 1 - Economy.SabotageVineyardLoss;
            Util.AddLog(state, target.Id, Lang.En ? $"Unknown persons damaged vines in {plot.Name} (−{Util.JsNumber(Economy.SabotageVineyardLoss * 100)}% yield)." : $"Unbekannte haben Rebstöcke in {plot.Name} beschädigt (−{Util.JsNumber(Economy.SabotageVineyardLoss * 100)} % Ertrag).");
        }
        Util.AddLog(state, player.Id, Lang.En ? $"{Economy.Sabotage[kind].Name} against {target.Name} succeeded unnoticed." : $"{Economy.Sabotage[kind].Name} gegen {target.Name} ist unbemerkt geglückt.");
        Decisions.OfferSabotageReaction(state, target, player, kind, false);
    }
}
