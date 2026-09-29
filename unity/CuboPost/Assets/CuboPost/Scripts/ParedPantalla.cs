using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CuboPost
{
    /// <summary>
    /// Una de las 4 pantallas LED de la fachada. Dibuja los puntitos y objetos flotantes, la
    /// composición de post. (en la pared de atrás) y las dedicatorias, sin que se pisen entre sí
    /// ni con las zonas bloqueadas (el túnel, la elipse central de la composición).
    /// El GameObject mira hacia adentro del cubo: su +Z es la dirección en la que mira el público.
    /// </summary>
    public class ParedPantalla : MonoBehaviour
    {
        [Header("Medidas de la pantalla (metros)")]
        public float largo = 13.5f;
        public float alto = 2.8f;
        [Tooltip("Metros por unidad de diseño del Figma. 0.001 → una nota XL mide 1,3 m de ancho y el texto ~6 cm.")]
        public float metrosPorUnidad = 0.001f;

        [Header("Notas")]
        [Tooltip("Ancho mínimo de la nota más chica (S), en metros. Las demás crecen en la misma proporción.")]
        public float anchoMinimoNota = 0.70f;
        [Tooltip("Segundos que cada nota queda visible con opacidad completa.")]
        public float segundosVisible = 15f;
        [Tooltip("Duración de la aparición (opacidad 0 → 100 %) y de la desaparición (100 → 0 %).")]
        public float segundosTransicion = 1f;

        [Header("Dónde pueden ir las notas (metros)")]
        public float margenArriba = 0.22f;
        public float margenAbajo = 0.3f;
        public float margenCostados = 0.25f;
        [Tooltip("Rectángulos tapados (desde abajo a la izquierda de la pantalla, en metros).")]
        public List<Rect> zonasBloqueadas = new List<Rect>();
        [Tooltip("Elipses tapadas: el rectángulo que las contiene (desde abajo a la izquierda, en metros).")]
        public List<Rect> zonasElipse = new List<Rect>();

        [Header("Decoración")]
        [Tooltip("Puntitos estilo SPOT! con la paleta sprout, por metro de pantalla.")]
        public float puntitosPorMetro = 2.2f;
        [Tooltip("Anillos, puntos y estrellas de post., por metro de pantalla.")]
        public float decoPostPorMetro = 1.3f;

        [Header("Composición post. (pared de atrás)")]
        public bool composicionCentral;
        [Tooltip("Ancho que ocupa la composición, de la estrella izquierda al último punto (metros).")]
        public float anchoComposicion = 3.8f;

        /// <summary>Se dispara cuando una nota sale de esta pantalla (terminó su tiempo, moderada o falta de lugar).</summary>
        public event Action<Nota> NotaRetirada;

        public int Cantidad => notas.Count;
        public bool Contiene(string mensaje) => notas.Exists(n => n.nota.mensaje == mensaje);

        /// <summary>Qué tan llena está: área de notas / área libre (sin túnel ni elipse).</summary>
        public float Ocupacion
        {
            get
            {
                float area = 0;
                foreach (var n in notas)
                {
                    var m = Medida(n);
                    area += m.x * m.y;
                }
                var util = AreaUtil;
                float libre = util.width * util.height;
                foreach (var z in zonasBloqueadas) libre -= Interseccion(util, AUnidades(z));
                foreach (var z in zonasElipse) libre -= Interseccion(util, AUnidades(z)) * Mathf.PI / 4f;
                return area / Mathf.Max(1f, libre);
            }
        }

        const float Separacion = 70f;
        RectTransform lienzo, capaDecoAtras, capaNotas, capaDecoAdelante;
        readonly List<NotaVisual> notas = new List<NotaVisual>();
        readonly Dictionary<NotaVisual, Vector2> destinos = new Dictionary<NotaVisual, Vector2>();
        readonly Dictionary<NotaVisual, Coroutine> ciclos = new Dictionary<NotaVisual, Coroutine>();
        bool inicializada;

        float U(float metros) => metros / metrosPorUnidad;
        float EscalaNota => Mathf.Max(1f, anchoMinimoNota / (PaletaPost.AnchoNota("S") * metrosPorUnidad));
        /// <summary>Tamaño de la nota en pantalla (unidades), ya escalada al mínimo de 70 cm.</summary>
        Vector2 Medida(NotaVisual nv) => nv.Tamano * EscalaNota;
        Rect AUnidades(Rect m) => new Rect(U(m.x), U(m.y), U(m.width), U(m.height));
        Vector2 TamanoLienzo => new Vector2(U(largo), U(alto));

        static float Interseccion(Rect a, Rect b)
        {
            float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return w > 0 && h > 0 ? w * h : 0f;
        }

        Rect AreaUtil
        {
            get
            {
                var t = TamanoLienzo;
                return new Rect(U(margenCostados), U(margenAbajo),
                    t.x - 2f * U(margenCostados), t.y - U(margenAbajo) - U(margenArriba));
            }
        }

        public void Inicializar()
        {
            if (inicializada) return;
            inicializada = true;

            var go = new GameObject("Pantalla (canvas)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            lienzo = (RectTransform)go.transform;
            lienzo.localPosition = new Vector3(0f, 0f, -0.012f);
            lienzo.localRotation = Quaternion.identity;
            lienzo.pivot = new Vector2(0.5f, 0.5f);
            lienzo.sizeDelta = TamanoLienzo;
            lienzo.localScale = Vector3.one * metrosPorUnidad;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;

            capaDecoAtras = Capa("Deco atrás");
            var capaComposicion = Capa("Composición post.");
            capaNotas = Capa("Notas");
            capaDecoAdelante = Capa("Deco adelante");

            int puntitos = Mathf.RoundToInt(puntitosPorMetro * largo);
            int decoPost = Mathf.RoundToInt(decoPostPorMetro * largo);
            CrearPuntitos(capaDecoAtras, puntitos);
            CrearDecoPost(capaDecoAtras, decoPost);
            CrearDecoPost(capaDecoAdelante, Mathf.Max(2, decoPost / 4), 0.7f);
            if (composicionCentral) CrearComposicion(capaComposicion);
        }

        RectTransform Capa(string nombre)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(lienzo, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        // ---------------- notas ----------------

        /// <summary>
        /// Muestra una nota: aparece (opacidad 0 → 100 %), queda <see cref="segundosVisible"/> y desaparece.
        /// </summary>
        public void Agregar(Nota n, float retardo = 0f)
        {
            Inicializar();
            var nv = NotaVisual.Crear(n, capaNotas);
            var tam = Medida(nv);

            if (!BuscarLugar(tam, out var centro))
            {
                // Falta lugar: se retiran primero los ejemplos y después las más viejas.
                for (int i = 0; i < 8 && !BuscarLugar(tam, out centro); i++)
                    if (!QuitarMasVieja()) break;
                if (!BuscarLugar(tam, out centro))
                    centro = LugarDeEmergencia(tam);
            }

            notas.Add(nv);
            destinos[nv] = centro;
            nv.rt.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(-5f, 5f));
            ciclos[nv] = StartCoroutine(Ciclo(nv, centro, retardo));
        }

        public bool Quitar(long id)
        {
            var nv = notas.Find(x => x.nota.id == id);
            if (nv == null) return false;
            Retirar(nv);
            return true;
        }

        public bool QuitarUnEjemplo()
        {
            var nv = notas.Find(x => x.nota.demo);
            if (nv == null) return false;
            Retirar(nv);
            return true;
        }

        bool QuitarMasVieja()
        {
            if (QuitarUnEjemplo()) return true;
            if (notas.Count == 0) return false;
            Retirar(notas[0]);
            return true;
        }

        void Retirar(NotaVisual nv)
        {
            if (ciclos.TryGetValue(nv, out var ciclo) && ciclo != null) StopCoroutine(ciclo);
            ciclos.Remove(nv);
            notas.Remove(nv);
            destinos.Remove(nv);
            NotaRetirada?.Invoke(nv.nota);
            StartCoroutine(Desaparecer(nv));
        }

        bool BuscarLugar(Vector2 tam, out Vector2 centro)
        {
            var area = AreaUtil;
            float w = tam.x + Separacion, h = tam.y + Separacion;
            centro = area.center;
            if (w > area.width || h > area.height) return false;

            for (int i = 0; i < 200; i++)
            {
                float x = UnityEngine.Random.Range(area.xMin + w / 2, area.xMax - w / 2);
                float y = UnityEngine.Random.Range(area.yMin + h / 2, area.yMax - h / 2);
                var r = new Rect(x - w / 2, y - h / 2, w, h);
                if (Choca(r)) continue;
                centro = new Vector2(x, y);
                return true;
            }
            return false;
        }

        /// <summary>Si no queda lugar libre, se acepta pisar otra nota pero nunca el túnel ni la elipse.</summary>
        Vector2 LugarDeEmergencia(Vector2 tam)
        {
            var area = AreaUtil;
            float w = Mathf.Min(tam.x, area.width), h = Mathf.Min(tam.y, area.height);
            var mejor = area.center;
            for (int i = 0; i < 200; i++)
            {
                var c = new Vector2(UnityEngine.Random.Range(area.xMin + w / 2, area.xMax - w / 2),
                                    UnityEngine.Random.Range(area.yMin + h / 2, area.yMax - h / 2));
                mejor = c;
                if (!TocaZona(new Rect(c.x - w / 2, c.y - h / 2, w, h))) break;
            }
            return mejor;
        }

        bool Choca(Rect r)
        {
            if (TocaZona(r)) return true;
            foreach (var kv in destinos)
            {
                var t = Medida(kv.Key);
                if (r.Overlaps(new Rect(kv.Value.x - t.x / 2, kv.Value.y - t.y / 2, t.x, t.y))) return true;
            }
            return false;
        }

        /// <summary>¿El rectángulo (unidades) pisa el túnel o la elipse central?</summary>
        bool TocaZona(Rect r)
        {
            foreach (var z in zonasBloqueadas)
                if (r.Overlaps(AUnidades(z))) return true;
            foreach (var z in zonasElipse)
            {
                // Se lleva todo al espacio donde la elipse es un círculo de radio 1.
                var e = AUnidades(z);
                float ax = e.width / 2f, ay = e.height / 2f;
                var c = e.center;
                float px = Mathf.Clamp(c.x, r.xMin, r.xMax), py = Mathf.Clamp(c.y, r.yMin, r.yMax);
                float dx = (px - c.x) / ax, dy = (py - c.y) / ay;
                if (dx * dx + dy * dy <= 1f) return true;
            }
            return false;
        }

        // ---------------- animaciones ----------------

        /// <summary>Aparece (opacidad 0 → 100 %), queda visible y desaparece (100 → 0 %).</summary>
        IEnumerator Ciclo(NotaVisual nv, Vector2 destino, float retardo)
        {
            nv.rt.anchoredPosition = destino;
            nv.rt.localScale = Vector3.one * EscalaNota;
            nv.grupo.alpha = 0f;
            if (retardo > 0) yield return new WaitForSeconds(retardo);

            for (float t = 0; t < 1f; t += Time.deltaTime / segundosTransicion)
            {
                nv.grupo.alpha = t;
                yield return null;
            }
            nv.grupo.alpha = 1f;

            yield return new WaitForSeconds(segundosVisible);
            ciclos.Remove(nv);
            if (notas.Contains(nv)) Retirar(nv);
        }

        IEnumerator Desaparecer(NotaVisual nv)
        {
            float desde = nv.grupo.alpha;
            for (float t = 0; t < 1f; t += Time.deltaTime / segundosTransicion)
            {
                if (nv == null) yield break;
                nv.grupo.alpha = desde * (1f - t);
                yield return null;
            }
            if (nv != null) Destroy(nv.gameObject);
        }

        // ---------------- decoración ----------------

        /// <summary>Puntitos de las pantallas SPOT!, con los colores de "sprout".</summary>
        void CrearPuntitos(RectTransform capa, int cantidad)
        {
            var lima = PaletaPost.SproutLima;
            var aqua = PaletaPost.SproutAqua;
            var violeta = PaletaPost.SproutVioleta;
            var variantes = new[]
            {
                RecursosPost.Puntito(lima, lima, aqua, 0.09f),       // lima con borde aqua
                RecursosPost.Puntito(lima, aqua, aqua, 0.09f),       // lima → aqua
                RecursosPost.Puntito(violeta, aqua, aqua, 0.09f),    // violeta → aqua
                RecursosPost.Puntito(violeta, violeta, aqua, 0.09f), // violeta con borde aqua
                RecursosPost.Puntito(aqua, aqua, aqua, 0f),          // gotita aqua
            };
            for (int i = 0; i < cantidad; i++)
            {
                int v = UnityEngine.Random.Range(0, variantes.Length);
                float tam = v == 4 ? UnityEngine.Random.Range(40f, 80f) : UnityEngine.Random.Range(60f, 150f);
                Deco(capa, variantes[v], tam, 1f, girar: false);
            }
        }

        /// <summary>Anillos crema, puntos verdes y estrellas de la pieza post.</summary>
        void CrearDecoPost(RectTransform capa, int cantidad, float escala = 1f)
        {
            for (int i = 0; i < cantidad; i++)
            {
                float dado = UnityEngine.Random.value;
                if (dado < 0.42f) Deco(capa, RecursosPost.AnilloCrema(0.1f), UnityEngine.Random.Range(60f, 180f) * escala, 1f, false);
                else if (dado < 0.76f) Deco(capa, RecursosPost.PuntoVerde(0.08f), UnityEngine.Random.Range(60f, 170f) * escala, 1f, false);
                else if (dado < 0.9f) Deco(capa, RecursosPost.EstrellaContorno, UnityEngine.Random.Range(260f, 420f) * escala, 1f, true);
                else Deco(capa, RecursosPost.EstrellaRellena, UnityEngine.Random.Range(280f, 440f) * escala, 1f, true);
            }
        }

        void Deco(RectTransform capa, Texture tex, float tam, float amplitud, bool girar)
        {
            var t = TamanoLienzo;
            Vector2 pos = Vector2.zero;
            for (int intento = 0; intento < 40; intento++)
            {
                pos = new Vector2(UnityEngine.Random.Range(tam, t.x - tam), UnityEngine.Random.Range(tam * 0.6f, t.y - tam * 0.6f));
                // Margen extra para que la flotación no los meta en la zona bloqueada.
                if (!TocaZona(new Rect(pos.x - tam / 2 - 90f, pos.y - tam / 2 - 90f, tam + 180f, tam + 180f))) break;
            }

            var go = new GameObject("Deco", typeof(RectTransform), typeof(RawImage), typeof(DecoFlotante));
            var rt = (RectTransform)go.transform;
            rt.SetParent(capa, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.sizeDelta = new Vector2(tam, tam);
            rt.anchoredPosition = pos;
            rt.localRotation = Quaternion.Euler(0, 0, girar ? UnityEngine.Random.Range(0f, 360f) : 0f);
            var raw = go.GetComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
            var f = go.GetComponent<DecoFlotante>();
            f.periodo = UnityEngine.Random.Range(6f, 11f);
            f.fase = UnityEngine.Random.value;
            f.amplitud = new Vector2(UnityEngine.Random.Range(20f, 60f), UnityEngine.Random.Range(50f, 110f)) * amplitud;
            f.giro = girar ? 12f : 0f;
        }

        // ---------------- composición post. ----------------

        /// <summary>
        /// Composición fija del frame "post." del Figma (1132 × 637), centrada y grande.
        /// Cada elemento hace un idle lento, a destiempo de los demás.
        /// Coordenadas del Figma: esquina superior izquierda del frame, en px.
        /// </summary>
        void CrearComposicion(RectTransform capa)
        {
            const float anchoFrame = 1132.4f, altoFrame = 637f;
            // Extensión visual de los elementos (de la estrella izquierda al último punto).
            const float xMin = 125.5f, xMax = 976f, yMin = 75.5f, yMax = 530f;
            float k = U(anchoComposicion) / (xMax - xMin);   // unidades de pantalla por px del Figma

            var go = new GameObject("Composición", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(capa, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(anchoFrame * k, altoFrame * k);
            rt.pivot = new Vector2(((xMin + xMax) / 2f) / anchoFrame, 1f - ((yMin + yMax) / 2f) / altoFrame);
            rt.anchoredPosition = Vector2.zero;

            const float trazo = 2.95f;   // borde crema de los Spot (OUTSIDE)
            // (x, y, diámetro, relleno) del Figma
            var spots = new (float x, float y, float d, bool relleno)[]
            {
                (902.4f, 119.7f, 31.9f, false), (225.3f, 112.1f, 15.3f, false), (957.9f, 394.6f, 15.3f, false),
                (686.5f, 436.5f, 15.3f, false), (432.9f, 460.6f, 15.3f, true),  (727.8f, 78.4f, 15.3f, true),
                (240.6f, 356.8f, 15.3f, false), (344.5f, 499.6f, 27.7f, false), (767.3f, 112.1f, 27.7f, false),
                (918.3f, 424.7f, 47.2f, true),  (200.5f, 436.5f, 55.4f, true),
            };
            int i = 0;
            foreach (var s in spots)
            {
                float exterior = s.d + 2f * trazo;
                var tex = s.relleno ? RecursosPost.PuntoVerde(trazo / exterior) : RecursosPost.AnilloCrema(trazo / exterior);
                Elemento(rt, "Spot", tex, s.x + s.d / 2f, s.y + s.d / 2f, exterior, exterior, k, 0f, i++);
            }
            // Estrellas (centro y tamaño de la imagen exportada, en px del Figma).
            Elemento(rt, "Estrella 7", RecursosPost.EstrellaRellena, 184.3f, 230.3f, 117.5f, 117.5f, k, 8f, i++);
            Elemento(rt, "Estrella 6", RecursosPost.EstrellaContorno, 883.1f, 273.2f, 99f, 99.8f, k, 10f, i++);
            // Logo: rectángulo de la imagen exportada (x, y, ancho, alto).
            Elemento(rt, "Logo post.", RecursosPost.Logo, 325.7f + 477.7f / 2f, 201.6f + 228.5f / 2f, 477.7f, 228.5f, k, 1.2f, i, 0.45f);
        }

        void Elemento(RectTransform padre, string nombre, Texture tex, float cx, float cy, float w, float h,
            float k, float giro, int indice, float amplitud = 1f)
        {
            if (tex == null) return;
            var go = new GameObject(nombre, typeof(RectTransform), typeof(RawImage), typeof(DecoFlotante));
            var rt = (RectTransform)go.transform;
            rt.SetParent(padre, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w * k, h * k);
            rt.anchoredPosition = new Vector2(cx * k, -cy * k);
            var raw = go.GetComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
            // Idle lento y a destiempo: cada elemento con su período y su fase.
            var f = go.GetComponent<DecoFlotante>();
            f.periodo = 10f + (indice * 1.37f) % 6f;
            f.fase = (indice * 0.618f) % 1f;
            f.amplitud = new Vector2(8f, 12f) * k * amplitud;
            f.giro = giro;
        }
    }
}
