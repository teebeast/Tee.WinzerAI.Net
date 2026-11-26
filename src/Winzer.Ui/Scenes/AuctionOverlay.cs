using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

/// <summary>
/// Live-Versteigerung über dem ganzen Bildschirm: links das Los, in der Mitte Höchstgebot und Gebotsverlauf,
/// rechts Finanzkraft und Gebote des Menschen, der gerade am Zug ist.
/// </summary>
public static class AuctionOverlay
{
    const string AmountKey = "auktion.betrag";

    public static SkiaControl Build(GameSession session, AuctionLot lot)
    {
        var state = session.State;
        var bidder = state.Players.Find(p => p.Id == lot.Awaiting) ?? session.Player;
        var regionLots = state.Auction!.Lots.Where(l => l.RegionId == lot.RegionId).ToList();
        var position = regionLots.IndexOf(lot) + 1;
        var lastClosed = regionLots.Take(position - 1).LastOrDefault(l => l.Status is LotStatus.Sold or LotStatus.Unsold);

        var back = Overlays.Backdrop(opaque: true);
        back.With(Kit.Paint(0, 0, Kit.DesignWidth, Kit.DesignHeight, PaintHall));

        // Kopf: Hammer, Titel, Fortschritt
        back.With(Kit.Paint(112, 40, 104, 104, (c, w, _) => VillagePaint.Gavel(c, w, Theme.SkGold, Theme.SkGoldDark)));
        back.With(Kit.Text($"{Lang.T("Live-Versteigerung", "Live auction")} · {Regions.All[lot.RegionId].Name}", 236, 40, 1000, 60, 46, Theme.Cream, bold: true, serif: true));
        back.With(Kit.Text(Lang.En ? $"Lot {position} of {regionLots.Count}" : $"Los {position} von {regionLots.Count}", 238, 102, 200, 34, 24, Theme.Gold, bold: true));
        back.With(Kit.Paint(410, 104, 400, 30, (c, w, h) => Pips(c, h, regionLots, position - 1)));
        if (session.HotSeat)
        {
            back.With(Kit.Box(1280, 54, 520, 76, radius: 38, gradient: Kit.Vertical(Theme.Gold, Theme.GoldDark), shadow: true)
                .With(Kit.Text(Lang.T("Am Zug", "Your turn"), 0, 8, 520, 22, 16, Theme.InkSoft, align: Align.Center))
                .With(Kit.Text(bidder.Name, 20, 28, 480, 38, 27, Theme.Ink, bold: true, serif: true, align: Align.Center)));
        }

        // Ergebnis des vorigen Loses – die Zeile steht immer, damit nichts springt
        var won = lastClosed?.Leader == bidder.Id;
        var result = lastClosed is null ? Lang.T("Erstes Los – noch kein Zuschlag in dieser Versteigerung", "First lot – no hammer yet in this auction")
            : lastClosed.Status == LotStatus.Sold && !string.IsNullOrEmpty(lastClosed.Leader)
                ? Lang.En
                    ? $"Sold {lastClosed.Name}: {Util.FormatEuro(lastClosed.Price)} to {(won ? "you" : Auction.BidderName(state, lastClosed.Leader, true))}"
                    : $"Zuschlag {lastClosed.Name}: {Util.FormatEuro(lastClosed.Price)} an {(won ? "dich" : Auction.BidderName(state, lastClosed.Leader, true))}"
                : Lang.En ? $"{lastClosed.Name}: no bid, stays unsold" : $"{lastClosed.Name}: kein Gebot, bleibt unverkauft";
        back.With(Kit.Box(120, 164, 1680, 52, won ? Theme.C("#804d7a2a") : Theme.GlassLight, 26, won ? Theme.GoodLight : Theme.GlassLine, 1.5)
            .With(Kit.Text(result, 30, 11, 1620, 30, 20, lastClosed is null ? Theme.CreamSoft : Theme.Cream, bold: won, align: Align.Center)));

        back.With(LotCard(state, lot));
        back.With(PricePanel(state, lot, bidder));
        back.With(Controls(session, lot, bidder));
        return back;
    }

