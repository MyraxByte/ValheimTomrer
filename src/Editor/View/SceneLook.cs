using UnityEngine;

namespace ValheimTomrer.Editor.View
{
    /// <summary>The four lights the editor's view can have.</summary>
    internal enum TimeOfDay
    {
        Morning,
        Day,
        Evening,
        Night,
    }

    /// <summary>Everything that makes one time of day look the way it does in the editor's scene.</summary>
    internal sealed class Look
    {
        /// <summary>The sun (or moon) above the horizon, in degrees, and where it stands round the scene.</summary>
        public float Elevation;

        public float Azimuth;
        public Color KeyColor;
        public float KeyIntensity;

        /// <summary>The cool light from the other side that keeps the shadows from going black.</summary>
        public Color FillColor;

        public float FillIntensity;
        public Color Ambient;

        /// <summary>The horizon: the background, and the haze the far ground fades into.</summary>
        public Color Sky;

        public Color Ground;
        public Color Grid;
        public Color GridMajor;
        public Color Ring;
        public float ShadowStrength;
    }

    /// <summary>
    /// The light of the editor's scene by time of day: morning, day, evening and night. Colours come from
    /// code, there is no sky texture: the sky is the haze colour the ground fades into. The choice is a setting
    /// (<see cref="EditorConfig.TimeOfDay"/>) and only changes how the view looks, never the blueprint.
    /// </summary>
    internal static class SceneLook
    {
        private static readonly Look[] Looks =
        {
            // Morning: a low warm sun from the east, long soft shadows, a cool haze.
            new Look
            {
                Elevation = 22f, Azimuth = -75f, KeyColor = C(1.00f, 0.80f, 0.62f), KeyIntensity = 0.95f,
                FillColor = C(0.55f, 0.66f, 0.90f), FillIntensity = 0.28f, Ambient = C(0.27f, 0.28f, 0.34f),
                Sky = C(0.34f, 0.37f, 0.46f), Ground = C(0.17f, 0.18f, 0.19f),
                Grid = C(0.85f, 0.85f, 0.9f, 0.030f), GridMajor = C(0.85f, 0.85f, 0.9f, 0.075f), Ring = C(0.95f, 0.85f, 0.75f),
                ShadowStrength = 0.75f,
            },

            // Day: the sun high and white, soft blue sky light, short shadows.
            new Look
            {
                Elevation = 58f, Azimuth = -35f, KeyColor = C(1.00f, 0.96f, 0.88f), KeyIntensity = 1.05f,
                FillColor = C(0.62f, 0.72f, 0.92f), FillIntensity = 0.32f, Ambient = C(0.34f, 0.38f, 0.45f),
                Sky = C(0.27f, 0.34f, 0.45f), Ground = C(0.19f, 0.21f, 0.20f),
                Grid = C(0.9f, 0.9f, 0.95f, 0.028f), GridMajor = C(0.9f, 0.9f, 0.95f, 0.070f), Ring = C(0.9f, 0.92f, 0.95f),
                ShadowStrength = 0.8f,
            },

            // Evening: a low orange sun from the west, a violet dusk.
            new Look
            {
                Elevation = 11f, Azimuth = 100f, KeyColor = C(1.00f, 0.55f, 0.32f), KeyIntensity = 0.85f,
                FillColor = C(0.46f, 0.40f, 0.62f), FillIntensity = 0.24f, Ambient = C(0.22f, 0.19f, 0.25f),
                Sky = C(0.24f, 0.17f, 0.22f), Ground = C(0.12f, 0.11f, 0.12f),
                Grid = C(0.85f, 0.8f, 0.8f, 0.026f), GridMajor = C(0.85f, 0.8f, 0.8f, 0.065f), Ring = C(0.95f, 0.75f, 0.6f),
                ShadowStrength = 0.7f,
            },

            // Night: a pale blue moon, deep blue shadows, an almost black sky.
            new Look
            {
                Elevation = 42f, Azimuth = 35f, KeyColor = C(0.55f, 0.66f, 1.00f), KeyIntensity = 0.40f,
                FillColor = C(0.22f, 0.28f, 0.50f), FillIntensity = 0.14f, Ambient = C(0.07f, 0.09f, 0.15f),
                Sky = C(0.025f, 0.035f, 0.07f), Ground = C(0.055f, 0.065f, 0.09f),
                Grid = C(0.7f, 0.78f, 1f, 0.030f), GridMajor = C(0.7f, 0.78f, 1f, 0.075f), Ring = C(0.7f, 0.78f, 1f),
                ShadowStrength = 0.6f,
            },
        };

        public static TimeOfDay Now => EditorConfig.TimeOfDay != null ? EditorConfig.TimeOfDay.Value : TimeOfDay.Day;

        public static Look Current => Looks[(int)Now];

        public static Look Of(TimeOfDay time) => Looks[(int)time];

        public static string Name(TimeOfDay time) => time.ToString();

        private static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);
    }
}
