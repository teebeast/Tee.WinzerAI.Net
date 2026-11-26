using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

/// <summary>
/// Hauptmenü: Startseite mit Titelbild, neues Spiel in drei Schritten (<c>MenuNewGame</c>), Spielstände,
/// Tages-Challenge und Lizenzhinweis (<c>MenuPages</c>). Die Seite steht in <c>app.Menu.Step</c>, alle Einstellungen
/// in einem <see cref="MenuSetup"/> unter <c>app.Menu</c>.
/// <para>
/// Entwicklerhilfe für Bildschirmfotos: Die Umgebungsvariable <c>WINZER_MENU_STEP</c> (0–6) wählt die Seite beim
/// ersten Aufbau, <c>WINZER_MENU_SET</c> setzt Werte, z. B. <c>length=0;seats=4;region=Pfalz;human=0;demo=8;page=1;confirm=1</c>
/// (<c>demo</c> legt nur in der Ablage im Arbeitsspeicher Beispiel-Spielstände an).
/// </para>
/// </summary>
public static class MainMenu
{
    internal const int Home = 0, StepMode = 1, StepRegion = 2, StepPlayers = 3, Saves = 4, Daily = 5, Licenses = 6, Settings = 7;

    public static SkiaControl Build(AppHost app)
    {
        var setup = MenuSetup.Of(app);
        var step = app.Menu.Step;
        var layer = Kit.Layer(0, 0, Kit.DesignWidth, Kit.DesignHeight);
        var home = step is < StepMode or > Settings;
        layer.With(Kit.Paint(0, 0, Kit.DesignWidth, Kit.DesignHeight, (c, w, h) => MenuArt.Landscape(c, w, h, home)));
        if (!home) layer.With(Kit.Box(0, 0, Kit.DesignWidth, Kit.DesignHeight, Theme.C("#b8140a0e")));
        switch (step)
        {
            case StepMode: MenuNewGame.ModePage(layer, app, setup); break;
            case StepRegion: MenuNewGame.RegionPage(layer, app, setup); break;
            case StepPlayers: MenuNewGame.PlayersPage(layer, app, setup); break;
            case Saves: MenuPages.SavesPage(layer, app, setup); break;
            case Daily: MenuPages.DailyPage(layer, app, setup); break;
            case Licenses: MenuPages.LicensesPage(layer, app); break;
            case Settings: MenuPages.SettingsPage(layer, app); break;
            default: HomePage(layer, app, setup); break;
        }
        return layer;
    }

    internal static void Go(AppHost app, int step)
    {
        var setup = MenuSetup.Of(app);
        setup.ConfirmDelete = null;
        app.Menu.Step = step;
        app.Notify();
    }

    /// <summary>Beginnt oder lädt eine Partie; die Liste der Spielstände wird danach neu gelesen.</summary>
    internal static void Start(AppHost app, MenuSetup setup, GameState state, string? saveId = null)
    {
        setup.Saves = null;
        setup.ConfirmDelete = null;
        setup.Seed = Random.Shared.Next(1_000_000);
        app.Start(state, saveId);
    }

