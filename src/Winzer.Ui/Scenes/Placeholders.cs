using DrawnUi.Draw;
using Winzer.Engine;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

static class Placeholder
{
    public static SkiaControl Scene(string title) =>
        Kit.Layer(0, 0, Shell.StageWidth, Shell.StageHeight).With(
            Kit.Box(0, 0, Shell.StageWidth, Shell.StageHeight, Theme.C("#2a1f1c")),
            Kit.Text(title, 0, 420, Shell.StageWidth, 60, 44, Theme.Cream, bold: true, serif: true, align: Align.Center),
            Kit.Text(Lang.T("Diese Szene ist noch im Bau.", "This scene is still under construction."), 0, 490, Shell.StageWidth, 40, 24, Theme.CreamSoft, align: Align.Center));
}
