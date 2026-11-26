using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

/// <summary>
/// Lexikon als aufgeschlagenes Buch: links Register A–Z und die geblätterte Begriffsliste, rechts der gewählte Begriff.
/// Auswahl in <c>Nav.Selected("lexikon.term")</c>, Seite der Liste in <c>Nav.Page("lexikon", …)</c>.
/// </summary>
public static class LexiconOverlay
{
    const string TermKey = "lexikon.term";
    const string PageKey = "lexikon";
    const int PerPage = 14;
    const double BookWidth = 1540, BookHeight = 888;

    public static SkiaControl Build(GameSession session)
    {
        var nav = session.Nav;
        var entries = Lexicon.Entries;
        var pages = (int)Math.Ceiling(entries.Count / (double)PerPage);
        var page = nav.Page(PageKey, pages);
        var selected = IndexOf(nav.Selected(TermKey));
        // Entwicklerhilfe für Bildschirmfotos: WINZER_LEXICON_TERM wählt den anfangs aufgeschlagenen Begriff (Nummer ab 0)
        if (selected < 0 && int.TryParse(Environment.GetEnvironmentVariable("WINZER_LEXICON_TERM"), out var start))
        {
            selected = Math.Clamp(start, 0, entries.Count - 1);
            page = selected / PerPage;
        }
        selected = Math.Max(0, selected);
        var entry = entries[selected];
        void Close() => nav.Show(Overlay.None);
        void Open(int index)
        {
            var target = Math.Clamp(index, 0, entries.Count - 1);
            if (target / PerPage != page) nav.SetPage(PageKey, target / PerPage);
            nav.Select(TermKey, entries[target].Term);
        }

        var book = Kit.Layer((Kit.DesignWidth - BookWidth) / 2, (Kit.DesignHeight - BookHeight) / 2, BookWidth, BookHeight);
        book.With(Kit.Paint(0, 0, BookWidth, BookHeight, PaintBook));
        book.Block();

        // Linke Seite: Register und Begriffsliste
        book.With(Kit.Text(Lang.T("Lexikon", "Lexicon"), 70, 46, 500, 60, 46, Theme.Ink, bold: true, serif: true));
        book.With(Kit.Text(Lang.T("Kurz erklärt – ersetzt das Handbuch.", "Briefly explained – replaces the manual."), 70, 106, 560, 28, 20, Theme.InkSoft));
        var letters = entries.Select(e => char.ToUpperInvariant(e.Term[0])).Distinct().ToList();
        var current = char.ToUpperInvariant(entry.Term[0]);
        var step = Math.Min(39, 655.0 / letters.Count); // die englische Liste hat mehr Anfangsbuchstaben
        for (var i = 0; i < letters.Count; i++)
        {
            var letter = letters[i];
            var on = letter == current;
            var first = IndexOfLetter(letter);
            book.With(Kit.Box(70 + i * step, 150, step - 5, 42, on ? Theme.Wine : Theme.C("#59d9c7a3"), 9)
                .With(Kit.Text(letter.ToString(), 0, 8, step - 5, 28, 20, on ? Theme.Paper : Theme.Ink, bold: true, serif: true, align: Align.Center))
                .Tap(() => Open(first)));
        }
        for (var i = 0; i < PerPage; i++)
        {
            var index = page * PerPage + i;
            if (index >= entries.Count) break;
            var on = index == selected;
            var row = Kit.Box(62, 208 + i * 41, 668, 38, on ? Theme.C("#248e2440") : Theme.Clear, 10);
            if (on) row.With(Kit.Box(0, 6, 5, 26, Theme.Wine, 2.5));
            row.With(Kit.Text(entries[index].Term, 18, 5, 620, 30, 21, on ? Theme.Wine : Theme.Ink, bold: on));
            row.Tap(() => Open(index));
            book.With(row);
        }
        book.With(Kit.Pager(70, 800, 660, page, pages, p => nav.SetPage(PageKey, p), dark: false));

        // Rechte Seite: der gewählte Begriff
        book.With(Kit.Text(Lang.En ? $"Term {selected + 1} of {entries.Count}" : $"Begriff {selected + 1} von {entries.Count}", 810, 56, 500, 28, 19, Theme.InkSoft));
        var longTitle = entry.Term.Length > 24;
        book.With(Kit.Text(entry.Term, 810, longTitle ? 96 : 90, 610, 60, longTitle ? 37 : 46, Theme.Wine, bold: true, serif: true));
        const double top = 166;
        book.With(Kit.Box(810, top, 120, 4, Theme.GoldDark, 2));
        book.With(Kit.Text(entry.Text, 810, top + 28, 660, 760 - top - 28, 26, Theme.Ink, maxLines: 14));

        if (selected > 0)
        {
            book.With(Kit.Button("‹  " + entries[selected - 1].Term, 810, 784, 322, 56, () => Open(selected - 1), ButtonKind.Ghost, fontSize: 18));
        }
        if (selected < entries.Count - 1)
        {
            book.With(Kit.Button(entries[selected + 1].Term + "  ›", 1148, 784, 322, 56, () => Open(selected + 1), ButtonKind.Ghost, fontSize: 18, align: Align.End));
        }
        book.With(Kit.Box(1430, 42, 48, 48, Theme.PaperLine, 12)
            .With(Kit.Text("✕", 0, 10, 48, 30, 21, Theme.Ink, bold: true, align: Align.Center))
            .Tap(Close).Tip(Lang.T("Schließen", "Close")));

        return Overlays.Backdrop(onTap: Close).With(book);
    }

