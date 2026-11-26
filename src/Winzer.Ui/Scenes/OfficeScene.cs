using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

/// <summary>
/// Kontor: das helle Schreibzimmer des Weinguts. Vier Reiter – Finanzen (Konto, Kredit, laufende Kosten),
/// Ausbau (Online-Shop, Vinothek, Kellerkapazität, Personal), Chronik (Logbuch als Zeitleiste) und Erfolge (Meilensteine).
/// </summary>
public static class OfficeScene
{
    static string[] TabNames => Lang.En ? ["Finances", "Upgrades", "Chronicle", "Achievements"] : ["Finanzen", "Ausbau", "Chronik", "Erfolge"];

    static readonly string[] CostLabelsEn = ["Staff", "Vineyards", "Crop protection", "Interest"];

    /// <summary>Kostenarten in fester Reihenfolge und Farbe – für Ring, Legende und Jahresverlauf</summary>
    static readonly (string Label, string Hex)[] CostKinds =
    [
        ("Personal", "#8e2440"),
        ("Weinberge", "#6f9a50"),
        ("Pflanzenschutz", "#d1a12c"),
        ("Zinsen", "#4a7fb5"),
    ];

    static readonly (string Key, double Value)[] QuickAmounts =
    [
        ("5000", 5_000), ("10000", 10_000), ("25000", 25_000), ("50000", 50_000),
    ];

    const string AmountKey = "kontor.betrag";

    public static SkiaControl Build(GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var scene = Kit.Layer(0, 0, Shell.StageWidth, Shell.StageHeight);
        scene.With(Kit.Paint(0, 0, Shell.StageWidth, Shell.StageHeight, PaintRoom));

        var tab = Math.Clamp(session.Nav.Tab, 0, TabNames.Length - 1);
        var badges = new[] { player.MonthsInsolvent > 0 ? 1 : 0, 0, 0, 0 };
        scene.With(Kit.Tabs(28, 20, TabNames, tab, session.Nav.SetTab, badges));

        string caption;
        switch (tab)
        {
            case 1:
                caption = Lang.En ? $"Cellar-door sales: {Util.FormatNumber(RulesV2.DirectDemand(state, player))} bottles per month" : $"Absatz ab Hof: {Util.FormatNumber(RulesV2.DirectDemand(state, player))} Flaschen im Monat";
                Expansion(scene, session);
                break;
            case 2:
                caption = OfficeChronicle.Build(scene, session);
                break;
            case 3:
                caption = OfficeMilestones.Build(scene, session);
                break;
            default:
                caption = Lang.En ? $"Running costs in {Game.MonthNames[state.Month - 1]}: {Util.FormatEuro(Market.MonthlyCosts(player, state.Month).Total)}" : $"Laufende Kosten im {Game.MonthNames[state.Month - 1]}: {Util.FormatEuro(Market.MonthlyCosts(player, state.Month).Total)}";
                Finance(scene, session);
                break;
        }
        scene.With(Kit.Text(caption, 640, 31, 590, 30, 20, Theme.CreamSoft, align: Align.End));
        return scene;
    }

    // ---- Reiter 0: Finanzen ----

    static void Finance(SkiaLayout scene, GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var costs = Market.MonthlyCosts(player, state.Month);
        var limit = Market.CreditLimit(player);
        var free = Market.AvailableCredit(player);

        // Drei Kennzahlen
        var konto = OfficeKit.Stat(28, Lang.T("Konto", "Account"), Util.FormatEuro(player.Money), player.Money < 0 ? Theme.Alert : Theme.Ink,
            player.Money < 0
                ? (Lang.En ? $"Overdraft costs {Js.Round(Economy.OverdraftInterestRate * 100)} % interest p. a." : $"Minus kostet {Js.Round(Economy.OverdraftInterestRate * 100)} % Zinsen p. a.")
                : (Lang.En ? $"after monthly costs: {Util.FormatEuro(player.Money - costs.Total)}" : $"nach den Monatskosten: {Util.FormatEuro(player.Money - costs.Total)}"),
            "#c8962e");
        var kredit = OfficeKit.Stat(435, Lang.T("Kredit", "Loan"), Util.FormatEuro(player.Loan), Theme.Ink,
            Lang.En ? $"Limit {Util.FormatEuro(limit)} · free {Util.FormatEuro(free)}" : $"Rahmen {Util.FormatEuro(limit)} · frei {Util.FormatEuro(free)}", "#4a7fb5");
        kredit.With(OfficeKit.Meter(250, 44, 110, 10, limit > 0 ? player.Loan / limit : 0, Theme.C("#4a7fb5")));
        var worth = OfficeKit.Stat(842, Lang.T("Reinvermögen", "Net worth"), Util.FormatEuro(Market.NetWorth(player)), Theme.Ink,
            Lang.En ? $"Business value {Util.FormatEuro(Market.AssetValue(player))} + account − loan" : $"Betriebswert {Util.FormatEuro(Market.AssetValue(player))} + Konto − Kredit", "#4d7a2a");
        scene.With(konto, kredit, worth);

        scene.With(CostCard(state, player, costs));
        scene.With(CreditInspector(session, costs, limit, free));
    }

    static double[] Parts(MonthlyCosts costs) => [costs.Salaries, costs.Upkeep, costs.Protection, costs.Interest];

