using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.Video;

namespace CuboPost
{
    /// <summary>
    /// Videos que aparecen cada tanto en unas pantallas. Siempre se ve la pantalla de post.; cuando
    /// le toca (o con su tecla, para probar):
    ///   1. si tiene barrido, entra uno de izquierda a derecha y en 2 s deja toda la pantalla del color
    ///      con el que empieza el primer video;
    ///   2. se reproducen los videos de la secuencia uno detrás del otro, sin cortes (cada uno ya está
    ///      cargado de antemano);
    ///   3. al terminar: si el último video es transparente (el "cierre de círculo", que se cierra sobre
    ///      negro puro) la pantalla de post. ya quedó a la vista; si no, la pantalla queda en el negro de
    ///      la marca y ese negro se retira con otro barrido (o se va directo, sin barrido).
    /// Encuadre: "cubrir" llena toda la pantalla sin deformar (recorta lo que sobra arriba y abajo); si
    /// no, el video va a toda la altura y los costados toman el color de su borde.
    /// Los archivos están en StreamingAssets (no se reimportan).
    ///
    /// Turnos: los videos no se pisan nunca. Si a uno le toca mientras otro está en pantalla, espera a
    /// que termine y deja una separación mínima; así quedan intercalados.
    /// </summary>
    public class VideoPantallas : MonoBehaviour
    {
        public ParedPantalla[] paredes;
        public string archivo = "gente-corriendo.mp4";
        [Tooltip("Videos que se pasan seguidos después del primero.")]
        public string[] siguientes = new string[0];
        [Tooltip("Video de la secuencia cuyo negro puro es transparente (deja ver la pantalla de atrás).")]
        public string transparente = "";
        [Tooltip("Tamaño de los videos en px (para el encuadre).")]
        public Vector2Int tamanoVideo = new Vector2Int(4096, 858);
        [Tooltip("Llenar toda la pantalla (recortando arriba y abajo) en vez de dejar costados.")]
        public bool cubrir;
        [Tooltip("Segundos hasta la primera vez (después, cada cadaCuantosSegundos).")]
        public float primeraVez = 420f;
        [Tooltip("Cada cuántos segundos se pasa la secuencia (420 = 7 minutos).")]
        public float cadaCuantosSegundos = 420f;
        public float segundosBarrido = 2f;
        [Tooltip("Con barrido de entrada (y de salida, si el último video no es transparente).")]
        public bool conBarrido = true;
        [Tooltip("Color del barrido de entrada: el del primer cuadro del primer video.")]
        public Color verde = new Color(0x4c / 255f, 0xb7 / 255f, 0x68 / 255f);
        [Tooltip("Tecla para pasarlo en el momento (para probar).")]
        public Key tecla = Key.M;
        [Tooltip("Segundos mínimos entre el final de una secuencia y el comienzo de otra.")]
        public float separacion = 20f;

        static readonly List<VideoPantallas> todos = new List<VideoPantallas>();
        static float ultimoFinal = -999f;

        void OnEnable() => todos.Add(this);
        void OnDisable() => todos.Remove(this);

        /// <summary>¿Hay otro video en pantalla, o terminó hace menos de "separacion" segundos?</summary>
        bool TurnoOcupado() => todos.Exists(v => v != this && v.estado != Estado.Oculto) || Time.time - ultimoFinal < separacion;

        enum Estado { Oculto, Entrando, Video, Saliendo }

        class Clip
        {
            public string archivo;
            public VideoPlayer reproductor;
            public RenderTexture textura;
            public bool termino, transparente;
        }

        class Capa
        {
            public GameObject contenido;          // video + costados
            public RawImage video;
            public Image costadoIzq, costadoDer;
            public RectTransform verde, negro;
        }

