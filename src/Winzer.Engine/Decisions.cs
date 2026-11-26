using System.Text.RegularExpressions;
using Winzer.Engine.Data;

namespace Winzer.Engine;

// Entscheidungsereignisse: Statt einfach einzutreten, stellen sie das Weingut vor eine Wahl – meist mit Risiko
// oder Preis. Die Entscheidung gilt bis Monatsende, danach greift die vorsichtige Wahl. Computergegner entscheiden sofort.

/// <summary>Ergebnis einer Entscheidung: Meldung und Tonfall</summary>
public sealed record Outcome(string Text, LogTone? Tone = null);

public sealed class DecisionDef
{
    public required string Id { get; init; }
    public required Rarity Rarity { get; init; }
    public int[]? Months { get; init; }
    public Func<EventContext, bool>? When { get; init; }
    /// <summary>Baut die Entscheidung mit ausgewürfelten Werten (ohne <c>EventId</c>)</summary>
    public required Func<EventContext, Decision> Offer { get; init; }
    /// <summary>Wahl eines Computergegners</summary>
    public required Func<EventContext, Decision, int> Ai { get; init; }
    public required Func<EventContext, int, Decision, Outcome> Resolve { get; init; }
}

public static class Decisions
{
    static DecisionOption Option(string label, string hint, double cost = 0) => new() { Label = label, Hint = hint, Cost = cost };
    static DecisionOption Decline(string? hint = null) => Option(Lang.T("Ablehnen", "Decline"), hint ?? Lang.T("Es passiert nichts.", "Nothing happens."));

    /// <summary>Kann der Betrag über Konto und freien Kreditrahmen bezahlt werden?</summary>
    public static bool Affordable(Player player, double amount) => Math.Max(0, player.Money) + Market.AvailableCredit(player) >= amount;

    /// <summary>Nur ausgeben, was die Rücklage nicht angreift – Maßstab der Computergegner</summary>
    static bool Comfortable(Player player, double amount) => player.Money - amount > 20_000;

    /// <summary>Bezahlt; ein Fehlbetrag wird automatisch über den Kredit gedeckt.</summary>
    static string Pay(EventContext ctx, double amount)
    {
        var player = ctx.Player;
        player.Money -= amount;
        if (player.Money < 0)
        {
            var loan = Math.Min(-player.Money, Market.AvailableCredit(player));
            player.Loan += loan;
            player.Money += loan;
            if (loan > 0) return (Lang.En ? $"{Util.FormatEuro(amount)}, of which {Util.FormatEuro(loan)} on credit" : $"{Util.FormatEuro(amount)}, davon {Util.FormatEuro(loan)} per Kredit");
        }
        return Util.FormatEuro(amount);
    }

    /// <summary>Erwarteter Erlös für Flaschen zum angegebenen Anteil des Ab-Hof-Preises, älteste zuerst</summary>
    static double ExpectedRevenue(EventContext ctx, double bottles, double share)
    {
        var left = bottles;
        var revenue = 0.0;
        foreach (var lot in Events.SellableLots(ctx))
        {
            var n = Math.Min(left, lot.Bottles);
            revenue += n * Market.BottlePrice(lot, ctx.Player, ctx.State) * share;
            left -= n;
            if (left <= 0) break;
        }
        return Js.Round(revenue);
    }

    /// <summary>Ein Computer-Nachbar im selben Gebiet, der nicht verfeindet ist – der freundlichste zuerst</summary>
    static Player? Neighbor(EventContext ctx, double minRelation = -10)
    {
        if (ctx.Player.Ai is not null) return null;
        return ctx.State.Players
            .Where(p => p.Ai is not null && !p.Bankrupt && p.RegionId == ctx.Player.RegionId && Rivals.Relation(p, ctx.Player.Id) >= minRelation)
            .OrderByDescending(p => Rivals.Relation(p, ctx.Player.Id))
            .FirstOrDefault();
    }

    static Player PartnerOf(EventContext ctx, Decision d) => ctx.State.Players[(int)d.Params["partner"]];

    /// <summary>Fertiger Wein im Tank, den ein Nachbar kaufen könnte</summary>
    static WineBatch? SellableBatch(EventContext ctx) =>
        ctx.Player.Cellar
            .Where(b => !b.Barrique && b.MonthsUntilReady == 0 && b.Hectoliters >= 20)
            .OrderByDescending(b => b.Hectoliters)
            .FirstOrDefault();

    static double BearingHa(EventContext ctx)
    {
        var sum = 0.0;
        foreach (var p in Events.BearingPlots(ctx)) sum += p.Hectares;
        return sum;
    }

    static readonly GrapeId[] GrapeIds = [.. Grapes.All.Keys];

    /// <summary>Titel ohne das führende Symbol (<c>title.replace(/^\S+\s/, '')</c>)</summary>
    static string PlainTitle(string title) => Regex.Replace(title, @"^\S+\s", "");

