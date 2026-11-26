using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

/// <summary>
/// Gutsansicht: das Weingut als Bühne. Parzellen und Gebäude führen in die Szenen, unten liegen die
/// Aufgaben des Monats, rechts oben die Neuigkeiten.
/// </summary>
public static class EstateScene
{
    /// <summary>So viele Parzellen zeigt der Hang; was darüber hinausgeht, steht nur in der Weinberg-Szene</summary>
    const int MaxShown = 24;

    /// <summary>Platz einer Parzelle im Hang (Koordinaten der Bühne)</summary>
    record Slot(string Path, float Cx, float Cy, float Width, float Height, int Columns);

    /// <summary>Hügelkante des Rebhangs – alle Parzellen liegen darunter</summary>
    static string Slope(float w, float h) => FormattableString.Invariant($"M0 428 C200 380 520 408 790 390 C1090 372 1390 412 {w} 386 L{w} {h} L0 {h} Z");

    /// <summary>
    /// Verteilt die Parzellen auf den Hang zwischen Hügelkante und Fluss: wenige groß in einer Reihe, viele in bis zu
    /// drei Reihen, die nach hinten kleiner werden. Die vorderste Reihe füllt sich zuerst.
    /// </summary>
    static List<Slot> Layout(int count)
    {
        var slots = new List<Slot>();
        if (count <= 0) return slots;
        var columns = count <= 4 ? count : count <= 8 ? 4 : count <= 12 ? 6 : 8;
        var rows = (count + columns - 1) / columns;
        (float Top, float Bottom)[] bands = rows switch
        {
            1 => [(452, 572)],
            2 => [(508, 584), (434, 500)],
            _ => [(534, 584), (482, 528), (434, 476)],
        };
        for (var r = 0; r < rows; r++)
        {
            var inRow = Math.Min(columns, count - r * columns);
            var (top, bottom) = bands[r];
            var height = bottom - top;
            var cell = Math.Min(430f, 1670f * (1 - 0.07f * r) / columns);
            var left = 905f - inRow * cell / 2;
            var lean = height * 0.3f;
            var gap = Math.Min(16f, cell * 0.06f);
            for (var i = 0; i < inRow; i++)
            {
                var x0 = left + i * cell + gap / 2;
                var x1 = x0 + cell - gap;
                var path = FormattableString.Invariant($"M{x0} {top} L{x1 - lean} {top} L{x1} {bottom} L{x0 + lean} {bottom} Z");
                slots.Add(new Slot(path, (x0 + x1) / 2, (top + bottom) / 2, cell - gap - lean, height, columns));
            }
        }
        return slots;
    }

    record Season(string SkyTop, string SkyMid, string SkyLow, string Far1, string Far2, string Mid1, string Mid2, string Near1, string Near2, bool Snow);

    static Season SeasonFor(int month) => month switch
    {
        12 or 1 or 2 => new("#33415c", "#7d8fa8", "#d9e2ec", "#9aa7b8", "#7c8a9d", "#cfd8e0", "#a9b6c2", "#e8eef2", "#b9c6cf", true),
        3 or 4 or 5 => new("#3f6fa8", "#8fb8de", "#e9f1d8", "#7f95a8", "#657b8c", "#9fbf6a", "#6f9a4a", "#86b04a", "#4a6e2a", false),
        6 or 7 or 8 => new("#2f6fb5", "#7fbbe8", "#f7edc0", "#7c8f9c", "#5f7480", "#8fae4a", "#5f8236", "#6f9a36", "#3a5a20", false),
        _ => new("#27364f", "#b8656a", "#f3b871", "#7c6a78", "#5d5468", "#8c7a45", "#5f6a35", "#6f7f33", "#34451f", false),
    };

    // Jahreszeitenwechsel: Farben blenden von der gezeigten zur neuen Jahreszeit über
    static Season? seasonFrom, seasonTo;
    static double seasonStart;
    static int seasonEpoch = -1;
    const double SeasonSeconds = 1.8;

