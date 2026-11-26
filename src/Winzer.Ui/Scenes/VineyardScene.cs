using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

/// <summary>
/// Weinberg: Parzellen am Hang mit Inspektor (lesen, pflanzen, roden, verkaufen), Pflanzenschutz und Bio-Stufenleiter,
/// Wetterschutz. Die Reiter 2 und 3 liegen in <see cref="VineyardCare"/>, die Malerei in <see cref="VineyardPaint"/>.
/// </summary>
public static class VineyardScene
{
    internal const string PlotKey = "weinberg.plot";
    /// <summary>Rückfrage im Inspektor: "clear:Parzelle" oder "sell:Parzelle"</summary>
    const string ModeKey = "weinberg.mode";
    const string GrapeKey = "weinberg.grape";
    const int PerPage = 8;

    public static SkiaControl Build(GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var tab = Math.Clamp(session.Nav.Tab, 0, 2);
        var scene = Kit.Layer(0, 0, Shell.StageWidth, Shell.StageHeight);
        scene.With(Kit.Paint(0, 0, Shell.StageWidth, Shell.StageHeight, (c, w, h) => VineyardPaint.Hill(c, w, h, state, tab > 0)));

        int[] badges =
        [
            player.Plots.Count(p => Harvestable(session, p)),
            session.Problem(new GameAction.ApplyBioLabel()) is null ? 1 : 0,
            session.Problem(new GameAction.FrostCandles()) is null ? 1 : 0,
        ];
        scene.With(Kit.Tabs(28, 20, Lang.En ? ["Plots", "Crop protection & organic", "Weather"] : ["Parzellen", "Pflanzenschutz & Bio", "Wetterschutz"], tab, session.Nav.SetTab, badges));

        var season = state.Month switch
        {
            3 or 4 => Lang.T("Pflanzzeit", "Planting season"),
            >= 8 => Lang.T("Lesezeit", "Harvest time"),
            >= 5 => Lang.T("Wachstum", "Growing season"),
            _ => Lang.T("Winterruhe", "Winter dormancy"),
        };
        var planted = Util.FormatNumber(Market.PlantedHectares(player), 1);
        var total = Util.FormatNumber(Market.TotalHectares(player), 1);
        var count = player.Plots.Count;
        scene.With(Pill(Lang.En ? $"{Game.MonthNames[state.Month - 1]} · {season} · {planted} of {total} ha planted · {count} {(count == 1 ? "plot" : "plots")}" : $"{Game.MonthNames[state.Month - 1]} · {season} · {planted} von {total} ha bestockt · {count} {(count == 1 ? "Parzelle" : "Parzellen")}",
            696, 24, 534, 40, Theme.C("#c71c1418"), Theme.Cream, 18));

        switch (tab)
        {
            case 1: VineyardCare.Protection(session, scene); break;
            case 2: VineyardCare.Weather(session, scene); break;
            default: Plots(session, scene); break;
        }
        return scene;
    }

    // ---- Gemeinsame Helfer ----

    internal static bool Harvestable(GameSession session, Plot plot) =>
        plot.Oechsle > 0 && session.Problem(new GameAction.Harvest(plot.Id)) is null;

    internal static SkiaShape Pill(string text, double x, double y, double w, double h, Color fill, Color color, double size = 16, bool bold = false) =>
        Kit.Box(x, y, w, h, fill, h / 2).With(Kit.Centered(text, 14, w - 28, h, size, color, bold: bold));

    internal static string GrapeName(GrapeId id) => Grapes.All[id].Name.Replace(" (PIWI)", "");

    internal static string Hectares(double hectares) => $"{Util.FormatNumber(hectares, 1)} ha";

    internal static string PlotSub(Plot plot) =>
        plot.Grape is { } grape ? $"{GrapeName(grape)} · {Hectares(plot.Hectares)}" : $"{Lang.T("Brache", "Fallow")} · {Hectares(plot.Hectares)}";

    static (string Text, Color Color) TrendText(GameState state, GrapeId id)
    {
        if (!state.Trends.TryGetValue((TrendKey)(int)id, out var trend)) return ("100 % →", Theme.Ink);
        var arrow = Trends.TrendDirection(trend) switch { TrendDirectionKind.Up => "↗", TrendDirectionKind.Down => "↘", _ => "→" };
        var color = trend.Value >= 1.05 ? Theme.Good : trend.Value <= 0.95 ? Theme.Alert : Theme.Ink;
        return ($"{Js.Round(trend.Value * 100)} % {arrow}", color);
    }