    static int IndexOf(string? term)
    {
        for (var i = 0; i < Lexicon.Entries.Count; i++)
        {
            if (Lexicon.Entries[i].Term == term) return i;
        }
        return -1;
    }

    static int IndexOfLetter(char letter)
    {
        for (var i = 0; i < Lexicon.Entries.Count; i++)
        {
            if (char.ToUpperInvariant(Lexicon.Entries[i].Term[0]) == letter) return i;
        }
        return 0;
    }

    /// <summary>Einband, zwei Pergamentseiten, Falz und Lesebändchen</summary>
    static void PaintBook(SKCanvas c, float w, float h)
    {
        using (var shadow = new SKPaint { IsAntialias = true, Color = SKColors.Black.WithAlpha(120), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 16) })
            c.DrawRoundRect(10, 22, w - 20, h - 20, 28, 28, shadow);
        using (var cover = Draw.VerticalGradient(0, h, (0, "#6a1a30"), (1, "#3f0d1c"))) c.DrawRoundRect(0, 0, w, h - 6, 26, 26, cover);
        using (var trim = Draw.Stroke(Theme.SkGoldDark.WithAlpha(170), 2)) c.DrawRoundRect(8, 8, w - 16, h - 22, 20, 20, trim);

        // Blattkanten unter den aufgeschlagenen Seiten
        using (var edge = Draw.Fill("#d8c7a2"))
        {
            c.DrawRoundRect(20, 20, w - 40, h - 44, 10, 10, edge);
        }
        using (var sheet = Draw.Stroke(SKColor.Parse("#a8946a").WithAlpha(150), 1))
        {
            for (var i = 0; i < 4; i++)
            {
                c.DrawLine(22 + i * 2, 30, 22 + i * 2, h - 34, sheet);
                c.DrawLine(w - 22 - i * 2, 30, w - 22 - i * 2, h - 34, sheet);
            }
        }
        var mid = w / 2;
        using (var left = Draw.HorizontalGradient(30, mid, (0, "#f8efdc"), (0.8f, "#fbf4e4"), (1, "#d9c8a4")))
            c.DrawRect(30, 20, mid - 30, h - 50, left);
        using (var right = Draw.HorizontalGradient(mid, w - 30, (0, "#d9c8a4"), (0.2f, "#fbf4e4"), (1, "#f8efdc")))
            c.DrawRect(mid, 20, mid - 30, h - 50, right);
        using (var fold = Draw.HorizontalGradient(mid - 26, mid + 26, (0, "#003a1d24"), (0.5f, "#663a1d24"), (1, "#003a1d24")))
            c.DrawRect(mid - 26, 20, 52, h - 50, fold);

        // Zierleiste mit Traube am Fuß der rechten Seite
        using (var rule = Draw.Stroke(Theme.SkGoldDark.WithAlpha(150), 2))
        {
            c.DrawLine(960, 734, 1100, 734, rule);
            c.DrawLine(1180, 734, 1320, 734, rule);
        }
        using (var berry = Draw.Fill(Theme.SkWine.WithAlpha(200)))
        {
            (float X, float Y)[] berries = [(1126, 722), (1140, 722), (1154, 722), (1133, 734), (1147, 734), (1140, 746)];
            foreach (var (x, y) in berries) c.DrawCircle(x, y, 6.5f, berry);
        }
        using (var stem = Draw.Stroke(SKColor.Parse("#4d7a2a"), 3)) Draw.Path(c, "M1140 716 V706 q10 -8 18 -2", stem);

        // Lesebändchen
        using var ribbon = Draw.VerticalGradient(0, 150, (0, "#8e2440"), (1, "#5c1429"));
        Draw.Path(c, "M676 0 H706 V138 L691 122 L676 138 Z", ribbon);
    }
}
