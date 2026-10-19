#pragma warning disable xUnit1051 // Die Tests brauchen keine Abbruchbarkeit
using System.Windows.Input;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Emoji;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Core.Undo;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Tests;

internal static class EmojiTestSupport
{
    public const string Grin = "\U0001F600";
    public const string Thumbs = "\U0001F44D";
    public const string ThumbsMedium = "\U0001F44D\U0001F3FD";

    public static void RequireFont() => Assert.SkipUnless(EmojiRasterizer.IsAvailable, "Die Schrift „Segoe UI Emoji“ ist nicht installiert.");

    public static EmojiAnnotation AddEmoji(this EditorHarness harness, RectD rect, string sequence = Grin, double rotation = 0)
    {
        var emoji = new EmojiAnnotation { Sequence = sequence, Rect = rect, Rotation = rotation };
        harness.Document.Undo.Execute(new AddAnnotationAction(harness.Document.Annotations, emoji));
        return emoji;
    }
}

public sealed class EmojiToolTests
{
    private static EditorHarness Harness(out EmojiTool tool)
    {
        var h = new EditorHarness();
        tool = h.Tools.Get<EmojiTool>();
        h.ViewModel.Activate(tool);
        return h;
    }

    [Fact]
    public void Werkzeug_IstRegistriertUndHatTasteJ()
    {
        var h = new EditorHarness();

        h.Tools.Find(EmojiTool.ToolId).ShouldBeOfType<EmojiTool>().ShortcutKey.ShouldBe(Key.J);
        h.Tools.FindByKey(Key.J).ShouldBeOfType<EmojiTool>();
    }

    [Fact]
    public void Klick_SetztEmojiMittigInStandardgroesse_AlsEineUndoAktion()
    {
        var h = Harness(out var tool);

        tool.OnPointerDown(EditorHarness.Pointer(100, 80));
        tool.OnPointerUp(EditorHarness.Pointer(100, 80));

        var emoji = h.Document.Annotations.ShouldHaveSingleItem().ShouldBeOfType<EmojiAnnotation>();
        emoji.Rect.ShouldBe(new RectD(52, 32, 96, 96));
        emoji.Sequence.ShouldBe(EmojiToolState.FallbackSequence);
        h.Context.Selection.Items.ShouldBe([emoji]);
        h.Context.Emoji.Recent[0].ShouldBe(emoji.Sequence);
        h.Document.Undo.Undo();
        h.Document.Annotations.ShouldBeEmpty();
    }

    [Fact]
    public void Aufziehen_ErzeugtEinQuadrat_MitUmschaltFrei()
    {
        var h = Harness(out var tool);

        tool.OnPointerDown(EditorHarness.Pointer(20, 20));
        tool.OnPointerMove(EditorHarness.Pointer(80, 50));
        tool.OnPointerUp(EditorHarness.Pointer(80, 50));
        tool.OnPointerDown(EditorHarness.Pointer(100, 20));
        tool.OnPointerMove(EditorHarness.Pointer(160, 50, ModifierKeys.Shift));
        tool.OnPointerUp(EditorHarness.Pointer(160, 50, ModifierKeys.Shift));

        h.Document.Annotations.Count.ShouldBe(2);
        ((EmojiAnnotation)h.Document.Annotations[0]).Rect.ShouldBe(new RectD(20, 20, 60, 60));
        ((EmojiAnnotation)h.Document.Annotations[1]).Rect.ShouldBe(new RectD(100, 20, 60, 30));
    }

    [Fact]
    public void Aufziehen_MitAlt_VonDerMitteAus()
    {
        var h = Harness(out var tool);

        tool.OnPointerDown(EditorHarness.Pointer(100, 100));
        tool.OnPointerMove(EditorHarness.Pointer(130, 120, ModifierKeys.Alt));
        tool.OnPointerUp(EditorHarness.Pointer(130, 120, ModifierKeys.Alt));

        ((EmojiAnnotation)h.Document.Annotations.Single()).Rect.ShouldBe(new RectD(70, 70, 60, 60));
    }

    [Fact]
    public void Esc_BrichtDasAufziehenAb()
    {
        var h = Harness(out var tool);

        tool.OnPointerDown(EditorHarness.Pointer(20, 20));
        tool.OnPointerMove(EditorHarness.Pointer(80, 80));
        tool.OnKeyDown(Key.Escape, ModifierKeys.None).ShouldBeTrue();
        tool.OnPointerUp(EditorHarness.Pointer(80, 80));

        h.Document.Annotations.ShouldBeEmpty();
    }

