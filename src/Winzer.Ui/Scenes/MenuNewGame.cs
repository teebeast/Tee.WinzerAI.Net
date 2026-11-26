using DrawnUi.Draw;
using Winzer.Engine;
using Winzer.Engine.Data;

namespace Winzer.Ui.Scenes;

/// <summary>Neues freies Spiel in drei Schritten: Spielart und Ziel, Gebiet, Mitspieler und Optionen.</summary>
internal static class MenuNewGame
{
    // Als Eigenschaften, damit die Sprache beim Lesen gilt
    static (int Value, string Icon, string Name, string Text)[] Lengths =>
    [
        (300, "⏱️", Lang.T("Kurz", "Short"), Lang.T("300 Punkte", "300 points")),
        (600, "🍷", "Normal", Lang.T("600 Punkte", "600 points")),
        (1000, "🏆", Lang.T("Klassisch", "Classic"), Lang.T("1000 Punkte", "1000 points")),
        (0, "∞", Lang.T("Endlos", "Endless"), Lang.T("ohne Punkteziel", "no points goal")),
    ];

    static string[] Prestige => [Lang.T("Geheimtipp", "insider tip"), Lang.T("bekannt", "known"), Lang.T("renommiert", "renowned"), Lang.T("weltberühmt", "world-famous")];
    static string[] Tourism => [Lang.T("kaum Ausflügler", "hardly any visitors"), Lang.T("einige Ausflügler", "some visitors"), Lang.T("viele Ausflügler", "many visitors")];

    static readonly Color WarmthColor = Theme.C("#e0884c");
    static readonly Color FrostColor = Theme.C("#6f9fc8");
    static readonly Color WetColor = Theme.C("#4d8f8a");

    static string Percent(double factor) => $"{Js.Round(factor * 100)} %";

    static string LengthText(MenuSetup setup)
    {
        if (!setup.Endless) return Lang.En ? $"{Lengths.First(l => l.Value == setup.Length).Name} · {setup.Length} points" : $"{Lengths.First(l => l.Value == setup.Length).Name} · {setup.Length} Punkte";
        return MenuSetup.EndlessYears[setup.EndlessStep] is { } years ? (Lang.En ? $"Endless · {years} years" : $"Endlos · {years} Jahre") : Lang.T("Endlos · ohne Zeitlimit", "Endless · no time limit");
    }

    // ---- Schritt 1: Spielart und Ziel ----