    static (string Word, Color Color) SuitabilityText(double value) => value switch
    {
        >= 2 => (Lang.T("ideal", "ideal"), Theme.Good),
        >= 1 => (Lang.T("gut geeignet", "well suited"), Theme.Good),
        >= 0 => (Lang.T("geeignet", "suitable"), Theme.InkSoft),
        >= -1 => (Lang.T("schwierig", "difficult"), Theme.Warn),
        _ => (Lang.T("ungeeignet", "unsuitable"), Theme.Alert),
    };

    static string SiteWord(double site) => site switch
    {
        >= 2 => Lang.T("Spitzenlage", "Prime site"),
        >= 1 => Lang.T("sehr gute Lage", "very good site"),
        >= 0 => Lang.T("gute Lage", "good site"),
        _ => Lang.T("einfache Lage", "basic site"),
    };

    // ---- Reiter 1: Parzellen ----

    static void Plots(GameSession session, SkiaLayout scene)
    {
        var player = session.Player;
        var plots = player.Plots;
        if (plots.Count == 0) return;
        var selected = plots.Find(p => p.Id == session.Nav.Selected(PlotKey)) ?? plots[0];
        var pages = Math.Max(1, (int)Math.Ceiling(plots.Count / (double)PerPage));
        // Ohne eigenes Blättern liegt die Seite der gewählten Parzelle auf
        var page = Math.Clamp(plots.IndexOf(selected) / PerPage, 0, pages - 1);
        var shown = plots.Skip(page * PerPage).Take(PerPage).ToList();

        const double tileWidth = 287, gap = 18;
        var twoRows = shown.Count > 4;
        var pictureHeight = twoRows ? 140.0 : 250.0;
        var tileHeight = pictureHeight + 186;
        for (var i = 0; i < shown.Count; i++)
        {
            var inRow = twoRows ? 4 : shown.Count;
            var left = twoRows ? 28 : 28 + (1202 - (inRow * tileWidth + (inRow - 1) * gap)) / 2;
            var x = left + (i % 4) * (tileWidth + gap);
            var y = twoRows ? 92 + (i / 4) * (tileHeight + 16) : 196;
            scene.With(Tile(session, shown[i], x, y, tileWidth, pictureHeight, shown[i] == selected));
        }
        if (pages > 1)
        {
            scene.With(Kit.Box(940, 764, 290, 44, Theme.C("#c71c1418"), 22));
            scene.With(Kit.Pager(958, 768, 262, page, pages, p =>
            {
                var target = Math.Clamp(p, 0, pages - 1);
                session.Nav.Select(PlotKey, plots[target * PerPage].Id);
            }));
        }
        scene.With(RegionPanel(player));
        scene.With(Inspector(session, selected));
    }