    [Fact]
    public void Werkzeugwahl_LeertDieAuswahl_UndPanelZeigtDieVorgaben()
    {
        var h = new EditorHarness();
        var emoji = h.AddEmoji(new RectD(10, 10, 50, 50));
        h.Context.Selection.Set(emoji);

        h.ViewModel.Activate(h.Tools.Get<EmojiTool>());

        h.Context.Selection.IsEmpty.ShouldBeTrue();
        h.ViewModel.EmojiProperties.IsApplicable.ShouldBeTrue();
        h.ViewModel.EmojiProperties.ShowDefaults.ShouldBeTrue();
        h.ViewModel.HasPanelContent.ShouldBeTrue();
    }

    [Fact]
    public void Standardgroesse_UndDeckkraft_GeltenFuerNeueEmojis()
    {
        var h = Harness(out var tool);
        h.ViewModel.EmojiProperties.DefaultSize = 40;
        h.ViewModel.EmojiProperties.DefaultOpacityPercent = 50;

        tool.OnPointerDown(EditorHarness.Pointer(100, 80));
        tool.OnPointerUp(EditorHarness.Pointer(100, 80));

        var emoji = (EmojiAnnotation)h.Document.Annotations.Single();
        emoji.Rect.ShouldBe(new RectD(80, 60, 40, 40));
        emoji.Opacity.ShouldBe(0.5);
    }

    [Fact]
    public void LetzteKonfiguration_UeberstehtEinenNeustart()
    {
        var settings = new FakeSettings();
        var first = new EmojiToolState(settings)
        {
            DefaultSize = 64,
            Opacity = 0.7,
            SkinTone = SkinTone.Dark,
        };
        first.AddRecent(EmojiTestSupport.Thumbs);
        first.ToggleFavorite(EmojiTestSupport.ThumbsMedium);

        var second = new EmojiToolState(settings);

        second.DefaultSize.ShouldBe(64);
        second.Opacity.ShouldBe(0.7);
        second.SkinTone.ShouldBe(SkinTone.Dark);
        second.Recent.ShouldBe([EmojiTestSupport.Thumbs]);
        second.SelectedSequence.ShouldBe(EmojiTestSupport.Thumbs); // ohne Wahl: zuletzt verwendetes
        second.Favorites.ShouldBe([EmojiTestSupport.Thumbs]);      // Favoriten gelten unabhängig vom Hautton
        settings.SaveCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void ZuletztVerwendet_MaximalDreissig_OhneDubletten_NeuesteVorn()
    {
        var state = new EmojiToolState(new FakeSettings());
        foreach (var emoji in EmojiCatalog.Shared.Emojis.Take(40))
        {
            state.AddRecent(emoji.Sequence);
        }

        state.AddRecent(EmojiCatalog.Shared.Emojis[35].Sequence);

        state.Recent.Count.ShouldBe(30);
        state.Recent[0].ShouldBe(EmojiCatalog.Shared.Emojis[35].Sequence);
        state.Recent.Distinct().Count().ShouldBe(30);
    }

    [Fact]
    public void Favoriten_LassenSichUmschalten()
    {
        var state = new EmojiToolState(new FakeSettings());

        state.ToggleFavorite(EmojiTestSupport.Grin);
        state.IsFavorite(EmojiTestSupport.Grin).ShouldBeTrue();
        state.ToggleFavorite(EmojiTestSupport.Grin);

        state.IsFavorite(EmojiTestSupport.Grin).ShouldBeFalse();
    }
}

public sealed class EmojiSelectToolTests
{
    private static (EditorHarness Harness, SelectTool Select, EmojiAnnotation Emoji) Setup(RectD rect, double rotation = 0)
    {
        var h = new EditorHarness();
        var emoji = h.AddEmoji(rect, rotation: rotation);
        var select = h.Tools.Get<SelectTool>();
        h.ViewModel.Activate(select);
        h.Context.Selection.Set(emoji);
        return (h, select, emoji);
    }

    private static void Drag(SelectTool tool, PointD from, PointD to, ModifierKeys modifiers = ModifierKeys.None)
    {
        tool.OnPointerDown(EditorHarness.Pointer(from.X, from.Y, modifiers));
        tool.OnPointerMove(EditorHarness.Pointer(to.X, to.Y, modifiers));
        tool.OnPointerUp(EditorHarness.Pointer(to.X, to.Y, modifiers));
    }