    /// <summary>Saal: Scheinwerfer auf das Los, Vorhänge an den Rändern</summary>
    static void PaintHall(SKCanvas c, float w, float h)
    {
        using (var dark = Draw.Fill("#140a0e")) c.DrawRect(0, 0, w, h, dark);
        using (var spot = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(new SKPoint(w / 2, 140), 1050,
                [SKColor.Parse("#70f6d477"), SKColor.Parse("#30c8962e"), SKColor.Parse("#00000000")], [0, 0.45f, 1], SKShaderTileMode.Clamp),
        })
        {
            c.DrawRect(0, 0, w, h, spot);
        }
        // Vorhänge
        foreach (var left in new[] { true, false })
        {
            var x0 = left ? 0 : w - 84;
            using var cloth = Draw.HorizontalGradient(x0, x0 + 84, (0, left ? "#3d0d1b" : "#7a1f37"), (0.5f, "#8e2440"), (1, left ? "#7a1f37" : "#3d0d1b"));
            c.DrawRect(x0, 0, 84, h, cloth);
            using var fold = Draw.Stroke(SKColors.Black.WithAlpha(70), 5);
            for (var i = 1; i < 4; i++) c.DrawLine(x0 + i * 21, 0, x0 + i * 21, h, fold);
            using var hem = Draw.Fill(Theme.SkGoldDark);
            c.DrawRect(left ? 80 : w - 84, 0, 4, h, hem);
        }
        using var floor = Draw.VerticalGradient(h - 60, h, (0, "#00000000"), (1, "#99000000"));
        c.DrawRect(0, h - 60, w, 60, floor);
    }

    static void Pips(SKCanvas c, float h, List<AuctionLot> lots, int current)
    {
        using var done = Draw.Fill(Theme.SkGoldDark);
        using var now = Draw.Fill(Theme.SkGold);
        using var open = Draw.Stroke(Theme.SkCreamSoft.WithAlpha(150), 2.5f);
        for (var i = 0; i < lots.Count; i++)
        {
            var x = 24 + i * 44f;
            if (i == current) c.DrawRoundRect(x - 18, h / 2 - 8, 36, 16, 8, 8, now);
            else if (i < current) c.DrawCircle(x, h / 2, 8, done);
            else c.DrawCircle(x, h / 2, 7, open);
        }
    }

    static string GrapeLine(GameState state, AuctionLot lot) =>
        lot.Grape is { } grape && lot.PlantedYear is { } planted
            ? Lang.En ? $"{Grapes.All[grape].Name}, {state.Year - planted} years old – bears fruit right away" : $"{Grapes.All[grape].Name}, {state.Year - planted} Jahre alt – trägt sofort"
            : Lang.T("Brache – muss erst bepflanzt werden", "Fallow – must be planted first");

    static SkiaControl LotCard(GameState state, AuctionLot lot)
    {
        var card = Kit.Card(120, 240, 520, 770, 24);
        card.With(Kit.Paint(24, 24, 472, 286, (c, w, h) => VillagePaint.Parcel(c, w, h, lot, state.Month, 18)));
        card.With(Kit.Text(lot.Name, 28, 324, 464, 56, 42, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text($"{Util.FormatNumber(lot.Hectares, 1)} ha · {Auction.SiteLabels[lot.Site]}", 28, 382, 464, 32, 24, Theme.Wine, bold: true));
        card.With(Kit.Text(GrapeLine(state, lot), 28, 420, 464, 56, 20, Theme.InkSoft, maxLines: 2));
        card.With(Kit.Box(28, 486, 464, 2, Theme.PaperLine));
        card.With(Kit.Row(Lang.T("Schätzwert", "Estimate"), Util.FormatEuro(lot.Estimate), 28, 502, 464, size: 23));
        card.With(Kit.Row(Lang.T("Ausrufpreis", "Opening price"), Util.FormatEuro(lot.StartPrice), 28, 544, 464, size: 23));
        card.With(Kit.Row(Lang.T("Gebotsschritt", "Bid increment"), Util.FormatEuro(lot.Increment), 28, 586, 464, size: 23));

        // Wo das Gebot im Verhältnis zum Schätzwert steht (Skala bis 150 %)
        var price = !string.IsNullOrEmpty(lot.Leader) ? lot.Price : lot.StartPrice;
        var ratio = price / lot.Estimate;
        var hot = ratio > 1;
        card.With(Kit.Box(28, 640, 464, 106, hot ? Theme.C("#33b3261e") : Theme.C("#40d9c7a3"), 14)
            .With(Kit.Text(Lang.En ? $"{(string.IsNullOrEmpty(lot.Leader) ? "Opening" : "Top bid")}: {Js.Round(ratio * 100)} % of estimate" : $"{(string.IsNullOrEmpty(lot.Leader) ? "Ausruf" : "Höchstgebot")}: {Js.Round(ratio * 100)} % des Schätzwerts", 18, 12, 428, 28, 19, hot ? Theme.Alert : Theme.Ink, bold: true))
            .With(Kit.Box(18, 52, 428, 12, Theme.PaperLine, 6))
            .With(Kit.Box(18, 52, Math.Max(12, 428 * Math.Clamp(ratio / 1.5, 0, 1)), 12, hot ? Theme.Alert : Theme.Wine, 6))
            .With(Kit.Box(18 + 428 / 1.5 - 1.5, 46, 3, 24, Theme.Ink, 1.5))
            .With(Kit.Text(Lang.T("Marke = Schätzwert", "Mark = estimate"), 18, 74, 428, 22, 15, Theme.InkSoft, align: Align.Center)));
        return card;
    }

    static SkiaControl PricePanel(GameState state, AuctionLot lot, Player bidder)
    {
        var panel = Kit.Box(672, 240, 576, 770, Theme.C("#eb1c1418"), 24, Theme.Gold, 2.5, shadow: true);
        var hasBid = !string.IsNullOrEmpty(lot.Leader);
        panel.With(Kit.Text(hasBid ? Lang.T("Höchstgebot", "Top bid") : Lang.T("Noch kein Gebot", "No bid yet"), 0, 26, 576, 32, 23, Theme.CreamSoft, align: Align.Center));
        panel.With(Kit.Text(Util.FormatEuro(hasBid ? lot.Price : lot.StartPrice), 0, 56, 576, 110, 84, Theme.Gold, bold: true, serif: true, align: Align.Center));
        panel.With(Kit.Text(hasBid ? Auction.BidderName(state, lot.Leader!) : Lang.T("Ausrufpreis", "Opening price"), 20, 168, 536, 40, 28, Theme.Cream, bold: hasBid, align: Align.Center));
        panel.With(Kit.Box(40, 226, 496, 2, Theme.GlassLine));

        panel.With(Kit.Text(Lang.T("Gebotsverlauf", "Bid history"), 40, 242, 300, 32, 22, Theme.Gold, bold: true, serif: true));
        panel.With(Kit.Text($"{lot.Bids.Count} {(Lang.En ? (lot.Bids.Count == 1 ? "bid" : "bids") : (lot.Bids.Count == 1 ? "Gebot" : "Gebote"))}", 336, 246, 200, 28, 18, Theme.CreamSoft, align: Align.End));
        var ticker = lot.Bids.AsEnumerable().Reverse().Take(6).ToList();
        if (ticker.Count == 0)
        {
            panel.With(Kit.Text(Lang.T("Noch keine Gebote – eröffne mit dem Ausrufpreis oder steig aus.", "No bids yet – open at the opening price or drop out."), 60, 420, 456, 64, 22, Theme.CreamSoft, align: Align.Center, maxLines: 2));
        }
        var y = 290.0;
        for (var i = 0; i < ticker.Count; i++)
        {
            var entry = ticker[i];
            var mine = entry.Bidder == bidder.Id;
            var newest = i == 0;
            var row = newest
                ? Kit.Box(28, y, 520, 68, Theme.C("#3df6d477"), 14, Theme.Gold, 1.5)
                : Kit.Box(28, y, 520, 68, Theme.C(i % 2 == 1 ? "#0ff4ead7" : "#1af4ead7"), 14);
            var color = mine ? Theme.Gold : Theme.Cream;
            row.With(Kit.Box(18, 28, 12, 12, mine ? Theme.Gold : entry.Bidder == Auction.Investor ? Theme.C("#9cb8d0") : Theme.CreamSoft, 6));
            row.With(Kit.Text(mine ? Lang.T("Du", "You") : Auction.BidderName(state, entry.Bidder), 44, 18, 290, 32, 22, color, bold: mine || newest));
            row.With(Kit.Text(Util.FormatEuro(entry.Amount), 320, 16, 182, 36, 25, color, bold: true, align: Align.End));
            row.Opacity = Math.Max(0.5, 1 - i * 0.1);
            panel.With(row);
            y += 76;
        }
        return panel;
    }

    /// <summary>Eigener Betrag – gilt nur, solange sich das Höchstgebot nicht ändert</summary>
    static double CustomAmount(GameSession session, AuctionLot lot, double minimum)
    {
        var parts = (session.Nav.Selected(AmountKey) ?? "").Split('|');
        if (parts.Length == 3 && parts[0] == lot.Id && parts[1] == lot.Bids.Count.ToString() &&
            double.TryParse(parts[2], System.Globalization.CultureInfo.InvariantCulture, out var amount))
        {
            return Math.Max(minimum, amount);
        }
        return minimum + lot.Increment;
    }

    static SkiaControl Controls(GameSession session, AuctionLot lot, Player bidder)
    {
        var minimum = Auction.NextMinimum(lot);
        var affordable = Auction.MaxAffordableBid(bidder);
        var card = Kit.Card(1280, 240, 520, 770, 24);
        string? Problem(double value) => session.Problem(new GameAction.AuctionBid(lot.Id, value));
        void Bid(double value) => session.Run(new GameAction.AuctionBid(lot.Id, value));

        card.With(Kit.Text(session.HotSeat ? (Lang.En ? $"Finances: {bidder.Name}" : $"Finanzkraft: {bidder.Name}") : Lang.T("Deine Finanzkraft", "Your finances"), 30, 20, 460, 34, 25, Theme.Wine, bold: true, serif: true));
        card.With(Kit.Row(Lang.T("Konto", "Account"), Util.FormatEuro(bidder.Money), 30, 60, 460, bidder.Money < 0 ? Theme.Alert : null));
        card.With(Kit.Row(Lang.T("Freier Kreditrahmen", "Available credit"), Util.FormatEuro(Market.AvailableCredit(bidder)), 30, 92, 460));
        card.With(Kit.Row(Lang.T("Finanzierbar bis", "Affordable up to"), Util.FormatEuro(affordable), 30, 124, 460, Theme.Wine));
        card.With(Kit.Box(30, 164, 460, 2, Theme.PaperLine));

        // Mindestgebot und Sprunggebote
        card.With(Kit.Button($"{Lang.T("Bieten", "Bid")}: {Util.FormatEuro(minimum)}", 30, 178, 460, 78, () => Bid(minimum), ButtonKind.Primary,
            string.IsNullOrEmpty(lot.Leader) ? Lang.T("Eröffnen zum Ausrufpreis", "Open at the opening price") : Lang.T("Mindestgebot – ein Schritt über dem Höchstgebot", "Minimum bid – one step above the top bid"), Problem(minimum), 26));
        var jump2 = minimum + 2 * lot.Increment;
        var jump5 = minimum + 5 * lot.Increment;
        card.With(Kit.Button($"+2 {Lang.T("Schritte", "steps")}: {Util.FormatEuro(jump2)}", 30, 266, 460, 62, () => Bid(jump2), ButtonKind.Secondary, problem: Problem(jump2)));
        card.With(Kit.Button($"+5 {Lang.T("Schritte", "steps")}: {Util.FormatEuro(jump5)}", 30, 336, 460, 62, () => Bid(jump5), ButtonKind.Secondary, problem: Problem(jump5)));

        // Eigenes Gebot über Schnellwahl
        var amount = CustomAmount(session, lot, minimum);
        void Set(double value) => session.Nav.Select(AmountKey,
            $"{lot.Id}|{lot.Bids.Count}|{Math.Max(minimum, Math.Floor(value)).ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        card.With(Kit.Text(Lang.T("Eigenes Gebot", "Custom bid"), 30, 410, 300, 28, 19, Theme.InkSoft, bold: true));
        card.With(Kit.IconButton("−", 30, 440, 56, () => Set(amount - lot.Increment), dark: false, enabled: amount > minimum, tip: Lang.T("Gebot um einen Schritt senken", "Lower the bid by one step")));
        card.With(Kit.Box(96, 440, 328, 56, Theme.Paper, 14, Theme.PaperLine, 2)
            .With(Kit.Text(Util.FormatEuro(amount), 0, 8, 328, 40, 28, amount > affordable ? Theme.Alert : Theme.Ink, bold: true, serif: true, align: Align.Center)));
        card.With(Kit.IconButton("+", 434, 440, 56, () => Set(amount + lot.Increment), dark: false, tip: Lang.T("Gebot um einen Schritt erhöhen", "Raise the bid by one step")));
        (string Label, double Value, bool Enabled)[] quick =
        [
            (Lang.T("+10 Schritte", "+10 steps"), amount + 10 * lot.Increment, true),
            (Lang.T("Schätzwert", "Estimate"), lot.Estimate, lot.Estimate >= minimum),
            (Lang.T("Maximum", "Maximum"), Math.Floor(affordable), affordable >= minimum),
        ];
        for (var i = 0; i < quick.Length; i++)
        {
            var (label, value, enabled) = quick[i];
            var on = Math.Abs(amount - value) < 0.5;
            var chip = Kit.Box(30 + i * 156, 506, 148, 38, on ? Theme.Wine : Theme.PaperLine, 19)
                .With(Kit.Text(label, 0, 8, 148, 24, 17, on ? Theme.Paper : Theme.Ink, bold: true, align: Align.Center));
            if (enabled) chip.Tap(() => Set(value));
            else chip.Opacity = 0.4;
            card.With(chip);
        }
        card.With(Kit.Button($"{Lang.T("Eigenes Gebot abgeben", "Place custom bid")}: {Util.FormatEuro(amount)}", 30, 554, 460, 62, () => Bid(amount), ButtonKind.Gold, problem: Problem(amount)));

        var pass = new GameAction.AuctionPass(lot.Id);
        card.With(Kit.Button(Lang.T("Aussteigen", "Drop out"), 30, 630, 460, 62, () => session.Run(pass), ButtonKind.Ghost,
            Lang.T("Für dieses Los bietest du nicht mehr mit", "You stop bidding on this lot"), session.Problem(pass)));
        card.With(Kit.Text(Lang.T("Ein Fehlbetrag wird beim Zuschlag automatisch über den Kredit gedeckt.", "Any shortfall is automatically covered by credit when the hammer falls."), 30, 704, 460, 48, 16, Theme.InkSoft, maxLines: 2));
        return card;
    }
}
