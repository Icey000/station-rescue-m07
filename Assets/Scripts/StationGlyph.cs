using UnityEngine;
using UnityEngine.UI;
namespace UnityAgentLab
{
    public enum GlyphKind { BlueCell, OrangeCell, Lightning, Ring, Dock }
    public sealed class StationGlyph : MaskableGraphic
    {
        [SerializeField] private GlyphKind kind;
        [SerializeField, Range(0, 1)] private float fill = 1;
        public GlyphKind Kind { get => kind; set { kind = value; SetVerticesDirty(); } }
        public float Fill { get => fill; set { if (Mathf.Abs(fill - value) > 0.001f) { fill = value; SetVerticesDirty(); } } }
        private Vector2 Point(float x, float y) => new Vector2(rectTransform.rect.xMin + x * rectTransform.rect.width, rectTransform.rect.yMin + y * rectTransform.rect.height);
        private void Quad(VertexHelper vh, float x, float y, float w, float h, Color c)
        {
            int n = vh.currentVertCount;
            vh.AddVert(Point(x, y), c, Vector2.zero); vh.AddVert(Point(x + w, y), c, Vector2.zero);
            vh.AddVert(Point(x + w, y + h), c, Vector2.zero); vh.AddVert(Point(x, y + h), c, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
        private void Oval(VertexHelper vh, float x, float y, float rx, float ry, Color c)
        {
            int n = vh.currentVertCount; vh.AddVert(Point(x, y), c, Vector2.zero);
            for (int i = 0; i <= 32; i++) { float a = i * Mathf.PI * 2 / 32; vh.AddVert(Point(x + Mathf.Cos(a) * rx, y + Mathf.Sin(a) * ry), c, Vector2.zero); }
            for (int i = 0; i < 32; i++) vh.AddTriangle(n, n + i + 1, n + i + 2);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Color white = new Color(0.86f, 0.95f, 1, color.a);
            Color cyan = new Color(0.04f, 0.59f, 1, color.a), dark = new Color(0.08f, 0.15f, 0.22f, color.a);
            if (kind == GlyphKind.BlueCell)
            {
                Oval(vh, 0.5f, 0.17f, 0.3f, 0.08f, white); Quad(vh, 0.2f, 0.17f, 0.6f, 0.65f, cyan);
                Quad(vh, 0.27f, 0.2f, 0.07f, 0.6f, new Color(0.2f, 0.87f, 1, color.a));
                Quad(vh, 0.2f, 0.25f, 0.6f, 0.045f, white); Quad(vh, 0.2f, 0.73f, 0.6f, 0.045f, white);
                Oval(vh, 0.5f, 0.82f, 0.3f, 0.08f, white); Oval(vh, 0.5f, 0.84f, 0.16f, 0.045f, cyan);
            }
            else if (kind == GlyphKind.OrangeCell)
            {
                Quad(vh, 0.12f, 0.14f, 0.76f, 0.69f, dark);
                Quad(vh, 0.2f, 0.2f, 0.6f, 0.57f, new Color(1, 0.46f, 0.06f, color.a));
                Quad(vh, 0.1f, 0.13f, 0.8f, 0.1f, white); Quad(vh, 0.1f, 0.77f, 0.8f, 0.1f, white);
                Quad(vh, 0.25f, 0.87f, 0.13f, 0.08f, white); Quad(vh, 0.62f, 0.87f, 0.13f, 0.08f, white);
                Quad(vh, 0.45f, 0.35f, 0.1f, 0.26f, white); Quad(vh, 0.32f, 0.43f, 0.36f, 0.09f, white);
            }
            else if (kind == GlyphKind.Ring)
            {
                for (int i = 0; i < 60; i++)
                {
                    int n = vh.currentVertCount; float a = Mathf.PI / 2 - i * Mathf.PI * 2 / 60, b = a - Mathf.PI * 2 / 60 * 0.86f;
                    Color c = i / 60f < fill ? color : new Color(0.17f, 0.29f, 0.35f, color.a);
                    foreach (Vector2 p in new[] { new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.47f, new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 0.47f, new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 0.36f, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.36f })
                        vh.AddVert(Point(0.5f + p.x, 0.5f + p.y), c, Vector2.zero);
                    vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
                }
            }
            else if (kind == GlyphKind.Dock)
            {
                Quad(vh, 0.15f, 0.12f, 0.12f, 0.72f, color); Quad(vh, 0.73f, 0.12f, 0.12f, 0.72f, color);
                Quad(vh, 0.15f, 0.8f, 0.7f, 0.1f, color); Quad(vh, 0.42f, 0.22f, 0.16f, 0.5f, white);
            }
            else
            {
                int n = vh.currentVertCount;
                foreach (Vector2 p in new[] { new Vector2(.62f,.95f), new Vector2(.22f,.45f), new Vector2(.48f,.45f), new Vector2(.36f,.05f), new Vector2(.83f,.59f), new Vector2(.57f,.59f) })
                    vh.AddVert(Point(p.x, p.y), color, Vector2.zero);
                vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+5); vh.AddTriangle(n+2,n+3,n+4); vh.AddTriangle(n+2,n+4,n+5);
            }
        }
    }
}