    [Fact]
    public void Griffe_SindMitgedrehtUndEnthaltenDenDrehgriff()
    {
        var emoji = new EmojiAnnotation { Rect = new RectD(100, 100, 100, 40), Rotation = 90 };

        var handles = SelectionHandles.Get([emoji], rotateOffset: 26);

        handles.Count.ShouldBe(9);
        var rotate = handles.Single(h => h.Kind == HandleKind.Rotate);
        rotate.Position.X.ShouldBe(150 + 20 + 26, 1e-9); // oben des gedrehten Rahmens zeigt nach rechts
        rotate.Position.Y.ShouldBe(120, 1e-9);
        SelectionHandles.Get([emoji]).ShouldNotContain(h => h.Kind == HandleKind.Rotate);
    }

    [Fact]
    public void Eckgriff_SkaliertProportional_MitUmschaltFrei()
    {
        var (h, select, emoji) = Setup(new RectD(50, 50, 100, 50));

        Drag(select, new PointD(150, 100), new PointD(250, 100));
        emoji.Rect.ShouldBe(new RectD(50, 50, 200, 100));
        h.Document.Undo.Undo();
        emoji.Rect.ShouldBe(new RectD(50, 50, 100, 50));

        Drag(select, new PointD(150, 100), new PointD(250, 100), ModifierKeys.Shift);
        emoji.Rect.ShouldBe(new RectD(50, 50, 200, 50));
    }

    [Fact]
    public void Kantengriff_SkaliertFrei()
    {
        var (_, select, emoji) = Setup(new RectD(50, 50, 100, 50));

        Drag(select, new PointD(150, 75), new PointD(200, 75));

        emoji.Rect.ShouldBe(new RectD(50, 50, 150, 50));
    }

    [Fact]
    public void GedrehtesEmoji_SkaliertImEigenenKoordinatensystem_GegenueberliegenderRandBleibtStehen()
    {
        var (_, select, emoji) = Setup(new RectD(100, 100, 100, 40), rotation: 90);
        var fixedCorner = RotationMath.Rotate(new PointD(100, 100), emoji.Center, 90);

        // Gedrehter Eckgriff unten rechts ziehen: lokal (200,140) → (300,140).
        var handle = RotationMath.Rotate(new PointD(200, 140), emoji.Center, 90);
        var target = RotationMath.Rotate(new PointD(300, 140), emoji.Center, 90);
        Drag(select, handle, target);

        emoji.Rect.Width.ShouldBe(200, 1e-6);
        emoji.Rect.Height.ShouldBe(80, 1e-6);
        emoji.Rotation.ShouldBe(90);
        var newFixed = RotationMath.Rotate(emoji.Rect.TopLeft, emoji.Center, 90);
        newFixed.X.ShouldBe(fixedCorner.X, 1e-6);
        newFixed.Y.ShouldBe(fixedCorner.Y, 1e-6);
    }

    [Fact]
    public void Drehgriff_DrehtUmDieMitte_AlsEineUndoAktion()
    {
        var (h, select, emoji) = Setup(new RectD(50, 50, 100, 100));
        h.Context.Scale = 1;

        Drag(select, new PointD(100, 50 - 26), new PointD(200, 100));

        emoji.Rotation.ShouldBe(90, 1e-6);
        h.Document.Undo.Undo();
        emoji.Rotation.ShouldBe(0, 1e-6);
        h.Document.Undo.CanUndo.ShouldBeTrue(); // nur die Drehung wurde zurückgenommen, das Einfügen bleibt
    }

    [Fact]
    public void Drehgriff_MitUmschalt_RastetIn15GradSchrittenEin()
    {
        var (_, select, emoji) = Setup(new RectD(50, 50, 100, 100));

        Drag(select, new PointD(100, 50 - 26), new PointD(190, 120), ModifierKeys.Shift);

        emoji.Rotation.ShouldBe(105, 1e-6);
    }

    [Fact]
    public void Esc_WaehrendDesDrehens_StelltDenAusgangszustandWiederHer()
    {
        var (_, select, emoji) = Setup(new RectD(50, 50, 100, 100));

        select.OnPointerDown(EditorHarness.Pointer(100, 24));
        select.OnPointerMove(EditorHarness.Pointer(200, 100));
        select.OnKeyDown(Key.Escape, ModifierKeys.None);
        select.OnPointerUp(EditorHarness.Pointer(200, 100));

        emoji.Rotation.ShouldBe(0);
    }

