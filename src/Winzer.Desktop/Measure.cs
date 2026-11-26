using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;
using Winzer.Ui;

namespace Winzer.Desktop;

public partial class App
{
    /// <summary>
    /// Entwicklerhilfe: <c>--messen bericht.txt [--sekunden 5] [--takt wpf|bild] [--wpf-takt] [--software]</c> misst die
    /// Bildrate im echten Fenster. Das Hauptmenü und, nachdem der Autopilot eine Partie (Regeln 2, Mosel, Seed 7) bis in
    /// den Mai des zweiten Jahres gespielt hat, jede Szene werden einige Sekunden lang ständig neu gezeichnet – das Wetter
    /// bewegt sich wie im Spiel. <c>--takt wpf</c> fordert die Bilder im Takt von <c>CompositionTarget.Rendering</c> an,
    /// <c>--takt bild</c> nach jedem gezeichneten Bild. Gezählt werden die tatsächlich gezeichneten Bilder
    /// (<c>WasDrawn</c>) und die Zeit von <c>WillDraw</c> bis <c>WasDrawn</c>. Es wird nichts gesichert.
    /// Auswertung und Vergleich der DrawnUi-Versionen: docs/leistung-drawnui.md.
    /// </summary>
    async void RunMeasure(string file, string[] args)
    {
        string Arg(string name, string fallback)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        var seconds = double.Parse(Arg("--sekunden", "5"), System.Globalization.CultureInfo.InvariantCulture);
        var accelerated = !args.Contains("--software");
        // --takt wpf: neue Bilder im Takt von CompositionTarget.Rendering anfordern; --takt bild: nach jedem gezeichneten Bild
        var afterFrame = Arg("--takt", "wpf") == "bild";

        var app = new AppHost(new MemorySaveStore(), new SilentAudio());
        var window = new MainWindow(app, accelerated);
        window.ApplyWindowMode(true);
        window.Topmost = true;
        MainWindow = window;
        window.Show();
        window.Activate();
        await Task.Delay(2000);

        // --wpf-takt schaltet DrawnUi ab 1.10.8.26 von seinem DWM-Takt zurück auf CompositionTarget.Rendering, wie bis
        // 1.10.8.25 (docs/leistung-drawnui.md). Greift auf nicht öffentliche Teile von DrawnUiElement zu; fehlen sie in
        // einer anderen Version, bleibt der eigene Takt der Bibliothek und der Bericht sagt es.
        var legacyClock = args.Contains("--wpf-takt") ? "eigener der Bibliothek (--wpf-takt ohne Wirkung)" : "eigener der Bibliothek";
        if (args.Contains("--wpf-takt"))
        {
            const System.Reflection.BindingFlags Hidden = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var type = typeof(DrawnUi.Wpf.DrawnUiElement);
            var stop = type.GetMethod("StopFrames", Hidden);
            var render = type.GetMethod("OnCompositionRendering", Hidden);
            var flag = type.GetField("_renderingSubscribed", Hidden);
            if (stop is not null && render is not null && flag is not null)
            {
                stop.Invoke(window.Surface, null);
                flag.SetValue(window.Surface, true);
                CompositionTarget.Rendering += (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), window.Surface, render);
                legacyClock = "CompositionTarget.Rendering (mit --wpf-takt umgeschaltet)";
            }
        }
        var canvas = window.Surface.Canvas!;
        var clock = Stopwatch.StartNew();
        var frames = 0;
        var drawTimes = new List<double>();
        long drawStart = 0;
        var counting = false;
        var gate = new object();
        canvas.WillDraw += (_, _) => Interlocked.Exchange(ref drawStart, clock.ElapsedTicks);
        canvas.WasDrawn += (_, _) =>
        {
            var ms = (clock.ElapsedTicks - Interlocked.Read(ref drawStart)) * 1000.0 / Stopwatch.Frequency;
            lock (gate)
            {
                if (!counting) return;
                frames++;
                drawTimes.Add(ms);
            }
        };

