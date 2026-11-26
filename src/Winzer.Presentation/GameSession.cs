using Winzer.Engine;

namespace Winzer.Presentation;

/// <summary>Übersicht nach dem Vorspulen und beim Spielerwechsel</summary>
public sealed class Summary
{
    /// <summary>Wer jetzt am Zug ist</summary>
    public string PlayerName { get; init; } = "";
    /// <summary>Ein anderer Mensch ist jetzt dran</summary>
    public bool Changed { get; init; }
    /// <summary>Vorgespulte Monate (0 beim einfachen Spielerwechsel)</summary>
    public int Months { get; init; }
    /// <summary>Wichtige Hinweise, die jetzt anstehen</summary>
    public List<Hint> Reasons { get; init; } = [];
    /// <summary>Neuigkeiten seit dem letzten Zug dieses Spielers</summary>
    public List<LogEntry> Entries { get; init; } = [];
    public bool Over { get; init; }
    public bool Skipped { get; init; }
}

/// <summary>
/// Eine laufende Partie: führt Züge aus, lässt Computergegner und Vorspulen laufen, sichert nach jedem Zug
/// und hält fest, was die Oberfläche zeigen soll. Entspricht der Logik von Game.svelte und App.svelte im Original.
/// </summary>
public sealed class GameSession
{
    readonly ISaveStore store;
    readonly IAudio audio;
    /// <summary>Wer gerade vorspult – seit welchem Spielmonat</summary>
    readonly Dictionary<string, int> skipping = [];
    /// <summary>Prämierungen und Jahresrückblicke, die ein Mensch schon gesehen hat</summary>
    readonly HashSet<string> shownShows = [];
    readonly HashSet<string> shownReviews = [];
    /// <summary>Was jeder Mensch zuletzt gesehen hat</summary>
    readonly Dictionary<string, (int From, HashSet<string> Keys)> seen = [];

    public GameSession(GameState state, string saveId, ISaveStore store, IAudio audio)
    {
        State = state;
        SaveId = saveId;
        this.store = store;
        this.audio = audio;
        Nav.Changed += () => Changed?.Invoke();
        // Geladene Partie: Was schon im Spielstand steht, gilt als gesehen
        foreach (var p in state.Players)
        {
            if (state.LastShow is { } show) shownShows.Add($"{p.Id}|{show.Competition}|{show.Year}");
            if (p.Review is { } review) shownReviews.Add($"{p.Id}|{review.Year}");
        }
        var first = Rules.CurrentPlayer(state);
        if (RulesV2.On(state) && first.Ai is null && (first.Ambitions.Count < RulesV2.AmbitionCount || (first.Goal is null && first.GoalOffers.Count > 0)))
        {
            Nav.Overlay = Overlay.Plan;
        }
    }

    public GameState State { get; private set; }
    public string SaveId { get; }
    public Navigator Nav { get; } = new();
    /// <summary>Meldung zum letzten abgelehnten Zug</summary>
    public string Error { get; private set; } = "";
    public Summary? Summary { get; private set; }
    public string ChallengeMessage { get; private set; } = "";
    public bool SaveFailed { get; private set; }

    /// <summary>Sichert den Stand sofort – etwa vor dem Beenden. Liefert, ob es geklappt hat.</summary>
    public bool Save()
    {
        SaveFailed = !store.Write(SaveId, State);
        return !SaveFailed;
    }
    public event Action? Changed;

    public Player Player => Rules.CurrentPlayer(State);
    /// <summary>Mehrere Menschen am selben Gerät</summary>
    public bool HotSeat => State.Players.Count(p => p.Ai is null) > 1;
    public Player? Winner => State.Players.FirstOrDefault(p => p.Id == State.WinnerId);
    public bool Over => IsOver(State);
    /// <summary>Live-Versteigerung, bei der gerade ein Mensch bieten muss</summary>
    public AuctionLot? LiveLot => Over ? null : Auction.AwaitingLot(State);

    static bool IsOver(GameState s) => !string.IsNullOrEmpty(s.WinnerId) || s.Finished || !Ai.ActiveHumans(s).Any();
    static int TimeOf(int year, int month) => year * 12 + month;
    static string LogKey(LogEntry e) => $"{TimeOf(e.Year, e.Month)}|{e.PlayerId}|{e.Text}";
    static bool IsNews(LogEntry e) => e.Tone is not null || e.Text.StartsWith("📣") || e.Text.StartsWith("🔨");

