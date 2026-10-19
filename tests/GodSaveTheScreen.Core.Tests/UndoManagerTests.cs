using System.Collections.ObjectModel;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Undo;

namespace GodSaveTheScreen.Core.Tests;

public sealed class UndoManagerTests
{
    private static RectangleAnnotation NewRect() => new() { Rect = new RectD(10, 10, 50, 40) };

    [Fact]
    public void Execute_Undo_Redo_StelltZustandWiederHer()
    {
        var list = new ObservableCollection<Annotation>();
        var undo = new UndoManager();
        var rect = NewRect();

        undo.Execute(new AddAnnotationAction(list, rect));
        list.ShouldContain(rect);
        undo.CanUndo.ShouldBeTrue();

        undo.Undo();
        list.ShouldBeEmpty();
        undo.CanRedo.ShouldBeTrue();

        undo.Redo();
        list.ShouldContain(rect);
    }

    [Fact]
    public void Transaktion_FasstAktionenZuEinemSchrittZusammen()
    {
        var list = new ObservableCollection<Annotation>();
        var undo = new UndoManager();

        using (undo.BeginTransaction("Mehrere"))
        {
            undo.Execute(new AddAnnotationAction(list, NewRect()));
            undo.Execute(new AddAnnotationAction(list, NewRect()));
            undo.CanUndo.ShouldBeFalse("während der Transaktion kein Undo");
        }

        undo.UndoCount.ShouldBe(1);
        undo.Undo();
        list.ShouldBeEmpty();
    }

    [Fact]
    public void PropertyChange_AufeinanderfolgendeAenderungenWerdenZusammengefasst()
    {
        var rect = NewRect();
        var undo = new UndoManager();
        void Set(double v) => rect.Opacity = v;

        undo.Execute(new PropertyChangeAction<double>(rect, "Opacity", 1, 0.8, Set));
        undo.Execute(new PropertyChangeAction<double>(rect, "Opacity", 0.8, 0.5, Set));
        undo.Execute(new PropertyChangeAction<double>(rect, "Opacity", 0.5, 0.3, Set));

        undo.UndoCount.ShouldBe(1);
        rect.Opacity.ShouldBe(0.3);
        undo.Undo();
        rect.Opacity.ShouldBe(1);
    }

    [Fact]
    public void PropertyChange_UnterschiedlicheEigenschaftenBleibenGetrennt()
    {
        var rect = NewRect();
        var undo = new UndoManager();
        undo.Execute(new PropertyChangeAction<double>(rect, "Opacity", 1, 0.5, v => rect.Opacity = v));
        undo.Execute(new PropertyChangeAction<bool>(rect, "IsLocked", false, true, v => rect.IsLocked = v));
        undo.UndoCount.ShouldBe(2);
    }

    [Fact]
    public void Limit_VerwirftAeltesteEintraege()
    {
        var list = new ObservableCollection<Annotation>();
        var undo = new UndoManager(limit: 3);
        for (var i = 0; i < 5; i++)
        {
            undo.Execute(new AddAnnotationAction(list, NewRect()));
        }

        undo.UndoCount.ShouldBe(3);
    }

    [Fact]
    public void NeueAktionNachUndo_LoeschtRedo()
    {
        var list = new ObservableCollection<Annotation>();
        var undo = new UndoManager();
        undo.Execute(new AddAnnotationAction(list, NewRect()));
        undo.Undo();
        undo.Execute(new AddAnnotationAction(list, NewRect()));
        undo.CanRedo.ShouldBeFalse();
    }

    [Fact]
    public void SavePoint_DirtyLogik()
    {
        var list = new ObservableCollection<Annotation>();
        var undo = new UndoManager();
        undo.IsAtSavePoint.ShouldBeTrue();

        undo.Execute(new AddAnnotationAction(list, NewRect()));
        undo.IsAtSavePoint.ShouldBeFalse();

        undo.MarkSaved();
        undo.IsAtSavePoint.ShouldBeTrue();

        undo.Undo();
        undo.IsAtSavePoint.ShouldBeFalse();
        undo.Redo();
        undo.IsAtSavePoint.ShouldBeTrue();
    }

