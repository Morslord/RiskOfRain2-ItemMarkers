using System;
using System.Collections.Generic;
using ItemMarkers.Core;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemMarkers
{
    internal sealed class HudMarkerLayer : IDisposable
    {
        private readonly HUD hud;
        private readonly RectTransform root;
        private readonly List<MarkerView> views = new List<MarkerView>(60);
        private readonly NearestMarkers<MarkerTarget> nearest = new NearestMarkers<MarkerTarget>();
        private readonly TMP_FontAsset? font;
        private readonly PickupIconCache icons;

        public HudMarkerLayer(HUD hud, PickupIconCache icons)
        {
            this.hud = hud;
            this.icons = icons;
            var go = new GameObject("Morslord.ItemMarkers.Hud", typeof(RectTransform));
            go.layer = hud.mainContainer.layer;
            root = go.GetComponent<RectTransform>();
            root.SetParent(hud.mainContainer.transform, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            var existingText = hud.GetComponentInChildren<TMP_Text>(true);
            font = existingText ? existingText.font : TMP_Settings.defaultFontAsset;
        }

        public bool IsValid => hud && root && hud.mainContainer && root.parent == hud.mainContainer.transform;

        public void Select(List<MarkerTarget> targets, MarkerSettings settings)
        {
            nearest.Clear();
            var camera = hud.cameraRigController ? hud.cameraRigController.sceneCam : null;
            if (camera == null || !camera) { Hide(); return; }
            Vector3 origin = hud.targetBodyObject ? hud.targetBodyObject.transform.position : camera.transform.position;
            foreach (var target in targets)
            {
                if (!target.IsAlive) continue;
                float distanceSquared = (target.Source.transform.position - origin).sqrMagnitude;
                if (!MarkerRules.ShouldShowAtDistance(distanceSquared, settings.HideWithinDistance.Value)) continue;
                nearest.Consider(target, distanceSquared, target.Identity, settings.MaximumMarkers.Value);
            }
            while (views.Count < nearest.Count) views.Add(new MarkerView(root, font, icons));
            for (int i = 0; i < views.Count; i++)
            {
                if (i < nearest.Count) views[i].Bind(nearest[i]);
                else views[i].Hide();
            }
        }

        public void Render(MarkerSettings settings)
        {
            var camera = hud.cameraRigController ? hud.cameraRigController.sceneCam : null;
            if (camera == null || !camera || !camera.isActiveAndEnabled || !hud.mainContainer.activeInHierarchy || !Run.instance
                || settings.MaximumMarkers.Value == 0) { Hide(); return; }
            Vector3 origin = hud.targetBodyObject ? hud.targetBodyObject.transform.position : camera.transform.position;
            float scale = MarkerRules.ScaleFromPercent(settings.MarkerSize.Value);
            for (int i = 0; i < nearest.Count; i++)
            {
                var target = nearest[i];
                if (!target.IsAlive || i >= MarkerRules.ClampLimit(settings.MaximumMarkers.Value)
                    || !settings.Allows(target.Category, target.Pickup.isTempItem, target.Pickup.isRental))
                { views[i].Hide(); continue; }
                float distanceSquared = (target.Source.transform.position - origin).sqrMagnitude;
                if (!MarkerRules.ShouldShowAtDistance(distanceSquared, settings.HideWithinDistance.Value))
                { views[i].Hide(); continue; }
                var viewport = camera.WorldToViewportPoint(target.Position);
                if (!MarkerProjection.TryPlace(viewport.x, viewport.y, viewport.z, root.rect.width, root.rect.height,
                    45f * scale, 48f * scale, out var placement)) { views[i].Hide(); continue; }
                var distance = Mathf.Sqrt(distanceSquared);
                views[i].Show(new Vector2(placement.X, placement.Y), scale, distance);
            }
        }

        public void Hide() { foreach (var view in views) view.Hide(); }
        public void Dispose() { if (root) UnityEngine.Object.Destroy(root.gameObject); views.Clear(); nearest.Clear(); }
    }

    internal sealed class MarkerView
    {
        private readonly RectTransform root;
        private readonly Image image;
        private readonly RawImage textureImage;
        private readonly DiamondGraphic diamond;
        private readonly TextMeshProUGUI distanceLabel;
        private readonly PickupIconCache icons;
        private int previousDistance = -1;

        public MarkerView(RectTransform parent, TMP_FontAsset? font, PickupIconCache icons)
        {
            this.icons = icons;
            root = MakeRect("Marker", parent, new Vector2(90, 84), Vector2.zero);
            image = MakeRect("Item Icon", root, new Vector2(32, 32), new Vector2(0, 6)).gameObject.AddComponent<Image>();
            image.preserveAspect = true; image.raycastTarget = false;
            textureImage = MakeRect("Item Texture", root, new Vector2(32, 32), new Vector2(0, 6)).gameObject.AddComponent<RawImage>();
            textureImage.raycastTarget = false;
            // New 100% is the old 60% diamond size (30 * 0.6 = 18 HUD units).
            diamond = MakeRect("Command Diamond", root, new Vector2(18, 18), new Vector2(0, 6)).gameObject.AddComponent<DiamondGraphic>();
            diamond.raycastTarget = false;
            distanceLabel = MakeRect("Distance", root, new Vector2(90, 22), new Vector2(0, -22)).gameObject.AddComponent<TextMeshProUGUI>();
            if (font) distanceLabel.font = font;
            distanceLabel.fontSize = 14;
            distanceLabel.alignment = TextAlignmentOptions.Center;
            distanceLabel.color = Color.white;
            distanceLabel.raycastTarget = false;
            distanceLabel.enableWordWrapping = false;
            var shadow = distanceLabel.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Color.black; shadow.effectDistance = new Vector2(1, -1);
            Hide();
        }

        public void Bind(MarkerTarget target)
        {
            // Catalog colors remain authoritative, including DLC and modded catalog values.
            var sprite = target.Command ? null : icons.Resolve(target.Definition);
            image.sprite = sprite;
            image.enabled = !target.Command && sprite;
            textureImage.texture = target.Definition.iconTexture;
            textureImage.enabled = !target.Command && !sprite && target.Definition.iconTexture;
            diamond.enabled = target.Command;
            if (target.Command)
            {
                Color fill = target.LunarEquipment ? ColorCatalog.GetColor(ColorCatalog.ColorIndex.Equipment) : target.Definition.baseColor;
                diamond.SetColors(fill, target.LunarEquipment, ColorCatalog.GetColor(ColorCatalog.ColorIndex.LunarItem));
            }
        }

        public void Show(Vector2 position, float scale, float distance)
        {
            if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
            root.anchoredPosition = position;
            root.localScale = Vector3.one * scale;
            int rounded = Mathf.Max(0, Mathf.RoundToInt(distance));
            if (rounded != previousDistance)
            {
                distanceLabel.SetText("{0:0} m", rounded);
                previousDistance = rounded;
            }
        }

        public void Hide() { if (root.gameObject.activeSelf) root.gameObject.SetActive(false); }

        private static RectTransform MakeRect(string name, Transform parent, Vector2 size, Vector2 offset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size; rect.anchoredPosition = offset;
            return rect;
        }
    }
}