    public static readonly List<DecisionDef> DecisionEvents =
    [
        // ── Alltag ───────────────────────────────────────────────────────────────────
        new()
        {
            Id = "stadtweinfest", Rarity = Rarity.Alltag, Months = [5, 6, 7, 8],
            Offer = c =>
            {
                var rent = c.Between(1500, 2500, 100);
                return new Decision
                {
                    Title = Lang.T("🎪 Stand auf dem Stadtweinfest", "🎪 Stall at the Town Wine Festival"),
                    Text = (Lang.En ? $"The big wine festival in the county town is still looking for winemakers. The stall rent is {Util.FormatEuro(rent)}." : $"Das große Weinfest in der Kreisstadt sucht noch Winzer. Die Standmiete beträgt {Util.FormatEuro(rent)}."),
                    Options = [Option(Lang.T("Stand mieten", "Rent a stall"), Lang.T("+3 Bekanntheit, nächsten Monat 40 % mehr Nachfrage", "+3 reputation, 40 % more demand next month"), rent), Decline()],
                    Fallback = 1,
                    Params = new() { ["rent"] = rent },
                };
            },
            Ai = (c, d) => Comfortable(c.Player, d.Params["rent"]) ? 0 : 1,
            Resolve = (c, choice, d) =>
            {
                if (choice == 1) return new Outcome(Lang.T("Du verzichtest auf einen Stand beim Stadtweinfest.", "You skip a stall at the Town Wine Festival."));
                var paid = Pay(c, d.Params["rent"]);
                Events.AddEffect(c.Player, EffectKind.Demand, 1.4, 1, Lang.T("Nach dem Stadtweinfest", "After the Town Wine Festival"));
                return new Outcome((Lang.En ? $"Your stall at the Town Wine Festival is well attended ({paid}, {Events.Reputation(c, 3)})." : $"Dein Stand auf dem Stadtweinfest ist gut besucht ({paid}, {Events.Reputation(c, 3)})."), LogTone.Good);
            },
        },
        new()
        {
            Id = "vereinsfest", Rarity = Rarity.Alltag, When = c => Events.BottleCount(Events.SellableLots(c)) >= 60,
            Offer = _ => new Decision
            {
                Title = Lang.T("⚽ Spende fürs Vereinsfest", "⚽ Donation for the club festival"),
                Text = Lang.T("Der Sportverein im Ort bittet um 60 Flaschen Wein für sein Sommerfest – als Spende.", "The local sports club asks for 60 bottles of wine for its summer festival – as a donation."),
                Options = [Option(Lang.T("Spenden", "Donate"), Lang.T("60 Flaschen weniger, +1,5 Bekanntheit", "60 bottles fewer, +1.5 reputation")), Option(Lang.T("Ablehnen", "Decline"), Lang.T("Im Ort wird getuschelt: −0,5 Bekanntheit", "The village gossips: −0.5 reputation"))],
                Fallback = 1,
                Params = [],
            },
            Ai = (_, _) => 0,
            Resolve = (c, choice, _) =>
            {
                if (choice == 1) return new Outcome((Lang.En ? $"No donation for the sports club ({Events.Reputation(c, -0.5)})." : $"Keine Spende für den Sportverein ({Events.Reputation(c, -0.5)})."), LogTone.Bad);
                Events.SellBottles(c, Events.SellableLots(c), 60, 0);
                return new Outcome((Lang.En ? $"The sports club thanks you for 60 bottles ({Events.Reputation(c, 1.5)})." : $"Der Sportverein bedankt sich für 60 Flaschen ({Events.Reputation(c, 1.5)})."), LogTone.Good);
            },
        },
        new()
        {
            Id = "influencer-werbung", Rarity = Rarity.Alltag,
            Offer = c =>
            {
                var fee = c.Between(800, 1500, 100);
                return new Decision
                {
                    Title = Lang.T("📱 Bezahlte Werbung", "📱 Paid advertising"),
                    Text = (Lang.En ? $"A wine influencer offers you a sponsored post for {Util.FormatEuro(fee)}." : $"Eine Wein-Influencerin bietet dir einen gesponserten Beitrag für {Util.FormatEuro(fee)} an."),
                    Options = [Option(Lang.T("Buchen", "Book"), Lang.T("Meist: +3 Bekanntheit und 2 Monate 25 % mehr Nachfrage – manchmal verpufft es", "Usually: +3 reputation and 25 % more demand for 2 months – sometimes it fizzles out"), fee), Decline()],
                    Fallback = 1,
                    Params = new() { ["fee"] = fee },
                };
            },
            Ai = (c, d) => Comfortable(c.Player, d.Params["fee"]) && c.Player.Reputation < 40 ? 0 : 1,
            Resolve = (c, choice, d) =>
            {
                if (choice == 1) return new Outcome(Lang.T("Du lehnst die bezahlte Werbung ab.", "You turn down the paid advertising."));
                var paid = Pay(c, d.Params["fee"]);
                if (c.Roll() < 0.7)
                {
                    Events.AddEffect(c.Player, EffectKind.Demand, 1.25, 2, Lang.T("Gesponserter Beitrag", "Sponsored post"));
                    return new Outcome((Lang.En ? $"The sponsored post goes down well ({paid}, {Events.Reputation(c, 3)})." : $"Der gesponserte Beitrag kommt gut an ({paid}, {Events.Reputation(c, 3)})."), LogTone.Good);
                }
                return new Outcome((Lang.En ? $"The sponsored post fizzles out – hardly anyone reacts ({paid})." : $"Der gesponserte Beitrag verpufft – kaum jemand reagiert ({paid})."), LogTone.Bad);
            },
        },

        // ── Gelegentlich ─────────────────────────────────────────────────────────────
        new()
        {
            Id = "discounter", Rarity = Rarity.Gelegentlich, When = c => Events.BottleCount(Events.SellableLots(c)) >= 1000,
            Offer = c =>
            {
                var bottles = Math.Min(Events.BottleCount(Events.SellableLots(c)), c.Between(800, 2000, 100));
                var revenue = ExpectedRevenue(c, bottles, 0.5);
                return new Decision
                {
                    Title = Lang.T("🛒 Großauftrag vom Discounter", "🛒 Big order from a discount chain"),
                    Text = (Lang.En ? $"A discount chain wants {Util.FormatNumber(bottles)} bottles in one go – at half the cellar-door price." : $"Eine Discounter-Kette will {Util.FormatNumber(bottles)} Flaschen auf einen Schlag – zum halben Ab-Hof-Preis."),
                    Options = [Option(Lang.T("Verkaufen", "Sell"), (Lang.En ? $"about +{Util.FormatEuro(revenue)} right away, but −3 reputation (bargain-bin image)" : $"rund +{Util.FormatEuro(revenue)} sofort, aber −3 Bekanntheit (Ramsch-Image)")), Decline()],
                    Fallback = 1,
                    Params = new() { ["bottles"] = bottles },
                };
            },
            Ai = (c, _) => c.Player.Money < 20_000 ? 0 : 1,
            Resolve = (c, choice, d) =>
            {
                if (choice == 1) return new Outcome(Lang.T("Du lehnst den Großauftrag des Discounters ab.", "You turn down the discounter's big order."));
                var (sold, revenue) = Events.SellBottles(c, Events.SellableLots(c), d.Params["bottles"], 0.5);
                return new Outcome((Lang.En ? $"{Util.JsNumber(sold)} bottles go to the discounter: +{Util.FormatEuro(revenue)} ({Events.Reputation(c, -3)})." : $"{Util.JsNumber(sold)} Flaschen gehen an den Discounter: +{Util.FormatEuro(revenue)} ({Events.Reputation(c, -3)})."), LogTone.Good);
            },
        },
        new()
        {
            Id = "journalist", Rarity = Rarity.Gelegentlich,
            Offer = c =>
            {
                var cost = c.Between(1500, 3000, 100);
                return new Decision
                {
                    Title = Lang.T("📰 Reportage im Weinmagazin", "📰 Feature in the wine magazine"),
                    Text = (Lang.En ? $"A journalist from a major wine magazine wants to write about your estate. Hospitality and time cost about {Util.FormatEuro(cost)}." : $"Ein Journalist eines großen Weinmagazins möchte über dein Gut schreiben. Bewirtung und Zeitaufwand kosten rund {Util.FormatEuro(cost)}."),
                    Options =
                    [
                        Option(Lang.T("Einladen", "Invite"), Lang.T("Meist ein begeisterter Artikel (+6 Bekanntheit, mehr Nachfrage) – manchmal ein Verriss (−3)", "Usually a glowing article (+6 reputation, more demand) – sometimes a pan (−3)"), cost),
                        Option(Lang.T("Absagen", "Decline"), Lang.T("Es passiert nichts.", "Nothing happens.")),
                    ],
                    Fallback = 1,
                    Params = new() { ["cost"] = cost },
                };
            },
            Ai = (c, d) => Comfortable(c.Player, d.Params["cost"] * 3) ? 0 : 1,
            Resolve = (c, choice, d) =>
            {
                if (choice == 1) return new Outcome(Lang.T("Du sagst dem Journalisten ab.", "You turn the journalist down."));
                var paid = Pay(c, d.Params["cost"]);
                if (c.Roll() < 0.7)
                {
                    Events.AddEffect(c.Player, EffectKind.Demand, 1.2, 2, Lang.T("Reportage im Weinmagazin", "Feature in the wine magazine"));
                    return new Outcome((Lang.En ? $"The feature is a hymn to your estate ({paid}, {Events.Reputation(c, 6)})." : $"Die Reportage ist eine Hymne auf dein Gut ({paid}, {Events.Reputation(c, 6)})."), LogTone.Good);
                }
                return new Outcome((Lang.En ? $"The journalist pans your wine ({paid}, {Events.Reputation(c, -3)})." : $"Der Journalist verreißt deinen Wein ({paid}, {Events.Reputation(c, -3)})."), LogTone.Bad);
            },
        },
        new()
        {
            Id = "erntehelfer-lohn", Rarity = Rarity.Gelegentlich, Months = [8, 9], When = c => Events.HasBearing(c),
            Offer = c =>
            {
                var extra = Js.Round(BearingHa(c) * c.Between(200, 400, 10) / 100) * 100;
                return new Decision
                {
                    Title = Lang.T("✊ Erntehelfer wollen mehr Lohn", "✊ Harvest workers want higher pay"),
                    Text = (Lang.En ? $"Just before the harvest the workers demand a bonus of {Util.FormatEuro(extra)}." : $"Kurz vor der Lese fordern die Erntehelfer einen Zuschlag von {Util.FormatEuro(extra)}."),
                    Options =
                    [
                        Option(Lang.T("Zahlen", "Pay"), Lang.T("Die Lese läuft wie geplant.", "The harvest goes as planned."), extra),
                        Option(Lang.T("Ablehnen", "Decline"), Lang.T("Einige reisen ab – rund 15 % weniger Ertrag auf allen tragenden Lagen", "Some leave – about 15 % less yield on all bearing plots")),
                    ],
                    Fallback = 1,
                    Params = new() { ["extra"] = extra },
                };
            },
            Ai = (c, d) => Affordable(c.Player, d.Params["extra"]) ? 0 : 1,
            Resolve = (c, choice, d) =>
            {
                if (choice == 0) return new Outcome((Lang.En ? $"You pay the harvest workers a bonus ({Pay(c, d.Params["extra"])})." : $"Du zahlst den Erntehelfern einen Zuschlag ({Pay(c, d.Params["extra"])})."));
                foreach (var plot in Events.BearingPlots(c)) plot.CropFactor *= 0.85;
                return new Outcome(Lang.T("Ein Teil der Erntehelfer reist ab – rund 15 % der Trauben bleiben hängen.", "Some of the harvest workers leave – about 15 % of the grapes stay on the vine."), LogTone.Bad);
            },
        },
        new()
        {
            Id = "gebrauchte-tanks", Rarity = Rarity.Gelegentlich,
            Offer = c =>
            {
                var hl = c.Between(100, 200, 50);
                var price = Js.Round(hl * Economy.TankCostPerHl * 0.5 / 100) * 100;
                return new Decision
                {
                    Title = Lang.T("🛢️ Gebrauchte Tanks günstig", "🛢️ Cheap used tanks"),
                    Text = (Lang.En ? $"An estate is closing down and selling {Util.JsNumber(hl)} hl of stainless steel tanks for {Util.FormatEuro(price)} – half the price of new." : $"Ein Weingut gibt auf und verkauft {Util.JsNumber(hl)} hl Edelstahltanks für {Util.FormatEuro(price)} – halb so viel wie neu."),
                    Options = [Option(Lang.T("Kaufen", "Buy"), (Lang.En ? $"+{Util.JsNumber(hl)} hl tank capacity" : $"+{Util.JsNumber(hl)} hl Tankkapazität"), price), Decline()],
                    Fallback = 1,
                    Params = new() { ["hl"] = hl, ["price"] = price },
                };
            },
            Ai = (c, d) => Rules.UsedTankHl(c.Player) > c.Player.TankCapacityHl * 0.7 && Comfortable(c.Player, d.Params["price"]) ? 0 : 1,
            Resolve = (c, choice, d) =>
            {
                if (choice == 1) return new Outcome(Lang.T("Du lässt die gebrauchten Tanks stehen.", "You leave the used tanks."));
                var paid = Pay(c, d.Params["price"]);
                c.Player.TankCapacityHl += d.Params["hl"];
                return new Outcome((Lang.En ? $"Bargain: bought {Util.JsNumber(d.Params["hl"])} hl of used tanks ({paid})." : $"Schnäppchen: {Util.JsNumber(d.Params["hl"])} hl gebrauchte Tanks gekauft ({paid})."), LogTone.Good);
            },
        },

        // ── Nachbarschaft: Angebote freundlicher Computergegner ─────────────────────
        new()
        {
            Id = "kooperation-messestand", Rarity = Rarity.Gelegentlich, When = c => Neighbor(c) is not null,
            Offer = c =>
            {
                var partner = Neighbor(c)!;
                var share = c.Between(3000, 5000, 500);
                return new Decision
                {
                    Title = (Lang.En ? $"🤝 Joint trade fair stand with {partner.Name}" : $"🤝 Gemeinsamer Messestand mit {partner.Name}"),
                    Text = (Lang.En ? $"{partner.Name} suggests exhibiting together at the regional trade fair. Your share: {Util.FormatEuro(share)}." : $"{partner.Name} schlägt vor, auf der Regionalmesse gemeinsam auszustellen. Dein Anteil: {Util.FormatEuro(share)}."),
                    Options =
                    [
                        Option(Lang.T("Mitmachen", "Join in"), Lang.T("Beide +5 Bekanntheit, das Verhältnis zum Nachbarn wird besser", "Both +5 reputation, your relationship with the neighbor improves"), share),
                        Option(Lang.T("Ablehnen", "Decline"), Lang.T("Der Nachbar ist ein wenig verstimmt", "The neighbor is a bit put out")),
                    ],
                    Fallback = 1,
                    Params = new() { ["share"] = share, ["partner"] = c.State.Players.IndexOf(partner) },
                };
            },
            Ai = (_, _) => 1,
            Resolve = (c, choice, d) =>
            {
                var partner = PartnerOf(c, d);
                if (choice == 1)
                {
                    Rivals.AdjustRelation(partner, c.Player.Id, -5);
                    return new Outcome((Lang.En ? $"You decline the joint trade fair stand with {partner.Name}." : $"Du lehnst den gemeinsamen Messestand mit {partner.Name} ab."));
                }
                var paid = Pay(c, d.Params["share"]);
                Rules.ChangeReputation(partner, 5);
                Rivals.AdjustRelation(partner, c.Player.Id, 15);
                return new Outcome((Lang.En ? $"The joint trade fair stand with {partner.Name} is a success ({paid}, {Events.Reputation(c, 5)})." : $"Der gemeinsame Messestand mit {partner.Name} ist ein Erfolg ({paid}, {Events.Reputation(c, 5)})."), LogTone.Good);
            },
        },
        new()
        {
            Id = "kooperation-weinkauf", Rarity = Rarity.Gelegentlich, Months = [3, 4, 5, 6, 7, 8],
            When = c => Neighbor(c) is not null && SellableBatch(c) is not null,
            Offer = c =>
            {
                var partner = Neighbor(c)!;
                var batch = SellableBatch(c)!;
                var hectoliters = Math.Min(batch.Hectoliters, c.Between(20, 40, 5));
                var price = Js.Round(Market.BulkPricePerHl(batch, c.State, c.Player) * hectoliters * 1.3);
                return new Decision
                {
                    Title = (Lang.En ? $"🍷 {partner.Name} needs wine" : $"🍷 {partner.Name} braucht Wein"),
                    Text = (Lang.En ? $"{partner.Name} is short of wine for their customers and wants to buy {Util.JsNumber(hectoliters)} hl {Rules.WineLabel(batch)} from you – for {Util.FormatEuro(price)}, 30 % above the bulk wine price." : $"{partner.Name} hat zu wenig Wein für seine Kunden und möchte dir {Util.JsNumber(hectoliters)} hl {Rules.WineLabel(batch)} abkaufen – für {Util.FormatEuro(price)}, 30 % über dem Fassweinpreis."),
                    Options =
                    [
                        Option(Lang.T("Verkaufen", "Sell"), (Lang.En ? $"+{Util.FormatEuro(price)}, your relationship with the neighbor improves" : $"+{Util.FormatEuro(price)}, das Verhältnis zum Nachbarn wird besser")),
                        Option(Lang.T("Ablehnen", "Decline"), Lang.T("Der Wein bleibt in deinem Tank", "The wine stays in your tank")),
                    ],
                    Fallback = 1,
                    Params = new() { ["partner"] = c.State.Players.IndexOf(partner), ["hectoliters"] = hectoliters, ["price"] = price },
                };
            },
            Ai = (_, _) => 1,
            Resolve = (c, choice, d) =>
            {
                var partner = PartnerOf(c, d);
                var batch = SellableBatch(c);
                if (choice == 1 || batch is null) return new Outcome((Lang.En ? $"You keep your wine – {partner.Name} looks elsewhere." : $"Du behältst deinen Wein – {partner.Name} sucht anderswo."));
                var hectoliters = Math.Min(batch.Hectoliters, d.Params["hectoliters"]);
                var label = Rules.WineLabel(batch);
                var bought = batch.Clone();
                bought.Id = Util.NewId(c.State, "wine");
                bought.Hectoliters = hectoliters;
                partner.Cellar.Add(bought);
                Rules.RemoveBatchVolume(c.Player, batch, hectoliters);
                c.Player.Money += d.Params["price"];
                Rivals.AdjustRelation(partner, c.Player.Id, 10);
                return new Outcome((Lang.En ? $"Sold {Util.JsNumber(hectoliters)} hl {label} to {partner.Name}: +{Util.FormatEuro(d.Params["price"])}." : $"{Util.JsNumber(hectoliters)} hl {label} an {partner.Name} verkauft: +{Util.FormatEuro(d.Params["price"])}."), LogTone.Good);
            },
        },
        new()
        {
            Id = "erntehilfe", Rarity = Rarity.Selten, Months = [9, 10], When = c => Events.HasBearing(c) && Neighbor(c, 20) is not null,
            Offer = c =>
            {
                var partner = Neighbor(c, 20)!;
                return new Decision
                {
                    Title = (Lang.En ? $"🧺 Harvest help from {partner.Name}" : $"🧺 Erntehilfe von {partner.Name}"),
                    Text = (Lang.En ? $"{partner.Name} has finished the harvest and offers you their experienced harvest workers – free of charge, between neighbors." : $"{partner.Name} ist mit der Lese fertig und bietet dir seine erfahrenen Erntehelfer an – unentgeltlich, unter Nachbarn."),
                    Options = [Option(Lang.T("Gern annehmen", "Gladly accept"), Lang.T("Sorgfältige Handlese: +3 Güte für diesen Jahrgang", "Careful hand harvest: +3 quality for this vintage")), Option(Lang.T("Danke, nein", "No, thanks"), Lang.T("Es passiert nichts", "Nothing happens"))],
                    Fallback = 1,
                    Params = new() { ["partner"] = c.State.Players.IndexOf(partner) },
                };
            },
            Ai = (_, _) => 1,
            Resolve = (c, choice, d) =>
            {
                var partner = PartnerOf(c, d);
                if (choice == 1) return new Outcome((Lang.En ? $"You thank {partner.Name} for the offer." : $"Du dankst {partner.Name} für das Angebot."));
                Events.AddEffect(c.Player, EffectKind.Quality, 3, 13 - c.State.Month, (Lang.En ? $"Harvest help from {partner.Name}" : $"Erntehilfe von {partner.Name}"));
                Rivals.AdjustRelation(partner, c.Player.Id, 5);
                return new Outcome((Lang.En ? $"{partner.Name}'s harvest workers help pick carefully (+3 quality for the vintage)." : $"Die Erntehelfer von {partner.Name} lesen sorgfältig mit (+3 Güte für den Jahrgang)."), LogTone.Good);
            },
        },

        // ── Selten ───────────────────────────────────────────────────────────────────
        new()
        {
            Id = "nachbarlage", Rarity = Rarity.Selten,
            Offer = c =>
            {
                var state = c.State;
                var player = c.Player;
                var hectares = c.Between(1, 3, 1);
                var site = c.Between(-1, 1, 1);
                var suitable = GrapeIds.Where(g => Regions.All[player.RegionId].Suitability[g] >= 0).ToArray();
                GrapeId? grape = c.Roll() < 0.7 ? suitable[(int)Math.Floor(c.Roll() * suitable.Length)] : null;
                int? plantedYear = grape is not null ? state.Year - (int)c.Between(5, 30, 1) : null;
                var estimate = Auction.ParcelValue(hectares, grape, plantedYear, site, player.RegionId, state.Year);
                var price = Js.Round(estimate * (0.85 + c.Roll() * 0.3) / 1000) * 1000;
                return new Decision
                {
                    Title = Lang.T("🤝 Der Nachbar will verkaufen", "🤝 The neighbor wants to sell"),
                    Text =
                        (Lang.En ? $"Your neighbor is retiring and privately offers you their site: {Util.JsNumber(hectares)} ha, {Auction.SiteLabels[site]}, " : $"Dein Nachbar setzt sich zur Ruhe und bietet dir privat seine Lage an: {Util.JsNumber(hectares)} ha, {Auction.SiteLabels[site]}, ") +
                        (Lang.En ? $"{(grape is { } gEn ? $"{Grapes.All[gEn].Name}, {state.Year - plantedYear!.Value} years old" : "fallow")}. Estimated value {Util.FormatEuro(Js.Round(estimate / 1000) * 1000)}." : $"{(grape is { } g ? $"{Grapes.All[g].Name}, {state.Year - plantedYear!.Value} Jahre alt" : "Brache")}. Schätzwert {Util.FormatEuro(Js.Round(estimate / 1000) * 1000)}."),
                    Options = [Option(Lang.T("Kaufen", "Buy"), (Lang.En ? $"For {Util.FormatEuro(price)} – no auction, any shortfall on credit" : $"Für {Util.FormatEuro(price)} – ohne Versteigerung, Fehlbetrag per Kredit"), price), Decline(Lang.T("Er verkauft an jemand anderen.", "They sell to someone else."))],
                    Fallback = 1,
                    Params = new()
                    {
                        ["hectares"] = hectares,
                        ["site"] = site,
                        ["grape"] = grape is { } id ? Array.IndexOf(GrapeIds, id) : -1,
                        ["plantedYear"] = plantedYear ?? -1,
                        ["price"] = price,
                    },
                };
            },
            Ai = (c, d) => Comfortable(c.Player, d.Params["price"]) ? 0 : 1,
            Resolve = (c, choice, d) =>
            {
                if (choice == 1) return new Outcome(Lang.T("Du lehnst das Angebot deines Nachbarn ab.", "You decline your neighbor's offer."));
                var player = c.Player;
                var state = c.State;
                var paid = Pay(c, d.Params["price"]);
                var taken = new HashSet<string>(player.Plots.Select(p => p.Name));
                var baseName = Lang.T("Nachbarlage", "Neighbor plot");
                var name = baseName;
                for (var n = 2; taken.Contains(name); n++) name = $"{baseName} {n}";
                GrapeId? grape = d.Params["grape"] >= 0 ? GrapeIds[(int)d.Params["grape"]] : null;
                player.Plots.Add(new Plot
                {
                    Id = Util.NewId(state, "plot"), Name = name, Hectares = d.Params["hectares"], Grape = grape,
                    PlantedYear = grape is not null ? (int)d.Params["plantedYear"] : null,
                    Site = d.Params["site"], Oechsle = 0, Health = 1, CropFactor = 1, HarvestedThisYear = false, Botrytis = false, IceWineGamble = false,
                });
                return new Outcome((Lang.En ? $"Handshake: the neighbor's site ({Util.JsNumber(d.Params["hectares"])} ha) is now yours ({paid})." : $"Handschlag: Die Lage des Nachbarn ({Util.JsNumber(d.Params["hectares"])} ha) gehört jetzt dir ({paid})."), LogTone.Good);
            },
        },
        new()
        {
            Id = "kellermeister-abwerbung", Rarity = Rarity.Selten, When = c => c.Player.CellarMaster is not null,
            Offer = c =>
            {
                var master = c.Player.CellarMaster!;
                var bonus = Js.Round(master.Salary * c.Between(12, 20, 1) / 10 / 500) * 500;
                return new Decision
                {
                    Title = Lang.T("🧑‍🔬 Dein Kellermeister wird abgeworben", "🧑‍🔬 Your cellar master is being poached"),
                    Text = (Lang.En ? $"A large estate wants to poach {master.Name}. With a bonus of {Util.FormatEuro(bonus)}, {master.Name.Split(' ')[0]} would stay." : $"Ein Großweingut will {master.Name} abwerben. Mit einer Prämie von {Util.FormatEuro(bonus)} würde {master.Name.Split(' ')[0]} bleiben."),
                    Options =
                    [
                        Option(Lang.T("Prämie zahlen", "Pay the bonus"), Lang.T("Bleibt – mit allen Fähigkeiten und der Erfahrung.", "Stays – with all skills and experience."), bonus),
                        Option(Lang.T("Ziehen lassen", "Let them go"), Lang.T("Geht: kein Gehalt mehr, aber auch kein Güte-Zuschlag und keine Spezialweine mehr", "Leaves: no more salary, but also no quality bonus and no special wines")),
                    ],
                    Fallback = 1,
                    Params = new() { ["bonus"] = bonus },
                };
            },
            Ai = (c, d) => Affordable(c.Player, d.Params["bonus"]) ? 0 : 1,
            Resolve = (c, choice, d) =>
            {
                var name = c.Player.CellarMaster?.Name ?? Lang.T("Dein Kellermeister", "Your cellar master");
                if (choice == 0) return new Outcome((Lang.En ? $"{name} stays (bonus {Pay(c, d.Params["bonus"])})." : $"{name} bleibt (Prämie {Pay(c, d.Params["bonus"])})."));
                c.Player.CellarMaster = null;
                return new Outcome((Lang.En ? $"{name} switches to the competition." : $"{name} wechselt zur Konkurrenz."), LogTone.Bad);
            },
        },
        new()
        {
            Id = "fernsehkoch", Rarity = Rarity.Selten, When = c => Events.BottleCount(Events.SellableLots(c)) >= 600,
            Offer = c => new Decision
            {
                Title = Lang.T("👨‍🍳 Auftritt in einer Kochsendung", "👨‍🍳 Appearance on a cooking show"),
                Text = Lang.T("Ein bekannter Fernsehkoch will deinen Wein in seiner Sendung vorstellen – wenn du 600 Flaschen zu 40 % des Preises lieferst.", "A famous TV chef wants to feature your wine on his show – if you deliver 600 bottles at 40 % of the price."),
                Options =
                [
                    Option(Lang.T("Liefern", "Deliver"), (Lang.En ? $"600 btl. for about {Util.FormatEuro(ExpectedRevenue(c, 600, 0.4))}, +8 reputation, 40 % more demand for three months" : $"600 Fl. für rund {Util.FormatEuro(ExpectedRevenue(c, 600, 0.4))}, +8 Bekanntheit, drei Monate 40 % mehr Nachfrage")),
                    Decline(),
                ],
                Fallback = 1,
                Params = [],
            },
            Ai = (_, _) => 0,
            Resolve = (c, choice, _) =>
            {
                if (choice == 1) return new Outcome(Lang.T("Du lehnst den Auftritt in der Kochsendung ab.", "You turn down the cooking show appearance."));
                var (_, revenue) = Events.SellBottles(c, Events.SellableLots(c), 600, 0.4);
                Events.AddEffect(c.Player, EffectKind.Demand, 1.4, 3, Lang.T("Auftritt in der Kochsendung", "Cooking show appearance"));
                return new Outcome((Lang.En ? $"Your wine shines on the cooking show (+{Util.FormatEuro(revenue)}, {Events.Reputation(c, 8)})." : $"Dein Wein glänzt in der Kochsendung (+{Util.FormatEuro(revenue)}, {Events.Reputation(c, 8)})."), LogTone.Good);
            },
        },
        new()
        {
            Id = "weinmesse-einladung", Rarity = Rarity.Selten,
            Offer = c =>
            {
                var fee = c.Between(6000, 10_000, 500);
                return new Decision
                {
                    Title = Lang.T("🌐 Einladung zur internationalen Weinmesse", "🌐 Invitation to the international wine fair"),
                    Text = (Lang.En ? $"You can exhibit at your region's joint stand. Stand share and travel cost {Util.FormatEuro(fee)}." : $"Du kannst auf dem Gemeinschaftsstand deines Gebiets ausstellen. Standanteil und Reise kosten {Util.FormatEuro(fee)}."),
                    Options = [Option(Lang.T("Teilnehmen", "Take part"), Lang.T("+6 Bekanntheit, ein Jahr lang 5 % höhere Preise", "+6 reputation, 5 % higher prices for a year"), fee), Decline()],
                    Fallback = 1,
                    Params = new() { ["fee"] = fee },
                };
            },
            Ai = (c, d) => Comfortable(c.Player, d.Params["fee"] * 2) ? 0 : 1,
            Resolve = (c, choice, d) =>
            {
                if (choice == 1) return new Outcome(Lang.T("Du verzichtest auf die internationale Weinmesse.", "You skip the international wine fair."));
                var paid = Pay(c, d.Params["fee"]);
                Events.AddEffect(c.Player, EffectKind.Price, 1.05, 12, Lang.T("Kontakte von der Weinmesse", "Wine fair contacts"));
                return new Outcome((Lang.En ? $"You make valuable contacts at the wine fair ({paid}, {Events.Reputation(c, 6)})." : $"Auf der Weinmesse knüpfst du wertvolle Kontakte ({paid}, {Events.Reputation(c, 6)})."), LogTone.Good);
            },
        },
    ];