        var report = new StringBuilder();
        report.AppendLine($"WinzerAI – Bildrate · DrawnUi {typeof(DrawnUi.Views.DrawnView).Assembly.GetName().Version} · " +
            $"{(accelerated ? "beschleunigt" : "Software")} · {RuntimeInformationText()}");
        report.AppendLine($"Anforderung: {(afterFrame ? "nach jedem Bild" : "CompositionTarget.Rendering")} · DrawnUi-Takt: {legacyClock} · " +
            $"Stromversorgung: {(SystemParameters.PowerLineStatus == System.Windows.PowerLineStatus.Online ? "Netz" : "Akku")}");
        report.AppendLine($"Fenster {window.Root.ActualWidth:0} × {window.Root.ActualHeight:0} (WPF-Einheiten), DPI-Faktor {VisualTreeHelper.GetDpi(window).DpiScaleX:0.##}, je Ansicht {seconds:0.#} s Dauer-Neuzeichnen");
        report.AppendLine();
        report.AppendLine($"{"Ansicht",-22} {"Bilder/s",9} {"Zeichnen Ø ms",14} {"95 % ms",9} {"max ms",8}");

        async Task Measure(string name)
        {
            // Neuaufbau und erstes Zeichnen abwarten, dann messen
            await Task.Delay(1200);
            lock (gate) { frames = 0; drawTimes.Clear(); counting = true; }
            var start = clock.Elapsed.TotalSeconds;
            void Tick(object? sender, EventArgs e)
            {
                Anim.Step(ambient: true);
                window.Present();
            }
            if (afterFrame)
            {
                // Nach jedem gezeichneten Bild gleich das nächste anfordern – den Takt bestimmt DrawnUi selbst
                void Next(object? sender, DrawnUi.Draw.SkiaDrawingContext? e) => window.Dispatcher.BeginInvoke(() => Tick(null, EventArgs.Empty));
                canvas.WasDrawn += Next;
                Tick(null, EventArgs.Empty);
                await Task.Delay(TimeSpan.FromSeconds(seconds));
                canvas.WasDrawn -= Next;
            }
            else
            {
                CompositionTarget.Rendering += Tick;
                await Task.Delay(TimeSpan.FromSeconds(seconds));
                CompositionTarget.Rendering -= Tick;
            }
            double elapsed;
            List<double> times;
            int count;
            lock (gate)
            {
                counting = false;
                elapsed = clock.Elapsed.TotalSeconds - start;
                times = [.. drawTimes.Order()];
                count = frames;
            }
            double At(double q) => times.Count == 0 ? 0 : times[Math.Min(times.Count - 1, (int)(q * times.Count))];
            report.AppendLine($"{name,-22} {count / elapsed,9:0.0} {(times.Count == 0 ? 0 : times.Average()),14:0.00} {At(0.95),9:0.00} {(times.Count == 0 ? 0 : times[^1]),8:0.00}");
        }

        await Measure("Hauptmenü");

        app.Start(Game.NewGame(new NewGameOptions
        {
            Seed = 7,
            TargetPoints = 600,
            RulesVersion = 2,
            Players =
            [
                new NewPlayer { Name = "Weingut Sonnenhang", RegionId = RegionId.Mosel },
                new NewPlayer { Name = Ai.AiEstateNames[AiProfileId.Tradition], RegionId = RegionId.Mosel, Ai = AiProfileId.Tradition },
                new NewPlayer { Name = Ai.AiEstateNames[AiProfileId.Bio], RegionId = RegionId.Mosel, Ai = AiProfileId.Bio },
                new NewPlayer { Name = Ai.AiEstateNames[AiProfileId.Schlitzohr], RegionId = RegionId.Mosel, Ai = AiProfileId.Schlitzohr },
            ],
        }));
        var session = app.Session!;
        var pilot = new AutoPilot { Adventurous = false };
        while (!session.Over && (session.State.Year < 2 || session.State.Month < 5))
        {
            var turns = pilot.Turns;
            pilot.PlayTurn(session);
            if (pilot.Turns == turns || session.Over) break;
        }
        session.Nav.Overlay = Overlay.None;

        foreach (var (scene, tab) in new[] { (Scene.Gut, 0), (Scene.Weinberg, 0), (Scene.Keller, 0), (Scene.Markt, 0), (Scene.Kontor, 0), (Scene.Dorf, 0) })
        {
            session.Nav.Go(scene, tab);
            await Measure(scene.ToString());
        }

        report.AppendLine();
        report.AppendLine($"Stand der Partie: {RulesV2.TurnName(session.State)} Jahr {session.State.Year}");
        System.IO.File.WriteAllText(file, report.ToString());
        Shutdown();
    }

    static string RuntimeInformationText() =>
        $"{System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture} · .NET {Environment.Version}";
}
