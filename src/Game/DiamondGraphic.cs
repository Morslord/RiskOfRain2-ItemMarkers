using UnityEngine;
using UnityEngine.UI;

namespace ItemMarkers
{
    // Draw the requested ◆ directly so it works even when the HUD font has no diamond glyph.
    public sealed class DiamondGraphic : MaskableGraphic
    {
        private Color borderColor;
        private bool border;
        public void SetColors(Color fill, bool hasBorder, Color outline)
        {
            if (color == fill && border == hasBorder && borderColor == outline) return;
            color = fill; border = hasBorder; borderColor = outline; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            var center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            float inner = border ? Mathf.Max(0, radius - 1.25f) : radius;
            var points = new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
            mesh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i < 4; i++) mesh.AddVert(center + points[i] * inner, color, Vector2.zero);
            for (int i = 0; i < 4; i++) mesh.AddTriangle(0, i + 1, (i + 1) % 4 + 1);
            if (!border) return;
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                int first = mesh.currentVertCount;
                mesh.AddVert(center + points[i] * radius, borderColor, Vector2.zero);
                mesh.AddVert(center + points[next] * radius, borderColor, Vector2.zero);
                mesh.AddVert(center + points[next] * inner, borderColor, Vector2.zero);
                mesh.AddVert(center + points[i] * inner, borderColor, Vector2.zero);
                mesh.AddTriangle(first, first + 1, first + 2);
                mesh.AddTriangle(first, first + 2, first + 3);
            }
        }
    }
}
