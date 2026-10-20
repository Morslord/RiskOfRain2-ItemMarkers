using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using ItemMarkers.Core;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace ItemMarkers
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(OptionalOptionsMenu.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class ItemMarkersPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.morslord.itemmarkers";
        public const string PluginName = "ItemMarkers";
        public const string PluginVersion = "1.0.0";
        public const string ConfigFileName = "local.itemmarkers.cfg";
        private MarkerSettings settings = null!;
        private PickupIconCache? icons;
        private Run? iconRun;
        private readonly List<MarkerTarget> targets = new List<MarkerTarget>(128);
        private readonly Dictionary<HUD, HudMarkerLayer> layers = new Dictionary<HUD, HudMarkerLayer>();
        private readonly List<HUD> staleHuds = new List<HUD>();
        private float nextSweep;
        private float nextErrorLog;

        private void Awake()
        {
            try
            {
                settings = new MarkerSettings(OpenMarkerConfig());
            }
            catch (Exception ex)
            {
                Logger.LogError($"Could not initialize {ConfigFileName}; ItemMarkers is disabled to avoid replacing existing settings. " + ex);
                enabled = false;
                return;
            }
            icons = new PickupIconCache(Logger);
            Logger.LogInfo($"ItemMarkers {PluginVersion} by Morslord initialized. Local HUD only.");
        }

        private ConfigFile OpenMarkerConfig()
        {
            string configPath = System.IO.Path.Combine(Paths.ConfigPath, ConfigFileName);
            string legacyPath = System.IO.Path.Combine(Paths.ConfigPath, PluginGuid + ".cfg");
            // Preserve existing values on the first renamed-config launch. An existing new file wins.
            // Copy rather than move so the user's original configuration is never removed.
            if (!File.Exists(configPath) && File.Exists(legacyPath))
            {
                File.Copy(legacyPath, configPath, false);
                Logger.LogInfo($"Copied existing settings to {ConfigFileName}; the original configuration is retained.");
            }
            return new ConfigFile(configPath, true, Info.Metadata);
        }

        private void Start() => OptionalOptionsMenu.Register(settings, Logger);

        private void LateUpdate()
        {
            try
            {
                // Drop owned icon references when the run changes, after the old views are removed.
                if (!ReferenceEquals(iconRun, Run.instance))
                {
                    ClearLayers();
                    icons?.Clear();
                    iconRun = Run.instance;
                }
                if (!Run.instance || HUD.readOnlyInstanceList.Count == 0 || MarkerRules.ClampLimit(settings.MaximumMarkers.Value) == 0)
                {
                    ClearLayers(); targets.Clear(); nextSweep = 0; return;
                }
                if (Time.unscaledTime >= nextSweep)
                {
                    nextSweep = Time.unscaledTime + 0.2f;
                    PickupDiscovery.Collect(targets, settings);
                    RefreshHuds();
                    foreach (var layer in layers.Values) layer.Select(targets, settings);
                }
                foreach (var layer in layers.Values) layer.Render(settings);
            }
            catch (Exception ex)
            {
                ClearLayers(); targets.Clear();
                nextSweep = Time.unscaledTime + 2f;
                if (Time.unscaledTime >= nextErrorLog)
                {
                    nextErrorLog = Time.unscaledTime + 10f;
                    Logger.LogError("ItemMarkers HUD update failed; retrying discovery after two seconds. " + ex);
                }
            }
        }

        private void RefreshHuds()
        {
            staleHuds.Clear();
            foreach (var pair in layers)
                if (!pair.Key || !pair.Value.IsValid || !HUD.readOnlyInstanceList.Contains(pair.Key)) staleHuds.Add(pair.Key);
            foreach (var hud in staleHuds) { layers[hud].Dispose(); layers.Remove(hud); }
            foreach (var hud in HUD.readOnlyInstanceList)
                if (hud && hud.mainContainer && hud.cameraRigController && !layers.ContainsKey(hud))
                    layers.Add(hud, new HudMarkerLayer(hud, icons!));
        }

        private void OnDisable() { ClearLayers(); icons?.Clear(); iconRun = null; }
        private void OnDestroy() { ClearLayers(); icons?.Clear(); iconRun = null; OptionalOptionsMenu.ReleaseIcon(); }
        private void ClearLayers()
        {
            foreach (var layer in layers.Values) layer.Dispose();
            layers.Clear();
        }
    }
}