    static Season SeasonNow(int month)
    {
        var target = SeasonFor(month);
        if (seasonTo is null || seasonEpoch != Anim.Epoch || !Anim.Enabled)
        {
            seasonEpoch = Anim.Epoch;
            seasonFrom = seasonTo = target;
            return target;
        }
        if (target != seasonTo)
        {
            seasonFrom = SeasonShown();
            seasonTo = target;
            seasonStart = Anim.Now;
        }
        return SeasonShown();
    }

    static Season SeasonShown()
    {
        var p = (Anim.Now - seasonStart) / SeasonSeconds;
        if (p >= 1 || seasonFrom == seasonTo) return seasonTo!;
        Anim.Busy();
        var k = (float)(p * p * (3 - 2 * p));
        var (a, b) = (seasonFrom!, seasonTo!);
        string Mix(string x, string y)
        {
            var (cx, cy) = (SKColor.Parse(x), SKColor.Parse(y));
            byte Channel(byte u, byte v) => (byte)Math.Round(u + (v - u) * k);
            return $"#{Channel(cx.Red, cy.Red):x2}{Channel(cx.Green, cy.Green):x2}{Channel(cx.Blue, cy.Blue):x2}";
        }
        return new Season(Mix(a.SkyTop, b.SkyTop), Mix(a.SkyMid, b.SkyMid), Mix(a.SkyLow, b.SkyLow), Mix(a.Far1, b.Far1), Mix(a.Far2, b.Far2),
            Mix(a.Mid1, b.Mid1), Mix(a.Mid2, b.Mid2), Mix(a.Near1, b.Near1), Mix(a.Near2, b.Near2), k > 0.5f ? b.Snow : a.Snow);
    }

