using System.Collections.Generic;
using System.Reflection;
using ItemMarkers.Core;
using RoR2;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace ItemMarkers
{
    internal readonly struct MarkerTarget
    {
        public MarkerTarget(Component source, UniquePickup pickup, bool command, PickupDef definition,
            MarkerCategory category, bool lunarEquipment)
        {
            Source = source; Pickup = pickup; Command = command; Definition = definition;
            Category = category; LunarEquipment = lunarEquipment; Identity = source.GetInstanceID();
        }
        public Component Source { get; }
        public UniquePickup Pickup { get; }
        public bool Command { get; }
        public PickupDef Definition { get; }
        public MarkerCategory Category { get; }
        public bool LunarEquipment { get; }
        public int Identity { get; }
        public bool IsAlive => Source && Source.gameObject.activeInHierarchy
            && (Command ? ((PickupPickerController)Source).available : ((GenericPickupController)Source).pickup == Pickup);
        public Vector3 Position => Source.transform.position + Vector3.up * 0.8f;
    }

    internal static class PickupDiscovery
    {
        // Read the game's actual Command prefab; do not rely on localized names or treat every
        // CreatePickup picker (for example other choice interactables) as a Command essence.
        private static readonly FieldInfo? CommandPrefab = typeof(RoR2.Artifacts.CommandArtifactManager)
            .GetField("commandCubePrefab", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        public static void Collect(List<MarkerTarget> targets, MarkerSettings settings)
        {
            targets.Clear();
            foreach (var controller in InstanceTracker.GetInstancesList<GenericPickupController>())
            {
                if (!controller || !controller.isActiveAndEnabled) continue;
                if (TryDescribe(controller.pickup, settings, out var def, out var category, out bool lunar))
                    targets.Add(new MarkerTarget(controller, controller.pickup, false, def!, category, lunar));
            }
            foreach (var picker in InstanceTracker.GetInstancesList<PickupPickerController>())
            {
                if (!picker || !picker.isActiveAndEnabled || !picker.available || !IsCommandEssence(picker)) continue;
                if (TryDescribeCommand(picker, settings, out var pickup, out var def, out var category, out bool lunar))
                    targets.Add(new MarkerTarget(picker, pickup, true, def!, category, lunar));
            }
        }

        private static bool TryDescribe(UniquePickup pickup, MarkerSettings settings, out PickupDef? def,
            out MarkerCategory category, out bool lunarEquipment)
        {
            def = PickupCatalog.GetPickupDef(pickup.pickupIndex);
            category = MarkerCategory.Unknown;
            lunarEquipment = false;
            if (def == null) return false;
            if (def.equipmentIndex != EquipmentIndex.None)
            {
                var equipment = EquipmentCatalog.GetEquipmentDef(def.equipmentIndex);
                if (!equipment) return false;
                lunarEquipment = equipment.isLunar;
                category = MarkerCategory.Equipment;
            }
            else if (def.itemIndex != ItemIndex.None)
            {
                var item = ItemCatalog.GetItemDef(def.itemIndex);
                if (!item || item.hidden) return false;
                category = MarkerRules.Classify(item.tier.ToString(), false);
            }
            return settings.Allows(category, pickup.isTempItem, pickup.isRental);
        }

        private static bool TryDescribeCommand(PickupPickerController picker, MarkerSettings settings,
            out UniquePickup pickup, out PickupDef? def, out MarkerCategory category, out bool lunar)
        {
            pickup = default; def = null; category = MarkerCategory.Unknown; lunar = false;
            bool found = false;
            // Command options can be synchronized before the generating pickup. Require a uniform
            // category and modifiers; never claim that a heterogeneous modded picker has one tier.
            if (picker.options == null) return false;
            foreach (var option in picker.options)
            {
                if (!option.available) continue;
                if (!TryDescribe(option.pickup, settings, out var optionDef, out var optionCategory, out bool optionLunar)) continue;
                if (found && (category != optionCategory || lunar != optionLunar
                    || pickup.isTempItem != option.pickup.isTempItem || pickup.isRental != option.pickup.isRental)) return false;
                if (!found)
                {
                    pickup = option.pickup; def = optionDef; category = optionCategory; lunar = optionLunar;
                    found = true;
                }
            }
            // Some runtime versions retain the authoritative generating pickup. Respect its modifiers
            // even if the synchronized choice list omits them.
            if (found && picker.pickupThatGeneratedOptions.HasValue)
            {
                var origin = picker.pickupThatGeneratedOptions.Value;
                if (!settings.Allows(category, origin.isTempItem, origin.isRental)) return false;
                pickup.isRental |= origin.isRental;
                if (origin.isTempItem) pickup.decayValue = origin.decayValue;
            }
            return found;
        }

        // Older Command prefab listeners still use the legacy event. Read it only for discovery.
#pragma warning disable CS0618
        private static bool IsCommandEssence(PickupPickerController picker)
        {
            if (picker.isDelusionChoice || picker.isRebirthChoice) return false;
            var prefab = CommandPrefab?.GetValue(null) as GameObject;
            if (prefab == null || !prefab) return false;
            var expected = prefab.GetComponent<NetworkIdentity>();
            var actual = picker.GetComponent<NetworkIdentity>();
            if (!expected || !actual || !expected.assetId.IsValid() || !expected.assetId.Equals(actual.assetId)) return false;
            return CreatesPickup(picker.onPickupSelected, picker) || CreatesPickup(picker.onUniquePickupSelected, picker);
        }
#pragma warning restore CS0618

        private static bool CreatesPickup(UnityEventBase? selectionEvent, PickupPickerController picker)
        {
            if (selectionEvent == null) return false;
            for (int i = 0; i < selectionEvent.GetPersistentEventCount(); i++)
                if (selectionEvent.GetPersistentTarget(i) == picker
                    && selectionEvent.GetPersistentMethodName(i) == "CreatePickup") return true;
            return false;
        }
    }
}
