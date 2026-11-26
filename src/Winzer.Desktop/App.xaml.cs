using System.Windows;
using Winzer.Engine;
using Winzer.Presentation;
using Winzer.Ui;

namespace Winzer.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var selfPlay = Array.IndexOf(e.Args, "--selbstspiel");
        if (selfPlay >= 0 && selfPlay + 1 < e.Args.Length)
        {
            RunSelfPlay(e.Args[selfPlay + 1], e.Args);
            return;
        }
        var measure = Array.IndexOf(e.Args, "--messen");
        if (measure >= 0 && measure + 1 < e.Args.Length)
        {
            RunMeasure(e.Args[measure + 1], e.Args);
            return;
        }
        var shot = ShotOptions.Parse(e.Args);
        if (shot is not null)
        {
            RunShot(shot);
            return;
        }
        var store = new FileSaveStore();
        var audio = new SynthAudio { Muted = store.GetSetting("muted") == "1" };
        var app = new AppHost(store, audio) { Notices = new FileNotices() };
        var window = new MainWindow(app);
        window.ApplyWindowMode(app.FullScreen);
        MainWindow = window;
        window.Show();
    }

    /// <summary>
    /// Entwicklerhilfe: <c>--selbstspiel ordner [--seed 7] [--rules 1|2] [--region mosel] [--humans 2] [--target 600] [--wild]</c>
    /// spielt im echten Fenster eine ganze Partie: Der Autopilot zieht für die Menschen, nach jedem Zug werden
    /// reihum alle Szenen, Reiter und Überlagerungen mit dem aktuellen Stand gezeichnet. Im Ordner landen Bildschirmfotos
    /// und ein Bericht (<c>bericht.txt</c>) mit Verlauf, benutzten Zugarten und allen Fehlern beim Zeichnen.
    /// Es wird nichts gesichert. <c>--wild</c> lässt den Piloten auch riskante Züge ausprobieren.
    /// </summary>
    async void RunSelfPlay(string folder, string[] args)
    {
        string Arg(string name, string fallback)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        System.IO.Directory.CreateDirectory(folder);
        var problems = new List<string>();
        DispatcherUnhandledException += (_, error) =>
        {
            problems.Add($"{error.Exception.GetType().Name}: {error.Exception.Message}\n{error.Exception.StackTrace?.Split('\n').FirstOrDefault(l => l.Contains("Winzer"))?.Trim()}");
            error.Handled = true;
        };

        var region = Json.Enum<RegionId>(Arg("--region", "mosel"));
        var humans = int.Parse(Arg("--humans", "1"));
        var options = new NewGameOptions { Seed = int.Parse(Arg("--seed", "7")), RulesVersion = int.Parse(Arg("--rules", "2")), TargetPoints = double.Parse(Arg("--target", "600")) };
        for (var i = 0; i < humans; i++) options.Players.Add(new NewPlayer { Name = i == 0 ? "Weingut Sonnenhang" : $"Gut Lerchenhöhe {i}", RegionId = region });
        foreach (var profile in new[] { AiProfileId.Tradition, AiProfileId.Bio, AiProfileId.Schlitzohr }.Take(4 - humans))
        {
            options.Players.Add(new NewPlayer { Name = Ai.AiEstateNames[profile], RegionId = region, Ai = profile });
        }
        var app = new AppHost(new MemorySaveStore(), new SilentAudio());
        app.Start(Game.NewGame(options));
        var session = app.Session!;
        var builds = 0;
        app.Changed += () => builds++;

        var window = new MainWindow(app, accelerated: false) { SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.Manual, Left = 0, Top = 0 };
        window.Root.Width = 1280;
        window.Root.Height = 720;
        MainWindow = window;
        // Vorn halten: Ein verdecktes Fenster zeichnet nicht, und dann prüfte der Lauf nur den Aufbau, nicht das Malen
        window.Topmost = true;
        window.Show();
        window.Activate();
        await Task.Delay(1500);

        (Scene Scene, int Tab)[] views =
        [
            (Scene.Gut, 0), (Scene.Weinberg, 0), (Scene.Weinberg, 1), (Scene.Weinberg, 2),
            (Scene.Keller, 0), (Scene.Keller, 1), (Scene.Keller, 2), (Scene.Keller, 3),
            (Scene.Markt, 0), (Scene.Markt, 1), (Scene.Markt, 2), (Scene.Markt, 3),
            (Scene.Kontor, 0), (Scene.Kontor, 1), (Scene.Kontor, 2), (Scene.Kontor, 3),
            (Scene.Dorf, 0), (Scene.Dorf, 1), (Scene.Dorf, 2),
        ];
        var delay = int.Parse(Arg("--delay", "70"));
        var pilot = new AutoPilot { Adventurous = args.Contains("--wild") };
        var shown = new Dictionary<string, int>();
        var view = 0;
        var shots = 0;
        var timeline = new List<string>();
        async Task Render(string what)
        {
            shown[what] = shown.GetValueOrDefault(what) + 1;
            await Task.Delay(delay);
        }
        void Shot(string name) => window.SaveScreenshot(System.IO.Path.Combine(folder, $"{++shots:00}-{name}.png"));

        while (!session.Over && pilot.Turns < 1500)
        {
            // Was von selbst erschienen ist, erst zeichnen lassen – der Pilot räumt es danach ab
            if (session.Summary is not null) await Render("Übersicht");
            if (session.LiveLot is not null)
            {
                await Render("Versteigerung");
                if (shown["Versteigerung"] == 1) Shot("versteigerung");
            }
            if (session.Nav.Overlay != Overlay.None)
            {
                var overlay = session.Nav.Overlay;
                await Render("Überlagerung " + overlay);
                if (shown["Überlagerung " + overlay] == 1) Shot("auto-" + overlay.ToString().ToLowerInvariant());
            }
            var year = session.State.Year;
            var turnsBefore = pilot.Turns;
            pilot.PlayTurn(session);
            if (session.Over) break;
            if (pilot.Turns == turnsBefore)
            {
                // Der Zug ließ sich nicht beenden: nicht endlos weiterdrehen, sondern als Fehler melden
                problems.Add($"Partie bleibt stehen: {RulesV2.TurnName(session.State)} Jahr {session.State.Year}, Zug beenden: {session.Problem(new GameAction.EndTurn()) ?? "möglich"}, Meldung: {session.Error}");
                break;
            }
            // Reihum drei Ansichten je Zug mit dem neuen Stand zeichnen
            var waiting = session.Nav.Overlay;
            session.Nav.Overlay = Overlay.None;
            for (var i = 0; i < 3; i++)
            {
                var (scene, tab) = views[view++ % views.Length];
                session.Nav.Go(scene, tab);
                await Render($"{scene} Reiter {tab}");
            }
            if (pilot.Turns % 9 == 0)
            {
                foreach (var overlay in new[] { Overlay.Lexicon, Overlay.Menu }.Concat(RulesV2.On(session.State) ? [Overlay.Plan] : Array.Empty<Overlay>()))
                {
                    session.Nav.SetOverlayTab(pilot.Turns / 9 % 3);
                    session.Nav.Show(overlay);
                    await Render("Überlagerung " + overlay);
                    session.Nav.Overlay = Overlay.None;
                }
            }
            session.Nav.Go(Scene.Gut);
            session.Nav.Overlay = waiting;
            if (session.State.Year != year)
            {
                var p = session.State.Players[0];
                timeline.Add($"Ende Jahr {year}: " + string.Join(" · ", session.State.Players.Select(x => $"{x.Name} {Util.JsNumber(x.Points)} P.{(x.Bankrupt ? " (pleite)" : "")}")) +
                    $" | Konto {Util.FormatEuro(p.Money)}, Kredit {Util.FormatEuro(p.Loan)}, {Util.FormatNumber(Market.TotalHectares(p), 1)} ha | gezeichnet {PaintBox.Paints}");
                if (year % 2 == 0)
                {
                    session.Nav.Overlay = Overlay.None;
                    await Task.Delay(250);
                    Shot($"jahr-{year + 1}");
                    session.Nav.Overlay = waiting;
                }
            }
        }
        await Task.Delay(600);
        Shot("ende");

        var state = session.State;
        var all = typeof(GameAction).GetNestedTypes().Select(t => t.Name).OrderBy(n => n).ToList();
        var report = new System.Text.StringBuilder();
        report.AppendLine($"Selbstspiel: Seed {options.Seed}, Regeln {options.RulesVersion}, {humans} Mensch(en), Gebiet {region}, Ziel {options.TargetPoints}, {(pilot.Adventurous ? "wild" : "vorsichtig")}");
        report.AppendLine($"Ende: {RulesV2.TurnName(state)} Jahr {state.Year} nach {pilot.Turns} Zügen · Sieger: {session.Winner?.Name ?? "keiner"} · beendet: {session.Over}");
        foreach (var p in state.Players) report.AppendLine($"  {p.Name}: {Util.JsNumber(p.Points)} Punkte, Reinvermögen {Util.FormatEuro(Market.NetWorth(p))}, {Util.FormatNumber(Market.TotalHectares(p), 1)} ha{(p.Bankrupt ? ", pleite" : "")}");
        report.AppendLine();
        report.AppendLine("Verlauf:");
        foreach (var line in timeline) report.AppendLine("  " + line);
        report.AppendLine();
        report.AppendLine($"Neuaufbauten der Oberfläche: {builds} · Zeichenvorgänge bemalter Flächen: {PaintBox.Paints}");
        report.AppendLine("Gezeichnet: " + string.Join(", ", shown.OrderBy(x => x.Key).Select(x => $"{x.Key} ×{x.Value}")));
        report.AppendLine();
        report.AppendLine("Zugarten: " + string.Join(", ", pilot.Used.OrderBy(x => x.Key).Select(x => $"{x.Key} ×{x.Value}")));
        report.AppendLine("Nicht vorgekommen: " + string.Join(", ", all.Where(n => !pilot.Used.ContainsKey(n))));
        report.AppendLine();
        report.AppendLine($"Abgelehnte Züge des Piloten: {pilot.Errors.Count}");
        foreach (var error in pilot.Errors.Take(20)) report.AppendLine("  " + error);
        report.AppendLine($"Fehler beim Zeichnen: {problems.Count}");
        foreach (var problem in problems.Distinct().Take(30)) report.AppendLine("  " + problem);
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "bericht.txt"), report.ToString());
        Shutdown(problems.Count == 0 && pilot.Errors.Count == 0 && session.Over ? 0 : 1);
    }

    /// <summary>
    /// Entwicklerhilfe: <c>--shot datei.png [--scene Keller] [--tab 1] [--months 9] [--seed 7] [--region mosel] [--menu]
    /// [--overlay Lexicon|Plan|Review|Show|Auto] [--otab 1] [--rules 1|2] [--click 1750,1015] [--key Space] [--size 1920x1080]</c> spielt eine Partie einige Monate weiter, zeigt die gewünschte Szene,
    /// speichert ein Bildschirmfoto und beendet sich. Es wird nichts auf die Platte gesichert. <c>--click</c> klickt vorher
    /// mit der echten Maus an Stellen der Entwurfsfläche (bewegt den Zeiger kurz); neben dem Bild entsteht eine Textdatei
    /// mit dem Stand danach.
    /// </summary>
    async void RunShot(ShotOptions shot)
    {
        var app = new AppHost(new MemorySaveStore(), new SilentAudio()) { Notices = new FileNotices() };
        if (!shot.Menu)
        {
            var region = Json.Enum<RegionId>(shot.Region);
            app.Start(Game.NewGame(new NewGameOptions
            {
                Seed = shot.Seed,
                TargetPoints = 600,
                RulesVersion = shot.Rules,
                Players =
                [
                    new NewPlayer { Name = "Weingut Sonnenhang", RegionId = region },
                    new NewPlayer { Name = Ai.AiEstateNames[AiProfileId.Tradition], RegionId = region, Ai = AiProfileId.Tradition },
                    new NewPlayer { Name = Ai.AiEstateNames[AiProfileId.Bio], RegionId = region, Ai = AiProfileId.Bio },
                    new NewPlayer { Name = Ai.AiEstateNames[AiProfileId.Schlitzohr], RegionId = region, Ai = AiProfileId.Schlitzohr },
                ],
            }));
            var session = app.Session!;
            // Ambitionen und Jahresziel wählen, damit die Gutsplanung nicht jedes Bild verdeckt
            void Choose()
            {
                foreach (var ambition in RulesV2.Ambitions.Take(RulesV2.AmbitionCount))
                {
                    var choose = new GameAction.ChooseAmbition(ambition.Id);
                    if (session.Problem(choose) is null) session.Run(choose);
                }
                if (session.Player.Goal is null && session.Player.GoalOffers.Count > 0) session.Run(new GameAction.ChooseGoal(session.Player.GoalOffers[0]));
                while (session.Nav.Overlay != Overlay.None) session.Nav.Show(Overlay.None);
            }
            if (shot.Rules >= 2 && shot.Months > 0) Choose();
            for (var i = 0; i < shot.Months && !session.Over; i++)
            {
                if (shot.Rules >= 2) Choose();
                // Offene Versteigerungen und Übersichten abräumen, damit die Partie weiterläuft
                while (session.LiveLot is { } lot) session.Run(new GameAction.AuctionPass(lot.Id));
                if (session.Summary is not null) session.CloseSummary();
                foreach (var script in shot.Script) script(session);
                session.Run(new GameAction.EndTurn());
            }
            if (!shot.KeepSummary && session.Summary is not null) session.CloseSummary();
            // Nur fürs Bild: viele Parzellen und ein bestimmtes Wetter
            var estate = session.Player;
            var patterns = estate.Plots.ToList();
            for (var i = estate.Plots.Count; i < shot.Plots; i++)
            {
                var extra = patterns[i % patterns.Count].Clone();
                extra.Id = $"foto{i}";
                extra.Name = $"{patterns[i % patterns.Count].Name} {i / patterns.Count + 1}";
                estate.Plots.Add(extra);
            }
            if (shot.Weather is { } kind) session.State.LastWeather = kind;
            if (shot.Offer && estate.Plots.Count > 1) session.Run(new GameAction.OfferPlot(estate.Plots[1].Id));
            var waiting = session.Nav.Overlay;
            session.Nav.Go(shot.Scene, shot.Tab);
            // --overlay Auto zeigt, was von selbst erschienen wäre (Prämierung, Rückblick, Gutsplanung)
            if (shot.AutoOverlay) session.Nav.Show(waiting);
            else if (shot.Overlay != Overlay.None) session.Nav.Show(shot.Overlay);
            session.Nav.SetOverlayTab(shot.OverlayTab);
        }
        var window = new MainWindow(app, accelerated: shot.Accelerated) { SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.Manual, Left = 0, Top = 0 };
        window.Root.Width = shot.Width;
        window.Root.Height = shot.Height;
        MainWindow = window;
        window.Show();
        // Lang genug, dass Übergänge (Aufdecken der Prämierung, Füllstände) vor dem Foto fertig sind
        await Task.Delay(3400);
        if (shot.Clicks.Count > 0)
        {
            // Echte Klicks treffen das vorderste Fenster: Mehrere Prüfläufe gleichzeitig dürfen sich nicht in die Quere kommen
            using var turn = new Semaphore(1, 1, "WinzerAI.Klickpruefung");
            var mine = false;
            try
            {
                mine = await Task.Run(() => turn.WaitOne(TimeSpan.FromMinutes(3)));
                window.Topmost = true;
                window.Activate();
                await Task.Delay(400);
                foreach (var (x, y) in shot.Clicks)
                {
                    if (x < 0)
                    {
                        // Negative Stelle = Tastendruck mit diesem virtuellen Tastencode
                        await window.RealKey((byte)y);
                        await Task.Delay(900);
                        continue;
                    }
                    await window.RealClick(x, y);
                    await Task.Delay(1200);
                }
                window.Topmost = false;
            }
            finally
            {
                if (mine) turn.Release();
            }
        }
        if (shot.Hover is { } hover)
        {
            window.Topmost = true;
            window.Activate();
            await Task.Delay(300);
            await window.RealHover(hover.X, hover.Y);
            window.Topmost = false;
        }
        window.SaveScreenshot(shot.File);
        if (app.Session is { } played)
        {
            // Kurzprotokoll neben dem Bild: Was nach den Tipps Sache ist
            var p = played.Player;
            System.IO.File.WriteAllText(shot.File + ".txt",
                $"szene={played.Nav.Scene} reiter={played.Nav.Tab} overlay={played.Nav.Overlay} zug={RulesV2.TurnName(played.State)} jahr={played.State.Year} " +
                $"konto={Util.JsNumber(Math.Round(p.Money))} kredit={Util.JsNumber(p.Loan)} chargen={p.Cellar.Count} posten={p.Bottles.Count} parzellen={p.Plots.Count(x => x.Grape is not null)} " +
                $"zeichnungen={PaintBox.Paints} preis={Util.JsNumber(p.PriceLevel)} ziel={p.Goal} ambitionen={p.Ambitions.Count} fehler={played.Error}");
        }
        Shutdown();
    }
}

