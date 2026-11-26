using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

/// <summary>Weinberg, Reiter „Pflanzenschutz &amp; Bio“ und „Wetterschutz“</summary>
internal static class VineyardCare
{
    /// <summary>Rückfrage beim Abschied vom Bio-Pflanzenschutz: Name der gewünschten Strategie</summary>
    const string ConfirmKey = "weinberg.confirm";

    static string Percent(double share) => $"{Js.Round(share * 100)} %";

    static SkiaShape Done(string title, string sub, double x, double y, double w, double h, bool dark)
    {
        var box = Kit.Box(x, y, w, h, Theme.C(dark ? "#407dbb6a" : "#334d7a2a"), 16, dark ? Theme.GoodLight : Theme.Good, 2);
        var top = Math.Max(6, (h - 29 - 24) / 2);
        box.With(Kit.Text(title, 22, top, w - 44, 30, 20, dark ? Theme.GoodLight : Theme.Good, bold: true));
        box.With(Kit.Text(sub, 22, top + 29, w - 44, 24, 16, dark ? Theme.Cream : Theme.InkSoft));
        return box;
    }

    // ---- Reiter 2: Pflanzenschutz & Bio ----

    public static void Protection(GameSession session, SkiaLayout scene)
    {
        var state = session.State;
        var player = session.Player;
        var isBio = player.Protection == ProtectionStrategy.Bio && player.BioSince is not null;
        var label = BioData.BioInfo(player);
        var confirm = session.Nav.Selected(ConfirmKey);

        (ProtectionStrategy Id, string Name, VineyardIcon Icon, string Text)[] strategies =
        [
            (ProtectionStrategy.None, Lang.T("Keiner", "None"), VineyardIcon.NoProtection, Lang.T("Ohne Schutz drohen bei Nässe und Hitze schwere Ausfälle.", "Without protection, wet weather and heat can cause severe losses.")),
            (ProtectionStrategy.Conventional, Economy.Protection[ProtectionStrategy.Conventional].Name, VineyardIcon.Conventional, Lang.T("Zuverlässig gegen Pilze und Schädlinge. Bio-Siegel gibt es damit nicht.", "Reliable against fungi and pests. No organic seal with this.")),
            (ProtectionStrategy.Bio, Economy.Protection[ProtectionStrategy.Bio].Name, VineyardIcon.Bio, Lang.T("Teurer und etwas schwächer – aber der Weg zu Grünblatt, Terravita und Lunaris.", "Pricier and a bit weaker – but the path to Grünblatt, Terravita and Lunaris.")),
        ];
        for (var i = 0; i < strategies.Length; i++)
        {
            var strategy = strategies[i];
            var info = Economy.Protection[strategy.Id];
            var on = player.Protection == strategy.Id;
            var card = Kit.Box(28 + i * 407, 92, 388, 296, Theme.C("#eb1c1418"), 18, on ? Theme.Gold : Theme.GlassLine, on ? 4 : 1.5, shadow: true);
            var icon = strategy.Icon;
            card.With(Kit.Paint(22, 20, 64, 64, (c, w, h) => VineyardPaint.Icon(c, icon, on ? Theme.SkGold : Theme.SkCreamSoft)));
            card.With(Kit.Text(strategy.Name, 102, 20, 270, 38, 28, on ? Theme.Gold : Theme.Cream, bold: true, serif: true));
            card.With(Kit.Text(Lang.T("Kosten fallen von Mai bis August an", "Costs apply from May to August"), 102, 58, 270, 26, 16, Theme.CreamSoft));
            var action = new GameAction.SetProtection(strategy.Id);
            if (confirm == strategy.Id.ToString() && !on && isBio)
            {
                var warning = label is not null
                    ? Lang.En ? $"Without organic crop protection you lose the {label.Name} seal and your conversion time. Switch anyway?" : $"Ohne Bio-Pflanzenschutz verlierst du das {label.Name}-Siegel und die Umstellungszeit. Trotzdem wechseln?"
                    : Lang.T("Ohne Bio-Pflanzenschutz beginnt die Umstellung später wieder von vorn. Trotzdem wechseln?", "Without organic crop protection the conversion starts over later. Switch anyway?");
                card.With(Kit.Text(warning, 22, 100, 344, 104, 18, Theme.Warn, bold: true, maxLines: 4));
                card.With(Kit.Button(Lang.T("Trotzdem wechseln", "Switch anyway"), 22, 222, 196, 56, () =>
                {
                    session.Nav.Select(ConfirmKey, null);
                    session.Run(action);
                }, ButtonKind.Primary, fontSize: 18, align: Align.Center));
                card.With(Kit.Button(Lang.T("Abbrechen", "Cancel"), 228, 222, 138, 56, () => session.Nav.Select(ConfirmKey, null), ButtonKind.Dark, fontSize: 18, align: Align.Center));
            }
            else
            {
                card.With(Kit.Text(Percent(info.Protection), 22, 98, 160, 46, 36, Theme.Cream, bold: true, serif: true));
                card.With(Kit.Text(Lang.T("Schutz", "Protection"), 22, 144, 160, 24, 16, Theme.CreamSoft));
                card.With(Kit.Text(Util.FormatEuro(info.CostPerHa), 192, 98, 180, 46, 36, Theme.Cream, bold: true, serif: true));
                card.With(Kit.Text(Lang.T("je Hektar und Jahr", "per hectare per year"), 192, 144, 180, 24, 16, Theme.CreamSoft));
                card.With(Kit.Text(strategy.Text, 22, 174, 344, 48, 16, strategy.Id == ProtectionStrategy.None ? Theme.Warn : Theme.Cream, maxLines: 2));
                if (on)
                {
                    card.With(Kit.Box(22, 230, 344, 50, radius: 16, gradient: Kit.Vertical(Theme.Gold, Theme.GoldDark))
                        .With(Kit.Text(Lang.T("gewählt", "chosen"), 0, 11, 344, 30, 20, Theme.Ink, bold: true, align: Align.Center)));
                }
                else
                {
                    var id = strategy.Id;
                    card.With(Kit.Button(Lang.T("Wählen", "Choose"), 22, 230, 344, 50, () =>
                    {
                        // Wer Bio verlässt, verliert Siegel und Umstellungszeit – erst nachfragen
                        if (isBio) session.Nav.Select(ConfirmKey, id.ToString());
                        else session.Run(action);
                    }, ButtonKind.Dark, problem: session.Problem(action), fontSize: 20, align: Align.Center));
                }
            }
            scene.With(card);
        }

        scene.With(Ladder(session, isBio));
        scene.With(ProtectionInspector(session, isBio));
    }

