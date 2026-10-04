using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ValheimTomrer.Editor.Catalog;
using ValheimTomrer.Editor.Doc;
using ValheimTomrer.Editor.Placement;

namespace ValheimTomrer.Editor
{
    /// <summary>What a click on the pane does.</summary>
    internal enum EditMode
    {
        /// <summary>Nothing in hand: a click selects.</summary>
        Idle,

        /// <summary>Something is in hand: a click drops it.</summary>
        Place,
    }

    /// <summary>Why something is in hand.</summary>
    internal enum PlaceAction
    {
        /// <summary>A new piece from the palette. Stays in hand after dropping.</summary>
        Add,

        /// <summary>The selected pieces, taken out of the scene. Back to idle after dropping.</summary>
        Move,

        /// <summary>Copies of the selected pieces. Stays in hand after dropping.</summary>
        Duplicate,
    }

    /// <summary>How a new selection meets the old one.</summary>
    internal enum SelectHow
    {
        Set,
        Add,
        Toggle,
    }

    /// <summary>
    /// What the editor is doing to the open blueprint: what is selected, what is in hand, and every
    /// action that changes either. The same list as the Tomrer editor's store (src/app/store.ts),
    /// so the two behave the same.
    ///
    /// Nothing here touches the UI or the 3D pane. The pane reads it once a frame and draws it,
    /// which is what lets the autotest drive a whole editing session with no window open.
    /// </summary>
    internal static class EditorState
    {
        /// <summary>Draw the pieces as boxes instead of models. A top bar switch.</summary>
        public static bool PieceBoxesOn { get; set; }

        /// <summary>Draw the selection's size and the gaps beside it over the view. This session only.</summary>
        public static bool DimensionsOn { get; set; } = true;

        /// <summary>The grid lines and axes are drawn on the ground (the setting). Placing still uses the grid step.</summary>
        public static bool GridShown => EditorConfig.ShowGrid == null || EditorConfig.ShowGrid.Value;

        /// <summary>Colour every piece by how well it is held up (green strong, red about to break). This session only.</summary>
        public static bool SupportColoursOn { get; set; }

        /// <summary>Show the snap points while placing. Snapping itself is always on.</summary>
        public static bool SnapDotsOn { get; set; } = true;

        /// <summary>A group bigger than this snaps against every piece, not only the ones within 10 m.</summary>
        private const int SearchAllFrom = 8;

        private static readonly HashSet<int> Selected = new HashSet<int>();
        private static readonly List<int> MovingIds = new List<int>();
        private static readonly HashSet<int> Hidden = new HashSet<int>();
        private static readonly HashSet<int> Locked = new HashSet<int>();
        private static readonly HashSet<int> Isolated = new HashSet<int>();
        private static readonly List<int> NotDrawnIds = new List<int>();

        // Groups belong to this editing session, like hide and lock: the file format has none.
        private static readonly Dictionary<int, int> GroupOf = new Dictionary<int, int>();
        private static readonly Dictionary<int, List<int>> GroupMembers = new Dictionary<int, List<int>>();
        private static int _nextGroup = 1;

        // What Copy took, kept across blueprints (pieces are immutable, so the list can be shared).
        private static List<DocPiece> _clipboard = new List<DocPiece>();

        private static SceneIndex _index;
        private static int _indexRevision = -1;
        private static int _indexMoving = -1;

        private static SupportMap _stability;
        private static SceneIndex _stabilityIndex;

        private static PlaceResult _weighed;
        private static SupportMap _weighedMap;
        private static MovingSet _weighedSet;

        // The piece table the entries in hand and in the index were read from.
        private static PieceTable _catalog;

        /// <summary>The blueprint being edited, or null while the editor is closed.</summary>
        public static BlueprintDocument Document { get; private set; }

        /// <summary>The ids of the selected pieces.</summary>
        public static IReadOnlyCollection<int> Selection => Selected;

        public static int SelectionCount => Selected.Count;

        /// <summary>The ids of the pieces in hand while moving. Empty otherwise.</summary>
        public static IReadOnlyList<int> Carrying =>
            Mode == EditMode.Place && Action == PlaceAction.Move ? MovingIds : (IReadOnlyList<int>)EmptyIds;

        private static readonly int[] EmptyIds = new int[0];

        public static EditMode Mode { get; private set; } = EditMode.Idle;

        public static PlaceAction Action { get; private set; }

        /// <summary>What is in hand, laid out around its pivot. Null while idle.</summary>
        public static MovingSet Moving { get; private set; }

        /// <summary>The catalog entry in hand while adding. Null for move and duplicate.</summary>
        public static PieceEntry Held { get; private set; }

        /// <summary>Wheel steps of 22.5 degrees for what is in hand.</summary>
        public static int Steps { get; private set; }

        /// <summary>The turn new pieces keep between placements, the way the game does.</summary>
        public static int PlaceSteps { get; private set; }

        /// <summary>The chosen snap point, -1 for automatic.</summary>
        public static int Manual { get; private set; } = -1;

        /// <summary>False while the no-snap modifier is held.</summary>
        public static bool Snapping { get; set; } = true;

        /// <summary>Where what is in hand would land, from the last <see cref="Aim"/>. Null when nothing was hit.</summary>
        public static PlaceResult Aimed { get; private set; }

        /// <summary>The last thing the editor wants to tell the player, and when it was said.</summary>
        public static string Message { get; private set; }

        public static float MessageAt { get; private set; }

        /// <summary>Goes up on every change of selection or mode, so the pane can notice.</summary>
        public static int Version { get; private set; }

