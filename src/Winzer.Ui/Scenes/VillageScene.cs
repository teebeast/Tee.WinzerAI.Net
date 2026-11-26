using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

/// <summary>
/// Dorf: der Dorfplatz mit den Häusern aller Weingüter. Drei Reiter – Rangliste (mit Meilensteinen),
/// Nachbarschaft (Verhältnis der Computergegner, Intrigen) und Versteigerung (angekündigte Lose).
/// </summary>
public static class VillageScene
{
    const string RivalKey = "dorf.rival";
    const string LotKey = "dorf.lot";
    const double PanelTop = 440;
    const double PanelHeight = 520;

    public static SkiaControl Build(GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var scene = Kit.Layer(0, 0, Shell.StageWidth, Shell.StageHeight);
        var tab = (session.Nav.Tab % 3 + 3) % 3;

        // Dorfplatz: je Weingut ein Haus
        var slots = VillagePaint.HouseSlots(state.Players.Count);
        var houses = state.Players.Select((p, i) => new VillagePaint.House(slots[i].Cx, slots[i].Width,
            VillagePaint.EstateColors[i % VillagePaint.EstateColors.Length], p.Id == player.Id, p.Bankrupt)).ToList();
        scene.With(Kit.Paint(0, 0, Shell.StageWidth, Shell.StageHeight, (c, w, h) => VillagePaint.Square(c, w, h, state, houses)));
        for (var i = 0; i < state.Players.Count; i++)
        {
            var p = state.Players[i];
            var own = p.Id == player.Id;
            var width = Math.Min(230, NearestGap(slots, i) - 10);
            var chip = own
                ? Kit.Box(slots[i].Cx - width / 2, 382, width, 32, radius: 16, gradient: Kit.Vertical(Theme.Gold, Theme.GoldDark))
                : Kit.Box(slots[i].Cx - width / 2, 382, width, 32, Theme.C("#d91c1418"), 16);
            chip.With(Kit.Centered(p.Name, 10, width - 20, 32, 16, own ? Theme.Ink : Theme.Cream, bold: own));
            if (!own && !p.Bankrupt)
            {
                var id = p.Id;
                chip.Tap(() =>
                {
                    session.Nav.Select(RivalKey, id);
                    session.Nav.SetTab(1);
                });
            }
            scene.With(chip);
        }

        var announced = state.Auction is { } auction && auction.Lots.Any(l => l.RegionId == player.RegionId);
        scene.With(Kit.Tabs(28, 20, Lang.En ? ["Standings", "Neighborhood", "Auction"] : ["Rangliste", "Nachbarschaft", "Versteigerung"], tab, session.Nav.SetTab, [0, 0, announced ? 1 : 0]));

        switch (tab)
        {
            case 1:
                Neighbors(session, scene);
                break;
            case 2:
                AuctionTab(session, scene);
                break;
            default:
                Ranking(session, scene);
                break;
        }
        return scene;
    }

    static double NearestGap(List<(float Cx, float Width)> slots, int i)
    {
        var gap = double.MaxValue;
        for (var j = 0; j < slots.Count; j++)
        {
            if (j != i) gap = Math.Min(gap, Math.Abs(slots[j].Cx - slots[i].Cx));
        }
        // Die Kirche mit dem Brunnen steht dazwischen
        return Math.Min(gap, Math.Max(120, 2 * (Math.Abs(slots[i].Cx - VillagePaint.ChurchX) - 100)));
    }

    static SkiaShape Inspector() => Kit.Card(1260, 20, 524, 948, 22);

    static SkiaControl Divider(double y) => Kit.Box(32, y, 460, 2, Theme.PaperLine);

    static string Hectares(double ha) => $"{Util.FormatNumber(ha, 1)} ha";

    static string Signed(double value)
    {
        var rounded = Js.Round(value);
        return (rounded > 0 ? "+" : rounded < 0 ? "−" : "") + Util.FormatNumber(Math.Abs(rounded));
    }

    static string EstateLine(Player p)
    {
        var line = Regions.All[p.RegionId].Name;
        if (p.Ai is { } ai) line += $" · {Lang.T("KI", "AI")} {Ai.AiProfiles[ai].Name}";
        return line;
    }

    // ---- Reiter 0: Rangliste ----

