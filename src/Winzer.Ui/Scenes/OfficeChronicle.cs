using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

/// <summary>Kontor, Reiter „Chronik“: das Logbuch als Zeitleiste – ein Monat je Seite, rechts der Jahreskalender zum Springen.</summary>
internal static class OfficeChronicle
{
    const string PageKey = "kontor.chronik";
    const int PerPage = 8;

    sealed record Sheet(int Year, int Month, List<LogEntry> Entries, int Part, int Parts);

    /// <summary>Alle Seiten in zeitlicher Folge: je Monat eine, bei mehr als acht Einträgen gleichmäßig geteilt</summary>
    static List<Sheet> Sheets(GameState state, Player player)
    {
        var sheets = new List<Sheet>();
        var months = state.Log
            .Where(e => e.PlayerId is null || e.PlayerId == player.Id)
            .GroupBy(e => (e.Year, e.Month))
            .OrderBy(g => g.Key.Year * 12 + g.Key.Month);
        foreach (var month in months)
        {
            var entries = month.ToList();
            var parts = (int)Math.Ceiling(entries.Count / (double)PerPage);
            var size = (int)Math.Ceiling(entries.Count / (double)parts);
            for (var p = 0; p < parts; p++)
            {
                sheets.Add(new Sheet(month.Key.Year, month.Key.Month, entries.Skip(p * size).Take(size).ToList(), p, parts));
            }
        }
        return sheets;
    }

    static Color ToneColor(LogTone? tone) => tone == LogTone.Good ? Theme.Good : tone == LogTone.Bad ? Theme.Alert : Theme.GoldDark;