    [Fact]
    public void GedrehtesObjekt_IstNurInnerhalbDerGedrehtenFlaecheAuswaehlbar()
    {
        var h = new EditorHarness();
        var emoji = h.AddEmoji(new RectD(100, 100, 100, 40), rotation: 90);

        h.Context.HitTest(new PointD(150, 75)).ShouldBe(emoji);
        h.Context.HitTest(new PointD(105, 120)).ShouldBeNull();
    }

    [Fact]
    public void Tastatur_DrehenUndSpiegeln()
    {
        var (h, select, emoji) = Setup(new RectD(50, 50, 100, 100));

        select.OnKeyDown(Key.OemCloseBrackets, ModifierKeys.None).ShouldBeTrue();
        emoji.Rotation.ShouldBe(15);
        select.OnKeyDown(Key.OemOpenBrackets, ModifierKeys.None).ShouldBeTrue();
        select.OnKeyDown(Key.OemOpenBrackets, ModifierKeys.None).ShouldBeTrue();
        emoji.Rotation.ShouldBe(345);
        select.OnKeyDown(Key.H, ModifierKeys.Shift).ShouldBeTrue();
        select.OnKeyDown(Key.V, ModifierKeys.Shift).ShouldBeTrue();
        emoji.FlipHorizontal.ShouldBeTrue();
        emoji.FlipVertical.ShouldBeTrue();
        h.Document.Undo.Undo();
        emoji.FlipVertical.ShouldBeFalse();
    }

    [Fact]
    public void Mehrfachauswahl_VerschiebenBehaeltDieDrehung()
    {
        var h = new EditorHarness();
        var a = h.AddEmoji(new RectD(10, 10, 40, 40), rotation: 30);
        var b = h.AddEmoji(new RectD(100, 10, 40, 40), rotation: 60);
        var select = h.Tools.Get<SelectTool>();
        h.ViewModel.Activate(select);
        h.Context.Selection.Set([a, b]);

        SelectionHandles.Get(h.Context.Selection.Items, 26).ShouldNotContain(handle => handle.Kind == HandleKind.Rotate);
        select.OnKeyDown(Key.Right, ModifierKeys.Shift);

        a.Rect.X.ShouldBe(20);
        a.Rotation.ShouldBe(30);
        b.Rotation.ShouldBe(60);
    }

    [Fact]
    public void Gruppenskalierung_SkaliertPositionUndGroesse_DrehungBleibt()
    {
        var h = new EditorHarness();
        var a = h.AddEmoji(new RectD(10, 10, 40, 40), rotation: 30);
        var b = h.AddEmoji(new RectD(100, 10, 40, 40), rotation: 60);
        var select = h.Tools.Get<SelectTool>();
        h.ViewModel.Activate(select);
        h.Context.Selection.Set([a, b]);

        select.ResizeByKey(Key.Right, 10);

        a.Rotation.ShouldBe(30);
        b.Rotation.ShouldBe(60);
        (a.Rect.Width > 40).ShouldBeTrue();
    }
}

public sealed class EmojiPropertiesTests
{
    [Fact]
    public void Drehung_GemischteWerteSindNull_SetzenGiltFuerAlle_GesperrteBleiben()
    {
        var h = new EditorHarness();
        var a = h.AddEmoji(new RectD(10, 10, 40, 40), rotation: 10);
        var b = h.AddEmoji(new RectD(100, 10, 40, 40), rotation: 20);
        var locked = h.AddEmoji(new RectD(200, 10, 40, 40), rotation: 5);
        locked.IsLocked = true;
        h.Context.Selection.Set([a, b, locked]);
        var properties = h.ViewModel.EmojiProperties;
        properties.Rotation.ShouldBeNull();

        properties.Rotation = 45;

        a.Rotation.ShouldBe(45);
        b.Rotation.ShouldBe(45);
        locked.Rotation.ShouldBe(5);
        h.Document.Undo.Undo();
        a.Rotation.ShouldBe(10);
        b.Rotation.ShouldBe(20);
    }