    static void Ranking(GameSession session, SkiaLayout scene)
    {
        var state = session.State;
        var player = session.Player;
        var ranking = state.Players.Select((p, i) => (Player: p, Index: i))
            .OrderByDescending(e => e.Player.Points).ThenByDescending(e => Market.NetWorth(e.Player)).ToList();
        var hasTarget = Rules.HasPointTarget(state);
        var scale = Math.Max(Math.Max(hasTarget ? state.TargetPoints : 0, state.Players.Max(p => p.Points)), 1);

        var panel = Kit.Glass(28, PanelTop, 1204, PanelHeight, 22);
        panel.With(Kit.Text(Lang.T("Siegpunkte und Weingüter im Vergleich", "Victory points and estates compared"), 28, 16, 700, 36, 26, Theme.Gold, bold: true, serif: true));
        var rule = hasTarget ? (Lang.En ? $"Whoever reaches {Util.FormatNumber(state.TargetPoints)} points first wins." : $"Gewonnen hat, wer zuerst {Util.FormatNumber(state.TargetPoints)} Punkte erreicht.")
            : Rules.IsEndless(state) ? Lang.T("Endlosspiel: Die Rangliste zeigt den aktuellen Stand.", "Endless game: the standings show the current state.")
            : Lang.T("Punkte entscheiden am Ende der Partie.", "Points decide at the end of the game.");
        panel.With(Kit.Text($"{rule} {Lang.T("Vermögen = Betriebswert + Konto − Kredit.", "Net worth = business value + account − loan.")}", 28, 54, 1000, 26, 17, Theme.CreamSoft));

        const int perPage = 6;
        var pages = Math.Max(1, (int)Math.Ceiling(ranking.Count / (double)perPage));
        var page = session.Nav.Page("dorf.rang", pages);
        if (pages > 1) panel.With(Kit.Pager(900, 18, 276, page, pages, p => session.Nav.SetPage("dorf.rang", p)));

        // Spaltenköpfe
        const double head = 92;
        panel.With(Kit.Text(Lang.T("Weingut", "Estate"), 104, head, 300, 22, 15, Theme.CreamSoft));
        panel.With(Kit.Text(hasTarget ? Lang.T("Siegpunkte bis zum Ziel", "Victory points to the goal") : Lang.T("Siegpunkte", "Victory points"), 440, head, 300, 22, 15, Theme.CreamSoft));
        panel.With(Kit.Text(Lang.T("Bekanntheit", "Reputation"), 846, head, 110, 22, 15, Theme.CreamSoft, align: Align.End));
        panel.With(Kit.Text(Lang.T("Rebfläche", "Vine area"), 962, head, 90, 22, 15, Theme.CreamSoft, align: Align.End));
        panel.With(Kit.Text(Lang.T("Vermögen", "Net worth"), 1040, head, 136, 22, 15, Theme.CreamSoft, align: Align.End));

        var shown = ranking.Skip(page * perPage).Take(perPage).ToList();
        var rowHeight = Math.Min(92, (PanelHeight - 122 - 14) / Math.Max(1, shown.Count));
        var y = 122.0;
        for (var i = 0; i < shown.Count; i++)
        {
            var (p, index) = shown[i];
            var place = page * perPage + i;
            var own = p.Id == player.Id;
            var h = rowHeight - 8;
            var row = own
                ? Kit.Box(16, y, 1172, h, Theme.C("#3df6d477"), 14, Theme.Gold, 2)
                : Kit.Box(16, y, 1172, h, Theme.C("#14f4ead7"), 14);
            var mid = h / 2;
            row.With(Kit.Paint(14, mid - 26, 52, 52, (c, w, _) => Medal(c, w, place)));
            row.With(Kit.Text((place + 1).ToString(), 14, mid - 15, 52, 30, 22, place < 3 ? Theme.Ink : Theme.Cream, bold: true, serif: true, align: Align.Center));
            row.With(Kit.Box(76, mid - 22, 6, 44, Theme.C(VillagePaint.EstateColors[index % VillagePaint.EstateColors.Length]), 3));
            row.With(Kit.Text(p.Name + (own ? Lang.T(" (du)", " (you)") : ""), 92, mid - 28, 320, 30, 21, own ? Theme.Gold : Theme.Cream, bold: true));
            row.With(Kit.Text(EstateLine(p), 92, mid + 3, p.Bankrupt ? 226 : 320, 24, 15, Theme.CreamSoft));
            if (p.Bankrupt)
            {
                row.With(Kit.Box(322, mid + 2, 92, 24, Theme.Alert, 12)
                    .With(Kit.Text(Lang.T("insolvent", "insolvent"), 0, 3, 92, 20, 14, Colors.White, bold: true, align: Align.Center)));
            }
            // Punktebalken, bei Punkteziel mit Zielmarke am Ende
            row.With(Kit.Box(424, mid - 9, 316, 18, Theme.C("#33f4ead7"), 9));
            var fill = 316 * Math.Clamp(p.Points / scale, 0, 1);
            if (fill > 0)
            {
                row.With(own
                    ? Kit.Box(424, mid - 9, Math.Max(18, fill), 18, radius: 9, gradient: Kit.Vertical(Theme.Gold, Theme.GoldDark))
                    : Kit.Box(424, mid - 9, Math.Max(18, fill), 18, Theme.C("#c98e2440"), 9));
            }
            if (hasTarget) row.With(Kit.Box(736, mid - 15, 4, 30, Theme.Gold, 2));
            row.With(Kit.Text(Util.FormatNumber(p.Points), 748, mid - 16, 76, 32, 24, own ? Theme.Gold : Theme.Cream, bold: true, align: Align.End));
            row.With(Kit.Paint(848, mid - 11, 22, 22, (c, w, _) => Star(c, w)));
            row.With(Kit.Text(Util.FormatNumber(Js.Round(p.Reputation)), 870, mid - 14, 70, 28, 20, Theme.Cream, bold: true, align: Align.End));
            row.With(Kit.Text(Hectares(Market.TotalHectares(p)), 946, mid - 14, 90, 28, 20, Theme.Cream, align: Align.End));
            var worth = Market.NetWorth(p);
            row.With(Kit.Text(Util.FormatEuro(Js.Round(worth)), 1016, mid - 14, 144, 28, 20, worth < 0 ? Theme.C("#ff9b8a") : Theme.Cream, bold: true, align: Align.End));
            if (p.Bankrupt) row.Opacity = 0.6;
            else if (!own)
            {
                var id = p.Id;
                row.Tap(() =>
                {
                    session.Nav.Select(RivalKey, id);
                    session.Nav.SetTab(1);
                });
            }
            panel.With(row);
            y += rowHeight;
        }
        scene.With(panel);
        scene.With(MilestonePanel(session));
    }

    static void Medal(SKCanvas c, float s, int place)
    {
        if (place >= 3)
        {
            using var ring = Draw.Stroke(Theme.SkCreamSoft.WithAlpha(120), 2.5f);
            c.DrawCircle(s / 2, s / 2, s * 0.4f, ring);
            return;
        }
        var (light, dark) = place switch
        {
            0 => ("#fbe08a", "#c8962e"),
            1 => ("#eef1f4", "#9aa3ad"),
            _ => ("#e6b083", "#a5673a"),
        };
        using var fill = Draw.VerticalGradient(0, s, (0, light), (1, dark));
        using var rim = Draw.Stroke(SKColor.Parse(dark), 2.5f);
        c.DrawCircle(s / 2, s / 2, s * 0.44f, fill);
        c.DrawCircle(s / 2, s / 2, s * 0.44f, rim);
        using var inner = Draw.Stroke(SKColors.White.WithAlpha(110), 1.5f);
        c.DrawCircle(s / 2, s / 2, s * 0.35f, inner);
    }

    static void Star(SKCanvas c, float s)
    {
        using var gold = Draw.VerticalGradient(0, s, (0, "#f6d477"), (1, "#c8962e"));
        c.Save();
        c.Scale(s / 30f);
        Draw.Path(c, "M15 1 l4.5 9.5 10.5 1.3 -7.7 7.2 2 10.3 -9.3 -5.1 -9.3 5.1 2 -10.3 -7.7 -7.2 10.5 -1.3 Z", gold);
        c.Restore();
    }