    static SkiaControl CostCard(GameState state, Player player, MonthlyCosts costs)
    {
        var card = Kit.Card(28, 262, 1202, 698);
        var month = Game.MonthNames[state.Month - 1];
        card.With(Kit.Text(Lang.En ? $"Running costs in {month}" : $"Laufende Kosten im {month}", 36, 22, 700, 44, 30, Theme.Ink, bold: true, serif: true));
        // Erweiterte Regeln: Der Winterzug bucht Januar und Februar ab, Ausbauten kosten zusätzlich Unterhalt
        var winter = RulesV2.On(state) && state.Month == 1;
        var buildings = RulesV2.On(state) ? player.Buildings.Sum(id => RulesV2.Building(id)?.MonthlyCost ?? 0) : 0;
        var when = winter ? Lang.T("im Winterzug für Januar und Februar abgebucht", "debited in the winter turn for January and February") : Lang.T("werden am Monatsende abgebucht", "debited at the end of the month");
        if (buildings > 0) when += Lang.En ? $" · plus {Util.FormatEuro(buildings)} upkeep for estate upgrades" : $" · dazu {Util.FormatEuro(buildings)} Unterhalt für den Gutsausbau";
        card.With(Kit.Text(when, 520, 34, 646, 28, 18, Theme.InkSoft, align: Align.End));

        // Ringdiagramm
        var parts = Parts(costs);
        var sum = parts.Sum();
        card.With(Kit.Paint(36, 80, 290, 290, (c, w, h) =>
        {
            using var track = Draw.Stroke(SKColor.Parse("#d9c7a3"), 46);
            c.DrawCircle(145, 145, 112, track);
            if (sum <= 0) return;
            var rect = new SKRect(33, 33, 257, 257);
            var start = -90f;
            for (var i = 0; i < parts.Length; i++)
            {
                var sweep = (float)(parts[i] / sum * 360);
                if (sweep <= 0) continue;
                using var paint = Draw.Stroke(SKColor.Parse(CostKinds[i].Hex), 46);
                paint.StrokeCap = SKStrokeCap.Butt;
                var gap = sweep > 4 ? 1.2f : 0;
                c.DrawArc(rect, start + gap, sweep - 2 * gap, false, paint);
                start += sweep;
            }
        }));
        card.With(Kit.Text(Util.FormatEuro(costs.Total), 76, 190, 210, 44, 30, Theme.Ink, bold: true, serif: true, align: Align.Center));
        card.With(Kit.Text(Lang.T("im Monat", "per month"), 76, 232, 210, 26, 17, Theme.InkSoft, align: Align.Center));

        // Legende mit Beträgen
        var fte = Market.StaffFte(player);
        var first = Game.MonthNames[Economy.ProtectionMonths.Min() - 1];
        var last = Game.MonthNames[Economy.ProtectionMonths.Max() - 1];
        string[] notes =
        [
            $"{OfficeKit.Staff(fte)}{(player.CellarMaster is not null ? Lang.T(" + Kellermeister", " + cellar master") : "")}{(player.HasVinothek ? Lang.T(" + Vinothek", " + wine room") : "")}",
            Lang.En ? $"Care of {Util.FormatNumber(Market.TotalHectares(player), 1)} ha, of which {Util.FormatNumber(Market.PlantedHectares(player), 1)} ha planted" : $"Pflege von {Util.FormatNumber(Market.TotalHectares(player), 1)} ha, davon {Util.FormatNumber(Market.PlantedHectares(player), 1)} ha bestockt",
            costs.Protection > 0 ? (Lang.En ? $"Spraying season {first} to {last}" : $"Spritzsaison {first} bis {last}") : (Lang.En ? $"only incurred from {first} to {last}" : $"fällt nur von {first} bis {last} an"),
            Lang.En ? $"{Js.Round(Economy.LoanInterestRate * 100)} % p. a. on the loan{(player.Money < 0 ? $", {Js.Round(Economy.OverdraftInterestRate * 100)} % on the overdraft" : "")}" : $"{Js.Round(Economy.LoanInterestRate * 100)} % p. a. auf den Kredit{(player.Money < 0 ? $", {Js.Round(Economy.OverdraftInterestRate * 100)} % auf das Minus" : "")}",
        ];
        for (var i = 0; i < parts.Length; i++)
        {
            var y = 84 + i * 72;
            var row = Kit.Layer(372, y, 794, 66);
            row.With(Kit.Box(0, 5, 20, 20, Theme.C(CostKinds[i].Hex), 6));
            row.With(Kit.Text(Lang.En ? CostLabelsEn[i] : CostKinds[i].Label, 34, 0, 360, 32, 22, Theme.Ink, bold: true));
            row.With(Kit.Text(notes[i], 34, 31, 540, 26, 17, Theme.InkSoft));
            row.With(Kit.Text(parts[i] > 0 ? Util.FormatEuro(Js.Round(parts[i])) : "–", 574, 0, 220, 32, 22, Theme.Ink, bold: true, align: Align.End));
            row.With(Kit.Text(sum > 0 && parts[i] > 0 ? $"{Js.Round(parts[i] / sum * 100)} %" : "", 574, 31, 220, 26, 17, Theme.InkSoft, align: Align.End));
            if (i < parts.Length - 1) row.With(Kit.Box(0, 64, 794, 2, Theme.PaperLine));
            card.With(row);
        }

        // Jahresverlauf: die Kosten jedes Monats nach heutigem Stand
        card.With(Kit.Box(36, 384, 1130, 2, Theme.PaperLine));
        card.With(Kit.Text(Lang.T("Jahresverlauf", "Over the year"), 36, 398, 300, 34, 23, Theme.Wine, bold: true, serif: true));
        card.With(Kit.Text(Lang.T("Monatskosten nach heutigem Stand, in Tausend Euro", "Monthly costs as of today, in thousand euros"), 566, 404, 600, 26, 17, Theme.InkSoft, align: Align.End));
        var year = Enumerable.Range(1, 12).Select(m => Market.MonthlyCosts(player, m)).ToArray();
        var top = Math.Max(1, year.Max(c => c.Total));
        const float chartHeight = 196, barSpace = 166, slot = 1130f / 12, barWidth = 58;
        var current = state.Month - 1;
        card.With(Kit.Paint(36, 444, 1130, chartHeight, (c, w, h) =>
        {
            using var grid = Draw.Stroke(SKColor.Parse("#d9c7a3").WithAlpha(150), 1.5f);
            for (var i = 1; i <= 3; i++) c.DrawLine(0, h - barSpace * i / 3, w, h - barSpace * i / 3, grid);
            using (var mark = Draw.Fill(Theme.SkGold.WithAlpha(70)))
                c.DrawRoundRect(current * slot + 6, 0, slot - 12, h, 12, 12, mark);
            for (var m = 0; m < 12; m++)
            {
                var values = Parts(year[m]);
                var total = values.Sum();
                var height = (float)(total / top * barSpace);
                if (height <= 0) continue;
                var left = m * slot + (slot - barWidth) / 2;
                using var clip = new SKPath();
                clip.AddRoundRect(new SKRoundRect(new SKRect(left, h - height, left + barWidth, h + 8), 8));
                c.Save();
                c.ClipPath(clip, antialias: true);
                var bottom = h;
                for (var i = 0; i < values.Length; i++)
                {
                    var part = (float)(values[i] / top * barSpace);
                    if (part <= 0) continue;
                    using var fill = Draw.Fill(SKColor.Parse(CostKinds[i].Hex).WithAlpha((byte)(m == current ? 255 : 190)));
                    c.DrawRect(left, bottom - part, barWidth, part + 0.5f, fill);
                    bottom -= part;
                }
                c.Restore();
            }
            using var line = Draw.Stroke(SKColor.Parse("#6a4a2a"), 2);
            c.DrawLine(0, h - 1, w, h - 1, line);
        }));
        for (var m = 0; m < 12; m++)
        {
            var on = m == current;
            var height = year[m].Total / top * barSpace;
            card.With(Kit.Text(Util.FormatNumber(year[m].Total / 1000, 1), 36 + m * slot, 444 + chartHeight - height - 26, slot, 22, 15,
                on ? Theme.Ink : Theme.InkSoft, bold: on, align: Align.Center));
            card.With(Kit.Text(Game.MonthNames[m][..3], 36 + m * slot, 646, slot, 26, 17, on ? Theme.Wine : Theme.InkSoft, bold: on, align: Align.Center));
        }
        return card;
    }

