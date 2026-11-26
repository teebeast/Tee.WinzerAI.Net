using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

/// <summary>Reiter „Kellermeister“: links die Bewerber des Jahres als Karten, rechts der aktuelle Kellermeister.</summary>
internal static class CellarMasterTab
{
    const string ConfirmKey = "keller.confirm";
    static readonly string[] LongHaired = ["Marie", "Lena", "Sophie", "Clara", "Hannah", "Julia", "Eva", "Nora", "Ines"];

    public static void Build(SkiaLayout scene, GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var candidates = state.CellarMasterCandidates;

        var head = Kit.Glass(28, 90, 1202, 88, 16, shadow: false);
        head.With(Kit.Text(Lang.En ? $"Applications year {state.Year}" : $"Bewerbungen Jahr {state.Year}", 24, 10, 500, 38, 27, Theme.Gold, bold: true, serif: true));
        head.With(Kit.Text(Lang.En ? $"New candidates apply every January. Experience: every {Economy.CellarMasterYearsPerExperience} years of service +1 quality (at most +{Economy.CellarMasterMaxExperience})." : $"Jeden Januar bewerben sich neue Kandidaten. Erfahrung: alle {Economy.CellarMasterYearsPerExperience} Dienstjahre +1 Güte (höchstens +{Economy.CellarMasterMaxExperience}).",
            24, 50, 1150, 26, 17, Theme.CreamSoft));
        scene.With(head);

        for (var i = 0; i < Economy.CellarMasterCandidates; i++)
        {
            var x = 28 + i * 407.0;
            if (i < candidates.Count) scene.With(CandidateCard(session, i, candidates[i], x));
            else
            {
                var gone = Kit.Box(x, 194, 388, 768, Theme.C("#14f4ead7"), 18, Theme.GlassLine, 2);
                gone.With(Kit.Paint(129, 250, 130, 130, (c, w, h) => PaintPortrait(c, 130, null)));
                gone.With(Kit.Text(candidates.Count == 0 && i == 1 ? Lang.T("Alle Bewerber sind vergeben", "All applicants are taken") : Lang.T("Stelle vergeben", "Position filled"), 20, 400, 348, 30, 21, Theme.Cream, bold: true, align: Align.Center));
                gone.With(Kit.Text(Lang.T("Im Januar kommen neue Bewerbungen.", "New applications arrive in January."), 20, 434, 348, 26, 17, Theme.CreamSoft, align: Align.Center));
                scene.With(gone);
            }
        }

        scene.With(player.CellarMaster is { } master ? CurrentCard(session, master) : VacantCard(session));
    }

    static string Confirming(GameSession session) => session.Nav.Selected(ConfirmKey) ?? "";

    /// <summary>Rückfrage gilt nur im selben Monat – danach verfällt sie</summary>
    static string Token(GameSession session, string what) => $"{what}@{session.State.Year}.{session.State.Month}";

    static string Signed(double value) => $"{(value > 0 ? "+" : value < 0 ? "−" : "±")}{Util.FormatNumber(Math.Abs(value))}";

    /// <summary>Fähigkeiten mit Zeichen, Namen und Wirkung</summary>
    static void Skills(SkiaLayout card, CellarMaster master, double x, double y, double w, double pitch)
    {
        if (master.Skills.Count == 0) card.With(Kit.Text(Lang.T("Keine besonderen Fähigkeiten.", "No special skills."), x, y, w, 26, 17, Theme.InkSoft));
        foreach (var skill in master.Skills)
        {
            var info = CellarMasters.Skills[skill];
            card.With(Kit.Box(x, y, 52, 52, Theme.PaperLine, 26).With(Kit.Text(info.Icon, 0, 9, 52, 36, 25, Theme.Ink, align: Align.Center)));
            card.With(Kit.Text(info.Name, x + 66, y - 2, w - 66, 28, 20, Theme.Ink, bold: true));
            card.With(Kit.Text(info.Text, x + 66, y + 26, w - 66, 44, 16, Theme.InkSoft, maxLines: 2));
            y += pitch;
        }
    }