sealed class ShotOptions
{
    public string File = "";
    public Scene Scene = Scene.Gut;
    public int Tab;
    public int Months;
    public int Seed = 7;
    public string Region = "mosel";
    public bool Menu;
    public bool KeepSummary;
    public Overlay Overlay = Overlay.None;
    public bool AutoOverlay;
    /// <summary>Stellen der Entwurfsfläche (1920 × 1080), die vor dem Foto nacheinander mit der echten Maus angeklickt werden</summary>
    public List<(double X, double Y)> Clicks = [];
    public int OverlayTab;
    public int Rules = 2;
    /// <summary>Füllt das Gut für das Foto auf so viele Parzellen auf</summary>
    public int Plots;
    public WeatherKind? Weather;
    /// <summary>Zeichnet wie das normale Spiel über die Grafikkarte statt im Speicher</summary>
    public bool Accelerated;
    /// <summary>Meldet fürs Bild die zweite Parzelle zur Versteigerung an</summary>
    public bool Offer;
    /// <summary>Stelle der Entwurfsfläche, über der der echte Mauszeiger beim Foto steht</summary>
    public (double X, double Y)? Hover;
    public double Width = 1280;
    public double Height = 720;
    public List<Action<GameSession>> Script = [];

    public static ShotOptions? Parse(string[] args)
    {
        var index = Array.IndexOf(args, "--shot");
        if (index < 0 || index + 1 >= args.Length) return null;
        var options = new ShotOptions { File = args[index + 1] };
        for (var i = 0; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : "";
            switch (args[i])
            {
                case "--scene": options.Scene = Enum.Parse<Scene>(value, true); break;
                case "--tab": options.Tab = int.Parse(value); break;
                case "--months": options.Months = int.Parse(value); break;
                case "--seed": options.Seed = int.Parse(value); break;
                case "--region": options.Region = value; break;
                case "--menu": options.Menu = true; break;
                case "--summary": options.KeepSummary = true; break;
                case "--overlay" when value == "Auto": options.AutoOverlay = true; break;
                case "--overlay": options.Overlay = Enum.Parse<Overlay>(value, true); break;
                case "--otab": options.OverlayTab = int.Parse(value); break;
                case "--key":
                    // Echter Tastendruck in der Reihenfolge der Klicks: Space, Escape, Tab, Enter, Ziffer oder Buchstabe
                    var code = value switch { "Space" => 0x20, "Escape" => 0x1B, "Tab" => 0x09, "Enter" => 0x0D, _ => (int)char.ToUpperInvariant(value[0]) };
                    options.Clicks.Add((-1, code));
                    break;
                case "--click":
                    var point = value.Split(',');
                    options.Clicks.Add((double.Parse(point[0], System.Globalization.CultureInfo.InvariantCulture), double.Parse(point[1], System.Globalization.CultureInfo.InvariantCulture)));
                    break;
                case "--rules": options.Rules = int.Parse(value); break;
                case "--hover":
                {
                    var spot = value.Split(',');
                    options.Hover = (double.Parse(spot[0], System.Globalization.CultureInfo.InvariantCulture), double.Parse(spot[1], System.Globalization.CultureInfo.InvariantCulture));
                    break;
                }
                case "--offer": options.Offer = true; break;
                case "--accel": options.Accelerated = true; break;
                case "--plots": options.Plots = int.Parse(value); break;
                case "--weather": options.Weather = Json.Enum<WeatherKind>(value); break;
                case "--size":
                    var parts = value.Split('x');
                    options.Width = double.Parse(parts[0]);
                    options.Height = double.Parse(parts[1]);
                    break;
                case "--play":
                    // Lässt den Menschen wie den Traditionswinzer spielen, damit Keller und Markt gefüllt sind
                    options.Script.Add(AutoPlay);
                    break;
            }
        }
        return options;
    }