    static SkiaControl CreditInspector(GameSession session, MonthlyCosts costs, double limit, double free)
    {
        var player = session.Player;
        var card = OfficeKit.Inspector();
        card.With(Kit.Text(Lang.T("Kredit", "Loan"), 32, 26, 460, 44, 32, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(Lang.En ? $"The bank lends up to {Js.Round(Economy.LoanToValue * 100)} % of the business value." : $"Die Bank leiht bis zu {Js.Round(Economy.LoanToValue * 100)} % des Betriebswerts.", 32, 70, 460, 28, 18, Theme.InkSoft));

        var used = limit > 0 ? Math.Clamp(player.Loan / limit, 0, 1) : 0;
        card.With(Kit.Paint(32, 114, 132, 132, (c, w, h) =>
            Draw.Ring(c, 66, 66, 54, 16, used, SKColor.Parse("#d9c7a3"), used >= 1 ? SKColor.Parse("#b3261e") : Theme.SkWine)));
        card.With(Kit.Text($"{Js.Round(used * 100)} %", 32, 158, 132, 40, 28, Theme.Ink, bold: true, serif: true, align: Align.Center));
        card.With(Kit.Row(Lang.T("Kredit", "Loan"), Util.FormatEuro(player.Loan), 192, 118, 300));
        card.With(Kit.Row(Lang.T("Rahmen", "Limit"), Util.FormatEuro(limit), 192, 156, 300));
        card.With(Kit.Row(Lang.T("Noch frei", "Still free"), Util.FormatEuro(free), 192, 194, 300, free <= 0 ? Theme.Alert : Theme.Good));

        card.With(Kit.Box(32, 262, 460, 2, Theme.PaperLine));
        card.With(Kit.Row(Lang.T("Zinssatz", "Interest rate"), $"{Js.Round(Economy.LoanInterestRate * 100)} % p. a.", 32, 276, 460));
        card.With(Kit.Row(Lang.T("Zinsen diesen Monat", "Interest this month"), Util.FormatEuro(Js.Round(costs.Interest)), 32, 312, 460));
        card.With(Kit.Box(32, 356, 460, 2, Theme.PaperLine));

        // Betrag über Schnellwahl – keine Texteingabe
        var key = session.Nav.Selected(AmountKey) ?? "10000";
        var isMax = key == "max";
        var chosen = QuickAmounts.FirstOrDefault(a => a.Key == key).Value;
        var maxBorrow = free;
        var maxRepay = Math.Max(0, Math.Min(player.Loan, Math.Floor(player.Money)));
        var borrow = isMax ? maxBorrow : chosen;
        var repay = isMax ? maxRepay : chosen;
        card.With(Kit.Text(Lang.T("Betrag wählen", "Choose amount"), 32, 370, 460, 30, 21, Theme.Ink, bold: true));
        for (var i = 0; i < QuickAmounts.Length; i++)
        {
            var amount = QuickAmounts[i];
            card.With(OfficeKit.Chip(Util.FormatNumber(amount.Value), 32 + i * 117, 408, 109, 46, amount.Key == key,
                () => session.Nav.Select(AmountKey, amount.Key)));
        }
        card.With(OfficeKit.Chip(Lang.En ? $"Maximum · borrow {Util.FormatEuro(maxBorrow)} · repay {Util.FormatEuro(maxRepay)}" : $"Maximum · leihen {Util.FormatEuro(maxBorrow)} · tilgen {Util.FormatEuro(maxRepay)}", 32, 464, 460, 46, isMax,
            () => session.Nav.Select(AmountKey, "max"), 17));

        var rate = Economy.LoanInterestRate / 12;
        var over = session.Over ? Lang.T("Die Partie ist beendet.", "The game is over.") : null;
        var borrowProblem = over ?? (borrow <= 0 || free <= 0 ? Lang.T("Der Kreditrahmen ist ausgeschöpft.", "The credit limit is used up.") : session.Problem(new GameAction.Borrow(borrow)));
        card.With(Kit.Button(Lang.En ? $"Borrow {Util.FormatEuro(borrow)}" : $"{Util.FormatEuro(borrow)} aufnehmen", 32, 530, 460, 84, () => session.Run(new GameAction.Borrow(borrow)),
            ButtonKind.Primary, Lang.En ? $"straight to your account · interest + {Util.FormatEuro(Js.Round(borrow * rate))} per month" : $"sofort aufs Konto · Zinsen + {Util.FormatEuro(Js.Round(borrow * rate))} im Monat", borrowProblem));
        var repayProblem = over is not null ? over
            : player.Loan <= 0 ? Lang.T("Es ist kein Kredit offen.", "There is no open loan.")
            : repay <= 0 ? Lang.T("Nicht genug Geld auf dem Konto.", "Not enough money in the account.")
            : session.Problem(new GameAction.Repay(repay)) is { } problem
                ? (repay > player.Loan ? (Lang.En ? $"Only {Util.FormatEuro(player.Loan)} is outstanding." : $"Offen sind nur {Util.FormatEuro(player.Loan)}.") : problem)
                : null;
        card.With(Kit.Button(Lang.En ? $"Repay {Util.FormatEuro(repay)}" : $"{Util.FormatEuro(repay)} tilgen", 32, 626, 460, 84, () => session.Run(new GameAction.Repay(repay)),
            ButtonKind.Secondary, Lang.En ? $"from your account · saves {Util.FormatEuro(Js.Round(repay * rate))} interest per month" : $"vom Konto · spart {Util.FormatEuro(Js.Round(repay * rate))} Zinsen im Monat", repayProblem));

        // Insolvenzwarnung oder Erklärung
        if (player.MonthsInsolvent > 0)
        {
            var left = Economy.InsolvencyMonths - player.MonthsInsolvent;
            var box = Kit.Box(32, 734, 460, 182, Theme.C("#1fb3261e"), 16, Theme.Alert, 3);
            box.With(Kit.Paint(20, 22, 44, 44, OfficeKit.WarningSign));
            box.With(Kit.Text(Lang.T("Insolvenzwarnung!", "Insolvency warning!"), 78, 22, 360, 40, 27, Theme.Alert, bold: true, serif: true));
            box.With(Kit.Text(
                Lang.En ? $"Your account is overdrawn and no credit limit is left. {left} more month{(left == 1 ? "" : "s")} until insolvency – sell wine or land." : $"Das Konto ist im Minus und es ist kein Kreditrahmen mehr frei. Noch {left} Monat{(left == 1 ? "" : "e")} bis zur Insolvenz – verkaufe Wein oder Land.",
                20, 76, 420, 96, 18, Theme.Ink, maxLines: 4));
            card.With(box);
        }
        else
        {
            var box = Kit.Box(32, 734, 460, 182, Theme.C("#33d9c7a3"), 16);
            box.With(Kit.Text(Lang.T("Gut zu wissen", "Good to know"), 20, 16, 420, 30, 20, Theme.Wine, bold: true, serif: true));
            box.With(Kit.Text(
                Lang.En ? $"An overdrawn account costs {Js.Round(Economy.OverdraftInterestRate * 100)} % interest per year. If the account is overdrawn and the credit limit is used up, insolvency follows after {Economy.InsolvencyMonths} months. More land, vines and tanks raise the limit." : $"Ein Minus auf dem Konto kostet {Js.Round(Economy.OverdraftInterestRate * 100)} % Zinsen im Jahr. Ist das Konto im Minus und der Kreditrahmen ausgeschöpft, folgt nach {Economy.InsolvencyMonths} Monaten die Insolvenz. Mehr Land, Reben und Tanks erhöhen den Rahmen.",
                20, 50, 420, 124, 17, Theme.InkSoft, maxLines: 5));
            card.With(box);
        }
        return card;
    }

    // ---- Reiter 1: Ausbau ----

    static void Expansion(SkiaLayout scene, GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        // Nachfrage samt Preisaufschlag und Gutsausbau der erweiterten Regeln
        double Demand(Player p) => RulesV2.DirectDemand(state, p);
        var demand = Demand(player);

        // Online-Shop
        var withShop = player.Clone();
        withShop.HasOnlineShop = true;
        var shopGain = Demand(withShop) - demand;
        var shop = UpgradeCard(28, Lang.T("Online-Shop", "Online shop"), player.HasOnlineShop, Lang.T("läuft", "running"),
            Lang.T("Verkauft zusätzlich ins ganze Land – je bekannter das Gut, desto mehr Bestellungen.", "Sells across the whole country as well – the better known the estate, the more orders."),
            (c, w, h) => PaintShop(c, w, h, player.HasOnlineShop));
        shop.With(Kit.Row(Lang.T("Absatz ab Hof im Monat", "Cellar-door sales per month"),
            player.HasOnlineShop ? $"{Util.FormatNumber(demand)} {Lang.T("Fl.", "btl.")}" : $"{Util.FormatNumber(demand)} → {Util.FormatNumber(demand + shopGain)} {Lang.T("Fl.", "btl.")}", 24, 300, 543, size: 19));
        shop.With(Kit.Row(Lang.T("Laufende Kosten", "Running costs"), Lang.T("keine", "none"), 24, 332, 543, size: 19));
        if (player.HasOnlineShop) shop.With(OfficeKit.Done(Lang.T("Der Online-Shop läuft", "The online shop is running"), 16, 372, 559, 64));
        else
        {
            shop.With(Kit.Button(Lang.T("Online-Shop starten", "Launch online shop"), 16, 372, 559, 64, () => session.Run(new GameAction.BuyOnlineShop()), ButtonKind.Primary,
                Lang.En ? $"costs {Util.FormatEuro(Economy.OnlineShopCost)} · + {Util.FormatNumber(shopGain)} bottles sold per month" : $"kostet {Util.FormatEuro(Economy.OnlineShopCost)} · + {Util.FormatNumber(shopGain)} Flaschen Absatz im Monat",
                session.Over ? Lang.T("Die Partie ist beendet.", "The game is over.") : session.Problem(new GameAction.BuyOnlineShop())));
        }
        scene.With(shop);

        // Vinothek
        var withVinothek = player.Clone();
        withVinothek.HasVinothek = true;
        var percent = Js.Round((Economy.VinothekDemandFactor - 1) * 100);
        var vinothek = UpgradeCard(639, Lang.T("Vinothek", "Wine room"), player.HasVinothek, Lang.T("geöffnet", "open"),
            Lang.En ? $"Tasting room at the estate: + {percent} % cellar-door sales and + {Util.FormatNumber(Economy.VinothekReputation)} reputation on opening." : $"Probierstube am Hof: + {percent} % Ab-Hof-Absatz und + {Util.FormatNumber(Economy.VinothekReputation)} Bekanntheit zur Eröffnung.",
            (c, w, h) => PaintVinothek(c, w, h, player.HasVinothek));
        vinothek.With(Kit.Row(Lang.T("Absatz ab Hof im Monat", "Cellar-door sales per month"),
            player.HasVinothek ? $"{Util.FormatNumber(demand)} {Lang.T("Fl.", "btl.")}" : $"{Util.FormatNumber(demand)} → {Util.FormatNumber(Demand(withVinothek))} {Lang.T("Fl.", "btl.")}", 24, 300, 543, size: 19));
        vinothek.With(Kit.Row(Lang.T("Laufende Kosten", "Running costs"), $"{Util.FormatEuro(Economy.VinothekMonthlyCost)} {Lang.T("im Monat", "per month")}", 24, 332, 543, size: 19));
        if (player.HasVinothek) vinothek.With(OfficeKit.Done(Lang.T("Die Vinothek ist geöffnet", "The wine room is open"), 16, 372, 559, 64));
        else
        {
            vinothek.With(Kit.Button(Lang.T("Vinothek bauen", "Build wine room"), 16, 372, 559, 64, () => session.Run(new GameAction.BuildVinothek()), ButtonKind.Primary,
                Lang.En ? $"costs {Util.FormatEuro(Economy.VinothekCost)} · + {percent} % sales · + {Util.FormatNumber(Economy.VinothekReputation)} reputation" : $"kostet {Util.FormatEuro(Economy.VinothekCost)} · + {percent} % Absatz · + {Util.FormatNumber(Economy.VinothekReputation)} Bekanntheit",
                session.Over ? Lang.T("Die Partie ist beendet.", "The game is over.") : session.Problem(new GameAction.BuildVinothek())));
        }
        scene.With(vinothek);

        // Kellerkapazität: nur Anzeige, gebaut wird im Keller
        var usedTank = Rules.UsedTankHl(player);
        scene.With(CapacityCard(session, 28, Lang.T("Edelstahltanks", "Stainless steel tanks"), usedTank, player.TankCapacityHl,
            Lang.T("Mehr Platz für die Ernte. Was nicht in die Tanks passt, geht bei der Lese verloren.", "More room for the harvest. Whatever doesn't fit in the tanks is lost at harvest."),
            Lang.En ? $"+ 100 hl cost {Util.FormatEuro(100 * Economy.TankCostPerHl)}" : $"+ 100 hl kosten {Util.FormatEuro(100 * Economy.TankCostPerHl)}",
            (c, w, h) => PaintTanks(c, w, h, player.TankCapacityHl > 0 ? usedTank / player.TankCapacityHl : 0)));
        var usedOak = Rules.UsedBarriqueHl(player);
        scene.With(CapacityCard(session, 639, "Barrique", usedOak, player.BarriqueCapacityHl,
            Lang.T("Eichenfässer – vor allem Rotwein gewinnt an Güte. Nutzbar nur mit einem Kellermeister, der Barrique beherrscht.", "Oak barrels – red wine in particular gains quality. Usable only with a cellar master who masters barrique."),
            Lang.En ? $"+ 100 hl cost {Util.FormatEuro(100 * Economy.BarriqueCostPerHl)}" : $"+ 100 hl kosten {Util.FormatEuro(100 * Economy.BarriqueCostPerHl)}",
            (c, w, h) => PaintBarrels(c, w, h, player.BarriqueCapacityHl > 0, player.BarriqueCapacityHl > 0 ? usedOak / player.BarriqueCapacityHl : 0)));

        scene.With(StaffInspector(session));
    }

    static SkiaShape UpgradeCard(double x, string name, bool owned, string ownedWord, string text, Action<SKCanvas, float, float> picture)
    {
        var card = Kit.Card(x, 96, 591, 452);
        card.With(Kit.Paint(16, 16, 559, 172, picture));
        card.With(Kit.Text(name, 24, 198, 340, 42, 30, Theme.Ink, bold: true, serif: true));
        card.With(OfficeKit.Tag(owned ? ownedWord : Lang.T("fehlt noch", "not yet"), 415, 202, 152, owned));
        card.With(Kit.Text(text, 24, 242, 543, 52, 18, Theme.InkSoft, maxLines: 2));
        return card;
    }

    static SkiaControl CapacityCard(GameSession session, double x, string name, double used, double capacity, string text, string price,
        Action<SKCanvas, float, float> picture)
    {
        var card = Kit.Card(x, 564, 591, 396);
        card.With(Kit.Paint(16, 16, 559, 144, picture));
        card.With(Kit.Text(name, 24, 168, 340, 40, 28, Theme.Ink, bold: true, serif: true));
        card.With(OfficeKit.Tag(capacity > 0 ? $"{Util.FormatNumber(capacity)} hl" : Lang.T("keins", "none"), 415, 170, 152, capacity > 0));
        card.With(OfficeKit.Meter(24, 216, 543, 14, capacity > 0 ? used / capacity : 0, capacity > 0 && used >= capacity ? Theme.Alert : Theme.Wine));
        card.With(Kit.Text(capacity > 0
            ? (Lang.En ? $"used {Util.FormatNumber(used, 1)} of {Util.FormatNumber(capacity)} hl · {Util.FormatNumber(Math.Max(0, capacity - used), 1)} hl free" : $"belegt {Util.FormatNumber(used, 1)} von {Util.FormatNumber(capacity)} hl · {Util.FormatNumber(Math.Max(0, capacity - used), 1)} hl frei")
            : Lang.T("noch keine Fässer im Keller", "no barrels in the cellar yet"), 24, 236, 543, 26, 17, Theme.Ink, bold: true));
        card.With(Kit.Text(text, 24, 264, 543, 48, 17, Theme.InkSoft, maxLines: 2));
        card.With(Kit.Button(Lang.T("Im Keller ausbauen ›", "Expand in the cellar ›"), 16, 318, 559, 62, () => session.Nav.Go(Scene.Keller, 0), ButtonKind.Secondary, price));
        return card;
    }

    static SkiaControl StaffInspector(GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var costs = Market.MonthlyCosts(player, state.Month);
        var hectares = Market.TotalHectares(player);
        var fte = Market.StaffFte(player);
        var factor = Market.CostFactor(player);
        var card = OfficeKit.Inspector();
        card.With(Kit.Text(Lang.T("Personal", "Staff"), 32, 26, 460, 44, 32, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(Lang.En ? $"One full-time worker per {Util.FormatNumber(Economy.HectaresPerWorker)} ha, at least one – part-time in between." : $"Eine Vollzeitkraft je {Util.FormatNumber(Economy.HectaresPerWorker)} ha, mindestens eine – dazwischen in Teilzeit.",
            32, 70, 460, 52, 18, Theme.InkSoft, maxLines: 2));

        // Eine Figur je Kraft, die letzte anteilig gefüllt
        card.With(Kit.Paint(32, 130, 460, 112, (c, w, h) =>
        {
            // Mindestens sechs Plätze: die noch freien bleiben blass und zeigen, wohin das Gut wachsen kann
            var count = Math.Max(6, (int)Math.Ceiling(fte - 0.001));
            var step = w / count;
            var scale = Math.Min(1f, step / 64f);
            for (var i = 0; i < count; i++)
            {
                var share = (float)Math.Clamp(fte - i, 0, 1);
                OfficeKit.Worker(c, i * step + step / 2, h, scale, share);
            }
        }));
        card.With(Kit.Text(OfficeKit.Staff(fte), 32, 250, 460, 36, 25, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(Lang.En ? $"for {Util.FormatNumber(hectares, 1)} ha of vineyard, of which {Util.FormatNumber(Market.PlantedHectares(player), 1)} ha planted" : $"für {Util.FormatNumber(hectares, 1)} ha Rebland, davon {Util.FormatNumber(Market.PlantedHectares(player), 1)} ha bestockt",
            32, 286, 460, 26, 18, Theme.InkSoft));

        card.With(Kit.Box(32, 322, 460, 2, Theme.PaperLine));
        var y = 334.0;
        void Line(string label, string value, Color? color = null)
        {
            card.With(Kit.Row(label, value, 32, y, 460, color));
            y += 36;
        }
        Line(Lang.T("Lohn je Kraft", "Wage per worker"), $"{Util.FormatEuro(Economy.WorkerSalary)} {Lang.T("im Monat", "per month")}");
        Line(Lang.T("Arbeiter im Weinberg", "Vineyard workers"), Util.FormatEuro(Js.Round(fte * Economy.WorkerSalary)));
        Line(Lang.T("Kellermeister", "Cellar master"), player.CellarMaster is { } master ? Util.FormatEuro(master.Salary) : Lang.T("keiner", "none"));
        Line(Lang.T("Vinothek", "Wine room"), player.HasVinothek ? Util.FormatEuro(Economy.VinothekMonthlyCost) : "–");
        if (Math.Abs(factor - 1) > 0.001)
        {
            var change = Js.Round((factor - 1) * 100);
            Line(Lang.T("Kostenlage", "Cost level"), $"{(change > 0 ? "+" : "−")} {Math.Abs(change)} %", change > 0 ? Theme.Alert : Theme.Good);
        }
        card.With(Kit.Box(32, y + 4, 460, 2, Theme.PaperLine));
        card.With(Kit.Row(Lang.T("Personal im Monat", "Staff per month"), Util.FormatEuro(Js.Round(costs.Salaries)), 32, y + 16, 460, Theme.Wine, 23));
        y += 66;

        var perHectare = Util.FormatEuro(Economy.WorkerSalary / Economy.HectaresPerWorker);
        var note = hectares < Economy.HectaresPerWorker
            ? (Lang.En ? $"One worker is enough up to {Util.FormatNumber(Economy.HectaresPerWorker)} ha. After that, every further hectare costs {perHectare} in wages per month – buying land also means hiring people." : $"Bis {Util.FormatNumber(Economy.HectaresPerWorker)} ha genügt eine Kraft. Danach kostet jeder weitere Hektar {perHectare} Lohn im Monat – Land kaufen heißt auch Leute einstellen.")
            : (Lang.En ? $"Every further hectare costs {perHectare} in wages per month – buying land also means hiring people. Fallow land needs the same workers as vineyard." : $"Jeder weitere Hektar kostet {perHectare} Lohn im Monat – Land kaufen heißt auch Leute einstellen. Brachen brauchen dieselben Kräfte wie Rebland.");
        card.With(Kit.Box(32, y, 460, 104, Theme.C("#33d9c7a3"), 16)
            .With(Kit.Text(note, 20, 12, 420, 80, 17, Theme.InkSoft, maxLines: 3)));

        // Was früher hier lag, ist umgezogen
        var v2 = RulesV2.On(state);
        var top = v2 ? 706.0 : 734;
        card.With(Kit.Text(Lang.T("Anderswo zu finden", "Found elsewhere"), 32, top, 460, 28, 19, Theme.Wine, bold: true, serif: true));
        card.With(Kit.Button(Lang.T("Kellermeister ›", "Cellar master ›"), 32, top + 34, 460, 60, () => session.Nav.Go(Scene.Keller), ButtonKind.Ghost,
            Lang.T("einstellen und entlassen im Keller", "hire and dismiss in the cellar"), fontSize: 19));
        card.With(Kit.Button(Lang.T("Marketing und Straußwirtschaft ›", "Marketing and wine tavern ›"), 32, top + 102, 460, 60, () => session.Nav.Go(Scene.Markt), ButtonKind.Ghost,
            Lang.T("Bekanntheit und Ausschank im Markt", "reputation and serving in the market"), fontSize: 19));
        if (v2)
        {
            card.With(Kit.Button(Lang.T("Weitere Ausbauten & Stiftung ›", "More upgrades & foundation ›"), 32, top + 170, 460, 60, () =>
            {
                session.Nav.SetOverlayTab(1);
                session.Nav.Show(Overlay.Plan);
            }, ButtonKind.Ghost, Lang.T("Schänke, Gästehaus, Brennerei, Schatzkammer", "Tavern, guesthouse, distillery, treasury"), fontSize: 19));
        }
        return card;
    }

    // ---- Malerei ----

    /// <summary>Helles Schreibzimmer: getünchte Wand mit Tapetenstreifen, dunkler Balken für die Reiter, Holzvertäfelung und Schreibtisch.</summary>
    static void PaintRoom(SKCanvas c, float w, float h)
    {
        using (var wall = Draw.VerticalGradient(0, h, (0, "#fdf6e6"), (0.55f, "#f4e7cb"), (1, "#e6d3ab")))
            c.DrawRect(0, 0, w, h, wall);
        using (var stripe = Draw.Fill(SKColor.Parse("#c8a86a").WithAlpha(34)))
        {
            for (var x = 14f; x < w; x += 68) c.DrawRect(x, 0, 24, h, stripe);
        }
        // Sonnenlicht vom Fenster
        using (var sun = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(new SKPoint(w * 0.72f, 120), 900,
                [SKColor.Parse("#fffbe8").WithAlpha(210), SKColor.Parse("#fffbe8").WithAlpha(0)], [0, 1], SKShaderTileMode.Clamp),
        })
            c.DrawRect(0, 0, w, h, sun);

        // Vertäfelung im unteren Drittel
        using (var panel = Draw.VerticalGradient(690, h, (0, "#d2a871"), (1, "#a97a45")))
            c.DrawRect(0, 690, w, h - 690, panel);
        using (var rail = Draw.Fill("#8a5a34")) c.DrawRect(0, 682, w, 12, rail);
        using (var groove = Draw.Stroke(SKColor.Parse("#7a4f2c").WithAlpha(120), 3))
        {
            for (var x = 20f; x < w; x += 136) c.DrawLine(x, 700, x, h, groove);
        }
        // Schreibtischkante
        using (var desk = Draw.VerticalGradient(936, h, (0, "#7c4f2e"), (1, "#553420")))
            c.DrawRect(0, 936, w, h - 936, desk);
        using (var edge = Draw.Fill(SKColor.Parse("#e9c48c").WithAlpha(150))) c.DrawRect(0, 936, w, 3, edge);

        // Balken für die Reiter
        using (var beam = Draw.VerticalGradient(0, 84, (0, "#6e4a31"), (1, "#452b1b")))
            c.DrawRect(0, 0, w, 84, beam);
        using (var grain = Draw.Stroke(SKColor.Parse("#2c1a10").WithAlpha(70), 2))
        {
            Draw.Path(c, $"M0 22 C300 12 620 34 940 20 S1500 14 {w} 26", grain);
            Draw.Path(c, $"M0 58 C360 70 700 48 1040 62 S1560 66 {w} 52", grain);
        }
        using (var gold = Draw.Fill(Theme.SkGoldDark)) c.DrawRect(0, 82, w, 3, gold);
        using (var shade = Draw.VerticalGradient(85, 105, (0, "#551c1418"), (1, "#001c1418")))
            c.DrawRect(0, 85, w, 20, shade);
    }

    static void Backdrop(SKCanvas c, float w, float h, string top, string bottom)
    {
        using var clip = new SKPath();
        clip.AddRoundRect(new SKRoundRect(new SKRect(0, 0, w, h), 12));
        c.ClipPath(clip, antialias: true);
        using var sky = Draw.VerticalGradient(0, h, (0, top), (1, bottom));
        c.DrawRect(0, 0, w, h, sky);
    }

    static void Veil(SKCanvas c, float w, float h, bool owned)
    {
        if (owned) return;
        using var veil = Draw.Fill(SKColor.Parse("#f4ead7").WithAlpha(105));
        c.DrawRect(0, 0, w, h, veil);
    }

    static void PaintShop(SKCanvas c, float w, float h, bool owned)
    {
        c.Save();
        Backdrop(c, w, h, "#cfe3ee", "#f3ead2");
        using (var hill = Draw.Fill("#a9c486")) Draw.Path(c, $"M0 118 C120 84 260 110 380 96 S520 80 {w} 104 L{w} {h} L0 {h} Z", hill);
        using (var road = Draw.Fill("#b9a37c")) c.DrawRect(0, 140, w, h - 140, road);
        using (var dash = Draw.Stroke(SKColor.Parse("#fbf4e4").WithAlpha(200), 3))
        {
            dash.PathEffect = SKPathEffect.CreateDash([22, 16], 0);
            c.DrawLine(0, 158, w, 158, dash);
        }
        // Lieferwagen
        using (var shadow = Draw.Fill(SKColors.Black.WithAlpha(40))) c.DrawOval(250, 150, 150, 8, shadow);
        using (var body = Draw.Fill("#8e2440")) c.DrawRoundRect(120, 50, 190, 92, 10, 10, body);
        using (var cab = Draw.Fill("#f4ead7")) Draw.Path(c, "M310 76 H356 L388 108 V142 H310 Z", cab);
        using (var glass = Draw.Fill("#7fa8c4")) Draw.Path(c, "M320 84 H352 L372 106 H320 Z", glass);
        using (var gold = Draw.Fill(Theme.SkGold))
        {
            c.DrawCircle(215, 96, 24, gold);
        }
        using (var grape = Draw.Fill("#5c1429"))
        {
            c.DrawCircle(207, 90, 7, grape);
            c.DrawCircle(223, 90, 7, grape);
            c.DrawCircle(215, 104, 7, grape);
        }
        using (var tire = Draw.Fill("#2c2622"))
        using (var hub = Draw.Fill("#d9c7a3"))
        {
            c.DrawCircle(170, 144, 20, tire);
            c.DrawCircle(170, 144, 8, hub);
            c.DrawCircle(340, 144, 20, tire);
            c.DrawCircle(340, 144, 8, hub);
        }
        // Pakete
        using (var box = Draw.Fill("#c79a62"))
        using (var tape = Draw.Fill("#e9d3a6"))
        using (var edge = Draw.Stroke(SKColor.Parse("#8a5a34"), 2))
        {
            (float X, float Y, float S)[] boxes = [(430, 96, 56), (492, 106, 46), (446, 54, 42)];
            foreach (var b in boxes)
            {
                c.DrawRect(b.X, b.Y, b.S, b.S, box);
                c.DrawRect(b.X + b.S / 2 - 5, b.Y, 10, b.S, tape);
                c.DrawRect(b.X, b.Y, b.S, b.S, edge);
            }
        }
        // Bestellungen fliegen ein
        using (var mail = Draw.Fill("#fbf4e4"))
        using (var line = Draw.Stroke(SKColor.Parse("#8e2440"), 2))
        {
            (float X, float Y)[] letters = [(36, 34), (74, 78), (22, 104)];
            foreach (var l in letters)
            {
                c.DrawRoundRect(l.X, l.Y, 44, 28, 4, 4, mail);
                Draw.Path(c, $"M{l.X} {l.Y + 2} l22 14 22 -14", line);
            }
        }
        Veil(c, w, h, owned);
        c.Restore();
    }

    static void PaintVinothek(SKCanvas c, float w, float h, bool owned)
    {
        c.Save();
        Backdrop(c, w, h, "#bcd9ec", "#f6ecd0");
        using (var hill = Draw.Fill("#8fae4a")) Draw.Path(c, $"M0 96 C140 60 300 92 420 70 S520 66 {w} 84 L{w} {h} L0 {h} Z", hill);
        using (var rows = Draw.Stroke(SKColor.Parse("#5f8236"), 3))
        {
            for (var i = 0; i < 9; i++) c.DrawLine(i * 70 - 20, 150, i * 70 + 40, 84, rows);
        }
        using (var ground = Draw.Fill("#c9b58f")) c.DrawRect(0, 150, w, h - 150, ground);
        // Gebäude
        using (var wall = Draw.Fill("#f8efdc")) c.DrawRect(120, 62, 320, 92, wall);
        using (var roof = Draw.Fill("#8a3b2c")) Draw.Path(c, "M100 66 L280 14 L460 66 Z", roof);
        using (var band = Draw.Fill("#5c1429")) c.DrawRect(108, 62, 344, 16, band);
        using (var awning = Draw.Fill("#8e2440"))
        using (var awningLight = Draw.Fill("#f4ead7"))
        {
            for (var i = 0; i < 8; i++) c.DrawRect(140 + i * 35, 78, 35, 14, i % 2 == 0 ? awning : awningLight);
        }
        using (var glass = Draw.Fill("#9cc0d8"))
        using (var frame = Draw.Stroke(SKColor.Parse("#6a4a2a"), 3))
        using (var bottle = Draw.Fill("#3f5a22"))
        {
            foreach (var x in new[] { 142f, 228f, 352f })
            {
                c.DrawRect(x, 98, 68, 44, glass);
                c.DrawRect(x, 98, 68, 44, frame);
                for (var i = 0; i < 4; i++)
                {
                    c.DrawRoundRect(x + 8 + i * 15, 118, 8, 24, 3, 3, bottle);
                    c.DrawRect(x + 10.5f + i * 15, 110, 3, 9, bottle);
                }
            }
        }
        using (var door = Draw.Fill("#5c1429")) c.DrawRoundRect(304, 96, 38, 58, 6, 6, door);
        using (var knob = Draw.Fill(Theme.SkGold)) c.DrawCircle(334, 128, 3, knob);
        // Stehtisch mit Gläsern
        using (var wood = Draw.Stroke(SKColor.Parse("#6e4524"), 5))
        {
            c.DrawLine(482, 124, 540, 124, wood);
            c.DrawLine(511, 124, 511, 160, wood);
        }
        using (var wine = Draw.Fill("#8e2440"))
        {
            c.DrawOval(496, 112, 7, 9, wine);
            c.DrawOval(526, 112, 7, 9, wine);
        }
        Veil(c, w, h, owned);
        c.Restore();
    }

    static void PaintTanks(SKCanvas c, float w, float h, double fill)
    {
        c.Save();
        Backdrop(c, w, h, "#4a3830", "#221a17");
        using (var lamp = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(new SKPoint(w / 2, 0), 320,
                [SKColor.Parse("#ffcf7a").WithAlpha(120), SKColor.Parse("#ffcf7a").WithAlpha(0)], [0, 1], SKShaderTileMode.Clamp),
        })
            c.DrawRect(0, 0, w, h, lamp);
        using (var floor = Draw.Fill("#16110f")) c.DrawRect(0, h - 16, w, 16, floor);
        var level = (float)Math.Clamp(fill, 0, 1);
        for (var i = 0; i < 6; i++)
        {
            var x = 34 + i * 84f;
            using var steel = Draw.HorizontalGradient(x, x + 66, (0, "#6d7780"), (0.35f, "#d7dee4"), (0.7f, "#8d98a2"), (1, "#555e66"));
            c.DrawRoundRect(x, 16, 66, h - 30, 14, 14, steel);
            using var dark = Draw.Fill(Theme.SkNight.WithAlpha(120));
            c.DrawRoundRect(x + 10, 26, 46, h - 50, 8, 8, dark);
            var height = (h - 50) * level;
            if (height > 2)
            {
                using var wine = Draw.VerticalGradient(26, h - 24, (0, "#f1dc7a"), (1, "#c9a93a"));
                c.DrawRoundRect(x + 10, 26 + (h - 50) - height, 46, height, 8, 8, wine);
            }
        }
        c.Restore();
    }

    static void PaintBarrels(SKCanvas c, float w, float h, bool owned, double fill)
    {
        c.Save();
        Backdrop(c, w, h, "#4a3830", "#221a17");
        using (var lamp = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(new SKPoint(w / 2, 0), 320,
                [SKColor.Parse("#ffcf7a").WithAlpha(120), SKColor.Parse("#ffcf7a").WithAlpha(0)], [0, 1], SKShaderTileMode.Clamp),
        })
            c.DrawRect(0, 0, w, h, lamp);
        using (var floor = Draw.Fill("#16110f")) c.DrawRect(0, h - 16, w, 16, floor);
        var full = (int)Math.Round(Math.Clamp(fill, 0, 1) * 5);
        for (var i = 0; i < 5; i++)
        {
            var cx = 66 + i * 107f;
            var cy = h / 2 + 4;
            if (!owned)
            {
                using var dash = Draw.Stroke(SKColor.Parse("#d9c7a3").WithAlpha(150), 3);
                dash.PathEffect = SKPathEffect.CreateDash([10, 8], 0);
                c.DrawOval(cx, cy, 46, 52, dash);
                continue;
            }
            using var oak = Draw.VerticalGradient(cy - 52, cy + 52, (0, "#b07a45"), (1, "#6e4524"));
            c.DrawOval(cx, cy, 46, 52, oak);
            using var head = Draw.Fill(i < full ? "#5a3518" : "#8a6238");
            c.DrawOval(cx, cy, 32, 37, head);
            using var hoop = Draw.Stroke(SKColor.Parse("#2c2622"), 5);
            c.DrawLine(cx - 40, cy - 26, cx + 40, cy - 26, hoop);
            c.DrawLine(cx - 40, cy + 26, cx + 40, cy + 26, hoop);
            if (i < full)
            {
                using var bung = Draw.Fill("#8e2440");
                c.DrawCircle(cx, cy, 6, bung);
            }
        }
        c.Restore();
    }
}