        /// <summary>The pieces that stay put: the document, minus whatever is being moved.</summary>
        public static SceneIndex Index
        {
            get
            {
                var moving = Action == PlaceAction.Move && Mode == EditMode.Place ? MovingIds.Count : 0;
                if (_index != null && Document != null && _indexRevision == Document.Revision && _indexMoving == moving)
                {
                    return _index;
                }

                _indexRevision = Document != null ? Document.Revision : -1;
                _indexMoving = moving;
                _index = new SceneIndex(StandingPieces());
                return _index;
            }
        }

        /// <summary>
        /// How well every piece that stays put is held up, by the game's own rule. Worked out again
        /// whenever the index is.
        /// </summary>
        public static SupportMap Stability
        {
            get
            {
                var index = Index;
                if (_stability == null || !ReferenceEquals(_stabilityIndex, index))
                {
                    _stability = Support.Solve(index);
                    _stabilityIndex = index;
                }

                return _stability;
            }
        }

        public static void Open(BlueprintDocument document)
        {
            if (document != null)
            {
                PieceBoxesOn = EditorConfig.Boxes != null && EditorConfig.Boxes.Value;
                SnapDotsOn = EditorConfig.SnapDots == null || EditorConfig.SnapDots.Value;
            }

            Document = document;
            Selected.Clear();
            Hidden.Clear();
            Locked.Clear();
            GroupOf.Clear();
            GroupMembers.Clear();
            Isolated.Clear();
            MovingIds.Clear();
            Mode = EditMode.Idle;
            Moving = null;
            Held = null;
            Steps = 0;
            PlaceSteps = 0;
            Manual = -1;
            Snapping = true;
            Aimed = null;
            Message = null;
            _index = null;
            _indexRevision = -1;
            _stability = null;
            _stabilityIndex = null;
            _weighed = null;
            _catalog = PieceCatalog.Table;
            Version++;
        }

        public static void Close()
        {
            Open(null);
        }

        /// <summary>
        /// The editor opens again on what it held when it closed. When the catalog was read again
        /// meanwhile (a world change), what is in hand takes the new entries, and the placing rule
        /// and the support numbers are worked out again.
        /// </summary>
        public static void Rebind()
        {
            if (ReferenceEquals(_catalog, PieceCatalog.Table))
            {
                return;
            }

            _catalog = PieceCatalog.Table;
            _index = null;
            _stability = null;
            _stabilityIndex = null;
            _weighed = null;
            if (Document == null || Mode != EditMode.Place || Moving == null)
            {
                return;
            }

            var stale = false;
            foreach (var moving in Moving.Pieces)
            {
                stale |= !ReferenceEquals(moving.Entry, PieceCatalog.Find(moving.Prefab));
            }

            if (!stale)
            {
                return;
            }

            if (Held != null)
            {
                var entry = PieceCatalog.Find(Held.PrefabName);
                if (entry == null)
                {
                    CancelMode();
                    return;
                }

                Held = entry;
                Moving = MovingSet.One(entry);
                Version++;
                return;
            }

            // Moved pieces and copies are taken from the document again, the way they were first taken.
            var pieces = new List<DocPiece>();
            foreach (var moving in Moving.Pieces)
            {
                var piece = Document.Find(moving.Id);
                if (piece == null)
                {
                    CancelMode();
                    return;
                }

                pieces.Add(piece);
            }

            Moving = MovingSet.Of(MovingPieces(pieces));
            Version++;
        }

        // ---------- selection ----------

        public static void Select(IEnumerable<int> ids, SelectHow how = SelectHow.Set)
        {
            // A new selection starts a new undo step for nudges and typed numbers.
            if (Document != null)
            {
                Document.BreakRun();
            }

            var requested = ids != null ? ids.ToList() : new List<int>();
            var previous = how == SelectHow.Set && requested.Count > 0 ? new List<int>(Selected) : null;
            if (how == SelectHow.Set)
            {
                Selected.Clear();
            }

            if (ids != null)
            {
                foreach (var id in requested)
                {
                    // A piece in a group brings its whole group.
                    var members = MembersOf(id);
                    if (how == SelectHow.Toggle && Selected.Contains(id))
                    {
                        foreach (var member in members)
                        {
                            Selected.Remove(member);
                        }
                    }
                    else
                    {
                        foreach (var member in members)
                        {
                            if (!Locked.Contains(member) && !IsHidden(member))
                            {
                                Selected.Add(member);
                            }
                        }
                    }
                }
            }

            // Everything asked for is hidden or locked: say so, and keep what was selected.
            if (previous != null && Selected.Count == 0)
            {
                foreach (var id in previous)
                {
                    Selected.Add(id);
                }

                Say("Those pieces are hidden or locked. Show or unlock them first.");
            }

            Version++;
        }

        public static bool IsSelected(int id)
        {
            return Selected.Contains(id);
        }

        public static void Select(int id, SelectHow how = SelectHow.Set)
        {
            Select(new[] { id }, how);
        }

        public static void SelectAll()
        {
            Selected.Clear();
            if (Document != null)
            {
                foreach (var piece in Document.Pieces)
                {
                    if (!Locked.Contains(piece.Id) && !IsHidden(piece.Id))
                    {
                        Selected.Add(piece.Id);
                    }
                }
            }

            Version++;
        }

        // ---------- groups ----------

        /// <summary>The piece and everyone in its group, or only the piece when it has none.</summary>
        private static IList<int> MembersOf(int id)
        {
            return GroupOf.TryGetValue(id, out var group) && GroupMembers.TryGetValue(group, out var list)
                ? (IList<int>)list
                : new[] { id };
        }

        public static bool IsGrouped(int id) => GroupOf.ContainsKey(id);

        public static int GroupCount => GroupMembers.Count;

