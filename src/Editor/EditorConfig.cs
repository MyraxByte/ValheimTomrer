using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ValheimTomrer.Editor
{
    /// <summary>
    /// Config for the in-game blueprint editor. Bound from the plugin so every editor
    /// setting sits in one place instead of growing the plugin file.
    /// </summary>
    /// <summary>The editor's look: dark (the default, easy on the eyes) or light.</summary>
    internal enum EditorTheme
    {
        Dark,
        Light,
    }

    internal static class EditorConfig
    {
        /// <summary>
        /// The layer the editor draws its own copies on. Free in Valheim 1.0 (Phase 0 measured
        /// 3, 6, 7 and 30 free). The main camera drops it in GameCameraAwakePatch, and every
        /// physics query the editor makes is masked to it, so the two worlds never mix.
        /// </summary>
        public const int Layer = 30;

        public static ConfigEntry<KeyCode> Key;

        /// <summary>Puts down the rectangle that turns a standing building into a blueprint, and captures it.</summary>
        public static ConfigEntry<KeyCode> CaptureKey;

        /// <summary>Off: the palette only lists what this character has unlocked.</summary>
        public static ConfigEntry<bool> ShowAllPieces;

        /// <summary>Snap dots while placing. The top bar's Dots button writes this.</summary>
        public static ConfigEntry<bool> SnapDots;

        /// <summary>Pieces as wire boxes instead of models. The top bar's Boxes button writes this.</summary>
        public static ConfigEntry<bool> Boxes;

        /// <summary>Pieces used last, prefab names, newest first. Written by <see cref="PieceMemory"/>.</summary>
        public static ConfigEntry<string> RecentPieces;

        /// <summary>Starred pieces, prefab names. Written by <see cref="PieceMemory"/>.</summary>
        public static ConfigEntry<string> FavouritePieces;

        /// <summary>The Layers card (the pieces of the blueprint). Closed by default: the view comes first.</summary>
        public static ConfigEntry<bool> LayersOpen;

        /// <summary>The Inspector card (blueprint, selection, problems).</summary>
        public static ConfigEntry<bool> InspectorOpen;

        /// <summary>The placing grid in metres, 0 for none. The snap bar's Grid button writes this.</summary>
        public static ConfigEntry<float> GridStep;

        /// <summary>Degrees per turn step: R, the wheel, the pad. The snap bar's Angle button writes this.</summary>
        public static ConfigEntry<float> AngleStep;

        /// <summary>Use the pieces' snap points. The snap bar's Points button writes this.</summary>
        public static ConfigEntry<bool> SnapPoints;

        /// <summary>Dark or Light. The top bar's theme button writes this.</summary>
        public static ConfigEntry<EditorTheme> Theme;

        public static void Bind(ConfigFile config)
        {
            Key = config.Bind(
                "Editor",
                "Key",
                KeyCode.F7,
                "Opens and closes the blueprint editor. Esc and the pad's circle close it too.");

            CaptureKey = config.Bind(
                "Editor",
                "CaptureKey",
                KeyCode.F8,
                "With the editor closed: puts a rectangle on the ground where you aim. The wheel turns it, "
                + "Shift + wheel and Alt + wheel change its sides. Press again and what stands inside opens "
                + "in the editor as a new blueprint. Esc stops it.");

            ShowAllPieces = config.Bind(
                "Editor",
                "ShowAllPieces",
                false,
                "Show every building piece in the editor. Off means only the ones this character has unlocked.");

            SnapDots = config.Bind(
                "Editor",
                "SnapDots",
                true,
                "Show the snap dots while placing a piece. Snapping itself is always on.");

            Boxes = config.Bind(
                "Editor",
                "Boxes",
                false,
                "Draw pieces as plain boxes instead of models. Easier to see through a full blueprint.");

            Theme = config.Bind(
                "Editor",
                "Theme",
                EditorTheme.Dark,
                "The editor's look. Dark: dark panels and a dark 3D view, easy on the eyes at night. Light: "
                + "white panels and a light grey view.");

            RecentPieces = config.Bind(
                "Editor",
                "RecentPieces",
                "",
                "Pieces you used last, as prefab names. Kept by the editor, shown first in Quick add (Tab).");

            FavouritePieces = config.Bind(
                "Editor",
                "FavouritePieces",
                "",
                "Pieces you starred (right click a tile in Quick add), as prefab names.");

            LayersOpen = config.Bind(
                "Editor",
                "LayersOpen",
                false,
                "Show the Layers card (the pieces of the open blueprint) on the left. Alt+1 or the Layers button.");

            InspectorOpen = config.Bind(
                "Editor",
                "InspectorOpen",
                true,
                "Show the Inspector card (blueprint, selection, problems) on the right. Alt+2 or the Inspector button.");

            GridStep = config.Bind(
                "Editor",
                "GridStep",
                0f,
                new ConfigDescription(
                    "A placing grid in metres, 0 for none. A piece that snaps to no snap point lands on the grid, "
                    + "and the arrow keys move this far. Alt+G or the snap bar's Grid button change it.",
                    new AcceptableValueList<float>(0f, 0.25f, 0.5f, 1f, 2f, 4f)));

            AngleStep = config.Bind(
                "Editor",
                "AngleStep",
                22.5f,
                new ConfigDescription(
                    "Degrees per turn: R, the wheel and the pad. 22.5 is the game's. Alt+R or the snap bar's "
                    + "Angle button change it.",
                    new AcceptableValueList<float>(5f, 15f, 22.5f, 45f, 90f)));

            SnapPoints = config.Bind(
                "Editor",
                "SnapPoints",
                true,
                "Snap to the pieces' snap points, like the game. Off: only the grid (if any). Shift held turns all "
                + "snapping off for a moment. Alt+S or the snap bar's Points button change it.");

            Input.Keymap.Bind(config);

            // Settings the mod no longer has. The mouse and the right stick follow the game's own
            // sensitivity settings, and the camera has one mode.
            Drop(config, "Editor", "LookSensitivity");
            Drop(config, "Editor", "PadLookSensitivity");
            Drop(config, "Editor", "CameraMode");
        }

        /// <summary>Takes a setting that is gone out of the file. BepInEx keeps its line and writes it back on every save.</summary>
        private static void Drop(ConfigFile config, string section, string key)
        {
            var orphans = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(config) as Dictionary<ConfigDefinition, string>;
            if (orphans != null && orphans.Remove(new ConfigDefinition(section, key)))
            {
                config.Save();
            }
        }
    }
}
