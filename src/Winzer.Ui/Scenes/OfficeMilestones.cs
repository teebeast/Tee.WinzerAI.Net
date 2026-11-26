using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

/// <summary>Kontor, Reiter „Erfolge“: die nächsten drei Ziele groß, darunter alle Meilensteine als Plaketten, rechts die Bilanz.</summary>
internal static class OfficeMilestones
{
    const string PageKey = "kontor.erfolge";
    const int PerPage = 9;

    static (string Light, string Dark) TierColors(Tier tier) => tier switch
    {
        Tier.Gold => ("#f8dc86", "#c8962e"),
        Tier.Silber => ("#f1f3f6", "#98a2ad"),
        _ => ("#e6a972", "#9a5a2a"),
    };

    static (double Value, double Target, double Ratio) Progress(GameState state, Player player, MilestoneDef def)
    {
        var (value, target) = def.Progress(state, player);
        value = Math.Max(0, value);
        return (Math.Min(value, target), target, target > 0 ? Math.Clamp(value / target, 0, 1) : 0);
    }

    static string Reward(Tier tier)
    {
        var reward = Milestones.TierRewards[tier];
        return $"{reward.Name} · {Util.FormatNumber(reward.Points)} {(Lang.En ? (reward.Points == 1 ? "point" : "points") : (reward.Points == 1 ? "Punkt" : "Punkte"))}";
    }