        /// <summary>Picks of one piece select all of them from now on. Not saved in the file, like hide and lock.</summary>
        public static bool GroupSelection()
        {
            if (Selected.Count < 2)
            {
                Say("Select two or more pieces to group them.");
                return false;
            }

            var ids = Selected.ToList();
            foreach (var id in ids)
            {
                Leave(id);
            }

            var group = _nextGroup++;
            GroupMembers[group] = ids;
            foreach (var id in ids)
            {
                GroupOf[id] = group;
            }

            Say($"Grouped {Count(ids.Count)}. A click on one picks them all. Not saved in the file.");
            Version++;
            return true;
        }

        /// <summary>Breaks up every group the selection touches.</summary>
        public static bool UngroupSelection()
        {
            var groups = new HashSet<int>();
            foreach (var id in Selected)
            {
                if (GroupOf.TryGetValue(id, out var group))
                {
                    groups.Add(group);
                }
            }

            if (groups.Count == 0)
            {
                Say("Nothing selected is in a group.");
                return false;
            }

            foreach (var group in groups)
            {
                foreach (var id in GroupMembers[group])
                {
                    GroupOf.Remove(id);
                }

                GroupMembers.Remove(group);
            }

            Say(groups.Count == 1 ? "Ungrouped." : $"Ungrouped {groups.Count} groups.");
            Version++;
            return true;
        }

        /// <summary>Takes a piece out of its group; a group left with one piece is gone.</summary>
        private static void Leave(int id)
        {
            if (!GroupOf.TryGetValue(id, out var group))
            {
                return;
            }

            GroupOf.Remove(id);
            var list = GroupMembers[group];
            list.Remove(id);
            if (list.Count < 2)
            {
                foreach (var left in list)
                {
                    GroupOf.Remove(left);
                }

                GroupMembers.Remove(group);
            }
        }

        // ---------- the clipboard ----------

        public static int ClipboardCount => _clipboard.Count;

        /// <summary>Copy: the selection is kept, with its places, for Paste, in this blueprint or another.</summary>
        public static bool CopySelection()
        {
            var pieces = SelectedPieces();
            if (pieces.Count == 0)
            {
                Say("Select something to copy.");
                return false;
            }

            _clipboard = pieces;
            Say($"Copied {Count(pieces.Count)}. Paste puts them in hand.");
            return true;
        }

        /// <summary>
        /// Paste: what Copy kept goes in hand like a duplicate, around where it was copied from, and stays
        /// there so more can be dropped.
        /// </summary>
        public static bool StartPaste()
        {
            if (_clipboard.Count == 0)
            {
                Say("Nothing is copied yet.");
                return false;
            }

            return StartPasteFrom(_clipboard);
        }

        /// <summary>Puts these pieces in hand as copies, wherever they came from (another blueprint, a file).</summary>
        public static bool StartPasteFrom(IList<DocPiece> pieces)
        {
            if (Document == null || pieces == null || pieces.Count == 0)
            {
                return false;
            }

            MovingIds.Clear();
            Held = null;
            Moving = MovingSet.Of(MovingPieces(pieces));
            Action = PlaceAction.Duplicate;
            Mode = EditMode.Place;
            Steps = 0;
            Manual = -1;
            Aimed = null;
            Version++;
            return true;
        }

        // ---------- hide, lock, select alike, align: the Figma-like commands ----------

        /// <summary>
        /// Hidden and locked pieces belong to this editing session only: they are not in the file, and a
        /// reopened blueprint shows and unlocks everything. A hidden piece is not drawn and cannot be
        /// picked; a locked one is drawn but cannot be selected.
        /// </summary>
        public static bool IsHidden(int id) => Hidden.Contains(id);

        public static bool IsLocked(int id) => Locked.Contains(id);

        public static int HiddenCount => Hidden.Count;

        public static int LockedCount => Locked.Count;

        /// <summary>The pieces the pane does not draw: the ones in hand and the hidden ones.</summary>
        public static IReadOnlyList<int> NotDrawn
        {
            get
            {
                if (Hidden.Count == 0)
                {
                    return Carrying;
                }

                NotDrawnIds.Clear();
                NotDrawnIds.AddRange(Carrying);
                NotDrawnIds.AddRange(Hidden);
                return NotDrawnIds;
            }
        }

        /// <summary>Degrees per turn step now: the setting, or the game's 22.5.</summary>
        public static float AngleStep => EditorConfig.AngleStep != null && EditorConfig.AngleStep.Value > 0f
            ? EditorConfig.AngleStep.Value
            : Placer.RotateStep;

        /// <summary>The placing grid in metres now, 0 for none.</summary>
        public static float GridStep => EditorConfig.GridStep != null ? Mathf.Max(0f, EditorConfig.GridStep.Value) : 0f;

        /// <summary>The axis Align and Spread work along: 0 x, 1 y, 2 z.</summary>
        public static int AlignAxis { get; private set; }

        public static string AlignAxisName => AlignAxis == 0 ? "X" : AlignAxis == 1 ? "Y" : "Z";

        public static void CycleAlignAxis()
        {
            AlignAxis = (AlignAxis + 1) % 3;
            Say($"Align and spread along {AlignAxisName}.");
            Version++;
        }

        public static void SetHidden(IEnumerable<int> ids, bool hidden)
        {
            foreach (var id in ids.ToList())
            {
                if (hidden)
                {
                    Hidden.Add(id);
                    Selected.Remove(id);
                }
                else
                {
                    Hidden.Remove(id);
                }
            }

            Version++;
        }

        public static void SetLocked(IEnumerable<int> ids, bool locked)
        {
            foreach (var id in ids.ToList())
            {
                if (locked)
                {
                    Locked.Add(id);
                    Selected.Remove(id);
                }
                else
                {
                    Locked.Remove(id);
                }
            }

            Version++;
        }

        /// <summary>Hides the selection. With nothing selected it shows everything again.</summary>
        public static void HideSelection()
        {
            SettleHand();
            if (Selected.Count == 0)
            {
                ShowAll();
                return;
            }

            var count = Selected.Count;
            SetHidden(Selected, true);
            Say($"Hid {Count(count)}. Hide again with nothing selected, or the Show all key, brings back everything.");
        }