/// <summary>Kleine Bausteine, die nur das Kontor braucht</summary>
internal static class OfficeKit
{
    public static SkiaShape Inspector() => Kit.Card(1260, 20, 524, 948, 22);

    /// <summary>„1 Vollzeitkraft“ / „2,4 Vollzeitkräfte“</summary>
    public static string Staff(double fte) =>
        Math.Abs(fte - 1) < 0.05 ? Lang.T("1 Vollzeitkraft", "1 full-time worker") : Lang.En ? $"{Util.FormatNumber(fte, 1)} full-time workers" : $"{Util.FormatNumber(fte, 1)} Vollzeitkräfte";

    /// <summary>Kennzahl oben auf der Arbeitsfläche: Bezeichnung, großer Wert, Unterzeile</summary>
    public static SkiaShape Stat(double x, string label, string value, Color valueColor, string sub, string accentHex)
    {
        var card = Kit.Card(x, 96, 388, 150);
        card.With(Kit.Box(0, 24, 8, 102, Theme.C(accentHex), 4));
        card.With(Kit.Text(label, 28, 14, 330, 28, 19, Theme.InkSoft));
        card.With(Kit.Text(value, 28, 42, 340, 54, 38, valueColor, bold: true, serif: true));
        card.With(Kit.Text(sub, 28, 104, 344, 26, 17, Theme.InkSoft));
        return card;
    }

