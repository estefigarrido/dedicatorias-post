using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CuboPost
{
    /// <summary>
    /// Texturas (festón de las notas, objetos flotantes, composición post.) y tipografías.
    /// Se crean una sola vez y se comparten.
    /// </summary>
    public static class RecursosPost
    {
        // ---------------- tipografías ----------------
        static TMP_FontAsset mono, texto;

        /// <summary>JetBrains Mono ExtraBold: el "para:" de las notas.</summary>
        public static TMP_FontAsset FuenteMono => mono ??= Cargar("Fuentes/JetBrainsMono-ExtraBold");

        /// <summary>Inter Regular (mensaje). Si no está en Resources/Fuentes, usa la de TextMeshPro.</summary>
        public static TMP_FontAsset FuenteTexto => texto ??= Cargar("Fuentes/Inter-Regular");

        static TMP_FontAsset Cargar(string ruta)
        {
            var fuente = Resources.Load<Font>(ruta);
            if (fuente != null)
            {
                var asset = TMP_FontAsset.CreateFontAsset(fuente);
                if (asset != null)
                {
                    asset.name = fuente.name;
                    return asset;
                }
            }
            return TMP_Settings.defaultFontAsset;
        }

        // ---------------- imágenes exportadas del Figma (4×) ----------------
        static Texture2D logo, estrellaContorno, estrellaRellena;

        /// <summary>Logo post. (crema con borde verde), exportado del Figma en 4×.</summary>
        public static Texture2D Logo => logo ??= Resources.Load<Texture2D>("Composicion/logo-post-4x");

        /// <summary>Estrella de 7 puntas con contorno verde (Star 6 del Figma).</summary>
        public static Texture2D EstrellaContorno => estrellaContorno ??=
            Resources.Load<Texture2D>("Composicion/estrella-contorno") ?? CrearEstrella(256, 7, 6f, false);

        /// <summary>Estrella de 12 puntas rellena de oscuro con borde verde (Star 7 del Figma).</summary>
        public static Texture2D EstrellaRellena => estrellaRellena ??=
            Resources.Load<Texture2D>("Composicion/estrella-rellena") ?? CrearEstrella(256, 12, 8f, true);

        // ---------------- texturas generadas ----------------
        static Texture2D feston;
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        /// <summary>
        /// Un módulo del festón: 85 × 150 unidades, con un círculo de Ø63 recortado arriba
        /// (como los Ellipse del Subtract de tarjetita1..4). Se repite en horizontal.
        /// </summary>
        public static Texture2D Feston => feston ??= CrearFeston();

        /// <summary>Anillo crema (Spot sin relleno del Figma). grosor = trazo / diámetro exterior.</summary>
        public static Texture2D AnilloCrema(float grosor = 0.1f) =>
            Cacheada($"anillo{grosor:0.000}", () => CrearCirculo(256, grosor, PaletaPost.Crema, PaletaPost.Crema, false));

        /// <summary>Punto verde con borde crema (Spot relleno del Figma).</summary>
        public static Texture2D PuntoVerde(float grosor = 0.08f) =>
            Cacheada($"punto{grosor:0.000}", () => CrearCirculo(256, grosor, PaletaPost.Verde, PaletaPost.Crema, true));

        /// <summary>
        /// Puntito estilo SPOT! con la paleta "sprout": relleno en degradé vertical (arriba → abajo)
        /// y borde. grosor = 0 → sin borde.
        /// </summary>
        public static Texture2D Puntito(Color arriba, Color abajo, Color borde, float grosor) =>
            Cacheada($"pt{ColorUtility.ToHtmlStringRGB(arriba)}{ColorUtility.ToHtmlStringRGB(abajo)}{ColorUtility.ToHtmlStringRGB(borde)}{grosor:0.000}",
                () => CrearPuntito(128, arriba, abajo, borde, grosor));

        static Texture2D Cacheada(string clave, System.Func<Texture2D> crear)
        {
            if (!cache.TryGetValue(clave, out var t) || t == null)
            {
                t = crear();
                t.name = clave;
                cache[clave] = t;
            }
            return t;
        }

        static Texture2D Nueva(int w, int h, TextureWrapMode wrapU = TextureWrapMode.Clamp)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                wrapModeU = wrapU,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
        }

        static Texture2D CrearFeston()
        {
            const float esc = 4f;                 // 4 px por unidad de diseño
            int w = Mathf.RoundToInt(85 * esc), h = Mathf.RoundToInt(150 * esc);
            var t = Nueva(w, h, TextureWrapMode.Repeat);
            var px = new Color32[w * h];
            float cx = 42.5f * esc, cy = h + 0.5f * esc, r = 31.5f * esc;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float a = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) - r + 0.5f);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Círculo cuyo borde exterior toca el borde de la textura (trazo hacia adentro).</summary>
        static Texture2D CrearCirculo(int n, float grosor, Color colorRelleno, Color colorTrazo, bool relleno)
        {
            var t = Nueva(n, n);
            var px = new Color[n * n];
            float c = n / 2f, exterior = n / 2f - 1f, trazo = Mathf.Max(1.5f, grosor * n);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                float dentro = Mathf.Clamp01(exterior - d + 0.5f);             // cobertura del disco completo
                float enTrazo = Mathf.Clamp01(d - (exterior - trazo) + 0.5f);   // 1 en el anillo del borde
                var col = Color.Lerp(colorRelleno, colorTrazo, enTrazo);
                col.a = dentro * (relleno ? 1f : enTrazo);
                px[y * n + x] = col;
            }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        static Texture2D CrearPuntito(int n, Color arriba, Color abajo, Color borde, float grosor)
        {
            var t = Nueva(n, n);
            var px = new Color[n * n];
            float c = n / 2f, exterior = n / 2f - 1f, trazo = grosor * n;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                float dentro = Mathf.Clamp01(exterior - d + 0.5f);
                float enTrazo = trazo > 0f ? Mathf.Clamp01(d - (exterior - trazo) + 0.5f) : 0f;
                // Degradé suave de arriba (y alto) hacia abajo, con un poco de curva.
                float k = Mathf.SmoothStep(0f, 1f, 1f - (y + 0.5f) / n);
                var col = Color.Lerp(Color.Lerp(arriba, abajo, k), borde, enTrazo);
                col.a = dentro;
                px[y * n + x] = col;
            }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Estrella "flor" de respaldo (si faltan los PNG del Figma).</summary>
        static Texture2D CrearEstrella(int n, int puntas, float trazo, bool relleno)
        {
            var t = Nueva(n, n);
            var px = new Color[n * n];
            float c = n / 2f, radio = n * 0.40f, amp = n * 0.05f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy) - (radio + amp * Mathf.Cos(puntas * Mathf.Atan2(dy, dx)));
                float enTrazo = Mathf.Clamp01(trazo / 2f + 0.5f - Mathf.Abs(d));
                float adentro = relleno ? Mathf.Clamp01(0.5f - d) : 0f;
                var col = Color.Lerp(PaletaPost.Oscuro, PaletaPost.Verde, enTrazo);
                col.a = Mathf.Max(enTrazo, adentro);
                px[y * n + x] = col;
            }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }
    }
}