        /// <summary>
        /// Shows only the selection: every other piece is hidden, so a room can be worked on from inside.
        /// Again, or with nothing selected, it shows everything. The same hide as the Layers card's: not saved.
        /// </summary>
        public static void IsolateSelection()
        {
            SettleHand();
            if (Document == null)
            {
                return;
            }

            var others = Selected.Count == 0
                ? new List<int>()
                : Document.Pieces.Where(p => !Selected.Contains(p.Id)).Select(p => p.Id).ToList();

            // Already isolated (or nothing selected): bring back only what Isolate hid, not what was hidden by hand.
            if (others.Count == 0 || others.All(IsHidden))
            {
                if (Isolated.Count > 0)
                {
                    SetHidden(Isolated.ToList(), false);
                    Say($"Showed the {Count(Isolated.Count)} that were out of the way.");
                    Isolated.Clear();
                }
                else if (Selected.Count == 0)
                {
                    ShowAll();
                }

                return;
            }

            Isolated.Clear();
            foreach (var id in others)
            {
                if (!IsHidden(id))
                {
                    Isolated.Add(id);
                }
            }

            SetHidden(others, true);
            Say($"Showing only the selection ({Count(Selected.Count)}). Isolate again brings back the rest.");
        }

        /// <summary>Locks the selection, so a click or a box can no longer pick it.</summary>
        public static void LockSelection()
        {
            SettleHand();
            if (Selected.Count == 0)
            {
                return;
            }

            var count = Selected.Count;
            SetLocked(Selected, true);
            Say($"Locked {Count(count)}. Show all unlocks them.");
        }

        /// <summary>Shows every hidden piece and unlocks every locked one.</summary>
        public static void ShowAll()
        {
            if (Hidden.Count == 0 && Locked.Count == 0)
            {
                return;
            }

            Say($"Showed {Hidden.Count} and unlocked {Locked.Count}.");
            Hidden.Clear();
            Locked.Clear();
            Isolated.Clear();
            Version++;
        }

        /// <summary>Selects every piece of the same kind as the selected ones.</summary>
        public static void SelectSimilar()
        {
            var chosen = SelectedPieces();
            if (chosen.Count == 0 || Document == null)
            {
                return;
            }

            var kinds = new HashSet<string>(chosen.Select(p => p.PrefabName));
            var ids = Document.Pieces.Where(p => kinds.Contains(p.PrefabName)).Select(p => p.Id).ToList();
            Select(ids);
            Say($"Selected {Count(Selected.Count)} of the same kind.");
        }

        /// <summary>
        /// Lines the selected pieces up along <see cref="AlignAxis"/> by their boxes: all on the lowest edge
        /// (-1), the middle (0) or the highest edge (1) of the whole selection. One undo step.
        /// </summary>
        public static bool AlignSelection(int mode)
        {
            SettleHand();
            var pieces = SelectedPieces();
            if (pieces.Count < 2)
            {
                Say("Select two or more pieces to line them up.");
                return false;
            }

            var axis = AlignAxis;
            var boxes = pieces.Select(p => BoxOf(p)).ToArray();
            var low = boxes.Min(b => b.min[axis]);
            var high = boxes.Max(b => b.max[axis]);
            var target = mode < 0 ? low : mode > 0 ? high : (low + high) * 0.5f;

            var moves = new List<PieceMove>(pieces.Count);
            for (var i = 0; i < pieces.Count; i++)
            {
                var at = mode < 0 ? boxes[i].min[axis] : mode > 0 ? boxes[i].max[axis] : boxes[i].center[axis];
                var position = pieces[i].Position;
                position[axis] += target - at;
                moves.Add(new PieceMove { Id = pieces[i].Id, Position = position, Rotation = pieces[i].Rotation });
            }

            Document.SetPieces(moves);
            Say($"Lined up {Count(pieces.Count)} along {AlignAxisName}.");
            return true;
        }

        /// <summary>
        /// Mirrors the selection across the middle of its box: along x (axis 0) or z (axis 2). Each piece
        /// moves to its mirrored spot and turns the mirrored way. The pieces themselves are not flipped
        /// (the game has no mirrored pieces), so a symmetric piece looks exactly mirrored. One undo step.
        /// </summary>
        /// <summary>
        /// The turn a piece needs to stand as the mirror image of how it stands: the way it faces and its up are
        /// reflected across the plane. A piece cannot be flipped itself (the game has no negative scale), so it is
        /// taken as left-right symmetrical, which nearly every piece is: a mirrored wall facing +Z across the Z
        /// plane faces -Z, a stair that climbs toward +Z climbs toward -Z, and across the X plane they keep facing
        /// the same way but a turn of 30 degrees becomes -30.
        /// </summary>
        public static Quaternion Mirrored(Quaternion rotation, int axis)
        {
            var plane = Matrix4x4.Scale(axis == 0 ? new Vector3(-1f, 1f, 1f) : new Vector3(1f, 1f, -1f));
            var symmetry = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
            var turned = plane * Matrix4x4.Rotate(rotation) * symmetry;
            return Quaternion.LookRotation(turned.MultiplyVector(Vector3.forward), turned.MultiplyVector(Vector3.up));
        }

        public static bool MirrorSelection(int axis)
        {
            SettleHand();
            var pieces = SelectedPieces();
            if (pieces.Count == 0)
            {
                Say("Select something to mirror.");
                return false;
            }

            var middle = (BoxOf(pieces) ?? new Bounds()).center[axis];
            var moves = new List<PieceMove>(pieces.Count);
            foreach (var piece in pieces)
            {
                var position = piece.Position;
                position[axis] = (2f * middle) - position[axis];
                moves.Add(new PieceMove { Id = piece.Id, Position = position, Rotation = Clean(Mirrored(piece.Rotation, axis)) });
            }

            Document.SetPieces(moves);
            Say($"Mirrored {Count(pieces.Count)} along {(axis == 0 ? "X" : "Z")}.");
            return true;
        }