    /// <summary>Füllstandsbalken 0–1</summary>
    public static SkiaControl Meter(double x, double y, double w, double h, double ratio, Color color)
    {
        var track = Kit.Box(x, y, w, h, Theme.PaperLine, h / 2);
        var share = Math.Clamp(ratio, 0, 1);
        if (share > 0) track.With(Kit.Box(0, 0, Math.Max(h, w * share), h, color, h / 2));
        return track;
    }

    /// <summary>Wählbare Pille, z. B. ein Schnellbetrag</summary>
    public static SkiaControl Chip(string text, double x, double y, double w, double h, bool on, Action tap, double size = 19)
    {
        var chip = on
            ? Kit.Box(x, y, w, h, radius: h / 2, gradient: Kit.Vertical(Theme.Wine, Theme.WineDark))
            : Kit.Box(x, y, w, h, Theme.PaperLine, h / 2);
        chip.With(Kit.Centered(text, 6, w - 12, h, size, on ? Theme.C("#fff6e6") : Theme.Ink, bold: true));
        chip.Tap(tap);
        return chip;
    }

    /// <summary>Zustandsschild rechts neben einem Titel</summary>
    public static SkiaControl Tag(string text, double x, double y, double w, bool good)
    {
        var tag = Kit.Box(x, y, w, 34, good ? Theme.C("#334d7a2a") : Theme.PaperLine, 17);
        tag.With(Kit.Text(text, 4, 5, w - 8, 26, 17, good ? Theme.Good : Theme.InkSoft, bold: true, align: Align.Center));
        return tag;
    }

