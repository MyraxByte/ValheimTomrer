using System;
using System.IO;
using System.Linq;
using UnityEngine;
using ValheimTomrer.Blueprints;
using ValheimTomrer.Editor.Doc;
using ValheimTomrer.Editor.Ui;

namespace ValheimTomrer.Editor
{
    /// <summary>
    /// The verbs behind the top bar, the dialogs and the shortcuts: new, open, save, save as,
    /// delete a file, build it in the world, centre the origin and the two view switches. Every
    /// one of them reports what happened through a toast, so a button, a key and the test all
    /// take the same path.
    /// </summary>
    internal static class EditorCommands
    {
        /// <summary>A busy chip shows for at least this long, or it would flash by unread.</summary>
        private const float MinBusySeconds = 0.35f;

        private static bool _working;
        private static float _busyUntil;
        private static bool _discardOk;

        /// <summary>What the editor is doing right now, or null.</summary>
        public static string Busy { get; private set; }

        /// <summary>Starts an empty blueprint, asking first when the open one has changes.</summary>
        public static void NewBlueprint()
        {
            if (AskFirst("Start a new blueprint?", NewBlueprint))
            {
                return;
            }

            EditorSession.Replace(DocumentStore.New());
            Toasts.Info("New blueprint.");
        }

        /// <summary>Shows the list of kits and files: the Blueprints button.</summary>
        public static void OpenDialog()
        {
            Dialogs.Open();
        }

        /// <summary>Asks for a name and keeps the selected pieces as a small blueprint of their own.</summary>
        public static void SaveSelectionDialog()
        {
            if (EditorState.SelectionCount == 0)
            {
                Toasts.Info("Select something to keep as a blueprint first.");
                return;
            }

            Dialogs.SaveAs("New part", SaveSelectionAs, "Save the selection as");
        }

        /// <summary>
        /// Writes the selection as a blueprint of its own, in the player's folder, its pieces placed round their
        /// bottom centre so it lands where it is aimed when it is inserted.
        /// </summary>
        public static bool SaveSelectionAs(string name, bool overwrite)
        {
            var pieces = EditorState.SelectedPieces();
            if (pieces.Count == 0)
            {
                return false;
            }

            var centre = EditorState.BottomCentre(pieces);
            var part = DocumentStore.New(name);
            part.AddPieces(pieces.Select(p => new NewPiece
            {
                PrefabName = p.PrefabName,
                Position = p.Position - centre,
                Rotation = p.Rotation,
            }).ToList());
            Begin("Saving");
            var ok = DocumentStore.SaveAs(part, name, overwrite, out var error);
            Done();
            if (!ok)
            {
                Toasts.Error(error);
                return false;
            }

            Dialogs.Close();
            Toasts.Ok($"Kept {pieces.Count} piece{(pieces.Count == 1 ? "" : "s")} as {Path.GetFileName(part.SourcePath)}. Ctrl+I puts it in any blueprint.");
            return true;
        }

        /// <summary>The list of blueprints, to pick one whose pieces go in hand.</summary>
        public static void InsertDialog()
        {
            if (EditorState.Document == null)
            {
                return;
            }

            Dialogs.Open(-1, true);
        }

        /// <summary>A blueprint of the list goes in hand, a piece-by-piece copy that is dropped where it is aimed.</summary>
        public static void InsertEntry(BlueprintEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            Begin("Reading");
            BlueprintDocument document;
            string error;
            var ok = entry.IsKit
                ? DocumentStore.OpenKit(entry, out document, out error)
                : DocumentStore.Open(entry.Path, out document, out error);
            Done();
            if (!ok)
            {
                Toasts.Error(error);
                return;
            }

            Dialogs.Close();
            if (EditorState.StartPasteFrom(document.Pieces.ToList()))
            {
                Toasts.Ok($"{document.Name}: {document.Pieces.Count} pieces in hand. Click to drop, Esc to stop.");
            }
        }

        /// <summary>Opens one row of that list, asking first when the open blueprint has changes.</summary>
        public static void OpenEntry(BlueprintEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            if (AskFirst($"Open {entry.Name}?", () => OpenEntry(entry)))
            {
                return;
            }

            Begin("Opening");
            BlueprintDocument document;
            string error;
            var ok = entry.IsKit
                ? DocumentStore.OpenKit(entry, out document, out error)
                : DocumentStore.Open(entry.Path, out document, out error);
            Done();
            if (!ok)
            {
                Toasts.Error(error);
                return;
            }

            Dialogs.Close();
            EditorSession.Replace(document);
            Toasts.Ok($"Opened {document.Name}, {document.Pieces.Count} pieces.");
        }