    [Fact]
    public void Groesse_AendertBreiteUndHoehe_MitteBleibt()
    {
        var h = new EditorHarness();
        var emoji = h.AddEmoji(new RectD(100, 100, 40, 40));
        h.Context.Selection.Set(emoji);

        h.ViewModel.EmojiProperties.Width = 80;

        emoji.Rect.ShouldBe(new RectD(80, 100, 80, 40));
        h.ViewModel.EmojiProperties.Width.ShouldBe(80);
        h.ViewModel.EmojiProperties.Height.ShouldBe(40);
    }

    [Fact]
    public void SeitenverhaeltnisZuruecksetzen_MachtDasEmojiQuadratisch()
    {
        var h = new EditorHarness();
        var emoji = h.AddEmoji(new RectD(100, 100, 80, 40));
        h.Context.Selection.Set(emoji);

        h.ViewModel.EmojiProperties.ResetAspectCommand.Execute(null);

        emoji.Rect.ShouldBe(new RectD(100, 80, 80, 80));
        h.Document.Undo.Undo();
        emoji.Rect.ShouldBe(new RectD(100, 100, 80, 40));
    }

    [Fact]
    public void DrehungZuruecksetzen_UndSpiegeln()
    {
        var h = new EditorHarness();
        var emoji = h.AddEmoji(new RectD(100, 100, 80, 40), rotation: 77);
        h.Context.Selection.Set(emoji);
        var properties = h.ViewModel.EmojiProperties;

        properties.ResetRotationCommand.Execute(null);
        properties.FlipHorizontal = true;
        properties.FlipVertical = true;

        emoji.Rotation.ShouldBe(0);
        emoji.FlipHorizontal.ShouldBeTrue();
        emoji.FlipVertical.ShouldBeTrue();
        properties.FlipHorizontal.ShouldBeTrue();
    }

    [Fact]
    public void Platzhalter_NichtDarstellbaresEmoji_ZeigtHinweisUndNamen()
    {
        var h = new EditorHarness();
        var emoji = h.AddEmoji(new RectD(10, 10, 40, 40), sequence: "");
        h.Context.Selection.Set(emoji);

        h.ViewModel.EmojiProperties.HasPlaceholder.ShouldBeTrue();
        h.ViewModel.Layers.Items.Single().DisplayName.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Ebenenliste_ZeigtLokalisiertenEmojiNamen()
    {
        var h = new EditorHarness();
        var emoji = h.AddEmoji(new RectD(10, 10, 40, 40));

        AnnotationNames.DisplayName(emoji).ShouldBe(EmojiCatalog.Shared.Find(EmojiTestSupport.Grin)!.GetName());
    }
}

public sealed class EmojiPickerTests
{
    private static async Task<(EditorHarness Harness, ViewModels.EmojiPickerViewModel Picker)> OpenAsync()
    {
        EmojiTestSupport.RequireFont();
        var h = new EditorHarness();
        h.ViewModel.Activate(h.Tools.Get<EmojiTool>());
        await h.ViewModel.EmojiPicker.LoadAsync();
        return (h, h.ViewModel.EmojiPicker);
    }

    [Fact]
    public async Task Kategorien_LeereFavoritenUndZuletztSindAusgeblendet()
    {
        var (h, picker) = await OpenAsync();

        picker.Categories.Any(c => c.Key is ViewModels.EmojiPickerViewModel.FavoritesKey or ViewModels.EmojiPickerViewModel.RecentKey).ShouldBeFalse();
        picker.Categories.Count.ShouldBe(9);

        h.Context.Emoji.AddRecent(EmojiTestSupport.Grin);
        h.Context.Emoji.ToggleFavorite(EmojiTestSupport.Grin);

        picker.Categories[0].Key.ShouldBe(ViewModels.EmojiPickerViewModel.FavoritesKey);
        picker.Categories[1].Key.ShouldBe(ViewModels.EmojiPickerViewModel.RecentKey);
        picker.Categories.Count.ShouldBe(11);
    }

    [Fact]
    public async Task Raster_ZeigtNurDarstellbareEmojis_JeKategorie()
    {
        var (_, picker) = await OpenAsync();

        picker.Cells.ShouldNotBeEmpty();
        picker.Cells.ShouldAllBe(c => EmojiRasterizer.IsSupported(c.Sequence));
        picker.Cells.ShouldAllBe(c => c.Info.GroupIndex == picker.SelectedCategory!.GroupIndex);
    }