    // ── Reaktion auf Sabotage ──────────────────────────────────────────────────────
    // Kein Zufallsereignis: Sie wird angeboten, wenn ein Mensch sabotiert wurde, und gilt bis Ende des Folgemonats –
    // so kann das Opfer reagieren, auch wenn der Täter erst nach ihm am Zug war.

    static readonly SabotageKind[] SabotageKinds = [SabotageKind.Geruecht, SabotageKind.Weinberg];

    static Player CulpritOf(EventContext ctx, Decision d) => ctx.State.Players[(int)d.Params["culprit"]];

    /// <summary>Täter zahlt dem Opfer Schadensersatz (notfalls ins Minus)</summary>
    static void Compensate(EventContext ctx, Player culprit, double amount)
    {
        culprit.Money -= amount;
        ctx.Player.Money += amount;
    }

    static string Guard(EventContext ctx)
    {
        Events.AddEffect(ctx.Player, EffectKind.Guard, 1, Economy.GuardMonths, Lang.T("Nachtwache im Weinberg", "Night watch in the vineyard"));
        return (Lang.En ? $"A night watch guards your estate from now on for {Economy.GuardMonths} months ({Pay(ctx, Economy.GuardCost)})." : $"Eine Nachtwache bewacht ab sofort {Economy.GuardMonths} Monate lang dein Gut ({Pay(ctx, Economy.GuardCost)}).");
    }