    public static void ModePage(SkiaLayout layer, AppHost app, MenuSetup setup)
    {
        MenuUi.Header(layer, Lang.T("Neues Spiel", "New Game"), Lang.T("Freies Spiel: Gebiet, Gegner und Spiellänge frei wählbar", "Free game: choose region, rivals and game length"));
        layer.With(MenuUi.StepBar(app, 1));

        var card = Kit.Card(110, 190, 1130, 740, 22);
        card.With(Kit.Text(Lang.T("Spiellänge", "Game length"), 40, 26, 600, 44, 32, Theme.Ink, bold: true, serif: true));
        for (var i = 0; i < Lengths.Length; i++)
        {
            var option = Lengths[i];
            var on = setup.Length == option.Value;
            var tile = MenuUi.Option(40 + i * 267, 86, 250, 196, on, () => { setup.Length = option.Value; app.Notify(); });
            tile.With(Kit.Text(option.Icon, 0, 22, 250, 64, 46, Theme.Ink, bold: true, serif: true, align: Align.Center));
            tile.With(Kit.Text(option.Name, 0, 98, 250, 40, 29, Theme.Ink, bold: true, serif: true, align: Align.Center));
            tile.With(Kit.Text(option.Text, 0, 142, 250, 30, 21, Theme.InkSoft, align: Align.Center));
            card.With(tile);
        }

        if (setup.Endless)
        {
            var years = MenuSetup.EndlessYears[setup.EndlessStep];
            card.With(Kit.Text(Lang.T("Spieldauer", "Duration"), 40, 304, 300, 32, 22, Theme.Ink, bold: true));
            for (var i = 0; i < MenuSetup.EndlessYears.Length; i++)
            {
                var index = i;
                var on = setup.EndlessStep == i;
                var label = MenuSetup.EndlessYears[i] is { } value ? (Lang.En ? $"{value} years" : $"{value} Jahre") : Lang.T("∞ ohne Limit", "∞ no limit");
                card.With(Kit.Box(40 + i * 213, 342, 198, 56, on ? Theme.Wine : Theme.PaperLine, 28)
                    .With(Kit.Text(label, 0, 13, 198, 30, 21, on ? Theme.Paper : Theme.Ink, bold: true, align: Align.Center))
                    .Tap(() => { setup.EndlessStep = index; app.Notify(); }));
            }
            card.With(Kit.Text(years is not null
                    ? (Lang.En ? $"The game ends after {years} years; whoever has the most points then wins." : $"Nach {years} Jahren endet das Spiel, wer dann die meisten Punkte hat, gewinnt.")
                    : Lang.T("Das Endlosspiel läuft, bis du es beendest oder pleitegehst. Die Rangliste zeigt jederzeit den Stand.", "The endless game runs until you quit or go bankrupt. The standings show the score at any time."),
                40, 410, 1050, 30, 20, Theme.InkSoft));
        }
        else
        {
            card.With(Kit.Box(40, 312, 1050, 108, Theme.C("#40d9c7a3"), 16));
            card.With(Kit.Text(Lang.En ? $"Goal: {setup.Length} points" : $"Ziel: {setup.Length} Punkte", 68, 328, 990, 38, 27, Theme.Wine, bold: true, serif: true));
            card.With(Kit.Text(Lang.En ? $"Whoever reaches {setup.Length} points first wins the game. No time limit." : $"Wer zuerst {setup.Length} Punkte erreicht, gewinnt die Partie. Ohne Zeitlimit.", 68, 370, 990, 30, 21, Theme.InkSoft));
        }

        card.With(Kit.Box(40, 452, 1050, 2, Theme.PaperLine));
        card.With(Kit.Text(Lang.T("Schwierigkeitsgrad", "Difficulty"), 40, 470, 600, 44, 32, Theme.Ink, bold: true, serif: true));
        var d = 0;
        foreach (var (id, info) in Economy.Difficulties)
        {
            var on = setup.Difficulty == id;
            var tile = MenuUi.Option(40 + d * 357, 530, 336, 104, on, () => { setup.Difficulty = id; app.Notify(); });
            tile.With(Kit.Text(info.Icon, 18, 24, 70, 56, 40, Theme.Ink, align: Align.Center));
            tile.With(Kit.Text(info.Name, 100, 16, 220, 40, 28, Theme.Ink, bold: true, serif: true));
            tile.With(Kit.Text(Util.FormatEuro(info.StartMoney) + Lang.T(" Startkapital", " starting capital"), 100, 58, 226, 28, 19, Theme.InkSoft));
            card.With(tile);
            d++;
        }
        card.With(Kit.Text(Lang.En ? $"{Economy.Difficulties[setup.Difficulty].Description}. The computer winemakers always play at the same strength." : $"{Economy.Difficulties[setup.Difficulty].Description}. Die Computergegner spielen immer gleich stark.",
            40, 654, 1050, 60, 21, Theme.InkSoft, maxLines: 2));

        layer.With(card, Summary(setup));
        MenuUi.Footer(layer, Lang.T("Hauptmenü", "Main menu"), () => MainMenu.Go(app, MainMenu.Home), Lang.T("Weiter: Gebiet wählen  ›", "Next: choose region  ›"), () => MainMenu.Go(app, MainMenu.StepRegion));
    }

