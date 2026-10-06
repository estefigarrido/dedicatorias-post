using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CuboPost
{
    /// <summary>
    /// Pantalla de espera para la fila (Figma, página "tareas" → "Tarea 6"), en la pantalla del lado
    /// este, entre la puerta de salida y la de entrada (la zona verde del Figma).
    ///   · Solo aparece cuando la persona de sistemas toca la tecla E: crece (scale up), queda 30 s y
    ///     se va achicándose (scale down).
    ///   · "La siguiente clase comienza en: 10 min" (siempre 10 min).
    ///   · Anillos verdes que se van abriendo desde el centro (como la animación de referencia de
    ///     Pinterest) sin pasar nunca el borde de la pantalla, y el puntito verde que gira.
    ///   · Sin fondo ni rellenos negros: círculos y estrellas son solo contornos (vectoriales).
    /// Diseño: frame del Figma de 2264 × 2108 px (rectángulo 1853:4778 y lo que tiene encima). La zona
    /// se mide desde abajo a la izquierda de la pantalla, vista de frente, en metros; 1 px del Figma
    /// = zona.width / 2264 metros.
    /// </summary>
    public class PantallaEspera : MonoBehaviour
    {
        const float AnchoDiseno = 2264f, AltoDiseno = 2108f;
        // Anillos (en px del Figma): centro, trazo y paso entre uno y otro. En reposo coinciden con
        // los del diseño (radios 354, 642, 916 y 1187; opacidades 40 % y 100 % alternadas).
        static readonly Vector2 CentroAnillos = new Vector2(4.5f, -7.5f);
        // Los anillos nunca pasan el borde de la pantalla: el más grande llega hasta radioMaximo
        // (lo que hay del centro al borde de arriba o de abajo) y se desvanece antes.
        const float Trazo = 7f, RadioInicial = 77f;
        const int CantidadAnillos = 4;
        float radioMaximo;
        // Puntito verde que gira (el "Spot" de Ø 124 que está sobre el tercer anillo).
        const float DiametroPuntito = 124f, RadioGiro = 642.5f, AnguloInicial = 48.5f;

        [Header("Ubicación (metros)")]
        [Tooltip("Zona verde del Figma: desde abajo a la izquierda de la pantalla, vista de frente.")]
        public Rect zona = new Rect(2.67f, 0.08f, 4.44f, 4.13f);
        public float largoPantalla = 12.42f;
        public float altoPantalla = 4.3f;

        [Header("Tiempos")]
        [Tooltip("Segundos que queda en pantalla (desde que aparece hasta que empieza a irse).")]
        public float segundosVisible = 30f;
        public float segundosEscala = 0.7f;
        [Tooltip("Cada cuántos segundos un anillo ocupa el lugar del siguiente.")]
        public float segundosPorAnillo = 2.5f;
        [Tooltip("Segundos por vuelta del puntito verde.")]
        public float segundosPorVuelta = 10f;

        enum Estado { Oculto, Entrando, Visible, Saliendo }

        RectTransform visual, puntito;
        AnilloUI[] anillos;
        TextMeshProUGUI minutos;
        TextMeshProUGUI[] contorno;
        Estado estado;
        float cambio, aparecio;
        float k;   // metros por px del Figma

        void Start() => Armar();

        void Armar()
        {
            k = zona.width / AnchoDiseno;
            var go = new GameObject("Pantalla de espera (canvas)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 1;   // delante del lienzo de la pantalla
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
            var lienzo = (RectTransform)go.transform;
            lienzo.sizeDelta = new Vector2(largoPantalla, altoPantalla) / k;   // toda la pantalla, en px del Figma
            lienzo.localScale = Vector3.one * k;
            lienzo.localPosition = new Vector3(0f, 0f, -0.016f);

            // Recorte: nada se dibuja fuera de la pantalla LED.
            var mascara = Hijo("Recorte a la pantalla", lienzo, typeof(RectMask2D));
            mascara.sizeDelta = lienzo.sizeDelta;

            visual = Hijo("Visual", mascara);
            visual.anchoredPosition = new Vector2(zona.center.x - largoPantalla / 2f, zona.center.y - altoPantalla / 2f) / k;
            visual.sizeDelta = new Vector2(AnchoDiseno, AltoDiseno);

            // Sin fondo propio: se ve la pantalla detrás. Radio máximo de los anillos: hasta el borde
            // más cercano de la pantalla (arriba o abajo), con un margen.
            float centroY = zona.center.y / k + CentroAnillos.y;
            radioMaximo = Mathf.Min(centroY, altoPantalla / k - centroY) - Trazo - 20f;

            anillos = new AnilloUI[CantidadAnillos];
            for (int i = 0; i < CantidadAnillos; i++)
            {
                anillos[i] = Hijo($"Anillo {i + 1}", visual, typeof(AnilloUI)).GetComponent<AnilloUI>();
                anillos[i].rectTransform.anchoredPosition = CentroAnillos;
                anillos[i].Grosor = Trazo;
                anillos[i].raycastTarget = false;
            }

            // Franja verde degradada detrás de "N min" (dos bandas inclinadas 18°).
            var degrade = Degrade();
            Banda(degrade, new Vector2(60f, -82f), new Vector2(1068f, 110f), 0.45f);
            Banda(degrade, new Vector2(76f, -119f), new Vector2(1043f, 34f), 0.30f);

            // Puntos y estrellas del diseño (flotan apenas, como los de la pantalla).
            Punto(new Vector2(-674.5f, 194.5f), 59f, false, 0);
            Punto(new Vector2(279.5f, 869.5f), 79f, false, 1);
            Punto(new Vector2(-236f, -810f), 46f, true, 2);
            Punto(new Vector2(576.5f, -614.5f), 99f, false, 3);
            Punto(new Vector2(-533.5f, -581.5f), 33f, false, 4);
            Punto(new Vector2(-579.5f, 727.5f), 59f, true, 5);
            Estrella(new Vector2(-1097f, -382f), 172f + 2f * 5.4f, 12, 0.6f, 8f, 0f, 6);
            Estrella(new Vector2(960.5f, 517.5f), 145f + 2f * 3.6f, 7, 0.6f, 7f, -16f, 7);

            // El puntito verde que gira.
            puntito = Hijo("Puntito que gira", visual);
            puntito.sizeDelta = Vector2.one * DiametroPuntito;
            Circulo(puntito, DiametroPuntito, true);

            // Textos.
            var titulo = Texto("La siguiente clase comienza en:", new Vector2(4.5f, 112.5f), new Vector2(1400f, 100f), 64f);
            titulo.color = PaletaPost.Crema;
            // "N min" como en el diseño: relleno oscuro con contorno verde. El contorno se arma con
            // copias verdes corridas alrededor (el contorno de TextMeshPro no anda con las fuentes que
            // se generan al vuelo).
            contorno = new TextMeshProUGUI[12];
            for (int i = 0; i < contorno.Length; i++)
            {
                float a = i * Mathf.PI * 2f / contorno.Length;
                contorno[i] = Texto("10 min", new Vector2(4.5f, -87.5f) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 7f, new Vector2(900f, 180f), 129f);
                contorno[i].color = PaletaPost.Verde;
            }
            minutos = Texto("10 min", new Vector2(4.5f, -87.5f), new Vector2(900f, 180f), 129f);
            minutos.color = PaletaPost.Oscuro;

            estado = Estado.Oculto;
            visual.localScale = Vector3.zero;
        }

        RectTransform Hijo(string nombre, Transform padre, params Type[] componentes)
        {
            // El CanvasRenderer va explícito: los gráficos propios (AnilloUI) no siempre lo agregan solos.
            var tipos = new Type[componentes.Length + 2];
            tipos[0] = typeof(RectTransform);
            tipos[1] = typeof(CanvasRenderer);
            componentes.CopyTo(tipos, 2);
            var go = new GameObject(nombre, tipos);
            var rt = (RectTransform)go.transform;
            rt.SetParent(padre, false);
            return rt;
        }

        void Punto(Vector2 pos, float d, bool verde, int i)
        {
            var rt = Hijo("Punto", visual, typeof(DecoFlotante));
            rt.anchoredPosition = pos;
            rt.sizeDelta = Vector2.one * d;
            Circulo(rt, d, verde);
            Flotar(rt, i, 0f);
        }

        /// <summary>Círculo vectorial: borde crema y, si es verde, relleno verde; si no, sin relleno.</summary>
        void Circulo(RectTransform padre, float d, bool verde)
        {
            if (verde)
            {
                var relleno = Hijo("Relleno", padre, typeof(AnilloUI)).GetComponent<AnilloUI>();
                relleno.rectTransform.sizeDelta = Vector2.one * d;
                relleno.Grosor = d / 2f;
                relleno.color = PaletaPost.Verde;
                relleno.raycastTarget = false;
            }
            var borde = Hijo("Borde", padre, typeof(ContornoUI)).GetComponent<ContornoUI>();
            borde.rectTransform.sizeDelta = Vector2.one * d;
            borde.grosor = 6.6f;
            borde.color = PaletaPost.Crema;
            borde.raycastTarget = false;
        }

        /// <summary>Estrella "flor" del Figma, solo el contorno verde (sin relleno).</summary>
        void Estrella(Vector2 pos, float tam, int puntas, float interior, float trazo, float giro, int i)
        {
            var rt = Hijo("Estrella", visual, typeof(ContornoUI), typeof(DecoFlotante));
            rt.anchoredPosition = pos;
            rt.sizeDelta = Vector2.one * tam;
            rt.localRotation = Quaternion.Euler(0f, 0f, giro);
            var e = rt.GetComponent<ContornoUI>();
            e.puntas = puntas;
            e.interior = interior;
            e.grosor = trazo;
            e.color = PaletaPost.Verde;
            e.raycastTarget = false;
            Flotar(rt, i, 10f);
        }

        static void Flotar(RectTransform rt, int i, float giro)
        {
            var f = rt.GetComponent<DecoFlotante>();
            f.amplitud = new Vector2(10f, 16f);
            f.periodo = 7f + (i * 1.37f) % 4f;
            f.fase = (i * 0.618f) % 1f;
            f.giro = giro;
        }

        void Banda(Texture tex, Vector2 pos, Vector2 tam, float alfa)
        {
            var rt = Hijo("Franja degradada", visual, typeof(RawImage));
            rt.anchoredPosition = pos;
            rt.sizeDelta = tam;
            rt.localRotation = Quaternion.Euler(0f, 0f, 18f);
            var raw = rt.GetComponent<RawImage>();
            raw.texture = tex;
            raw.color = new Color(PaletaPost.Verde.r, PaletaPost.Verde.g, PaletaPost.Verde.b, alfa);
            raw.raycastTarget = false;
        }

        TextMeshProUGUI Texto(string texto, Vector2 pos, Vector2 tam, float tamano)
        {
            var rt = Hijo("Texto", visual, typeof(TextMeshProUGUI));
            rt.anchoredPosition = pos;
            rt.sizeDelta = tam;
            var t = rt.GetComponent<TextMeshProUGUI>();
            t.font = RecursosPost.FuenteTitulo;
            t.fontSize = tamano;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            t.text = texto;
            return t;
        }

        /// <summary>Degradé horizontal: transparente en las puntas, opaco en el medio.</summary>
        static Texture2D Degrade()
        {
            const int n = 256;
            var tex = new Texture2D(n, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Degradé" };
            var px = new Color32[n * 4];
            for (int x = 0; x < n; x++)
            {
                float a = 1f - Mathf.Abs((x + 0.5f) / n * 2f - 1f);
                for (int y = 0; y < 4; y++) px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Aparece (crece) y queda segundosVisible. Si ya estaba, vuelve a contar los 30 s.</summary>
        public void Mostrar()
        {
            if (estado == Estado.Oculto || estado == Estado.Saliendo)
            {
                estado = Estado.Entrando;
                cambio = Time.time;
            }
            aparecio = Time.time;
        }

        public void Ocultar()
        {
            if (estado == Estado.Oculto || estado == Estado.Saliendo) return;
            estado = Estado.Saliendo;
            cambio = Time.time;
        }

        void Update()
        {
            if (visual == null) return;
            var teclado = Keyboard.current;
            if (teclado != null && teclado.eKey.wasPressedThisFrame) Mostrar();

            float p = Mathf.Clamp01((Time.time - cambio) / Mathf.Max(0.01f, segundosEscala));
            switch (estado)
            {
                case Estado.Entrando:
                    visual.localScale = Vector3.one * Rebote(p);
                    if (p >= 1f) estado = Estado.Visible;
                    break;
                case Estado.Visible:
                    visual.localScale = Vector3.one;
                    if (Time.time - aparecio >= segundosVisible) Ocultar();
                    break;
                case Estado.Saliendo:
                    visual.localScale = Vector3.one * (1f - Rebote(p, true));
                    if (p >= 1f) { estado = Estado.Oculto; visual.localScale = Vector3.zero; }
                    break;
            }
            Despejar(estado == Estado.Oculto ? 0f : Mathf.Clamp01(visual.localScale.x));
            if (estado == Estado.Oculto) return;

            // Anillos que se abren desde el centro: cada uno avanza un lugar cada segundosPorAnillo,
            // aparece de a poco en el medio y se desvanece antes de llegar al borde de la pantalla.
            float avance = Time.time / segundosPorAnillo;
            float paso = (radioMaximo - RadioInicial) / CantidadAnillos;
            for (int i = 0; i < anillos.Length; i++)
            {
                float lugar = (i + avance) % CantidadAnillos;
                float radio = RadioInicial + lugar * paso;
                anillos[i].rectTransform.sizeDelta = Vector2.one * (radio + Trazo) * 2f;
                float alfa = (i % 2 == 0 ? 1f : 0.4f)
                    * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(lugar))
                    * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CantidadAnillos - 0.8f, CantidadAnillos, lugar)));
                anillos[i].color = new Color(PaletaPost.Verde.r, PaletaPost.Verde.g, PaletaPost.Verde.b, alfa);
            }

            // El puntito gira en el sentido de las agujas del reloj.
            float ang = (AnguloInicial - Time.time / segundosPorVuelta * 360f) * Mathf.Deg2Rad;
            puntito.anchoredPosition = CentroAnillos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * RadioGiro;
        }

        // Puntos propios de la pantalla que caen dentro de la animación: se desvanecen mientras está.
        readonly System.Collections.Generic.List<RawImage> tapados = new System.Collections.Generic.List<RawImage>();
        float despejado = -1f;

        void Despejar(float cuanto)
        {
            if (Mathf.Approximately(cuanto, despejado)) return;
            if (despejado <= 0f && cuanto > 0f) BuscarTapados();
            despejado = cuanto;
            foreach (var r in tapados)
            {
                if (r == null) continue;
                var c = r.color;
                c.a = 1f - cuanto;
                r.color = c;
            }
        }

        /// <summary>Los puntos de la pantalla (no los de esta animación) que quedan dentro del círculo de los anillos.</summary>
        void BuscarTapados()
        {
            tapados.Clear();
            var pared = GetComponentInParent<ParedPantalla>();
            if (pared == null) return;
            var centro = visual.TransformPoint(CentroAnillos);
            float radio = (radioMaximo + 120f) * k;
            foreach (var r in pared.GetComponentsInChildren<RawImage>(true))
            {
                if (r.transform.IsChildOf(transform) || r.name != "Punto") continue;
                if (Vector3.Distance(r.transform.position, centro) < radio) tapados.Add(r);
            }
        }

        static float Rebote(float p, bool alReves = false)
        {
            const float c = 1.6f;
            if (alReves) return p * p * ((c + 1f) * p - c);
            float q = p - 1f;
            return 1f + q * q * ((c + 1f) * q + c);
        }
    }
}