    static readonly DecisionDef SabotageReaction = new()
    {
        Id = "sabotage-reaktion",
        Rarity = Rarity.Selten,
        When = _ => false,
        Offer = _ => throw new InvalidOperationException("wird über offerSabotageReaction angeboten"),
        Ai = (_, d) => d.Fallback,
        Resolve = (c, choice, d) =>
        {
            var culprit = CulpritOf(c, d);
            var damage = d.Params["damage"];
            var known = d.Params["detected"] == 1;
            if (choice == 1) return new Outcome(Guard(c), LogTone.Good);
            if (choice == 2)
            {
                if (!known) return new Outcome(Lang.T("Du nimmst die Sabotage hin und blickst nach vorn.", "You accept the sabotage and look ahead."));
                return new Outcome((Lang.En ? $"You generously drop any lawsuit against {culprit.Name} ({Events.Reputation(c, 2)})." : $"Du verzichtest großmütig auf eine Klage gegen {culprit.Name} ({Events.Reputation(c, 2)})."), LogTone.Good);
            }
            if (known)
            {
                var lawyer = Pay(c, Economy.LawsuitCost);
                if (c.Roll() >= Economy.LawsuitSuccess) return new Outcome((Lang.En ? $"The lawsuit against {culprit.Name} is dismissed ({lawyer} legal costs)." : $"Die Klage gegen {culprit.Name} wird abgewiesen ({lawyer} Anwaltskosten)."), LogTone.Bad);
                Compensate(c, culprit, damage);
                Util.AddLog(c.State, culprit.Id, (Lang.En ? $"{c.Player.Name} sued you over the sabotage: {Util.FormatEuro(damage)} compensation." : $"{c.Player.Name} hat dich wegen der Sabotage verklagt: {Util.FormatEuro(damage)} Entschädigung."), LogTone.Bad);
                return new Outcome((Lang.En ? $"The court rules in your favor: {culprit.Name} pays {Util.FormatEuro(damage)} compensation ({lawyer} legal costs)." : $"Das Gericht gibt dir recht: {culprit.Name} zahlt {Util.FormatEuro(damage)} Entschädigung ({lawyer} Anwaltskosten)."), LogTone.Good);
            }
            var paid = Pay(c, Economy.DetectiveCost);
            if (c.Roll() >= Economy.DetectiveSuccess) return new Outcome((Lang.En ? $"The detective agency finds no usable lead ({paid})." : $"Die Detektei findet keine verwertbare Spur ({paid})."), LogTone.Bad);
            culprit.Money -= Economy.SabotageFine;
            Rules.ChangeReputation(culprit, -Economy.SabotageReputation);
            Compensate(c, culprit, damage);
            Rivals.AdjustRelation(culprit, c.Player.Id, -20);
            Util.AddLog(
                c.State,
                null,
                (Lang.En ? $"A detective agency convicts {culprit.Name} of sabotage against {c.Player.Name}: {Util.FormatEuro(Economy.SabotageFine)} fine and {Util.FormatEuro(damage)} damages." : $"Eine Detektei überführt {culprit.Name} der Sabotage gegen {c.Player.Name}: {Util.FormatEuro(Economy.SabotageFine)} Strafe und {Util.FormatEuro(damage)} Schadensersatz."));
            return new Outcome((Lang.En ? $"The detective agency has caught {culprit.Name} – you receive {Util.FormatEuro(damage)} damages ({paid})." : $"Die Detektei hat {culprit.Name} überführt – du erhältst {Util.FormatEuro(damage)} Schadensersatz ({paid})."), LogTone.Good);
        },
    };