    [Fact]
    public async Task Suche_IstEntprelltUndFindetDeutschUndEnglisch()
    {
        var (_, picker) = await OpenAsync();
        var before = picker.Cells;

        picker.SearchText = "lachen";
        picker.Cells.ShouldBeSameAs(before); // erst nach der Entprellung
        picker.ApplyFilterNow();

        picker.Cells.Select(c => c.Sequence).ShouldContain(EmojiTestSupport.Grin);
        picker.SearchText = "thumbs up";
        picker.ApplyFilterNow();
        picker.Cells.Select(c => c.Info.Sequence).ShouldContain(EmojiTestSupport.Thumbs);
    }

    [Fact]
    public async Task Auswahl_OhneObjekt_MerktDasEmojiFuerDasEinfuegen()
    {
        var (h, picker) = await OpenAsync();
        picker.SearchText = "daumen hoch";
        picker.ApplyFilterNow();

        picker.ChooseCommand.Execute(picker.Cells[0]);

        h.Context.Emoji.SelectedSequence.ShouldBe(EmojiTestSupport.Thumbs);
        h.Document.Annotations.ShouldBeEmpty();
    }

    [Fact]
    public async Task Auswahl_MitObjekt_TauschtDasEmojiAusEineUndoAktion()
    {
        var (h, picker) = await OpenAsync();
        var emoji = h.AddEmoji(new RectD(10, 10, 50, 50));
        h.Context.Selection.Set(emoji);
        picker.SearchText = "daumen hoch";
        picker.ApplyFilterNow();

        picker.ChooseCommand.Execute(picker.Cells[0]);

        emoji.Sequence.ShouldBe(EmojiTestSupport.Thumbs);
        h.Context.Emoji.Recent[0].ShouldBe(EmojiTestSupport.Thumbs);
        h.Document.Undo.Undo();
        emoji.Sequence.ShouldBe(EmojiTestSupport.Grin);
    }

    [Fact]
    public async Task Enter_FuegtInDieAnsichtsmitteEin()
    {
        var (h, picker) = await OpenAsync();
        h.Context.VisibleCenterProvider = () => new PointD(60, 70);
        picker.SearchText = "daumen hoch";
        picker.ApplyFilterNow();
        picker.SelectedCell = picker.Cells[0];

        picker.InsertCommand.Execute(null);

        var emoji = h.Document.Annotations.ShouldHaveSingleItem().ShouldBeOfType<EmojiAnnotation>();
        emoji.Rect.ShouldBe(new RectD(12, 22, 96, 96));
        emoji.Sequence.ShouldBe(EmojiTestSupport.Thumbs);
        h.Context.Selection.Items.ShouldBe([emoji]);
    }

    [Fact]
    public async Task Hautton_AendertAuswahlUndRaster()
    {
        var (h, picker) = await OpenAsync();
        var emoji = h.AddEmoji(new RectD(10, 10, 50, 50), EmojiTestSupport.Thumbs);
        h.Context.Selection.Set(emoji);

        picker.SelectedSkinTone = SkinTone.Medium;

        emoji.Sequence.ShouldBe(EmojiTestSupport.ThumbsMedium);
        h.Context.Emoji.SkinTone.ShouldBe(SkinTone.Medium);
        h.Document.Undo.Undo();
        emoji.Sequence.ShouldBe(EmojiTestSupport.Thumbs);
    }

    [Fact]
    public async Task Favoriten_RechtsklickMenueSchaltetUm()
    {
        var (h, picker) = await OpenAsync();
        var cell = picker.Cells[0];

        picker.ToggleFavoriteCommand.Execute(cell);

        cell.IsFavorite.ShouldBeTrue();
        h.Context.Emoji.Favorites.ShouldBe([cell.Info.Sequence]);
        picker.ToggleFavoriteCommand.Execute(cell);
        cell.IsFavorite.ShouldBeFalse();
    }