    /// <summary>Rechte Karte in Schritt 1: Was bisher eingestellt ist</summary>
    static SkiaControl Summary(MenuSetup setup)
    {
        var card = Kit.Card(1270, 190, 540, 740, 22);
        card.With(Kit.Box(0, 0, 540, 12, Theme.Wine, 6));
        card.With(Kit.Text(Lang.T("Deine Partie", "Your game"), 40, 34, 460, 44, 32, Theme.Ink, bold: true, serif: true));
        var info = Economy.Difficulties[setup.Difficulty];
        var region = Regions.All[setup.Seats[0].RegionId];
        var humans = setup.Seats.Count(s => s.Ai is null);
        var machines = setup.Seats.Count - humans;
        var y = 100.0;
        void Row(string label, string value, Color? color = null)
        {
            card.With(Kit.Row(label, value, 40, y, 460, color, 21));
            y += 44;
        }
        void Line()
        {
            card.With(Kit.Box(40, y + 2, 460, 2, Theme.PaperLine));
            y += 20;
        }
        string Change(double factor)
        {
            var percent = Js.Round((factor - 1) * 100);
            return percent == 0 ? Lang.T("unverändert", "unchanged") : $"{(percent > 0 ? "+" : "−")}{Math.Abs(percent)} %";
        }
        Row(Lang.T("Spiellänge", "Game length"), LengthText(setup));
        Row(Lang.T("Schwierigkeit", "Difficulty"), info.Name);
        Row(Lang.T("Startkapital", "Starting capital"), Util.FormatEuro(info.StartMoney));
        Row(Lang.T("Erlöse", "Revenue"), Change(info.PriceFactor), info.PriceFactor > 1 ? Theme.Good : info.PriceFactor < 1 ? Theme.Alert : null);
        Row(Lang.T("Kosten", "Costs"), Change(info.CostFactor), info.CostFactor < 1 ? Theme.Good : info.CostFactor > 1 ? Theme.Alert : null);
        Line();
        Row(Lang.T("Anbaugebiet", "Wine region"), region.Name);
        Row(Lang.T("Klimawandel", "Climate change"), setup.ClimateChange ? Lang.T("an", "on") : Lang.T("aus", "off"));
        Row(Lang.T("Zufallsereignisse", "Random events"), setup.RandomEvents ? Lang.T("an", "on") : Lang.T("aus", "off"));
        Row("Seed", setup.Seed.ToString());
        Line();
        card.With(Kit.Text(Lang.En ? $"Estates · {humans} {(humans == 1 ? "human" : "humans")}, {machines} AI" : $"Weingüter · {humans} {(humans == 1 ? "Mensch" : "Menschen")}, {machines} KI", 40, y, 460, 30, 21, Theme.InkSoft));
        y += 40;
        foreach (var seat in setup.Seats)
        {
            card.With(Kit.Box(40, y + 9, 12, 12, seat.Ai is null ? Theme.Wine : Theme.GoldDark, 6));
            card.With(Kit.Text(seat.Name, 64, y, 250, 30, 20, Theme.Ink, bold: true));
            card.With(Kit.Text(seat.Ai is { } ai ? (Lang.En ? $"AI: {Ai.AiProfiles[ai].Name}" : $"KI: {Ai.AiProfiles[ai].Name}") : Lang.T("Mensch", "Human"), 300, y + 1, 200, 28, 18, Theme.InkSoft, align: Align.End));
            y += 36;
        }
        return card;
    }

    // ---- Schritt 2: Gebiet wählen ----