        /// <summary>Lowers or lifts the selection so its lowest point is at height 0. One undo step.</summary>
        public static bool DropToFloor()
        {
            SettleHand();
            var pieces = SelectedPieces();
            if (pieces.Count == 0 || Document == null)
            {
                Say("Select something to drop to the floor.");
                return false;
            }

            var low = BoxOf(pieces) ?? new Bounds();
            var by = -low.min.y;
            if (Mathf.Abs(by) < 1e-4f)
            {
                Say("It is on the floor already.");
                return false;
            }

            var moves = pieces.Select(p => new PieceMove { Id = p.Id, Position = p.Position + new Vector3(0f, by, 0f), Rotation = p.Rotation }).ToList();
            Document.SetPieces(moves);
            Say($"Moved {Count(pieces.Count)} {(by < 0f ? "down" : "up")} {Mathf.Abs(by):0.##} m, onto the floor.");
            return true;
        }

        /// <summary>
        /// A copy of the selection right next to it, one box width along the ground axis given (the
        /// camera's right, from the key). The copy is selected, so pressing again lays a row. One undo step.
        /// </summary>
        public static bool CopyInRow(Vector3 direction)
        {
            SettleHand();
            var pieces = SelectedPieces();
            if (pieces.Count == 0 || Document == null)
            {
                Say("Select something to copy in a row.");
                return false;
            }

            var box = BoxOf(pieces) ?? new Bounds();
            var alongX = Mathf.Abs(direction.x) >= Mathf.Abs(direction.z);
            var offset = alongX
                ? new Vector3(Mathf.Sign(direction.x) * box.size.x, 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(direction.z) * box.size.z);
            var copies = pieces.Select(p => new NewPiece
            {
                PrefabName = p.PrefabName,
                Position = p.Position + offset,
                Rotation = p.Rotation,
            }).ToList();
            Select(Document.AddPieces(copies));
            Say($"Copied {Count(pieces.Count)} {offset.magnitude:0.##} m over. Again lays the next one.");
            return true;
        }

        /// <summary>
        /// Spreads three or more pieces along <see cref="AlignAxis"/> so the gaps between their boxes are
        /// equal. The two outer pieces stay. One undo step.
        /// </summary>
        public static bool SpreadSelection()
        {
            SettleHand();
            var pieces = SelectedPieces();
            if (pieces.Count < 3)
            {
                Say("Select three or more pieces to spread them out.");
                return false;
            }

            var axis = AlignAxis;
            var order = pieces.Select(p => new { Piece = p, Box = BoxOf(p) })
                .OrderBy(x => x.Box.center[axis])
                .ToList();
            var first = order[0].Box.min[axis];
            var last = order[order.Count - 1].Box.max[axis];
            var filled = order.Sum(x => x.Box.size[axis]);
            var gap = (last - first - filled) / (order.Count - 1);

            var moves = new List<PieceMove>(order.Count);
            var edge = first;
            foreach (var x in order)
            {
                var position = x.Piece.Position;
                position[axis] += edge - x.Box.min[axis];
                moves.Add(new PieceMove { Id = x.Piece.Id, Position = position, Rotation = x.Piece.Rotation });
                edge += x.Box.size[axis] + gap;
            }

            Document.SetPieces(moves);
            Say($"Spread {Count(pieces.Count)} along {AlignAxisName}.");
            return true;
        }

        public static void DeleteSelection()
        {
            if (Document == null || Selected.Count == 0)
            {
                return;
            }

            var count = Selected.Count;
            Document.RemovePieces(new List<int>(Selected));
            Selected.Clear();
            CancelMode();
            Say($"Deleted {Count(count)}. Ctrl+Z brings {(count == 1 ? "it" : "them")} back.");
            Version++;
        }

        /// <summary>The selected pieces, in the document's own order.</summary>
        public static List<DocPiece> SelectedPieces()
        {
            var pieces = new List<DocPiece>(Selected.Count);
            if (Document == null)
            {
                return pieces;
            }

            foreach (var piece in Document.Pieces)
            {
                if (Selected.Contains(piece.Id))
                {
                    pieces.Add(piece);
                }
            }

            return pieces;
        }

        // ---------- what is in hand ----------

        /// <summary>A piece from the palette goes in hand, and stays there after each drop.</summary>
        public static bool StartAdd(PieceEntry entry)
        {
            if (Document == null || entry == null)
            {
                return false;
            }

            Held = entry;
            Moving = MovingSet.One(entry);
            Action = PlaceAction.Add;
            Mode = EditMode.Place;
            Steps = PlaceSteps;
            Manual = -1;
            Aimed = null;
            Version++;
            return true;
        }

        /// <summary>The selection leaves the scene and follows the aim until it is dropped.</summary>
        public static bool StartMove()
        {
            var pieces = SelectedPieces();
            if (pieces.Count == 0)
            {
                return false;
            }

            MovingIds.Clear();
            foreach (var piece in pieces)
            {
                MovingIds.Add(piece.Id);
            }

            Held = null;
            Moving = MovingSet.Of(MovingPieces(pieces));
            Action = PlaceAction.Move;
            Mode = EditMode.Place;
            Steps = 0;
            Manual = -1;
            Aimed = null;
            _index = null;
            Version++;
            return true;
        }

