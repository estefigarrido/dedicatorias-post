using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CuboPost.EditorTools
{
    /// <summary>
    /// Texturas del entorno, pintadas por código (no hay imágenes externas): adoquines, piedra del
    /// murete, césped, tierra, polvo de ladrillo, hormigón, baldosas, asfalto y follaje. Todas se
    /// repiten sin costura. Las que tienen relieve traen además su mapa de normales.
    /// </summary>
    public static partial class ConstruirEntorno
    {
        // =====================================================================
        //  Ruido que se repite sin costura
        // =====================================================================

        static float Azar(int x, int y, int semilla)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)(semilla * 362437);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        /// <summary>Ruido suave de 0 a 1 con <paramref name="periodo"/> celdas por lado: u y v van de 0 a 1 y los bordes empalman.</summary>
        static float Ruido(float u, float v, int periodo, int semilla)
        {
            float x = u * periodo, y = v * periodo;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int xa = ((x0 % periodo) + periodo) % periodo, xb = (xa + 1) % periodo;
            int ya = ((y0 % periodo) + periodo) % periodo, yb = (ya + 1) % periodo;
            float a = Azar(xa, ya, semilla), b = Azar(xb, ya, semilla), c = Azar(xa, yb, semilla), d = Azar(xb, yb, semilla);
            return Mathf.LerpUnclamped(Mathf.LerpUnclamped(a, b, fx), Mathf.LerpUnclamped(c, d, fx), fy);
        }

        /// <summary>Suma de ruidos cada vez más finos (de 0 a 1 aprox.).</summary>
        static float Nubes(float u, float v, int periodo, int octavas, int semilla)
        {
            float suma = 0f, peso = 0.5f, total = 0f;
            for (int o = 0; o < octavas; o++)
            {
                suma += Ruido(u, v, periodo, semilla + o * 31) * peso;
                total += peso;
                peso *= 0.5f;
                periodo *= 2;
            }
            return suma / total;
        }

        static float Escalon(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static Color Gris(float g) => new Color(g, g, g, 1f);
        static Color Mezcla(Color a, Color b, float t) => Color.Lerp(a, b, t);

        // =====================================================================
        //  Creación y guardado
        // =====================================================================

        // Texturas ya pintadas en esta pasada, por nombre: si se piden dos veces, es la misma.
        static readonly Dictionary<string, Texture2D> texturas = new Dictionary<string, Texture2D>();

        static Texture2D Pintar(string nombre, int ancho, int alto, System.Func<float, float, Color> pintor, bool repetirV = true)
        {
            if (texturas.TryGetValue(nombre, out var hecha)) return hecha;
            var t = new Texture2D(ancho, alto, TextureFormat.RGBA32, true) { name = nombre, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            t.wrapModeU = TextureWrapMode.Repeat;
            t.wrapModeV = repetirV ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            var px = new Color[ancho * alto];
            for (int y = 0; y < alto; y++)
            for (int x = 0; x < ancho; x++)
            {
                var c = pintor((x + 0.5f) / ancho, (y + 0.5f) / alto);
                c.a = 1f;
                px[y * ancho + x] = c;
            }
            t.SetPixels(px);
            t.Apply(true);
            // Comprimida ocupa mucho menos en el proyecto. Si esta versión de Unity no lo permite, queda sin comprimir.
            try { EditorUtility.CompressTexture(t, TextureFormat.DXT1, TextureCompressionQuality.Normal); }
            catch (System.Exception) { }
            texturas[nombre] = t;
            return Guardar(t, nombre + ".asset");
        }

        /// <summary>
        /// Mapa de normales a partir de un relieve (0 a 1). <paramref name="fuerza"/>: cuánto se inclina
        /// la normal. Queda sin comprimir para que no aparezcan bloques en la luz.
        /// </summary>
        static Texture2D PintarNormales(string nombre, int ancho, int alto, System.Func<float, float, float> relieve, float fuerza)
        {
            if (texturas.TryGetValue(nombre, out var hecha)) return hecha;
            var t = new Texture2D(ancho, alto, TextureFormat.RGBA32, true, true) { name = nombre, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            var h = new float[ancho * alto];
            for (int y = 0; y < alto; y++)
            for (int x = 0; x < ancho; x++)
                h[y * ancho + x] = relieve((x + 0.5f) / ancho, (y + 0.5f) / alto);
            var px = new Color[ancho * alto];
            for (int y = 0; y < alto; y++)
            for (int x = 0; x < ancho; x++)
            {
                float dx = h[y * ancho + (x + 1) % ancho] - h[y * ancho + (x + ancho - 1) % ancho];
                float dy = h[((y + 1) % alto) * ancho + x] - h[((y + alto - 1) % alto) * ancho + x];
                var n = new Vector3(-dx * fuerza, -dy * fuerza, 1f).normalized;
                px[y * ancho + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
            t.SetPixels(px);
            t.Apply(true);
            texturas[nombre] = t;
            return Guardar(t, nombre + ".asset");
        }

        // =====================================================================
        //  Adoquines de la explanada (mosaico de 2 × 2 m: hiladas de 10 cm, piezas de 20 cm)
        // =====================================================================

        const int AdoquinHiladas = 20, AdoquinPorHilada = 10;

        /// <summary>Cuánto "pieza" (1) o "junta" (0) hay en ese punto, y qué pieza es.</summary>
        static float Adoquin(float u, float v, out int fila, out int columna)
        {
            float fy = v * AdoquinHiladas;
            fila = Mathf.FloorToInt(fy) % AdoquinHiladas;
            float fx = u * AdoquinPorHilada + (fila % 2) * 0.5f + (Azar(fila, 7, 11) - 0.5f) * 0.12f;
            columna = ((Mathf.FloorToInt(fx) % AdoquinPorHilada) + AdoquinPorHilada) % AdoquinPorHilada;
            float lx = fx - Mathf.Floor(fx), ly = fy - Mathf.Floor(fy);
            // Distancia al borde de la pieza (metros), con las esquinas redondeadas: rectángulo de
            // 20 × 10 cm con radio de 2 cm.
            const float r = 0.02f;
            float qx = Mathf.Abs(lx - 0.5f) * 0.2f - (0.1f - r), qy = Mathf.Abs(ly - 0.5f) * 0.1f - (0.05f - r);
            float afuera = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            float d = r - afuera - Mathf.Min(Mathf.Max(qx, qy), 0f);
            return Escalon(0.004f, 0.013f, d);
        }

        static Texture2D TexturaAdoquines()
        {
            return Pintar("Adoquines", 512, 512, (u, v) =>
            {
                int fila, col;
                float pieza = Adoquin(u, v, out fila, out col);
                float tono = 0.53f + 0.11f * Azar(fila, col, 3);
                float grano = Nubes(u, v, 96, 3, 5) - 0.5f;
                float mancha = Nubes(u, v, 4, 3, 9) - 0.5f;
                float g = tono + grano * 0.08f + mancha * 0.03f;
                var piedra = new Color(g * (1.03f + 0.04f * (Azar(fila, col, 13) - 0.3f)), g * 0.99f, g * (0.87f + 0.04f * Azar(fila, col, 17)));
                var junta = new Color(0.36f, 0.35f, 0.32f) * (0.85f + 0.3f * Nubes(u, v, 64, 2, 21));
                return Mezcla(junta, piedra, pieza);
            });
        }

        static Texture2D NormalesAdoquines()
        {
            return PintarNormales("Adoquines normales", 512, 512, (u, v) =>
            {
                int fila, col;
                float pieza = Adoquin(u, v, out fila, out col);
                return pieza * (0.82f + 0.18f * Azar(fila, col, 29)) + (Nubes(u, v, 96, 3, 5) - 0.5f) * 0.22f * pieza;
            }, 2.2f);
        }

        // =====================================================================
        //  Piedra del murete (mosaico de 1,20 m de largo × 0,50 m de alto: dos hiladas trabadas)
        // =====================================================================

        static float Bloque(float u, float v, out int fila, out int columna)
        {
            float fy = v * 2f;
            fila = Mathf.FloorToInt(fy) % 2;
            float fx = u * 2f + fila * 0.5f;
            columna = ((Mathf.FloorToInt(fx) % 2) + 2) % 2;
            float lx = fx - Mathf.Floor(fx), ly = fy - Mathf.Floor(fy);
            float dx = Mathf.Min(lx, 1f - lx) * 0.6f, dy = Mathf.Min(ly, 1f - ly) * 0.25f;
            return Escalon(0.003f, 0.012f, Mathf.Min(dx, dy));
        }

        static Texture2D TexturaPiedra()
        {
            return Pintar("Piedra del murete", 512, 256, (u, v) =>
            {
                int fila, col;
                float bloque = Bloque(u, v, out fila, out col);
                float tono = 0.36f + 0.07f * Azar(fila, col, 41);
                float veta = Nubes(u, v * 0.5f, 6, 4, 43) - 0.5f;
                float grano = Azar(Mathf.FloorToInt(u * 512f), Mathf.FloorToInt(v * 256f), 47) - 0.5f;
                float fino = Nubes(u, v * 0.5f, 128, 2, 53) - 0.5f;
                float g = tono + veta * 0.14f + grano * 0.07f + fino * 0.10f;
                var piedra = new Color(g * 0.99f, g, g * 1.03f);
                var junta = new Color(0.17f, 0.17f, 0.17f);
                return Mezcla(junta, piedra, bloque);
            });
        }

        static Texture2D NormalesPiedra()
        {
            return PintarNormales("Piedra del murete normales", 512, 256, (u, v) =>
            {
                int fila, col;
                float bloque = Bloque(u, v, out fila, out col);
                return bloque * 0.75f + (Nubes(u, v * 0.5f, 24, 4, 59) - 0.5f) * 0.5f * bloque;
            }, 3.2f);
        }

        // =====================================================================
        //  Césped, tierra y senderos
        // =====================================================================

        static Color ColorCesped(float u, float v, float seco)
        {
            float parche = Nubes(u, v, 5, 4, 61);
            float brizna = Ruido(u, v * 0.35f, 220, 67) * 0.6f + Ruido(u * 0.5f, v, 300, 71) * 0.4f;
            float fino = Azar(Mathf.FloorToInt(u * 512f), Mathf.FloorToInt(v * 512f), 73);
            var verde = Mezcla(new Color(0.25f, 0.34f, 0.14f), new Color(0.42f, 0.50f, 0.22f), parche * 0.7f + brizna * 0.5f);
            var amarillo = new Color(0.58f, 0.55f, 0.30f);
            float quemado = Escalon(0.45f, 0.80f, Nubes(u, v, 7, 3, 79)) * 0.55f + seco;
            var c = Mezcla(verde, amarillo, quemado);
            return c * (0.90f + 0.20f * fino);
        }

        static Texture2D TexturaCesped() => Pintar("Césped", 512, 512, (u, v) => ColorCesped(u, v, 0f));

        /// <summary>
        /// Césped del anillo: abajo (v = 0, pegado al murete) es tierra pisada; después viene pasto
        /// seco y recién arriba (v = 1, lejos del murete) pasto verde. Se repite solo a lo largo (u).
        /// </summary>
        static Texture2D TexturaCespedAnillo()
        {
            return Pintar("Césped del anillo", 512, 256, (u, v) =>
            {
                float borde = (Nubes(u, v * 0.5f, 6, 4, 83) - 0.5f) * 0.42f;
                float tierra = 1f - Escalon(0.10f, 0.34f, v + borde);
                float seco = 1f - Escalon(0.30f, 0.80f, v + borde * 0.8f);
                var pasto = ColorCesped(u, v * 0.5f, seco * 0.75f);
                float piedritas = Azar(Mathf.FloorToInt(u * 512f), Mathf.FloorToInt(v * 256f), 89);
                float mancha = Nubes(u, v * 0.5f, 10, 3, 97);
                var suelo = Mezcla(new Color(0.50f, 0.45f, 0.34f), new Color(0.62f, 0.57f, 0.44f), mancha) * (0.9f + 0.2f * piedritas);
                return Mezcla(pasto, suelo, tierra);
            }, false);
        }

        /// <summary>Suelo del jardín donde no hay césped cortado: tierra con pasto ralo y matas.</summary>
        static Texture2D TexturaPastoRalo()
        {
            return Pintar("Pasto ralo", 256, 256, (u, v) =>
            {
                float mata = Nubes(u, v, 8, 4, 101);
                float fino = Azar(Mathf.FloorToInt(u * 256f), Mathf.FloorToInt(v * 256f), 103);
                var tierra = new Color(0.47f, 0.43f, 0.30f);
                var oliva = new Color(0.36f, 0.40f, 0.20f);
                var verde = new Color(0.24f, 0.33f, 0.14f);
                var c = Mezcla(tierra, oliva, Escalon(0.35f, 0.55f, mata));
                c = Mezcla(c, verde, Escalon(0.60f, 0.78f, mata));
                return c * (0.88f + 0.24f * fino);
            });
        }

        static Texture2D TexturaPolvoDeLadrillo()
        {
            return Pintar("Polvo de ladrillo", 256, 256, (u, v) =>
            {
                float mancha = Nubes(u, v, 5, 4, 107);
                float fino = Azar(Mathf.FloorToInt(u * 256f), Mathf.FloorToInt(v * 256f), 109);
                var c = Mezcla(new Color(0.60f, 0.38f, 0.25f), new Color(0.72f, 0.52f, 0.36f), mancha);
                return c * (0.92f + 0.16f * fino);
            });
        }

        static Texture2D TexturaMantillo()
        {
            return Pintar("Mantillo", 256, 256, (u, v) =>
            {
                float astilla = Ruido(u, v * 0.4f, 90, 113) * 0.5f + Ruido(u * 0.4f, v, 110, 127) * 0.5f;
                float mancha = Nubes(u, v, 6, 3, 131);
                var c = Mezcla(new Color(0.13f, 0.10f, 0.08f), new Color(0.32f, 0.24f, 0.17f), astilla * 0.7f + mancha * 0.3f);
                return c;
            });
        }

        /// <summary>Hormigón claro de los senderos (mosaico de 2 m, con el corte de las juntas).</summary>
        static Texture2D TexturaHormigon()
        {
            return Pintar("Hormigón", 256, 256, (u, v) =>
            {
                float mancha = Nubes(u, v, 4, 4, 137);
                float fino = Azar(Mathf.FloorToInt(u * 256f), Mathf.FloorToInt(v * 256f), 139);
                float g = 0.66f + mancha * 0.10f + (fino - 0.5f) * 0.06f;
                float junta = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) * 2f;   // metros al corte
                var c = new Color(g * 1.03f, g, g * 0.90f);
                return Mezcla(c * 0.72f, c, Escalon(0.004f, 0.014f, junta));
            });
        }

        // =====================================================================
        //  Baldosas de vereda (mosaico de 1,60 m: 4 × 4 baldosas de 40 cm con 9 panes cada una)
        // =====================================================================

        static float Baldosa(float u, float v, out int fila, out int columna, out float panes)
        {
            float fx = u * 4f, fy = v * 4f;
            columna = Mathf.FloorToInt(fx) % 4; fila = Mathf.FloorToInt(fy) % 4;
            float lx = fx - Mathf.Floor(fx), ly = fy - Mathf.Floor(fy);
            float d = Mathf.Min(Mathf.Min(lx, 1f - lx), Mathf.Min(ly, 1f - ly)) * 0.4f;
            // Ranuras de los 9 panes (a un tercio y dos tercios).
            float px = Mathf.Abs(lx * 3f - Mathf.Round(lx * 3f)) / 3f * 0.4f, py = Mathf.Abs(ly * 3f - Mathf.Round(ly * 3f)) / 3f * 0.4f;
            panes = Escalon(0.002f, 0.008f, Mathf.Min(px, py));
            return Escalon(0.002f, 0.007f, d);
        }

        static Texture2D TexturaBaldosas()
        {
            return Pintar("Baldosas", 512, 512, (u, v) =>
            {
                int fila, col; float panes;
                float baldosa = Baldosa(u, v, out fila, out col, out panes);
                float fino = Azar(Mathf.FloorToInt(u * 512f), Mathf.FloorToInt(v * 512f), 149);
                float g = 0.72f + 0.035f * Azar(fila, col, 151) + (Nubes(u, v, 4, 3, 157) - 0.5f) * 0.03f + (fino - 0.5f) * 0.04f;
                var c = new Color(g, g, g * 0.96f);
                c = Mezcla(c * 0.93f, c, panes);
                return Mezcla(new Color(0.50f, 0.50f, 0.48f), c, baldosa);
            });
        }

        static Texture2D NormalesBaldosas()
        {
            return PintarNormales("Baldosas normales", 512, 512, (u, v) =>
            {
                int fila, col; float panes;
                float baldosa = Baldosa(u, v, out fila, out col, out panes);
                return baldosa * (0.6f + 0.4f * panes);
            }, 1.6f);
        }

        // =====================================================================
        //  Asfalto, granito claro y follaje
        // =====================================================================

        static Texture2D TexturaAsfalto()
        {
            return Pintar("Asfalto", 256, 256, (u, v) =>
            {
                float mancha = Nubes(u, v, 3, 4, 163);
                float arido = Azar(Mathf.FloorToInt(u * 256f), Mathf.FloorToInt(v * 256f), 167);
                float g = 0.27f + mancha * 0.07f + (arido - 0.5f) * 0.07f;
                return new Color(g, g, g * 1.04f);
            });
        }

        /// <summary>Granito claro de las bandas de la explanada y de los cordones (mosaico de 1 m, con junta).</summary>
        static Texture2D TexturaGranitoClaro()
        {
            return Pintar("Granito claro", 256, 256, (u, v) =>
            {
                float pinta = Azar(Mathf.FloorToInt(u * 256f), Mathf.FloorToInt(v * 256f), 173);
                float mancha = Nubes(u, v, 5, 3, 179);
                float g = 0.62f + mancha * 0.08f + (pinta - 0.5f) * 0.12f;
                float junta = Mathf.Min(v, 1f - v);
                var c = new Color(g * 1.02f, g, g * 0.95f);
                return Mezcla(c * 0.6f, c, Escalon(0.004f, 0.012f, junta));
            });
        }

        /// <summary>Follaje: manchas de hojas a la luz y a la sombra, en dos tamaños (0 = hueco oscuro, 1 = hoja al sol).</summary>
        static float Hojas(float u, float v)
        {
            float grande = Escalon(0.38f, 0.62f, Ruido(u, v, 14, 181));
            float chico = Escalon(0.35f, 0.65f, Ruido(u, v, 44, 191));
            float fino = Ruido(u, v, 110, 197);
            return Mathf.Clamp01(grande * 0.45f + chico * 0.40f + fino * 0.25f);
        }

        static Texture2D TexturaFollaje()
        {
            return Pintar("Follaje", 256, 256, (u, v) =>
            {
                float h = Hojas(u, v);
                var c = Mezcla(new Color(0.30f, 0.34f, 0.26f), new Color(1.0f, 1.0f, 0.86f), h);
                return c;
            });
        }

        static Texture2D NormalesFollaje() => PintarNormales("Follaje normales", 256, 256, (u, v) => Hojas(u, v), 2.0f);
    }
}
