using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CuboPost
{
    /// <summary>
    /// Ejercicio de respiración para la fila de espera (Figma, página "tareas" → "Tarea 1" y "Tarea 5"),
    /// en la pantalla del lado este, a la derecha de la puerta de entrada (zona bloqueada para los
    /// puntos de la pantalla).
    ///   · Aparece solo cada 5 minutos, o con la tecla R (con R de nuevo se corta).
    ///   · Entra creciendo (scale up), hace 2 respiraciones (5 s inhalar + 5 s exhalar) y se va
    ///     achicándose (scale down).
    ///   · Al inhalar aparece un anillo más grande detrás de la carita cada segundo; al exhalar se va
    ///     uno por segundo. La carita cambia según se inhale o se exhale, late suave y "respira".
    /// La carita mide lo que el círculo violeta del Figma (Ø 1,47 m) y los anillos la acompañan en
    /// proporción. Va como hijo de la pantalla, en su centro: la zona (el cuadrado que ocupa el anillo
    /// más grande) se mide desde abajo a la izquierda de la pantalla, vista de frente, en metros.
    /// </summary>
    [ExecuteAlways]
    public class EjercicioRespiracion : MonoBehaviour
    {
        // Frame de la animación en el Figma (1782:5656), en px: anillos del más chico al más
        // grande y la carita del centro.
        static readonly float[] DiametrosAnillo = { 524f, 657f, 771f, 900f, 996f };
        public const float DiametroCara = 409f, DiametroMayor = 996f;
        // Colores de los anillos (1782:5674): crema al 33 % y violeta al 48 %, alternados desde afuera.
        static readonly Color AnilloCrema = new Color(PaletaPost.Crema.r, PaletaPost.Crema.g, PaletaPost.Crema.b, 0.33f);
        static readonly Color AnilloVioleta = new Color(PaletaPost.NotaVioleta.r, PaletaPost.NotaVioleta.g, PaletaPost.NotaVioleta.b, 0.48f);

        [Header("Ubicación (metros)")]
        [Tooltip("Cuadrado del anillo más grande: desde abajo a la izquierda de la pantalla, vista de frente.")]
        public Rect zona = new Rect(8.71f, 0.06f, 3.58f, 3.58f);
        public float largoPantalla = 12.42f;
        public float altoPantalla = 4.3f;

        [Header("Ritmo")]
        public float segundosInhalar = 5f;
        public float segundosExhalar = 5f;
        [Tooltip("Respiraciones (inhalar + exhalar) cada vez que aparece.")]
        public int respiraciones = 2;
        [Tooltip("Cada cuántos segundos aparece solo (300 = 5 minutos).")]
        public float cadaCuantosSegundos = 300f;
        [Tooltip("Lo que tarda cada anillo en aparecer o en irse.")]
        public float segundosAnillo = 0.5f;
        [Tooltip("Lo que tarda en crecer al aparecer y en achicarse al irse.")]
        public float segundosEscala = 0.7f;

        // Leyenda (en mm del lienzo): violeta #AB8AE5 y la onda con el grosor del trazo de la letra.
        static readonly Color VioletaLeyenda = new Color32(0xAB, 0x8A, 0xE5, 0xFF);
        const float TamanoLeyenda = 260f, GrosorOnda = 34f, AmplitudOnda = 22f, LargoOndaPeriodo = 165f;

        const string NombreLienzo = "Respiración (canvas)";
        const float MetrosPorUnidad = 0.001f;   // 1 unidad del lienzo = 1 mm

        enum Estado { Oculto, Entrando, Visible, Saliendo }

        RectTransform lienzo, visual, cara;
        RawImage[] anillos;
        float[] presencia;
        RawImage caraInhalando, caraExhalando;
        TextMeshProUGUI leyenda;
        GameObject ondaLeyenda;
        Estado estado;
        float inicio, cambio, proximaVez, mezclaCara;

        void OnEnable() => Armar();

        void Start()
        {
            if (Application.isPlaying) proximaVez = Time.time + cadaCuantosSegundos;
        }

        /// <summary>Rearma la animación (por ejemplo, después de cambiar la zona). Queda oculta.</summary>
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

            // Todo va dentro de "visual", que es lo que crece y se achica al aparecer y al irse.
            visual = (RectTransform)Nuevo("Visual", typeof(RectTransform)).transform;
            visual.SetParent(lienzo, false);
            visual.sizeDelta = lienzo.sizeDelta;

            float diametro = Mathf.Min(lienzo.sizeDelta.x, lienzo.sizeDelta.y);
            float k = diametro / DiametroMayor;
            var disco = DiscoDifuminado();
            anillos = new RawImage[DiametrosAnillo.Length];
            presencia = new float[DiametrosAnillo.Length];
            for (int i = anillos.Length - 1; i >= 0; i--)   // del más grande al más chico: los grandes quedan detrás
            {
                anillos[i] = Imagen($"Anillo {i + 1}", disco, Vector2.zero, DiametrosAnillo[i] * k, visual);
                anillos[i].color = i % 2 == 0 ? AnilloCrema : AnilloVioleta;
            }

            cara = (RectTransform)Nuevo("Carita", typeof(RectTransform)).transform;
            cara.SetParent(visual, false);
            cara.sizeDelta = Vector2.one * DiametroCara * k;
            caraExhalando = Imagen("Exhalando", Resources.Load<Texture2D>("Respiracion/cara-exhalando"), Vector2.zero, DiametroCara * k, cara);
            caraInhalando = Imagen("Inhalando", Resources.Load<Texture2D>("Respiracion/cara-inhalando"), Vector2.zero, DiametroCara * k, cara);

            // Cuenta (inhalá 3 / exhalá 2) arriba de los anillos, sin tocarlos: en el estilo de los
            // títulos de post. ("cuerpo"): Sora Bold en minúscula, violeta, con una onda subrayando.
            var t = Nuevo("Cuenta", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)t.transform;
            rt.SetParent(visual, false);
            leyenda = t.GetComponent<TextMeshProUGUI>();
            leyenda.font = RecursosPost.FuenteTitulo;
            leyenda.fontSize = TamanoLeyenda;
            leyenda.alignment = TextAlignmentOptions.Bottom;
            leyenda.textWrappingMode = TextWrappingModes.NoWrap;
            leyenda.color = VioletaLeyenda;
            leyenda.raycastTarget = false;

            float ancho = leyenda.GetPreferredValues("exhalá 5").x;
            float altoOnda = 2f * AmplitudOnda + GrosorOnda;
            float yOnda = diametro / 2f + 60f + altoOnda / 2f;   // 6 cm sobre el anillo más grande
            var onda = Imagen("Onda", Onda(Mathf.CeilToInt(ancho), Mathf.CeilToInt(altoOnda)), new Vector2(0f, yOnda), 1f, visual);
            onda.rectTransform.sizeDelta = new Vector2(Mathf.Ceil(ancho), Mathf.Ceil(altoOnda));
            onda.color = VioletaLeyenda;
            ondaLeyenda = onda.gameObject;
            rt.sizeDelta = new Vector2(ancho + 200f, TamanoLeyenda * 1.3f);
            rt.anchoredPosition = new Vector2(0f, yOnda + altoOnda / 2f + 10f + rt.sizeDelta.y / 2f);

            estado = Estado.Oculto;
            visual.localScale = Vector3.zero;
            mezclaCara = 0f;
            Dibujar(-1f, 0f, true);
        }

        /// <summary>Aparece (crece) y hace sus respiraciones. Si ya estaba, no hace nada.</summary>
        public void Mostrar()
        {
            if (estado == Estado.Entrando || estado == Estado.Visible) return;
            for (int i = 0; i < presencia.Length; i++) presencia[i] = 0f;
            mezclaCara = 0f;
            estado = Estado.Entrando;
            cambio = Time.time;
            inicio = Time.time + segundosEscala;   // las respiraciones arrancan cuando terminó de crecer
            proximaVez = Time.time + cadaCuantosSegundos;
        }

        /// <summary>Se va (se achica).</summary>
        public void Ocultar()
        {
            if (estado == Estado.Oculto || estado == Estado.Saliendo) return;
            estado = Estado.Saliendo;
            cambio = Time.time;
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

        static Texture2D discoDifuminado, onda;

        /// <summary>0 → 1 → 0: dos golpes cortos por latido (el segundo más suave), 0,92 s por latido.</summary>
        static float Latido(float tiempo)
        {
            float f = tiempo / 0.92f % 1f;
            float golpe(float centro, float ancho) => Mathf.Exp(-Mathf.Pow((f - centro) / ancho, 2f));
            return Mathf.Clamp01(golpe(0.1f, 0.06f) + 0.6f * golpe(0.32f, 0.07f));
        }

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

        /// <summary>
        /// Onda blanca (se tiñe con el color de la imagen) de ancho × alto px, 1 px = 1 mm: una senoide de
        /// trazo parejo con puntas redondas, con una cantidad entera de períodos.
        /// </summary>
        static Texture2D Onda(int ancho, int alto)
        {
            if (onda != null && onda.width == ancho && onda.height == alto) return onda;
            var t = onda = new Texture2D(ancho, alto, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, name = "Onda de la cuenta" };
            float r = GrosorOnda / 2f, a = AmplitudOnda, c = alto / 2f;
            float largo = ancho - 2f * r;
            float periodos = Mathf.Max(1f, Mathf.Round(largo / LargoOndaPeriodo));
            float w = periodos * 2f * Mathf.PI / largo;
            float Y(float x) => c + a * Mathf.Sin(w * (Mathf.Clamp(x, r, ancho - r) - r));
            var px = new Color32[ancho * alto];
            for (int x = 0; x < ancho; x++)
            for (int y = 0; y < alto; y++)
            {
                // Distancia al trazo: el punto más cercano de la curva, buscado en un entorno de ±r.
                float d = float.MaxValue;
                for (float s = x - r - 1f; s <= x + r + 1f; s += 1f)
                {
                    float sx = Mathf.Clamp(s, r, ancho - r);
                    d = Mathf.Min(d, Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(sx, Y(sx))));
                }
                float alfa = Mathf.Clamp01(r - d + 0.5f);
                px[y * ancho + x] = new Color32(255, 255, 255, (byte)(alfa * 255));
            }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Objeto nuevo. Fuera de Play es solo vista previa: no se guarda en la escena.</summary>
        static GameObject Nuevo(string nombre, params Type[] componentes)
        {
            var go = new GameObject(nombre, componentes);
            if (!Application.isPlaying) go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            return go;
        }

        static RawImage Imagen(string nombre, Texture tex, Vector2 posicion, float diametro, RectTransform padre)
        {
            var go = Nuevo(nombre, typeof(RectTransform), typeof(RawImage));
            var rt = (RectTransform)go.transform;
            rt.SetParent(padre, false);
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
            if (teclado != null && teclado.rKey.wasPressedThisFrame)
            {
                if (estado == Estado.Oculto || estado == Estado.Saliendo) Mostrar();
                else Ocultar();
            }
            if (estado == Estado.Oculto && Time.time >= proximaVez) Mostrar();

            // Aparecer creciendo (con un leve rebote) e irse achicándose.
            float p = Mathf.Clamp01((Time.time - cambio) / Mathf.Max(0.01f, segundosEscala));
            switch (estado)
            {
                case Estado.Entrando:
                    visual.localScale = Vector3.one * Rebote(p);
                    if (p >= 1f) estado = Estado.Visible;
                    break;
                case Estado.Visible:
                    visual.localScale = Vector3.one;
                    if (Time.time - inicio >= (segundosInhalar + segundosExhalar) * respiraciones) Ocultar();
                    break;
                case Estado.Saliendo:
                    visual.localScale = Vector3.one * (1f - Rebote(p, true));
                    if (p >= 1f) { estado = Estado.Oculto; visual.localScale = Vector3.zero; }
                    break;
            }
            if (estado == Estado.Oculto) return;

            float t = Time.time - inicio;
            bool respirando = estado == Estado.Visible && t >= 0f;
            Dibujar(respirando ? t : -1f, Time.deltaTime, false);
        }

        /// <summary>0 → 1 con un leve pasarse de largo al final (aparecer) o al principio (irse).</summary>
        static float Rebote(float p, bool alReves = false)
        {
            const float c = 1.6f;
            if (alReves) return p * p * ((c + 1f) * p - c);              // se encoge un poco antes de irse
            float q = p - 1f;
            return 1f + q * q * ((c + 1f) * q + c);                       // crece y se pasa apenas
        }

        /// <summary>
        /// t: segundos desde que arrancaron las respiraciones (negativo = todavía no). Cuántos anillos
        /// se ven sale del segundo en curso: al inhalar 0, 1, 2, 3, 4 (y el quinto al terminar), al
        /// exhalar 5, 4, 3, 2, 1.
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
                leyenda.text = (inhalando ? "inhalá " : "exhalá ") + Mathf.CeilToInt(restan);
            }
            else leyenda.text = "";
            ondaLeyenda.SetActive(leyenda.text.Length > 0);

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

            // Carita: cambia con un fundido corto, "respira" apenas (crece un 5 % al inhalar) y late suave.
            mezclaCara = Mathf.MoveTowards(mezclaCara, t >= 0f && inhalando ? 1f : 0f, inmediato ? 1f : dt / 0.25f);
            caraInhalando.color = new Color(1f, 1f, 1f, mezclaCara);
            caraExhalando.color = new Color(1f, 1f, 1f, 1f - mezclaCara);
            float aire = t < 0f ? 0f : Mathf.SmoothStep(0f, 1f, inhalando ? avance : 1f - avance);
            cara.localScale = Vector3.one * (1f + 0.05f * aire + 0.035f * Latido(Application.isPlaying ? Time.time : 0f));
        }
    }
}