    static void HomePage(SkiaLayout layer, AppHost app, MenuSetup setup)
    {
        var saves = setup.SavesOf(app);
        var today = Challenge.TodayKey();
        var daily = Challenge.DailyChallenge(today);
        var best = app.Store.GetSetting("challenge." + today);

        layer.With(Kit.Text(Lang.T("Übernimm ein Weingut mit 10 ha, keltere die besten Weine und miss dich mit klugen Computer-Winzern.", "Take over a 10 ha estate, press the finest wines and measure yourself against clever computer winemakers."),
            116, 322, 700, 76, 25, Theme.Cream, maxLines: 2));

        var panel = Kit.Glass(104, 430, 560, 524, 24);
        const double x = 28, w = 504, h = 84;
        var y = 24.0;
        if (saves.Count > 0)
        {
            var slot = saves[0];
            panel.With(Kit.Button(Lang.T("Weiterspielen", "Continue"), x, y, w, h, () => Start(app, setup, slot.State, slot.Id), ButtonKind.Primary,
                $"{MenuPages.SlotTitle(slot)} · {MenuPages.SlotWhen(slot.State)}", fontSize: 26));
        }
        else
        {
            panel.With(Kit.Button(Lang.T("Weiterspielen", "Continue"), x, y, w, h, () => { }, ButtonKind.Dark, problem: Lang.T("Noch kein Spielstand vorhanden", "No saved game yet"), fontSize: 26));
        }
        y += 98;
        panel.With(Kit.Button(Lang.T("Neues Spiel", "New Game"), x, y, w, h, () => Go(app, StepMode), saves.Count > 0 ? ButtonKind.Dark : ButtonKind.Primary,
            Lang.T("Freies Spiel: Gebiet, Gegner und Spiellänge frei wählbar", "Free game: choose region, rivals and game length"), fontSize: 26));
        y += 98;
        panel.With(Kit.Button(Lang.T("Tages-Challenge", "Daily Challenge"), x, y, w, h, () => Go(app, Daily), ButtonKind.Dark,
            Lang.En ? $"{daily.Scenario.Name} · {(best is null ? "not played yet" : $"Best today: {best} points")}" : $"{daily.Scenario.Name} · {(best is null ? "noch nicht gespielt" : $"Bestwert heute: {best} Punkte")}", fontSize: 26));
        y += 98;
        panel.With(Kit.Button(Lang.T("Spielstände", "Saved games"), x, y, w, h, () => Go(app, Saves), ButtonKind.Dark,
            saves.Count == 0 ? Lang.T("Laden und löschen", "Load and delete") : saves.Count == 1 ? Lang.T("1 Spielstand · laden und löschen", "1 saved game · load and delete") : Lang.En ? $"{saves.Count} saved games · load and delete" : $"{saves.Count} Spielstände · laden und löschen", fontSize: 26));
        y += 98;
        panel.With(Kit.Button(Lang.T("Beenden", "Quit"), x, y, w, h, app.Quit, ButtonKind.Dark, Lang.T("Jede Partie wird nach jedem Zug automatisch gespeichert", "Every game is saved automatically after every turn"), fontSize: 26));
        layer.With(panel);

        layer.With(Kit.Text(Lang.T("Alle Weingüter, Personen, Siegel und Prämierungen sind frei erfunden.", "All estates, people, seals and wine awards are fictional."), 116, 992, 640, 30, 19, Theme.CreamSoft));
        layer.With(Kit.Box(1450, 980, 198, 48, Theme.C("#99140a0e"), 24, Theme.GlassLine, 1.5)
            .With(Kit.Centered("⚙️ " + Lang.T("Einstellungen", "Settings"), 0, 198, 48, 19, Theme.Cream))
            .Tap(() => Go(app, Settings)));
        layer.With(Kit.Box(1660, 980, 150, 48, Theme.C("#99140a0e"), 24, Theme.GlassLine, 1.5)
            .With(Kit.Centered(Lang.T("Lizenzen", "Licenses"), 0, 150, 48, 19, Theme.Cream))
            .Tap(() => Go(app, Licenses)));
    }
}

/// <summary>Ein Platz in der Aufstellung des freien Spiels</summary>
internal sealed class MenuSeat
{
    public string Name = "";
    public RegionId RegionId = RegionId.Mosel;
    /// <summary>null = Mensch</summary>
    public AiProfileId? Ai;
}

/// <summary>Alles, was im Hauptmenü eingestellt ist; liegt unter dem Schlüssel „setup“ in <c>app.Menu</c>.</summary>
internal sealed class MenuSetup
{
    /// <summary>Vorgeschlagene, frei erfundene Gutsnamen – Texteingabe gibt es nicht.</summary>
    public static readonly string[] Names =
    [
        "Weingut Sonnenhang", "Weingut Silberlay", "Gut Lerchenhöhe", "Weingut Goldkuppe", "Rebhof Abendrot", "Weingut Kranichgrund",
        "Hofgut Schieferturm", "Weingut Mondterrasse", "Gut Wildrosenberg", "Weingut Nebeltal", "Winzerhof Dreilinden", "Weingut Kupferlay",
    ];

