using DrawnUi.Draw;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

/// <summary>Weitere Seiten des Hauptmenüs: Spielstände, Tages-Challenge, Lizenzhinweis.</summary>
internal static class MenuPages
{
    // ---- Spielstände ----

    public static string SlotTitle(SaveSlot slot)
    {
        var title = string.Join(" & ", slot.State.Players.Where(p => p.Ai is null).Select(p => p.Name));
        return title.Length > 0 ? title : Lang.T("Spielstand", "Saved game");
    }

    public static string SlotWhen(GameState state) =>
        Lang.En ? $"{Game.MonthNames[state.Month - 1]}, year {state.Year}{(state.YearLimit is > 0 ? $" of {state.YearLimit}" : "")}"
            : $"{Game.MonthNames[state.Month - 1]}, Jahr {state.Year}{(state.YearLimit is > 0 ? $" von {state.YearLimit}" : "")}";

    static string SlotIcon(GameState state) =>
        state.Challenge is not null ? "🎯" : Rules.HasPointTarget(state) ? "🍇" : "∞";

    static string SavedAtText(DateTime savedAt) =>
        savedAt.Date == DateTime.Today ? (Lang.En ? $"today, {savedAt:HH:mm}" : $"heute, {savedAt:HH:mm}") : (Lang.En ? $"{savedAt:M/d/yyyy}, {savedAt:HH:mm}" : $"{savedAt:d.M.yyyy}, {savedAt:HH:mm}");

    public static void SavesPage(SkiaLayout layer, AppHost app, MenuSetup setup)
    {
        MenuUi.Header(layer, Lang.T("Spielstände", "Saved games"), Lang.T("Jede Partie wird nach jedem Zug automatisch gespeichert", "Every game is saved automatically after every turn"), 1100);
        var saves = setup.SavesOf(app);
        const int perPage = 6;
        var pages = Math.Max(1, (int)Math.Ceiling(saves.Count / (double)perPage));
        var page = Math.Clamp(setup.SavesPage, 0, pages - 1);

        if (saves.Count == 0)
        {
            var empty = Kit.Glass(460, 400, 1000, 220, 24);
            empty.With(Kit.Text(Lang.T("Noch keine Spielstände vorhanden.", "No saved games yet."), 0, 56, 1000, 50, 36, Theme.Cream, bold: true, serif: true, align: Align.Center));
            empty.With(Kit.Text(Lang.T("Starte ein Spiel – es erscheint dann hier.", "Start a game – it will then appear here."), 0, 118, 1000, 36, 24, Theme.CreamSoft, align: Align.Center));
            layer.With(empty);
        }

        var index = 0;
        foreach (var slot in saves.Skip(page * perPage).Take(perPage))
        {
            layer.With(SlotCard(app, setup, slot, MenuUi.Left + index % 3 * 576, 190 + index / 3 * 358));
            index++;
        }

        MenuUi.Footer(layer, Lang.T("Hauptmenü", "Main menu"), () => MainMenu.Go(app, MainMenu.Home));
        if (app.Store.Location is { } location)
        {
            layer.With(Kit.Text(Lang.En ? $"Storage: {location}" : $"Ablage: {location}", 440, 986, 900, 30, 19, Theme.CreamSoft));
        }
        if (pages > 1)
        {
            layer.With(Kit.Pager(MenuUi.Right - 300, 982, 300, page, pages, p =>
            {
                setup.SavesPage = p;
                setup.ConfirmDelete = null;
                app.Notify();
            }));
        }
    }