        /// <summary>Copies of the selection go in hand, and stay there so more can be dropped.</summary>
        public static bool StartDuplicate()
        {
            var pieces = SelectedPieces();
            if (pieces.Count == 0)
            {
                return false;
            }

            MovingIds.Clear();
            Held = null;
            Moving = MovingSet.Of(MovingPieces(pieces));
            Action = PlaceAction.Duplicate;
            Mode = EditMode.Place;
            Steps = 0;
            Manual = -1;
            Aimed = null;
            Version++;
            return true;
        }

        public static void CancelMode()
        {
            if (Mode == EditMode.Idle)
            {
                return;
            }

            Mode = EditMode.Idle;
            Moving = null;
            Held = null;
            MovingIds.Clear();
            Aimed = null;
            _index = null;
            Version++;
        }

        public static void SetPlaceSteps(int steps)
        {
            if (Mode != EditMode.Place)
            {
                return;
            }

            Steps = steps;
            if (Action == PlaceAction.Add)
            {
                PlaceSteps = steps;
            }

            Version++;
        }

        /// <summary>Walks the chosen snap point, the way Q and E do in the game.</summary>
        public static void SetManualSnap(int manual)
        {
            if (Mode != EditMode.Place || Moving == null)
            {
                return;
            }

            Manual = Placer.WrapManual(manual, Moving.Snaps.Count);
            Version++;
        }

        /// <summary>The name of the chosen snap point, or null while it is automatic.</summary>
        public static string ManualName()
        {
            if (Moving == null || Manual < 0 || Manual >= Moving.Snaps.Count)
            {
                return null;
            }

            return Moving.Snaps[Manual].Name;
        }

        /// <summary>
        /// Runs the placing rule for one ray, in the editor scene's own space, and remembers the
        /// answer for the ghost and the HUD. False when the ray met nothing at all.
        /// </summary>
        public static bool Aim(Vector3 origin, Vector3 dir, out PlaceResult result)
        {
            result = null;
            if (Mode != EditMode.Place || Moving == null || Moving.Count == 0)
            {
                Aimed = null;
                return false;
            }

            var options = new PlaceOptions
            {
                Steps = Steps,
                Manual = Manual,
                Snapping = Snapping,
                SearchAll = Moving.Count > SearchAllFrom,
                Assist = true,
                AngleStep = AngleStep,
                Grid = GridStep,
                NoSnapPoints = EditorConfig.SnapPoints != null && !EditorConfig.SnapPoints.Value,
            };

            var hit = Placer.Place(Index, Moving, origin, dir.normalized, options, out result);
            if (hit)
            {
                Weigh(result);
            }

            Aimed = hit ? result : null;
            return hit;
        }

        /// <summary>
        /// The support each piece in hand would have where it lands, and whether any would fall.
        /// Worked out again only when the spot, what is in hand or the scene changed.
        /// </summary>
        private static void Weigh(PlaceResult result)
        {
            var stability = Stability;
            if (_weighed != null && ReferenceEquals(_weighedMap, stability) && ReferenceEquals(_weighedSet, Moving)
                && _weighed.Pos == result.Pos && _weighed.Rot == result.Rot)
            {
                result.Support = _weighed.Support;
                result.Falls = _weighed.Falls;
                result.WouldFall = _weighed.WouldFall;
                return;
            }

            var count = result.World.Length;
            result.Support = new float[count];
            result.Falls = new bool[count];
            result.WouldFall = Support.Evaluate(stability, result.World, result.Support, result.Falls);
            _weighed = result;
            _weighedMap = stability;
            _weighedSet = Moving;
        }

        /// <summary>What the status line and the refused click say when something would fall.</summary>
        public static string FallText(PlaceResult result)
        {
            var falling = 0;
            if (result != null && result.Falls != null)
            {
                foreach (var falls in result.Falls)
                {
                    falling += falls ? 1 : 0;
                }
            }

            return falling <= 1 && (result == null || result.World.Length <= 1)
                ? "it would fall down, nothing holds it up there"
                : $"{Count(falling)} would fall down, nothing holds {(falling == 1 ? "it" : "them")} up there";
        }

        /// <summary>Nothing is in hand, so there is nowhere for it to land.</summary>
        public static void ClearAim()
        {
            Aimed = null;
        }

        /// <summary>A click while something is in hand: add it, drop the moved pieces, or drop the copies.</summary>
        public static bool CommitPlacement(PlaceResult result)
        {
            if (Document == null || Mode != EditMode.Place || Moving == null || result == null || result.Duplicate)
            {
                return false;
            }

            // The game would break it at once and the materials would be gone.
            if (result.WouldFall)
            {
                var text = FallText(result);
                Say(char.ToUpperInvariant(text[0]) + text.Substring(1) + ".");
                return false;
            }

            var world = result.World;
            switch (Action)
            {
                case PlaceAction.Add:
                {
                    var id = Document.AddPiece(world[0].Prefab, world[0].Pos, Clean(world[0].Rot));
                    Select(id);
                    break;
                }

                case PlaceAction.Move:
                {
                    var moves = new List<PieceMove>(world.Length);
                    foreach (var placed in world)
                    {
                        moves.Add(new PieceMove { Id = placed.Id, Position = placed.Pos, Rotation = Clean(placed.Rot) });
                    }

                    Document.SetPieces(moves);
                    CancelMode();
                    break;
                }

                default:
                {
                    var copies = new List<NewPiece>(world.Length);
                    foreach (var placed in world)
                    {
                        copies.Add(new NewPiece
                        {
                            PrefabName = placed.Prefab,
                            Position = placed.Pos,
                            Rotation = Clean(placed.Rot),
                        });
                    }

                    var made = Document.AddPieces(copies);
                    Select(made);
                    break;
                }
            }

            Aimed = null;
            Version++;
            return true;
        }

        // ---------- editing the selection ----------