    /// <summary>Stufen fürs Endlosspiel; null = ohne Zeitlimit</summary>
    public static readonly int?[] EndlessYears = [5, 10, 25, 50, null];

    public List<MenuSeat> Seats =
    [
        new() { Name = Names[0] },
        new() { Name = Ai.AiEstateNames[AiProfileId.Tradition], Ai = AiProfileId.Tradition },
    ];
    /// <summary>Punkteziel 300, 600 oder 1000; 0 = Endlosspiel</summary>
    public int Length = 600;
    public int EndlessStep = EndlessYears.Length - 1;
    public Difficulty Difficulty = Difficulty.Normal;
    public bool ClimateChange = true;
    public bool RandomEvents = true;
    /// <summary>Erweiterte Regeln (Regelversion 2) statt der Regeln des Originals</summary>
    public bool ExtendedRules = true;
    public int Seed = Random.Shared.Next(1_000_000);
    public int SavesPage;
    /// <summary>Spielstand, dessen Löschen gerade bestätigt werden muss</summary>
    public string? ConfirmDelete;
    /// <summary>Gelesene Spielstände; null = neu lesen</summary>
    public IReadOnlyList<SaveSlot>? Saves;

    public bool Endless => Length == 0;
    public bool HasHuman => Seats.Any(s => s.Ai is null);

    public IReadOnlyList<SaveSlot> SavesOf(AppHost app) => Saves ??= app.Store.List();

    public static MenuSetup Of(AppHost app)
    {
        if (app.Menu.Values.TryGetValue("setup", out var value) && value is MenuSetup known) return known;
        var setup = new MenuSetup();
        app.Menu.Set("setup", setup);
        setup.ApplyDeveloperSettings(app);
        return setup;
    }

    /// <summary>Wie submit() in NewGame.svelte</summary>
    public NewGameOptions Options() => new()
    {
        Seed = Seed,
        TargetPoints = Endless ? Rules.NoPointTarget : Length,
        YearLimit = Endless ? EndlessYears[EndlessStep] : null,
        ClimateChange = ClimateChange,
        RandomEvents = RandomEvents,
        RulesVersion = ExtendedRules ? 2 : 1,
        Difficulty = Difficulty,
        Players = Seats.Select((s, i) => new NewPlayer
        {
            Name = s.Name.Trim().Length > 0 ? s.Name.Trim() : Lang.En ? $"Player {i + 1}" : $"Spieler {i + 1}",
            RegionId = s.RegionId,
            Ai = s.Ai,
        }).ToList(),
    };

    /// <summary>Das eigene Gebiet wechseln: Wer im selben Gebiet saß wie das erste Weingut, zieht mit um.</summary>
    public void SetHomeRegion(RegionId id)
    {
        var old = Seats[0].RegionId;
        foreach (var seat in Seats)
        {
            if (seat.RegionId == old) seat.RegionId = id;
        }
    }

    public void AddSeat()
    {
        if (Seats.Count >= 4) return;
        var kind = Ai.AiProfiles.Keys.Cast<AiProfileId?>().FirstOrDefault(p => Seats.All(s => s.Ai != p)) ?? AiProfileId.Tradition;
        Seats.Add(new MenuSeat { Name = Ai.AiEstateNames[kind], RegionId = Seats[0].RegionId, Ai = kind });
    }

    /// <summary>Erster vorgeschlagener Name, den noch kein Weingut trägt</summary>
    public string FreeName() => Names.FirstOrDefault(n => Seats.All(s => s.Name != n)) ?? Names[0];

    /// <summary>Mensch ↔ KI-Profil; der Name folgt der Art des Weinguts.</summary>
    public void SetKind(MenuSeat seat, AiProfileId? kind)
    {
        if (seat.Ai == kind) return;
        var wasHuman = seat.Ai is null;
        var wasDefault = wasHuman || seat.Name == Ai.AiEstateNames[seat.Ai!.Value];
        seat.Ai = kind;
        if (kind is { } profile)
        {
            if (wasDefault) seat.Name = Ai.AiEstateNames[profile];
        }
        else
        {
            seat.Name = "";
            seat.Name = FreeName();
        }
    }

