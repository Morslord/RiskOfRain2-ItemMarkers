using System;
using System.IO;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace ItemMarkers
{
    // Reflection keeps RiskOfOptions entirely optional, including during type loading.
    internal static class OptionalOptionsMenu
    {
        public const string Guid = "com.rune580.riskofoptions";
        private static Texture2D? modIconTexture;
        private static Sprite? modIcon;

        public static void Register(MarkerSettings settings, ManualLogSource log)
        {
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out var plugin)) return;
            try
            {
                var assembly = plugin.Instance.GetType().Assembly;
                var manager = RequiredType(assembly, "RiskOfOptions.ModSettingsManager");
                var baseOption = RequiredType(assembly, "RiskOfOptions.Options.BaseOption");
                var add = manager.GetMethod("AddOption", new[] { baseOption, typeof(string), typeof(string) })
                    ?? throw new MissingMethodException("RiskOfOptions.ModSettingsManager.AddOption");
                object integerConfig = NewConfig(assembly, "IntSliderConfig", ("min", 0), ("max", 60));
                object distanceConfig = NewConfig(assembly, "IntSliderConfig", ("min", 0), ("max", 50), ("formatString", "{0} m"));
                object sizeConfig = NewConfig(assembly, "StepSliderConfig", ("min", 50f), ("max", 200f), ("increment", 5f), ("remapManualInputToStep", true));
                void Add(string optionName, ConfigEntryBase entry, object? optionConfig = null)
                {
                    var optionType = RequiredType(assembly, "RiskOfOptions.Options." + optionName);
                    object? option = optionConfig == null
                        ? Activator.CreateInstance(optionType, new object[] { entry })
                        : Activator.CreateInstance(optionType, new[] { (object)entry, optionConfig });
                    add.Invoke(null, new[] { option, ItemMarkersPlugin.PluginGuid, ItemMarkersPlugin.PluginName });
                }
                Add("IntSliderOption", settings.MaximumMarkers, integerConfig);
                Add("StepSliderOption", settings.MarkerSize, sizeConfig);
                Add("IntSliderOption", settings.HideWithinDistance, distanceConfig);
                Add("CheckBoxOption", settings.ShowTemporary);
                Add("CheckBoxOption", settings.ShowEthereal);
                foreach (var entry in settings.Categories.Values) Add("CheckBoxOption", entry);
                manager.GetMethod("SetModDescription", new[] { typeof(string), typeof(string), typeof(string) })?
                    .Invoke(null, new object[] { "Client-side ground item and Command essence markers by Morslord.", ItemMarkersPlugin.PluginGuid, ItemMarkersPlugin.PluginName });
                RegisterIcon(manager, log);
                log.LogInfo("Registered " + (5 + settings.Categories.Count) + " ItemMarkers options in RiskOfOptions.");
            }
            catch (Exception ex)
            {
                log.LogError("RiskOfOptions registration failed. BepInEx configuration remains available. " + ex);
            }
        }

        private static void RegisterIcon(Type manager, ManualLogSource log)
        {
            try
            {
                var setIcon = manager.GetMethod("SetModIcon", new[] { typeof(Sprite), typeof(string), typeof(string) })
                    ?? throw new MissingMethodException("RiskOfOptions.ModSettingsManager.SetModIcon(Sprite, string, string)");
                if (!modIcon)
                {
                    using var resource = typeof(OptionalOptionsMenu).Assembly.GetManifestResourceStream("ItemMarkers.Resources.ModIcon.png")
                        ?? throw new InvalidOperationException("Embedded ItemMarkers mod icon is missing.");
                    using var buffer = new MemoryStream();
                    resource.CopyTo(buffer);
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    modIconTexture = texture;
                    if (!ImageConversion.LoadImage(texture, buffer.ToArray(), true))
                        throw new InvalidOperationException("Embedded ItemMarkers mod icon could not be decoded as PNG.");
                    texture.name = "ItemMarkers Mod Icon";
                    texture.filterMode = FilterMode.Bilinear;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    modIcon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    if (!modIcon) throw new InvalidOperationException("Could not create ItemMarkers mod icon sprite.");
                    modIcon.name = "ItemMarkers Mod Icon";
                }
                setIcon.Invoke(null, new object[] { modIcon!, ItemMarkersPlugin.PluginGuid, ItemMarkersPlugin.PluginName });
                log.LogInfo("Registered embedded ItemMarkers mod icon in RiskOfOptions.");
            }
            catch (Exception ex)
            {
                ReleaseIcon();
                log.LogWarning("RiskOfOptions mod icon registration failed; settings remain available. " + ex);
            }
        }

        public static void ReleaseIcon()
        {
            if (modIcon) UnityEngine.Object.Destroy(modIcon);
            if (modIconTexture) UnityEngine.Object.Destroy(modIconTexture);
            modIcon = null;
            modIconTexture = null;
        }

        private static Type RequiredType(Assembly assembly, string name) => assembly.GetType(name, true)!;
        private static object NewConfig(Assembly assembly, string name, params (string name, object value)[] values)
        {
            var type = RequiredType(assembly, "RiskOfOptions.OptionConfigs." + name);
            var instance = Activator.CreateInstance(type)!;
            foreach (var field in values)
                (type.GetField(field.name) ?? throw new MissingFieldException(type.FullName, field.name)).SetValue(instance, field.value);
            return instance;
        }
    }
}