    static SkiaControl SlotCard(AppHost app, MenuSetup setup, SaveSlot slot, double x, double y)
    {
        var state = slot.State;
        var human = state.Players.FirstOrDefault(p => p.Ai is null) ?? state.Players[0];
        var over = state.Finished || state.WinnerId is not null;
        var card = Kit.Card(x, y, 548, 334, 20);
        card.With(Kit.Box(28, 28, 72, 72, Theme.C("#248e2440"), 18)
            .With(Kit.Text(SlotIcon(state), 0, 14, 72, 48, 34, Theme.Wine, bold: true, serif: true, align: Align.Center)));
        card.With(Kit.Text(SlotTitle(slot), 118, 24, 404, 40, 28, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text($"{Regions.All[human.RegionId].Name} · {SlotWhen(state)}", 118, 66, 404, 30, 20, Theme.InkSoft));

        var mode = state.Challenge is not null ? (Lang.En ? $"Daily Challenge {state.Challenge}" : $"Tages-Challenge {state.Challenge}") : Economy.Difficulties[state.Difficulty].Name;
        var goal = Rules.HasPointTarget(state) ? $"{Util.FormatNumber(human.Points)} / {Util.FormatNumber(state.TargetPoints)}" : Util.FormatNumber(human.Points);
        card.With(Kit.Row(Lang.T("Punkte", "Points"), goal, 28, 122, 492, Theme.Wine, 21));
        card.With(Kit.Row(Lang.T("Spielart", "Mode"), mode + (over ? Lang.T(" · beendet", " · finished") : ""), 28, 160, 492, size: 21));
        card.With(Kit.Row(Lang.T("Gespeichert", "Saved"), SavedAtText(slot.SavedAt), 28, 198, 492, size: 21));

        var confirm = setup.ConfirmDelete == slot.Id;
        if (confirm)
        {
            card.With(Kit.Button(Lang.T("Wirklich löschen", "Really delete"), 28, 250, 300, 64, () =>
            {
                app.Store.Delete(slot.Id);
                setup.Saves = null;
                setup.ConfirmDelete = null;
                app.Notify();
            }, ButtonKind.Primary, fontSize: 22, align: Align.Center));
            card.With(Kit.Button(Lang.T("Behalten", "Keep"), 344, 250, 176, 64, () =>
            {
                setup.ConfirmDelete = null;
                app.Notify();
            }, ButtonKind.Ghost, fontSize: 22, align: Align.Center));
        }
        else
        {
            card.With(Kit.Button(Lang.T("Laden", "Load"), 28, 250, 300, 64, () => MainMenu.Start(app, setup, slot.State, slot.Id), ButtonKind.Primary, fontSize: 24, align: Align.Center));
            card.With(Kit.Button(Lang.T("Löschen", "Delete"), 344, 250, 176, 64, () =>
            {
                setup.ConfirmDelete = slot.Id;
                app.Notify();
            }, ButtonKind.Secondary, fontSize: 22, align: Align.Center));
        }
        return card;
    }

    // ---- Tages-Challenge ----

    public static void DailyPage(SkiaLayout layer, AppHost app, MenuSetup setup)
    {
        var today = Challenge.TodayKey();
        var daily = Challenge.DailyChallenge(today);
        var scenario = daily.Scenario;
        var best = app.Store.GetSetting("challenge." + today);
        MenuUi.Header(layer, Lang.T("Tages-Challenge", "Daily Challenge"), Lang.En ? $"{today} · the same game for everyone" : $"{today} · für alle dieselbe Partie", 1100);

        var card = Kit.Card(310, 210, 1300, 700, 24);
        card.With(Kit.Box(0, 0, 1300, 14, Theme.Wine, 7));
        card.With(Kit.Box(60, 64, 132, 132, Theme.C("#248e2440"), 28)
            .With(Kit.Text(scenario.Icon, 0, 26, 132, 84, 62, Theme.Wine, align: Align.Center)));
        card.With(Kit.Text(Lang.T("Szenario des Tages", "Scenario of the day"), 224, 66, 800, 30, 21, Theme.InkSoft));
        card.With(Kit.Text(scenario.Name, 224, 98, 1010, 72, 52, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(scenario.Description, 60, 232, 1180, 110, 27, Theme.Ink, maxLines: 3));

        var chips = new List<string>
        {
            $"📍 {Regions.All[daily.Options.Players[0].RegionId].Name}",
            Lang.En ? $"📅 {scenario.Years} years" : $"📅 {scenario.Years} Jahre",
            $"🤖 {string.Join(", ", daily.Options.Players.Skip(1).Select(p => p.Name))}",
        };
        if (!scenario.ClimateChange) chips.Add(Lang.T("🌡️ ohne Klimawandel", "🌡️ no climate change"));
        double x = 60, y = 372;
        foreach (var text in chips)
        {
            var width = MenuUi.ChipWidth(text, 22);
            if (x + width > 1240)
            {
                x = 60;
                y += 62;
            }
            card.With(MenuUi.Chip(text, x, y, out _, size: 22));
            x += width + 14;
        }

        card.With(Kit.Text(Lang.T("Gewertet werden die Punkte am Ende der vorgegebenen Jahre – gleiches Szenario, Gebiet, Wetter und dieselben Gegner für alle.", "Points are scored at the end of the given years – same scenario, region, weather and the same rivals for everyone."),
            60, 506, 1180, 64, 21, Theme.InkSoft, maxLines: 2));
        card.With(Kit.Box(60, 590, 620, 72, best is null ? Theme.C("#59d9c7a3") : Theme.C("#40f6d477"), 36, best is null ? null : Theme.GoldDark, 2)
            .With(Kit.Text(best is null ? Lang.T("Noch nicht gespielt", "Not played yet") : (Lang.En ? $"🏅 Your best today: {best} points" : $"🏅 Dein Bestwert heute: {best} Punkte"), 0, 20, 620, 34, 24, Theme.Ink, bold: true, align: Align.Center)));
        card.With(Kit.Button(Lang.T("Challenge starten", "Start challenge"), 820, 584, 420, 84, () => MainMenu.Start(app, setup, Game.NewGame(daily.Options)), ButtonKind.Primary,
            fontSize: 28, align: Align.Center));
        layer.With(card);

        MenuUi.Footer(layer, Lang.T("Hauptmenü", "Main menu"), () => MainMenu.Go(app, MainMenu.Home));
    }

    // ---- Lizenzhinweis ----

    public static void LicensesPage(SkiaLayout layer, AppHost app)
    {
        MenuUi.Header(layer, Lang.T("Lizenzen", "Licenses"), Lang.T("Hinweise zu fremder Software und zu den Namen im Spiel", "Notes on third-party software and on the names in the game"), 1100);
        var card = Kit.Card(410, 230, 1100, 640, 24);
        card.With(Kit.Box(0, 0, 1100, 14, Theme.Wine, 7));
        card.With(Kit.Text(Lang.T("📜 Lizenzen", "📜 Licenses"), 60, 50, 980, 54, 40, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(Lang.T("WinzerAI steht unter der MIT-Lizenz und nutzt quelloffene Software von Dritten. Alle Namen von Weingütern, Personen, Siegeln und Prämierungen im Spiel sind frei erfunden.", "WinzerAI is released under the MIT license and uses open-source software from third parties. All names of estates, people, seals and wine awards in the game are fictional."),
            60, 122, 980, 76, 24, Theme.Ink, maxLines: 2));

        // Die Übersicht kommt aus THIRD-PARTY-NOTICES.txt, die beim Veröffentlichen entsteht (tools/third-party-notices.ps1)
        var text = app.Notices.Read();
        var groups = text is null ? [] : ThirdPartyNotices.ByLicense(ThirdPartyNotices.Parse(text));
        double y = 226;
        foreach (var (license, projects) in groups)
        {
            var names = string.Join(" · ", projects);
            var lines = Math.Clamp((int)Math.Ceiling(Kit.TextWidth(names, 20) / 700), 1, 3);
            var height = 30 * lines + 34;
            var row = Kit.Box(60, y, 980, height, Theme.C("#40d9c7a3"), 14);
            row.With(Kit.Text(license, 24, 17, 200, 34, 24, Theme.Wine, bold: true, serif: true));
            row.With(Kit.Text(names, 230, 20, 730, 30 * lines, 20, Theme.Ink, maxLines: lines));
            card.With(row);
            y += height + 12;
        }
        card.With(text is null
            ? Kit.Text(Lang.T("Die Lizenztexte der Fremdsoftware liegen der ausgelieferten Fassung als THIRD-PARTY-NOTICES.txt bei. Diese Fassung ist ein Entwicklerbau ohne die Datei.", "The license texts of the third-party software come with the released version as THIRD-PARTY-NOTICES.txt. This version is a developer build without the file."),
                60, 226, 980, 64, 20, Theme.InkSoft, maxLines: 2)
            : Kit.Text(Lang.T("Alle Lizenztexte, auch die der Bausteine von Skia, HarfBuzz und .NET, stehen in THIRD-PARTY-NOTICES.txt im Programmordner.", "All license texts, including those of the Skia, HarfBuzz and .NET components, are in THIRD-PARTY-NOTICES.txt in the program folder."),
                60, y + 12, 980, 64, 20, Theme.InkSoft, maxLines: 2));
        layer.With(card);
        MenuUi.Footer(layer, Lang.T("Hauptmenü", "Main menu"), () => MainMenu.Go(app, MainMenu.Home), Lang.T("Lizenztexte öffnen", "Open license texts"), () => app.Notices.Open(),
            nextProblem: text is null ? Lang.T("Nur in der ausgelieferten Fassung", "Only in the released version") : null);
    }

    // ---- Einstellungen ----

    public static void SettingsPage(SkiaLayout layer, AppHost app)
    {
        MenuUi.Header(layer, Lang.T("Einstellungen", "Settings"), Lang.T("Gilt für alle Spielstände", "Applies to all saved games"), 1100);
        var card = Kit.Card(410, 230, 1100, 400, 24);
        card.With(Kit.Box(0, 0, 1100, 14, Theme.Wine, 7));
        card.With(Kit.Text(Lang.T("Sprache", "Language"), 60, 50, 980, 54, 40, Theme.Ink, bold: true, serif: true));
        (bool En, string Name, string Sub)[] languages = [(false, "Deutsch", Lang.En ? "German" : ""), (true, "English", Lang.En ? "" : "Englisch")];
        for (var i = 0; i < languages.Length; i++)
        {
            var (en, name, sub) = languages[i];
            var tile = MenuUi.Option(60 + i * 500, 140, 480, 104, Lang.En == en, () => app.SetLanguage(en));
            tile.With(Kit.Text(name, 32, 16, 420, 40, 30, Theme.Ink, bold: true, serif: true));
            tile.With(Kit.Text(sub, 32, 60, 420, 28, 20, Theme.InkSoft));
            card.With(tile);
        }
        card.With(Kit.Text(Lang.T("Meldungen, die schon im Spiel stehen, bleiben in der Sprache, in der sie entstanden sind.", "Messages already in a game stay in the language they were written in."),
            60, 280, 980, 64, 21, Theme.InkSoft, maxLines: 2));
        layer.With(card);
        MenuUi.Footer(layer, Lang.T("Hauptmenü", "Main menu"), () => MainMenu.Go(app, MainMenu.Home));
    }
}
