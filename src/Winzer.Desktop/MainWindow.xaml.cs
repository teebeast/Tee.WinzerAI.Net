using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DrawnUi.Draw;
using DrawnUi.Wpf;
using Winzer.Ui;

namespace Winzer.Desktop;

/// <summary>Das Fenster hält genau eine gezeichnete Fläche; alles Sichtbare kommt aus <see cref="AppHost"/>.</summary>
public partial class MainWindow : Window
{
    readonly AppHost app;
    readonly DrawnUiElement surface;
    /// <summary>Die gezeichnete Fläche – für den Messlauf (<c>--messen</c>)</summary>
    internal DrawnUiElement Surface => surface;
    bool rebuildPending;
    int trailing;


    public MainWindow(AppHost app, bool accelerated = true)
    {
        InitializeComponent();
        this.app = app;
        surface = new DrawnUiElement { RenderingMode = accelerated ? RenderingModeType.Accelerated : RenderingModeType.Default };
        Root.Children.Add(surface);
        app.Changed += ScheduleRebuild;
        app.QuitRequested += Close;
        app.WindowModeChanged += () => ApplyWindowMode(app.FullScreen);
        Root.SizeChanged += (_, _) => ScheduleRebuild();
        PreviewKeyDown += OnKey;
        Loaded += (_, _) => Rebuild();
        Kit.HoverChanged += () => Cursor = Kit.Hovering > 0 ? Cursors.Hand : Cursors.Arrow;

        // Bewegte Bilder (Wetter): nur neu zeichnen, solange etwas Bewegtes auf dem Schirm ist und das Fenster zu sehen ist
        // Hintergrund-Priorität: Mit höherer Priorität drängelt sich die Anforderung vor das Zeichnen der Fläche selbst,
        // und nach einem Neuaufbau bleiben Bilder aus (im Selbstspiel gemessen: ein Zwölftel der Zeichenvorgänge).
        var frames = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(50) };
        frames.Tick += (_, _) =>
        {
            // Im Hintergrund ruht die Dauerbewegung (Wetter); angefangene Übergänge laufen trotzdem zu Ende
            var front = IsVisible && WindowState != WindowState.Minimized && (IsActive || Topmost);
            var draw = Anim.Step(ambient: front);
            // Übergänge (Überblenden, Zählen) laufen flüssig, das Wetter gemächlich
            frames.Interval = TimeSpan.FromMilliseconds(Anim.InTransition ? 16 : 50);
            // Nach der letzten Änderung noch einige Bilder nachschieben: Im beschleunigten Modus kann die Anzeige dem
            // Zeichnen ein Bild hinterherhinken, und ohne Nachlauf bliebe dann das vorletzte stehen.
            if (!draw)
            {
                if (trailing <= 0) return;
                trailing--;
                Show();
                return;
            }
            Present();
        };
        frames.Start();
        Activated += (_, _) =>
        {
            Present();
        };
        Closed += (_, _) => frames.Stop();
    }

    bool fillsWorkArea;
    bool watchesWorkArea;

    /// <summary>
    /// Für das normale Spiel: <paramref name="fullScreen"/> = ohne Titelleiste und so groß wie der Arbeitsbereich des
    /// Bildschirms – die Taskleiste bleibt sichtbar. (Ein rahmenloses Fenster im Zustand „maximiert“ würde sie
    /// überdecken.) Sonst ein normales Fenster mit Titelleiste, das sich verschieben, verkleinern und minimieren lässt.
    /// </summary>
    public void ApplyWindowMode(bool fullScreen)
    {
        fillsWorkArea = fullScreen;
        WindowStartupLocation = WindowStartupLocation.Manual;
        var area = SystemParameters.WorkArea;
        if (fullScreen)
        {
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            MinWidth = 0;
            MinHeight = 0;
            Fit();
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            MinWidth = Math.Min(1280, area.Width);
            MinHeight = Math.Min(760, area.Height);
            Width = Math.Min(1600, area.Width);
            Height = Math.Min(940, area.Height);
            Left = area.Left + (area.Width - Width) / 2;
            Top = area.Top + (area.Height - Height) / 2;
        }
        if (watchesWorkArea) return;
        watchesWorkArea = true;
        // Auflösung geändert oder Taskleiste verschoben: im Vollbild wieder einpassen
        SystemParameters.StaticPropertyChanged += (_, e) =>
        {
            if (fillsWorkArea && e.PropertyName == nameof(SystemParameters.WorkArea)) Fit();
        };
    }

    void Fit()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left;
        Top = area.Top;
        Width = area.Width;
        Height = area.Height;
    }

    /// <summary>Mehrere Änderungen in einem Zug führen zu einem einzigen Neuaufbau.</summary>
    void ScheduleRebuild()
    {
        if (rebuildPending) return;
        rebuildPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            rebuildPending = false;
            Rebuild();
        });
    }

    void Rebuild()
    {
        if (Root.ActualWidth < 1 || Root.ActualHeight < 1) return;
        var previous = surface.Content as SkiaControl;
        surface.Content = app.Build(Root.ActualWidth, Root.ActualHeight);
        previous?.Dispose();
        // Die Fläche zeichnet nur auf Anforderung. Ohne diese Anforderung bliebe nach einem Neuaufbau das alte Bild stehen,
        // bis zufällig die Maus über das Fenster fährt – etwa nach einem Zug per Tastatur.
        Present();
    }

    /// <summary>
    /// Fordert ein neues Bild an und sorgt dafür, dass es auch auf dem Schirm ankommt. Im beschleunigten Modus zeichnet
    /// die Fläche in ein inneres Element; ohne dessen Auffrischung bliebe das alte Bild stehen, bis die Maus sich bewegt.
    /// </summary>
    internal void Present()
    {
        trailing = 8;
        Show();
    }

    void Show()
    {
        surface.Canvas?.Update();
        surface.InvalidateVisual();
        InvalidateBelow(surface);
    }

    static void InvalidateBelow(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            (child as UIElement)?.InvalidateVisual();
            InvalidateBelow(child);
        }
    }

    void OnKey(object sender, KeyEventArgs e)
    {
        var key = e.Key switch
        {
            Key.Space => "Space",
            Key.Enter => "Enter",
            Key.Escape => "Escape",
            Key.Tab => "Tab",
            >= Key.D1 and <= Key.D9 => ((int)e.Key - (int)Key.D0).ToString(),
            >= Key.NumPad1 and <= Key.NumPad9 => ((int)e.Key - (int)Key.NumPad0).ToString(),
            >= Key.A and <= Key.Z => e.Key.ToString(),
            _ => null,
        };
        if (key is not null && app.HandleKey(key)) e.Handled = true;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool SetCursorPos(int x, int y);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool GetCursorPos(out System.Drawing.Point point);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);

    /// <summary>
    /// Klickt mit der echten Maus an eine Stelle der Entwurfsfläche und setzt den Zeiger danach zurück –
    /// die verlässlichste Prüfung, weil sie denselben Weg nimmt wie der Spieler. Das Fenster muss vorn liegen.
    /// </summary>
    public async Task RealClick(double designX, double designY)
    {
        var scale = Math.Min(Root.ActualWidth / Kit.DesignWidth, Root.ActualHeight / Kit.DesignHeight);
        var local = new Point((Root.ActualWidth - Kit.DesignWidth * scale) / 2 + designX * scale, (Root.ActualHeight - Kit.DesignHeight * scale) / 2 + designY * scale);
        var screen = Root.PointToScreen(local);
        GetCursorPos(out var before);
        SetCursorPos((int)screen.X, (int)screen.Y);
        await Task.Delay(120);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        await Task.Delay(60);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        await Task.Delay(120);
        SetCursorPos(before.X, before.Y);
    }

    /// <summary>Stellt den echten Mauszeiger auf eine Stelle der Entwurfsfläche und lässt ihn dort – für Bilder vom Hover-Effekt.</summary>
    public async Task RealHover(double designX, double designY)
    {
        var scale = Math.Min(Root.ActualWidth / Kit.DesignWidth, Root.ActualHeight / Kit.DesignHeight);
        var local = new Point((Root.ActualWidth - Kit.DesignWidth * scale) / 2 + designX * scale, (Root.ActualHeight - Kit.DesignHeight * scale) / 2 + designY * scale);
        var screen = Root.PointToScreen(local);
        SetCursorPos((int)screen.X - 3, (int)screen.Y);
        await Task.Delay(80);
        SetCursorPos((int)screen.X, (int)screen.Y);
        await Task.Delay(500);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);

    /// <summary>Drückt eine echte Taste (virtueller Tastencode) – das Fenster muss vorn liegen.</summary>
    public async Task RealKey(byte virtualKey)
    {
        keybd_event(virtualKey, 0, 0, UIntPtr.Zero);
        await Task.Delay(60);
        keybd_event(virtualKey, 0, 0x0002, UIntPtr.Zero);
        await Task.Delay(120);
    }

    /// <summary>Speichert den Fensterinhalt als PNG – für Bildschirmfotos in Tests und Dokumentation.</summary>
    public void SaveScreenshot(string file)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var bitmap = new RenderTargetBitmap((int)(Root.ActualWidth * dpi.DpiScaleX), (int)(Root.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(Root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file);
        encoder.Save(stream);
    }
}
