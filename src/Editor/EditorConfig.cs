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
    /// <summary>The editor's look: the game's wood in daylight, or the same wood dimmed for night work.</summary>
    internal enum EditorTheme
    {
        Day,
        Night,
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

        /// <summary>Day or Night. The top bar's Night button writes this.</summary>
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
                EditorTheme.Day,
                "Day is the game's own wood and a light pane. Night dims the panels, and the 3D pane gets a dark "
                + "sky, ground and grid, easier on the eyes in a dark room.");

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
