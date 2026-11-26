using DrawnUi.Draw;
using DrawnUi.Views;
using Winzer.Engine;
using Winzer.Presentation;
using Winzer.Ui.Scenes;

namespace Winzer.Ui;

/// <summary>
/// Die Anwendung oberhalb der Köpfe: Hauptmenü oder laufende Partie. Der Kopf (WPF, Blazor) fragt
/// <see cref="Build"/> nach dem Baum für die aktuelle Fenstergröße und baut bei <see cref="Changed"/> neu auf.
/// </summary>
public sealed class AppHost(ISaveStore store, IAudio audio)
{
    // Gezeichnete Pfade entstehen aus Zahlen im Text („M0 12.5 L…“). Unter deutscher Kultur würde daraus „12,5“ und der
    // Pfad wäre ungültig. Sichtbare Zahlen formatiert Util selbst, deshalb läuft die Oberfläche kulturneutral.
    readonly bool neutral = UseNeutralCulture();

    static bool UseNeutralCulture()
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        return true;
    }

    readonly bool language = UseStoredLanguage(store);

    // Entwicklerhilfe: WINZER_LANG=en gilt, solange keine Sprache gespeichert ist (z. B. für --shot und --selbstspiel)
    static bool UseStoredLanguage(ISaveStore store) =>
        Lang.En = (store.GetSetting("language") ?? Environment.GetEnvironmentVariable("WINZER_LANG")) == "en";

    /// <summary>Wechselt die Sprache aller Texte und merkt sie sich für den nächsten Start.</summary>
    public void SetLanguage(bool english)
    {
        Lang.En = english;
        Store.SetSetting("language", english ? "en" : "de");
        Changed?.Invoke();
    }

    public ISaveStore Store { get; } = store;
    public IAudio Audio { get; } = audio;
    /// <summary>Lizenzhinweise der mitgelieferten Fremdsoftware für die Lizenzseite</summary>
    public INotices Notices { get; init; } = new NoNotices();
    public GameSession? Session { get; private set; }
    /// <summary>Zustand des Hauptmenüs (Schritt, Auswahl), der einen Neuaufbau überdauert</summary>
    public MenuState Menu { get; } = new();
    public event Action? Changed;
    /// <summary>Vollbild oder Fenster hat gewechselt; der Kopf richtet sein Fenster neu ein</summary>
    public event Action? WindowModeChanged;

    /// <summary>Vollbild (Standard) oder normales Fenster; gilt auch für den nächsten Start</summary>
    public bool FullScreen => Store.GetSetting("window") != "window";

    public void ToggleFullScreen()
    {
        Store.SetSetting("window", FullScreen ? "window" : "full");
        WindowModeChanged?.Invoke();
        Changed?.Invoke();
    }

    /// <summary>Der Kopf soll die Anwendung beenden</summary>
    public event Action? QuitRequested;

    /// <summary>
    /// Was gerade zu sehen ist: Hauptmenü oder eine Szene der Partie. Wechselt der Wert, blendet <see cref="Build"/>
    /// vom alten Bild zum neuen über.
    /// </summary>
    public string View => Session is { } session ? $"szene:{session.Nav.Scene}" : "menü";

    /// <summary>Dauer des Überblendens beim Szenenwechsel in Sekunden; 0 schneidet hart.</summary>
    public double FadeSeconds { get; set; } = 0.32;

    // Der zuletzt gebaute Baum – beim Szenenwechsel bleibt sein Inhalt kurz über dem neuen stehen und verblasst
    SkiaLayout? shownRoot;
    SkiaControl? shownFrame;
    string? shownView;
    double shownScale;

    public void Notify() => Changed?.Invoke();

    public void Quit() => QuitRequested?.Invoke();

    public void ShowMenu()
    {
        Session = null;
        Anim.Reset();
        Menu.Reset();
        Changed?.Invoke();
    }

    public void Start(GameState state, string? saveId = null)
    {
        var id = saveId ?? NewSaveId(DateTime.Now);
        if (saveId is null) Store.Write(id, state);
        Anim.Reset();
        Session = new GameSession(state, id, Store, Audio);
        Session.Changed += () => Changed?.Invoke();
        Changed?.Invoke();
    }

    /// <summary>Neue Speicherplatz-ID, zugleich lesbarer Dateiname, z. B. 2026-10-16_14-03-22-512</summary>
    public static string NewSaveId(DateTime date) => date.ToString("yyyy-MM-dd_HH-mm-ss-fff");

    /// <summary>Baut die Oberfläche für ein Fenster dieser Größe (in Bildschirmpunkten).</summary>
    public SkiaControl Build(double width, double height)
    {
        Kit.S = Math.Max(0.2, Math.Min(width / Kit.DesignWidth, height / Kit.DesignHeight));
        Kit.ResetHover();
        Anim.BeginBuild();
        // Szenenwechsel: Die alte Szene aus ihrem Baum lösen, bevor der Kopf ihn wegwirft
        var view = View;
        SkiaControl? leaving = null;
        if (FadeSeconds > 0 && Anim.Enabled && shownRoot is not null && shownFrame is not null && view != shownView && shownScale == Kit.S)
        {
            shownRoot.Children.Remove(shownFrame);
            leaving = shownFrame;
        }
        var root = new SkiaLayout
        {
            Type = LayoutType.Absolute,
            BackgroundColor = Theme.C("#120c0e"),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        // Entwurfsfläche mittig; bei abweichendem Seitenverhältnis bleibt ein dunkler Rand
        var offsetX = (width / Kit.S - Kit.DesignWidth) / 2;
        var offsetY = (height / Kit.S - Kit.DesignHeight) / 2;
        var frame = Kit.Layer(offsetX, offsetY, Kit.DesignWidth, Kit.DesignHeight);
        frame.IsClippedToBounds = true;
        frame.With(Session is { } session ? Shell.Build(session, this) : MainMenu.Build(this));
        root.With(frame, Kit.TipLayer(width, height));
        if (leaving is not null)
        {
            // Die alte Szene liegt über der neuen und wird durchsichtig. Die neue ist darunter schon bedienbar.
            leaving.InputTransparent = true;
            leaving.ZIndex = 5000;
            root.With(leaving);
            var start = Anim.Now;
            Anim.Drive(() =>
            {
                var p = (Anim.Now - start) / FadeSeconds;
                if (p >= 1)
                {
                    leaving.IsVisible = false;
                    return false;
                }
                leaving.Opacity = 1 - p * p * (3 - 2 * p);
                return true;
            });
        }
        (shownRoot, shownFrame, shownView, shownScale) = (root, frame, view, Kit.S);
        return root;
    }

    /// <summary>Tastenkürzel; liefert true, wenn die Taste verarbeitet wurde.</summary>
    public bool HandleKey(string key)
    {
        if (Session is not { } session) return false;
        var nav = session.Nav;
        if (key == "Escape")
        {
            if (nav.Overlay != Overlay.None) nav.Show(Overlay.None);
            else if (session.Error.Length > 0) session.ClearError();
            else nav.Show(Overlay.Menu);
            return true;
        }
        if (session.Summary is not null)
        {
            if (key is "Space" or "Enter") session.CloseSummary();
            return true;
        }
        if (session.LiveLot is not null || nav.Overlay != Overlay.None) return false;
        foreach (var info in Navigator.Scenes)
        {
            if (info.Key == key)
            {
                nav.Go(info.Scene);
                return true;
            }
        }
        switch (key)
        {
            case "Space" when !session.Over:
                session.Run(new GameAction.EndTurn());
                return true;
            case "F" when !session.Over:
                session.Run(new GameAction.AutoSkipTurn());
                return true;
            case "L":
                nav.Show(Overlay.Lexicon);
                return true;
            case "Tab":
                nav.SetTab(nav.Tab + 1);
                return true;
        }
        return false;
    }
}

/// <summary>Zustand des Hauptmenüs</summary>
public sealed class MenuState
{
    /// <summary>Frei belegbar durch das Hauptmenü: Schritt, gewählte Werte</summary>
    public Dictionary<string, object> Values { get; } = [];
    public int Step { get; set; }

    public T Get<T>(string key, T fallback) => Values.TryGetValue(key, out var value) && value is T typed ? typed : fallback;

    public void Set(string key, object value) => Values[key] = value;

    public void Reset()
    {
        Step = 0;
    }
}