    void ApplyDeveloperSettings(AppHost app)
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("WINZER_MENU_STEP"), out var step)) app.Menu.Step = step;
        foreach (var pair in (Environment.GetEnvironmentVariable("WINZER_MENU_SET") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=');
            if (parts.Length != 2) continue;
            var number = int.TryParse(parts[1], out var n) ? n : 0;
            switch (parts[0].Trim())
            {
                case "length": Length = number; break;
                case "endless": EndlessStep = Math.Clamp(number, 0, EndlessYears.Length - 1); break;
                case "seats":
                    while (Seats.Count < Math.Min(4, number)) AddSeat();
                    break;
                case "human":
                    if (number == 0) SetKind(Seats[0], AiProfileId.Bio);
                    break;
                case "region":
                    if (Enum.TryParse<RegionId>(parts[1], true, out var region)) SetHomeRegion(region);
                    break;
                case "page": SavesPage = number; break;
                case "demo" when app.Store is MemorySaveStore:
                    for (var i = 0; i < number; i++)
                    {
                        var options = i % 3 == 1 ? Challenge.DailyChallenge(Challenge.TodayKey()).Options : Options();
                        if (i % 3 == 2) options.TargetPoints = Rules.NoPointTarget;
                        options.Players[0].Name = Names[i % Names.Length];
                        if (i % 4 == 3) options.Players.Add(new NewPlayer { Name = Names[(i + 5) % Names.Length], RegionId = RegionId.Pfalz });
                        app.Store.Write($"demo-{i:00}", Game.NewGame(options));
                    }
                    break;
                case "confirm":
                    ConfirmDelete = SavesOf(app).Skip(number).FirstOrDefault()?.Id;
                    break;
            }
        }
    }
}

/// <summary>Bausteine, die nur das Hauptmenü braucht</summary>
internal static class MenuUi
{
    public const double Left = 110, Right = 1810;

    /// <summary>Seitentitel mit Unterzeile auf dunklem Grund</summary>
    public static void Header(SkiaLayout layer, string title, string sub, double width = 800)
    {
        layer.With(Kit.Text(title, Left, 44, width, 72, 54, Theme.Cream, bold: true, serif: true));
        layer.With(Kit.Text(sub, Left, 118, width, 32, 22, Theme.CreamSoft));
    }

    /// <summary>Schrittanzeige des neuen Spiels: erledigte und aktueller Schritt in Gold, antippbar.</summary>
    public static SkiaControl StepBar(AppHost app, int step)
    {
        string[] labels = [Lang.T("Spielart und Ziel", "Mode and goal"), Lang.T("Gebiet wählen", "Choose region"), Lang.T("Mitspieler und Optionen", "Players and options")];
        double[] starts = [0, 304, 563];
        double[] widths = [190, 145, 255];
        var bar = Kit.Layer(Right - 880, 62, 880, 60);
        for (var i = 0; i < labels.Length; i++)
        {
            var number = i + 1;
            var on = number == step;
            var done = number < step;
            var item = Kit.Layer(starts[i], 0, 60 + widths[i], 60);
            item.With(on || done
                ? Kit.Box(0, 6, 48, 48, radius: 24, gradient: Kit.Vertical(Theme.Gold, Theme.GoldDark))
                : Kit.Box(0, 6, 48, 48, Theme.GlassLight, 24, Theme.GlassLine, 1.5));
            item.With(Kit.Text(number.ToString(), 0, 14, 48, 32, 23, on || done ? Theme.Ink : Theme.CreamSoft, bold: true, serif: true, align: Align.Center));
            item.With(Kit.Text(labels[i], 60, 16, widths[i], 30, 20, on ? Theme.Gold : done ? Theme.Cream : Theme.CreamSoft, bold: on));
            item.Tap(() => MainMenu.Go(app, number));
            bar.With(item);
            if (i > 0) bar.With(Kit.Box(starts[i] - 42, 29, 30, 3, i < step ? Theme.Gold : Theme.GlassLine, 1.5));
        }
        return bar;
    }