    /// <summary>Warum ein Zug gerade nicht geht – null, wenn er möglich ist.</summary>
    public string? Problem(GameAction action) => Game.ValidateAction(State, action);

    public void ClearError()
    {
        if (Error.Length == 0) return;
        Error = "";
        Changed?.Invoke();
    }

    public void CloseSummary(Scene? goTo = null)
    {
        Summary = null;
        if (goTo is { } scene) Nav.Go(scene);
        else Changed?.Invoke();
    }

    public void Run(GameAction action)
    {
        try
        {
            var before = Player;
            var endsTurn = action is GameAction.EndTurn or GameAction.AutoSkipTurn;
            if (endsTurn) MarkSeen(State, before.Id);
            if (action is GameAction.AutoSkipTurn) skipping[before.Id] = Bio.GameMonth(State);
            // Nach einem Zug ziehen die Computergegner und die Züge der Vorspulenden laufen durch, bis ein Mensch dran ist.
            var next = endsTurn ? Advisor.Advance(Game.ApplyAction(State, action)) : Game.ApplyAction(State, action);
            Commit(next, endsTurn, before);
            if (!endsTurn) return;
            var now = Rules.CurrentPlayer(next);
            // Wer jetzt dran ist und vorgespult hat, ist fertig damit – oder das Spiel endete während des Vorspulens
            var skipper = skipping.ContainsKey(now.Id) && now.AutoSkip is null ? now.Id
                : IsOver(next) && skipping.ContainsKey(before.Id) ? before.Id
                : null;
            if (skipper is not null)
            {
                var since = skipping[skipper];
                skipping.Remove(skipper);
                ShowSummary(next, before.Id, Bio.GameMonth(next) - since, IsOver(next) ? [] : Advisor.ImportantHints(next), true);
            }
            else if (HotSeat && now.Id != before.Id && !IsOver(next))
            {
                // Mehrere Menschen am Gerät: Die Übersicht verdeckt den Wechsel und zeigt dem Nächsten, was ansteht
                ShowSummary(next, before.Id, 0, Advisor.ImportantHints(next), false);
            }
            Changed?.Invoke();
        }
        catch (RuleException e)
        {
            Error = e.Message;
            Changed?.Invoke();
        }
    }

    void Commit(GameState next, bool endedTurn, Player before)
    {
        Error = "";
        var previous = State;
        if (next.Finished && next.Challenge is not null && !previous.Finished)
        {
            var me = next.Players.First(p => p.Ai is null);
            var best = ChallengeBest(next.Challenge);
            ChallengeMessage = RecordChallengeResult(next.Challenge, me.Points)
                ? Lang.En ? $"New best of the day: {Util.JsNumber(me.Points)} points!" : $"Neuer Tagesbestwert: {Util.JsNumber(me.Points)} Punkte!"
                : Lang.En ? $"{Util.JsNumber(me.Points)} points – your best today: {Util.JsNumber(best ?? 0)} points." : $"{Util.JsNumber(me.Points)} Punkte – dein Bestwert heute: {Util.JsNumber(best ?? 0)} Punkte.";
        }
        audio.Play(Sounds.ForChange(previous, next) ?? Sound.Click);
        State = next;
        SaveFailed = !store.Write(SaveId, next);
        if (endedTurn && HotSeat && Rules.CurrentPlayer(next).Id != before.Id) Nav.Go(Scene.Gut);
        // Was neu ist, kommt zum Spieler, statt in einer Ecke zu warten: Prämierung, Jahresrückblick, Entscheidung, Jahresziel
        var now = Rules.CurrentPlayer(next);
        if (now.Decision is null && Nav.Overlay == Overlay.Decision) Nav.Overlay = Overlay.None;
        if (now.Ai is null && !IsOver(next))
        {
            var queue = new List<Overlay>();
            if (next.LastShow is { Entries.Count: > 0 } show && shownShows.Add($"{now.Id}|{show.Competition}|{show.Year}")) queue.Add(Overlay.Show);
            if (now.Review is { } review && shownReviews.Add($"{now.Id}|{review.Year}")) queue.Add(Overlay.Review);
            var earlier = previous.Players.FirstOrDefault(p => p.Id == now.Id)?.Decision;
            if (now.Decision is { } decision && (earlier is null || earlier.EventId != decision.EventId || earlier.Title != decision.Title))
            {
                queue.Add(Overlay.Decision);
            }
            if (RulesV2.On(next) && endedTurn && (now.Ambitions.Count < RulesV2.AmbitionCount || (now.Goal is null && now.GoalOffers.Count > 0)))
            {
                queue.Add(Overlay.Plan);
            }
            Nav.Enqueue(queue);
        }
        if (!endedTurn) Changed?.Invoke();
    }