    public static SkiaControl Build(GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var scene = Kit.Layer(0, 0, Shell.StageWidth, Shell.StageHeight);
        var badges = Shell.Badges(session);
        var shown = player.Plots.Take(MaxShown).ToList();
        var slots = Layout(shown.Count);

        scene.With(Kit.Paint(0, 0, Shell.StageWidth, Shell.StageHeight, (c, w, h) => PaintPanorama(c, w, h, state, player, shown, slots)));

        // Parzellen: Schild und Tippfläche. Bis vier Parzellen zweizeilig, darüber einzeilig und schmaler.
        for (var i = 0; i < shown.Count; i++)
        {
            var plot = shown[i];
            var slot = slots[i];
            var ripe = IsHarvestable(state, plot);
            var roomy = slots.Count <= 4;
            // Schmale Schilder lassen bei vielen Parzellen die Rebzeilen ringsum sichtbar
            var width = roomy ? 290.0 : Math.Min(280.0, slot.Width * 0.78);
            var height = roomy ? 64.0 : Math.Clamp(slot.Height - 22, 24, 32);
            var label = ripe
                ? Kit.Box(slot.Cx - width / 2, slot.Cy - height / 2, width, height, radius: roomy ? 12 : height / 2, gradient: Kit.Vertical(Theme.Paper, Theme.PaperDark), shadow: true)
                : Kit.Box(slot.Cx - width / 2, slot.Cy - height / 2, width, height, Theme.C("#c71c1418"), roomy ? 12 : height / 2);
            if (roomy)
            {
                label.With(Kit.Text(plot.Name, 14, 6, width - 28, 28, 19, ripe ? Theme.Ink : Theme.Cream, bold: true));
                label.With(Kit.Text(PlotLine(state, plot), 14, 33, width - 28, 26, 17, ripe ? Theme.InkSoft : Theme.CreamSoft));
            }
            else
            {
                var text = slot.Columns >= 8 ? plot.Name : $"{plot.Name} · {PlotShort(state, plot)}";
                var size = height < 28 ? 13.5 : slot.Columns >= 6 ? 15 : 17;
                label.With(Kit.Text(text, 8, (height - size * 1.45) / 2, width - 16, size * 1.5, size, ripe ? Theme.Ink : Theme.Cream, bold: ripe, align: Align.Center));
            }
            var id = plot.Id;
            label.Tap(() =>
            {
                session.Nav.Select("weinberg.plot", id);
                session.Nav.Go(Scene.Weinberg, 0);
            });
            scene.With(label);
            if (ripe) scene.With(Kit.Badge(1, slot.Cx + width / 2 - 4, slot.Cy - height / 2 + 2, roomy ? 13 : 10));
        }
        if (player.Plots.Count > shown.Count)
        {
            scene.With(Kit.Button(Lang.En ? $"+ {player.Plots.Count - shown.Count} more sites" : $"+ {player.Plots.Count - shown.Count} weitere Lagen", 28, 280, 250, 48, () => session.Nav.Go(Scene.Weinberg, 0), ButtonKind.Dark, fontSize: 18, align: Align.Center));
        }

        // Gebäude
        scene.With(Building(Lang.T("Kontor", "Office"), 508, 822, badges.GetValueOrDefault(Scene.Kontor), () => session.Nav.Go(Scene.Kontor)));
        scene.With(Building(Lang.T("Keller", "Cellar"), 778, 822, badges.GetValueOrDefault(Scene.Keller), () => session.Nav.Go(Scene.Keller)));
        scene.With(Building(player.HasVinothek ? Lang.T("Vinothek", "Wine room") : Lang.T("Hofladen", "Farm shop"), 1048, 822, badges.GetValueOrDefault(Scene.Markt), () => session.Nav.Go(Scene.Markt)));
        scene.With(Kit.Box(448, 660, 250, 150, Theme.Clear).Tap(() => session.Nav.Go(Scene.Kontor)));
        scene.With(Kit.Box(738, 700, 210, 110, Theme.Clear).Tap(() => session.Nav.Go(Scene.Keller)));
        scene.With(Kit.Box(998, 704, 230, 106, Theme.Clear).Tap(() => session.Nav.Go(Scene.Markt)));
        scene.With(Building(Lang.T("Dorf", "Village"), 1640, 372, 0, () => session.Nav.Go(Scene.Dorf), 96));

        // Wetterhinweise oben links
        var chipY = 24.0;
        void Chip(string text)
        {
            scene.With(Kit.Box(28, chipY, 420, 44, Theme.C("#c71c1418"), 22).With(Kit.Centered(text, 20, 384, 44, 18, Theme.Cream, align: Align.Start)));
            chipY += 52;
        }
        var frost = Defense.FrostChance(state, player);
        if (frost > 0) Chip(Lang.En ? $"Late frost risk this month: {Js.Round(frost * 100)} %{(player.FrostCandles ? " · candles ready" : "")}" : $"Spätfrostgefahr diesen Monat: {Js.Round(frost * 100)} %{(player.FrostCandles ? " · Kerzen stehen" : "")}");
        if (state.Month is 11 or 12) Chip(Lang.En ? $"Eiswein frost in December: {Js.Round(Vineyard.IceWineChance(state, player.RegionId) * 100)} %" : $"Eiswein-Frost im Dezember: {Js.Round(Vineyard.IceWineChance(state, player.RegionId) * 100)} %");
        if (state.ClimateChange && state.Year > 1) Chip(Lang.En ? $"Climate: +{Util.FormatNumber(Rules.ClimateWarming(state), 1)} warming steps since the start" : $"Klima: +{Util.FormatNumber(Rules.ClimateWarming(state), 1)} Wärmestufen seit Beginn");

        scene.With(News(session));
        scene.With(Tasks(session));

        if (!session.Over)
        {
            scene.With(Kit.Box(1374, 876, 96, 96, Theme.C("#cc1c1418"), 18, Theme.GlassLine, 1.5, shadow: true)
                .With(Kit.Paint(24, 18, 48, 44, (c, w, h) =>
                {
                    using var p = Draw.Fill(Theme.SkCream);
                    Draw.Path(c, "M2 4 l22 18 -22 18 Z M24 4 l22 18 -22 18 Z", p);
                }))
                .With(Kit.Text(Lang.T("Vorspulen", "Fast-forward"), 0, 68, 96, 20, 14, Theme.CreamSoft, align: Align.Center))
                .Tap(() => session.Run(new GameAction.AutoSkipTurn())));
            var next = RulesV2.NextTurnName(state);
            var end = Kit.Box(1486, 876, 298, 96, radius: 18, gradient: Kit.Vertical(Theme.Wine, Theme.WineDark), stroke: Theme.Gold, strokeWidth: 3, shadow: true);
            end.With(Kit.Text(session.HotSeat || state.HalfMonth > 0 ? Lang.T("Zug beenden", "End turn") : Lang.T("Monat beenden", "End month"), 0, 16, 298, 40, 27, Theme.C("#fff6e6"), bold: true, serif: true, align: Align.Center));
            end.With(Kit.Text(Lang.En ? $"on to {next}{(next.Length > 12 ? "" : " · Space")}" : $"weiter in {(next.StartsWith("die ") ? "" : "den ")}{next}{(next.Length > 12 ? "" : " · Leertaste")}", 0, 58, 298, 24, 15, Theme.C("#f0cfd6"), align: Align.Center));
            end.Tap(() => session.Run(new GameAction.EndTurn()));
            scene.With(end);
        }
        return scene;
    }