    /// <summary>Baut den Reiter auf und liefert die Zeile für den Balken.</summary>
    public static string Build(SkiaLayout scene, GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var all = Milestones.All;
        var reached = all.Where(m => player.Milestones.ContainsKey(m.Id)).OrderByDescending(m => player.Milestones[m.Id]).ToList();
        var open = all.Where(m => !player.Milestones.ContainsKey(m.Id)).OrderByDescending(m => Progress(state, player, m).Ratio).ToList();
        var points = reached.Sum(m => Milestones.TierRewards[m.Tier].Points);

        // Die nächsten drei Ziele
        if (open.Count == 0)
        {
            scene.With(Kit.Card(28, 96, 1202, 196)
                .With(Kit.Text(Lang.T("Alle Meilensteine erreicht!", "All milestones reached!"), 0, 50, 1202, 54, 38, Theme.Wine, bold: true, serif: true, align: Align.Center))
                .With(Kit.Text(Lang.T("Mehr kann ein Weingut nicht erreichen.", "An estate can't achieve more than this."), 0, 112, 1202, 30, 20, Theme.InkSoft, align: Align.Center)));
        }
        for (var i = 0; i < Math.Min(3, open.Count); i++)
        {
            var def = open[i];
            var p = Progress(state, player, def);
            var card = Kit.Card(28 + i * 407, 96, 388, 196);
            card.With(Kit.Text(i == 0 ? Lang.T("Nächstes Ziel", "Next goal") : Lang.En ? $"Goal {i + 1}" : $"Ziel {i + 1}", 132, 12, 110, 24, 16, Theme.Wine, bold: true));
            card.With(Kit.Text(Reward(def.Tier), 232, 12, 140, 24, 16, Theme.InkSoft, align: Align.End));
            card.With(Medal(14, 44, 106, def, false, p.Ratio));
            card.With(Kit.Text(def.Name, 132, 34, 244, 34, 23, Theme.Ink, bold: true, serif: true));
            card.With(Kit.Text(def.Text, 132, 70, 244, 64, 15, Theme.InkSoft, maxLines: 3));
            card.With(OfficeKit.Meter(132, 140, 240, 12, p.Ratio, Theme.Wine));
            card.With(Kit.Text(p.Target > 1 ? $"{Util.FormatNumber(p.Value)} / {Util.FormatNumber(p.Target)}" : Lang.T("noch offen", "still open"), 132, 157, 240, 24, 16, Theme.Ink, bold: true));
            scene.With(card);
        }

        // Alle Meilensteine, geblättert
        var pages = (int)Math.Ceiling(all.Count / (double)PerPage);
        var page = session.Nav.Page(PageKey, pages);
        scene.With(Kit.Box(28, 304, 1202, 44, Theme.C("#b3fbf4e4"), 22));
        scene.With(Kit.Text(Lang.T("Alle Meilensteine", "All milestones"), 50, 309, 400, 34, 23, Theme.Wine, bold: true, serif: true));
        scene.With(Kit.Pager(900, 308, 318, page, pages, p => session.Nav.SetPage(PageKey, p), dark: false));
        var shown = all.Skip(page * PerPage).Take(PerPage).ToList();
        for (var i = 0; i < shown.Count; i++)
        {
            var def = shown[i];
            var done = player.Milestones.TryGetValue(def.Id, out var when);
            var p = Progress(state, player, def);
            const double width = 392.6;
            var x = 28 + i % 3 * 404.7;
            var y = 358 + i / 3 * 202;
            var cell = Kit.Card(x, y, width, 192, 14);
            if (done) cell.With(Kit.Box(0, 0, width, 192, Theme.C("#144d7a2a"), 14, Theme.Good, 2));
            cell.With(Medal(14, 14, 92, def, done, p.Ratio));
            cell.With(Kit.Text(def.Name, 120, 14, 258, 30, 20, Theme.Ink, bold: true));
            cell.With(Kit.Text(Reward(def.Tier), 120, 43, 258, 24, 15, Theme.Wine, bold: true));
            cell.With(Kit.Text(def.Text, 120, 68, 258, 66, 15, Theme.InkSoft, maxLines: 3));
            if (done)
            {
                cell.With(Kit.Paint(120, 150, 22, 22, (c, w, h) =>
                {
                    using var fill = Draw.Fill(SKColor.Parse("#4d7a2a"));
                    c.DrawCircle(11, 11, 11, fill);
                    using var tick = Draw.Stroke(SKColor.Parse("#fbf4e4"), 2.6f);
                    Draw.Path(c, "M6 11.5 l3.5 3.5 6.5 -7.5", tick);
                }));
                cell.With(Kit.Text(Orders.MonthText(when), 150, 148, 228, 26, 17, Theme.Good, bold: true));
            }
            else
            {
                cell.With(OfficeKit.Meter(120, 156, 110, 10, p.Ratio, Theme.Wine));
                cell.With(Kit.Text(p.Target > 1 ? $"{Util.FormatNumber(p.Value)} / {Util.FormatNumber(p.Target)}" : Lang.T("noch offen", "still open"),
                    236, 148, 142, 24, 15, Theme.Ink, bold: true, align: Align.End));
            }
            scene.With(cell);
        }

        scene.With(Balance(state, player, reached, points));
        return Lang.En ? $"{reached.Count} of {all.Count} milestones reached" : $"{reached.Count} von {all.Count} Meilensteinen erreicht";
    }

    /// <summary>Plakette: Medaille in der Farbe der Stufe, offene blass mit Fortschrittsring</summary>
    static SkiaControl Medal(double x, double y, double size, MilestoneDef def, bool done, double ratio)
    {
        var colors = TierColors(def.Tier);
        var layer = Kit.Layer(x, y, size, size);
        layer.With(Kit.Paint(0, 0, size, size, (c, w, h) =>
        {
            var r = w / 2;
            if (done)
            {
                using var rim = Draw.VerticalGradient(0, h, (0, colors.Light), (1, colors.Dark));
                c.DrawCircle(r, r, r - 1, rim);
                using var face = Draw.VerticalGradient(0, h, (0, colors.Dark), (1, colors.Light));
                c.DrawCircle(r, r, r * 0.8f, face);
                using var inner = Draw.Fill(SKColor.Parse("#fbf4e4").WithAlpha(215));
                c.DrawCircle(r, r, r * 0.68f, inner);
                using var shine = Draw.Stroke(SKColors.White.WithAlpha(150), w * 0.03f);
                c.DrawArc(new SKRect(w * 0.08f, w * 0.08f, w * 0.92f, w * 0.92f), 200, 70, false, shine);
            }
            else
            {
                using var face = Draw.Fill(SKColor.Parse("#e6d8b8"));
                c.DrawCircle(r, r, r * 0.8f, face);
                using var edge = Draw.Stroke(SKColor.Parse(colors.Dark), w * 0.035f);
                edge.PathEffect = SKPathEffect.CreateDash([w * 0.07f, w * 0.06f], 0);
                c.DrawCircle(r, r, r * 0.8f, edge);
                Draw.Ring(c, r, r, r * 0.93f, w * 0.06f, ratio, SKColor.Parse("#d9c7a3"), Theme.SkWine);
            }
        }));
        var glyph = Kit.Text(def.Icon, 0, size * 0.24, size, size * 0.56, size * 0.38, Theme.Ink, align: Align.Center);
        if (!done) glyph.Opacity = 0.6;
        layer.With(glyph);
        return layer;
    }

