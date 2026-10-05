using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CuboPost
{
    /// <summary>
    /// Ejercicio de respiración para la fila de espera (Figma, página "tareas" → "Tarea 1"), en la
    /// pantalla del lado este, a la derecha de la puerta de entrada: la zona roja del Figma, que
    /// queda bloqueada para que los puntos de la pantalla no la crucen.
    /// Con la tecla E arranca (y con E de nuevo se corta): 5 s de inhalar y 5 s de exhalar.
    /// Al inhalar aparece un anillo más grande detrás de la carita cada segundo; al exhalar se va
    /// uno por segundo, del más grande al más chico. La carita cambia según se inhale o se exhale.
    /// Va como hijo de la pantalla, en su centro: la zona se mide desde abajo a la izquierda de la
    /// pantalla (vista de frente), en metros. También se ve sin apretar Play (quieta, sin guardarse).
    /// </summary>
    [ExecuteAlways]
    public class EjercicioRespiracion : MonoBehaviour
    {
        // Frame de la animación en el Figma (1782:5656), en px: anillos del más chico al más
        // grande y la carita del centro.
        static readonly float[] DiametrosAnillo = { 524f, 657f, 771f, 900f, 996f };
        const float DiametroCara = 409f, DiametroMayor = 996f;
        // Colores de los anillos (1782:5674): crema al 33 % y violeta al 48 %, alternados desde afuera.
        static readonly Color AnilloCrema = new Color(PaletaPost.Crema.r, PaletaPost.Crema.g, PaletaPost.Crema.b, 0.33f);
        static readonly Color AnilloVioleta = new Color(PaletaPost.NotaVioleta.r, PaletaPost.NotaVioleta.g, PaletaPost.NotaVioleta.b, 0.48f);

        [Header("Ubicación (metros)")]
        [Tooltip("Zona roja del Figma: desde abajo a la izquierda de la pantalla, vista de frente.")]
        public Rect zona = new Rect(8.72f, 0f, 2f, 2.3f);
        public float largoPantalla = 12.42f;
        public float altoPantalla = 5.3f;

        [Header("Ritmo")]
        public float segundosInhalar = 5f;
        public float segundosExhalar = 5f;
        [Tooltip("Respiraciones (inhalar + exhalar) cada vez que se toca la E.")]
        public int respiraciones = 6;
        [Tooltip("Lo que tarda cada anillo en aparecer o en irse.")]
        public float segundosAnillo = 0.5f;

        const string NombreLienzo = "Respiración (canvas)";
        const float MetrosPorUnidad = 0.001f;   // 1 unidad del lienzo = 1 mm
        const float AltoLeyenda = 300f;         // franja de abajo para "INHALÁ" / "EXHALÁ"

        RectTransform lienzo, cara;
        RawImage[] anillos;
        float[] presencia;
        RawImage caraInhalando, caraExhalando;
        TextMeshProUGUI leyenda;
        bool activo;
        float inicio, mezclaCara;

        void OnEnable() => Armar();

        /// <summary>Rearma la animación (por ejemplo, después de cambiar la zona).</summary>
        public void Armar()
        {
            Borrar();
            var go = Nuevo(NombreLienzo, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 1;   // delante del lienzo de la pantalla
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
            lienzo = (RectTransform)go.transform;
            lienzo.pivot = new Vector2(0.5f, 0.5f);
            lienzo.sizeDelta = zona.size / MetrosPorUnidad;
            lienzo.localScale = Vector3.one * MetrosPorUnidad;
            lienzo.localRotation = Quaternion.identity;
            lienzo.localPosition = new Vector3(zona.center.x - largoPantalla / 2f, zona.center.y - altoPantalla / 2f, -0.016f);

            // El círculo más grande ocupa todo el ancho de la zona; abajo queda la leyenda.
            var tam = lienzo.sizeDelta;
            float diametro = Mathf.Min(tam.x, tam.y - AltoLeyenda);
            var centro = new Vector2(0f, tam.y / 2f - diametro / 2f);
            float k = diametro / DiametroMayor;

            var disco = DiscoDifuminado();
            anillos = new RawImage[DiametrosAnillo.Length];
            presencia = new float[DiametrosAnillo.Length];
            for (int i = anillos.Length - 1; i >= 0; i--)   // del más grande al más chico: los grandes quedan detrás
            {
                anillos[i] = Imagen($"Anillo {i + 1}", disco, centro, DiametrosAnillo[i] * k);
                anillos[i].color = i % 2 == 0 ? AnilloCrema : AnilloVioleta;
            }

            cara = (RectTransform)Nuevo("Carita", typeof(RectTransform)).transform;
            cara.SetParent(lienzo, false);
            cara.anchoredPosition = centro;
            cara.sizeDelta = Vector2.one * DiametroCara * k;
            caraExhalando = Imagen("Exhalando", Resources.Load<Texture2D>("Respiracion/cara-exhalando"), Vector2.zero, DiametroCara * k, cara);
            caraInhalando = Imagen("Inhalando", Resources.Load<Texture2D>("Respiracion/cara-inhalando"), Vector2.zero, DiametroCara * k, cara);

            var t = Nuevo("Leyenda", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)t.transform;
            rt.SetParent(lienzo, false);
            rt.anchoredPosition = new Vector2(0f, -tam.y / 2f + AltoLeyenda / 2f);
            rt.sizeDelta = new Vector2(tam.x, AltoLeyenda);
            leyenda = t.GetComponent<TextMeshProUGUI>();
            leyenda.font = RecursosPost.FuenteMono;
            leyenda.fontSize = 88f;
            leyenda.characterSpacing = 4f;
            leyenda.alignment = TextAlignmentOptions.Center;
            leyenda.textWrappingMode = TextWrappingModes.NoWrap;
            leyenda.color = PaletaPost.Crema;
            leyenda.raycastTarget = false;

            activo = false;
            mezclaCara = 0f;
            Dibujar(-1f, 0f, true);
        }

        /// <summary>0 → 1 → 0: dos golpes cortos por latido (el segundo más suave), 0,92 s por latido.</summary>
        static float Latido(float tiempo)
        {
            float f = tiempo / 0.92f % 1f;
            float golpe(float centro, float ancho) => Mathf.Exp(-Mathf.Pow((f - centro) / ancho, 2f));
            return Mathf.Clamp01(golpe(0.1f, 0.06f) + 0.6f * golpe(0.32f, 0.07f));
        }

        static Texture2D discoDifuminado;

        /// <summary>Disco blanco con el borde difuminado: el último 30 % del radio se desvanece de a poco.</summary>
        static Texture2D DiscoDifuminado()
        {
            if (discoDifuminado != null) return discoDifuminado;
            const int n = 256;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, name = "Disco difuminado" };
            var px = new Color32[n * n];
            float c = n / 2f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) / c;   // 0 centro, 1 borde
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.7f, d));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            t.SetPixels32(px);
            t.Apply(true);
            return discoDifuminado = t;
        }

        void Borrar()
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

        RawImage Imagen(string nombre, Texture tex, Vector2 posicion, float diametro, RectTransform padre = null)
        {
            var go = Nuevo(nombre, typeof(RectTransform), typeof(RawImage));
            var rt = (RectTransform)go.transform;
            rt.SetParent(padre != null ? padre : lienzo, false);
            rt.anchoredPosition = posicion;
            rt.sizeDelta = Vector2.one * diametro;
            var raw = go.GetComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
            return raw;
        }

        void Update()
        {
            if (!Application.isPlaying || lienzo == null) return;
            var teclado = Keyboard.current;
            if (teclado != null && teclado.eKey.wasPressedThisFrame)
            {
                activo = !activo;
                inicio = Time.time;
            }
            float t = Time.time - inicio;
            if (activo && t >= (segundosInhalar + segundosExhalar) * respiraciones) activo = false;
            Dibujar(activo ? t : -1f, Time.deltaTime, false);
        }

        /// <summary>
        /// t: segundos desde que arrancó (negativo = en espera). Cuántos anillos se ven sale del
        /// segundo en curso: al inhalar 0, 1, 2, 3, 4 (y el quinto al terminar), al exhalar 5, 4, 3, 2, 1.
        /// </summary>
        void Dibujar(float t, float dt, bool inmediato)
        {
            int n = anillos.Length, visibles = 0;
            bool inhalando = false;
            float avance = 0f;   // 0 → 1 dentro de la fase
            if (t >= 0f)
            {
                float fase = t % (segundosInhalar + segundosExhalar);
                inhalando = fase < segundosInhalar;
                avance = inhalando ? fase / segundosInhalar : (fase - segundosInhalar) / segundosExhalar;
                int paso = Mathf.Min(n - 1, Mathf.FloorToInt(avance * n));
                visibles = inhalando ? paso : n - paso;
                float restan = inhalando ? segundosInhalar - fase : segundosInhalar + segundosExhalar - fase;
                leyenda.text = (inhalando ? "INHALÁ  " : "EXHALÁ  ") + Mathf.CeilToInt(restan);
            }
            else leyenda.text = "";   // en espera no se muestra nada: la E es solo un atajo para manejarlo

            float paso01 = inmediato ? 1f : dt / Mathf.Max(0.01f, segundosAnillo);
            for (int i = 0; i < n; i++)
            {
                presencia[i] = Mathf.MoveTowards(presencia[i], i < visibles ? 1f : 0f, paso01);
                float p = Mathf.SmoothStep(0f, 1f, presencia[i]);
                var c = anillos[i].color;
                c.a = (i % 2 == 0 ? AnilloCrema : AnilloVioleta).a * p;
                anillos[i].color = c;
                anillos[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, p);
            }

            // Carita: cambia con un fundido corto, "respira" apenas (crece un 5 % al inhalar) y late
            // suave todo el tiempo (doble golpe, como un corazón tranquilo, ~65 por minuto).
            mezclaCara = Mathf.MoveTowards(mezclaCara, t >= 0f && inhalando ? 1f : 0f, inmediato ? 1f : dt / 0.25f);
            caraInhalando.color = new Color(1f, 1f, 1f, mezclaCara);
            caraExhalando.color = new Color(1f, 1f, 1f, 1f - mezclaCara);
            float aire = t < 0f ? 0f : Mathf.SmoothStep(0f, 1f, inhalando ? avance : 1f - avance);
            cara.localScale = Vector3.one * (1f + 0.05f * aire + 0.035f * Latido(Application.isPlaying ? Time.time : 0f));
        }
    }
}