    /// <summary>Fußleiste: links zurück, rechts weiter</summary>
    public static void Footer(SkiaLayout layer, string backTitle, Action back, string? nextTitle = null, Action? next = null,
        string? nextSub = null, string? nextProblem = null, double nextWidth = 420)
    {
        layer.With(Kit.Button("‹  " + backTitle, Left, 962, 300, 76, back, ButtonKind.Dark, fontSize: 24, align: Align.Center));
        if (nextTitle is not null && next is not null)
        {
            layer.With(Kit.Button(nextTitle, Right - nextWidth, 962, nextWidth, 76, next, ButtonKind.Gold, nextSub, nextProblem, 25, Align.Center));
        }
    }

    /// <summary>Wählbare Kachel auf Pergament</summary>
    public static SkiaShape Option(double x, double y, double w, double h, bool on, Action tap)
    {
        var box = Kit.Box(x, y, w, h, on ? Theme.C("#248e2440") : Theme.C("#66ffffff"), 16, on ? Theme.Wine : Theme.PaperLine, on ? 4 : 2);
        box.Tap(tap);
        return box;
    }

    /// <summary>Auswahl durch Blättern: ‹ Wert ›</summary>
    public static SkiaControl Cycler(double x, double y, double w, string text, Action previous, Action next, bool bold = false)
    {
        var box = Kit.Box(x, y, w, 52, Theme.C("#80ffffff"), 12, Theme.PaperLine, 2);
        box.With(Kit.IconButton("‹", 5, 5, 42, previous, dark: false, tip: Lang.T("Vorherige Auswahl", "Previous choice")));
        box.With(Kit.Text(text, 50, 13, w - 100, 28, 18, Theme.Ink, bold: bold, align: Align.Center));
        box.With(Kit.IconButton("›", w - 47, 5, 42, next, dark: false, tip: Lang.T("Nächste Auswahl", "Next choice")));
        return box;
    }

    /// <summary>Schalter an/aus</summary>
    public static SkiaControl Toggle(double x, double y, bool on, Action tap)
    {
        var box = Kit.Box(x, y, 84, 44, on ? Theme.Wine : Theme.C("#b8a888"), 22);
        box.With(Kit.Box(on ? 44 : 4, 4, 36, 36, Theme.Paper, 18));
        box.Tap(tap);
        return box;
    }

    /// <summary>Balken 0–1 mit Füllung</summary>
    public static SkiaControl Meter(double x, double y, double w, double h, double value, Color color)
    {
        var track = Kit.Box(x, y, w, h, Theme.C("#40a08a5c"), h / 2);
        track.With(Kit.Box(0, 0, Math.Max(h, w * Math.Clamp(value, 0, 1)), h, color, h / 2));
        return track;
    }

    public static double ChipWidth(string text, double size) => 34 + text.Length * size * 0.54;

    /// <summary>Kleines Schild; liefert die Breite über <paramref name="width"/> fürs Aneinanderreihen.</summary>
    public static SkiaControl Chip(string text, double x, double y, out double width, bool dark = false, double size = 20, Color? fill = null, Color? color = null)
    {
        width = ChipWidth(text, size);
        var h = size * 2.1;
        return Kit.Box(x, y, width, h, fill ?? (dark ? Theme.GlassLight : Theme.C("#59d9c7a3")), h / 2)
            .With(Kit.Centered(text, 0, width, h, size, color ?? (dark ? Theme.Cream : Theme.Ink)));
    }
}

/// <summary>Gemalte Weinlandschaft für Titel und Hintergrund des Menüs</summary>
internal static class MenuArt
{
    static readonly SKTypeface SerifBold = SKTypeface.FromFamilyName(Theme.Serif, SKFontStyle.Bold);