        /// <summary>Arrow keys and PageUp/PageDown. A run of presses is one undo step.</summary>
        public static void Nudge(Vector3 delta)
        {
            var pieces = SelectedPieces();
            if (pieces.Count == 0 || delta == Vector3.zero)
            {
                return;
            }

            var moves = new List<PieceMove>(pieces.Count);
            foreach (var piece in pieces)
            {
                moves.Add(new PieceMove { Id = piece.Id, Position = piece.Position + delta, Rotation = piece.Rotation });
            }

            Document.SetPieces(moves, "nudge");
        }

        /// <summary>R: turns the selection 22.5 degrees, a group around the bottom centre of its box.</summary>
        public static void RotateSelection(int direction)
        {
            var pieces = SelectedPieces();
            if (pieces.Count == 0)
            {
                return;
            }

            foreach (var piece in pieces)
            {
                var entry = PieceCatalog.Find(piece.PrefabName);
                if (entry != null && !entry.CanRotate)
                {
                    Say("This piece can't turn in the game.");
                    return;
                }
            }

            var turn = Quaternion.Euler(0f, AngleStep * Mathf.Sign(direction), 0f);
            var centre = pieces.Count == 1 ? pieces[0].Position : BottomCentre(pieces);
            var moves = new List<PieceMove>(pieces.Count);
            foreach (var piece in pieces)
            {
                moves.Add(new PieceMove
                {
                    Id = piece.Id,
                    Position = centre + (turn * (piece.Position - centre)),
                    Rotation = Clean(turn * piece.Rotation),
                });
            }

            Document.SetPieces(moves);
        }

        /// <summary>The yaw field: turns one piece to a free angle, keeping any tilt it has.</summary>
        public static void SetYaw(int id, float degrees)
        {
            var piece = Document != null ? Document.Find(id) : null;
            if (piece == null || float.IsNaN(degrees) || float.IsInfinity(degrees))
            {
                return;
            }

            // The game does not turn this piece, so neither does the box.
            var entry = PieceCatalog.Find(piece.PrefabName);
            if (entry != null && !entry.CanRotate)
            {
                Say("This piece cannot turn in the game.");
                return;
            }

            degrees = Mathf.Repeat(degrees, 360f);
            var level = Vector3.Angle(piece.Rotation * Vector3.up, Vector3.up) < 0.01f;
            var rotation = level
                ? Quaternion.Euler(0f, degrees, 0f)
                : Quaternion.Euler(0f, degrees - YawOf(piece.Rotation), 0f) * piece.Rotation;
            Document.SetPieces(
                new List<PieceMove> { new PieceMove { Id = id, Position = piece.Position, Rotation = Clean(rotation) } },
                "yaw:" + id);
        }

        public static void SetPosition(int id, Vector3 position)
        {
            var piece = Document != null ? Document.Find(id) : null;
            if (piece == null)
            {
                return;
            }

            position = Limited(position);
            Document.SetPieces(
                new List<PieceMove> { new PieceMove { Id = id, Position = position, Rotation = piece.Rotation } },
                "pos:" + id);
        }

        /// <summary>No piece is placed farther than this from the origin by typing: past it the box, the camera and the game misbehave.</summary>
        public const float FarthestMetres = 10000f;

        private static Vector3 Limited(Vector3 position)
        {
            var clamped = new Vector3(
                Mathf.Clamp(position.x, -FarthestMetres, FarthestMetres),
                Mathf.Clamp(position.y, -FarthestMetres, FarthestMetres),
                Mathf.Clamp(position.z, -FarthestMetres, FarthestMetres));
            if (clamped != position)
            {
                Say($"A piece stays within {FarthestMetres:0} m of the origin.");
            }

            return clamped;
        }

        /// <summary>
        /// The group's bottom centre, one axis, typed: every selected piece moves by the difference. One
        /// undo step for a run of typing.
        /// </summary>
        public static void MoveSelectionTo(int axis, float value)
        {
            var pieces = SelectedPieces();
            if (pieces.Count == 0 || Document == null || float.IsNaN(value) || float.IsInfinity(value))
            {
                return;
            }

            SettleHand();
            var by = Vector3.zero;
            by[axis] = Mathf.Clamp(value, -FarthestMetres, FarthestMetres) - BottomCentre(pieces)[axis];
            var moves = pieces.Select(p => new PieceMove { Id = p.Id, Position = p.Position + by, Rotation = p.Rotation }).ToList();
            Document.SetPieces(moves, "group:" + axis);
        }

        /// <summary>
        /// An edit made while pieces are being moved by hand would be overwritten by the drop, so the hand
        /// is put away first: the moved pieces stand where they were.
        /// </summary>
        public static void SettleHand()
        {
            if (Mode == EditMode.Place && Action == PlaceAction.Move)
            {
                CancelMode();
            }
        }

        /// <summary>Moves the origin to the bottom centre of the blueprint. The pieces shift the other way.</summary>
        public static bool CenterOrigin()
        {
            if (Document == null || Document.HasSections || Document.Pieces.Count == 0)
            {
                return false;
            }

            var pieces = new List<DocPiece>(Document.Pieces);
            var shift = BottomCentre(pieces);
            if (shift.magnitude < 1e-4f)
            {
                Say("The origin is already at the bottom centre.");
                return false;
            }

            var moves = new List<PieceMove>(pieces.Count);
            foreach (var piece in pieces)
            {
                moves.Add(new PieceMove { Id = piece.Id, Position = piece.Position - shift, Rotation = piece.Rotation });
            }

            Document.SetPieces(moves);
            Say($"Moved the origin by {shift.x:0.00}, {shift.y:0.00}, {shift.z:0.00} m.");
            return true;
        }

        // ---------- history ----------

        public static bool Undo()
        {
            CancelMode();
            if (Document == null)
            {
                return false;
            }

            var before = Placements();
            if (!Document.Undo())
            {
                return false;
            }

            Prune();
            SelectWhatChanged(before);
            return true;
        }