    [Fact]
    public void SavePoint_GemergteAenderungNachSpeichernIstDirty()
    {
        var rect = NewRect();
        var undo = new UndoManager();
        undo.Execute(new PropertyChangeAction<double>(rect, "Opacity", 1, 0.5, v => rect.Opacity = v));
        undo.MarkSaved();
        undo.Execute(new PropertyChangeAction<double>(rect, "Opacity", 0.5, 0.2, v => rect.Opacity = v));
        undo.IsAtSavePoint.ShouldBeFalse();
    }

    [Fact]
    public void ImageDocument_IsDirtyFolgtDemSavePoint()
    {
        var document = new ImageDocument(new PixelBuffer(10, 10));
        document.IsDirty.ShouldBeFalse();

        document.Undo.Execute(new AddAnnotationAction(document.Annotations, NewRect()));
        document.IsDirty.ShouldBeTrue();

        document.MarkSaved();
        document.IsDirty.ShouldBeFalse();

        document.Undo.Undo();
        document.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void RemoveAnnotations_UndoStelltUrspruenglicheReihenfolgeHer()
    {
        var a = NewRect();
        var b = NewRect();
        var c = NewRect();
        var list = new ObservableCollection<Annotation> { a, b, c };
        var undo = new UndoManager();

        undo.Execute(new RemoveAnnotationsAction(list, [a, c]));
        list.ShouldBe([b]);
        undo.Undo();
        list.ShouldBe([a, b, c]);
    }

    [Fact]
    public void Reorder_UndoStelltReihenfolgeWiederHer()
    {
        var a = NewRect();
        var b = NewRect();
        var c = NewRect();
        var list = new ObservableCollection<Annotation> { a, b, c };
        var undo = new UndoManager();

        undo.Execute(new ReorderAction(list, [c, a, b]));
        list.ShouldBe([c, a, b]);
        undo.Undo();
        list.ShouldBe([a, b, c]);
    }

    [Fact]
    public void ReplaceGeometry_UndoRedo()
    {
        var rect = NewRect();
        var before = rect.Clone();
        rect.Rect = new RectD(0, 0, 100, 100);
        var after = rect.Clone();
        var undo = new UndoManager();

        undo.Execute(new ReplaceGeometryAction([(rect, before, after)]));
        undo.Undo();
        ((RectangleAnnotation)rect).Rect.ShouldBe(new RectD(10, 10, 50, 40));
        undo.Redo();
        rect.Rect.ShouldBe(new RectD(0, 0, 100, 100));
    }

    [Fact]
    public void DreissigGemischteAenderungen_LassenSichVollstaendigRueckgaengigMachen()
    {
        var document = new ImageDocument(new PixelBuffer(200, 200));
        var undo = document.Undo;
        var created = new List<RectangleAnnotation>();
        for (var i = 0; i < 10; i++)
        {
            var rect = new RectangleAnnotation { Rect = new RectD(i * 10, i * 10, 20, 20) };
            created.Add(rect);
            undo.Execute(new AddAnnotationAction(document.Annotations, rect));
        }

        for (var i = 0; i < 10; i++)
        {
            var rect = created[i];
            undo.Execute(new MoveAnnotationsAction([rect], new VectorD(5, 5)));
            undo.Execute(new PropertyChangeAction<bool>(rect, "IsLocked", false, true, v => rect.IsLocked = v));
        }

        undo.UndoCount.ShouldBeGreaterThanOrEqualTo(30);
        while (undo.CanUndo)
        {
            undo.Undo();
        }

        document.Annotations.ShouldBeEmpty();
        while (undo.CanRedo)
        {
            undo.Redo();
        }

        document.Annotations.Count.ShouldBe(10);
        created.ShouldAllBe(r => r.IsLocked);
        created[3].Rect.ShouldBe(new RectD(35, 35, 20, 20));
    }
}