    static bool IsHarvestable(GameState state, Plot plot) =>
        plot.Grape is not null && !plot.HarvestedThisYear && plot.Oechsle > 0 &&
        Rules.PlotAgeFactor(plot, state.Year) > 0 && Array.IndexOf(Rules.HarvestMonths, state.Month) >= 0;

    /// <summary>Kurzfassung für die einzeiligen Schilder bei vielen Parzellen</summary>
    static string PlotShort(GameState state, Plot plot)
    {
        if (plot.ForAuction) return Lang.T("Auktion", "Auction");
        if (plot.Grape is not { } grapeId) return Lang.T("Brache", "Fallow");
        if (Rules.PlotAgeFactor(plot, state.Year) <= 0) return Lang.T("Junganlage", "Young vines");
        if (plot.HarvestedThisYear) return Lang.T("gelesen", "harvested");
        if (plot.IceWineGamble) return "Eiswein";
        if (plot.Oechsle > 0) return $"{Js.Round(plot.Oechsle)} °Oe";
        return Grapes.All[grapeId].Name.Replace(" (PIWI)", "");
    }

    static string PlotLine(GameState state, Plot plot)
    {
        var ha = $"{Util.FormatNumber(plot.Hectares, 1)} ha";
        if (plot.ForAuction) return Lang.En ? $"up for auction · {ha}" : $"wird versteigert · {ha}";
        if (plot.Grape is not { } grapeId) return $"{Lang.T("Brache", "Fallow")} · {ha}";
        var grape = Grapes.All[grapeId].Name.Replace(" (PIWI)", "");
        var age = Rules.PlotAgeFactor(plot, state.Year);
        if (age <= 0) return $"{grape} · {Lang.T("Junganlage", "young vines")}";
        if (plot.HarvestedThisYear) return $"{grape} · {Lang.T("gelesen", "harvested")}";
        if (plot.IceWineGamble) return Lang.En ? $"{grape} · hanging for Eiswein" : $"{grape} · hängt für Eiswein";
        if (plot.Oechsle > 0) return $"{grape} · {Js.Round(plot.Oechsle)} °Oe";
        return $"{grape} · {ha}";
    }

    static SkiaControl Building(string name, double cx, double y, int badge, Action tap, double width = 130)
    {
        var layer = Kit.Layer(cx - width / 2, y, width + 20, 44);
        layer.With(Kit.Box(0, 4, width, 34, Theme.C("#c71c1418"), 17).With(Kit.Centered(name, 0, width, 34, 19, Theme.Cream)));
        if (badge > 0) layer.With(Kit.Badge(badge, width - 6, 8, 13));
        layer.Tap(tap);
        return layer;
    }