    /// <summary>Platzhalter für eine erledigte Anschaffung – an der Stelle der Schaltfläche</summary>
    public static SkiaControl Done(string text, double x, double y, double w, double h)
    {
        var box = Kit.Box(x, y, w, h, Theme.C("#264d7a2a"), 16, Theme.Good, 2);
        box.With(Kit.Paint(22, (h - 30) / 2, 30, 30, (c, pw, ph) =>
        {
            using var fill = Draw.Fill(SKColor.Parse("#4d7a2a"));
            c.DrawCircle(15, 15, 15, fill);
            using var tick = Draw.Stroke(SKColor.Parse("#fbf4e4"), 3.5f);
            Draw.Path(c, "M8 15.5 l5 5 9 -10", tick);
        }));
        box.With(Kit.Text(text, 66, (h - 30) / 2, w - 80, 32, 21, Theme.Good, bold: true));
        return box;
    }

    public static void WarningSign(SKCanvas c, float w, float h)
    {
        using var fill = Draw.Fill(SKColor.Parse("#b3261e"));
        Draw.Path(c, "M22 3 L42 40 H2 Z", fill);
        using var mark = Draw.Stroke(SKColor.Parse("#fbf4e4"), 4);
        c.DrawLine(22, 16, 22, 28, mark);
        c.DrawPoint(22, 34.5f, mark);
    }

    /// <summary>Arbeiterfigur, von unten bis <paramref name="share"/> gefüllt (Teilzeit)</summary>
    public static void Worker(SKCanvas c, float cx, float bottom, float scale, float share)
    {
        c.Save();
        c.Translate(cx, bottom);
        c.Scale(scale);
        using var path = SKPath.ParseSvgPathData("M-24 0 V-44 A24 24 0 0 1 24 -44 V0 Z");
        using var hat = SKPath.ParseSvgPathData("M-24 -92 H24 L14 -100 A14 12 0 0 0 -14 -100 Z");
        void Figure(SKColor color)
        {
            using var paint = Draw.Fill(color);
            c.DrawPath(path, paint);
            c.DrawCircle(0, -82, 15, paint);
            c.DrawPath(hat, paint);
        }
        Figure(SKColor.Parse("#d9c7a3"));
        if (share > 0)
        {
            c.Save();
            c.ClipRect(new SKRect(-30, -112 * share, 30, 0));
            Figure(Theme.SkWine);
            c.Restore();
        }
        c.Restore();
    }
}
