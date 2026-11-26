using Winzer.Engine;

namespace Winzer.Presentation;

public enum Scene { Gut, Weinberg, Keller, Markt, Kontor, Dorf }

/// <summary>Plan: Ziele, Ausbau und Preise · Review: Jahresrückblick · Show: Prämierung (alle Regelversion 2)</summary>
public enum Overlay { None, Decision, Lexicon, Menu, Plan, Review, Show }

public sealed record SceneInfo(Scene Scene, string LabelDe, string LabelEn, string Key)
{
    public string Label => Lang.T(LabelDe, LabelEn);
}

/// <summary>
/// Zustand der Oberfläche, der einen Neuaufbau überdauert: Szene, Reiter, Auswahl, aufgeschlagene Seiten.
/// </summary>
public sealed class Navigator
{
    public static readonly SceneInfo[] Scenes =
    [
        new(Scene.Gut, "Gut", "Estate", "1"),
        new(Scene.Weinberg, "Weinberg", "Vineyard", "2"),
        new(Scene.Keller, "Keller", "Cellar", "3"),
        new(Scene.Markt, "Markt", "Market", "4"),
        new(Scene.Kontor, "Kontor", "Office", "5"),
        new(Scene.Dorf, "Dorf", "Village", "6"),
    ];

    readonly Dictionary<Scene, int> tabs = [];
    readonly Dictionary<string, string?> selections = [];
    readonly Dictionary<string, int> pages = [];
    readonly Dictionary<string, double> amounts = [];

    public Scene Scene { get; private set; } = Scene.Gut;
    public Overlay Overlay { get; set; }
    public event Action? Changed;

    public static Scene SceneFor(HintArea area) => area switch
    {
        HintArea.Weinberg => Scene.Weinberg,
        HintArea.Keller => Scene.Keller,
        HintArea.Auftraege => Scene.Markt,
        HintArea.Betrieb => Scene.Kontor,
        _ => Scene.Gut,
    };

    public static string LabelFor(Scene scene) => Scenes.First(s => s.Scene == scene).Label;

    public void Go(Scene scene, int? tab = null)
    {
        Scene = scene;
        if (tab is { } t) tabs[scene] = t;
        Overlay = Overlay.None;
        Changed?.Invoke();
    }

    public int Tab => tabs.GetValueOrDefault(Scene);

    public void SetTab(int tab)
    {
        tabs[Scene] = tab;
        Changed?.Invoke();
    }

    /// <summary>Gewähltes Ding (Parzelle, Charge, Posten …) in einem Bereich, z. B. "keller.batch".</summary>
    public string? Selected(string area) => selections.GetValueOrDefault(area);

    public void Select(string area, string? id)
    {
        selections[area] = id;
        Changed?.Invoke();
    }

    /// <summary>Aufgeschlagene Seite einer geblätterten Liste (ab 0), auf den gültigen Bereich begrenzt.</summary>
    public int Page(string list, int pageCount) => Math.Clamp(pages.GetValueOrDefault(list), 0, Math.Max(0, pageCount - 1));

    public void SetPage(string list, int page)
    {
        pages[list] = Math.Max(0, page);
        Changed?.Invoke();
    }

    /// <summary>Am Schieber eingestellter Anteil 0–1 (Standard: alles).</summary>
    public double Amount(string key, double fallback = 1) => amounts.GetValueOrDefault(key, fallback);

    public void SetAmount(string key, double share)
    {
        amounts[key] = Math.Clamp(share, 0, 1);
        Changed?.Invoke();
    }

    readonly Queue<Overlay> pending = new();

    /// <summary>Zeigt eine Überlagerung. Beim Schließen (<see cref="Overlay.None"/>) folgt die nächste wartende.</summary>
    public void Show(Overlay overlay)
    {
        Overlay = overlay == Overlay.None && pending.Count > 0 ? pending.Dequeue() : overlay;
        Changed?.Invoke();
    }

    /// <summary>Reiht Überlagerungen ein, die nacheinander erscheinen sollen; die erste sofort, wenn gerade nichts offen ist.</summary>
    public void Enqueue(IEnumerable<Overlay> overlays)
    {
        foreach (var overlay in overlays)
        {
            if (overlay != Overlay && !pending.Contains(overlay)) pending.Enqueue(overlay);
        }
        if (Overlay == Overlay.None && pending.Count > 0) Overlay = pending.Dequeue();
    }

    /// <summary>Reiter innerhalb einer Überlagerung, z. B. der Gutsplanung</summary>
    public int OverlayTab { get; private set; }

    public void SetOverlayTab(int tab)
    {
        OverlayTab = tab;
        Changed?.Invoke();
    }

    public void Notify() => Changed?.Invoke();
}
