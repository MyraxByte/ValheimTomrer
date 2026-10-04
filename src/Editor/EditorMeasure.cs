using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ValheimTomrer.Editor.Doc;

namespace ValheimTomrer.Editor
{
    /// <summary>One free stretch between the selection and its nearest neighbour along an axis.</summary>
    internal struct Gap
    {
        /// <summary>0 x, 1 y, 2 z.</summary>
        public int Axis;

        /// <summary>The empty distance between the two faces, in metres. Always above zero.</summary>
        public float Distance;

        /// <summary>The middle of the stretch's start on the selection's face, and its end on the neighbour's.</summary>
        public Vector3 From;

        public Vector3 To;

        /// <summary>+1: the neighbour is on the high side of the selection, -1: on the low side.</summary>
        public int Side;
    }

    /// <summary>
    /// Measuring: how far the selection is from the pieces beside it, and closing that distance. The
    /// numbers come from the pieces' boxes (<see cref="EditorState.BoxOf(DocPiece)"/>), so they are
    /// the same ones the selection's size shows.
    /// </summary>
    internal static class EditorMeasure
    {
        /// <summary>Gaps smaller than this are touching, not a gap.</summary>
        private const float Touching = 0.005f;

        /// <summary>
        /// The nearest neighbour on each side of the box along each axis, up to six. A piece counts as a
        /// neighbour on an axis only when it overlaps the box on the other two, so it is really beside it.
        /// </summary>
        public static List<Gap> Around(Bounds box, IReadOnlyCollection<int> except)
        {
            var best = new Gap?[6];
            var document = EditorState.Document;
            if (document == null)
            {
                return new List<Gap>();
            }

            foreach (var piece in document.Pieces)
            {
                if (except.Contains(piece.Id) || EditorState.IsHidden(piece.Id))
                {
                    continue;
                }

                var other = EditorState.BoxOf(piece);
                for (var axis = 0; axis < 3; axis++)
                {
                    var a = (axis + 1) % 3;
                    var b = (axis + 2) % 3;
                    var lowA = Mathf.Max(box.min[a], other.min[a]);
                    var highA = Mathf.Min(box.max[a], other.max[a]);
                    var lowB = Mathf.Max(box.min[b], other.min[b]);
                    var highB = Mathf.Min(box.max[b], other.max[b]);
                    if (highA - lowA <= Touching || highB - lowB <= Touching)
                    {
                        continue;
                    }

                    var side = 0;
                    var distance = 0f;
                    if (other.min[axis] >= box.max[axis])
                    {
                        side = 1;
                        distance = other.min[axis] - box.max[axis];
                    }
                    else if (other.max[axis] <= box.min[axis])
                    {
                        side = -1;
                        distance = box.min[axis] - other.max[axis];
                    }

                    if (side == 0 || distance <= Touching)
                    {
                        continue;
                    }

                    var slot = (axis * 2) + (side > 0 ? 1 : 0);
                    if (best[slot].HasValue && best[slot].Value.Distance <= distance)
                    {
                        continue;
                    }

                    var from = Vector3.zero;
                    from[axis] = side > 0 ? box.max[axis] : box.min[axis];
                    from[a] = (lowA + highA) * 0.5f;
                    from[b] = (lowB + highB) * 0.5f;
                    var to = from;
                    to[axis] = side > 0 ? other.min[axis] : other.max[axis];
                    best[slot] = new Gap { Axis = axis, Distance = distance, From = from, To = to, Side = side };
                }
            }

            return best.Where(g => g.HasValue).Select(g => g.Value).ToList();
        }

        /// <summary>
        /// Moves the selection along the align axis until it touches the nearest piece beside it, on
        /// whichever side that is. One undo step.
        /// </summary>
        public static bool CloseGap()
        {
            EditorState.SettleHand();
            var document = EditorState.Document;
            var pieces = EditorState.SelectedPieces();
            if (document == null || pieces.Count == 0)
            {
                EditorState.Say("Select something to close the gap beside it.");
                return false;
            }

            var box = EditorState.BoxOf(pieces) ?? new Bounds();
            var axis = EditorState.AlignAxis;
            var gaps = Around(box, EditorState.Selection).Where(g => g.Axis == axis).OrderBy(g => g.Distance).ToList();
            if (gaps.Count == 0)
            {
                EditorState.Say($"Nothing is beside the selection along {EditorState.AlignAxisName} with a gap to close.");
                return false;
            }

            var gap = gaps[0];
            var by = Vector3.zero;
            by[axis] = gap.Side * gap.Distance;
            var moves = pieces.Select(p => new PieceMove { Id = p.Id, Position = p.Position + by, Rotation = p.Rotation }).ToList();
            document.SetPieces(moves, "gap");
            EditorState.Say($"Closed a {gap.Distance:0.##} m gap along {EditorState.AlignAxisName}.");
            return true;
        }
    }
}