    /// <summary>
    /// Ein Mensch wurde sabotiert: Er darf reagieren – Detektei oder Klage, Nachtwache oder hinnehmen.
    /// Eine noch offene Entscheidung wird dafür mit ihrer vorsichtigen Wahl abgeschlossen.
    /// </summary>
    public static void OfferSabotageReaction(GameState state, Player victim, Player culprit, SabotageKind kind, bool detected)
    {
        if (victim.Ai is not null) return; // Computergegner merken sich so etwas über ihr Verhältnis
        if (victim.Decision is not null) ExpireDecision(state, victim, true);
        // Erwischte Täter haben nichts angerichtet – eine Klage bringt dann nur eine Entschädigung für den Ärger
        var damage = detected ? Economy.SabotageDamages[kind] / 2 : Economy.SabotageDamages[kind];
        var what = kind == SabotageKind.Geruecht ? Lang.T("Üble Gerüchte über dein Weingut", "Nasty rumors about your estate") : Lang.T("Beschädigte Rebstöcke", "Damaged vines");
        List<DecisionOption> options = detected
            ?
            [
                Option(Lang.T("Auf Entschädigung klagen", "Sue for compensation"), (Lang.En ? $"{Util.JsNumber(Js.Round(Economy.LawsuitSuccess * 100))} % chance of {Util.FormatEuro(damage)} from {culprit.Name}" : $"{Util.JsNumber(Js.Round(Economy.LawsuitSuccess * 100))} % Aussicht auf {Util.FormatEuro(damage)} von {culprit.Name}"), Economy.LawsuitCost),
                Option(Lang.T("Nachtwache anheuern", "Hire a night watch"), (Lang.En ? $"{Economy.GuardMonths} months of protection: saboteurs are almost certain to be caught" : $"{Economy.GuardMonths} Monate Schutz: Saboteure werden fast sicher erwischt"), Economy.GuardCost),
                Option(Lang.T("Großmütig verzeihen", "Generously forgive"), Lang.T("+2 Bekanntheit", "+2 reputation")),
            ]
            :
            [
                Option(
                    Lang.T("Detektei beauftragen", "Hire a detective agency"),
                    (Lang.En ? $"{Util.JsNumber(Js.Round(Economy.DetectiveSuccess * 100))} % chance of catching the culprit: a fine for them, {Util.FormatEuro(damage)} damages for you" : $"{Util.JsNumber(Js.Round(Economy.DetectiveSuccess * 100))} % Aussicht, den Täter zu überführen: Strafe für ihn, {Util.FormatEuro(damage)} Schadensersatz für dich"),
                    Economy.DetectiveCost),
                Option(Lang.T("Nachtwache anheuern", "Hire a night watch"), (Lang.En ? $"{Economy.GuardMonths} months of protection: saboteurs are almost certain to be caught" : $"{Economy.GuardMonths} Monate Schutz: Saboteure werden fast sicher erwischt"), Economy.GuardCost),
                Option(Lang.T("Hinnehmen", "Accept it"), Lang.T("Es passiert nichts.", "Nothing happens.")),
            ];
        victim.Decision = new Decision
        {
            EventId = SabotageReaction.Id,
            Title = Lang.T("🕵️ Sabotage!", "🕵️ Sabotage!"),
            Text = detected
                ? (Lang.En ? $"{culprit.Name} tried to harm you ({(kind == SabotageKind.Geruecht ? "rumors" : "damaging your vines")}), but was caught and has to pay a fine. Want to follow up?" : $"{culprit.Name} wollte dir schaden ({(kind == SabotageKind.Geruecht ? "Gerüchte" : "Rebstöcke beschädigen")}), wurde aber erwischt und muss Strafe zahlen. Willst du nachlegen?")
                : (Lang.En ? $"{what} – someone wants to harm you. Who, is unclear. How do you react?" : $"{what} – jemand will dir schaden. Wer, ist unklar. Wie reagierst du?"),
            Options = options,
            Fallback = 2,
            Params = new()
            {
                ["culprit"] = state.Players.IndexOf(culprit),
                ["kind"] = Array.IndexOf(SabotageKinds, kind),
                ["damage"] = damage,
                ["detected"] = detected ? 1 : 0,
            },
            Deadline = (state.Year - 1) * 12 + state.Month + 1,
        };
        Util.AddLog(state, victim.Id, Lang.T("Entscheidung gefragt: Reaktion auf die Sabotage.", "Decision needed: reaction to the sabotage."), LogTone.Bad);
    }