    const string FarHill = "M0 520 C260 420 520 470 760 440 C1040 400 1300 470 1560 430 C1720 410 1840 440 1920 430 L1920 1080 L0 1080 Z";
    const string MidHill = "M0 640 C300 540 640 600 900 560 C1200 510 1500 600 1920 540 L1920 1080 L0 1080 Z";
    const string River = "M0 700 C300 660 520 720 820 690 C1100 660 1400 720 1920 670 L1920 730 C1400 780 1100 720 820 750 C520 780 300 720 0 760 Z";
    const string NearHill = "M0 760 C360 700 760 780 1120 730 C1460 690 1700 740 1920 720 L1920 1080 L0 1080 Z";

    static readonly (string Path, string Dark, string Light, float Angle)[] Parcels =
    [
        ("M290 612 L560 600 L620 676 L320 680 Z", "#6b5a26", "#c9a23a", -18),
        ("M720 598 L960 580 L1030 668 L770 678 Z", "#3f5a22", "#7d9a3a", -12),
        ("M1170 574 L1410 580 L1456 672 L1186 670 Z", "#6a3320", "#b5562e", -24),
        ("M1546 600 L1750 588 L1796 674 L1566 680 Z", "#4a6226", "#8aa644", -8),
        ("M96 640 L214 622 L258 684 L124 686 Z", "#6a3320", "#b5562e", -30),
    ];