        /// <summary>
        /// Deletes one of the player's files. A blueprint open in the editor from that file stays
        /// open, as not saved. Kits live in the DLL and cannot be deleted.
        /// </summary>
        public static bool DeleteEntry(BlueprintEntry entry)
        {
            if (entry == null || entry.IsKit || string.IsNullOrEmpty(entry.Path))
            {
                return false;
            }

            var file = Path.GetFileName(entry.Path);
            if (!File.Exists(entry.Path))
            {
                Toasts.Error($"{file} is not there any more.");
                return false;
            }

            Begin("Deleting");
            var ok = DocumentStore.Delete(entry.Path, out var error);
            Done();
            if (!ok)
            {
                Toasts.Error(error);
                return false;
            }

            var document = EditorState.Document;
            if (document != null
                && string.Equals(document.SourcePath, entry.Path, System.StringComparison.OrdinalIgnoreCase))
            {
                document.LoseFile();
            }

            ValheimTomrerPlugin.Log.LogInfo($"editor deleted blueprint file {entry.Path}");
            Toasts.Ok($"Deleted {file}.");
            return true;
        }

        /// <summary>
        /// A blueprint from outside the window takes the open one's place: the one in the build
        /// tool's hand, or a world capture. Asks first when the open one has changes.
        /// <paramref name="taken"/> runs once it is the open one, so a dialog that follows it
        /// (the capture's Save as) cannot close the question and act on the old blueprint.
        /// </summary>
        public static void Take(BlueprintDocument document, ResolvedBlueprint blueprint, System.Action taken = null)
        {
            if (document == null)
            {
                return;
            }

            var name = string.IsNullOrEmpty(document.Name) ? "this blueprint" : document.Name;
            if (AskFirst($"Open {name}?", () => Take(document, blueprint, taken)))
            {
                return;
            }

            EditorSession.Replace(document, blueprint);
            taken?.Invoke();
        }

        /// <summary>
        /// Writes the blueprint back over its own file. A kit, a read-only file or a blueprint
        /// that was never saved goes to Save as instead.
        /// </summary>
        public static bool Save()
        {
            var document = EditorState.Document;
            if (document == null)
            {
                return false;
            }

            if (document.ReadOnly || string.IsNullOrEmpty(document.SourcePath))
            {
                Dialogs.SaveAs(document.Name);
                return false;
            }

            Begin("Saving");
            var ok = DocumentStore.Save(document, out var error);
            Done();
            if (!ok)
            {
                Toasts.Error(error);
                return false;
            }

            Toasts.Ok($"Saved {Path.GetFileName(document.SourcePath)}.");
            return true;
        }

        /// <summary>Writes the blueprint under a name of its own, in the player's folder.</summary>
        public static bool SaveAs(string name, bool overwrite)
        {
            var document = EditorState.Document;
            if (document == null)
            {
                return false;
            }

            Begin("Saving");
            var ok = DocumentStore.SaveAs(document, name, overwrite, out var error);
            Done();
            if (!ok)
            {
                Toasts.Error(error);
                return false;
            }

            Dialogs.Close();
            Toasts.Ok($"Saved {Path.GetFileName(document.SourcePath)}.");
            return true;
        }

        /// <summary>
        /// Hands the open blueprint to the build tool: saves it when it has changes, closes the
        /// window, and leaves the vanilla preview in hand, ready for a click. It equips nothing,
        /// so the build tool has to be out already.
        /// </summary>
        public static bool BuildThis()
        {
            var document = EditorState.Document;
            if (document == null)
            {
                return false;
            }

            if (document.Pieces.Count == 0)
            {
                Toasts.Error("A blueprint needs at least one piece.");
                return false;
            }

            var player = Player.m_localPlayer;
            if (player == null)
            {
                Toasts.Error("No player to build with.");
                return false;
            }

            // No hammer out means no build tool to put the preview in. Say so, do not equip one.
            if (!player.InPlaceMode())
            {
                Toasts.Error("Take the hammer out first, then press Build this again.");
                return false;
            }

            // Save first, so what stands in the world is what the file holds. A blueprint with no
            // file of its own opens the Save as dialog instead, and the next press goes through.
            if (document.Dirty && !Save())
            {
                return false;
            }

            if (!ResolvedBlueprint.TryResolve(document.ToBlueprint(), out var resolved, out var error))
            {
                Toasts.Error(error);
                return false;
            }

            var locked = BlueprintRules.UnavailablePieces(player, resolved);
            if (locked.Count > 0)
            {
                Toasts.Error("Not unlocked yet: "
                    + string.Join(", ", locked.Select(p => Localization.instance.Localize(p.m_name))));
                return false;
            }

            EditorSession.Close();
            BlueprintMode.Select(player, resolved);
            player.Message(MessageHud.MessageType.Center, $"{resolved.Name}: click to build");
            ValheimTomrerPlugin.Log.LogInfo(
                $"editor handed '{resolved.Name}' ({resolved.Parts.Count} pieces) to the build tool");
            return true;
        }

        /// <summary>Moves the origin to the bottom centre of the blueprint.</summary>
        public static void CenterOrigin()
        {
            var document = EditorState.Document;
            if (document == null)
            {
                return;
            }

            if (document.HasSections)
            {
                Toasts.Error("This file has snap point or terrain sections that would have to move too.");
                return;
            }

            if (!EditorState.CenterOrigin())
            {
                Toasts.Info("The origin is already at the bottom centre.");
                return;
            }

            Toasts.Ok("The origin is now the bottom centre.");
        }