        readonly List<Clip> clips = new List<Clip>();
        readonly List<Capa> capas = new List<Capa>();
        Material sinNegro;
        Estado estado;
        float cambio, proximaVez;
        int actual;
        bool arranco;
        Clip enPantalla;   // el clip cuya textura se está mostrando
        Color colorBorde;
        bool leyendoBorde;

        void Start()
        {
            var nombres = new List<string> { archivo };
            nombres.AddRange(siguientes);
            foreach (var n in nombres)
            {
                var c = new Clip { archivo = n, transparente = !string.IsNullOrEmpty(transparente) && n == transparente };
                c.textura = new RenderTexture(tamanoVideo.x, tamanoVideo.y, 0) { name = "Video " + n };
                c.reproductor = gameObject.AddComponent<VideoPlayer>();
                c.reproductor.playOnAwake = false;
                c.reproductor.isLooping = false;
                c.reproductor.source = VideoSource.Url;
                c.reproductor.url = Path.Combine(Application.streamingAssetsPath, n);
                c.reproductor.renderMode = VideoRenderMode.RenderTexture;
                c.reproductor.targetTexture = c.textura;
                c.reproductor.audioOutputMode = VideoAudioOutputMode.None;
                c.reproductor.skipOnDrop = true;
                c.reproductor.waitForFirstFrame = true;
                c.reproductor.loopPointReached += _ => c.termino = true;
                c.reproductor.Prepare();   // todos cargados de antemano: los cambios de video son instantáneos
                clips.Add(c);
            }
            var shader = Shader.Find("CuboPost/VideoSinNegro");
            if (shader != null) sinNegro = new Material(shader) { name = "Video sin negro" };

            foreach (var p in paredes) if (p != null) capas.Add(Armar(p));
            Mostrar(false);
            proximaVez = Time.time + primeraVez;
        }

        void OnDestroy()
        {
            foreach (var c in clips) if (c.textura != null) c.textura.Release();
        }