    static SkiaControl News(GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var month = state.Month == 1 ? 12 : state.Month - 1;
        var year = state.Month == 1 ? state.Year - 1 : state.Year;
        var lines = new List<(string Text, Color Dot)>();
        foreach (var e in state.Log)
        {
            if (e.Tone is null || e.Month != month || e.Year != year || (e.PlayerId is not null && e.PlayerId != player.Id)) continue;
            lines.Add((e.Text, e.Tone == LogTone.Good ? Theme.GoodLight : Theme.Warn));
        }
        foreach (var effect in player.Effects)
        {
            lines.Add((Lang.En ? $"{effect.Label}: {EffectText(effect)} · {effect.MonthsLeft} mo. left" : $"{effect.Label}: {EffectText(effect)} · noch {effect.MonthsLeft} Mon.", Theme.Gold));
        }
        var panel = Kit.Glass(1388, 24, 396, 248);
        panel.With(Kit.Text(Lang.T("Neuigkeiten", "News"), 24, 14, 300, 32, 22, Theme.Gold, bold: true, serif: true));
        const int perPage = 3;
        var pages = Math.Max(1, (int)Math.Ceiling(lines.Count / (double)perPage));
        var page = session.Nav.Page("gut.news", pages);
        if (lines.Count == 0)
        {
            panel.With(Kit.Text(Lang.T("Keine besonderen Vorkommnisse im letzten Monat.", "Nothing special happened last month."), 24, 60, 348, 56, 18, Theme.CreamSoft, maxLines: 2));
        }
        var y = 54.0;
        foreach (var line in lines.Skip(page * perPage).Take(perPage))
        {
            panel.With(Kit.Box(24, y + 9, 12, 12, line.Dot, 6));
            panel.With(Kit.Text(line.Text, 46, y, 330, 48, 16, Theme.Cream, maxLines: 2));
            y += 50;
        }
        if (pages > 1) panel.With(Kit.Pager(24, 206, 348, page, pages, p => session.Nav.SetPage("gut.news", p)));
        return panel;
    }

    public static string EffectText(Effect effect)
    {
        if (effect.Kind == EffectKind.Guard) return Lang.T("Schutz vor Sabotage", "Protection from sabotage");
        if (effect.Kind == EffectKind.Quality) return $"{(effect.Value > 0 ? "+" : "")}{Util.JsNumber(effect.Value)} {Lang.T("Güte", "quality")}";
        var percent = Js.Round((effect.Value - 1) * 100);
        var what = effect.Kind switch { EffectKind.Demand => Lang.T("Nachfrage", "demand"), EffectKind.Price => Lang.T("Preise", "prices"), _ => Lang.T("Kosten", "costs") };
        return $"{(percent > 0 ? "+" : "−")}{Math.Abs(percent)} % {what}";
    }

    static SkiaControl Tasks(GameSession session)
    {
        var layer = Kit.Layer(24, 836, 1340, 140);
        if (session.Over) return layer;
        var hints = Advisor.MonthHints(session.State, session.Player);
        const int perPage = 4;
        var pages = Math.Max(1, (int)Math.Ceiling(hints.Count / (double)perPage));
        var page = session.Nav.Page("gut.tasks", pages);
        layer.With(Kit.Box(0, 0, 250, 32, Theme.C("#c71c1418"), 16)
            .With(Kit.Text(Lang.En ? $"This month · {hints.Count} {(hints.Count == 1 ? "task" : "tasks")}" : $"Diesen Monat · {hints.Count} {(hints.Count == 1 ? "Aufgabe" : "Aufgaben")}", 14, 5, 230, 24, 16, Theme.Cream)));
        if (pages > 1)
        {
            layer.With(Kit.Box(260, 0, 250, 32, Theme.C("#c71c1418"), 16));
            layer.With(Kit.Pager(274, -2, 228, page, pages, p => session.Nav.SetPage("gut.tasks", p)));
        }
        var x = 0.0;
        foreach (var hint in hints.Skip(page * perPage).Take(perPage))
        {
            var target = Navigator.SceneFor(hint.Area);
            var card = Kit.Card(x, 40, 322, 96, 16);
            card.With(Kit.Box(0, 0, 10, 96, hint.Important ? Theme.Alert : Theme.C("#7d9a3a"), 5));
            card.With(Kit.Text(hint.Text, 26, 8, 284, 58, 16, Theme.Ink, bold: hint.Important, maxLines: 3));
            if (hint.Area == HintArea.Hof)
            {
                if (hint.Text.StartsWith("Wähle") || hint.Text.StartsWith("Choose"))
                {
                    card.With(Kit.Text(Lang.T("Zur Gutsplanung ›", "To estate planning ›"), 26, 68, 284, 22, 15, Theme.Wine, bold: true));
                    card.Tap(() => session.Nav.Show(Overlay.Plan));
                }
                else if (session.Player.Decision is not null)
                {
                    card.With(Kit.Text(Lang.T("Entscheiden ›", "Decide ›"), 26, 68, 284, 22, 15, Theme.Wine, bold: true));
                    card.Tap(() => session.Nav.Show(Overlay.Decision));
                }
            }
            else
            {
                card.With(Kit.Text(Lang.En ? $"Go to {Navigator.LabelFor(target)} ›" : $"Zum {Navigator.LabelFor(target)} ›", 26, 68, 284, 22, 15, Theme.Wine, bold: true));
                card.Tap(() => session.Nav.Go(target));
            }
            layer.With(card);
            x += 338;
        }
        return layer;
    }