    /// <summary>Stufenleiter der Bio-Zertifizierung: Umstellung → Grünblatt → Terravita → Lunaris</summary>
    static SkiaControl Ladder(GameSession session, bool isBio)
    {
        var state = session.State;
        var player = session.Player;
        var converted = isBio ? Math.Min(Economy.BioConversionMonths, Bio.GameMonth(state) - player.BioSince!.Value) : 0;
        var reached = player.BioLabel is { } current ? Array.IndexOf(BioData.BioOrder, current) : -1;
        var next = Bio.NextBioLabel(player);
        var wait = Bio.MonthsUntilEligible(state, player);

        var panel = Kit.Glass(28, 404, 1202, 564);
        panel.With(Kit.Text(Lang.T("Bio-Stufenleiter", "Organic ladder"), 24, 14, 280, 36, 26, Theme.Gold, bold: true, serif: true));
        panel.With(Kit.Text(Lang.T("Jedes Siegel muss beantragt werden. Wer vom Bio-Pflanzenschutz abweicht oder beim Panschen erwischt wird, verliert alle Siegel und die Umstellungszeit.", "Every seal must be applied for. Anyone who leaves organic crop protection or is caught adulterating wine loses all seals and the conversion time."),
            300, 12, 878, 48, 16, Theme.CreamSoft, maxLines: 2));

        const double w = 278, h = 474, top = 72;
        SkiaShape Step(int index, bool done, bool active)
        {
            var box = done ? Kit.Box(24 + index * 292, top, w, h, Theme.C("#337dbb6a"), 16, Theme.GoodLight, 2)
                : active ? Kit.Box(24 + index * 292, top, w, h, Theme.GlassLight, 16, Theme.Gold, 3)
                : Kit.Box(24 + index * 292, top, w, h, Theme.C("#14f4ead7"), 16);
            return box;
        }

        // Umstellung
        var conversionDone = converted >= Economy.BioConversionMonths;
        var conversion = Step(0, conversionDone, isBio && !conversionDone);
        conversion.With(Kit.Text("🌱", 14, 12, 50, 44, 30, Theme.Cream));
        conversion.With(Kit.Text(Lang.T("Umstellung", "Conversion"), 64, 14, 200, 34, 24, Theme.Cream, bold: true, serif: true));
        conversion.With(Kit.Text(conversionDone ? Lang.T("abgeschlossen", "complete") : isBio ? Lang.T("läuft", "in progress") : Lang.T("beginnt mit Bio-Pflanzenschutz", "starts with organic protection"), 16, 56, 250, 26, 17,
            conversionDone ? Theme.GoodLight : isBio ? Theme.Gold : Theme.CreamSoft, bold: true));
        conversion.With(Kit.Text(Lang.En ? $"{Economy.BioConversionMonths} months following organic rules – no seal yet." : $"{Economy.BioConversionMonths} Monate nach Bio-Vorgaben – noch ohne Siegel.", 16, 90, 246, 72, 16, Theme.Cream, maxLines: 3));
        conversion.With(Kit.Box(16, 176, 246, 12, Theme.GlassLine, 6));
        if (converted > 0) conversion.With(Kit.Box(16, 176, Math.Max(12, 246.0 * converted / Economy.BioConversionMonths), 12, Theme.GoodLight, 6));
        conversion.With(Kit.Text($"{converted} / {Economy.BioConversionMonths} {Lang.T("Monate", "months")}", 16, 196, 246, 26, 17, Theme.Cream, bold: true));
        if (!isBio) conversion.With(Kit.Text(Lang.T("Wähle oben „Bio“, um die Umstellung zu beginnen.", "Choose “Organic” above to begin the conversion."), 16, 232, 246, 72, 16, Theme.CreamSoft, maxLines: 3));
        panel.With(conversion);

        for (var i = 0; i < BioData.BioOrder.Length; i++)
        {
            var id = BioData.BioOrder[i];
            var info = BioData.BioLabels[id];
            var done = i <= reached;
            var isNext = id == next && isBio;
            var step = Step(i + 1, done, isNext);
            step.With(Kit.Text(info.Icon, 14, 12, 50, 44, 30, Theme.Cream));
            step.With(Kit.Text(info.Name, 64, 14, 200, 34, 24, Theme.Cream, bold: true, serif: true));
            var status = id == player.BioLabel ? Lang.T("dein Siegel", "your seal")
                : done ? Lang.T("erreicht", "reached")
                : isNext ? (wait == 0 ? Lang.T("jetzt beantragbar", "can be applied for now") : Lang.En ? $"can be applied for in {wait} month{(wait == 1 ? "" : "s")}" : $"beantragbar in {wait} Monat{(wait == 1 ? "" : "en")}")
                : info.Requires is { } required ? (Lang.En ? $"{info.WaitMonths} months after {BioData.BioLabels[required].Name}" : $"nach {info.WaitMonths} Monaten {BioData.BioLabels[required].Name}") : Lang.T("nach der Umstellung", "after conversion");
            step.With(Kit.Text(status, 16, 56, 250, 26, 17, done ? Theme.GoodLight : isNext ? Theme.Gold : Theme.CreamSoft, bold: true));
            step.With(Kit.Text(info.Text, 16, 90, 246, 116, 16, Theme.Cream, maxLines: 5));

            var y = 216.0;
            void Line(string text, int tone)
            {
                step.With(Kit.Box(16, y + 8, 10, 10, tone > 0 ? Theme.GoodLight : tone < 0 ? Theme.Warn : Theme.CreamSoft, 5));
                step.With(Kit.Text(text, 36, y, 230, 26, 17, tone > 0 ? Theme.GoodLight : tone < 0 ? Theme.Warn : Theme.Cream));
                y += 30;
            }
            Line($"{Lang.T("Preise", "Prices")} +{Percent(info.PriceBonus)}", 1);
            if (info.DemandBonus > 0) Line($"{Lang.T("Nachfrage", "Demand")} +{Percent(info.DemandBonus)}", 1);
            if (info.Quality > 0) Line(Lang.En ? $"+{Util.FormatNumber(info.Quality)} quality at every harvest" : $"+{Util.FormatNumber(info.Quality)} Güte bei jeder Lese", 1);
            if (info.YieldFactor < 1) Line($"{Lang.T("Ertrag", "Yield")} −{Percent(1 - info.YieldFactor)}", -1);
            if (info.ProtectionCostFactor > 1) Line($"{Lang.T("Pflanzenschutz", "Crop protection")} +{Percent(info.ProtectionCostFactor - 1)}", -1);
            Line($"{Lang.T("Antrag", "Application")} {Util.FormatEuro(info.Fee)}", 0);
            Line($"{Util.FormatEuro(info.AnnualFee)} {Lang.T("im Jahr", "per year")}", 0);
            if (!done && !isNext) step.Opacity = 0.62;
            panel.With(step);
        }
        return panel;
    }