    /// <summary>Entscheidungen, die gerade angeboten werden können</summary>
    public static List<DecisionDef> PossibleDecisions(GameState state, Player player)
    {
        if (player.Decision is not null) return [];
        var ctx = Events.Context(state, player);
        var result = new List<DecisionDef>();
        foreach (var d in DecisionEvents)
        {
            if ((d.Months is null || d.Months.Contains(state.Month)) && (d.When is null || d.When(ctx))) result.Add(d);
        }
        return result;
    }

    /// <summary>Bietet eine Entscheidung an; Computergegner entscheiden sofort.</summary>
    public static void OfferDecision(GameState state, Player player, DecisionDef def)
    {
        var ctx = Events.Context(state, player);
        var decision = def.Offer(ctx);
        decision.EventId = def.Id;
        if (player.Ai is not null)
        {
            var choice = def.Ai(ctx, decision);
            if (!Affordable(player, decision.Options[choice].Cost)) choice = decision.Fallback;
            ApplyDecision(state, player, decision, choice);
            return;
        }
        player.Decision = decision;
        Util.AddLog(state, player.Id, (Lang.En ? $"Decision needed: {PlainTitle(decision.Title)}." : $"Entscheidung gefragt: {PlainTitle(decision.Title)}."));
    }

    static void ApplyDecision(GameState state, Player player, Decision decision, int choice)
    {
        var def = DecisionEvents.Append(SabotageReaction).First(d => d.Id == decision.EventId);
        var outcome = def.Resolve(Events.Context(state, player), choice, decision);
        Util.AddLog(state, player.Id, outcome.Text, outcome.Tone);
    }