        /// <summary>Draws the pieces as boxes instead of models, which is faster on a big blueprint.</summary>
        public static void ToggleBoxes()
        {
            EditorState.PieceBoxesOn = !EditorState.PieceBoxesOn;
            Remember(EditorConfig.Boxes, EditorState.PieceBoxesOn);
            Toasts.Info(EditorState.PieceBoxesOn ? "Pieces as boxes." : "Pieces as models.");
        }

        /// <summary>Snapping stays on either way: this only shows where the snap points are.</summary>
        public static void ToggleSnapDots()
        {
            EditorState.SnapDotsOn = !EditorState.SnapDotsOn;
            Remember(EditorConfig.SnapDots, EditorState.SnapDotsOn);
            Toasts.Info(EditorState.SnapDotsOn ? "Snap dots on." : "Snap dots off.");
        }

        /// <summary>The size of the selection along its edges, and the gaps to the pieces beside it.</summary>
        public static void ToggleDimensions()
        {
            EditorState.DimensionsOn = !EditorState.DimensionsOn;
            Toasts.Info(EditorState.DimensionsOn ? "Sizes and gaps shown." : "Sizes and gaps hidden.");
        }

        /// <summary>Colours every piece by its support, so the weak places show without pointing at each one.</summary>
        public static void ToggleSupportColours()
        {
            EditorState.SupportColoursOn = !EditorState.SupportColoursOn;
            Toasts.Info(EditorState.SupportColoursOn
                ? "Support colours on: green is held well, red is about to break."
                : "Support colours off.");
        }

        /// <summary>The next light for the 3D view: morning, day, evening, night, round.</summary>
        public static void CycleTimeOfDay()
        {
            if (EditorConfig.TimeOfDay == null)
            {
                return;
            }

            var next = (View.TimeOfDay)(((int)EditorConfig.TimeOfDay.Value + 1) % 4);
            EditorConfig.TimeOfDay.Value = next;
            Toasts.Info(next + " light.");
        }

        private static readonly float[] Grids = { 0f, 0.25f, 0.5f, 1f, 2f, 4f };
        private static readonly float[] Angles = { 22.5f, 45f, 90f, 5f, 15f };

        /// <summary>The next placing grid: off, 0.25, 0.5, 1, 2, 4 m, off.</summary>
        public static void CycleGrid()
        {
            var now = EditorState.GridStep;
            var next = Grids[(Array.FindIndex(Grids, g => Mathf.Approximately(g, now)) + 1) % Grids.Length];
            EditorConfig.GridStep.Value = next;
            Toasts.Info(next > 0f ? $"Grid {next:0.##} m: free pieces land on it, arrows move this far." : "Grid off.");
        }

        /// <summary>The next turn step: 22.5 (the game's), 45, 90, 5, 15 degrees.</summary>
        public static void CycleAngle()
        {
            var now = EditorState.AngleStep;
            var next = Angles[(Array.FindIndex(Angles, a => Mathf.Approximately(a, now)) + 1) % Angles.Length];
            EditorConfig.AngleStep.Value = next;
            Toasts.Info($"Turn {next:0.##} degrees a step.");
        }

        /// <summary>Snap points on or off. The grid, when on, still works.</summary>
        public static void ToggleSnapPoints()
        {
            var on = !EditorConfig.SnapPoints.Value;
            EditorConfig.SnapPoints.Value = on;
            Toasts.Info(on ? "Snap points on." : "Snap points off: pieces go where you aim" + (EditorState.GridStep > 0f ? ", on the grid." : "."));
        }

        /// <summary>A view switch is kept, so the editor opens the way it was left.</summary>
        private static void Remember(BepInEx.Configuration.ConfigEntry<bool> entry, bool value)
        {
            if (entry != null && entry.Value != value)
            {
                entry.Value = value;
            }
        }

        public static void Help()
        {
            Dialogs.Help();
        }

        /// <summary>Lets the busy chip fade after its shortest showing.</summary>
        public static void Tick()
        {
            if (!_working && Busy != null && Time.unscaledTime >= _busyUntil)
            {
                Busy = null;
            }
        }

        /// <summary>Forgets the busy chip when the editor closes. A question that is up stays up.</summary>
        public static void Reset()
        {
            _working = false;
            _discardOk = false;
            Busy = null;
        }

        /// <summary>
        /// True when the question was asked instead of doing the thing. Answering it runs the
        /// same command again, and the second run goes straight through.
        /// </summary>
        private static bool AskFirst(string title, System.Action again)
        {
            if (_discardOk)
            {
                _discardOk = false;
                return false;
            }

            var document = EditorState.Document;
            if (document == null || !document.Dirty)
            {
                return false;
            }

            var name = string.IsNullOrEmpty(document.Name) ? "This blueprint" : document.Name;
            Dialogs.Confirm(
                title,
                $"{name} has changes that are not saved. They will be lost.",
                "Discard",
                () =>
                {
                    _discardOk = true;
                    again();
                    _discardOk = false;
                });
            return true;
        }

        private static void Begin(string what)
        {
            _working = true;
            Busy = what;
            _busyUntil = Time.unscaledTime + MinBusySeconds;
        }

        private static void Done()
        {
            _working = false;
        }
    }
}
