using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Emoji;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Settings;
using NSubstitute;

namespace GodSaveTheScreen.App.Tests;

public sealed class ToolDefaultsResetTests
{
    private static EditorHarness Harness(bool confirm = true)
    {
        var h = new EditorHarness();
        h.Dialogs.Confirm(default!, default!).ReturnsForAnyArgs(confirm);
        return h;
    }

    [StaFact]
    public void Formwerkzeug_EntferntGespeicherteWerte_NurFuerDiesesWerkzeug()
    {
        var h = Harness();
        var styled = new ShapeStyle { Stroke = new StrokeStyle(ArgbColor.FromRgb(1, 2, 3), 17) };
        h.Defaults.SetShape(RectangleTool.ToolId, styled);
        h.Defaults.SetShape(EllipseTool.ToolId, styled);
        var tool = h.Tools.Get<RectangleTool>();
        h.ViewModel.Activate(tool);

        h.ViewModel.ResetToolDefaultsCommand.Execute(null);

        h.Defaults.GetShape(RectangleTool.ToolId, tool.BuiltInStyle).ShouldBe(tool.BuiltInStyle);
        h.Defaults.GetShape(EllipseTool.ToolId, new ShapeStyle()).ShouldBe(styled);
        h.ViewModel.Properties.ThicknessValue.ShouldBe(tool.BuiltInStyle.Stroke.Thickness);
    }

    [StaFact]
    public void Abgelehnt_AendertNichts()
    {
        var h = Harness(confirm: false);
        h.Defaults.SetText(new TextStyle { FontSize = 99 });
        h.ViewModel.Activate(h.Tools.Get<TextTool>());

        h.ViewModel.ResetToolDefaultsCommand.Execute(null);

        h.Defaults.GetText().FontSize.ShouldBe(99);
    }

    [StaFact]
    public void Stift_SetztFormUndFreihandZurueck()
    {
        var h = Harness();
        var pen = h.Tools.Get<PenTool>();
        h.Defaults.SetShape(PenTool.ToolId, new ShapeStyle { Stroke = new StrokeStyle(ArgbColor.Black, 30) });
        h.Context.Freehand = new FreehandOptions(0.1, false);
        h.ViewModel.Activate(pen);

        h.ViewModel.ResetToolDefaultsCommand.Execute(null);

        h.Defaults.GetShape(PenTool.ToolId, pen.BuiltInStyle).ShouldBe(pen.BuiltInStyle);
        h.Context.Freehand.Smoothing.ShouldBe(new FreehandStyle().Smoothing);
        h.Context.Freehand.UsePressure.ShouldBe(new FreehandStyle().UsePressure);
    }

    [StaFact]
    public void TextMarkerUnschaerfeLupe_ErhaltenEingebauteWerte()
    {
        var h = Harness();
        h.Defaults.SetText(new TextStyle { FontSize = 99 });
        h.Defaults.SetMarker(new MarkerStyle { Size = 80 });
        h.Defaults.MarkerStartNumber = 7;
        h.Context.BlurTemplate.Apply(new BlurStyle { Strength = 40, Mode = BlurMode.GaussianBlur });
        h.Context.SaveBlurTemplate();
        h.Context.MagnifierTemplate.Apply(new MagnifierStyle { Zoom = 6 });
        h.Context.SaveMagnifierTemplate();

        ToolDefaultsReset.Reset(h.Context, h.Tools.Get<TextTool>());
        ToolDefaultsReset.Reset(h.Context, h.Tools.Get<MarkerTool>());
        ToolDefaultsReset.Reset(h.Context, h.Tools.Get<BlurTool>());
        ToolDefaultsReset.Reset(h.Context, h.Tools.Get<MagnifierTool>());

        h.Defaults.GetText().ShouldBe(new TextStyle());
        h.Defaults.GetMarker().ShouldBe(new MarkerStyle());
        h.Defaults.MarkerStartNumber.ShouldBe(1);
        h.Context.BlurTemplate.ToStyle().ShouldBe(new BlurStyle());
        h.Settings.Current.BlurDefaults.ShouldBe(new BlurStyle());
        h.Settings.Current.MagnifierDefaults.ShouldBe(new MagnifierStyle());
    }

    [StaFact]
    public void Maskenwerkzeuge_SetzenNurIhreEigenenWerteZurueck()
    {
        var h = Harness();
        h.Context.MaskTool = new MaskToolStyle { Shape = AreaSelectShape.Lasso, Tolerance = 60, Feather = 5, BrushSize = 200, BrushHardness = 10 };

        ToolDefaultsReset.Reset(h.Context, h.Tools.Get<AreaSelectTool>());

        h.Context.MaskTool.ShouldBe(new MaskToolStyle { BrushSize = 200, BrushHardness = 10 });

        ToolDefaultsReset.Reset(h.Context, h.Tools.Get<BackgroundEraserTool>());

        h.Context.MaskTool.ShouldBe(new MaskToolStyle());
    }

    [StaFact]
    public void EmojiUndStempel_BehaltenHauttonUndSetzenStilZurueck()
    {
        var h = Harness();
        h.Context.Emoji.SkinTone = SkinTone.Dark;
        h.Context.Emoji.Opacity = 0.3;
        h.Context.Emoji.DefaultSize = 300;
        h.Context.Stamp.Style = new StampStyle { Text = "TOP SECRET", Wear = 90, Rotation = 20 };

        ToolDefaultsReset.Reset(h.Context, h.Tools.Get<EmojiTool>());
        ToolDefaultsReset.Reset(h.Context, h.Tools.Get<StampTool>());

        h.Settings.Current.EmojiDefaults.ShouldBe(new EmojiStyle { SkinTone = SkinTone.Dark });
        h.Context.Stamp.Style.ShouldBe(new StampStyle());
    }

    [StaFact]
    public void Schaltflaeche_NurOhneAuswahlUndBeiWerkzeugenMitEinstellungen()
    {
        var h = Harness();

        h.ViewModel.CanResetToolDefaults.ShouldBeFalse(); // Auswahlwerkzeug

        h.ViewModel.Activate(h.Tools.Get<EraserTool>());
        h.ViewModel.CanResetToolDefaults.ShouldBeTrue();

        h.ViewModel.Activate(h.Tools.Get<RectangleTool>());
        h.ViewModel.CanResetToolDefaults.ShouldBeTrue();

        var rect = h.AddRectangle(10, 10, 20, 20);
        h.Context.Selection.Set(rect);
        h.ViewModel.CanResetToolDefaults.ShouldBeFalse();

        h.ViewModel.Activate(h.Tools.Get<CropTool>());
        h.Context.Selection.Clear();
        h.ViewModel.CanResetToolDefaults.ShouldBeFalse();
    }
}