    // ---- Panorama ----

    static void PaintPanorama(SKCanvas c, float w, float h, GameState state, Player player, List<Plot> plots, List<Slot> slots)
    {
        var t = Anim.Use();
        var season = SeasonNow(state.Month);
        var wet = state.LastWeather == WeatherKind.Nass;
        using (var sky = Draw.VerticalGradient(0, h * 0.8f, (0, season.SkyTop), (0.5f, season.SkyMid), (1, season.SkyLow)))
            c.DrawRect(0, 0, w, h, sky);

        // Sonne: Stand nach Jahreszeit, bei Nässe verdeckt
        var weather = state.LastWeather;
        if (!wet)
        {
            var sunY = (float)Anim.Tween("gut.sonne", state.Month is >= 5 and <= 8 ? 190 : 330, SeasonSeconds);
            WeatherFx.Sun(c, 1268, sunY, 260, 58, t, rays: weather == WeatherKind.Sonnig && !season.Snow);
        }
        // Wolken ziehen: bei Sonne wenige und gemächlich, bei Nässe eine dichte, graue Decke
        var cloudColor = wet ? SKColor.Parse(season.Snow ? "#dfe6ee" : "#c3cbd8").WithAlpha(170) : SKColors.White.WithAlpha((byte)(weather == WeatherKind.Sonnig ? 70 : 105));
        WeatherFx.Clouds(c, w, t, wet ? 11 : weather == WeatherKind.Sonnig ? 3 : 6, wet ? 26 : weather == WeatherKind.Sonnig ? 9 : 17, cloudColor, 70, wet ? 250 : 210);

        // Ein Vogelschwarm zieht vorbei, solange es nicht regnet oder schneit
        if (!wet && !season.Snow) EstateFx.Birds(c, w, t);

        using (var far = Draw.VerticalGradient(330, h, (0, season.Far1), (1, season.Far2)))
            Draw.Path(c, $"M0 428 C148 328 408 378 648 348 C928 308 1188 378 1448 338 C1608 318 1728 348 {w} 338 L{w} {h} L0 {h} Z", far);

        // Dorf am fernen Hang
        using (var house = Draw.Fill(season.Snow ? "#c9d2da" : "#d9c7a3"))
        using (var roof = Draw.Fill("#7a4a3a"))
        {
            c.DrawRect(1604, 330, 26, 30, house);
            Draw.Path(c, "M1600 332 L1617 314 L1634 332 Z", roof);
            c.DrawRect(1640, 316, 18, 44, house);
            Draw.Path(c, "M1637 318 L1649 286 L1661 318 Z", roof);
            c.DrawRect(1668, 336, 30, 24, house);
            Draw.Path(c, "M1664 338 L1683 322 L1702 338 Z", roof);
        }

        using (var mid = Draw.VerticalGradient(430, h, (0, season.Mid1), (1, season.Mid2)))
            Draw.Path(c, Slope(w, h), mid);

        // Parzellen liegen auf dem Rebhang; nichts ragt über die Hügelkante
        using (var slope = SKPath.ParseSvgPathData(Slope(w, h)))
        {
            c.Save();
            c.ClipPath(slope, antialias: true);
            for (var i = 0; i < plots.Count; i++) PaintPlot(c, slots[i].Path, state, plots[i], Math.Clamp(slots[i].Height / 110f, 0.45f, 1f), t, i);
            c.Restore();
        }

        // Fluss
        using (var river = Draw.Fill(SKColor.Parse(season.Snow ? "#dfe8ef" : "#c9d7e6").WithAlpha(150)))
            Draw.Path(c, $"M0 608 C188 568 408 628 708 598 C988 568 1288 628 {w} 578 L{w} 638 C1288 688 988 628 708 658 C408 688 188 628 0 668 Z", river);

        using (var near = Draw.VerticalGradient(640, h, (0, season.Near1), (1, season.Near2)))
            Draw.Path(c, $"M0 668 C248 608 648 688 1008 638 C1348 598 1588 648 {w} 628 L{w} {h} L0 {h} Z", near);

        var cold = state.Month is 11 or 12 or 1 or 2 or 3;
        var dusk = state.Month is >= 9 or <= 2;
        PaintBuildings(c, player, season.Snow, dusk, t);
        if (cold) WeatherFx.Smoke(c, 649, 590, t);
        // Zur Lese fährt der Traktor die Trauben ein
        if (!season.Snow && Array.IndexOf(Rules.HarvestMonths, state.Month) >= 0) EstateFx.Tractor(c, 1270, 1740, 808, t);

        // Niederschlag und Laub liegen über allem, lassen aber die Aufgaben am unteren Rand frei
        c.Save();
        c.ClipRect(new SKRect(0, 0, w, h - 150));
        if (season.Snow)
        {
            if (weather != WeatherKind.Sonnig) WeatherFx.Snow(c, w, h, t, wet ? 190 : 90);
        }
        else if (wet)
        {
            using (var veil = Draw.Fill(SKColor.Parse("#33415c").WithAlpha(70))) c.DrawRect(0, 0, w, h, veil);
            WeatherFx.Rain(c, w, h, t, 170);
        }
        else if (state.Month is 10 or 11)
        {
            WeatherFx.Leaves(c, w, h, t, weather == WeatherKind.Sonnig ? 10 : 18);
        }
        c.Restore();
    }