    public static void RegionPage(SkiaLayout layer, AppHost app, MenuSetup setup)
    {
        MenuUi.Header(layer, Lang.T("Gebiet wählen", "Choose region"), Lang.T("Hier liegt dein Weingut – die Mitspieler ziehen mit.", "This is where your estate lies – the other players follow."));
        layer.With(MenuUi.StepBar(app, 2));

        var chosen = setup.Seats[0].RegionId;
        const double cardWidth = 216, cardHeight = 232, pitchX = 231, pitchY = 246;
        var index = 0;
        foreach (var region in Regions.All.Values)
        {
            var id = region.Id;
            var on = id == chosen;
            var x = MenuUi.Left + index % 5 * pitchX;
            var y = 190 + index / 5 * pitchY;
            var card = Kit.Box(x, y, cardWidth, cardHeight, radius: 16, gradient: Kit.Vertical(Theme.Paper, Theme.PaperDark),
                stroke: on ? Theme.Gold : null, strokeWidth: 6, shadow: true);
            if (on) card.With(Kit.Box(0, 0, cardWidth, 50, radius: 0, gradient: Kit.Vertical(Theme.Wine, Theme.WineDark)));
            card.With(Kit.Text(region.Name, 16, region.Name.Length > 14 ? 15 : 10, cardWidth - 28, 32, region.Name.Length > 14 ? 18 : 23,
                on ? Theme.C("#fff6e6") : Theme.Ink, bold: true, serif: true));
            MiniMeter(card, Lang.T("Wärme", "Warmth"), region.Warmth, WarmthColor, 62);
            MiniMeter(card, "Frost", region.FrostRisk, FrostColor, 90);
            MiniMeter(card, Lang.T("Nässe", "Damp"), region.Wetness, WetColor, 118);
            card.With(Kit.Box(16, 150, cardWidth - 32, 2, Theme.PaperLine));
            card.With(Kit.Text(Grapes.All[region.StartGrape].Name, 16, 160, cardWidth - 32, 28, 19, Theme.Ink, bold: true));
            card.With(Kit.Text(Prestige[(int)region.Character.Prestige + 1], 16, 190, cardWidth - 32, 24, 17,
                region.Character.Prestige > 0 ? Theme.Good : region.Character.Prestige < 0 ? Theme.Alert : Theme.InkSoft));
            card.Tap(() => { setup.SetHomeRegion(id); app.Notify(); });
            layer.With(card);
            index++;
        }

        // Die beiden freien Plätze des Rasters erklären die Balken
        var legend = Kit.Glass(MenuUi.Left + 3 * pitchX, 190 + 2 * pitchY, cardWidth + pitchX, cardHeight, 16, shadow: false);
        (string Name, string Text, Color Color)[] entries =
        [
            (Lang.T("Wärme", "Warmth"), Lang.T("beschleunigt die Reife", "speeds up ripening"), WarmthColor),
            ("Frost", Lang.T("Spätfrost, aber auch Chance auf Eiswein", "late frost, but a chance for Eiswein"), FrostColor),
            (Lang.T("Nässe", "Damp"), Lang.T("Krankheitsdruck durch Nässe", "disease pressure from damp"), WetColor),
        ];
        for (var i = 0; i < entries.Length; i++)
        {
            legend.With(Kit.Box(24, 30 + i * 46, 16, 16, entries[i].Color, 8));
            legend.With(Kit.Text(entries[i].Name, 52, 22 + i * 46, 80, 28, 19, Theme.Cream, bold: true));
            legend.With(Kit.Text(entries[i].Text, 124, 23 + i * 46, 310, 28, 17, Theme.CreamSoft));
        }
        legend.With(Kit.Text(Lang.T("Auf jeder Karte außerdem: Startsorte und Renommee. Tippe ein Gebiet an, um es zu wählen.", "Each card also shows the starting variety and reputation. Tap a region to choose it."), 24, 162, 400, 56, 17, Theme.CreamSoft, maxLines: 2));
        layer.With(legend);

        layer.With(RegionDetail(Regions.All[chosen]));
        MenuUi.Footer(layer, Lang.T("Zurück", "Back"), () => MainMenu.Go(app, MainMenu.StepMode), Lang.T("Weiter: Mitspieler  ›", "Next: players  ›"), () => MainMenu.Go(app, MainMenu.StepPlayers));
    }

    static void MiniMeter(SkiaShape card, string label, double value, Color color, double y)
    {
        card.With(Kit.Text(label, 16, y, 66, 24, 17, Theme.InkSoft));
        card.With(MenuUi.Meter(84, y + 7, 116, 11, value / 10, color));
    }