    public static void Landscape(SKCanvas c, float w, float h, bool title)
    {
        var t = Anim.Use();
        using (var sky = Draw.VerticalGradient(0, h, (0, "#1f2b45"), (0.2f, "#4a4366"), (0.42f, "#b8656a"), (0.62f, "#f3b871"), (1, "#f3b871")))
            c.DrawRect(0, 0, w, h, sky);

        // Erste Sterne am Abendhimmel, sie funkeln
        WeatherFx.Stars(c, w, 230, t, 46);

        // Sonne knapp über den Hügeln
        WeatherFx.Sun(c, 1380, 430, 340, 60, t, rays: false);

        WeatherFx.Clouds(c, w, t, 5, 7, SKColor.Parse("#ffe2c2").WithAlpha(70), 160, 260);

        // Ferner Höhenzug im Dunst, davor die Hügel mit Burg und Dorf
        using (var haze = Draw.VerticalGradient(380, 560, (0, "#a8788a"), (1, "#8a6a80")))
            Draw.Path(c, "M0 462 C180 396 380 430 620 398 C860 366 1080 420 1320 392 C1560 366 1760 400 1920 380 L1920 1080 L0 1080 Z", haze);
        using (var far = Draw.VerticalGradient(420, 700, (0, "#7c6a78"), (1, "#5d5468")))
            Draw.Path(c, FarHill, far);
        using (var ruin = Draw.Fill("#55485c"))
        {
            Draw.Path(c, "M286 458 V398 H294 V406 H302 V398 H310 V406 H318 V398 H326 V458 Z", ruin);
            Draw.Path(c, "M326 458 V428 H352 V420 H360 V428 H368 V420 H376 V458 Z", ruin);
        }
        using (var house = Draw.Fill("#e2cfae"))
        using (var roof = Draw.Fill("#7a4a3a"))
        using (var lit = Draw.Fill("#ffd98a"))
        {
            (float X, float W, float H)[] houses = [(952, 26, 22), (982, 22, 28), (1034, 30, 22), (1068, 24, 26), (1096, 28, 20)];
            foreach (var (x, width, height) in houses)
            {
                var ground = 446f;
                c.DrawRect(x, ground - height, width, height + 6, house);
                Draw.Path(c, $"M{x - 4} {ground - height + 1} L{x + width / 2} {ground - height - 14} L{x + width + 4} {ground - height + 1} Z", roof);
                c.DrawRect(x + width / 2 - 3, ground - height + 8, 6, 7, lit);
            }
            c.DrawRect(1010, 396, 18, 54, house);
            Draw.Path(c, "M1006 398 L1019 356 L1032 398 Z", roof);
        }

        using (var mid = Draw.VerticalGradient(520, 900, (0, "#8c7a45"), (1, "#5f6a35")))
            Draw.Path(c, MidHill, mid);
        // Die Rebflächen liegen auf dem Hang: Nichts davon darf über die Hügelkante hinausragen
        using (var slope = SKPath.ParseSvgPathData(MidHill))
        {
            c.Save();
            c.ClipPath(slope, antialias: true);
            foreach (var parcel in Parcels) Parcel(c, parcel.Path, parcel.Dark, parcel.Light, parcel.Angle);
            c.Restore();
        }

        // Fluss mit dem Widerschein der Sonne
        using (var river = Draw.Fill(SKColor.Parse("#c9d7e6").WithAlpha(150))) Draw.Path(c, River, river);
        for (var i = 0; i < 3; i++)
        {
            // Der Widerschein glitzert
            var alpha = (byte)(110 + 70 * MathF.Sin(t * 1.7f + i * 2.1f));
            using var shine = Draw.Stroke(SKColor.Parse("#fff3c4").WithAlpha(alpha), 4);
            var half = (90 - i * 30) * (1 + 0.08f * MathF.Sin(t * 1.1f + i));
            c.DrawLine(1380 - half, 700 + i * 11.5f, 1380 + half, 696 + i * 11.5f, shine);
        }

        // Vorderer Hang mit Rebzeilen, die auf den Betrachter zulaufen
        using (var hill = SKPath.ParseSvgPathData(NearHill))
        {
            using (var near = Draw.VerticalGradient(700, h, (0, "#6f7f33"), (1, "#2c3d1a"))) c.DrawPath(hill, near);
            c.Save();
            c.ClipPath(hill, antialias: true);
            using var row = Draw.Fill(SKColor.Parse("#22331a").WithAlpha(105));
            using var leaf = Draw.Fill(SKColor.Parse("#9ab04a").WithAlpha(70));
            for (var i = -34; i <= 34; i++)
            {
                var top = 960 + i * 40f;
                var bottom = 960 + i * 150f;
                using var strip = new SKPath();
                strip.MoveTo(top - 4, 690);
                strip.LineTo(top + 4, 690);
                strip.LineTo(bottom + 22, 1090);
                strip.LineTo(bottom - 22, 1090);
                strip.Close();
                c.DrawPath(strip, row);
                using var edge = new SKPath();
                edge.MoveTo(top + 4, 690);
                edge.LineTo(top + 7, 690);
                edge.LineTo(bottom + 34, 1090);
                edge.LineTo(bottom + 22, 1090);
                edge.Close();
                c.DrawPath(edge, leaf);
            }
            c.Restore();
        }

        Estate(c);
        WeatherFx.Smoke(c, 1281, 672, t);

        // Abdunkelung: links für die Schaltflächen, unten als Rahmen
        using (var left = Draw.HorizontalGradient(0, 1150, (0, "#eb140a0e"), (0.45f, "#a6140a0e"), (1, "#00140a0e")))
            c.DrawRect(0, 0, w, h, left);
        using (var low = Draw.VerticalGradient(840, h, (0, "#00140a0e"), (1, "#b3140a0e")))
            c.DrawRect(0, 840, w, h - 840, low);

        if (title) Title(c);
    }

    static void Parcel(SKCanvas c, string data, string dark, string light, float angle)
    {
        using var path = SKPath.ParseSvgPathData(data);
        using (var ground = Draw.Fill(dark)) c.DrawPath(path, ground);
        c.Save();
        c.ClipPath(path, antialias: true);
        var bounds = path.Bounds;
        using (var row = Draw.Stroke(light, 9))
        {
            c.RotateDegrees(angle, bounds.MidX, bounds.MidY);
            for (var y = bounds.Top - 160; y < bounds.Bottom + 160; y += 22) c.DrawLine(bounds.Left - 200, y, bounds.Right + 200, y, row);
        }
        c.Restore();
        using var edge = Draw.Stroke(Theme.SkNight.WithAlpha(110), 3);
        c.DrawPath(path, edge);
    }

