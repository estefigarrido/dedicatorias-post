using UnityEngine;
using UnityEngine.UI;

namespace CuboPost
{
    /// <summary>
    /// Contorno vectorial sin relleno para la UI: un círculo (puntas = 0) o una estrella "flor" de
    /// puntas redondeadas como las del componente "Punto" del Figma (puntas = 12 o 7, interior = radio
    /// interior / radio exterior). El tamaño es el ancho del RectTransform; grosor en unidades del lienzo.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class ContornoUI : MaskableGraphic
    {
        public int puntas;
        [Range(0.1f, 1f)] public float interior = 0.55f;
        public float grosor = 6f;
        public int segmentos = 240;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            float R = Mathf.Min(r.width, r.height) / 2f;
            var c = (Color32)color;
            for (int i = 0; i <= segmentos; i++)
            {
                float t = i * Mathf.PI * 2f / segmentos;
                // Radio en este ángulo: liso para el círculo, ondulado (puntas redondeadas) para la estrella.
                float radio = puntas > 0 ? R * Mathf.Lerp(interior, 1f, 0.5f + 0.5f * Mathf.Cos(puntas * t + Mathf.PI / 2f)) : R;
                var d = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
                vh.AddVert(r.center + d * radio, c, Vector2.zero);
                vh.AddVert(r.center + d * Mathf.Max(0f, radio - grosor), c, Vector2.zero);
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