    static SkiaControl RegionDetail(Region region)
    {
        var character = region.Character;
        var card = Kit.Card(1280, 190, 530, 724, 22);
        card.With(Kit.Box(0, 0, 530, 12, Theme.Wine, 6));
        card.With(Kit.Text(region.Name, 40, 28, 450, 54, region.Name.Length > 14 ? 36 : 42, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(character.Profile, 40, 88, 450, 84, 21, Theme.Ink, maxLines: 3));

        (string Name, double Value, string Text, Color Color)[] meters =
        [
            (Lang.T("Wärme", "Warmth"), region.Warmth, Lang.T("beschleunigt die Reife", "speeds up ripening"), WarmthColor),
            ("Frost", region.FrostRisk, Lang.T("Spätfrost, aber Chance auf Eiswein", "late frost, but a chance for Eiswein"), FrostColor),
            (Lang.T("Nässe", "Damp"), region.Wetness, Lang.T("Krankheitsdruck", "disease pressure"), WetColor),
        ];
        for (var i = 0; i < meters.Length; i++)
        {
            var y = 184 + i * 56;
            card.With(Kit.Text(meters[i].Name, 40, y, 90, 28, 20, Theme.Ink, bold: true));
            card.With(Kit.Text(meters[i].Text, 130, y + 3, 290, 26, 17, Theme.InkSoft));
            card.With(Kit.Text($"{Util.JsNumber(meters[i].Value)} / 10", 400, y, 90, 28, 20, Theme.Ink, bold: true, align: Align.End));
            card.With(MenuUi.Meter(40, y + 32, 450, 12, meters[i].Value / 10, meters[i].Color));
        }

        card.With(Kit.Box(40, 356, 450, 2, Theme.PaperLine));
        var top = 366.0;
        void Fact(string label, string value, string hint, Color? color = null)
        {
            card.With(Kit.Text(label, 40, top, 200, 28, 20, Theme.Ink, bold: true));
            card.With(Kit.Text(value, 200, top, 290, 28, 20, color ?? Theme.Ink, bold: true, align: Align.End));
            card.With(Kit.Text(hint, 40, top + 25, 450, 22, 15.5, Theme.InkSoft));
            top += 49;
        }
        Fact(Lang.T("Steillagen", "Steep slopes"), Percent(character.Slope), Lang.T("teurere Handarbeit bei Pflege und Lese, dafür mehr Güte", "costlier handwork in care and harvest, but higher quality"));
        Fact(Lang.T("Renommee", "Prestige"), Prestige[(int)character.Prestige + 1], Lang.T("wirkt auf die Flaschenpreise", "affects bottle prices"),
            character.Prestige > 0 ? Theme.Good : character.Prestige < 0 ? Theme.Alert : null);
        Fact(Lang.T("Weintourismus", "Wine tourism"), Tourism[(int)character.Tourism], Lang.T("mehr Kundschaft ab Hof", "more cellar-door customers"), character.Tourism > 0 ? Theme.Good : null);
        Fact(Lang.T("Ertrag", "Yield"), Percent(character.YieldFactor), Lang.T("Ertragsniveau der Weinberge", "yield level of the vineyards"),
            character.YieldFactor > 1 ? Theme.Good : character.YieldFactor < 1 ? Theme.Alert : null);
        Fact(Lang.T("Bodenpreis", "Land price"), Percent(character.LandPrice), Lang.T("Wert der Weinberge, Kreditrahmen, Versteigerungen", "value of vineyards, loan limit, auctions"));

        card.With(Kit.Box(40, top + 4, 450, 2, Theme.PaperLine));
        card.With(Kit.Row(Lang.T("Startsorte", "Starting variety"), Grapes.All[region.StartGrape].Name, 40, top + 14, 450, Theme.Wine, 20));
        var best = region.Suitability.Where(s => s.Value >= 2).Select(s => Grapes.All[s.Key].Name.Replace(" (PIWI)", "")).ToList();
        if (best.Count > 0)
        {
            card.With(Kit.Text(Lang.En ? $"Thrives especially well here: {string.Join(", ", best)}" : $"Gedeiht hier besonders gut: {string.Join(", ", best)}", 40, top + 48, 450, 50, 17, Theme.InkSoft, maxLines: 2));
        }
        return card;
    }

    // ---- Schritt 3: Mitspieler und Optionen ----

    public static void PlayersPage(SkiaLayout layer, AppHost app, MenuSetup setup)
    {
        MenuUi.Header(layer, Lang.T("Mitspieler und Optionen", "Players and options"), Lang.T("Bis zu vier Weingüter – Menschen spielen abwechselnd am selben Gerät.", "Up to four estates – humans take turns on the same device."));
        layer.With(MenuUi.StepBar(app, 3));
        void Change(Action action)
        {
            action();
            app.Notify();
        }

        var card = Kit.Card(110, 190, 1200, 724, 22);
        card.With(Kit.Text(Lang.T("Weingüter", "Estates"), 40, 24, 400, 44, 32, Theme.Ink, bold: true, serif: true));
        const double nameX = 104, nameW = 312, regionX = 428, regionW = 284, kindX = 724, kindW = 380, removeX = 1116;
        card.With(Kit.Text("Name", nameX + 4, 76, nameW, 24, 17, Theme.InkSoft));
        card.With(Kit.Text(Lang.T("Anbaugebiet", "Wine region"), regionX + 4, 76, regionW, 24, 17, Theme.InkSoft));
        card.With(Kit.Text(Lang.T("Gesteuert von", "Controlled by"), kindX + 4, 76, kindW, 24, 17, Theme.InkSoft));

        var regionIds = Regions.All.Keys.ToList();
        var kinds = new List<AiProfileId?> { null };
        kinds.AddRange(Ai.AiProfiles.Keys.Cast<AiProfileId?>());
        for (var i = 0; i < setup.Seats.Count; i++)
        {
            var seat = setup.Seats[i];
            var index = i;
            var y = 104 + i * 102;
            var human = seat.Ai is null;
            card.With(Kit.Box(40, y, 52, 52, human ? Theme.Wine : Theme.C("#8a7a5c"), 26)
                .With(Kit.Text((i + 1).ToString(), 0, 10, 52, 32, 24, Theme.Paper, bold: true, serif: true, align: Align.Center)));

            var names = new List<string>();
            if (seat.Ai is { } profile) names.Add(Ai.AiEstateNames[profile]);
            names.AddRange(MenuSetup.Names);
            void Name(int step) => Change(() => seat.Name = names[(Math.Max(0, names.IndexOf(seat.Name)) + step + names.Count) % names.Count]);
            card.With(MenuUi.Cycler(nameX, y, nameW, seat.Name, () => Name(-1), () => Name(1), bold: true));

            void Move(int step) => Change(() => seat.RegionId = regionIds[(regionIds.IndexOf(seat.RegionId) + step + regionIds.Count) % regionIds.Count]);
            card.With(MenuUi.Cycler(regionX, y, regionW, Regions.All[seat.RegionId].Name, () => Move(-1), () => Move(1)));

            void Kind(int step) => Change(() => setup.SetKind(seat, kinds[(kinds.IndexOf(seat.Ai) + step + kinds.Count) % kinds.Count]));
            var kindText = seat.Ai is { } ai ? (Lang.En ? $"AI: {Ai.AiProfiles[ai].Name}{(ai == AiProfileId.Genossenschaft ? " (easy)" : "")}" : $"KI: {Ai.AiProfiles[ai].Name}{(ai == AiProfileId.Genossenschaft ? " (leicht)" : "")}") : Lang.T("Mensch", "Human");
            card.With(MenuUi.Cycler(kindX, y, kindW, kindText, () => Kind(-1), () => Kind(1)));

            if (setup.Seats.Count > 1)
            {
                card.With(Kit.Box(removeX, y + 4, 44, 44, Theme.PaperLine, 11)
                    .With(Kit.Text("✕", 0, 9, 44, 28, 19, Theme.Ink, bold: true, align: Align.Center))
                    .Tap(() => Change(() => setup.Seats.RemoveAt(index))).Tip(Lang.T("Mitspieler entfernen", "Remove player")));
            }
            var note = seat.Ai is { } described ? Ai.AiProfiles[described].Description
                : setup.Seats.Count(s => s.Ai is null) > 1 ? Lang.T("Spielt im Wechsel mit den anderen Menschen am selben Gerät (Hot-Seat).", "Takes turns with the other humans on the same device (hot seat).") : Lang.T("Das bist du.", "That's you.");
            card.With(Kit.Text(note, nameX + 4, y + 58, 1050, 26, 17, Theme.InkSoft));
        }

        // Freie Plätze: der erste nimmt das nächste Weingut auf
        for (var i = setup.Seats.Count; i < 4; i++)
        {
            var y = 104 + i * 102;
            var first = i == setup.Seats.Count;
            var slot = Kit.Box(40, y, 1120, 84, Theme.C(first ? "#40d9c7a3" : "#24d9c7a3"), 14);
            slot.With(Kit.Box(0, 16, 52, 52, Theme.C("#59d9c7a3"), 26)
                .With(Kit.Text((i + 1).ToString(), 0, 10, 52, 32, 24, Theme.InkSoft, bold: true, serif: true, align: Align.Center)));
            if (first)
            {
                slot.With(Kit.Button(Lang.T("+ Weingut hinzufügen", "+ Add estate"), 64, 14, 320, 56, () => Change(setup.AddSeat), ButtonKind.Ghost, fontSize: 21, align: Align.Center));
                slot.With(Kit.Text(Lang.T("Ein weiterer Computergegner oder ein Mensch am selben Gerät", "Another computer winemaker or a human on the same device"), 404, 28, 700, 28, 19, Theme.InkSoft));
            }
            else
            {
                slot.With(Kit.Text(Lang.T("Freier Platz", "Free slot"), 68, 28, 400, 28, 19, Theme.InkSoft));
            }
            card.With(slot);
        }
        card.With(Kit.Box(40, 590, 1120, 2, Theme.PaperLine));
        card.With(Kit.Text(Lang.T("Mehrere Menschen spielen abwechselnd am selben Gerät (Hot-Seat). Versteigerungen gibt es nur unter Weingütern im selben Gebiet – und gegen auswärtige Investoren.", "Several humans take turns on the same device (hot seat). Auctions only take place between estates in the same region – and against outside investors."),
            40, 604, 1120, 58, 19, Theme.InkSoft, maxLines: 2));
        if (!setup.HasHuman)
        {
            card.With(Kit.Text(Lang.T("Mindestens ein Weingut muss von einem Menschen gespielt werden.", "At least one estate must be played by a human."), 40, 670, 1120, 30, 20, Theme.Alert, bold: true));
        }

        var world = Kit.Card(1340, 190, 470, 724, 22);
        world.With(Kit.Box(0, 0, 470, 12, Theme.Wine, 6));
        world.With(Kit.Text(Lang.T("Welt", "World"), 36, 30, 300, 44, 32, Theme.Ink, bold: true, serif: true));
        world.With(Kit.Text(Lang.T("🌡️ Klimawandel", "🌡️ Climate change"), 36, 94, 300, 34, 23, Theme.Ink, bold: true));
        world.With(MenuUi.Toggle(350, 90, setup.ClimateChange, () => Change(() => setup.ClimateChange = !setup.ClimateChange)));
        world.With(Kit.Text(Lang.T("Jedes Jahr etwas wärmer: frühere Reife, mehr Hitze, seltener Eiswein.", "A little warmer every year: earlier ripening, more heat, rarer Eiswein."), 36, 138, 398, 56, 18, Theme.InkSoft, maxLines: 2));
        world.With(Kit.Text(Lang.T("🎲 Zufallsereignisse", "🎲 Random events"), 36, 222, 300, 34, 23, Theme.Ink, bold: true));
        world.With(MenuUi.Toggle(350, 218, setup.RandomEvents, () => Change(() => setup.RandomEvents = !setup.RandomEvents)));
        world.With(Kit.Text(Lang.T("Glücksfälle und Rückschläge – von der Wandergruppe bis zum Kellerbrand.", "Strokes of luck and setbacks – from a group of hikers to a cellar fire."), 36, 266, 398, 56, 18, Theme.InkSoft, maxLines: 2));

        world.With(Kit.Box(36, 344, 398, 2, Theme.PaperLine));
        world.With(Kit.Text("Seed", 36, 362, 200, 36, 26, Theme.Ink, bold: true, serif: true));
        world.With(Kit.Text(setup.Seed.ToString(), 36, 404, 200, 50, 36, Theme.Wine, bold: true, serif: true));
        world.With(Kit.Button(Lang.T("neu würfeln", "re-roll"), 244, 398, 190, 56, () => Change(() => setup.Seed = Random.Shared.Next(1_000_000)), ButtonKind.Ghost, fontSize: 20, align: Align.Center));
        world.With(Kit.Text(Lang.T("Gleicher Seed, gleiche Einstellungen – gleiche Partie.", "Same seed, same settings – same game."), 36, 468, 398, 52, 18, Theme.InkSoft, maxLines: 2));

        world.With(Kit.Box(36, 536, 398, 2, Theme.PaperLine));
        world.With(Kit.Text(Lang.T("📜 Erweiterte Regeln", "📜 Extended rules"), 36, 558, 300, 34, 23, Theme.Ink, bold: true));
        world.With(MenuUi.Toggle(350, 554, setup.ExtendedRules, () => Change(() => setup.ExtendedRules = !setup.ExtendedRules)));
        world.With(Kit.Text(setup.ExtendedRules
            ? Lang.T("Preise selbst setzen, Jahresziele, Ambitionen, Gutsausbau, Lese in Halbmonaten, Prämierung mit Jury.", "Set your own prices, yearly goals, ambitions, estate upgrades, harvest in half-months, wine awards with a jury.")
            : Lang.T("Klassisch: die Regeln des ursprünglichen Spiels, ein Zug je Monat.", "Classic: the rules of the original game, one turn per month."), 36, 602, 398, 84, 18, Theme.InkSoft, maxLines: 3));

        layer.With(card, world);
        MenuUi.Footer(layer, Lang.T("Zurück", "Back"), () => MainMenu.Go(app, MainMenu.StepRegion), Lang.T("Partie beginnen", "Start game"),
            () => MainMenu.Start(app, setup, Game.NewGame(setup.Options())),
            nextProblem: setup.HasHuman ? null : Lang.T("Mindestens ein Weingut muss ein Mensch spielen", "At least one estate must be played by a human"), nextWidth: 520);
    }
}