    static SkiaControl Tile(GameSession session, Plot plot, double x, double y, double w, double pictureHeight, bool on)
    {
        var state = session.State;
        var h = pictureHeight + 186;
        var tile = Kit.Box(x, y, w, h, Theme.C("#eb1c1418"), 18, on ? Theme.Gold : Theme.GlassLine, on ? 4 : 1.5, shadow: true);
        tile.With(Kit.Paint(8, 8, w - 16, pictureHeight, (c, pw, ph) => VineyardPaint.Plot(c, pw, ph, state, plot)));
        if (Harvestable(session, plot))
            tile.With(Kit.Box(18, 18, 112, 32, radius: 16, gradient: Kit.Vertical(Theme.Gold, Theme.GoldDark)).With(Kit.Centered(Lang.T("lesereif", "ready to harvest"), 0, 112, 32, 17, Theme.Ink, bold: true)));
        else if (plot.Grape is null && session.Problem(new GameAction.Plant(plot.Id, GrapeId.Riesling)) is null)
            tile.With(Kit.Box(18, 18, 132, 32, radius: 16, gradient: Kit.Vertical(Theme.Gold, Theme.GoldDark)).With(Kit.Centered(Lang.T("pflanzbereit", "ready to plant"), 0, 132, 32, 17, Theme.Ink, bold: true)));

        if (plot.ForAuction)
            tile.With(Kit.Box(w - 18 - 148, 18, 148, 32, radius: 16, gradient: Kit.Vertical(Theme.Wine, Theme.WineDark)).With(Kit.Centered(Lang.T("Versteigerung", "Auction"), 0, 148, 32, 17, Theme.Cream, bold: true)));

        var ty = pictureHeight + 16;
        tile.With(Kit.Text(plot.Name, 16, ty, w - 32 - 96, 30, 21, on ? Theme.Gold : Theme.Cream, bold: true));
        tile.With(Kit.Paint(w - 16 - 92, ty + 5, 92, 20, (c, pw, ph) => VineyardPaint.Stars(c, pw, ph, plot.Site)));
        tile.With(Kit.Text(PlotSub(plot), 16, ty + 31, w - 32, 26, 18, Theme.CreamSoft));

        var young = plot.Grape is not null && Rules.PlotAgeFactor(plot, state.Year) == 0;
        if (plot.Grape is { } grapeId && !young && !plot.HarvestedThisYear)
        {
            var color = Grapes.All[grapeId].Color;
            tile.With(Kit.Paint(16, ty + 66, w - 32, 14, (c, pw, ph) => VineyardPaint.Gauge(c, pw, ph, plot.Oechsle, color, true)));
            var ripeness = plot.Oechsle > 0 ? $"{Js.Round(plot.Oechsle)} °Oe · {Quality.LevelForOechsle(plot.Oechsle).Name}" : Lang.T("Reife noch nicht begonnen", "Ripening not yet begun");
            tile.With(Kit.Text(ripeness, 16, ty + 86, w - 32, 26, 18, Theme.Cream, bold: plot.Oechsle > 0));
            var weak = plot.Health < 0.7 || plot.CropFactor < 0.99;
            var health = $"{Lang.T("Gesundheit", "Health")} {Js.Round(plot.Health * 100)} %" + (plot.CropFactor < 0.99 ? $" · {Lang.T("Ertrag", "Yield")} {Js.Round(plot.CropFactor * 100)} %" : "");
            tile.With(Kit.Text(health, 16, ty + 114, w - 32, 24, 16, weak ? Theme.Warn : Theme.CreamSoft));
        }
        else
        {
            var status = plot.Grape is null ? Lang.T("Brache – gepflanzt wird im März und April.", "Fallow – planting happens in March and April.")
                : young ? Lang.T("Junganlage – trägt ab dem dritten Jahr.", "Young vines – they bear from the third year.")
                : Lang.T("In diesem Jahr gelesen.", "Harvested this year.");
            tile.With(Kit.Text(status, 16, ty + 64, w - 32, 50, 17, Theme.Cream, maxLines: 2));
        }
        var extras = new List<string>();
        if (plot.Botrytis) extras.Add(Lang.T("Edelfäule", "Noble rot"));
        if (plot.IceWineGamble) extras.Add($"{Lang.T("Eiswein-Wette", "Eiswein gamble")} {Js.Round(Vineyard.IceWineChance(state, session.Player.RegionId) * 100)} %");
        if (plot.HailNet) extras.Add(Lang.T("Hagelnetz", "Hail net"));
        if (plot.Irrigation) extras.Add(Lang.T("Bewässerung", "Irrigation"));
        if (extras.Count > 0) tile.With(Kit.Text(string.Join(" · ", extras), 16, ty + 140, w - 32, 24, 16, Theme.Gold));

        var id = plot.Id;
        tile.Tap(() => session.Nav.Select(PlotKey, id));
        return tile;
    }

    /// <summary>Charakter des Anbaugebiets in Kurzform (RegionProfile des Originals)</summary>
    static SkiaControl RegionPanel(Player player)
    {
        var region = Regions.All[player.RegionId];
        var character = region.Character;
        string[] prestige = Lang.En ? ["Insider tip", "well known", "renowned", "world famous"] : ["Geheimtipp", "bekannt", "renommiert", "weltberühmt"];
        string[] tourism = Lang.En ? ["few visitors", "some visitors", "many visitors"] : ["kaum Ausflügler", "einige Ausflügler", "viele Ausflügler"];
        string Percent(double factor) => $"{Js.Round(factor * 100)} %";
        var panel = Kit.Glass(28, 818, 1202, 150);
        panel.With(Kit.Text(region.Name, 24, 12, 300, 36, 26, Theme.Gold, bold: true, serif: true));
        var nameWidth = 40 + region.Name.Length * 15.0;
        panel.With(Kit.Text(character.Profile, 24 + nameWidth, 19, 1154 - nameWidth, 28, 19, Theme.Cream));
        (string Title, string Note, int Tone)[] chips =
        [
            ($"{Lang.T("Steillagen", "Steep slopes")} {Percent(character.Slope)}", Lang.T("mehr Arbeit, mehr Güte", "more work, more quality"), 0),
            (prestige[(int)Math.Clamp(character.Prestige + 1, 0, 3)], Lang.T("Renommee hebt die Preise", "Renown lifts prices"), Math.Sign(character.Prestige)),
            (tourism[(int)Math.Clamp(character.Tourism, 0, 2)], Lang.T("Kundschaft ab Hof", "Cellar-door customers"), character.Tourism > 0 ? 1 : 0),
            ($"{Lang.T("Ertrag", "Yield")} {Percent(character.YieldFactor)}", Lang.T("Ertragsniveau im Gebiet", "Yield level in the region"), character.YieldFactor > 1 ? 1 : character.YieldFactor < 1 ? -1 : 0),
            ($"{Lang.T("Boden", "Land")} {Percent(character.LandPrice)}", Lang.T("Wert, Kredit, Auktionen", "Value, credit, auctions"), 0),
        ];
        for (var i = 0; i < chips.Length; i++)
        {
            var chip = chips[i];
            var color = chip.Tone > 0 ? Theme.GoodLight : chip.Tone < 0 ? Theme.Warn : Theme.Cream;
            panel.With(Kit.Box(24 + i * 232.5, 62, 224, 72, Theme.GlassLight, 14).With(
                Kit.Text(chip.Title, 14, 9, 196, 28, 19, color, bold: true),
                Kit.Text(chip.Note, 14, 39, 200, 24, 15, Theme.CreamSoft)));
        }
        return panel;
    }