    /// <summary>Einfache Züge für gefüllte Bildschirmfotos: lesen, was reif ist, und abfüllen, was fertig ist.</summary>
    static void AutoPlay(GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        // Flüssig bleiben, damit abgefüllt werden kann
        if (player.Money < 30_000)
        {
            var borrow = new GameAction.Borrow(Math.Min(60_000, Market.AvailableCredit(player)));
            if (borrow.Amount >= 1_000 && session.Problem(borrow) is null) session.Run(borrow);
        }
        if (state.Month == 10 && state.HalfMonth != 1)
        {
            foreach (var plot in player.Plots.ToList())
            {
                var harvest = new GameAction.Harvest(plot.Id);
                if (session.Problem(harvest) is null) session.Run(harvest);
            }
        }
        foreach (var batch in session.Player.Cellar.ToList())
        {
            if (batch.MonthsUntilReady > 0) continue;
            var bottle = new GameAction.Bottle(batch.Id, Math.Floor(batch.Hectoliters / 2));
            if (bottle.Hectoliters >= 1 && session.Problem(bottle) is null) session.Run(bottle);
        }
        foreach (var lot in session.Player.Bottles.ToList())
        {
            var register = new GameAction.Register(lot.Id);
            if (!lot.Registered && session.Problem(register) is null) session.Run(register);
        }
    }
}