    /// <summary>Baut den Reiter auf und liefert die Zeile für den Balken.</summary>
    public static string Build(SkiaLayout scene, GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var sheets = Sheets(state, player);
        var card = Kit.Card(28, 96, 1202, 864);
        scene.With(card);
        if (sheets.Count == 0)
        {
            card.With(Kit.Text(Lang.T("Die Chronik ist noch leer", "The chronicle is still empty"), 0, 360, 1202, 50, 34, Theme.Ink, bold: true, serif: true, align: Align.Center));
            card.With(Kit.Text(Lang.T("Was auf dem Weingut geschieht, wird hier Monat für Monat festgehalten.", "Whatever happens on the estate is recorded here, month by month."), 0, 416, 1202, 30, 20, Theme.InkSoft, align: Align.Center));
            scene.With(OfficeKit.Inspector().With(Kit.Text(Lang.T("Chronik", "Chronicle"), 32, 26, 460, 44, 32, Theme.Ink, bold: true, serif: true)));
            return Lang.T("Noch keine Einträge", "No entries yet");
        }

        // Gespeichert wird der Abstand zur neuesten Seite – so ist ohne Blättern immer die neueste aufgeschlagen
        var index = sheets.Count - 1 - session.Nav.Page(PageKey, sheets.Count);
        void Open(int target) => session.Nav.SetPage(PageKey, sheets.Count - 1 - Math.Clamp(target, 0, sheets.Count - 1));
        var sheet = sheets[index];
        var monthEntries = sheets.Where(s => s.Year == sheet.Year && s.Month == sheet.Month).SelectMany(s => s.Entries).ToList();

        // Kopf
        card.With(Kit.Text(Lang.En ? $"{Game.MonthNames[sheet.Month - 1]} · Year {sheet.Year}" : $"{Game.MonthNames[sheet.Month - 1]} · Jahr {sheet.Year}", 40, 22, 620, 48, 34, Theme.Ink, bold: true, serif: true));
        var good = monthEntries.Count(e => e.Tone == LogTone.Good);
        var bad = monthEntries.Count(e => e.Tone == LogTone.Bad);
        var summary = Lang.En ? $"{monthEntries.Count} {(monthEntries.Count == 1 ? "entry" : "entries")}" : $"{monthEntries.Count} {(monthEntries.Count == 1 ? "Eintrag" : "Einträge")}";
        if (good > 0) summary += Lang.En ? $" · {good} good" : $" · {good} gute";
        if (bad > 0) summary += Lang.En ? $" · {bad} bad" : $" · {bad} schlechte";
        if (good + bad > 0) summary += Lang.En ? (good + bad == 1 ? " news item" : " news items") : (good + bad == 1 ? " Nachricht" : " Nachrichten");
        card.With(Kit.Text(summary, 660, 36, 502, 28, 19, Theme.InkSoft, align: Align.End));
        card.With(Kit.Box(40, 80, 1122, 2, Theme.PaperLine));

        // Zeitleiste
        const double rowHeight = 84, top = 98;
        card.With(Kit.Box(63, top + 8, 4, sheet.Entries.Count * rowHeight - 20, Theme.PaperLine, 2));
        for (var i = 0; i < sheet.Entries.Count; i++)
        {
            var entry = sheet.Entries[i];
            var y = top + i * rowHeight;
            var color = ToneColor(entry.Tone);
            card.With(Kit.Box(52, y + 25, 26, 26, Theme.Paper, 13, color, 5));
            if (entry.Tone is not null) card.With(Kit.Box(60, y + 33, 10, 10, color, 5));
            var fill = entry.Tone == LogTone.Good ? Theme.C("#1f4d7a2a") : entry.Tone == LogTone.Bad ? Theme.C("#1cb3261e") : Theme.C("#40d9c7a3");
            var row = Kit.Box(100, y, 1062, rowHeight - 10, fill, 14);
            row.With(Kit.Box(0, 12, 6, rowHeight - 34, color, 3));
            // Kurze Einträge stehen einzeilig in der Mitte, lange zweizeilig
            var oneLine = entry.Text.Length <= 100;
            row.With(Kit.Text(entry.Text, 26, oneLine ? 22 : 9, 1012, oneLine ? 30 : 58, 19, Theme.Ink, bold: entry.Tone is not null, maxLines: oneLine ? 1 : 2));
            card.With(row);
        }

        // Blättern
        var older = index > 0;
        var newer = index < sheets.Count - 1;
        card.With(Kit.Box(40, 784, 1122, 2, Theme.PaperLine));
        var back = Kit.Button(Lang.T("‹ Früher", "‹ Earlier"), 40, 798, 190, 52, () => { if (older) Open(index - 1); }, ButtonKind.Ghost, fontSize: 20, align: Align.Center);
        var forth = Kit.Button(Lang.T("Später ›", "Later ›"), 972, 798, 190, 52, () => { if (newer) Open(index + 1); }, ButtonKind.Ghost, fontSize: 20, align: Align.Center);
        if (!older) back.Opacity = 0.35;
        if (!newer) forth.Opacity = 0.35;
        card.With(back, forth);
        var where = sheet.Parts > 1
            ? Lang.En ? $"{Game.MonthNames[sheet.Month - 1]}, sheet {sheet.Part + 1} of {sheet.Parts} · page {index + 1} of {sheets.Count}" : $"{Game.MonthNames[sheet.Month - 1]}, Blatt {sheet.Part + 1} von {sheet.Parts} · Seite {index + 1} von {sheets.Count}"
            : Lang.En ? $"Page {index + 1} of {sheets.Count}" : $"Seite {index + 1} von {sheets.Count}";
        card.With(Kit.Text(where, 240, 811, 722, 28, 19, Theme.InkSoft, align: Align.Center));

        scene.With(Calendar(session, sheets, index, Open));
        return Lang.En ? $"Logbook since {Game.MonthNames[sheets[0].Month - 1]}, year {sheets[0].Year}" : $"Logbuch seit {Game.MonthNames[sheets[0].Month - 1]}, Jahr {sheets[0].Year}";
    }