    /// <summary>Inspektor: Bilanz nach Stufen und die zuletzt erreichten Meilensteine</summary>
    static SkiaControl Balance(GameState state, Player player, List<MilestoneDef> reached, double points)
    {
        var all = Milestones.All;
        var card = OfficeKit.Inspector();
        card.With(Kit.Text(Lang.T("Erfolge", "Achievements"), 32, 26, 460, 44, 32, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(Lang.T("Meilensteine bringen Punkte und etwas Bekanntheit.", "Milestones bring points and some reputation."), 32, 70, 460, 28, 18, Theme.InkSoft));

        var share = reached.Count / (double)all.Count;
        card.With(Kit.Paint(32, 112, 150, 150, (c, w, h) => Draw.Ring(c, 75, 75, 62, 18, share, SKColor.Parse("#d9c7a3"), Theme.SkGoldDark)));
        card.With(Kit.Text(reached.Count.ToString(), 32, 150, 150, 50, 38, Theme.Ink, bold: true, serif: true, align: Align.Center));
        card.With(Kit.Text(Lang.En ? $"of {all.Count}" : $"von {all.Count}", 32, 196, 150, 24, 16, Theme.InkSoft, align: Align.Center));
        card.With(Kit.Text($"{Util.FormatNumber(points)} {(Lang.En ? (points == 1 ? "point" : "points") : (points == 1 ? "Punkt" : "Punkte"))}", 210, 136, 282, 48, 36, Theme.Wine, bold: true, serif: true));
        card.With(Kit.Text(Lang.T("aus Meilensteinen", "from milestones"), 210, 186, 282, 28, 19, Theme.InkSoft));
        card.With(Kit.Text(Lang.En ? $"{all.Count - reached.Count} still open" : $"{all.Count - reached.Count} noch offen", 210, 214, 282, 28, 19, Theme.InkSoft));

        // Stufen
        var y = 280.0;
        foreach (var tier in new[] { Tier.Bronze, Tier.Silber, Tier.Gold })
        {
            var reward = Milestones.TierRewards[tier];
            var total = all.Count(m => m.Tier == tier);
            var got = reached.Count(m => m.Tier == tier);
            var colors = TierColors(tier);
            var row = Kit.Box(32, y, 460, 64, Theme.C("#40d9c7a3"), 14);
            row.With(Kit.Paint(12, 10, 44, 44, (c, w, h) =>
            {
                using var rim = Draw.VerticalGradient(0, h, (0, colors.Light), (1, colors.Dark));
                c.DrawCircle(22, 22, 21, rim);
                using var face = Draw.VerticalGradient(0, h, (0, colors.Dark), (1, colors.Light));
                c.DrawCircle(22, 22, 15, face);
            }));
            row.With(Kit.Text(reward.Name, 70, 6, 200, 28, 20, Theme.Ink, bold: true));
            row.With(Kit.Text(Lang.En ? $"+ {Util.FormatNumber(reward.Points)} {(reward.Points == 1 ? "point" : "points")} · + {Util.FormatNumber(reward.Reputation)} reputation" : $"+ {Util.FormatNumber(reward.Points)} {(reward.Points == 1 ? "Punkt" : "Punkte")} · + {Util.FormatNumber(reward.Reputation)} Bekanntheit",
                70, 33, 290, 24, 15, Theme.InkSoft));
            row.With(Kit.Text($"{got} / {total}", 340, 16, 106, 32, 23, got == total ? Theme.Good : Theme.Ink, bold: true, serif: true, align: Align.End));
            card.With(row);
            y += 72;
        }

        // Zuletzt erreicht
        card.With(Kit.Box(32, 506, 460, 2, Theme.PaperLine));
        card.With(Kit.Text(Lang.T("Zuletzt erreicht", "Recently reached"), 32, 518, 460, 32, 22, Theme.Wine, bold: true, serif: true));
        if (reached.Count == 0)
        {
            card.With(Kit.Text(Lang.T("Noch kein Meilenstein – die erste Lese bringt den ersten.", "No milestone yet – your first harvest brings the first."), 32, 560, 460, 56, 18, Theme.InkSoft, maxLines: 2));
        }
        y = 560;
        foreach (var def in reached.Take(5))
        {
            card.With(Medal(32, y, 60, def, true, 1));
            card.With(Kit.Text(def.Name, 106, y + 4, 386, 28, 19, Theme.Ink, bold: true));
            card.With(Kit.Text($"{Orders.MonthText(player.Milestones[def.Id])} · {Reward(def.Tier)}", 106, y + 31, 386, 24, 16, Theme.InkSoft));
            y += 74;
        }
        // Solange die Liste kurz ist: ein Lorbeerkranz mit dem, was noch wartet
        if (reached.Count <= 3)
        {
            var left = all.Count - reached.Count;
            card.With(Kit.Paint(32, 790, 460, 132, Laurel));
            card.With(Kit.Text(left.ToString(), 162, 812, 200, 56, 44, Theme.GoldDark, bold: true, serif: true, align: Align.Center));
            card.With(Kit.Text(Lang.En ? (left == 1 ? "goal still waiting" : "goals still waiting") : (left == 1 ? "Ziel wartet noch" : "Ziele warten noch"), 112, 868, 300, 28, 18, Theme.InkSoft, align: Align.Center));
        }
        return card;
    }

    static void Laurel(SKCanvas c, float w, float h)
    {
        using var leaf = Draw.Fill(SKColor.Parse("#c8962e").WithAlpha(190));
        using var stem = Draw.Stroke(SKColor.Parse("#c8962e").WithAlpha(190), 3);
        foreach (var side in new[] { -1f, 1f })
        {
            c.Save();
            c.Translate(w / 2, 0);
            c.Scale(side, 1);
            // Zweig als Bézierkurve von unten nach oben; die Blätter sitzen paarweise entlang der Kurve
            SKPoint p0 = new(-108, 126), p1 = new(-176, 114), p2 = new(-204, 62), p3 = new(-184, 6);
            Draw.Path(c, $"M{p0.X} {p0.Y} C{p1.X} {p1.Y} {p2.X} {p2.Y} {p3.X} {p3.Y}", stem);
            for (var i = 0; i < 7; i++)
            {
                var t = 0.1f + i * 0.15f;
                var u = 1 - t;
                var x = u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X;
                var y = u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y;
                var dx = 3 * u * u * (p1.X - p0.X) + 6 * u * t * (p2.X - p1.X) + 3 * t * t * (p3.X - p2.X);
                var dy = 3 * u * u * (p1.Y - p0.Y) + 6 * u * t * (p2.Y - p1.Y) + 3 * t * t * (p3.Y - p2.Y);
                var angle = (float)(Math.Atan2(dy, dx) * 180 / Math.PI) + 90;
                foreach (var tilt in new[] { -38f, 38f })
                {
                    c.Save();
                    c.Translate(x, y);
                    c.RotateDegrees(angle + tilt);
                    c.DrawOval(0, -13, 6, 14, leaf);
                    c.Restore();
                }
            }
            c.Restore();
        }
    }
}
