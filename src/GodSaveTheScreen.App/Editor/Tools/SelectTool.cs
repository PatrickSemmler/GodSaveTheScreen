using System.Windows.Input;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Undo;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Editor.Tools;

public enum HandleKind
{
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
    LineStart,
    LineEnd,
    PolygonPoint,
    SourceTopLeft,
    SourceTopRight,
    SourceBottomRight,
    SourceBottomLeft,

    /// <summary>Drehgriff über der oberen Kante (nur bei genau einem drehbaren Objekt).</summary>
    Rotate,
}

public readonly record struct SelectionHandle(HandleKind Kind, PointD Position, int Index = 0);

/// <summary>Lage der Griffe einer Auswahl: Box-Griffe, Endpunkte von Linien, Eckpunkte von Polygonen.</summary>
public static class SelectionHandles
{
    /// <param name="selection">Ausgewählte Objekte.</param>
    /// <param name="rotateOffset">Abstand des Drehgriffs von der oberen Kante in Bildpixeln (0 = kein Drehgriff).</param>
    public static IReadOnlyList<SelectionHandle> Get(IReadOnlyList<Annotation> selection, double rotateOffset = 0)
    {
        var movable = selection.Where(a => !a.IsLocked && a.IsVisible).ToList();
        if (movable.Count == 0)
        {
            return [];
        }

        if (movable is [LineAnnotation line])
        {
            return [new(HandleKind.LineStart, line.Start), new(HandleKind.LineEnd, line.End)];
        }

        if (movable is [PolygonAnnotation polygon])
        {
            return [.. polygon.Points.Select((p, i) => new SelectionHandle(HandleKind.PolygonPoint, p, i))];
        }

        if (movable is [IRotatableAnnotation rotatable])
        {
            var rotated = RotatedBoxHandles(rotatable.Rect.Normalize(), rotatable.Rotation, rotateOffset);

            // Stempel: nur proportionales Skalieren über die Ecken (Rahmen und Schrift wachsen gemeinsam).
            return rotatable is StampAnnotation
                ? [.. rotated.Where(h => h.Kind is HandleKind.TopLeft or HandleKind.TopRight or HandleKind.BottomRight or HandleKind.BottomLeft or HandleKind.Rotate)]
                : rotated;
        }

        if (movable is [MagnifierAnnotation magnifier])
        {
            var s = magnifier.SourceRect.Normalize();
            return
            [
                new(HandleKind.SourceTopLeft, new PointD(s.X, s.Y)),
                new(HandleKind.SourceTopRight, new PointD(s.Right, s.Y)),
                new(HandleKind.SourceBottomRight, new PointD(s.Right, s.Bottom)),
                new(HandleKind.SourceBottomLeft, new PointD(s.X, s.Bottom)),
            ];
        }

        var bounds = movable.Select(a => a.Bounds).Aggregate((x, y) => x.Union(y));
        var handles = BoxHandles(bounds);
        return movable switch
        {
            // Text: Seitengriffe aendern die Umbruchbreite, Eckgriffe skalieren die Schrift.
            [TextAnnotation] => [.. handles.Where(h => h.Kind is not (HandleKind.Top or HandleKind.Bottom))],

            // Marker: nur proportionales Skalieren.
            [MarkerAnnotation] => [.. handles.Where(h => h.Kind is HandleKind.TopLeft or HandleKind.TopRight or HandleKind.BottomLeft or HandleKind.BottomRight)],
            _ => handles,
        };
    }

    public static IReadOnlyList<SelectionHandle> BoxHandles(RectD r)
    {
        var cx = r.X + (r.Width / 2);
        var cy = r.Y + (r.Height / 2);
        return
        [
            new(HandleKind.TopLeft, new PointD(r.X, r.Y)),
            new(HandleKind.Top, new PointD(cx, r.Y)),
            new(HandleKind.TopRight, new PointD(r.Right, r.Y)),
            new(HandleKind.Right, new PointD(r.Right, cy)),
            new(HandleKind.BottomRight, new PointD(r.Right, r.Bottom)),
            new(HandleKind.Bottom, new PointD(cx, r.Bottom)),
            new(HandleKind.BottomLeft, new PointD(r.X, r.Bottom)),
            new(HandleKind.Left, new PointD(r.X, cy)),
        ];
    }