    void MarkSeen(GameState state, string playerId)
    {
        var from = TimeOf(state.Year, state.Month);
        seen[playerId] = (from, state.Log.Where(e => TimeOf(e.Year, e.Month) == from).Select(LogKey).ToHashSet());
    }

    /// <summary>Neuigkeiten für einen Spieler seit seinem letzten Zug</summary>
    List<LogEntry> NewsSince(GameState state, string playerId)
    {
        var mark = seen.TryGetValue(playerId, out var m) ? m : (From: TimeOf(state.Year, state.Month) - 1, Keys: new HashSet<string>());
        return state.Log
            .Where(e => (e.PlayerId is null || e.PlayerId == playerId) && IsNews(e) && TimeOf(e.Year, e.Month) >= mark.From && !mark.Keys.Contains(LogKey(e)))
            .ToList();
    }

    void ShowSummary(GameState state, string previousId, int months, List<Hint> reasons, bool skipped)
    {
        var now = Rules.CurrentPlayer(state);
        Summary = new Summary
        {
            PlayerName = now.Name,
            Changed = HotSeat && now.Id != previousId,
            Months = months,
            Reasons = reasons,
            Entries = NewsSince(state, now.Id),
            Over = IsOver(state),
            Skipped = skipped,
        };
    }

    /// <summary>Bestes eigenes Ergebnis der Tages-Challenge an diesem Datum</summary>
    public double? ChallengeBest(string date) =>
        double.TryParse(store.GetSetting("challenge." + date), System.Globalization.CultureInfo.InvariantCulture, out var best) ? best : null;

    bool RecordChallengeResult(string date, double points)
    {
        var best = ChallengeBest(date);
        if (best is not null && best >= points) return false;
        store.SetSetting("challenge." + date, Util.JsNumber(points));
        return true;
    }
}

/// <summary>Welcher Klang zu einer Zustandsänderung passt – angelehnt an soundForChange im Original.</summary>
public static class Sounds
{
    public static Sound? ForChange(GameState before, GameState after)
    {
        var fresh = after.Log.Skip(Math.Max(0, after.Log.Count - Math.Max(0, NewEntries(before, after)))).ToList();
        if (fresh.Any(e => e.Text.Contains("Weinskandal") || e.Text.Contains("wine scandal", StringComparison.OrdinalIgnoreCase))) return Sound.Scandal;
        if (fresh.Any(e => e.Text.Contains("Medaille") || e.Text.Contains("medal", StringComparison.OrdinalIgnoreCase) || e.Text.Contains("🥇") || e.Text.Contains("🥈") || e.Text.Contains("🥉"))) return Sound.Medal;
        if (fresh.Any(e => e.Text.Contains("Frost") || e.Text.Contains("frost"))) return Sound.Frost;
        if (after.Month != before.Month || after.Year != before.Year) return Sound.Month;
        var a = Rules.CurrentPlayer(after);
        var b = before.Players.FirstOrDefault(p => p.Id == a.Id);
        if (b is null) return null;
        if (a.Plots.Count(p => p.HarvestedThisYear) > b.Plots.Count(p => p.HarvestedThisYear)) return Sound.Harvest;
        if (a.Money > b.Money) return Sound.Sale;
        return null;
    }

    static int NewEntries(GameState before, GameState after)
    {
        if (before.Log.Count == 0) return after.Log.Count;
        var last = before.Log[^1];
        var index = after.Log.LastIndexOf(last);
        return index < 0 ? after.Log.Count : after.Log.Count - 1 - index;
    }
}