        /// <summary>Un lienzo encima de la pantalla, del mismo tamaño (1 unidad = 1 mm).</summary>
        Capa Armar(ParedPantalla p)
        {
            var go = new GameObject("Video (canvas)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(p.transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 3;   // encima de todo lo de la pantalla (puntos, gráfica y notas)
            var lienzo = (RectTransform)go.transform;
            lienzo.sizeDelta = new Vector2(p.largo, p.alto) * 1000f;
            lienzo.localScale = Vector3.one * 0.001f;
            lienzo.localPosition = new Vector3(0f, 0f, -0.02f);

            var c = new Capa();
            c.contenido = Hijo("Contenido", lienzo, typeof(RectMask2D)).gameObject;   // recorta lo que sobra
            var cont = (RectTransform)c.contenido.transform;
            Estirar(cont);
            float proporcion = (float)tamanoVideo.x / tamanoVideo.y;
            float largo = p.largo * 1000f, altoP = p.alto * 1000f;
            float ancho, alto;
            if (cubrir)
            {
                // Llena la pantalla: el lado que sobra se recorta (sin deformar).
                ancho = Mathf.Max(largo, altoP * proporcion);
                alto = ancho / proporcion;
            }
            else
            {
                // A toda la altura, centrado, y los costados del color del borde.
                ancho = Mathf.Min(altoP * proporcion, largo);
                alto = ancho / proporcion;
                float costado = Mathf.Max(0f, (largo - ancho) / 2f) + 2f;
                c.costadoIzq = Bloque("Costado izquierdo", cont, PaletaPost.Oscuro);
                Anclar(c.costadoIzq.rectTransform, 0f, 0f, costado);
                c.costadoDer = Bloque("Costado derecho", cont, PaletaPost.Oscuro);
                Anclar(c.costadoDer.rectTransform, 1f, 1f, costado);
            }
            c.video = Hijo("Video", cont, typeof(RawImage)).GetComponent<RawImage>();
            c.video.raycastTarget = false;
            c.video.rectTransform.sizeDelta = new Vector2(ancho, alto);

            c.verde = Bloque("Barrido verde", lienzo, verde).rectTransform;
            c.negro = Bloque("Negro de la marca", lienzo, PaletaPost.Oscuro).rectTransform;
            return c;
        }

        static RectTransform Hijo(string nombre, Transform padre, params System.Type[] tipos)
        {
            var lista = new List<System.Type> { typeof(RectTransform), typeof(CanvasRenderer) };
            lista.AddRange(tipos);
            var rt = (RectTransform)new GameObject(nombre, lista.ToArray()).transform;
            rt.SetParent(padre, false);
            return rt;
        }

        static Image Bloque(string nombre, Transform padre, Color color)
        {
            var img = Hijo(nombre, padre, typeof(Image)).GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            Estirar(img.rectTransform);
            return img;
        }

        static void Estirar(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>Franja de ancho fijo pegada al borde izquierdo (x = 0) o derecho (x = 1).</summary>
        static void Anclar(RectTransform rt, float x0, float x1, float ancho)
        {
            rt.anchorMin = new Vector2(x0, 0f);
            rt.anchorMax = new Vector2(x1, 1f);
            rt.pivot = new Vector2(x0, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(ancho, 0f);
        }

        /// <summary>Muestra u oculta todo lo del video (la pantalla de post. queda a la vista).</summary>
        void Mostrar(bool si)
        {
            foreach (var c in capas)
            {
                c.contenido.SetActive(false);
                c.verde.gameObject.SetActive(si);
                c.negro.gameObject.SetActive(false);
            }
        }

        /// <summary>Pone en pantalla la textura de un clip (con el recorte del negro si es transparente).</summary>
        void Ver(Clip clip)
        {
            enPantalla = clip;
            foreach (var c in capas)
            {
                c.video.texture = clip.textura;
                c.video.material = clip.transparente ? sinNegro : null;
                if (c.costadoIzq != null) c.costadoIzq.enabled = c.costadoDer.enabled = !clip.transparente;
                c.contenido.SetActive(true);
                c.verde.gameObject.SetActive(false);
            }
        }

        /// <summary>Arranca la secuencia (barrido → videos → salida).</summary>
        public void Arrancar()
        {
            if (estado != Estado.Oculto) return;
            estado = Estado.Entrando;
            cambio = Time.time;
            actual = 0;
            arranco = false;
            enPantalla = null;
            colorBorde = verde;
            Mostrar(true);
            foreach (var c in capas)
            {
                Barrido(c.verde, 0f, 0f);
                if (c.costadoIzq != null) c.costadoIzq.color = c.costadoDer.color = verde;
            }
            foreach (var c in clips) { c.termino = false; if (!c.reproductor.isPrepared) c.reproductor.Prepare(); }
            proximaVez = Time.time + cadaCuantosSegundos;
        }

        /// <summary>Recorta un bloque a la franja horizontal [desde, hasta] (0 a 1 del ancho).</summary>
        static void Barrido(RectTransform rt, float desde, float hasta)
        {
            rt.anchorMin = new Vector2(desde, 0f);
            rt.anchorMax = new Vector2(hasta, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void Update()
        {
            var teclado = Keyboard.current;
            // Con la tecla arranca si no hay otro video en pantalla; solo, cuando le toca y el turno
            // está libre (si no, espera: así nunca se pisan).
            if (teclado != null && teclado[tecla].wasPressedThisFrame && !todos.Exists(v => v != this && v.estado != Estado.Oculto)) Arrancar();
            if (estado == Estado.Oculto && Time.time >= proximaVez && !TurnoOcupado()) Arrancar();

            float p = Mathf.Clamp01((Time.time - cambio) / segundosBarrido);
            float suave = Mathf.SmoothStep(0f, 1f, p);
            switch (estado)
            {
                case Estado.Entrando:
                    // El barrido entra desde la izquierda y cubre toda la pantalla en 2 s.
                    // Sin barrido: no se tapa nada y el video arranca apenas está listo.
                    foreach (var c in capas) Barrido(c.verde, 0f, conBarrido ? suave : 0f);
                    if ((p >= 1f || !conBarrido) && clips[0].reproductor.isPrepared)
                    {
                        clips[0].reproductor.time = 0;
                        clips[0].reproductor.Play();
                        estado = Estado.Video;
                    }
                    break;

                case Estado.Video:
                    var clip = clips[actual];
                    // Hasta que el clip muestra su primer cuadro queda lo anterior (el barrido o el último
                    // cuadro del video anterior): así no hay saltos entre un video y otro.
                    if (!arranco && clip.reproductor.isPlaying && clip.reproductor.frame > 0)
                    {
                        arranco = true;
                        var anterior = enPantalla;
                        Ver(clip);
                        if (anterior != null && anterior != clip) { anterior.reproductor.Stop(); anterior.reproductor.Prepare(); }
                    }
                    if (arranco && !clip.transparente && capas.Count > 0 && capas[0].costadoIzq != null) LeerBorde();
                    if (clip.termino)
                    {
                        clip.termino = false;
                        if (actual + 1 < clips.Count)
                        {
                            // El siguiente arranca enseguida; el último cuadro de este queda hasta que llegue.
                            clip.reproductor.Pause();
                            actual++;
                            arranco = false;
                            clips[actual].reproductor.time = 0;
                            clips[actual].reproductor.Play();
                        }
                        else Terminar(clip);
                    }
                    break;

                case Estado.Saliendo:
                    // El negro se retira hacia la derecha y deja ver la pantalla de post.
                    foreach (var c in capas) Barrido(c.negro, suave, 1f);
                    if (p >= 1f) Ocultar();
                    break;
            }
        }

        void Terminar(Clip ultimo)
        {
            ultimo.reproductor.Stop();
            if (ultimo.transparente)
            {
                // El círculo ya se cerró: la pantalla de post. quedó a la vista.
                Ocultar();
                return;
            }
            // Termina en negro: la pantalla queda en el negro de la marca y empieza a irse.
            foreach (var c in capas)
            {
                c.contenido.SetActive(false);
                c.verde.gameObject.SetActive(false);
                c.negro.gameObject.SetActive(conBarrido);
                Barrido(c.negro, 0f, 1f);
            }
            estado = Estado.Saliendo;
            // Sin barrido se va directo (la salida termina en el próximo cuadro).
            cambio = conBarrido ? Time.time : Time.time - segundosBarrido;
        }

        void Ocultar()
        {
            Mostrar(false);
            estado = Estado.Oculto;
            ultimoFinal = Time.time;
            enPantalla = null;
            foreach (var c in clips) { c.reproductor.Stop(); c.reproductor.Prepare(); }   // listos para la próxima vez
        }

        /// <summary>Lee el color del borde del video (arriba a la izquierda) para pintar los costados.</summary>
        void LeerBorde()
        {
            var textura = enPantalla != null ? enPantalla.textura : null;
            if (textura == null) return;
            if (!leyendoBorde && SystemInfo.supportsAsyncGPUReadback)
            {
                leyendoBorde = true;
                AsyncGPUReadback.Request(textura, 0, 8, 1, textura.height - 9, 1, 0, 1, TextureFormat.RGBA32, r =>
                {
                    leyendoBorde = false;
                    if (r.hasError) return;
                    var px = r.GetData<Color32>();
                    if (px.Length > 0) colorBorde = px[0];
                });
            }
            var col = colorBorde;
            col.a = 1f;
            foreach (var c in capas)
            {
                c.costadoIzq.color = Color.Lerp(c.costadoIzq.color, col, 1f - Mathf.Exp(-12f * Time.deltaTime));
                c.costadoDer.color = c.costadoIzq.color;
            }
        }
    }
}
