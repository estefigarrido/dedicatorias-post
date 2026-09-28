using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CuboPost
{
    /// <summary>
    /// Una notita en pantalla. Replica el auto layout de tarjetita1..4 (Figma), en unidades de diseño:
    ///   Nota     = vertical, gap 0, ancho fijo por tamaño (619 / 956 / 1044 / 1299), alto HUG
    ///   ├ Banda  = 150 de alto con festón (círculos Ø63 cada 85, el primero en x 24)
    ///   └ Cuerpo = vertical, padding 50 / 100 / 150 / 100, gap 50, alto HUG
    ///       ├ "para: …"  JetBrains Mono ExtraBold 64
    ///       └ mensaje    Inter Regular 64
    /// Relleno del color al 80 %.
    /// </summary>
    public class NotaVisual : MonoBehaviour
    {
        public Nota nota;
        public RectTransform rt;
        public CanvasGroup grupo;

        /// <summary>Tamaño final (unidades de diseño), ya calculado el alto HUG.</summary>
        public Vector2 Tamano => rt.rect.size;

        public static NotaVisual Crear(Nota n, Transform padre)
        {
            var tam = n.Tamano;
            float ancho = PaletaPost.AnchoNota(tam);
            // En tarjetita1 (S) el texto mide 442: se come 23 del padding derecho.
            int paddingDerecho = tam == "S" ? 77 : 100;
            var color = PaletaPost.ColorNota(n.color);
            color.a = PaletaPost.OpacidadNota;

            var go = new GameObject($"Nota {n.id}", typeof(RectTransform), typeof(CanvasGroup),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(NotaVisual));
            var rt = (RectTransform)go.transform;
            rt.SetParent(padre, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(ancho, 1000f);

            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 0;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fit = go.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Banda con festón.
            var banda = new GameObject("Banda", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
            banda.transform.SetParent(rt, false);
            var raw = banda.GetComponent<RawImage>();
            raw.texture = RecursosPost.Feston;
            raw.color = color;
            raw.raycastTarget = false;
            // Primer círculo centrado en x = 24 + 31.5 = 55.5; en el módulo está en 42.5 → corrimiento de -13.
            raw.uvRect = new Rect(-13f / 85f, 0f, ancho / 85f, 1f);
            var leBanda = banda.GetComponent<LayoutElement>();
            leBanda.minHeight = leBanda.preferredHeight = 150f;

            // Cuerpo con los textos.
            var cuerpo = new GameObject("Cuerpo", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            cuerpo.transform.SetParent(rt, false);
            var img = cuerpo.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var vlgCuerpo = cuerpo.GetComponent<VerticalLayoutGroup>();
            vlgCuerpo.padding = new RectOffset(100, paddingDerecho, 50, 150);
            vlgCuerpo.spacing = 50;
            vlgCuerpo.childControlWidth = true;
            vlgCuerpo.childControlHeight = true;
            vlgCuerpo.childForceExpandWidth = true;
            vlgCuerpo.childForceExpandHeight = false;

            Texto(cuerpo.transform, "Para", "para: " + (n.para ?? ""), RecursosPost.FuenteMono, 1.05f);
            Texto(cuerpo.transform, "Mensaje", n.mensaje ?? "", RecursosPost.FuenteTexto, 1.0f);

            var nv = go.GetComponent<NotaVisual>();
            nv.nota = n;
            nv.rt = rt;
            nv.grupo = go.GetComponent<CanvasGroup>();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            return nv;
        }

        static void Texto(Transform padre, string nombre, string contenido, TMP_FontAsset fuente, float interlineado)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(padre, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.richText = false;              // el texto viene de la gente: nada de etiquetas
            t.font = fuente;
            t.fontSize = 64f;
            t.color = PaletaPost.Oscuro;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.lineSpacing = (interlineado - 1f) * 100f;
            t.raycastTarget = false;
            t.text = contenido;
        }
    }
}