    /// <summary>Gutshaus, Kelterhaus und Vinothek im Abendlicht</summary>
    static void Estate(SKCanvas c)
    {
        using (var shadow = Draw.Fill(SKColors.Black.WithAlpha(60))) c.DrawOval(1470, 896, 430, 16, shadow);
        using (var tree = Draw.Fill("#2c4420"))
        using (var treeLight = Draw.Fill("#4a6a2c"))
        {
            c.DrawOval(1040, 800, 46, 78, tree);
            c.DrawOval(1028, 786, 26, 50, treeLight);
            c.DrawOval(1886, 812, 40, 70, tree);
            c.DrawOval(1876, 800, 22, 44, treeLight);
        }
        using (var trunk = Draw.Fill("#3a2a22"))
        {
            c.DrawRect(1034, 860, 12, 34, trunk);
            c.DrawRect(1880, 868, 12, 28, trunk);
        }
        using var lit = Draw.Fill("#ffd98a");

        using (var wall = Draw.Fill("#efe1c6")) c.DrawRect(1080, 742, 250, 150, wall);
        using (var roof = Draw.Fill("#8a3b2c")) Draw.Path(c, "M1060 746 L1205 658 L1350 746 Z", roof);
        using (var chimney = Draw.Fill("#6f2f24")) c.DrawRect(1270, 676, 22, 44, chimney);
        using (var door = Draw.Fill("#5c1429")) c.DrawRoundRect(1180, 816, 50, 76, 24, 24, door);
        c.DrawRect(1110, 780, 40, 46, lit);
        c.DrawRect(1260, 780, 40, 46, lit);
        c.DrawCircle(1205, 712, 13, lit);
        using (var frame = Draw.Stroke("#6a4a2a", 3))
        {
            c.DrawLine(1130, 780, 1130, 826, frame);
            c.DrawLine(1280, 780, 1280, 826, frame);
        }

        using (var wall = Draw.Fill("#d9c7a3")) c.DrawRect(1370, 782, 210, 110, wall);
        using (var roof = Draw.Fill("#6f4a36")) Draw.Path(c, "M1356 786 L1475 720 L1594 786 Z", roof);
        using (var gate = Draw.Fill("#3a2a22")) Draw.Path(c, "M1425 892 L1425 836 A50 50 0 0 1 1525 836 L1525 892 Z", gate);
        using (var oak = Draw.Fill("#b07a45"))
        using (var hoop = Draw.Stroke("#2c2622", 3))
        {
            c.DrawOval(1600, 878, 20, 16, oak);
            c.DrawOval(1600, 878, 20, 16, hoop);
        }

        using (var wall = Draw.Fill("#f4ead7")) c.DrawRect(1650, 802, 210, 90, wall);
        using (var band = Draw.Fill("#5c1429")) c.DrawRect(1638, 786, 234, 22, band);
        c.DrawRect(1666, 824, 50, 50, lit);
        c.DrawRect(1730, 824, 50, 50, lit);
        c.DrawRect(1794, 824, 50, 50, lit);
    }

    /// <summary>Schriftzug „WinzerAI“ mit goldenem „AI“ und Zierlinie</summary>
    static void Title(SKCanvas c)
    {
        using var font = new SKFont(SerifBold, 168);
        var first = font.MeasureText("Winzer");
        using (var shadow = new SKPaint { IsAntialias = true, Color = SKColors.Black.WithAlpha(150), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 10) })
            c.DrawText("WinzerAI", 108, 252, font, shadow);
        using (var cream = Draw.Fill("#fbf4e4")) c.DrawText("Winzer", 104, 244, font, cream);
        using (var gold = Draw.VerticalGradient(120, 250, (0, "#fbe39a"), (1, "#c8962e"))) c.DrawText("AI", 104 + first, 244, font, gold);

        using var line = Draw.HorizontalGradient(110, 760, (0, "#f6d477"), (1, "#00f6d477"));
        c.DrawRect(116, 290, 640, 3, line);
        using var berry = Draw.Fill(Theme.SkGold);
        c.DrawCircle(116, 291, 7, berry);
    }
}
