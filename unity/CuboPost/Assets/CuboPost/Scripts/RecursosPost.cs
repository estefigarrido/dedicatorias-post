using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CuboPost
{
    /// <summary>
    /// Texturas (festón de las notas, puntos y estrellas del componente "Punto", composición post.)
    /// y tipografías.
    /// Se crean una sola vez y se comparten.
    /// </summary>
    public static class RecursosPost
    {
        // ---------------- tipografías ----------------
        static TMP_FontAsset mono, texto;

        /// <summary>JetBrains Mono ExtraBold: el "para:" de las notas.</summary>
        public static TMP_FontAsset FuenteMono => mono ??= Cargar("Fuentes/JetBrainsMono-ExtraBold");

        /// <summary>Reddit Sans SemiBold (mensaje), igual que en la web. Si falta, usa la de TextMeshPro.</summary>
        public static TMP_FontAsset FuenteTexto => texto ??= Cargar("Fuentes/RedditSans-SemiBold");

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
        static Texture2D logo, estrella12Verde, estrella12Crema, estrella7Verde;

        /// <summary>Logo post. (crema con borde verde), exportado del Figma en 4×.</summary>
        public static Texture2D Logo => logo ??= Resources.Load<Texture2D>("Composicion/logo-post-4x");

        /// <summary>Componente Punto: Marca=POST, Forma=Estrella 12, Color=Negro, Borde=Verde.</summary>
        public static Texture2D Estrella12Verde => estrella12Verde ??=
            Resources.Load<Texture2D>("Composicion/estrella-rellena") ?? CrearEstrella(256, 12, 8f, PaletaPost.Verde);

        /// <summary>Componente Punto: Marca=POST, Forma=Estrella 12, Color=Negro, Borde=Blanco Crema.</summary>
        public static Texture2D Estrella12Crema => estrella12Crema ??=
            Resources.Load<Texture2D>("Composicion/estrella-12-crema") ?? CrearEstrella(256, 12, 8f, PaletaPost.Crema);

        /// <summary>Componente Punto: Marca=POST, Forma=Estrella 7, Color=Negro, Borde=Verde.</summary>
        public static Texture2D Estrella7Verde => estrella7Verde ??=
            Resources.Load<Texture2D>("Composicion/estrella-7-rellena") ?? CrearEstrella(256, 7, 6f, PaletaPost.Verde);

        // ---------------- texturas generadas ----------------
        static Texture2D feston;
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        /// <summary>
        /// Un módulo del festón: 85 × 150 unidades, con un círculo de Ø63 recortado arriba
        /// (como los Ellipse del Subtract de tarjetita1..4). Se repite en horizontal.
        /// </summary>
        public static Texture2D Feston => feston ??= CrearFeston();

        /// <summary>
        /// Círculo del componente "Punto" del Figma (Marca=POST): relleno y borde con los colores de
        /// la marca. grosor = trazo / diámetro exterior; 0 = "Sin borde".
        /// </summary>
        public static Texture2D Punto(Color relleno, Color borde, float grosor) =>
            Cacheada($"punto{ColorUtility.ToHtmlStringRGB(relleno)}{ColorUtility.ToHtmlStringRGB(borde)}{Mathf.RoundToInt(grosor * 1000f)}",
                () => CrearCirculo(256, grosor, relleno, grosor > 0f ? borde : relleno));

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

        /// <summary>Círculo relleno cuyo borde exterior toca el borde de la textura (trazo hacia adentro).</summary>
        static Texture2D CrearCirculo(int n, float grosor, Color colorRelleno, Color colorTrazo)
        {
            var t = Nueva(n, n);
            var px = new Color[n * n];
            float c = n / 2f, exterior = n / 2f - 1f;
            float trazo = grosor > 0f ? Mathf.Max(1.5f, grosor * n) : 0f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                float dentro = Mathf.Clamp01(exterior - d + 0.5f);             // cobertura del disco completo
                float enTrazo = trazo > 0f ? Mathf.Clamp01(d - (exterior - trazo) + 0.5f) : 0f;   // 1 en el anillo del borde
                var col = Color.Lerp(colorRelleno, colorTrazo, enTrazo);
                col.a = dentro;
                px[y * n + x] = col;
            }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Estrella "flor" de respaldo (si faltan los PNG del Figma): negra con borde de color.</summary>
        static Texture2D CrearEstrella(int n, int puntas, float trazo, Color colorBorde)
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
                float adentro = Mathf.Clamp01(0.5f - d);
                var col = Color.Lerp(PaletaPost.Oscuro, colorBorde, enTrazo);
                col.a = Mathf.Max(enTrazo, adentro);
                px[y * n + x] = col;
            }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }
    }
}