    // ---- Inspektor ----

    static SkiaControl Inspector(GameSession session, Plot plot)
    {
        var state = session.State;
        var player = session.Player;
        var card = Kit.Card(1260, 20, 524, 948, 22);
        const double x = 32, w = 460;
        card.With(Kit.Text(plot.Name, x, 26, w - 124, 46, 32, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Paint(x + w - 116, 36, 116, 25, (c, pw, ph) => VineyardPaint.Stars(c, pw, ph, plot.Site)));

        var young = plot.Grape is not null && Rules.PlotAgeFactor(plot, state.Year) == 0;
        var subtitle = plot.Grape is null ? Lang.T("Brache – bereit für eine neue Rebsorte", "Fallow – ready for a new grape variety")
            : young ? Lang.T("Junganlage – trägt ab dem dritten Jahr", "Young vines – they bear from the third year")
            : plot.IceWineGamble ? Lang.T("Die Trauben hängen für Eiswein", "The grapes are hanging for Eiswein")
            : plot.HarvestedThisYear ? Lang.T("In diesem Jahr gelesen", "Harvested this year")
            : plot.Botrytis ? Lang.T("Edelfäule – das Mostgewicht steigt", "Noble rot – the must weight is rising")
            : plot.Oechsle > 0 ? Lang.T("Die Trauben reifen", "The grapes are ripening") : Lang.T("Die Reife hat noch nicht begonnen", "Ripening has not begun yet");
        card.With(Kit.Text(subtitle, x, 74, w, 28, 19, Theme.InkSoft));

        var equipment = new List<string>();
        if (plot.HailNet) equipment.Add(Lang.T("Hagelnetz", "Hail net"));
        if (plot.Irrigation) equipment.Add(Lang.T("Bewässerung", "Irrigation"));
        var equipmentText = equipment.Count > 0 ? string.Join(", ", equipment) : Lang.T("keine", "none");

        var y = 112.0;
        void Row(string label, string value, Color? color = null)
        {
            card.With(Kit.Row(label, value, x, y, w, color, 18));
            y += 30;
        }

        if (plot.Grape is not { } grapeId)
        {
            Row(Lang.T("Fläche", "Area"), Hectares(plot.Hectares));
            Row(Lang.T("Lage", "Site"), SiteWord(plot.Site));
            Row(Lang.T("Ausstattung", "Equipment"), equipmentText);
            card.With(Kit.Box(x, y + 6, w, 2, Theme.PaperLine));
            PlantSection(session, card, plot, y + 18);
            return card;
        }

        var grape = Grapes.All[grapeId];
        var age = state.Year - (plot.PlantedYear ?? state.Year);
        var ageFactor = Rules.PlotAgeFactor(plot, state.Year);
        var ageText = (age <= 0 ? Lang.T("frisch gepflanzt", "freshly planted") : age == 1 ? Lang.T("1 Jahr", "1 year") : Lang.En ? $"{age} years" : $"{age} Jahre") +
            (ageFactor <= 0 ? Lang.T(" · Junganlage", " · young vines") : ageFactor < 1 ? Lang.T(" · halber Ertrag", " · half yield") : Lang.T(" · voll im Ertrag", " · full yield"));
        var trend = TrendText(state, grapeId);
        Row(Lang.T("Sorte", "Variety"), $"{grape.Name} · {(grape.Color == GrapeColor.Rot ? Lang.T("rot", "red") : Lang.T("weiß", "white"))}");
        Row(Lang.T("Alter", "Age"), ageText);
        Row(Lang.T("Fläche", "Area"), Hectares(plot.Hectares));
        Row(Lang.T("Lage", "Site"), SiteWord(plot.Site));
        Row(Lang.T("Gesundheit", "Health"), $"{Js.Round(plot.Health * 100)} %" + (plot.CropFactor < 0.99 ? $" · {Lang.T("Ertrag", "Yield")} {Js.Round(plot.CropFactor * 100)} %" : ""),
            plot.Health < 0.7 || plot.CropFactor < 0.99 ? Theme.Alert : null);
        Row(Lang.T("Markttrend", "Market trend"), trend.Text, trend.Color);
        Row(Lang.T("Ausstattung", "Equipment"), equipmentText);
        card.With(Kit.Box(x, y + 6, w, 2, Theme.PaperLine));
        y += 18;

        // Reife: Skala mit den Schwellen der Qualitätsstufen
        var ripening = !young && !plot.HarvestedThisYear;
        card.With(Kit.Text(Lang.T("Reife", "Ripeness"), x, y, 200, 30, 21, Theme.Ink, bold: true));
        if (ripening)
        {
            card.With(Kit.Text(plot.Oechsle > 0 ? $"{Js.Round(plot.Oechsle)} °Oe" : "0 °Oe", x + 200, y - 4, w - 200, 36, 26, Theme.Wine, bold: true, serif: true, align: Align.End));
            card.With(Kit.Paint(x, y + 40, w, 16, (c, pw, ph) => VineyardPaint.Gauge(c, pw, ph, plot.Oechsle, grape.Color, false)));
            (QualityLevel Level, string Short)[] ticks =
            [
                (QualityLevel.Qualitaetswein, "QbA"), (QualityLevel.Spaetlese, "Spl"), (QualityLevel.Auslese, "Asl"),
                (QualityLevel.Beerenauslese, "BA"), (QualityLevel.Trockenbeerenauslese, "TBA"),
            ];
            foreach (var tick in ticks)
            {
                var at = x + w * VineyardPaint.ScalePosition(Quality.QualityInfo(tick.Level).MinOechsle);
                card.With(Kit.Text(tick.Short, Math.Min(at - 30, x + w - 60), y + 59, 60, 22, 15, Theme.InkSoft, align: Align.Center));
            }
            string reach;
            if (plot.Oechsle <= 0) reach = Lang.T("Das Mostgewicht steigt ab dem Sommer, Lese ab August.", "The must weight rises from summer, harvest from August.");
            else
            {
                var level = Quality.LevelForOechsle(plot.Oechsle);
                var next = Quality.QualityLevels.FirstOrDefault(q => !q.Special && q.MinOechsle > plot.Oechsle);
                reach = Lang.En ? $"Enough for {level.Name}" + (next is null ? "" : $" · {Math.Max(1, Math.Ceiling(next.MinOechsle - plot.Oechsle))} °Oe more to {next.Name}")
                    : $"Reicht für {level.Name}" + (next is null ? "" : $" · noch {Math.Max(1, Math.Ceiling(next.MinOechsle - plot.Oechsle))} °Oe bis {next.Name}");
            }
            card.With(Kit.Text(reach, x, y + 84, w, 26, 17, Theme.InkSoft));
            y += 116;
        }
        else
        {
            card.With(Kit.Text(young ? Lang.T("Junge Reben tragen noch nicht. Im dritten Jahr gibt es die halbe, danach die volle Ernte.", "Young vines don't bear yet. In the third year you get half a harvest, then the full one.")
                : Lang.T("Diese Parzelle ist für dieses Jahr gelesen. Im nächsten Sommer reifen neue Trauben.", "This plot has been harvested for this year. New grapes ripen next summer."), x, y + 34, w, 52, 17, Theme.InkSoft, maxLines: 2));
            y += 92;
        }

        // Ertragsvorschau
        var harvest = new GameAction.Harvest(plot.Id);
        var harvestProblem = session.Problem(harvest);
        HarvestPreview? preview = ripening ? Game.HarvestPreview(state, player, plot) : null;
        var previewBox = Kit.Box(x, y, w, 62, Theme.C("#40d9c7a3"), 12);
        if (preview is not null && harvestProblem is null)
        {
            previewBox.With(Kit.Text($"{Lang.T("Ertragsvorschau", "Yield preview")}: ~{Util.FormatNumber(preview.Hectoliters)} hl {Quality.QualityInfo(preview.Level).Name}", 14, 6, w - 28, 26, 18, Theme.Ink, bold: true));
            var early = state.Month is 8 or 9 ? Lang.T(" · früh: weniger Menge", " · early: smaller quantity") : "";
            previewBox.With(Kit.Text(Lang.En ? $"Quality {Util.FormatNumber(preview.Quality)} · harvest cost {Util.FormatEuro(preview.Cost)}{early}" : $"Güte {Util.FormatNumber(preview.Quality)} · Lesekosten {Util.FormatEuro(preview.Cost)}{early}", 14, 32, w - 28, 24, 16, Theme.InkSoft));
        }
        else if (preview is not null && plot.Oechsle > 0)
        {
            previewBox.With(Kit.Text(Lang.En ? $"Yield preview: quality {Util.FormatNumber(preview.Quality)} as of today" : $"Ertragsvorschau: Güte {Util.FormatNumber(preview.Quality)} nach heutigem Stand", 14, 6, w - 28, 26, 18, Theme.Ink, bold: true));
            previewBox.With(Kit.Text(Lang.En ? $"Harvest cost {Util.FormatEuro(preview.Cost)} · harvesting runs from August to December" : $"Lesekosten {Util.FormatEuro(preview.Cost)} · gelesen wird von August bis Dezember", 14, 32, w - 28, 24, 16, Theme.InkSoft));
        }
        else
        {
            previewBox.With(Kit.Text(Lang.T("Ertragsvorschau", "Yield preview"), 14, 6, w - 28, 26, 18, Theme.Ink, bold: true));
            previewBox.With(Kit.Text(ripening ? Lang.T("erscheint, sobald die Trauben reifen", "appears once the grapes ripen") : young ? Lang.T("erst wenn die Reben tragen", "only once the vines bear") : Lang.T("wieder im nächsten Jahr", "again next year"), 14, 32, w - 28, 24, 16, Theme.InkSoft));
        }
        card.With(previewBox);
        y += 72;

        // Rückfragen ersetzen die Aktionen
        var mode = session.Nav.Selected(ModeKey);
        if (mode == $"clear:{plot.Id}")
        {
            var cost = plot.Hectares * Economy.ClearingCostPerHa;
            Confirm(session, card, y,
                Lang.En ? $"Clear {grape.Name} on site {plot.Name}? This costs {Util.FormatEuro(cost)}; you plant a new variety in March or April – it only bears from the third year."
                    : $"{grape.Name} in der Lage {plot.Name} roden? Das kostet {Util.FormatEuro(cost)}; eine neue Rebsorte pflanzt du im März oder April – sie trägt erst ab dem dritten Jahr.",
                Lang.T("Ja, roden", "Yes, clear it"), new GameAction.ClearPlot(plot.Id));
            return card;
        }
        if (mode == $"sell:{plot.Id}")
        {
            SellConfirm(session, card, plot, y);
            return card;
        }

        var red = grape.Color == GrapeColor.Rot;
        var step = red ? 71.0 : 88.0;
        var height = red ? 63.0 : 78.0;
        var harvestSub = preview is not null && harvestProblem is null
            ? Lang.En ? $"~{Util.FormatNumber(preview.Hectoliters)} hl {Quality.QualityInfo(preview.Level).Name}, quality {Util.FormatNumber(preview.Quality)} · costs {Util.FormatEuro(preview.Cost)}" : $"~{Util.FormatNumber(preview.Hectoliters)} hl {Quality.QualityInfo(preview.Level).Name}, Güte {Util.FormatNumber(preview.Quality)} · kostet {Util.FormatEuro(preview.Cost)}"
            : null;
        card.With(Kit.Button(Lang.T("Lesen", "Harvest"), x, y, w, height, () => session.Run(harvest), ButtonKind.Primary, harvestSub, harvestProblem, 20));
        y += step;
        if (red)
        {
            var rose = new GameAction.Harvest(plot.Id, true);
            var roseTrend = state.Trends.TryGetValue(TrendKey.Rose, out var t) ? $" · {Lang.T("Markt", "Market")} {Js.Round(t.Value * 100)} %" : "";
            card.With(Kit.Button(Lang.T("Als Rosé lesen", "Harvest as rosé"), x, y, w, height, () => session.Run(rose), ButtonKind.Secondary,
                Lang.En ? $"pressed light · ready to drink in 3 months{roseTrend}" : $"hell gekeltert · in 3 Monaten trinkfertig{roseTrend}", session.Problem(rose), 20));
            y += step;
        }
        var chance = Js.Round(Vineyard.IceWineChance(state, player.RegionId) * 100);
        var gamble = new GameAction.GambleIceWine(plot.Id, !plot.IceWineGamble);
        card.With(Kit.Button(plot.IceWineGamble ? Lang.T("Eiswein-Wette abbrechen", "Cancel Eiswein gamble") : Lang.T("Für Eiswein hängen lassen", "Leave hanging for Eiswein"), x, y, w, height, () => session.Run(gamble), ButtonKind.Secondary,
            plot.IceWineGamble ? (Lang.En ? $"Frost chance {chance} % · the grapes will be harvested normally again" : $"Frostchance {chance} % · die Trauben werden wieder normal gelesen") : (Lang.En ? $"Frost in December: {chance} % · otherwise the harvest is lost" : $"Frost im Dezember: {chance} % · sonst ist die Ernte verloren"),
            session.Problem(gamble), 20));
        y += step;
        var clear = new GameAction.ClearPlot(plot.Id);
        var id = plot.Id;
        card.With(Kit.Button(Lang.T("Roden", "Clear vines"), x, y, w, height, () => session.Nav.Select(ModeKey, $"clear:{id}"), ButtonKind.Secondary,
            Lang.En ? $"costs {Util.FormatEuro(plot.Hectares * Economy.ClearingCostPerHa)} · makes room for another variety" : $"kostet {Util.FormatEuro(plot.Hectares * Economy.ClearingCostPerHa)} · macht Platz für eine andere Sorte", session.Problem(clear), 20));
        y += step;
        SellButton(session, card, plot, y, height);
        return card;
    }

    /// <summary>
    /// Trennen von einer Parzelle: sofort zum festen Preis verkaufen oder – mit den erweiterten Regeln – zur nächsten
    /// Versteigerung geben, wo sie mehr bringen kann, aber auch liegen bleiben.
    /// </summary>
    static void SellButton(GameSession session, SkiaLayout card, Plot plot, double y, double height)
    {
        var id = plot.Id;
        var state = session.State;
        var player = session.Player;
        var price = Game.PlotSalePrice(player, plot, state.Year);
        if (!RulesV2.On(state))
        {
            card.With(Kit.Button(Lang.T("Verkaufen", "Sell"), 32, y, 460, height, () => session.Nav.Select(ModeKey, $"sell:{id}"), ButtonKind.Secondary,
                Lang.En ? $"brings {Util.FormatEuro(price)} · 90 % of the estimate" : $"bringt {Util.FormatEuro(price)} · 90 % des Schätzwerts", session.Problem(new GameAction.SellPlot(plot.Id)), 20));
            return;
        }
        var start = Util.FormatEuro(Auction.OfferStartPrice(player, plot, state.Year));
        if (plot.ForAuction)
        {
            var withdraw = new GameAction.WithdrawPlot(id);
            card.With(Kit.Button(Lang.T("Versteigerung zurückziehen", "Withdraw from auction"), 32, y, 460, height, () => session.Run(withdraw), ButtonKind.Secondary,
                Lang.En ? $"entered in the next auction · opening {start}" : $"angemeldet für die nächste Versteigerung · Ausruf {start}", session.Problem(withdraw), 20));
            return;
        }
        var sell = session.Problem(new GameAction.SellPlot(id));
        var offer = new GameAction.OfferPlot(id);
        var blocked = session.Problem(offer);
        card.With(Kit.Button(Lang.T("Verkaufen", "Sell"), 32, y, 224, height, () => session.Nav.Select(ModeKey, $"sell:{id}"), ButtonKind.Secondary,
            Lang.En ? $"{Util.FormatEuro(price)} right away" : $"{Util.FormatEuro(price)} sofort", sell, 20, quiet: sell is not null));
        card.With(Kit.Button(Lang.T("Versteigern", "Auction"), 268, y, 224, height, () => session.Run(offer), ButtonKind.Secondary,
            Lang.En ? $"opening {start}" : $"Ausruf {start}", blocked, 20, quiet: blocked is not null));
    }

    static void SellConfirm(GameSession session, SkiaLayout card, Plot plot, double y)
    {
        var price = Game.PlotSalePrice(session.Player, plot, session.State.Year);
        Confirm(session, card, y,
            Lang.En ? $"Sell the site {plot.Name} for {Util.FormatEuro(price)}? The vineyard is gone afterwards, grapes on the vine included."
                : $"Die Lage {plot.Name} für {Util.FormatEuro(price)} verkaufen? Der Weinberg ist danach weg, Trauben am Stock inklusive.",
            Lang.T("Ja, verkaufen", "Yes, sell it"), new GameAction.SellPlot(plot.Id));
    }

    static void Confirm(GameSession session, SkiaLayout card, double y, string question, string yes, GameAction action)
    {
        const double x = 32, w = 460;
        var box = Kit.Box(x, y, w, 250, Theme.C("#1f8e2440"), 16, Theme.Wine, 2.5);
        box.With(Kit.Text(Lang.T("Bist du sicher?", "Are you sure?"), 20, 14, w - 40, 32, 23, Theme.Wine, bold: true, serif: true));
        box.With(Kit.Text(question, 20, 50, w - 40, 104, 18, Theme.Ink, maxLines: 4));
        box.With(Kit.Button(yes, 20, 170, 206, 62, () =>
        {
            session.Nav.Select(ModeKey, null);
            session.Run(action);
        }, ButtonKind.Primary, problem: session.Problem(action), fontSize: 20, align: Align.Center));
        box.With(Kit.Button(Lang.T("Abbrechen", "Cancel"), 234, 170, 206, 62, () => session.Nav.Select(ModeKey, null), ButtonKind.Secondary, fontSize: 20, align: Align.Center));
        card.With(box);
    }

    /// <summary>Brache: Rebsorte aus einem blätterbaren Raster wählen und pflanzen</summary>
    static void PlantSection(GameSession session, SkiaLayout card, Plot plot, double y)
    {
        var state = session.State;
        var region = Regions.All[session.Player.RegionId];
        const double x = 32, w = 460;
        if (session.Nav.Selected(ModeKey) == $"sell:{plot.Id}")
        {
            SellConfirm(session, card, plot, y);
            return;
        }
        // Die im Gebiet am besten geeigneten Sorten stehen vorn
        var grapes = Grapes.All.Values.OrderByDescending(g => region.Suitability[g.Id]).ToList();
        var chosen = Enum.TryParse<GrapeId>(session.Nav.Selected(GrapeKey), out var stored) ? stored : grapes[0].Id;
        const int perPage = 6;
        var pages = (int)Math.Ceiling(grapes.Count / (double)perPage);
        var page = session.Nav.Page("weinberg.grapes", pages);
        card.With(Kit.Text(Lang.T("Rebsorte wählen", "Choose grape variety"), x, y + 4, 240, 30, 21, Theme.Ink, bold: true));
        card.With(Kit.Pager(x + 250, y, 210, page, pages, p => session.Nav.SetPage("weinberg.grapes", Math.Clamp(p, 0, pages - 1)), dark: false));
        y += 46;
        var shown = grapes.Skip(page * perPage).Take(perPage).ToList();
        for (var i = 0; i < shown.Count; i++)
        {
            var grape = shown[i];
            var on = grape.Id == chosen;
            var suit = SuitabilityText(region.Suitability[grape.Id]);
            var trend = TrendText(state, grape.Id);
            var cell = on
                ? Kit.Box(x + (i % 2) * 236, y + (i / 2) * 110, 224, 102, Theme.Paper, 14, Theme.Wine, 3.5)
                : Kit.Box(x + (i % 2) * 236, y + (i / 2) * 110, 224, 102, Theme.C("#4dd9c7a3"), 14);
            cell.With(Kit.Text(grape.Name, 14, 8, 200, 28, 19, on ? Theme.Wine : Theme.Ink, bold: true));
            cell.With(Kit.Box(14, 45, 12, 12, suit.Color, 6));
            cell.With(Kit.Text(suit.Word, 34, 37, 180, 26, 17, suit.Color, bold: true));
            cell.With(Kit.Text(Util.FormatEuro(grape.PlantCostPerHa * plot.Hectares), 14, 66, 110, 26, 17, Theme.Ink));
            cell.With(Kit.Text(trend.Text, 110, 66, 100, 26, 17, trend.Color, align: Align.End));
            var id = grape.Id;
            cell.Tap(() => session.Nav.Select(GrapeKey, id.ToString()));
            card.With(cell);
        }
        y += 3 * 110 + 4;

        var pick = Grapes.All[chosen];
        var pickSuit = SuitabilityText(region.Suitability[chosen]);
        var pickTrend = TrendText(state, chosen);
        card.With(Kit.Text($"{pick.Name}: {(pick.Color == GrapeColor.Rot ? Lang.T("rot", "red") : Lang.T("weiß", "white"))} · {Util.FormatNumber(pick.YieldPerHa)} hl/ha · {Lang.T("Güte-Potenzial", "quality potential")} {Util.FormatNumber(pick.QualityPotential)}/10",
            x, y, w, 26, 17, Theme.Ink, bold: true));
        card.With(Kit.Text(Lang.En ? $"In {region.Name}: {pickSuit.Word} · market {pickTrend.Text} · new vines only bear from the third year." : $"In {region.Name} {pickSuit.Word} · Markt {pickTrend.Text} · neue Reben tragen erst ab dem dritten Jahr.", x, y + 26, w, 48, 16, Theme.InkSoft, maxLines: 2));
        y += 82;
        var plant = new GameAction.Plant(plot.Id, chosen);
        card.With(Kit.Button(Lang.En ? $"Plant {GrapeName(chosen)}" : $"{GrapeName(chosen)} pflanzen", x, y, w, 78, () => session.Run(plant), ButtonKind.Primary,
            Lang.En ? $"costs {Util.FormatEuro(pick.PlantCostPerHa * plot.Hectares)} for {Hectares(plot.Hectares)}" : $"kostet {Util.FormatEuro(pick.PlantCostPerHa * plot.Hectares)} für {Hectares(plot.Hectares)}", session.Problem(plant)));
        SellButton(session, card, plot, y + 88, 66);
    }
}