    /// <summary>Meilensteine: nächste Ziele mit Fortschritt und die Chronik des Erreichten</summary>
    static SkiaControl MilestonePanel(GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var card = Inspector();
        (double Value, double Target, double Ratio) Progress(MilestoneDef def)
        {
            var (value, target) = def.Progress(state, player);
            return (Math.Max(0, value), target, Math.Clamp(value / target, 0, 1));
        }
        var reached = Milestones.All.Where(m => player.Milestones.ContainsKey(m.Id)).OrderByDescending(m => player.Milestones[m.Id]).ToList();
        var open = Milestones.All.Where(m => !player.Milestones.ContainsKey(m.Id)).OrderByDescending(m => Progress(m).Ratio).ToList();
        var earned = reached.Sum(m => Milestones.TierRewards[m.Tier].Points);

        card.With(Kit.Text(Lang.T("Meilensteine", "Milestones"), 32, 26, 460, 44, 32, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(Lang.En ? $"{reached.Count} of {Milestones.All.Count} reached · {Util.FormatNumber(earned)} {(earned == 1 ? "point" : "points")} from them. Bronze brings 1 point, silver 3, gold 5 – plus some reputation." : $"{reached.Count} von {Milestones.All.Count} erreicht · {Util.FormatNumber(earned)} {(earned == 1 ? "Punkt" : "Punkte")} daraus. Bronze bringt 1 Punkt, Silber 3, Gold 5 – dazu etwas Bekanntheit.",
            32, 74, 460, 50, 17, Theme.InkSoft, maxLines: 2));

        // Ohne offene Ziele bekommt die Chronik den ganzen Platz – und umgekehrt
        var openRows = open.Count == 0 ? 0 : reached.Count == 0 ? 8 : 4;
        var doneRows = reached.Count == 0 ? 0 : open.Count == 0 ? 9 : 4;
        var y = 138.0;
        if (openRows > 0)
        {
            var pages = Math.Max(1, (int)Math.Ceiling(open.Count / (double)openRows));
            var page = session.Nav.Page("dorf.ziele", pages);
            card.With(Kit.Text(Lang.T("Nächste Ziele", "Next goals"), 32, y, 240, 32, 23, Theme.Wine, bold: true, serif: true));
            if (pages > 1) card.With(Kit.Pager(272, y - 2, 220, page, pages, p => session.Nav.SetPage("dorf.ziele", p), dark: false));
            y += 42;
            foreach (var def in open.Skip(page * openRows).Take(openRows))
            {
                var progress = Progress(def);
                var row = Kit.Box(24, y, 476, 86, Theme.C("#40d9c7a3"), 12);
                row.With(Kit.Text(def.Icon, 10, 12, 48, 44, 30, Theme.Ink, align: Align.Center));
                row.With(Kit.Text(def.Name, 66, 6, 330, 28, 19, Theme.Ink, bold: true));
                row.With(TierBadge(def.Tier, 400, 8));
                row.With(Kit.Text(def.Text, 66, 32, 398, 24, 15, Theme.InkSoft));
                if (progress.Target > 1)
                {
                    row.With(Kit.Box(66, 63, 250, 8, Theme.PaperLine, 4));
                    if (progress.Ratio > 0) row.With(Kit.Box(66, 63, Math.Max(8, 250 * progress.Ratio), 8, Theme.Wine, 4));
                    row.With(Kit.Text($"{Util.FormatNumber(Math.Min(progress.Value, progress.Target))} / {Util.FormatNumber(progress.Target)}",
                        322, 54, 142, 22, 15, Theme.Ink, bold: true, align: Align.End));
                }
                else
                {
                    row.With(Kit.Text(Lang.T("noch offen", "still open"), 66, 56, 200, 22, 15, Theme.InkSoft));
                }
                card.With(row);
                y += 94;
            }
            y += 6;
        }
        if (doneRows > 0)
        {
            var pages = Math.Max(1, (int)Math.Ceiling(reached.Count / (double)doneRows));
            var page = session.Nav.Page("dorf.chronik", pages);
            card.With(Kit.Text(Lang.T("Chronik", "Chronicle"), 32, y, 240, 32, 23, Theme.Wine, bold: true, serif: true));
            if (pages > 1) card.With(Kit.Pager(272, y - 2, 220, page, pages, p => session.Nav.SetPage("dorf.chronik", p), dark: false));
            y += 42;
            foreach (var def in reached.Skip(page * doneRows).Take(doneRows))
            {
                var row = Kit.Box(24, y, 476, 78, Theme.C("#2e4d7a2a"), 12, Theme.C("#664d7a2a"), 1.5);
                row.With(Kit.Text(def.Icon, 10, 12, 48, 44, 30, Theme.Ink, align: Align.Center));
                row.With(Kit.Text(def.Name, 66, 6, 330, 28, 19, Theme.Ink, bold: true));
                row.With(TierBadge(def.Tier, 400, 8));
                row.With(Kit.Text(def.Text, 66, 31, 398, 22, 15, Theme.InkSoft));
                row.With(Kit.Text(Orders.MonthText(player.Milestones[def.Id]), 66, 52, 398, 22, 15, Theme.Good, bold: true));
                card.With(row);
                y += 86;
            }
        }
        return card;
    }

    static SkiaControl TierBadge(Tier tier, double x, double y)
    {
        var (name, fill, text) = tier switch
        {
            Tier.Gold => ("Gold", Theme.GoldDark, Theme.Ink),
            Tier.Silber => (Lang.T("Silber", "Silver"), Theme.C("#9aa3ad"), Theme.Ink),
            _ => ("Bronze", Theme.C("#a5673a"), Colors.White),
        };
        return Kit.Box(x, y, 66, 22, fill, 11).With(Kit.Centered(name, 0, 66, 22, 13, text, bold: true));
    }

    // ---- Reiter 1: Nachbarschaft ----

    static void Neighbors(GameSession session, SkiaLayout scene)
    {
        var state = session.State;
        var player = session.Player;
        // Computergegner zuerst; menschliche Mitspieler bleiben als Ziel für Intrigen wählbar
        var rivals = state.Players.Where(p => p.Id != player.Id && !p.Bankrupt).OrderBy(p => p.Ai is null ? 1 : 0).ToList();

        var intro = Kit.Glass(28, PanelTop, 1204, 96, 22);
        intro.With(Kit.Text(
            Lang.En ? "This is how the AI rivals feel about you. Cooperation makes neighbors friendlier – sabotage you get caught at, vineyards snatched away and " +
            "wine scandals sour them. Friendly neighbors in the same region offer cooperation, annoyed ones launch discount campaigns or " +
            "complain publicly. In time, tempers cool." : "So stehen die Computergegner zu dir. Kooperationen machen Nachbarn freundlicher – erwischte Sabotage, weggeschnappte Weinberge und " +
            "Weinskandale verstimmen sie. Freundliche Nachbarn im selben Gebiet bieten Zusammenarbeit an, verstimmte starten Rabattaktionen oder " +
            "beschweren sich öffentlich. Mit der Zeit beruhigen sich die Gemüter.",
            28, 12, 1150, 76, 17, Theme.Cream, maxLines: 3));
        scene.With(intro);

        if (rivals.Count == 0)
        {
            var empty = Kit.Glass(28, PanelTop + 110, 1204, PanelHeight - 110, 22);
            empty.With(Kit.Paint(552, 90, 100, 100, (c, w, _) => VillagePaint.Face(c, w, 0)));
            empty.With(Kit.Text(Lang.T("Keine Nachbarn im Spiel", "No neighbors in the game"), 0, 210, 1204, 40, 28, Theme.Gold, bold: true, serif: true, align: Align.Center));
            empty.With(Kit.Text(Lang.T("Du wirtschaftest allein – es gibt niemanden, mit dem du dich gut oder schlecht stellen könntest.", "You run your estate alone – there is no one to get on well or badly with."), 0, 256, 1204, 30, 19, Theme.CreamSoft, align: Align.Center));
            scene.With(empty);
            var none = Inspector();
            none.With(Kit.Text(Lang.T("Nachbarschaft", "Neighborhood"), 32, 26, 460, 44, 32, Theme.Ink, bold: true, serif: true));
            none.With(Kit.Text(Lang.T("Ohne Mitspieler gibt es weder Verhältnisse noch Intrigen.", "Without other players there are no relations and no intrigues."), 32, 78, 460, 56, 19, Theme.InkSoft, maxLines: 2));
            scene.With(none);
            return;
        }

        var selected = rivals.FirstOrDefault(p => p.Id == session.Nav.Selected(RivalKey)) ?? rivals[0];
        const int perPage = 4;
        var pages = Math.Max(1, (int)Math.Ceiling(rivals.Count / (double)perPage));
        // Ohne eigenes Blättern die Seite des gewählten Nachbarn zeigen
        var page = session.Nav.Page("dorf.nachbarn", pages);
        var top = PanelTop + 110;
        var shown = rivals.Skip(page * perPage).Take(perPage).ToList();
        for (var i = 0; i < shown.Count; i++)
        {
            var rival = shown[i];
            var x = 28 + (i % 2) * 609;
            var y = top + (i / 2) * 207;
            scene.With(RivalCard(session, rival, x, y, rival.Id == selected.Id));
        }
        if (pages > 1)
        {
            scene.With(Kit.Box(940, 388, 292, 44, Theme.C("#d91c1418"), 22));
            scene.With(Kit.Pager(960, 392, 260, page, pages, p => session.Nav.SetPage("dorf.nachbarn", p)));
        }
        scene.With(RivalInspector(session, selected));
    }

    static SkiaControl RivalCard(GameSession session, Player rival, double x, double y, bool selected)
    {
        var player = session.Player;
        const double w = 595, h = 197;
        var card = Kit.Card(x, y, w, h, 20);
        // Auch ohne Auswahl ein (unsichtbarer) Rand, damit der Inhalt nicht springt
        card.StrokeColor = selected ? Theme.Wine : Theme.PaperLine;
        card.StrokeWidth = 5 * Kit.S;
        var isAi = rival.Ai is not null;
        var value = Rivals.Relation(rival, player.Id);
        int? level = isAi ? VillagePaint.RelationLevel(value) : null;
        card.With(Kit.Paint(22, 20, 92, 92, (c, s, _) => VillagePaint.Face(c, s, level)));
        card.With(Kit.Text(rival.Name, 132, 16, 440, 34, 25, Theme.Ink, bold: true, serif: true));
        var where = Regions.All[rival.RegionId].Name + (rival.RegionId == player.RegionId ? Lang.T(" (Nachbar)", " (neighbor)") : "");
        card.With(Kit.Text(isAi ? $"{where} · {Lang.T("KI", "AI")} {Ai.AiProfiles[rival.Ai!.Value].Name}" : $"{where} · {Lang.T("Mitspieler", "Player")}", 132, 52, 440, 24, 16, Theme.InkSoft));

        if (isAi)
        {
            var label = Rivals.RelationLabel(value);
            var hex = VillagePaint.RelationHex(level!.Value);
            var chipWidth = 60 + (label.Text.Length + Signed(value).Length) * 11.5;
            card.With(Kit.Box(132, 82, chipWidth, 34, Theme.C(hex), 17)
                .With(Kit.Text($"{label.Text} ({Signed(value)})", 0, 5, chipWidth, 26, 18, Theme.Ink, bold: true, align: Align.Center)));
            // Skala −100 … +100 mit den Schwellen der Stimmungen
            card.With(Kit.Paint(24, 132, w - 48, 54, (c, sw, sh) => RelationScale(c, sw, value)));
            card.With(Kit.Text(Lang.T("verfeindet", "hostile"), 24, 164, 160, 20, 13, Theme.InkSoft));
            card.With(Kit.Text(Lang.T("neutral", "neutral"), w / 2 - 80, 164, 160, 20, 13, Theme.InkSoft, align: Align.Center));
            card.With(Kit.Text(Lang.T("befreundet", "friendly"), w - 184, 164, 160, 20, 13, Theme.InkSoft, align: Align.End));
        }
        else
        {
            card.With(Kit.Box(132, 82, 150, 34, Theme.C("#9cb8d0"), 17)
                .With(Kit.Text(Lang.T("Mitspieler", "Player"), 0, 5, 150, 26, 18, Theme.Ink, bold: true, align: Align.Center)));
            card.With(Kit.Text(Lang.T("Menschen führen keine Liste über ihr Verhältnis zu dir – was sie von dir halten, sagen sie dir selbst.", "People don't keep a tally of how they feel about you – they tell you themselves."),
                24, 132, w - 48, 50, 16, Theme.InkSoft, maxLines: 2));
        }
        var id = rival.Id;
        card.Tap(() => session.Nav.Select(RivalKey, id));
        return card;
    }

    static void RelationScale(SKCanvas c, float w, double value)
    {
        const float top = 8, height = 14;
        using (var track = Draw.HorizontalGradient(0, w, (0, "#d2573f"), (0.3f, "#e0994c"), (0.5f, "#e3c65a"), (0.7f, "#a9c860"), (1, "#6fae5a")))
            c.DrawRoundRect(0, top, w, height, height / 2, height / 2, track);
        using (var tick = Draw.Stroke(SKColor.Parse("#3a1d24").WithAlpha(110), 2))
        {
            foreach (var at in new[] { -40, -10, 10, 40 })
            {
                var tx = w * (at + 100) / 200f;
                c.DrawLine(tx, top, tx, top + height, tick);
            }
        }
        var x = (float)(w * (Math.Clamp(value, -100, 100) + 100) / 200);
        x = Math.Clamp(x, 10, w - 10);
        using var knob = Draw.Fill(Theme.SkInk);
        using var rim = Draw.Stroke(SKColor.Parse("#fbf4e4"), 3);
        c.DrawCircle(x, top + height / 2, 11, knob);
        c.DrawCircle(x, top + height / 2, 11, rim);
    }

    static string RelationMeaning(Player rival, Player player, double value)
    {
        var same = rival.RegionId == player.RegionId;
        return VillagePaint.RelationLevel(value) switch
        {
            2 or 1 => same ? Lang.T("Freundlich gesinnt: bietet dir als Nachbar gelegentlich Zusammenarbeit an.", "Friendly: as a neighbor, occasionally offers you cooperation.")
                : Lang.T("Freundlich gesinnt – Zusammenarbeit bieten aber nur Nachbarn im selben Gebiet an.", "Friendly – but only neighbors in the same region offer cooperation."),
            0 => Lang.T("Neutral: weder Angebote noch Ärger zu erwarten.", "Neutral: expect neither offers nor trouble."),
            -1 => same ? Lang.T("Verstimmt: kann eine Rabattaktion gegen dich starten (weniger Kundschaft ab Hof).", "Annoyed: may launch a discount campaign against you (fewer cellar-door customers).")
                : Lang.T("Verstimmt – Rabattaktionen starten aber nur Nachbarn im selben Gebiet.", "Annoyed – but only neighbors in the same region launch discount campaigns."),
            _ => Lang.En ? "Hostile: complains publicly about you, which costs reputation" + (same ? ", and launches discount campaigns." : ".") : "Verfeindet: beschwert sich öffentlich über dich, das kostet Bekanntheit" + (same ? ", und startet Rabattaktionen." : "."),
        };
    }

    static SkiaControl RivalInspector(GameSession session, Player rival)
    {
        var player = session.Player;
        var card = Inspector();
        card.With(Kit.Text(rival.Name, 32, 26, 460, 44, 30, Theme.Ink, bold: true, serif: true));
        var where = Regions.All[rival.RegionId].Name + (rival.RegionId == player.RegionId ? Lang.T(" (Nachbar)", " (neighbor)") : "");
        var y = 72.0;
        if (rival.Ai is { } ai)
        {
            var profile = Ai.AiProfiles[ai];
            card.With(Kit.Text($"{where} · {Lang.T("KI", "AI")} {profile.Name}", 32, y, 460, 26, 18, Theme.InkSoft));
            card.With(Kit.Text(profile.Description, 32, y + 30, 460, 48, 16, Theme.InkSoft, maxLines: 2));
            y += 88;
            var value = Rivals.Relation(rival, player.Id);
            var label = Rivals.RelationLabel(value);
            card.With(Kit.Row(Lang.T("Verhältnis zu dir", "Relation to you"), $"{label.Text} ({Signed(value)})", 32, y, 460,
                value >= 10 ? Theme.Good : value <= -10 ? Theme.Alert : Theme.Ink));
            y += 34;
            card.With(Kit.Text(RelationMeaning(rival, player, value), 32, y, 460, 46, 16, Theme.InkSoft, maxLines: 2));
            y += 54;
        }
        else
        {
            card.With(Kit.Text($"{where} · {Lang.T("Mitspieler am selben Gerät", "player on the same device")}", 32, y, 460, 26, 18, Theme.InkSoft));
            y += 44;
        }
        card.With(Kit.Row(Lang.T("Siegpunkte", "Victory points"), Util.FormatNumber(rival.Points), 32, y, 460));
        card.With(Kit.Row(Lang.T("Bekanntheit", "Reputation"), Util.FormatNumber(Js.Round(rival.Reputation)), 32, y + 34, 460));
        card.With(Kit.Row(Lang.T("Rebfläche", "Vine area"), Hectares(Market.TotalHectares(rival)), 32, y + 68, 460));
        card.With(Kit.Row(Lang.T("Reinvermögen", "Net worth"), Util.FormatEuro(Js.Round(Market.NetWorth(rival))), 32, y + 102, 460));
        y += 146;
        card.With(Divider(y));
        y += 16;

        // Intrigen
        card.With(Kit.Text(Lang.T("Schmutzige Tricks", "Dirty tricks"), 32, y, 460, 34, 25, Theme.Wine, bold: true, serif: true));
        y += 40;
        card.With(Kit.Text(
            Lang.En ? $"{Js.Round(Economy.SabotageDetection * 100)} % risk of getting caught: {Util.FormatEuro(Economy.SabotageFine)} fine, " +
            $"−{Util.FormatNumber(Economy.SabotageReputation)} reputation – and the victim won't forget it soon."
            : $"{Js.Round(Economy.SabotageDetection * 100)} % Risiko, erwischt zu werden: {Util.FormatEuro(Economy.SabotageFine)} Strafe, " +
            $"−{Util.FormatNumber(Economy.SabotageReputation)} Bekanntheit – und das Opfer vergisst es so schnell nicht.",
            32, y, 460, 48, 16, Theme.InkSoft, maxLines: 2));
        y += 60;
        var rumor = new GameAction.Sabotage(rival.Id, SabotageKind.Geruecht);
        card.With(Kit.Button($"{Economy.Sabotage[SabotageKind.Geruecht].Name} ({Util.FormatEuro(Economy.Sabotage[SabotageKind.Geruecht].Cost)})", 32, y, 460, 84,
            () => session.Run(rumor), ButtonKind.Primary,
            Lang.En ? $"−{Util.FormatNumber(Economy.SabotageRumorReputation)} reputation for {Short(rival.Name)}" : $"−{Util.FormatNumber(Economy.SabotageRumorReputation)} Bekanntheit bei {Short(rival.Name)}", session.Problem(rumor)));
        y += 96;
        var damage = new GameAction.Sabotage(rival.Id, SabotageKind.Weinberg);
        card.With(Kit.Button($"{Economy.Sabotage[SabotageKind.Weinberg].Name} ({Util.FormatEuro(Economy.Sabotage[SabotageKind.Weinberg].Cost)})", 32, y, 460, 84,
            () => session.Run(damage), ButtonKind.Secondary,
            Lang.En ? $"−{Js.Round(Economy.SabotageVineyardLoss * 100)} % yield on one of the victim's sites" : $"−{Js.Round(Economy.SabotageVineyardLoss * 100)} % Ertrag in einer Lage des Opfers", session.Problem(damage)));
        y += 100;

        // Limit: eine Intrige pro Monat
        var used = player.IntrigueThisMonth;
        var limit = Kit.Box(32, y, 460, 70, used ? Theme.C("#33b3261e") : Theme.C("#40d9c7a3"), 12);
        limit.With(Kit.Box(14, 25, 20, 20, used ? Theme.Alert : Theme.Good, 10));
        limit.With(Kit.Text(used ? Lang.T("Diesen Monat hast du schon etwas riskiert", "You already risked something this month") : Lang.T("Diesen Monat ist noch eine Intrige möglich", "One more intrigue is possible this month"), 46, 9, 404, 26, 17, Theme.Ink, bold: true));
        limit.With(Kit.Text(Lang.T("Höchstens eine Intrige pro Monat – Kellertricks zählen mit.", "At most one intrigue per month – cellar tricks count too."), 46, 36, 404, 24, 15, Theme.InkSoft));
        card.With(limit);
        y += 80;

        // Eigene Nachtwache
        var guard = player.Effects.FirstOrDefault(e => e.Kind == EffectKind.Guard);
        var watch = Kit.Box(32, y, 460, 92, guard is not null ? Theme.C("#2e4d7a2a") : Theme.C("#40d9c7a3"), 12);
        watch.With(Kit.Paint(10, 24, 30, 40, (c, w, h) => Lantern(c, w, h, guard is not null)));
        if (guard is not null)
        {
            watch.With(Kit.Text(Lang.En ? $"Your night watch: {guard.MonthsLeft} more {(guard.MonthsLeft == 1 ? "month" : "months")}" : $"Deine Nachtwache: noch {guard.MonthsLeft} {(guard.MonthsLeft == 1 ? "Monat" : "Monate")}", 46, 9, 404, 26, 17, Theme.Ink, bold: true));
            watch.With(Kit.Text(Lang.En ? $"Anyone who sabotages you gets caught {Js.Round(Economy.GuardDetection * 100)} % of the time." : $"Wer dich sabotiert, wird zu {Js.Round(Economy.GuardDetection * 100)} % erwischt.", 46, 36, 404, 46, 15, Theme.InkSoft, maxLines: 2));
        }
        else
        {
            watch.With(Kit.Text(Lang.T("Keine Nachtwache auf deinem Gut", "No night watch on your estate"), 46, 9, 404, 26, 17, Theme.Ink, bold: true));
            watch.With(Kit.Text(
                Lang.En ? $"If you are sabotaged, you can hire one: {Util.FormatEuro(Economy.GuardCost)} for {Economy.GuardMonths} months, saboteurs are then caught {Js.Round(Economy.GuardDetection * 100)} % of the time."
                    : $"Wirst du sabotiert, kannst du eine anheuern: {Util.FormatEuro(Economy.GuardCost)} für {Economy.GuardMonths} Monate, Saboteure fliegen dann zu {Js.Round(Economy.GuardDetection * 100)} % auf.",
                46, 36, 404, 46, 15, Theme.InkSoft, maxLines: 2));
        }
        card.With(watch);
        return card;
    }

    static string Short(string name) => name.Length > 26 ? name[..25] + "…" : name;

    static void Lantern(SKCanvas c, float w, float h, bool lit)
    {
        using var frame = Draw.Stroke(Theme.SkInk, 2.5f);
        using var glass = Draw.Fill(lit ? "#f6d477" : "#d9c7a3");
        c.DrawRoundRect(w * 0.2f, h * 0.3f, w * 0.6f, h * 0.55f, 4, 4, glass);
        c.DrawRoundRect(w * 0.2f, h * 0.3f, w * 0.6f, h * 0.55f, 4, 4, frame);
        VillagePaint.Path(c, $"M{w * 0.3f} {h * 0.3f} L{w * 0.5f} {h * 0.12f} L{w * 0.7f} {h * 0.3f}", frame);
        c.DrawLine(w * 0.15f, h * 0.88f, w * 0.85f, h * 0.88f, frame);
        if (lit)
        {
            using var flame = Draw.Fill("#b5562e");
            c.DrawOval(w * 0.5f, h * 0.6f, w * 0.1f, h * 0.14f, flame);
        }
    }

    // ---- Reiter 2: Versteigerung ----

    static void AuctionTab(GameSession session, SkiaLayout scene)
    {
        var state = session.State;
        var player = session.Player;
        var auction = state.Auction;
        var lots = auction?.Lots.Where(l => l.RegionId == player.RegionId).ToList() ?? [];

        if (auction is null || lots.Count == 0)
        {
            scene.With(NoAuction(session));
            scene.With(FinanceInspector(session, null));
            return;
        }

        var announced = auction.Phase == AuctionPhase.Announced;
        var head = Kit.Glass(28, PanelTop, 1204, 96, 22);
        head.With(Kit.Paint(22, 14, 68, 68, (c, w, _) => VillagePaint.Gavel(c, w, Theme.SkGold, Theme.SkGoldDark)));
        head.With(Kit.Text(Lang.En ? $"Vineyard auction {(announced ? $"in {Game.MonthNames[auction.Month - 1]}" : "under way")}" : $"Weinbergversteigerung {(announced ? $"im {Game.MonthNames[auction.Month - 1]}" : "läuft")}", 104, 10, 700, 36, 27, Theme.Gold, bold: true, serif: true));
        head.With(Kit.Text(announced
                ? Lang.En ? $"Live bidding next month: you bid round by round against neighbors and outside investors. Whoever pays the most gets the hammer. You can currently afford up to {Util.FormatEuro(Auction.MaxAffordableBid(player))}." : $"Nächsten Monat wird live geboten: Du bietest Runde für Runde gegen Nachbarn und auswärtige Investoren. Wer am meisten zahlt, bekommt den Zuschlag. Finanzierbar sind derzeit bis zu {Util.FormatEuro(Auction.MaxAffordableBid(player))}."
                : Lang.T("Die Versteigerung dieses Monats: Zuschläge und offene Lose in deinem Anbaugebiet.", "This month's auction: sold and open lots in your region."),
            104, 46, 1076, 46, 16, Theme.Cream, maxLines: 2));
        scene.With(head);

        var selected = lots.FirstOrDefault(l => l.Id == session.Nav.Selected(LotKey)) ?? lots[0];
        const int perPage = 3;
        var pages = Math.Max(1, (int)Math.Ceiling(lots.Count / (double)perPage));
        var page = session.Nav.Page("dorf.lose", pages);
        var shown = lots.Skip(page * perPage).Take(perPage).ToList();
        for (var i = 0; i < shown.Count; i++)
        {
            scene.With(LotCard(session, shown[i], lots.IndexOf(shown[i]) + 1, lots.Count, 28 + i * 406, PanelTop + 110, shown[i].Id == selected.Id));
        }
        if (shown.Count < perPage) scene.With(Bidders(session, 28 + shown.Count * 406, PanelTop + 110, 1204 - shown.Count * 406));
        if (pages > 1)
        {
            scene.With(Kit.Box(940, 388, 292, 44, Theme.C("#d91c1418"), 22));
            scene.With(Kit.Pager(960, 392, 260, page, pages, p => session.Nav.SetPage("dorf.lose", p)));
        }
        scene.With(FinanceInspector(session, selected));
    }

    /// <summary>Füllt den Platz neben wenigen Losen: Wer im eigenen Gebiet mitbieten dürfte</summary>
    static SkiaControl Bidders(GameSession session, double x, double y, double w)
    {
        var state = session.State;
        var player = session.Player;
        var panel = Kit.Glass(x, y, w, 410, 20);
        panel.With(Kit.Text(Lang.T("Wer bietet mit?", "Who is bidding?"), 26, 18, w - 52, 34, 25, Theme.Gold, bold: true, serif: true));
        panel.With(Kit.Text(Lang.T("Lose gehen nur an Weingüter im selben Anbaugebiet – oder an auswärtige Investoren.", "Lots only go to estates in the same region – or to outside investors."), 26, 56, w - 52, 46, 16, Theme.CreamSoft, maxLines: 2));
        var rows = state.Players.Where(p => p.Id != player.Id && !p.Bankrupt && p.RegionId == player.RegionId).Take(3).ToList();
        var ry = 112.0;
        foreach (var rival in rows)
        {
            int? level = rival.Ai is null ? null : VillagePaint.RelationLevel(Rivals.Relation(rival, player.Id));
            var row = Kit.Box(18, ry, w - 36, 64, Theme.C("#14f4ead7"), 12);
            row.With(Kit.Paint(12, 10, 44, 44, (c, s, _) => VillagePaint.Face(c, s, level)));
            row.With(Kit.Text(rival.Name, 68, 7, w - 290, 28, 19, Theme.Cream, bold: true));
            row.With(Kit.Text(rival.Ai is { } ai ? $"{Lang.T("KI", "AI")} {Ai.AiProfiles[ai].Name}" : Lang.T("Mitspieler – bietet live", "Player – bids live"), 68, 34, w - 290, 24, 15, Theme.CreamSoft));
            row.With(Kit.Text(Hectares(Market.TotalHectares(rival)), w - 220, 18, 170, 28, 19, Theme.Cream, align: Align.End));
            panel.With(row);
            ry += 72;
        }
        var investors = Kit.Box(18, ry, w - 36, 64, Theme.C("#14f4ead7"), 12);
        investors.With(Kit.Box(22, 20, 24, 24, Theme.C("#9cb8d0"), 12));
        investors.With(Kit.Text(Lang.T("Auswärtige Investoren", "Outside investors"), 68, 7, w - 120, 28, 19, Theme.Cream, bold: true));
        investors.With(Kit.Text(Lang.T("bieten nicht bei jedem Los – und selten weit über dem Schätzwert", "don't bid on every lot – and rarely far above the estimate"), 68, 34, w - 120, 24, 15, Theme.CreamSoft));
        panel.With(investors);
        return panel;
    }
    static string GrapeLine(GameState state, AuctionLot lot) =>
        lot.Grape is { } grape && lot.PlantedYear is { } planted
            ? Lang.En ? $"{Grapes.All[grape].Name}, {state.Year - planted} years old – bears fruit right away" : $"{Grapes.All[grape].Name}, {state.Year - planted} Jahre alt – trägt sofort"
            : Lang.T("Brache – muss erst bepflanzt werden", "Fallow – must be planted first");

    static (string Text, Color Fill, Color Ink)? LotStatusChip(GameSession session, AuctionLot lot)
    {
        var player = session.Player;
        if (lot.Status == LotStatus.Sold && !string.IsNullOrEmpty(lot.Leader))
        {
            var mine = lot.Leader == player.Id;
            return (Lang.En ? $"Sold for {Util.FormatEuro(lot.Price)} to {(mine ? "you" : Auction.BidderName(session.State, lot.Leader, true))}" : $"Zuschlag für {Util.FormatEuro(lot.Price)} an {(mine ? "dich" : Auction.BidderName(session.State, lot.Leader, true))}",
                mine ? Theme.Good : Theme.PaperLine, mine ? Colors.White : Theme.Ink);
        }
        if (lot.Status == LotStatus.Unsold) return (Lang.T("nicht verkauft", "unsold"), Theme.PaperLine, Theme.Ink);
        if (lot.Status == LotStatus.Open) return (Lang.T("wird gerade versteigert", "being auctioned now"), Theme.Gold, Theme.Ink);
        return null;
    }

    static SkiaControl LotCard(GameSession session, AuctionLot lot, int number, int count, double x, double y, bool selected)
    {
        var state = session.State;
        const double w = 392, h = 410;
        var card = Kit.Card(x, y, w, h, 20);
        // Auch ohne Auswahl ein (unsichtbarer) Rand, damit der Inhalt nicht springt
        card.StrokeColor = selected ? Theme.Wine : Theme.PaperLine;
        card.StrokeWidth = 5 * Kit.S;
        card.With(Kit.Paint(14, 14, w - 28, 148, (c, pw, ph) => VillagePaint.Parcel(c, pw, ph, lot, state.Month)));
        card.With(Kit.Box(w - 118, 124, 92, 28, Theme.C("#d91c1418"), 14)
            .With(Kit.Text(Lang.En ? $"Lot {number} of {count}" : $"Los {number} von {count}", 0, 4, 92, 22, 14, Theme.Cream, align: Align.Center)));
        card.With(Kit.Text(lot.Name, 22, 170, w - 44, 36, 27, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text($"{Hectares(lot.Hectares)} · {Auction.SiteLabels[lot.Site]}", 22, 206, w - 44, 26, 18, Theme.Wine, bold: true));
        card.With(Kit.Text(GrapeLine(state, lot), 22, 234, w - 44, 44, 16, Theme.InkSoft, maxLines: 2));
        card.With(Kit.Row(Lang.T("Schätzwert", "Estimate"), Util.FormatEuro(lot.Estimate), 22, 284, w - 44, size: 19));
        card.With(Kit.Row(Lang.T("Ausrufpreis", "Opening price"), Util.FormatEuro(lot.StartPrice), 22, 316, w - 44, Theme.Wine, 19));
        if (LotStatusChip(session, lot) is { } chip)
        {
            card.With(Kit.Box(22, 358, w - 44, 36, chip.Fill, 18)
                .With(Kit.Text(chip.Text, 12, 7, w - 68, 24, 15, chip.Ink, bold: true, align: Align.Center)));
        }
        else
        {
            var affordable = Auction.MaxAffordableBid(session.Player) >= lot.StartPrice;
            card.With(Kit.Box(22, 358, w - 44, 36, affordable ? Theme.C("#2e4d7a2a") : Theme.C("#33b3261e"), 18)
                .With(Kit.Text(affordable ? Lang.T("Ausruf für dich finanzierbar", "Opening price affordable for you") : Lang.T("Ausruf derzeit nicht finanzierbar", "Opening price currently not affordable"), 12, 7, w - 68, 24, 15,
                    affordable ? Theme.Good : Theme.Alert, bold: true, align: Align.Center)));
        }
        var id = lot.Id;
        card.Tap(() => session.Nav.Select(LotKey, id));
        return card;
    }

    /// <summary>Kein Termin angesetzt: Aushang am Rathaus und die letzten Zuschläge</summary>
    static SkiaControl NoAuction(GameSession session)
    {
        var state = session.State;
        var panel = Kit.Glass(28, PanelTop, 1204, PanelHeight, 22);
        panel.With(Kit.Paint(40, 36, 150, 150, (c, w, _) => VillagePaint.Gavel(c, w, Theme.SkGold, Theme.SkGoldDark)));
        panel.With(Kit.Text(Lang.T("Zurzeit ist keine Versteigerung angekündigt", "No auction is currently announced"), 216, 34, 950, 42, 30, Theme.Gold, bold: true, serif: true));
        var (min, max) = Economy.AuctionInterval;
        panel.With(Kit.Text(
            Lang.En ? $"Roughly every {Util.FormatNumber(min)} to {Util.FormatNumber(max)} months, one to three vineyards in each region go under the hammer. " +
            "The lots are posted here a month ahead, with live bidding the following month – round by round against neighbors and outside investors."
            : $"Etwa alle {Util.FormatNumber(min)} bis {Util.FormatNumber(max)} Monate kommen in jedem Anbaugebiet ein bis drei Weinberge unter den Hammer. " +
            "Die Lose werden einen Monat vorher hier ausgehängt, geboten wird im Folgemonat live – Runde für Runde gegen Nachbarn und auswärtige Investoren.",
            216, 84, 950, 84, 19, Theme.Cream, maxLines: 3));

        var past = state.Log.Where(e => e.Text.StartsWith("🔨")).Reverse().Take(4).ToList();
        panel.With(Kit.Box(28, 204, 1148, 2, Theme.GlassLine));
        panel.With(Kit.Text(Lang.T("Letzte Zuschläge", "Recent sales"), 28, 222, 600, 34, 24, Theme.Gold, bold: true, serif: true));
        if (past.Count == 0)
        {
            panel.With(Kit.Text(Lang.T("Bisher wurde noch kein Weinberg versteigert.", "No vineyard has been auctioned yet."), 28, 268, 1100, 30, 19, Theme.CreamSoft));
        }
        var y = 266.0;
        foreach (var entry in past)
        {
            var row = Kit.Box(28, y, 1148, 54, Theme.C("#14f4ead7"), 12);
            row.With(Kit.Text(Lang.En ? $"{Game.MonthNames[entry.Month - 1]}, year {entry.Year}" : $"{Game.MonthNames[entry.Month - 1]}, Jahr {entry.Year}", 18, 14, 190, 26, 17, Theme.Gold, bold: true));
            row.With(Kit.Text(entry.Text.Replace("🔨", "").Trim(), 214, 14, 916, 26, 17, Theme.Cream));
            panel.With(row);
            y += 61;
        }
        return panel;
    }

    /// <summary>Inspektor der Versteigerung: gewähltes Los, eigene Finanzkraft, Ablauf</summary>
    static SkiaControl FinanceInspector(GameSession session, AuctionLot? lot)
    {
        var state = session.State;
        var player = session.Player;
        var card = Inspector();
        var y = 26.0;
        if (lot is not null)
        {
            card.With(Kit.Text(lot.Name, 32, y, 460, 44, 32, Theme.Ink, bold: true, serif: true));
            card.With(Kit.Text($"{Hectares(lot.Hectares)} · {Auction.SiteLabels[lot.Site]} · {Regions.All[lot.RegionId].Name}", 32, y + 48, 460, 26, 18, Theme.InkSoft));
            card.With(Kit.Text(GrapeLine(state, lot), 32, y + 78, 460, 26, 18, Theme.InkSoft));
            y += 122;
            card.With(Kit.Row(Lang.T("Schätzwert", "Estimate"), Util.FormatEuro(lot.Estimate), 32, y, 460));
            card.With(Kit.Row(Lang.T("Ausrufpreis", "Opening price"), Util.FormatEuro(lot.StartPrice), 32, y + 34, 460, Theme.Wine));
            card.With(Kit.Row(Lang.T("Gebotsschritt", "Bid increment"), Util.FormatEuro(lot.Increment), 32, y + 68, 460));
            card.With(Kit.Row(Lang.T("Schätzwert je Hektar", "Estimate per hectare"), Util.FormatEuro(Js.Round(lot.Estimate / lot.Hectares)), 32, y + 102, 460));
            y += 148;
            card.With(Divider(y));
            y += 18;
        }
        card.With(Kit.Text(Lang.T("Deine Finanzkraft", "Your finances"), 32, y, 460, lot is null ? 44 : 34, lot is null ? 32 : 25, lot is null ? Theme.Ink : Theme.Wine, bold: true, serif: true));
        y += lot is null ? 58 : 44;
        var affordable = Auction.MaxAffordableBid(player);
        card.With(Kit.Row(Lang.T("Konto", "Account"), Util.FormatEuro(player.Money), 32, y, 460, player.Money < 0 ? Theme.Alert : null));
        card.With(Kit.Row(Lang.T("Freier Kreditrahmen", "Available credit"), Util.FormatEuro(Market.AvailableCredit(player)), 32, y + 34, 460));
        card.With(Kit.Row(Lang.T("Finanzierbar bis", "Affordable up to"), Util.FormatEuro(affordable), 32, y + 68, 460, Theme.Wine));
        y += 110;
        if (lot is not null && lot.Status is LotStatus.Pending or LotStatus.Open)
        {
            // Wie weit die eigene Finanzkraft reicht, gemessen am Schätzwert
            var reach = affordable / lot.Estimate;
            var ok = affordable >= lot.StartPrice;
            var box = Kit.Box(32, y, 460, 84, ok ? Theme.C("#2e4d7a2a") : Theme.C("#33b3261e"), 12);
            box.With(Kit.Text(ok ? (Lang.En ? $"You can go up to {Js.Round(reach * 100)} % of the estimate" : $"Du kannst bis {Js.Round(reach * 100)} % des Schätzwerts mitgehen") : Lang.T("Schon der Ausruf ist nicht finanzierbar", "Even the opening price is not affordable"),
                16, 10, 428, 26, 17, ok ? Theme.Good : Theme.Alert, bold: true));
            box.With(Kit.Box(16, 46, 428, 10, Theme.PaperLine, 5));
            box.With(Kit.Box(16, 46, Math.Max(10, 428 * Math.Clamp(reach / 1.5, 0, 1)), 10, ok ? Theme.Good : Theme.Alert, 5));
            box.With(Kit.Box(16 + 428 * (1 / 1.5) - 1.5, 41, 3, 20, Theme.Ink, 1.5));
            box.With(Kit.Text(Lang.T("Marke = Schätzwert", "Mark = estimate"), 16, 60, 428, 20, 13, Theme.InkSoft, align: Align.Center));
            card.With(box);
            y += 98;
        }
        card.With(Divider(y));
        y += 18;
        card.With(Kit.Text(Lang.T("So läuft die Versteigerung", "How the auction works"), 32, y, 460, 34, 25, Theme.Wine, bold: true, serif: true));
        y += 44;
        string[] steps = Lang.En ?
        [
            "Humans bid live in the auction month – there is no sealed advance bid for you.",
            "Computer rivals and outside investors have set maximum bids and outbid you step by step.",
            "Any shortfall is automatically covered by credit when the hammer falls.",
            "Anyone who bid and lost won't think kindly of the winner.",
        ] :
        [
            "Menschen bieten live im Versteigerungsmonat – ein verdecktes Vorabgebot gibt es für dich nicht.",
            "Computergegner und auswärtige Investoren haben Höchstgebote hinterlegt und überbieten Schritt für Schritt.",
            "Ein Fehlbetrag wird beim Zuschlag automatisch über den Kredit gedeckt.",
            "Wer mitgeboten und verloren hat, ist auf den Gewinner nicht gut zu sprechen.",
        ];
        for (var i = 0; i < steps.Length && y < 880; i++)
        {
            card.With(Kit.Box(32, y + 2, 28, 28, Theme.Wine, 14)
                .With(Kit.Text((i + 1).ToString(), 0, 3, 28, 22, 16, Theme.Paper, bold: true, align: Align.Center)));
            card.With(Kit.Text(steps[i], 72, y, 420, 48, 16, Theme.Ink, maxLines: 2));
            y += 58;
        }
        return card;
    }
}