    [Fact]
    public async Task Zellen_HabenDenEmojiNamenAlsAutomationName()
    {
        var (_, picker) = await OpenAsync();

        picker.Cells.ShouldAllBe(c => c.Name.Length > 0);
        picker.Cells[0].Name.ShouldBe(picker.Cells[0].Info.GetName());
    }
}

public sealed class EmojiRenderingTests
{
    [StaFact]
    public void Rasterizer_RendertFarbigeEmojis()
    {
        EmojiTestSupport.RequireFont();

        var buffer = EmojiRasterizer.Render(EmojiTestSupport.Grin, 96).ShouldNotBeNull();

        buffer.Width.ShouldBe(96);
        buffer.Height.ShouldBe(96);
        var colors = new HashSet<int>();
        var colorful = false;
        for (var i = 0; i < buffer.Pixels.Length; i += 4)
        {
            if (buffer.Pixels[i + 3] > 200)
            {
                colors.Add((buffer.Pixels[i] >> 5 << 6) | (buffer.Pixels[i + 1] >> 5 << 3) | (buffer.Pixels[i + 2] >> 5));
                colorful |= Math.Abs(buffer.Pixels[i + 2] - buffer.Pixels[i]) > 60; // Rot- gegen Blauanteil: nicht nur Graustufen
            }
        }

        colors.Count.ShouldBeGreaterThan(4);
        colorful.ShouldBeTrue();
    }

    [StaFact]
    public void Rasterizer_ZentriertDasEmojiImQuadrat()
    {
        EmojiTestSupport.RequireFont();

        var buffer = EmojiRasterizer.Render(EmojiTestSupport.Grin, 128).ShouldNotBeNull();
        int minX = 999, maxX = -1, minY = 999, maxY = -1;
        for (var y = 0; y < 128; y++)
        {
            for (var x = 0; x < 128; x++)
            {
                if (buffer.Pixels[(y * buffer.Stride) + (x * 4) + 3] > 12)
                {
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
            }
        }

        ((minX + maxX) / 2.0).ShouldBe(64, 3);
        ((minY + maxY) / 2.0).ShouldBe(64, 3);
        (maxX - minX).ShouldBeGreaterThan(100); // füllt das Quadrat
    }

    [StaFact]
    public void Unterstuetzung_FlaggenUndUnbekanntes_SindNichtDarstellbar()
    {
        EmojiTestSupport.RequireFont();

        EmojiRasterizer.IsSupported("\U0001F1E9\U0001F1EA").ShouldBeFalse();
        EmojiRasterizer.IsSupported("").ShouldBeFalse();
        EmojiRasterizer.IsSupported(string.Empty).ShouldBeFalse();
        EmojiRasterizer.IsSupported(EmojiTestSupport.Grin).ShouldBeTrue();
        EmojiRasterizer.IsSupported("\U0001F468‍\U0001F469‍\U0001F467").ShouldBeTrue();
        EmojiRasterizer.Render("", 64).ShouldBeNull();
    }

    private static PixelBuffer White(int width, int height) => PixelTools.Solid(width, height, 255, 255, 255);

    private static (int MinX, int MinY, int MaxX, int MaxY) NonWhiteBounds(PixelBuffer image)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var i = (y * image.Stride) + (x * 4);
                if (image.Pixels[i] < 235 || image.Pixels[i + 1] < 235 || image.Pixels[i + 2] < 235)
                {
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
            }
        }

        return (minX, minY, maxX, maxY);
    }

    [StaFact]
    public void Export_ZeichnetDasEmojiAnDerPosition()
    {
        EmojiTestSupport.RequireFont();
        var h = new EditorHarness(new ImageDocument(White(300, 200)));
        h.AddEmoji(new RectD(100, 50, 100, 100));

        var bounds = NonWhiteBounds(h.Exporter.Render(h.Document));

        bounds.MinX.ShouldBeInRange(95, 115);
        bounds.MaxX.ShouldBeInRange(185, 205);
        bounds.MinY.ShouldBeInRange(45, 65);
        bounds.MaxY.ShouldBeInRange(135, 155);
    }

    [StaFact]
    public void Export_GedrehtesEmoji_LiegtLageversetztWieErwartet()
    {
        EmojiTestSupport.RequireFont();
        var h = new EditorHarness(new ImageDocument(White(300, 200)));
        h.AddEmoji(new RectD(100, 90, 100, 20), rotation: 90); // gedreht: 20 breit, 100 hoch um (150,100)

        var bounds = NonWhiteBounds(h.Exporter.Render(h.Document));

        (bounds.MaxX - bounds.MinX).ShouldBeLessThan(30);
        (bounds.MaxY - bounds.MinY).ShouldBeGreaterThan(70);
        ((bounds.MinX + bounds.MaxX) / 2.0).ShouldBe(150, 6);
        ((bounds.MinY + bounds.MaxY) / 2.0).ShouldBe(100, 6);
    }