    static void PaintPlot(SKCanvas c, string pathData, GameState state, Plot plot, float scale = 1, float t = 0, int index = 0)
    {
        using var path = SKPath.ParseSvgPathData(pathData);
        var winter = state.Month is 11 or 12 or 1 or 2 or 3;
        string dark, light;
        var rows = true;
        if (plot.Grape is not { } grapeId)
        {
            (dark, light, rows) = ("#7a6a4a", "#93825c", false);
        }
        else
        {
            var grape = Grapes.All[grapeId];
            var age = Rules.PlotAgeFactor(plot, state.Year);
            if (plot.HarvestedThisYear || (winter && !plot.IceWineGamble && plot.Oechsle <= 0)) (dark, light) = ("#5a4a34", "#7c6a4c");
            else if (age <= 0) (dark, light) = ("#6f6a45", "#9aa860");
            else if (plot.Oechsle >= 60) (dark, light) = grape.Color == GrapeColor.Rot ? ("#6a3320", "#b5562e") : ("#6b5a26", "#c9a23a");
            else (dark, light) = ("#3f5a22", "#7d9a3a");
        }
        using (var ground = Draw.Fill(dark)) c.DrawPath(path, ground);
        c.Save();
        c.ClipPath(path, antialias: true);
        var bounds = path.Bounds;
        if (rows)
        {
            using var row = Draw.Stroke(light, 9 * scale);
            // Belaubte Zeilen wiegen sich leicht im Wind
            var leafy = !winter && !plot.HarvestedThisYear;
            var sway = leafy ? MathF.Sin(t * 1.1f + index * 1.9f) : 0;
            c.RotateDegrees(-16 + sway * 0.45f, bounds.MidX, bounds.MidY);
            for (var y = bounds.Top - 120 + sway * 1.8f * scale; y < bounds.Bottom + 120; y += 22 * scale) c.DrawLine(bounds.Left - 150, y, bounds.Right + 150, y, row);
        }
        else
        {
            using var dot = Draw.Fill(light);
            for (var y = bounds.Top; y < bounds.Bottom; y += 18)
            {
                for (var x = bounds.Left + ((int)(y / 18) % 2) * 9; x < bounds.Right; x += 18) c.DrawCircle(x, y, 1.8f, dot);
            }
        }
        c.Restore();
        using var edge = Draw.Stroke(Theme.SkNight.WithAlpha(110), 3);
        c.DrawPath(path, edge);
        if (IsHarvestable(state, plot))
        {
            // Lesereif: Der goldene Rand pulsiert sanft und wandert
            var pulse = 0.5f + 0.5f * MathF.Sin(t * 2.6f + index);
            using var mark = Draw.Stroke(Theme.SkGold.WithAlpha((byte)(150 + 105 * pulse)), (5 + 2 * pulse) * Math.Max(0.6f, scale));
            mark.PathEffect = SKPathEffect.CreateDash([18, 10], -t * 12);
            c.DrawPath(path, mark);
        }
        if (plot.HailNet)
        {
            using var net = Draw.Stroke(SKColors.White.WithAlpha(120), 1.5f);
            c.Save();
            c.ClipPath(path);
            for (var x = bounds.Left - 100; x < bounds.Right + 100; x += 16) c.DrawLine(x, bounds.Top, x + 60, bounds.Bottom, net);
            c.Restore();
        }
    }