    static SkiaControl ProtectionInspector(GameSession session, bool isBio)
    {
        var state = session.State;
        var player = session.Player;
        var info = Economy.Protection[player.Protection];
        var label = BioData.BioInfo(player);
        var next = Bio.NextBioLabel(player);
        var wait = Bio.MonthsUntilEligible(state, player);
        var card = Kit.Card(1260, 20, 524, 948, 22);
        const double x = 32, w = 460;
        card.With(Kit.Text(Lang.T("Pflanzenschutz", "Crop protection"), x, 26, w, 46, 32, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(Lang.T("gilt für alle bestockten Lagen", "applies to all planted sites"), x, 74, w, 28, 19, Theme.InkSoft));

        var y = 112.0;
        void Row(string name, string value, Color? color = null)
        {
            card.With(Kit.Row(name, value, x, y, w, color, 19));
            y += 33;
        }
        var costs = Market.MonthlyCosts(player, state.Month).Protection;
        var seasonCosts = Economy.ProtectionMonths.Sum(m => Market.MonthlyCosts(player, m).Protection);
        Row(Lang.T("Strategie", "Strategy"), player.Protection == ProtectionStrategy.None ? Lang.T("Keiner", "None") : info.Name);
        Row(Lang.T("Schutz", "Protection"), Percent(info.Protection), player.Protection == ProtectionStrategy.None ? Theme.Alert : null);
        Row(Lang.T("Bestockte Fläche", "Planted area"), VineyardScene.Hectares(Market.PlantedHectares(player)));
        Row(Lang.T("Kosten diesen Monat", "Costs this month"), Util.FormatEuro(Js.Round(costs)));
        Row(Lang.T("Kosten Mai bis August", "Costs May to August"), Util.FormatEuro(Js.Round(seasonCosts)));

        string status;
        var statusColor = Theme.Ink;
        if (player.Protection == ProtectionStrategy.Bio && label is not null) status = Lang.En ? $"{label.Icon} {label.Name} certified" : $"{label.Icon} {label.Name}-zertifiziert";
        else if (player.Protection == ProtectionStrategy.Bio)
            status = wait == 0 ? Lang.T("Umstellung abgeschlossen – das Grünblatt-Siegel kann beantragt werden.", "Conversion complete – you can apply for the Grünblatt seal.")
                : Lang.En ? $"In conversion – {wait} more months until the Grünblatt seal." : $"In Umstellung – noch {wait} Monate bis zum Grünblatt-Siegel.";
        else if (player.Protection == ProtectionStrategy.Conventional)
            status = Lang.En ? $"{Percent(info.Protection)} protection against fungi and pests · organic seal only with organic crop protection." : $"{Percent(info.Protection)} Schutz vor Pilzen und Schädlingen · Bio-Siegel nur mit Bio-Pflanzenschutz.";
        else (status, statusColor) = (Lang.T("Ohne Schutz drohen bei Nässe und Hitze schwere Ausfälle.", "Without protection, wet weather and heat can cause severe losses."), Theme.Alert);
        card.With(Kit.Box(x, y + 6, w, 70, Theme.C("#40d9c7a3"), 12).With(Kit.Text(status, 14, 9, w - 28, 54, 18, statusColor, bold: true, maxLines: 2)));
        y += 92;
        card.With(Kit.Box(x, y, w, 2, Theme.PaperLine));
        y += 14;

        card.With(Kit.Text(Lang.T("Bio-Siegel", "Organic seal"), x, y, w, 38, 27, Theme.Ink, bold: true, serif: true));
        y += 46;
        var converted = isBio ? Math.Min(Economy.BioConversionMonths, Bio.GameMonth(state) - player.BioSince!.Value) : 0;
        Row(Lang.T("Aktuelles Siegel", "Current seal"), label is null ? Lang.T("noch keins", "none yet") : label.Name, label is null ? null : Theme.Good);
        Row(Lang.T("Umstellung", "Conversion"), $"{converted} / {Economy.BioConversionMonths} {Lang.T("Monate", "months")}");
        card.With(Kit.Box(x, y + 2, w, 12, Theme.PaperLine, 6));
        if (converted > 0) card.With(Kit.Box(x, y + 2, Math.Max(12, w * converted / Economy.BioConversionMonths), 12, Theme.Good, 6));
        y += 28;

        if (next is { } nextId)
        {
            var nextInfo = BioData.BioLabels[nextId];
            Row(Lang.T("Nächstes Siegel", "Next seal"), nextInfo.Name);
            Row(Lang.T("Antragsgebühr", "Application fee"), Util.FormatEuro(nextInfo.Fee));
            Row(Lang.T("Jahresgebühr", "Annual fee"), Util.FormatEuro(nextInfo.AnnualFee));
            Row(Lang.T("Wartezeit", "Waiting time"), wait is null ? Lang.T("erst mit Bio-Pflanzenschutz", "only with organic crop protection") : wait == 0 ? Lang.T("jetzt beantragbar", "can be applied for now") : Lang.En ? $"{wait} more month{(wait == 1 ? "" : "s")}" : $"noch {wait} Monat{(wait == 1 ? "" : "e")}",
                wait == 0 ? Theme.Good : null);
            var effects = new List<string> { $"{Lang.T("Preise", "Prices")} +{Percent(nextInfo.PriceBonus)}" };
            if (nextInfo.DemandBonus > 0) effects.Add($"{Lang.T("Nachfrage", "Demand")} +{Percent(nextInfo.DemandBonus)}");
            if (nextInfo.Quality > 0) effects.Add($"+{Util.FormatNumber(nextInfo.Quality)} {Lang.T("Güte", "quality")}");
            if (nextInfo.YieldFactor < 1) effects.Add($"{Lang.T("Ertrag", "Yield")} −{Percent(1 - nextInfo.YieldFactor)}");
            if (nextInfo.ProtectionCostFactor > 1) effects.Add($"{Lang.T("Pflanzenschutz", "Crop protection")} +{Percent(nextInfo.ProtectionCostFactor - 1)}");
            card.With(Kit.Text($"{Lang.T("Bringt", "Brings")}: {string.Join(" · ", effects)}", x, y + 2, w, 48, 17, Theme.InkSoft, maxLines: 2));
            y += 58;
            var apply = new GameAction.ApplyBioLabel();
            card.With(Kit.Button(Lang.En ? $"Apply for {nextInfo.Name} seal" : $"{nextInfo.Name}-Siegel beantragen", x, y, w, 84, () => session.Run(apply), ButtonKind.Primary,
                Lang.En ? $"Application {Util.FormatEuro(nextInfo.Fee)} · then {Util.FormatEuro(nextInfo.AnnualFee)} per year" : $"Antrag {Util.FormatEuro(nextInfo.Fee)} · danach {Util.FormatEuro(nextInfo.AnnualFee)} im Jahr", session.Problem(apply)));
            y += 96;
        }
        else
        {
            card.With(Done(Lang.T("Strengstes Siegel erreicht", "Strictest seal reached"), Lang.T("Mehr als Lunaris geht nicht.", "There is nothing beyond Lunaris."), x, y + 6, w, 76, false));
            y += 96;
        }
        card.With(Kit.Text(Lang.T("Wer vom Bio-Pflanzenschutz abweicht oder beim Panschen erwischt wird, verliert alle Siegel und die Umstellungszeit.", "Anyone who leaves organic crop protection or is caught adulterating wine loses all seals and the conversion time."),
            x, y, w, 72, 16, Theme.InkSoft, maxLines: 3));
        return card;
    }

    // ---- Reiter 3: Wetterschutz ----

    public static void Weather(GameSession session, SkiaLayout scene)
    {
        var state = session.State;
        var player = session.Player;
        var plots = player.Plots;
        var frost = Defense.FrostChance(state, player);
        var nets = plots.Count(p => p.HailNet);
        var watered = plots.Count(p => p.Irrigation);

        (VineyardIcon Icon, string Title, string Value, string When, string Loss, string Guard, bool Now)[] hazards =
        [
            (VineyardIcon.Frost, Lang.T("Spätfrost", "Late frost"), frost > 0 ? Percent(frost) : "–",
                frost > 0 ? Lang.T("Gefahr in diesem Monat", "Risk this month") : Lang.T("droht im April und Mai", "a risk in April and May"),
                Lang.En ? $"−{Percent(Economy.FrostLoss)} yield on bearing sites" : $"−{Percent(Economy.FrostLoss)} Ertrag auf tragenden Lagen",
                player.FrostCandles ? Lang.T("Frostkerzen stehen bereit", "Frost candles are ready") : Lang.T("Schutz: Frostkerzen für einen Monat", "Protection: frost candles for one month"), frost > 0),
            (VineyardIcon.Hail, Lang.T("Hagel", "Hail"), Percent(Defense.HailChance(state)),
                Lang.T("pro Monat von Juni bis August", "per month from June to August"),
                Lang.En ? $"−{Percent(Economy.HailLoss)} yield · a net blocks {Percent(Economy.HailNetProtection)}" : $"−{Percent(Economy.HailLoss)} Ertrag · ein Netz hält {Percent(Economy.HailNetProtection)} ab",
                Lang.En ? $"{nets} of {plots.Count} plots with hail net" : $"{nets} von {plots.Count} Parzellen mit Hagelnetz", Economy.HailMonths.Contains(state.Month)),
            (VineyardIcon.Drought, Lang.T("Dürre", "Drought"), Percent(Defense.DroughtChance(state, player)),
                Lang.T("bei Sonne im Juli und August", "on sunny days in July and August"),
                Lang.En ? $"−{Percent(Economy.DroughtLoss)} yield without irrigation" : $"−{Percent(Economy.DroughtLoss)} Ertrag ohne Bewässerung",
                Lang.En ? $"{watered} of {plots.Count} plots irrigated" : $"{watered} von {plots.Count} Parzellen bewässert", Economy.DroughtMonths.Contains(state.Month)),
        ];
        for (var i = 0; i < hazards.Length; i++)
        {
            var hazard = hazards[i];
            var card = Kit.Box(28 + i * 407, 92, 388, 226, Theme.C("#eb1c1418"), 18, hazard.Now ? Theme.Gold : Theme.GlassLine, hazard.Now ? 3 : 1.5, shadow: true);
            var icon = hazard.Icon;
            var now = hazard.Now;
            card.With(Kit.Paint(22, 22, 64, 64, (c, w, h) => VineyardPaint.Icon(c, icon, now ? Theme.SkGold : Theme.SkCreamSoft)));
            card.With(Kit.Text(hazard.Title, 102, 16, 150, 36, 26, now ? Theme.Gold : Theme.Cream, bold: true, serif: true));
            card.With(Kit.Text(hazard.Value, 232, 10, 134, 50, 38, Theme.Cream, bold: true, serif: true, align: Align.End));
            card.With(Kit.Text(hazard.When, 102, 58, 264, 26, 17, Theme.CreamSoft));
            card.With(Kit.Box(22, 102, 344, 1.5, Theme.GlassLine));
            card.With(Kit.Text(hazard.Loss, 22, 114, 344, 26, 17, Theme.Cream));
            card.With(Kit.Text(hazard.Guard, 22, 144, 344, 26, 17, Theme.Gold, bold: true));
            card.With(Kit.Text(now ? Lang.T("Jetzt ist Saison für diese Gefahr.", "This hazard is in season now.") : Lang.T("Zurzeit keine Gefahr.", "No danger at the moment."), 22, 180, 344, 26, 16, now ? Theme.Warn : Theme.CreamSoft));
            scene.With(card);
        }

        // Hagelnetz und Bewässerung je Parzelle, geblättert
        const int perPage = 5;
        var pages = Math.Max(1, (int)Math.Ceiling(plots.Count / (double)perPage));
        var page = session.Nav.Page("weinberg.defense", pages);
        scene.With(VineyardScene.Pill(Lang.T("Hagelnetz und Bewässerung je Parzelle", "Hail net and irrigation per plot"), 28, 334, 420, 40, Theme.C("#c71c1418"), Theme.Cream, 18, true));
        if (pages > 1)
        {
            scene.With(Kit.Box(940, 332, 290, 44, Theme.C("#c71c1418"), 22));
            scene.With(Kit.Pager(958, 336, 262, page, pages, p => session.Nav.SetPage("weinberg.defense", Math.Clamp(p, 0, pages - 1))));
        }
        var y = 386.0;
        var rows = plots.Skip(page * perPage).Take(perPage).ToList();
        // Wenige Parzellen bekommen höhere Zeilen, damit die Fläche nicht leer wirkt
        var gapY = rows.Count <= 3 ? 46.0 : rows.Count == 4 ? 26.0 : 10.0;
        foreach (var plot in rows)
        {
            var row = Kit.Glass(28, y, 1202, 106, 16);
            var current = plot;
            row.With(Kit.Paint(9, 9, 150, 88, (c, w, h) => VineyardPaint.Plot(c, w, h, state, current, 10)));
            row.With(Kit.Text(plot.Name, 176, 14, 270, 32, 22, Theme.Cream, bold: true));
            row.With(Kit.Text(VineyardScene.PlotSub(plot), 176, 46, 270, 26, 17, Theme.CreamSoft));
            row.With(Kit.Text(Lang.En ? $"Net {Util.FormatEuro(Economy.HailNetCostPerHa)}/ha · Water {Util.FormatEuro(Economy.IrrigationCostPerHa)}/ha" : $"Netz {Util.FormatEuro(Economy.HailNetCostPerHa)}/ha · Wasser {Util.FormatEuro(Economy.IrrigationCostPerHa)}/ha", 176, 72, 280, 24, 15, Theme.CreamSoft));
            if (plot.HailNet) row.With(Done(Lang.T("Hagelnetz installiert", "Hail net installed"), Lang.En ? $"blocks {Percent(Economy.HailNetProtection)} of hail damage" : $"hält {Percent(Economy.HailNetProtection)} des Hagelschadens ab", 470, 13, 350, 80, true));
            else
            {
                var net = new GameAction.InstallHailNet(plot.Id);
                row.With(Kit.Button(Lang.T("Hagelnetz installieren", "Install hail net"), 470, 13, 350, 80, () => session.Run(net), ButtonKind.Dark,
                    Lang.En ? $"{Util.FormatEuro(Defense.InstallCost(plot, Installation.HailNet))} · blocks {Percent(Economy.HailNetProtection)} of hail damage" : $"{Util.FormatEuro(Defense.InstallCost(plot, Installation.HailNet))} · hält {Percent(Economy.HailNetProtection)} Hagelschaden ab", session.Problem(net), 20));
            }
            if (plot.Irrigation) row.With(Done(Lang.T("Bewässerung installiert", "Irrigation installed"), Lang.T("schützt vor Hitze- und Dürreschäden", "protects against heat and drought damage"), 836, 13, 350, 80, true));
            else
            {
                var water = new GameAction.InstallIrrigation(plot.Id);
                row.With(Kit.Button(Lang.T("Bewässerung installieren", "Install irrigation"), 836, 13, 350, 80, () => session.Run(water), ButtonKind.Dark,
                    Lang.En ? $"{Util.FormatEuro(Defense.InstallCost(plot, Installation.Irrigation))} · protects against heat and drought" : $"{Util.FormatEuro(Defense.InstallCost(plot, Installation.Irrigation))} · schützt vor Hitze und Dürre", session.Problem(water), 20));
            }
            scene.With(row);
            y += 106 + gapY;
        }
        scene.With(WeatherInspector(session, frost));
    }

    static SkiaControl WeatherInspector(GameSession session, double frost)
    {
        var state = session.State;
        var player = session.Player;
        var card = Kit.Card(1260, 20, 524, 948, 22);
        const double x = 32, w = 460;
        card.With(Kit.Text(Lang.T("Wetterschutz", "Weather protection"), x, 26, w, 46, 32, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(Lang.T("Versicherung und Frostkerzen fürs ganze Gut", "Insurance and frost candles for the whole estate"), x, 74, w, 28, 19, Theme.InkSoft));
        var bearing = Defense.BearingPlots(state, player).Sum(p => p.Hectares);

        // Ernteversicherung
        var y = 116.0;
        card.With(Kit.Text(Lang.T("Ernteversicherung", "Harvest insurance"), x, y, w, 36, 25, Theme.Wine, bold: true, serif: true));
        var payout = Percent(1 - Economy.InsuranceDeductible);
        card.With(Kit.Text(Lang.En ? $"Sign up January to April for the current year. Pays {payout} of damage from frost, hail, drought and storms." : $"Abschluss Januar bis April für das laufende Jahr. Zahlt {payout} des Schadens durch Frost, Hagel, Dürre und Unwetter.",
            x, y + 38, w, 72, 17, Theme.InkSoft, maxLines: 3));
        y += 116;
        card.With(Kit.Row(Lang.T("Tragende Fläche", "Bearing area"), VineyardScene.Hectares(bearing), x, y, w, size: 19));
        card.With(Kit.Row(Lang.T("Prämie", "Premium"), Util.FormatEuro(Defense.InsurancePremium(state, player)), x, y + 33, w, size: 19));
        y += 74;
        if (Defense.IsInsured(state, player)) card.With(Done(Lang.En ? $"Insured for year {state.Year}" : $"Versichert für Jahr {state.Year}", Lang.En ? $"pays {payout} of harvest damage" : $"zahlt {payout} des Ernteschadens", x, y, w, 84, false));
        else
        {
            var insure = new GameAction.InsureHarvest();
            card.With(Kit.Button(Lang.T("Ernte versichern", "Insure harvest"), x, y, w, 84, () => session.Run(insure), ButtonKind.Primary,
                Lang.En ? $"Premium {Util.FormatEuro(Defense.InsurancePremium(state, player))} for year {state.Year}" : $"Prämie {Util.FormatEuro(Defense.InsurancePremium(state, player))} für Jahr {state.Year}", session.Problem(insure)));
        }
        y += 102;
        card.With(Kit.Box(x, y, w, 2, Theme.PaperLine));
        y += 14;

        // Frostkerzen
        card.With(Kit.Text(Lang.T("Frostkerzen", "Frost candles"), x, y, w, 36, 25, Theme.Wine, bold: true, serif: true));
        card.With(Kit.Text(frost > 0
                ? Lang.En ? $"Late frost risk this month: {Percent(frost)} (−{Percent(Economy.FrostLoss)} yield). Candles protect all bearing sites for this month." : $"Spätfrostgefahr diesen Monat: {Percent(frost)} (−{Percent(Economy.FrostLoss)} Ertrag). Kerzen schützen alle tragenden Lagen für diesen Monat."
                : Lang.T("Spätfrost droht im April und Mai – dann lassen sich hier für einen Monat Kerzen aufstellen.", "Late frost threatens in April and May – you can then set up candles here for a month."),
            x, y + 38, w, 72, 17, Theme.InkSoft, maxLines: 3));
        y += 116;
        card.With(Kit.Row(Lang.T("Spätfrostgefahr", "Late frost risk"), frost > 0 ? Percent(frost) : Lang.T("keine", "none"), x, y, w, frost > 0 ? Theme.Alert : null, 19));
        card.With(Kit.Row(Lang.T("Kosten", "Cost"), Util.FormatEuro(Defense.FrostCandlesCost(state, player)), x, y + 33, w, size: 19));
        y += 74;
        if (player.FrostCandles) card.With(Done(Lang.T("Frostkerzen stehen bereit", "Frost candles are ready"), Lang.T("für alle tragenden Lagen in diesem Monat", "for all bearing sites this month"), x, y, w, 84, false));
        else
        {
            var candles = new GameAction.FrostCandles();
            card.With(Kit.Button(Lang.T("Frostkerzen bereitstellen", "Set up frost candles"), x, y, w, 84, () => session.Run(candles), ButtonKind.Secondary,
                Lang.En ? $"costs {Util.FormatEuro(Defense.FrostCandlesCost(state, player))} · valid for this month" : $"kostet {Util.FormatEuro(Defense.FrostCandlesCost(state, player))} · gilt für diesen Monat", session.Problem(candles)));
        }
        y += 102;
        card.With(Kit.Box(x, y, w, 2, Theme.PaperLine));
        y += 14;

        // Hagel und Dürre
        card.With(Kit.Text(Lang.T("Hagel und Dürre", "Hail and drought"), x, y, w, 36, 25, Theme.Wine, bold: true, serif: true));
        card.With(Kit.Text(Lang.En ? $"Hail June–August: {Percent(Defense.HailChance(state))} per month · drought in sunny weather in July/August: {Percent(Defense.DroughtChance(state, player))}. " +
            "You install nets and irrigation on the left, per plot." : $"Hagel Juni–August: {Percent(Defense.HailChance(state))} pro Monat · Dürre bei Sonne im Juli/August: {Percent(Defense.DroughtChance(state, player))}. " +
            "Netze und Bewässerung installierst du links je Parzelle.", x, y + 38, w, 96, 17, Theme.InkSoft, maxLines: 4));
        return card;
    }
}