    [StaFact]
    public void Export_Spiegelung_SpiegeltDasBild()
    {
        EmojiTestSupport.RequireFont();
        var normal = new EditorHarness(new ImageDocument(White(200, 200)));
        normal.AddEmoji(new RectD(50, 50, 100, 100), EmojiTestSupport.Thumbs);
        var flipped = new EditorHarness(new ImageDocument(White(200, 200)));
        flipped.AddEmoji(new RectD(50, 50, 100, 100), EmojiTestSupport.Thumbs).FlipHorizontal = true;

        var a = normal.Exporter.Render(normal.Document);
        var b = flipped.Exporter.Render(flipped.Document);

        long difference = 0;
        for (var y = 50; y < 150; y++)
        {
            for (var x = 50; x < 150; x++)
            {
                var mirror = 199 - x;
                difference += Math.Abs(a.Pixels[(y * a.Stride) + (x * 4) + 1] - b.Pixels[(y * b.Stride) + (mirror * 4) + 1]);
            }
        }

        (difference / 10000.0).ShouldBeLessThan(6); // mittlere Abweichung je Pixel
    }

    [StaFact]
    public void Export_Deckkraft_MachtDasEmojiDurchscheinend()
    {
        EmojiTestSupport.RequireFont();
        var opaque = new EditorHarness(new ImageDocument(White(200, 200)));
        opaque.AddEmoji(new RectD(50, 50, 100, 100));
        var faded = new EditorHarness(new ImageDocument(White(200, 200)));
        faded.AddEmoji(new RectD(50, 50, 100, 100)).Opacity = 0.3;

        var a = opaque.Exporter.Render(opaque.Document);
        var b = faded.Exporter.Render(faded.Document);

        // Mittlerer Blauanteil der Emoji-Fläche: bei Deckkraft 0,3 deutlich näher an Weiß.
        MeanBlue(b).ShouldBeGreaterThan(MeanBlue(a) + 30);
    }

    private static double MeanBlue(PixelBuffer image)
    {
        long sum = 0;
        for (var y = 50; y < 150; y++)
        {
            for (var x = 50; x < 150; x++)
            {
                sum += image.Pixels[(y * image.Stride) + (x * 4)];
            }
        }

        return sum / 10000.0;
    }

    [StaFact]
    public void Platzhalter_FuerNichtDarstellbareSequenz_WirdGezeichnet()
    {
        EmojiTestSupport.RequireFont();
        var h = new EditorHarness(new ImageDocument(White(200, 200)));
        h.AddEmoji(new RectD(50, 50, 100, 100), "");

        var bounds = NonWhiteBounds(h.Exporter.Render(h.Document));

        bounds.MaxX.ShouldBeGreaterThan(bounds.MinX + 50); // gestrichelter Rahmen und „?“
    }

    [StaFact]
    public void Cache_HaeltDieSpeichergrenzeEin_UndRastertJedeGroesseEinmal()
    {
        EmojiTestSupport.RequireFont();
        var cache = new EmojiCache(150_000); // ~ 6 Bilder à 64 × 64

        foreach (var size in new[] { 64, 80, 100, 125, 156, 195, 244 })
        {
            cache.Get(EmojiTestSupport.Grin, size).ShouldNotBeNull();
        }

        cache.Bytes.ShouldBeLessThanOrEqualTo(150_000 + (244L * 244 * 4)); // die zuletzt geladene darf überstehen, die Liste schrumpft
        cache.Count.ShouldBeLessThan(7);
        var misses = cache.Misses;
        cache.Get(EmojiTestSupport.Grin, 244);
        cache.Misses.ShouldBe(misses);
        cache.Get("", 64).ShouldBeNull();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(37.5)]
    [InlineData(500)]
    [InlineData(3000)]
    public void Quantize_RundetAufStufenAufUndBegrenzt(double wanted)
    {
        var size = EmojiCache.Quantize(wanted);

        size.ShouldBeGreaterThanOrEqualTo(Math.Min((int)Math.Ceiling(wanted), EmojiRasterizer.MaxPixelSize));
        size.ShouldBeLessThanOrEqualTo((int)(wanted * 1.26) + 5);
        EmojiCache.Quantize(100000).ShouldBe(EmojiRasterizer.MaxPixelSize);
        EmojiCache.Quantize(100000, 2048).ShouldBe(2048);
        EmojiCache.Quantize(wanted).ShouldBe(EmojiCache.Quantize(size)); // stabil
    }
}
