using UnityEngine;
using UnityEngine.UI;

namespace CuboPost
{
    /// <summary>
    /// Anillo (o disco) vectorial para la UI: nítido a cualquier tamaño, a diferencia de una textura.
    /// El diámetro exterior es el ancho del RectTransform; grosor en unidades del lienzo
    /// (grosor ≥ radio = disco lleno). Respeta los RectMask2D.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class AnilloUI : MaskableGraphic
    {
        [SerializeField] float grosor = 7f;
        [SerializeField] int segmentos = 180;

        public float Grosor
        {
            get => grosor;
            set { grosor = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            float exterior = Mathf.Min(r.width, r.height) / 2f;
            float interior = Mathf.Max(0f, exterior - grosor);
            var centro = r.center;
            var c = (Color32)color;
            for (int i = 0; i <= segmentos; i++)
            {
                float t = i * Mathf.PI * 2f / segmentos;
                var d = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
                vh.AddVert(centro + d * exterior, c, Vector2.zero);
                vh.AddVert(centro + d * interior, c, Vector2.zero);
            }
            for (int i = 0; i < segmentos; i++)
            {
                int k = i * 2;
                vh.AddTriangle(k, k + 2, k + 1);
                vh.AddTriangle(k + 1, k + 2, k + 3);
            }
        }
    }
}