    /// <summary>Griffe eines um seine Mitte gedrehten Rahmens samt Drehgriff.</summary>
    public static IReadOnlyList<SelectionHandle> RotatedBoxHandles(RectD rect, double rotation, double rotateOffset)
    {
        var center = rect.Center;
        var handles = BoxHandles(rect)
            .Select(h => h with { Position = RotationMath.Rotate(h.Position, center, rotation) })
            .ToList();
        if (rotateOffset > 0)
        {
            handles.Add(new SelectionHandle(HandleKind.Rotate, RotationMath.Rotate(new PointD(center.X, rect.Y - rotateOffset), center, rotation)));
        }

        return handles;
    }

    public static SelectionHandle? HitTest(IReadOnlyList<SelectionHandle> handles, PointD point, double radius)
    {
        SelectionHandle? best = null;
        var bestDistance = double.MaxValue;
        foreach (var handle in handles)
        {
            var distance = handle.Position.DistanceTo(point);
            if (distance <= radius && distance < bestDistance)
            {
                best = handle;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>
    /// Abbildung der Punkte beim Ziehen eines Box-Griffs. Fester Rand bleibt stehen, der gezogene folgt dem Zeiger
    /// (Umkehren ergibt Spiegelung). <paramref name="proportional"/> erzwingt gleichen Faktor in beiden Richtungen.
    /// </summary>
    public static (Func<PointD, PointD> Map, double Factor) ResizeMap(RectD original, HandleKind kind, PointD pointerPosition, bool proportional)
    {
        var (movesLeft, movesRight, movesTop, movesBottom) = kind switch
        {
            HandleKind.TopLeft => (true, false, true, false),
            HandleKind.Top => (false, false, true, false),
            HandleKind.TopRight => (false, true, true, false),
            HandleKind.Right => (false, true, false, false),
            HandleKind.BottomRight => (false, true, false, true),
            HandleKind.Bottom => (false, false, false, true),
            HandleKind.BottomLeft => (true, false, false, true),
            _ => (true, false, false, false),
        };

        double fixedX = movesLeft ? original.Right : original.X;
        double fixedY = movesTop ? original.Bottom : original.Y;
        var movingX = movesLeft ? original.X : original.Right;
        var movingY = movesTop ? original.Y : original.Bottom;
        var scaleX = 1.0;
        var scaleY = 1.0;
        if ((movesLeft || movesRight) && Math.Abs(movingX - fixedX) > 1e-9)
        {
            scaleX = Clamp((pointerPosition.X - fixedX) / (movingX - fixedX));
        }

        if ((movesTop || movesBottom) && Math.Abs(movingY - fixedY) > 1e-9)
        {
            scaleY = Clamp((pointerPosition.Y - fixedY) / (movingY - fixedY));
        }

        var corner = (movesLeft || movesRight) && (movesTop || movesBottom);
        if (proportional && corner)
        {
            var magnitude = Math.Max(Math.Abs(scaleX), Math.Abs(scaleY));
            scaleX = Math.Sign(scaleX == 0 ? 1 : scaleX) * magnitude;
            scaleY = Math.Sign(scaleY == 0 ? 1 : scaleY) * magnitude;
        }

        var factor = corner ? (Math.Abs(scaleX) + Math.Abs(scaleY)) / 2 : 0;
        return (p => new PointD(fixedX + ((p.X - fixedX) * scaleX), fixedY + ((p.Y - fixedY) * scaleY)), factor);
    }

    private static double Clamp(double scale)
    {
        const double minimum = 0.01;
        return Math.Abs(scale) < minimum ? (scale < 0 ? -minimum : minimum) : scale;
    }
}

/// <summary>Auswählen, Verschieben, Skalieren, Eckpunkte bearbeiten, Rahmenauswahl, Entf, Pfeiltasten.</summary>
public sealed class SelectTool : ToolBase
{
    public const string ToolId = "select";

    private enum Mode
    {
        None,
        Move,
        Resize,
        LineHandle,
        PolygonPoint,
        MagnifierSource,
        Rotate,
        RubberBand,
    }

    private enum MovePart
    {
        Both,
        Target,
        Source,
    }

    private const double MoveThreshold = 0.5;

    /// <summary>Abstand des Drehgriffs von der oberen Kante in Bildschirm-DIPs.</summary>
    private const double RotateHandleOffset = 26;

    private Mode _mode;
    private PointD _start;
    private PointD _last;
    private bool _moved;
    private List<(Annotation Target, Annotation Before)> _snapshots = [];
    private SelectionHandle _handle;
    private RectD _groupRect;
    private List<Annotation> _baseSelection = [];
    private Cursor _cursor = Cursors.Arrow;
    private MovePart _part;

    public SelectTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Select";

    public override SymbolRegular Symbol => SymbolRegular.Cursor24;

    public override Key ShortcutKey => Key.V;

    public override Cursor Cursor => _cursor;

    public override void Deactivate() => Cancel();

    public override void Cancel()
    {
        if (_mode is Mode.Move or Mode.Resize or Mode.LineHandle or Mode.PolygonPoint or Mode.MagnifierSource or Mode.Rotate)
        {
            foreach (var (target, before) in _snapshots)
            {
                target.CopyFrom(before);
            }
        }

        _mode = Mode.None;
        _snapshots = [];
        Context.Interaction.RubberBand = null;
        Context.InvalidateInteraction();
    }

    public override void OnPointerDown(EditorPointerArgs args)
    {
        if (args.Button != MouseButton.Left)
        {
            return;
        }

        _start = _last = args.Position;
        _moved = false;
        var selection = Context.Selection;

        var handles = SelectionHandles.Get(selection.Items, Context.ToImageUnits(RotateHandleOffset));
        var radius = Context.ToImageUnits(7);
        if (SelectionHandles.HitTest(handles, args.Position, radius) is { } handle)
        {
            BeginHandleDrag(handle);
            return;
        }

        var hit = Context.HitTest(args.Position);
        if (hit is not null)
        {
            if (args.ClickCount >= 2 && !args.Ctrl && !args.Shift)
            {
                HandleDoubleClick(hit, args.Position);
                return;
            }

            if (args.Ctrl || args.Shift)
            {
                selection.Toggle(hit);
                return;
            }

            if (!selection.Contains(hit))
            {
                selection.Set(hit);
            }

            BeginMove();
            return;
        }

        _baseSelection = args.Ctrl || args.Shift ? [.. selection.Items] : [];
        if (_baseSelection.Count == 0)
        {
            selection.Clear();
        }

        _mode = Mode.RubberBand;
    }

    public override void OnPointerMove(EditorPointerArgs args)
    {
        switch (_mode)
        {
            case Mode.Move:
                MoveTo(args.Position);
                break;
            case Mode.Resize:
                ResizeTo(args);
                break;
            case Mode.LineHandle:
                DragLineHandle(args);
                break;
            case Mode.PolygonPoint:
                DragPolygonPoint(args);
                break;
            case Mode.MagnifierSource:
                DragMagnifierSource(args);
                break;
            case Mode.Rotate:
                DragRotate(args);
                break;
            case Mode.RubberBand:
                UpdateRubberBand(args.Position);
                break;
            default:
                UpdateHoverCursor(args.Position);
                break;
        }
    }

    public override void OnPointerUp(EditorPointerArgs args)
    {
        var mode = _mode;
        _mode = Mode.None;
        if (mode == Mode.RubberBand)
        {
            Context.Interaction.RubberBand = null;
            Context.InvalidateInteraction();
        }
        else if (mode != Mode.None && _moved)
        {
            Commit(mode switch { Mode.Move => "Verschieben", Mode.Rotate => "Drehen", _ => "Größe ändern" });
        }

        _snapshots = [];
    }

    public override bool OnKeyDown(Key key, ModifierKeys modifiers)
    {
        var selection = Context.Selection;
        switch (key)
        {
            case Key.Escape:
                if (_mode != Mode.None)
                {
                    Cancel();
                }
                else
                {
                    selection.Clear();
                }

                return true;
            case Key.Delete or Key.Back when !selection.IsEmpty && _mode == Mode.None:
                DeleteSelection();
                return true;
            case Key.A when modifiers == ModifierKeys.Control:
                selection.Set(Context.Document.Annotations.Where(a => a.IsVisible));
                return true;
            case Key.Left or Key.Right or Key.Up or Key.Down when !selection.IsEmpty && _mode == Mode.None && (modifiers & ModifierKeys.Alt) != 0:
                ResizeByKey(key, (modifiers & ModifierKeys.Shift) != 0 ? 10 : 1);
                return true;
            case Key.Left or Key.Right or Key.Up or Key.Down when !selection.IsEmpty && _mode == Mode.None && (modifiers & ModifierKeys.Control) == 0:
                Nudge(key, (modifiers & ModifierKeys.Shift) != 0 ? 10 : 1);
                return true;
            case Key.OemOpenBrackets or Key.OemCloseBrackets when modifiers == ModifierKeys.None && _mode == Mode.None && HasRotatable():
                RotateByKey(key == Key.OemCloseBrackets ? ShapeGeometry.AngleStepDegrees : -ShapeGeometry.AngleStepDegrees);
                return true;
            case Key.H or Key.V when modifiers == ModifierKeys.Shift && _mode == Mode.None && HasRotatable():
                FlipSelection(horizontal: key == Key.H);
                return true;
            case Key.Tab when _mode == Mode.None && (modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == 0:
                return CycleSelection((modifiers & ModifierKeys.Shift) != 0);
            default:
                return false;
        }
    }

    /// <summary>
    /// <c>Tab</c> / <c>Umschalt+Tab</c>: wählt das nächste bzw. vorherige sichtbare Objekt (Reihenfolge der Ebenenliste von hinten nach vorn).
    /// Nach dem letzten Objekt wird die Auswahl aufgehoben und der Fokus darf die Zeichenfläche verlassen (<c>false</c>).
    /// </summary>
    public bool CycleSelection(bool backwards)
    {
        var visible = Context.Document.Annotations.Where(a => a.IsVisible).ToList();
        if (visible.Count == 0)
        {
            return false;
        }

        var current = Context.Selection.Items.Count == 1 ? visible.IndexOf(Context.Selection.Items[0]) : -1;
        int next;
        if (current < 0)
        {
            next = backwards ? visible.Count - 1 : 0;
        }
        else
        {
            next = current + (backwards ? -1 : 1);
        }

        if (next < 0 || next >= visible.Count)
        {
            Context.Selection.Clear();
            return false;
        }

        Context.Selection.Set(visible[next]);
        return true;
    }

    /// <summary>
    /// <c>Alt+Pfeil</c>: ändert die Größe der Auswahl um <paramref name="step"/> Pixel (rechts/unten größer, links/oben kleiner), Ankerpunkt oben links.
    /// Objekte ohne frei skalierbare Form (Text, Nummer, Lupe …) werden proportional skaliert.
    /// </summary>
    public void ResizeByKey(Key key, double step)
    {
        var items = Context.Selection.Items.Where(a => !a.IsLocked).ToList();
        if (items.Count == 0)
        {
            return;
        }

        var bounds = items.Select(a => a.Bounds).Aggregate((x, y) => x.Union(y));
        var dw = key == Key.Right ? step : key == Key.Left ? -step : 0;
        var dh = key == Key.Down ? step : key == Key.Up ? -step : 0;
        var width = Math.Max(1, bounds.Width + dw);
        var height = Math.Max(1, bounds.Height + dh);
        var sx = bounds.Width > 0 ? width / bounds.Width : 1;
        var sy = bounds.Height > 0 ? height / bounds.Height : 1;
        var proportional = items.Any(a => a is not (ShapeAnnotation or BlurAnnotation));
        double? uniform = null;
        if (proportional)
        {
            uniform = dw != 0 ? sx : sy;
            sx = sy = uniform.Value;
        }

        var origin = bounds.TopLeft;
        var changes = new List<(Annotation Target, Annotation Before, Annotation After)>();
        foreach (var item in items)
        {
            var before = item.Clone();
            item.MapPoints(p => new PointD(origin.X + ((p.X - origin.X) * sx), origin.Y + ((p.Y - origin.Y) * sy)));
            if (uniform is { } factor)
            {
                item.ScaleSizes(factor);
            }

            changes.Add((item, before, item.Clone()));
        }

        Context.Undo.Execute(new ReplaceGeometryAction(changes, "Größe ändern"));
        Context.InvalidateInteraction();
    }

    private bool HasRotatable() => Context.Selection.Items.Any(a => a is IRotatableAnnotation && !a.IsLocked);

    /// <summary><c>[</c> / <c>]</c>: dreht alle drehbaren Objekte der Auswahl um <paramref name="degrees"/> (ein Undo-Schritt).</summary>
    public void RotateByKey(double degrees)
    {
        var items = Context.Selection.Items.Where(a => a is IRotatableAnnotation && !a.IsLocked).ToList();
        if (items.Count == 0)
        {
            return;
        }

        using (Context.Undo.BeginTransaction("Drehen"))
        {
            foreach (var item in items)
            {
                var target = (IRotatableAnnotation)item;
                Context.Undo.Execute(new PropertyChangeAction<double>(item, nameof(IRotatableAnnotation.Rotation), target.Rotation, RotationMath.Normalize(target.Rotation + degrees), v => target.Rotation = v, "Drehen"));
            }
        }

        Context.InvalidateInteraction();
    }

    /// <summary><c>Umschalt+H</c> / <c>Umschalt+V</c>: spiegelt alle Emojis der Auswahl (ein Undo-Schritt).</summary>
    public void FlipSelection(bool horizontal)
    {
        var items = Context.Selection.Items.OfType<EmojiAnnotation>().Where(e => !e.IsLocked).ToList();
        if (items.Count == 0)
        {
            return;
        }

        using (Context.Undo.BeginTransaction("Spiegeln"))
        {
            foreach (var item in items)
            {
                var target = item;
                var old = horizontal ? target.FlipHorizontal : target.FlipVertical;
                Context.Undo.Execute(new PropertyChangeAction<bool>(
                    target,
                    horizontal ? nameof(EmojiAnnotation.FlipHorizontal) : nameof(EmojiAnnotation.FlipVertical),
                    old,
                    !old,
                    v =>
                    {
                        if (horizontal)
                        {
                            target.FlipHorizontal = v;
                        }
                        else
                        {
                            target.FlipVertical = v;
                        }
                    },
                    "Spiegeln"));
            }
        }

        Context.InvalidateInteraction();
    }

    public void DeleteSelection()
    {
        var items = Context.Selection.Items.Where(a => !a.IsLocked).ToList();
        if (items.Count == 0)
        {
            return;
        }

        Context.Undo.Execute(new RemoveAnnotationsAction(Context.Document.Annotations, items, "Löschen"));
        Context.Selection.Clear();
    }

    private void Nudge(Key key, double step)
    {
        var items = Context.Selection.Items.Where(a => !a.IsLocked).ToList();
        if (items.Count == 0)
        {
            return;
        }

        var delta = key switch
        {
            Key.Left => new VectorD(-step, 0),
            Key.Right => new VectorD(step, 0),
            Key.Up => new VectorD(0, -step),
            _ => new VectorD(0, step),
        };
        Context.Undo.Execute(new MoveAnnotationsAction(items, delta, "Verschieben"));
        Context.InvalidateInteraction();
    }

    private void HandleDoubleClick(Annotation hit, PointD position)
    {
        Context.Selection.Set(hit);
        switch (hit)
        {
            case TextAnnotation text when !text.IsLocked:
                Context.BeginTextEdit(text, isNew: false);
                break;
            case StampAnnotation stamp when !stamp.IsLocked:
                Context.BeginTextEdit(stamp, isNew: false);
                break;
            case PolygonAnnotation polygon when !polygon.IsLocked:
                InsertPolygonPoint(polygon, position);
                break;
        }
    }

    private void InsertPolygonPoint(PolygonAnnotation polygon, PointD position)
    {
        var points = polygon.Points;
        if (points.Length < 2)
        {
            return;
        }

        var segmentCount = polygon.IsClosed ? points.Length : points.Length - 1;
        var bestIndex = 0;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < segmentCount; i++)
        {
            var distance = HitTesting.DistanceToSegment(position, points[i], points[(i + 1) % points.Length]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        var before = polygon.Clone();
        polygon.Points = points.Insert(bestIndex + 1, position);
        var after = polygon.Clone();
        Context.Undo.Execute(new ReplaceGeometryAction([(polygon, before, after)], "Punkt einfügen"));
    }

    private void BeginMove()
    {
        var items = Context.Selection.Items.Where(a => !a.IsLocked).ToList();
        if (items.Count == 0)
        {
            return;
        }

        _part = MovePart.Both;
        if (items is [MagnifierAnnotation magnifier])
        {
            // Lupe: Ziehen am Lupenkörper verschiebt nur das Ziel, am Quellrahmen nur die Quelle.
            var target = magnifier.TargetRect.Inflate(Context.HitTolerance, Context.HitTolerance);
            _part = target.Contains(_start) ? MovePart.Target : MovePart.Source;
        }

        _snapshots = [.. items.Select(a => (a, a.Clone()))];
        _mode = Mode.Move;
    }

    private void BeginHandleDrag(SelectionHandle handle)
    {
        var items = Context.Selection.Items.Where(a => !a.IsLocked).ToList();
        _snapshots = [.. items.Select(a => (a, a.Clone()))];
        _handle = handle;
        _mode = handle.Kind switch
        {
            HandleKind.LineStart or HandleKind.LineEnd => Mode.LineHandle,
            HandleKind.PolygonPoint => Mode.PolygonPoint,
            HandleKind.SourceTopLeft or HandleKind.SourceTopRight or HandleKind.SourceBottomRight or HandleKind.SourceBottomLeft => Mode.MagnifierSource,
            HandleKind.Rotate => Mode.Rotate,
            _ => Mode.Resize,
        };
        _groupRect = items.Select(a => a.Bounds).Aggregate((x, y) => x.Union(y));
    }

    private void MoveTo(PointD position)
    {
        var delta = position - _last;
        if (!_moved && position.DistanceTo(_start) < MoveThreshold)
        {
            return;
        }

        _moved = true;
        _last = position;
        foreach (var (target, _) in _snapshots)
        {
            if (target is MagnifierAnnotation magnifier && _part != MovePart.Both)
            {
                if (_part == MovePart.Target)
                {
                    magnifier.TargetCenter += delta;
                }
                else
                {
                    magnifier.SourceRect = magnifier.SourceRect with { X = magnifier.SourceRect.X + delta.X, Y = magnifier.SourceRect.Y + delta.Y };
                }
            }
            else
            {
                target.Translate(delta);
            }
        }

        Context.InvalidateInteraction();
    }

    private void ResizeTo(EditorPointerArgs args)
    {
        _moved = true;
        if (_snapshots is [(TextAnnotation textTarget, TextAnnotation textBefore)] && _handle.Kind is HandleKind.Left or HandleKind.Right)
        {
            ResizeTextWidth(textTarget, textBefore, args.Position);
            Context.InvalidateInteraction();
            return;
        }

        if (_snapshots is [(EmojiAnnotation emojiTarget, EmojiAnnotation emojiBefore)])
        {
            ResizeRotated(emojiTarget, emojiBefore, args);
            Context.InvalidateInteraction();
            return;
        }

        if (_snapshots is [(StampAnnotation stampTarget, StampAnnotation stampBefore)])
        {
            ResizeStamp(stampTarget, stampBefore, args.Position);
            Context.InvalidateInteraction();
            return;
        }

        var (map, factor) = SelectionHandles.ResizeMap(_groupRect, _handle.Kind, args.Position, args.Shift || _snapshots.Any(s => s.Before is not (ShapeAnnotation or BlurAnnotation)));
        foreach (var (target, before) in _snapshots)
        {
            target.CopyFrom(before);
            target.MapPoints(map);
            if (factor > 0)
            {
                target.ScaleSizes(factor);
            }
        }

        Context.InvalidateInteraction();
    }

    /// <summary>
    /// Skaliert ein gedrehtes Objekt im eigenen Koordinatensystem: Der Zeiger wird zurückgedreht, der gegenüberliegende Rand
    /// bleibt auf dem Bildschirm stehen. Eckgriffe skalieren proportional, mit <c>Umschalt</c> frei; Kantengriffe immer frei.
    /// </summary>
    private void ResizeRotated(EmojiAnnotation target, EmojiAnnotation before, EditorPointerArgs args)
    {
        var rect = before.Rect.Normalize();
        var center = rect.Center;
        var local = RotationMath.Unrotate(args.Position, center, before.Rotation);
        var (map, _) = SelectionHandles.ResizeMap(rect, _handle.Kind, local, proportional: !args.Shift);
        var resized = RectD.FromPoints(map(rect.TopLeft), map(rect.BottomRight));
        if (resized.Width < EmojiAnnotation.MinSize || resized.Height < EmojiAnnotation.MinSize)
        {
            return;
        }

        var worldCenter = RotationMath.Rotate(resized.Center, center, before.Rotation);
        var shift = new VectorD(worldCenter.X - resized.Center.X, worldCenter.Y - resized.Center.Y);
        target.Rect = new RectD(resized.X + shift.X, resized.Y + shift.Y, resized.Width, resized.Height);
    }

    /// <summary>
    /// Skaliert einen Stempel proportional über einen Eckgriff im eigenen (gedrehten) Koordinatensystem:
    /// Schriftgröße und Rahmenstärke wachsen mit, die gegenüberliegende Ecke bleibt stehen.
    /// </summary>
    private void ResizeStamp(StampAnnotation target, StampAnnotation before, PointD pointer)
    {
        var rect = before.Rect;
        var center = before.Center;
        var local = RotationMath.Unrotate(pointer, center, before.Rotation);
        var (map, _) = SelectionHandles.ResizeMap(rect, _handle.Kind, local, proportional: true);
        var resized = RectD.FromPoints(map(rect.TopLeft), map(rect.BottomRight));
        var factor = rect.Width > 0 ? resized.Width / rect.Width : 1;
        if (before.FontSize * factor < StampAnnotation.MinFontSize || before.FontSize * factor > StampAnnotation.MaxFontSize)
        {
            return;
        }

        target.CopyFrom(before);
        target.ScaleSizes(factor);
        target.Center = RotationMath.Rotate(resized.Center, center, before.Rotation);
    }

    /// <summary>Drehgriff: dreht um die Mitte; <c>Umschalt</c> rastet in 15°-Schritten ein.</summary>
    private void DragRotate(EditorPointerArgs args)
    {
        _moved = true;
        if (_snapshots is not [(IRotatableAnnotation target, IRotatableAnnotation before)])
        {
            return;
        }

        var center = before.Center;
        var angle = (Math.Atan2(args.Position.Y - center.Y, args.Position.X - center.X) * 180 / Math.PI) + 90;
        if (args.Shift)
        {
            angle = Math.Round(angle / ShapeGeometry.AngleStepDegrees) * ShapeGeometry.AngleStepDegrees;
        }

        target.Rotation = angle;
        Context.InvalidateInteraction();
    }

    /// <summary>Seitengriffe eines Textes ändern die Umbruchbreite (links verschiebt zusätzlich die Position).</summary>
    private void ResizeTextWidth(TextAnnotation target, TextAnnotation before, PointD pointerPosition)
    {
        const double minimumWidth = 12;
        var inflate = before.TextRect.X - before.Bounds.X;
        var textLeft = before.TextRect.X;
        var textRight = before.TextRect.Right;
        if (_handle.Kind == HandleKind.Right)
        {
            target.WrapWidth = Math.Max(minimumWidth, pointerPosition.X - inflate - textLeft);
            return;
        }

        var left = Math.Min(pointerPosition.X + inflate, textRight - minimumWidth);
        target.Position = new PointD(left, before.Position.Y);
        target.WrapWidth = textRight - left;
    }

    private void DragLineHandle(EditorPointerArgs args)
    {
        _moved = true;
        if (_snapshots is not [(LineAnnotation line, LineAnnotation before)])
        {
            return;
        }

        var anchor = _handle.Kind == HandleKind.LineStart ? before.End : before.Start;
        var point = args.Shift ? ShapeGeometry.SnapAngle(anchor, args.Position) : args.Position;
        if (_handle.Kind == HandleKind.LineStart)
        {
            line.Start = point;
        }
        else
        {
            line.End = point;
        }

        Context.InvalidateInteraction();
    }

    private void DragMagnifierSource(EditorPointerArgs args)
    {
        _moved = true;
        if (_snapshots is not [(MagnifierAnnotation magnifier, MagnifierAnnotation before)])
        {
            return;
        }

        var s = before.SourceRect.Normalize();
        var anchor = _handle.Kind switch
        {
            HandleKind.SourceTopLeft => new PointD(s.Right, s.Bottom),
            HandleKind.SourceTopRight => new PointD(s.X, s.Bottom),
            HandleKind.SourceBottomRight => new PointD(s.X, s.Y),
            _ => new PointD(s.Right, s.Y),
        };
        var square = args.Shift || magnifier.Shape == MagnifierShape.Circle;
        var rect = ShapeGeometry.ConstrainBox(anchor, args.Position, square ? ModifierKeys.Shift : ModifierKeys.None);
        if (rect.Width >= 4 && rect.Height >= 4)
        {
            magnifier.SourceRect = rect;
        }

        Context.InvalidateInteraction();
    }

    private void DragPolygonPoint(EditorPointerArgs args)
    {
        _moved = true;
        if (_snapshots is not [(PolygonAnnotation polygon, PolygonAnnotation before)])
        {
            return;
        }

        var points = before.Points;
        var origin = points.Length > 1 ? points[(_handle.Index + points.Length - 1) % points.Length] : args.Position;
        var point = args.Shift ? ShapeGeometry.SnapAngle(origin, args.Position) : args.Position;
        polygon.Points = points.SetItem(_handle.Index, point);
        Context.InvalidateInteraction();
    }

    private void UpdateRubberBand(PointD position)
    {
        var rect = RectD.FromPoints(_start, position);
        Context.Interaction.RubberBand = rect;
        var inside = Context.Document.Annotations.Where(a => a.IsVisible && rect.ContainsRect(a.Bounds));
        Context.Selection.Set(_baseSelection.Concat(inside));
        Context.InvalidateInteraction();
    }

    private void UpdateHoverCursor(PointD position)
    {
        var handles = SelectionHandles.Get(Context.Selection.Items, Context.ToImageUnits(RotateHandleOffset));
        var handle = SelectionHandles.HitTest(handles, position, Context.ToImageUnits(7));
        _cursor = handle is { } h
            ? h.Kind switch
            {
                HandleKind.Top or HandleKind.Bottom => Cursors.SizeNS,
                HandleKind.Left or HandleKind.Right => Cursors.SizeWE,
                HandleKind.TopLeft or HandleKind.BottomRight or HandleKind.SourceTopLeft or HandleKind.SourceBottomRight => Cursors.SizeNWSE,
                HandleKind.TopRight or HandleKind.BottomLeft or HandleKind.SourceTopRight or HandleKind.SourceBottomLeft => Cursors.SizeNESW,
                HandleKind.Rotate => Cursors.Hand,
                _ => Cursors.Cross,
            }
            : Context.HitTest(position) is { IsLocked: false } ? Cursors.SizeAll : Cursors.Arrow;
    }

    private void Commit(string description)
    {
        var changes = _snapshots
            .Select(s => (s.Target, s.Before, After: s.Target.Clone()))
            .ToList();
        if (changes.Count > 0)
        {
            Context.Undo.Execute(new ReplaceGeometryAction(changes, description));
        }
    }
}