    /// <summary>Inspektor: Jahr wählen, Monat antippen</summary>
    static SkiaControl Calendar(GameSession session, List<Sheet> sheets, int index, Action<int> open)
    {
        var sheet = sheets[index];
        var card = OfficeKit.Inspector();
        card.With(Kit.Text(Lang.T("Chronik", "Chronicle"), 32, 26, 460, 44, 32, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(Lang.T("Monat antippen, um ihn aufzuschlagen.", "Tap a month to open it."), 32, 70, 460, 28, 18, Theme.InkSoft));

        // Jahressprung: gleicher Monat im Nachbarjahr, sonst der nächstgelegene
        var years = sheets.Select(s => s.Year).Distinct().OrderBy(y => y).ToList();
        int? Target(int year)
        {
            if (!years.Contains(year)) return null;
            var best = -1;
            for (var i = 0; i < sheets.Count; i++)
            {
                if (sheets[i].Year != year || sheets[i].Part != 0) continue;
                if (best < 0 || Math.Abs(sheets[i].Month - sheet.Month) < Math.Abs(sheets[best].Month - sheet.Month)) best = i;
            }
            return best < 0 ? null : best;
        }
        var previous = Target(sheet.Year - 1);
        var next = Target(sheet.Year + 1);
        card.With(Kit.Box(32, 112, 460, 76, Theme.C("#40d9c7a3"), 18));
        card.With(Kit.IconButton("‹", 44, 122, 56, () => open(previous ?? index), dark: false, enabled: previous is not null, tip: Lang.T("Vorheriger Eintrag", "Previous entry")));
        card.With(Kit.IconButton("›", 424, 122, 56, () => open(next ?? index), dark: false, enabled: next is not null, tip: Lang.T("Nächster Eintrag", "Next entry")));
        card.With(Kit.Text(Lang.En ? $"Year {sheet.Year}" : $"Jahr {sheet.Year}", 110, 126, 304, 46, 33, Theme.Ink, bold: true, serif: true, align: Align.Center));

        // Zwölf Monate
        for (var m = 1; m <= 12; m++)
        {
            var month = m;
            var first = sheets.FindIndex(s => s.Year == sheet.Year && s.Month == month);
            var entries = sheets.Where(s => s.Year == sheet.Year && s.Month == month).SelectMany(s => s.Entries).ToList();
            var on = month == sheet.Month;
            var x = 32 + (m - 1) % 3 * 158;
            var y = 204 + (m - 1) / 3 * 104;
            var cell = on
                ? Kit.Box(x, y, 144, 92, radius: 14, gradient: Kit.Vertical(Theme.Gold, Theme.GoldDark))
                : Kit.Box(x, y, 144, 92, first >= 0 ? Theme.C("#66d9c7a3") : Theme.C("#26d9c7a3"), 14);
            cell.With(Kit.Text(Game.MonthNames[m - 1], 12, 8, 124, 28, 19, first >= 0 ? Theme.Ink : Theme.C("#806a4a2a"), bold: first >= 0));
            if (first >= 0)
            {
                var good = entries.Count(e => e.Tone == LogTone.Good);
                var bad = entries.Count(e => e.Tone == LogTone.Bad);
                cell.With(Kit.Text(Lang.En ? $"{entries.Count} {(entries.Count == 1 ? "entry" : "entries")}" : $"{entries.Count} {(entries.Count == 1 ? "Eintrag" : "Einträge")}", 12, 35, 124, 24, 16, on ? Theme.Ink : Theme.InkSoft));
                var dx = 12.0;
                if (good > 0)
                {
                    cell.With(Kit.Box(dx, 66, 12, 12, Theme.Good, 6));
                    cell.With(Kit.Text(good.ToString(), dx + 17, 59, 30, 24, 16, Theme.Ink, bold: true));
                    dx += 52;
                }
                if (bad > 0)
                {
                    cell.With(Kit.Box(dx, 66, 12, 12, Theme.Alert, 6));
                    cell.With(Kit.Text(bad.ToString(), dx + 17, 59, 30, 24, 16, Theme.Ink, bold: true));
                }
                cell.Tap(() => open(first));
            }
            card.With(cell);
        }

        // Jahresbilanz und Legende
        var yearEntries = sheets.Where(s => s.Year == sheet.Year).SelectMany(s => s.Entries).ToList();
        card.With(Kit.Box(32, 628, 460, 2, Theme.PaperLine));
        card.With(Kit.Text(Lang.En ? $"Summary, year {sheet.Year}" : $"Bilanz Jahr {sheet.Year}", 32, 640, 460, 32, 22, Theme.Wine, bold: true, serif: true));
        Legend(card, 684, Theme.Good, Lang.T("Gute Nachrichten", "Good news"), yearEntries.Count(e => e.Tone == LogTone.Good));
        Legend(card, 722, Theme.Alert, Lang.T("Schlechte Nachrichten", "Bad news"), yearEntries.Count(e => e.Tone == LogTone.Bad));
        Legend(card, 760, Theme.GoldDark, Lang.T("Sonstige Einträge", "Other entries"), yearEntries.Count(e => e.Tone is null));

        card.With(Kit.Button(Lang.T("Zum neuesten Eintrag", "To the latest entry"), 32, 836, 460, 76, () => open(sheets.Count - 1), ButtonKind.Secondary,
            Lang.En ? $"{Game.MonthNames[sheets[^1].Month - 1]}, year {sheets[^1].Year}" : $"{Game.MonthNames[sheets[^1].Month - 1]}, Jahr {sheets[^1].Year}", index == sheets.Count - 1 ? Lang.T("ist aufgeschlagen", "shown now") : null));
        return card;
    }

    static void Legend(SkiaLayout card, double y, Color color, string label, int count)
    {
        card.With(Kit.Box(32, y + 7, 16, 16, color, 8));
        card.With(Kit.Text(label, 60, y, 300, 30, 20, Theme.InkSoft));
        card.With(Kit.Text(count.ToString(), 372, y, 120, 30, 20, Theme.Ink, bold: true, align: Align.End));
    }
}