    static SkiaControl CandidateCard(GameSession session, int index, CellarMaster candidate, double x)
    {
        var state = session.State;
        var player = session.Player;
        var nav = session.Nav;
        var current = player.CellarMaster;
        var card = Kit.Card(x, 194, 388, 768, 18);
        card.With(Kit.Paint(24, 24, 120, 120, (c, w, h) => PaintPortrait(c, 120, candidate.Name)));
        card.With(Kit.Text(candidate.Name, 160, 28, 216, 36, 25, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(Lang.En ? $"Talent {Util.FormatNumber(candidate.Talent)} of 5" : $"Talent {Util.FormatNumber(candidate.Talent)} von 5", 160, 66, 216, 26, 17, Theme.InkSoft));
        card.With(Kit.Paint(160, 100, 150, 30, (c, w, h) => CellarParts.PaintStars(c, (int)candidate.Talent)));

        card.With(Kit.Row(Lang.T("Güte je Lese", "Quality per harvest"), $"+{Util.FormatNumber(candidate.Talent)}", 24, 168, 340, Theme.Good));
        card.With(Kit.Row(Lang.T("Gehalt", "Salary"), $"{Util.FormatEuro(candidate.Salary)}/{Lang.T("Monat", "month")}", 24, 204, 340));
        if (current is not null)
        {
            var now = current.Talent + CellarMasters.Experience(current, state.Year);
            var delta = candidate.Talent - now;
            card.With(Kit.Row(Lang.T("Güte ggü. heute", "Quality vs. today"), Signed(delta), 24, 240, 340, delta > 0 ? Theme.Good : delta < 0 ? Theme.Alert : Theme.Ink, 19));
            var pay = candidate.Salary - current.Salary;
            card.With(Kit.Row(Lang.T("Gehalt ggü. heute", "Salary vs. today"), (Lang.En ? Signed(pay).Insert(1, "€") : $"{Signed(pay)} €"), 24, 276, 340, pay > 0 ? Theme.Alert : pay < 0 ? Theme.Good : Theme.Ink, 19));
        }
        else
        {
            card.With(Kit.Row(Lang.T("Erstes Gehalt", "First salary"), Lang.T("sofort fällig", "due immediately"), 24, 240, 340));
            card.With(Kit.Row(Lang.T("Erfahrung", "Experience"), Lang.T("wächst mit den Jahren", "grows over the years"), 24, 276, 340));
        }
        card.With(Kit.Box(24, 322, 340, 2, Theme.PaperLine, 1));
        card.With(Kit.Text(Lang.T("Fähigkeiten", "Skills"), 24, 336, 340, 28, 20, Theme.Wine, bold: true, serif: true));
        Skills(card, candidate, 24, 378, 340, 88);

        var hire = new GameAction.HireCellarMaster(index);
        var problem = session.Problem(hire);
        var token = Token(session, $"hire{index}");
        if (current is not null && problem is null && Confirming(session) == token)
        {
            card.With(Kit.Button(Lang.T("Wirklich einstellen?", "Really hire?"), 24, 652, 340, 60, () => { nav.Select(ConfirmKey, null); session.Run(hire); }, ButtonKind.Gold,
                Lang.En ? $"{current.Name} has to go for this" : $"{current.Name} muss dafür gehen", fontSize: 20));
            card.With(Kit.Button(Lang.T("Abbrechen", "Cancel"), 24, 718, 340, 38, () => nav.Select(ConfirmKey, null), ButtonKind.Ghost, fontSize: 17, align: Align.Center));
        }
        else
        {
            card.With(CellarParts.Action(Lang.T("Einstellen", "Hire"), 24, 660, 340, 84, () =>
                {
                    if (current is null) session.Run(hire);
                    else nav.Select(ConfirmKey, token);
                }, ButtonKind.Primary,
                current is null ? (Lang.En ? $"first salary {Util.FormatEuro(candidate.Salary)}" : $"erstes Gehalt {Util.FormatEuro(candidate.Salary)}") : (Lang.En ? $"replaces {current.Name}" : $"löst {current.Name} ab"), problem));
        }
        return card;
    }

    static SkiaControl CurrentCard(GameSession session, CellarMaster master)
    {
        var state = session.State;
        var player = session.Player;
        var nav = session.Nav;
        var card = CellarParts.Inspector();
        var experience = CellarMasters.Experience(master, state.Year);
        card.With(Kit.Paint(CellarParts.Pad, 28, 130, 130, (c, w, h) => PaintPortrait(c, 130, master.Name)));
        card.With(Kit.Text(master.Name, 182, 32, 310, 42, master.Name.Length > 16 ? 26 : 30, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(Lang.En ? $"Your cellar master{(master.HiredYear is { } hiredEn ? $" since year {hiredEn}" : "")}" : $"Dein Kellermeister{(master.HiredYear is { } hired ? $" seit Jahr {hired}" : "")}", 182, 76, 310, 26, 18, Theme.InkSoft));
        card.With(Kit.Paint(182, 112, 150, 30, (c, w, h) => CellarParts.PaintStars(c, (int)master.Talent)));

        card.With(Kit.Row(Lang.T("Talent", "Talent"), $"+{Util.FormatNumber(master.Talent)} {Lang.T("Güte", "quality")}", CellarParts.Pad, 184, CellarParts.Inner));
        card.With(Kit.Row(Lang.T("Erfahrung", "Experience"), experience > 0 ? $"+{Util.FormatNumber(experience)} {Lang.T("Güte", "quality")}" : Lang.T("noch keine", "none yet"), CellarParts.Pad, 220, CellarParts.Inner));
        card.With(Kit.Row(Lang.T("Güte für jede Lese", "Quality for every harvest"), $"+{Util.FormatNumber(master.Talent + experience)}", CellarParts.Pad, 256, CellarParts.Inner, Theme.Good));
        string next;
        if (experience >= Economy.CellarMasterMaxExperience || master.HiredYear is null) next = Lang.T("erreicht", "reached");
        else
        {
            var years = (int)(experience + 1) * Economy.CellarMasterYearsPerExperience - (state.Year - master.HiredYear.Value);
            next = years <= 1 ? Lang.T("nächstes Jahr", "next year") : Lang.En ? $"in {years} years" : $"in {years} Jahren";
        }
        card.With(Kit.Row(Lang.T("Nächste Erfahrungsstufe", "Next experience level"), next, CellarParts.Pad, 292, CellarParts.Inner));
        card.With(Kit.Row(Lang.T("Gehalt", "Salary"), $"{Util.FormatEuro(master.Salary)}/{Lang.T("Monat", "month")}", CellarParts.Pad, 328, CellarParts.Inner));
        card.With(CellarParts.Divider(374));

        card.With(Kit.Text(Lang.T("Fähigkeiten", "Skills"), CellarParts.Pad, 388, 300, 30, 21, Theme.Wine, bold: true, serif: true));
        Skills(card, master, CellarParts.Pad, 432, CellarParts.Inner, 86);
        if (CellarParts.MasterHint(player) is { } hint)
        {
            card.With(Kit.Text(hint, CellarParts.Pad, 432 + Math.Max(1, master.Skills.Count) * 86 + 6, CellarParts.Inner, 50, 17, Theme.Alert, bold: true, maxLines: 2));
        }

        var fire = new GameAction.FireCellarMaster();
        var token = Token(session, "fire");
        if (Confirming(session) == token)
        {
            card.With(Kit.Text(Lang.En ? $"Really dismiss {master.Name}? Experience and skills will be lost." : $"{master.Name} wirklich entlassen? Erfahrung und Fähigkeiten gehen verloren.", CellarParts.Pad, 772, CellarParts.Inner, 50, 18, Theme.Ink, bold: true, maxLines: 2));
            card.With(Kit.Button(Lang.T("Ja, entlassen", "Yes, dismiss"), CellarParts.Pad, 836, 224, 76, () => { nav.Select(ConfirmKey, null); session.Run(fire); }, ButtonKind.Primary, fontSize: 21, align: Align.Center));
            card.With(Kit.Button(Lang.T("Abbrechen", "Cancel"), CellarParts.Pad + 236, 836, 224, 76, () => nav.Select(ConfirmKey, null), ButtonKind.Ghost, fontSize: 21, align: Align.Center));
        }
        else
        {
            card.With(CellarParts.Action(Lang.T("Entlassen", "Dismiss"), CellarParts.Pad, 828, CellarParts.Inner, 84, () => nav.Select(ConfirmKey, token), ButtonKind.Secondary,
                Lang.T("Erfahrung und Fähigkeiten gehen verloren", "Experience and skills will be lost"), session.Problem(fire)));
        }
        return card;
    }

    static SkiaControl VacantCard(GameSession session)
    {
        var state = session.State;
        var card = CellarParts.Inspector();
        card.With(Kit.Paint(CellarParts.Pad, 28, 130, 130, (c, w, h) => PaintPortrait(c, 130, null)));
        card.With(Kit.Text(Lang.T("Kein Kellermeister", "No cellar master"), 182, 32, 310, 42, 28, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(Lang.T("Die Stelle ist frei", "The position is vacant"), 182, 76, 310, 26, 18, Theme.InkSoft));
        card.With(Kit.Paint(182, 112, 150, 30, (c, w, h) => CellarParts.PaintStars(c, 0)));
        card.With(Kit.Text(Lang.T("Ohne Kellermeister gibt es keinen Güte-Zuschlag – und weder Sekt noch Barriqueweine. Wähle links aus den Bewerbungen dieses Jahres.", "Without a cellar master there is no quality bonus – and neither sparkling wine nor barrique wines. Choose from this year's applications on the left."),
            CellarParts.Pad, 184, CellarParts.Inner, 110, 19, Theme.Ink, maxLines: 4));
        card.With(CellarParts.Divider(304));
        card.With(Kit.Row(Lang.T("Güte für jede Lese", "Quality for every harvest"), Lang.T("kein Zuschlag", "no bonus"), CellarParts.Pad, 320, CellarParts.Inner, Theme.Alert));
        card.With(Kit.Row(Lang.T("Sekt", "Sparkling wine"), Lang.T("nicht möglich", "not possible"), CellarParts.Pad, 356, CellarParts.Inner, Theme.Alert));
        card.With(Kit.Row(Lang.T("Barrique-Ausbau", "Barrique aging"), Lang.T("nicht möglich", "not possible"), CellarParts.Pad, 392, CellarParts.Inner, Theme.Alert));
        card.With(Kit.Row(Lang.T("Gehalt", "Salary"), Lang.T("0 €/Monat", "€0/month"), CellarParts.Pad, 428, CellarParts.Inner));
        card.With(CellarParts.Divider(474));
        card.With(Kit.Text(Lang.T("Was Kellermeister können", "What cellar masters can do"), CellarParts.Pad, 488, CellarParts.Inner, 30, 21, Theme.Wine, bold: true, serif: true));
        var y = 528.0;
        foreach (var info in CellarMasters.Skills.Values)
        {
            card.With(Kit.Text($"{info.Icon} {info.Name}", CellarParts.Pad, y, 150, 26, 17, Theme.Ink, bold: true));
            card.With(Kit.Text(info.Text, CellarParts.Pad + 150, y, CellarParts.Inner - 150, 44, 15, Theme.InkSoft, maxLines: 2));
            y += 50;
        }
        card.With(Kit.Text(state.CellarMasterCandidates.Count == 0 ? Lang.T("Alle Bewerber sind vergeben – im Januar kommen neue.", "All applicants are taken – new ones arrive in January.") : Lang.T("Erfahrung: mit den Dienstjahren wächst der Zuschlag.", "Experience: the bonus grows with the years of service."),
            CellarParts.Pad, 880, CellarParts.Inner, 28, 17, Theme.InkSoft));
        return card;
    }

    /// <summary>Brustbild in einem Kreis; ohne Namen ein leerer Platz</summary>
    static void PaintPortrait(SKCanvas c, float size, string? name)
    {
        var s = size / 120f;
        c.Save();
        c.Scale(s);
        var seed = name is null ? 0 : name.Aggregate(7, (sum, ch) => (sum * 31 + ch) & 0xffff);
        string[] grounds = ["#8e2440", "#4d7a2a", "#3f6fa8", "#c8962e", "#6f4a36", "#5d5468"];
        string[] skins = ["#f0c9a4", "#e3b48c", "#f4d3b4", "#d9a47a"];
        string[] hairs = ["#3a2a22", "#7a4a2a", "#c9a23a", "#8d98a2", "#5c1429", "#1c1418"];
        using var clip = new SKPath();
        clip.AddCircle(60, 60, 58);
        c.Save();
        c.ClipPath(clip, antialias: true);
        if (name is null)
        {
            using (var back = Draw.Fill(SKColor.Parse("#8a7d6a").WithAlpha(110))) c.DrawRect(0, 0, 120, 120, back);
            using var ghost = Draw.Fill(SKColor.Parse("#f4ead7").WithAlpha(120));
            c.DrawCircle(60, 48, 22, ghost);
            c.DrawOval(60, 118, 44, 36, ghost);
        }
        else
        {
            var ground = grounds[seed % grounds.Length];
            using (var back = Draw.VerticalGradient(0, 120, (0, ground), (1, "#1c1418"))) c.DrawRect(0, 0, 120, 120, back);
            var skin = skins[(seed >> 3) % skins.Length];
            var hair = hairs[(seed >> 5) % hairs.Length];
            var longHair = LongHaired.Contains(name.Split(' ')[0]);
            if (longHair)
            {
                using var back = Draw.Fill(hair);
                c.DrawRoundRect(34, 30, 52, 62, 22, 22, back);
            }
            using (var coat = Draw.Fill("#f6efe2")) c.DrawOval(60, 122, 46, 40, coat);
            using (var apron = Draw.Fill("#3a2a22")) Draw.Path(c, "M44 92 H76 L82 122 H38 Z", apron);
            using (var neck = Draw.Fill(skin)) c.DrawRect(52, 66, 16, 22, neck);
            using (var face = Draw.Fill(skin)) c.DrawOval(60, 52, 20, 23, face);
            using (var top = Draw.Fill(hair)) Draw.Path(c, longHair ? "M38 54 Q36 26 60 26 Q84 26 82 54 Q74 38 60 38 Q46 38 38 54 Z" : "M40 48 Q38 28 60 27 Q82 28 80 48 Q72 36 60 37 Q48 36 40 48 Z", top);
            using (var eye = Draw.Fill("#3a1d24"))
            {
                c.DrawCircle(52, 54, 2.2f, eye);
                c.DrawCircle(68, 54, 2.2f, eye);
            }
            using var smile = Draw.Stroke("#8e2440", 2);
            Draw.Path(c, "M53 64 Q60 69 67 64", smile);
        }
        c.Restore();
        using var rim = Draw.Stroke(name is null ? Theme.SkCreamSoft.WithAlpha(150) : Theme.SkGoldDark, 4);
        c.DrawCircle(60, 60, 58, rim);
        c.Restore();
    }
}
