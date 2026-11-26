using Winzer.Engine.Data;

namespace Winzer.Engine;

/// <summary>
/// Bio-Zertifizierung als Stufenleiter: Wer auf Bio umstellt, arbeitet zwei Jahre nach Bio-Vorgaben und beantragt
/// dann das Grünblatt-Siegel. Darauf aufbauend folgen Terravita und Lunaris – jeweils strenger, teurer und lohnender.
/// Wer vom Bio-Pflanzenschutz abweicht oder beim Panschen erwischt wird, verliert alle Siegel. (bio.ts)
/// BIO_LABELS, BIO_ORDER und bioInfo liegen in <see cref="BioData"/>.
/// </summary>
public static class Bio
{
    /// <summary>Fortlaufender Spielmonat: Januar in Jahr 1 = 1</summary>
    public static int GameMonth(GameState state) => (state.Year - 1) * 12 + state.Month;

    /// <summary>Das nächste Siegel, das sich beantragen ließe</summary>
    public static BioLabel? NextBioLabel(Player player)
    {
        var index = player.BioLabel is { } label ? Array.IndexOf(BioData.BioOrder, label) + 1 : 0;
        return index < BioData.BioOrder.Length ? BioData.BioOrder[index] : null;
    }

    /// <summary>Monate, bis das nächste Siegel beantragt werden kann (0 = jetzt) – null ohne Bio-Pflanzenschutz</summary>
    public static int? MonthsUntilEligible(GameState state, Player player)
    {
        var next = NextBioLabel(player);
        if (player.Protection != ProtectionStrategy.Bio || player.BioSince is null || next is null) return null;
        var info = BioData.BioLabels[next.Value];
        var since = info.Requires is null ? player.BioSince.Value : player.BioLabelSince!.Value;
        return Math.Max(0, since + info.WaitMonths - GameMonth(state));
    }

    public static string? ValidateBioLabel(GameState state, Player player)
    {
        var next = NextBioLabel(player);
        if (next is null) return Lang.T("Du trägst bereits das strengste Siegel.", "You already hold the strictest seal.");
        if (player.Protection != ProtectionStrategy.Bio) return Lang.T("Ein Bio-Siegel gibt es nur mit Bio-Pflanzenschutz.", "An organic seal requires organic crop protection.");
        var info = BioData.BioLabels[next.Value];
        var wait = MonthsUntilEligible(state, player)!.Value;
        if (wait > 0)
        {
            if (Lang.En)
            {
                var beforeEn = info.Requires is { } req ? BioData.BioLabels[req].Name : "conversion";
                return $"{wait} more month{(wait == 1 ? "" : "s")} of {beforeEn} before {info.Name} can be applied for.";
            }
            var before = info.Requires is { } required ? BioData.BioLabels[required].Name : "Umstellung";
            return $"Noch {wait} Monat{(wait == 1 ? "" : "e")} {before}, bevor {info.Name} beantragt werden kann.";
        }
        if (player.Money < info.Fee) return Lang.En ? $"Not enough money for certification ({Util.FormatEuro(info.Fee)})." : $"Nicht genug Geld für die Zertifizierung ({Util.FormatEuro(info.Fee)}).";
        return null;
    }

    public static void ApplyBioLabel(GameState state, Player player)
    {
        var next = NextBioLabel(player)!.Value;
        var info = BioData.BioLabels[next];
        player.Money -= info.Fee;
        player.BioLabel = next;
        player.BioLabelSince = GameMonth(state);
        Rules.ChangeReputation(player, info.Reputation);
        Util.AddLog(
            state,
            null,
            Lang.En ? $"{info.Icon} {player.Name} is now {info.Name}-certified (+{Util.JsNumber(Js.Round(info.PriceBonus * 100))}% on bottle prices)." : $"{info.Icon} {player.Name} ist jetzt {info.Name}-zertifiziert (+{Util.JsNumber(Js.Round(info.PriceBonus * 100))} % auf die Flaschenpreise).",
            LogTone.Good);
    }

    /// <summary>Wechsel weg vom Bio-Pflanzenschutz oder Weinskandal: Siegel und Umstellungszeit sind verloren.</summary>
    public static void LoseBio(GameState state, Player player, string reason)
    {
        var label = BioData.BioInfo(player);
        player.BioLabel = null;
        player.BioLabelSince = null;
        if (label is not null) Util.AddLog(state, null, Lang.En ? $"{player.Name} loses the {label.Name} seal: {reason}." : $"{player.Name} verliert das {label.Name}-Siegel: {reason}.", LogTone.Bad);
    }

    /// <summary>Jahresbeginn: Kontroll- und Verbandsgebühren</summary>
    public static void ChargeBioFees(GameState state, Player player)
    {
        var label = BioData.BioInfo(player);
        if (label is null) return;
        player.Money -= label.AnnualFee;
        Util.AddLog(state, player.Id, Lang.En ? $"Annual fee for inspection and association ({label.Name}): −{Util.FormatEuro(label.AnnualFee)}." : $"Jahresgebühr für Kontrolle und Verband ({label.Name}): −{Util.FormatEuro(label.AnnualFee)}.");
    }
}
