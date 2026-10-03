using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace ValheimTomrer.Editor
{
    /// <summary>
    /// The pieces the player used last and the ones they starred, kept in the config file as prefab
    /// names so they survive a restart. Quick add (Tab) shows them first. Nothing here reads the
    /// world, and a name the game no longer has is skipped by whoever reads it.
    /// </summary>
    internal static class PieceMemory
    {
        public const int RecentLimit = 16;

        private static readonly List<string> RecentNames = new List<string>();
        private static readonly HashSet<string> FavouriteNames = new HashSet<string>();
        private static bool _loaded;

        /// <summary>Most recent first.</summary>
        public static IReadOnlyList<string> Recent
        {
            get
            {
                Load();
                return RecentNames;
            }
        }

        public static int FavouriteCount
        {
            get
            {
                Load();
                return FavouriteNames.Count;
            }
        }

        public static bool IsFavourite(string prefabName)
        {
            Load();
            return prefabName != null && FavouriteNames.Contains(prefabName);
        }

        /// <summary>Position in the recent list, or -1.</summary>
        public static int RecentIndex(string prefabName)
        {
            Load();
            return prefabName == null ? -1 : RecentNames.IndexOf(prefabName);
        }

        /// <summary>A piece went in hand: it moves to the front of the recent list.</summary>
        public static void Use(string prefabName)
        {
            Load();
            if (string.IsNullOrEmpty(prefabName))
            {
                return;
            }

            RecentNames.Remove(prefabName);
            RecentNames.Insert(0, prefabName);
            if (RecentNames.Count > RecentLimit)
            {
                RecentNames.RemoveRange(RecentLimit, RecentNames.Count - RecentLimit);
            }

            Save();
        }

        /// <summary>Stars or unstars a piece. True when it is starred now.</summary>
        public static bool ToggleFavourite(string prefabName)
        {
            Load();
            if (string.IsNullOrEmpty(prefabName))
            {
                return false;
            }

            var on = FavouriteNames.Add(prefabName);
            if (!on)
            {
                FavouriteNames.Remove(prefabName);
            }

            Save();
            return on;
        }

        /// <summary>Forgets what was read, so the next call reads the config again. The autotest uses it.</summary>
        public static void Reset()
        {
            _loaded = false;
            RecentNames.Clear();
            FavouriteNames.Clear();
        }

        private static void Load()
        {
            if (_loaded || EditorConfig.RecentPieces == null || EditorConfig.FavouritePieces == null)
            {
                return;
            }

            _loaded = true;
            RecentNames.Clear();
            RecentNames.AddRange(Split(EditorConfig.RecentPieces.Value).Take(RecentLimit));
            FavouriteNames.Clear();
            foreach (var name in Split(EditorConfig.FavouritePieces.Value))
            {
                FavouriteNames.Add(name);
            }
        }

        private static void Save()
        {
            Write(EditorConfig.RecentPieces, string.Join(",", RecentNames));
            Write(EditorConfig.FavouritePieces, string.Join(",", FavouriteNames.OrderBy(n => n)));
        }

        private static void Write(ConfigEntry<string> entry, string value)
        {
            if (entry != null && entry.Value != value)
            {
                entry.Value = value;
            }
        }

        private static IEnumerable<string> Split(string text)
        {
            return (text ?? "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0);
        }
    }
}