    static void PaintBuildings(SKCanvas c, Player player, bool snow, bool dusk, float t)
    {
        using var shadow = Draw.Fill(SKColors.Black.WithAlpha(50));
        c.DrawOval(700, 818, 420, 14, shadow);

        // Gutshaus (Kontor)
        using (var wall = Draw.Fill("#efe1c6")) c.DrawRect(448, 660, 250, 150, wall);
        using (var roof = Draw.Fill(snow ? "#e8eef2" : "#8a3b2c")) Draw.Path(c, "M428 664 L573 576 L718 664 Z", roof);
        using (var chimney = Draw.Fill("#6f2f24")) c.DrawRect(638, 594, 22, 44, chimney);
        if (snow)
        {
            using var cap = Draw.Fill("#e8eef2");
            c.DrawRect(635, 590, 28, 7, cap);
        }
        using (var door = Draw.Fill("#5c1429")) c.DrawRoundRect(548, 734, 50, 76, 24, 24, door);
        // In der dunklen Jahreshälfte brennt Licht; es flackert kaum merklich
        using (var window = Draw.Fill(dusk ? SKColor.Parse("#ffd98a").WithAlpha((byte)(225 + 30 * MathF.Sin(t * 3.1f))) : SKColor.Parse("#3a4c66")))
        {
            c.DrawRect(478, 698, 40, 46, window);
            c.DrawRect(628, 698, 40, 46, window);
        }

        // Kelterhaus mit Kellertor
        using (var wall = Draw.Fill("#d9c7a3")) c.DrawRect(738, 700, 210, 110, wall);
        using (var roof = Draw.Fill(snow ? "#e8eef2" : "#6f4a36")) Draw.Path(c, "M724 704 L843 638 L962 704 Z", roof);
        using (var gate = Draw.Fill("#3a2a22")) Draw.Path(c, "M793 810 L793 754 A50 50 0 0 1 893 754 L893 810 Z", gate);
        if (player.BarriqueCapacityHl > 0)
        {
            using var oak = Draw.Fill("#b07a45");
            using var hoop = Draw.Stroke("#2c2622", 3);
            c.DrawOval(970, 796, 20, 16, oak);
            c.DrawOval(970, 796, 20, 16, hoop);
        }

        // Vinothek oder einfacher Hofladen
        if (player.HasVinothek)
        {
            using (var wall = Draw.Fill("#f4ead7")) c.DrawRect(998, 720, 230, 90, wall);
            using (var band = Draw.Fill("#5c1429")) c.DrawRect(986, 704, 254, 22, band);
            using var glass = Draw.Fill("#9cc0d8");
            c.DrawRect(1016, 742, 56, 50, glass);
            c.DrawRect(1086, 742, 56, 50, glass);
            c.DrawRect(1156, 742, 56, 50, glass);
        }
        else
        {
            using (var wall = Draw.Fill("#c9b58f")) c.DrawRect(1018, 744, 150, 66, wall);
            using (var roof = Draw.Fill(snow ? "#e8eef2" : "#7a4a3a")) Draw.Path(c, "M1006 748 L1093 706 L1180 748 Z", roof);
            using var counter = Draw.Fill("#5c1429");
            c.DrawRect(1040, 772, 106, 38, counter);
        }
    }
}
