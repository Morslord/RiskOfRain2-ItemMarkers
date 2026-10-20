using System.Collections.Generic;
using BepInEx.Configuration;
using ItemMarkers.Core;

namespace ItemMarkers
{
    internal sealed class MarkerSettings
    {
        public readonly ConfigEntry<int> MaximumMarkers;
        public readonly ConfigEntry<float> MarkerSize;
        public readonly ConfigEntry<int> HideWithinDistance;
        public readonly ConfigEntry<bool> ShowTemporary;
        public readonly ConfigEntry<bool> ShowEthereal;
        public readonly Dictionary<MarkerCategory, ConfigEntry<bool>> Categories = new Dictionary<MarkerCategory, ConfigEntry<bool>>();

        public MarkerSettings(ConfigFile config)
        {
            MaximumMarkers = config.Bind("Markers", "Maximum Markers", 60,
                new ConfigDescription("Maximum simultaneous markers, including Command essences.\n\n0 hides all markers. The nearest eligible pickups are preferred.", new AcceptableValueRange<int>(0, 60)));
            MarkerSize = config.Bind("Markers", "Marker Size", 100f,
                new ConfigDescription("Marker size as a percentage.\n\nApplies to the icon or diamond and the distance label. Steps of 5 percent.", new AcceptableValueRange<float>(50f, 200f)));
            HideWithinDistance = config.Bind("Markers", "Hide Within Distance", 5,
                new ConfigDescription("Hide item and Command essence markers when closer than this distance in meters.\n\n0 disables proximity hiding. Steps of 1 meter. Nearby hidden pickups do not use marker slots.", new AcceptableValueRange<int>(0, 50)));
            ShowTemporary = config.Bind("Item Variants", "Temporary Items", true,
                "Show temporary items with a decay duration.\n\nThe item's category filter must also be enabled.");
            ShowEthereal = config.Bind("Item Variants", "Ethereal Items", true,
                "Show Ethereal items that last for the current stage.\n\nThe item's category filter must also be enabled. This filter is separate from temporary items.");
            foreach (MarkerCategory category in System.Enum.GetValues(typeof(MarkerCategory)))
                if (category != MarkerCategory.Unknown)
                    Categories.Add(category, config.Bind("Item Categories", category.ToString(), true,
                        "Show " + category + " pickups and their Command essences.\n\nTemporary and Ethereal variants must also pass their respective filters."));
        }

        public bool Allows(MarkerCategory category, bool temporary, bool ethereal) =>
            MarkerRules.Allows(category, Categories.TryGetValue(category, out var option) && option.Value,
                temporary, ethereal, ShowTemporary.Value, ShowEthereal.Value);
    }
}
