using System;
using System.Collections.Generic;

namespace ItemMarkers.Core
{
    public enum MarkerCategory { Unknown, White, Green, Red, Boss, Lunar, Void, Hallowed, Hex, Pages, Equipment }

    // Tier-name mapping adapted from ItemShareFix by Akilorp2D and contributors (MIT).
    // See THIRD_PARTY_NOTICES.md for the original license and copyright notice.
    public static class MarkerRules
    {
        public static MarkerCategory Classify(string tier, bool equipment)
        {
            if (equipment) return MarkerCategory.Equipment;
            switch (tier)
            {
                case "Tier1": return MarkerCategory.White;
                case "Tier2": return MarkerCategory.Green;
                case "Tier3": return MarkerCategory.Red;
                case "Boss": return MarkerCategory.Boss;
                case "Lunar": return MarkerCategory.Lunar;
                case "Cursed": return MarkerCategory.Hallowed;
                case "Hex": return MarkerCategory.Hex;
                case "Page": return MarkerCategory.Pages;
                default: return tier.StartsWith("Void", StringComparison.Ordinal) ? MarkerCategory.Void : MarkerCategory.Unknown;
            }
        }

        public static bool Allows(MarkerCategory category, bool categoryEnabled, bool temporary, bool ethereal,
            bool showTemporary, bool showEthereal) => category != MarkerCategory.Unknown && categoryEnabled
                && (!temporary || showTemporary) && (!ethereal || showEthereal);

        public static int ClampLimit(int value) => Math.Max(0, Math.Min(60, value));

        public static bool ShouldShowAtDistance(float distanceSquared, int hideWithinMeters)
        {
            if (!IsFinite(distanceSquared) || distanceSquared < 0) return false;
            int radius = Math.Max(0, Math.Min(50, hideWithinMeters));
            return radius == 0 || distanceSquared >= radius * radius;
        }

        public static float ScaleFromPercent(float value)
        {
            if (!IsFinite(value)) return 1f;
            return (float)Math.Round(Math.Max(50f, Math.Min(200f, value)) / 5f, MidpointRounding.AwayFromZero) * 0.05f;
        }

        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct MarkerPlacement
    {
        public MarkerPlacement(float x, float y, bool atEdge) { X = x; Y = y; AtEdge = atEdge; }
        public float X { get; }
        public float Y { get; }
        public bool AtEdge { get; }
    }

    public static class MarkerProjection
    {
        // Input coordinates are camera viewport coordinates; output is relative to the HUD center.
        // Mirroring the camera projection preserves the direction of objects behind the camera.
        public static bool TryPlace(float viewportX, float viewportY, float depth, float width, float height,
            float marginX, float marginY, out MarkerPlacement placement)
        {
            placement = default;
            if (!MarkerRules.IsFinite(viewportX) || !MarkerRules.IsFinite(viewportY) || !MarkerRules.IsFinite(depth)
                || !MarkerRules.IsFinite(width) || !MarkerRules.IsFinite(height) || width <= 0 || height <= 0) return false;
            float x = (viewportX - 0.5f) * width;
            float y = (viewportY - 0.5f) * height;
            float boundX = Math.Max(1, width / 2 - marginX);
            float boundY = Math.Max(1, height / 2 - marginY);
            bool edge = depth <= 0 || Math.Abs(x) > boundX || Math.Abs(y) > boundY;
            if (depth <= 0) { x = -x; y = -y; }
            if (edge)
            {
                // Directly behind the camera has no unique horizontal direction: use the lower edge.
                if (Math.Abs(x) + Math.Abs(y) < 0.001f) y = -1;
                float ratio = Math.Max(Math.Abs(x) / boundX, Math.Abs(y) / boundY);
                x /= ratio;
                y /= ratio;
            }
            placement = new MarkerPlacement(x, y, edge);
            return MarkerRules.IsFinite(x) && MarkerRules.IsFinite(y);
        }
    }

    public sealed class NearestMarkers<T>
    {
        private readonly List<(T value, float distance, int identity)> entries = new List<(T, float, int)>(60);
        public int Count => entries.Count;
        public T this[int index] => entries[index].value;
        public void Clear() => entries.Clear();

        // Keep only the nearest display candidates, without imposing a discovery limit.
        public void Consider(T value, float distanceSquared, int identity, int limit)
        {
            limit = MarkerRules.ClampLimit(limit);
            if (limit == 0 || !MarkerRules.IsFinite(distanceSquared) || distanceSquared < 0) return;
            int index = 0;
            while (index < entries.Count && (entries[index].distance < distanceSquared
                || (entries[index].distance == distanceSquared && entries[index].identity < identity))) index++;
            if (index >= limit) return;
            entries.Insert(index, (value, distanceSquared, identity));
            if (entries.Count > limit) entries.RemoveAt(entries.Count - 1);
        }
    }
}
