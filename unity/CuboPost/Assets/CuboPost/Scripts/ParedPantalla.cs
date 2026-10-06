using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CuboPost
{
    /// <summary>
    /// Una de las 4 pantallas LED de la fachada. Dibuja los puntos y estrellas del componente "Punto"
    /// (marca POST) dispersos por la pantalla, la gráfica de post. (en las dos paredes largas) y las
    /// dedicatorias, sin que se pisen entre sí ni con las zonas bloqueadas (la entrada y la elipse
    /// central de la gráfica, que es la zona roja del Figma).
    /// El GameObject mira hacia adentro del cubo: su +Z es la dirección en la que mira el público.
    /// También se dibuja sin apretar Play (vista previa quieta, sin notas): esa vista no se guarda
    /// en la escena, se rearma sola cada vez que se abre.
    /// </summary>
    [ExecuteAlways]
    public class ParedPantalla : MonoBehaviour
    {
        // Frame de la gráfica en el Figma (página "Pantallas POST", nodo 1622:4941), en px.
        // El alto del frame equivale al alto de la pantalla; la elipse es la zona sin notas.
        public const float FigmaAnchoFrame = 3007f;
        public const float FigmaAltoFrame = 637f;
        public const float FigmaAnchoElipse = 1286f;

        [Header("Medidas de la pantalla (metros)")]
        public float largo = 20f;
        public float alto = 3.3f;
        [Tooltip("Metros por unidad de diseño del Figma. 0.001 → una nota XL mide 1,3 m de ancho y el texto ~6 cm.")]
        public float metrosPorUnidad = 0.001f;

        [Header("Notas")]
        [Tooltip("Si esta pantalla muestra dedicatorias. Apagado: solo gráfica y puntos.")]
        public bool recibeNotas = true;
        [Tooltip("Ancho mínimo de la nota más chica (S), en metros. Las demás crecen en la misma proporción.")]
        public float anchoMinimoNota = 1.092f;
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

        [Header("Puntos POST dispersos (componente \"Punto\" del Figma)")]
        [Tooltip("Puntos y estrellas detrás de las notas, por metro de pantalla.")]
        public float puntosPorMetro = 2.6f;
        [Tooltip("Puntos chicos que flotan por delante de las notas, por metro de pantalla.")]
        public float puntosAdelantePorMetro = 0.3f;
        [Tooltip("De cada 100 puntos de atrás, cuántos son estrellas.")]
        [Range(0f, 100f)] public float porcentajeEstrellas = 12f;

        [Header("Gráfica post. (logo con sus puntos, centrada)")]
        public bool composicionCentral;

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
        const string NombreLienzo = "Pantalla (canvas)";
        RectTransform lienzo, capaDecoAtras, capaNotas, capaDecoAdelante;
        readonly List<NotaVisual> notas = new List<NotaVisual>();
        readonly Dictionary<NotaVisual, Vector2> destinos = new Dictionary<NotaVisual, Vector2>();
        readonly Dictionary<NotaVisual, Coroutine> ciclos = new Dictionary<NotaVisual, Coroutine>();
        // Puntos ya ubicados (centro y radio, en unidades), para que no se encimen.
        readonly List<Vector3> puntosPuestos = new List<Vector3>();
        bool inicializada;

        float U(float metros) => metros / metrosPorUnidad;
        /// <summary>Unidades de pantalla por px del Figma: el alto del frame es el alto de la pantalla.</summary>
        float PxFigma => U(alto) / FigmaAltoFrame;
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

        void OnEnable()
        {
            // Sin Play: vista previa quieta de la gráfica y los puntos. En Play la arma ControladorCubo.
            if (!Application.isPlaying) VistaPrevia();
        }

        /// <summary>Rearma la vista previa del editor (por ejemplo, después de cambiar medidas o zonas).</summary>
        public void VistaPrevia()
        {
            if (Application.isPlaying) return;
            inicializada = false;
            Inicializar();
        }

        public void Inicializar()
        {
            if (inicializada) return;
            BorrarLienzo();   // la vista previa del editor, si había
            inicializada = true;

            var go = Nuevo(NombreLienzo, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
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

            capaDecoAtras = Capa("Puntos atrás");
            var capaComposicion = Capa("Gráfica post.");
            capaNotas = Capa("Notas");
            capaDecoAdelante = Capa("Puntos adelante");

            // Misma disposición cada vez (por pantalla), así la vista previa coincide con el Play.
            var azar = UnityEngine.Random.state;
            UnityEngine.Random.InitState(Semilla());
            puntosPuestos.Clear();
            if (composicionCentral) CrearComposicion(capaComposicion);
            CrearPuntos(capaDecoAtras, Mathf.RoundToInt(puntosPorMetro * largo), false);
            CrearPuntos(capaDecoAdelante, Mathf.RoundToInt(puntosAdelantePorMetro * largo), true);
            UnityEngine.Random.state = azar;
        }

        /// <summary>Quita el canvas anterior de esta pantalla, si existe.</summary>
        void BorrarLienzo()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var hijo = transform.GetChild(i);
                if (hijo.name != NombreLienzo) continue;
                if (Application.isPlaying) Destroy(hijo.gameObject);
                else DestroyImmediate(hijo.gameObject);
            }
            lienzo = null;
        }

        /// <summary>Objeto nuevo. Fuera de Play es solo vista previa: no se guarda en la escena.</summary>
        static GameObject Nuevo(string nombre, params Type[] componentes)
        {
            var go = new GameObject(nombre, componentes);
            if (!Application.isPlaying) go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            return go;
        }

        int Semilla()
        {
            int h = 17;
            foreach (char c in name) h = unchecked(h * 31 + c);
            return h;
        }

        RectTransform Capa(string nombre)
        {
            var go = Nuevo(nombre, typeof(RectTransform));
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

        // ---------------- puntos POST dispersos ----------------

        // Componente "Punto" del Figma (1517:8), marca POST. Diámetros en px: S 21,23 · M 35 · L 61,34.
        static readonly float[] DiametrosPunto = { 21.23f, 35f, 61.34f };
        const float TrazoPunto = 2.95f;         // borde de los círculos, en px del Figma
        const float Estrella12 = 112.65f;       // tamaño XL de la estrella de 12 puntas
        const float Estrella7 = 94.84f;         // tamaño XL de la estrella de 7 puntas
        const float SobranteEstrella = 1.043f;  // el PNG exportado incluye el borde, que sobresale

        /// <summary>Las 5 variantes de círculo de la marca POST. indice: 0 a 4.</summary>
        static Texture2D TexturaPunto(int indice, float diametro)
        {
            float grosor = TrazoPunto / diametro;
            switch (indice)
            {
                case 0: return RecursosPost.Punto(PaletaPost.Oscuro, PaletaPost.Crema, grosor);   // Negro, borde Blanco Crema
                case 1: return RecursosPost.Punto(PaletaPost.Oscuro, PaletaPost.Verde, grosor);   // Negro, borde Verde
                case 2: return RecursosPost.Punto(PaletaPost.Verde, PaletaPost.Crema, grosor);    // Verde, borde Blanco Crema
                case 3: return RecursosPost.Punto(PaletaPost.Crema, PaletaPost.Crema, 0f);        // Blanco Crema, sin borde
                default: return RecursosPost.Punto(PaletaPost.Verde, PaletaPost.Verde, 0f);       // Verde, sin borde
            }
        }

        /// <summary>
        /// Dispersa círculos (S, M, L en sus 5 variantes) y estrellas (12 y 7 puntas) por la pantalla,
        /// al tamaño que tienen en el Figma. No entran en las zonas bloqueadas ni se enciman.
        /// soloChicos: solo círculos S y M (los que flotan por delante de las notas).
        /// </summary>
        void CrearPuntos(RectTransform capa, int cantidad, bool soloChicos)
        {
            float k = PxFigma;
            for (int i = 0; i < cantidad; i++)
            {
                if (!soloChicos && UnityEngine.Random.value * 100f < porcentajeEstrellas)
                {
                    int e = UnityEngine.Random.Range(0, 3);
                    var tex = e == 0 ? RecursosPost.Estrella12Crema : e == 1 ? RecursosPost.Estrella12Verde : RecursosPost.Estrella7Verde;
                    float nominal = e == 2 ? Estrella7 : Estrella12;
                    Punto(capa, tex, nominal * SobranteEstrella * k, true);
                }
                else
                {
                    // Más chicos que grandes: S 45 %, M 35 %, L 20 %.
                    float dado = UnityEngine.Random.value;
                    int tamano = soloChicos ? (dado < 0.6f ? 0 : 1) : (dado < 0.45f ? 0 : dado < 0.8f ? 1 : 2);
                    float diametro = DiametrosPunto[tamano];
                    Punto(capa, TexturaPunto(UnityEngine.Random.Range(0, 5), diametro), diametro * k, false);
                }
            }
        }

        void Punto(RectTransform capa, Texture tex, float tam, bool girar)
        {
            if (tex == null) return;
            var t = TamanoLienzo;
            // Cuánto se mueve al flotar: se reserva ese lugar para que no salga de la pantalla
            // ni se meta en una zona bloqueada.
            var flota = new Vector2(UnityEngine.Random.Range(20f, 60f), UnityEngine.Random.Range(50f, 110f));
            float mx = tam / 2f + flota.x, my = tam / 2f + flota.y;
            if (2f * mx >= t.x || 2f * my >= t.y) return;

            bool hayLugar = false;
            Vector2 pos = Vector2.zero;
            for (int intento = 0; intento < 60 && !hayLugar; intento++)
            {
                pos = new Vector2(UnityEngine.Random.Range(mx, t.x - mx), UnityEngine.Random.Range(my, t.y - my));
                if (TocaZona(new Rect(pos.x - mx - 40f, pos.y - my - 40f, 2f * mx + 80f, 2f * my + 80f))) continue;
                hayLugar = true;
                foreach (var p in puntosPuestos)
                    if (Vector2.Distance(pos, new Vector2(p.x, p.y)) < tam / 2f + p.z + 140f) { hayLugar = false; break; }
            }
            if (!hayLugar) return;   // pantalla llena: mejor uno menos que uno encimado
            puntosPuestos.Add(new Vector3(pos.x, pos.y, tam / 2f));

            var go = Nuevo("Punto", typeof(RectTransform), typeof(RawImage), typeof(DecoFlotante));
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
            f.amplitud = flota;
            f.giro = girar ? 12f : 0f;
        }

        // ---------------- gráfica post. ----------------

        /// <summary>
        /// Gráfica del frame del Figma (1622:4941, 3007 × 637 px): logo post. con sus puntos y sus
        /// dos estrellas, centrada. El alto del frame ocupa todo el alto de la pantalla. La elipse
        /// roja del frame no se dibuja: es la zona donde no entran notas ni puntos sueltos.
        /// Cada elemento hace un idle lento, a destiempo de los demás.
        /// Coordenadas del Figma: esquina superior izquierda del frame, en px.
        /// </summary>
        void CrearComposicion(RectTransform capa)
        {
            float k = PxFigma;   // unidades de pantalla por px del Figma

            var go = Nuevo("Gráfica", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(capa, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(FigmaAnchoFrame * k, FigmaAltoFrame * k);
            rt.anchoredPosition = Vector2.zero;

            // (x, y, diámetro sin borde, relleno verde) de cada Spot del Figma. Los que no son
            // verdes son "Negro con borde Blanco Crema"; todos llevan el borde por fuera.
            var spots = new (float x, float y, float d, bool verde)[]
            {
                (1855.43f, 135.29f, 31.85f, false), (1178.32f, 127.62f, 15.34f, false), (1910.87f, 410.14f, 15.34f, false),
                (1639.55f, 452.02f, 15.34f, false), (1385.93f, 476.20f, 15.34f, true),  (1680.84f, 94.00f, 15.34f, true),
                (1193.65f, 372.39f, 15.34f, false), (1297.46f, 515.13f, 27.72f, false), (1720.36f, 127.62f, 27.72f, false),
                (1871.35f, 440.22f, 47.19f, true),  (1153.55f, 452.02f, 55.44f, true),
            };
            int i = 0;
            foreach (var s in spots)
            {
                float exterior = s.d + 2f * TrazoPunto;
                var tex = RecursosPost.Punto(s.verde ? PaletaPost.Verde : PaletaPost.Oscuro, PaletaPost.Crema, TrazoPunto / exterior);
                Elemento(rt, "Spot", tex, s.x + s.d / 2f, s.y + s.d / 2f, exterior, exterior, k, 0f, i++);
            }
            // Estrellas (centro y tamaño de la imagen exportada, en px del Figma).
            Elemento(rt, "Estrella 12", RecursosPost.Estrella12Verde, 1137.3f, 245.9f, 117.5f, 117.5f, k, 8f, i++);
            Elemento(rt, "Estrella 7", RecursosPost.Estrella7Verde, 1836.1f, 288.8f, 99f, 99.8f, k, 10f, i++);
            // Logo: centro y tamaño de la imagen exportada (incluye el borde verde).
            Elemento(rt, "Logo post.", RecursosPost.Logo, 1517.6f, 331.4f, 477.7f, 228.5f, k, 1.2f, i, 0.45f);
        }

        void Elemento(RectTransform padre, string nombre, Texture tex, float cx, float cy, float w, float h,
            float k, float giro, int indice, float amplitud = 1f)
        {
            if (tex == null) return;
            var go = Nuevo(nombre, typeof(RectTransform), typeof(RawImage), typeof(DecoFlotante));
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