    public static string? ValidateDecide(Player player, int choice)
    {
        var decision = player.Decision;
        if (decision is null) return Lang.T("Es steht keine Entscheidung an.", "No decision is pending.");
        if (choice < 0 || choice >= decision.Options.Count) return Lang.T("Ungültige Wahl.", "Invalid choice.");
        var option = decision.Options[choice];
        if (!Affordable(player, option.Cost)) return (Lang.En ? $"Can't afford it (need {Util.FormatEuro(option.Cost)})." : $"Nicht finanzierbar (benötigt {Util.FormatEuro(option.Cost)}).");
        return null;
    }

    public static void Decide(GameState state, Player player, int choice)
    {
        var decision = player.Decision!;
        player.Decision = null;
        ApplyDecision(state, player, decision, choice);
    }

    /// <summary>Monatsende: Ohne Antwort gilt die vorsichtige Wahl. <paramref name="force"/> schließt auch eine Entscheidung mit späterer Frist ab.</summary>
    public static void ExpireDecision(GameState state, Player player, bool force = false)
    {
        var decision = player.Decision;
        if (decision is null) return;
        if (!force && decision.Deadline is { } deadline && deadline > (state.Year - 1) * 12 + state.Month) return;
        player.Decision = null;
        Util.AddLog(state, player.Id, (Lang.En ? $"No answer to “{PlainTitle(decision.Title)}” – the default applies: {decision.Options[decision.Fallback].Label}." : $"Keine Antwort auf „{PlainTitle(decision.Title)}“ – es gilt: {decision.Options[decision.Fallback].Label}."));
        ApplyDecision(state, player, decision, decision.Fallback);
    }

    /// <summary>Monatliches Zufallsereignis: Glücksfall, Rückschlag oder Entscheidung</summary>
    public static void MonthlyEvent(GameState state, Player player)
    {
        var decisions = PossibleDecisions(state, player)
            .Select(def => new ExtraCandidate(def.Rarity, () => OfferDecision(state, player, def)))
            .ToList();
        Events.EstateEvents(state, player, decisions);
    }
}