        public static bool Redo()
        {
            CancelMode();
            if (Document == null)
            {
                return false;
            }

            var before = Placements();
            if (!Document.Redo())
            {
                return false;
            }

            Prune();
            SelectWhatChanged(before);
            return true;
        }

        /// <summary>Every piece's place, to tell afterwards which ones an undo or a redo touched.</summary>
        private static Dictionary<int, KeyValuePair<Vector3, Quaternion>> Placements()
        {
            var all = new Dictionary<int, KeyValuePair<Vector3, Quaternion>>(Document.Pieces.Count);
            foreach (var piece in Document.Pieces)
            {
                all[piece.Id] = new KeyValuePair<Vector3, Quaternion>(piece.Position, piece.Rotation);
            }

            return all;
        }

        /// <summary>After an undo or a redo the pieces it brought back or moved are the selection, so the step can be seen.</summary>
        private static void SelectWhatChanged(Dictionary<int, KeyValuePair<Vector3, Quaternion>> before)
        {
            var changed = new List<int>();
            foreach (var piece in Document.Pieces)
            {
                if (!before.TryGetValue(piece.Id, out var was)
                    || (was.Key - piece.Position).sqrMagnitude > 1e-10f
                    || Quaternion.Angle(was.Value, piece.Rotation) > 1e-3f)
                {
                    changed.Add(piece.Id);
                }
            }

            if (changed.Count > 0)
            {
                Select(changed);
            }
        }

        // ---------- boxes ----------

        /// <summary>The box around one piece, in the editor scene's space.</summary>
        public static Bounds BoxOf(DocPiece piece)
        {
            var entry = PieceCatalog.Find(piece.PrefabName);
            if (entry == null || entry.Bounds.size == Vector3.zero)
            {
                return new Bounds(piece.Position, Vector3.one * 0.5f);
            }

            var centre = piece.Position + (piece.Rotation * entry.Bounds.center);
            var e = entry.Bounds.extents;
            var extent =
                Abs(piece.Rotation * new Vector3(e.x, 0f, 0f))
                + Abs(piece.Rotation * new Vector3(0f, e.y, 0f))
                + Abs(piece.Rotation * new Vector3(0f, 0f, e.z));
            return new Bounds(centre, extent * 2f);
        }

        /// <summary>The box around a list of pieces, or null when it is empty.</summary>
        public static Bounds? BoxOf(IList<DocPiece> pieces)
        {
            if (pieces == null || pieces.Count == 0)
            {
                return null;
            }

            var box = BoxOf(pieces[0]);
            for (var i = 1; i < pieces.Count; i++)
            {
                box.Encapsulate(BoxOf(pieces[i]));
            }

            return box;
        }

        /// <summary>
        /// The bottom centre of a set of pieces: the middle of their box in x and z, the lowest
        /// point in y. Center origin and the world capture both put the origin here, so a captured
        /// blueprint is centred the way the button would centre it.
        /// </summary>
        public static Vector3 BottomCentre(IList<DocPiece> pieces)
        {
            var box = BoxOf(pieces) ?? new Bounds();
            return new Vector3(box.center.x, box.min.y, box.center.z);
        }

        // ---------- messages ----------

        public static void Say(string text)
        {
            Message = text;
            MessageAt = Time.unscaledTime;
            ValheimTomrerPlugin.Log.LogInfo("editor: " + text);
        }

        public static void ClearMessage()
        {
            Message = null;
        }

        // ---------- inside ----------

        /// <summary>What the placing rule aims at: every piece but the ones in hand.</summary>
        private static IEnumerable<ScenePiece> StandingPieces()
        {
            if (Document == null)
            {
                yield break;
            }

            var moving = Mode == EditMode.Place && Action == PlaceAction.Move;
            foreach (var piece in Document.Pieces)
            {
                if (moving && MovingIds.Contains(piece.Id))
                {
                    continue;
                }

                yield return new ScenePiece
                {
                    Id = piece.Id,
                    Prefab = piece.PrefabName,
                    Pos = piece.Position,
                    Rot = piece.Rotation,
                    Entry = PieceCatalog.Find(piece.PrefabName),
                };
            }
        }

        private static List<MovingPiece> MovingPieces(IList<DocPiece> pieces)
        {
            var moving = new List<MovingPiece>(pieces.Count);
            foreach (var piece in pieces)
            {
                moving.Add(new MovingPiece
                {
                    Id = piece.Id,
                    Prefab = piece.PrefabName,
                    Pos = piece.Position,
                    Rot = piece.Rotation,
                    Entry = PieceCatalog.Find(piece.PrefabName),
                });
            }

            return moving;
        }

        /// <summary>Drops from the selection the ids the document no longer has.</summary>
        private static void Prune()
        {
            var gone = new List<int>();
            foreach (var id in Selected)
            {
                if (Document.Find(id) == null)
                {
                    gone.Add(id);
                }
            }

            foreach (var id in gone)
            {
                Selected.Remove(id);
            }

            foreach (var id in GroupOf.Keys.ToList())
            {
                if (Document.Find(id) == null)
                {
                    Leave(id);
                }
            }

            Version++;
        }

        private static float YawOf(Quaternion q)
        {
            var forward = q * Vector3.forward;
            if (Mathf.Abs(forward.x) < 1e-6f && Mathf.Abs(forward.z) < 1e-6f)
            {
                // Lying flat: the same baseline the Design tab shows.
                var right = q * Vector3.right;
                return Mathf.Atan2(-right.z, right.x) * Mathf.Rad2Deg;
            }

            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        private static Quaternion Clean(Quaternion q)
        {
            return q.normalized;
        }

        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }

        private static string Count(int n)
        {
            return n == 1 ? "1 piece" : n + " pieces";
        }
    }
}
