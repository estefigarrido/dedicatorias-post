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
    /// Un video que aparece cada tanto en unas pantallas. Siempre se ve la pantalla de post.; cuando
    /// le toca (o con su tecla, para probar):
    ///   1. un barrido entra de izquierda a derecha y en 2 s deja toda la pantalla del color con el
    ///      que empieza el video (verde en "gente corriendo", negro #252525 en la insignia);
    ///   2. se reproduce el video, que termina en el negro de la marca;
    ///   3. la pantalla queda negra (#252525) y ese negro se retira con otro barrido de izquierda a
    ///      derecha: detrás aparece la pantalla de post. de siempre.
    /// El video va a toda la altura, centrado, sin deformar; los costados toman el color del borde del
    /// video, así se ve continuo. Los archivos están en StreamingAssets (no se reimportan).
    ///
    /// Turnos: los videos no se pisan nunca. Si a uno le toca mientras otro está en pantalla, espera a
    /// que termine y deja una separación mínima; así quedan intercalados.
    /// </summary>
    public class VideoPantallas : MonoBehaviour
    {
        public ParedPantalla[] paredes;
        public string archivo = "gente-corriendo.mp4";
        [Tooltip("Tamaño del video en px (para el encuadre).")]
        public Vector2Int tamanoVideo = new Vector2Int(4096, 858);
        [Tooltip("Segundos hasta la primera vez (después, cada cadaCuantosSegundos).")]
        public float primeraVez = 420f;
        [Tooltip("Cada cuántos segundos se pasa el video (420 = 7 minutos).")]
        public float cadaCuantosSegundos = 420f;
        public float segundosBarrido = 2f;
        [Tooltip("Con barrido de entrada y de salida. Apagado: el video aparece y se va directo.")]
        public bool conBarrido = true;
        [Tooltip("Color del barrido de entrada: el del primer cuadro del video.")]
        public Color verde = new Color(0x4c / 255f, 0xb7 / 255f, 0x68 / 255f);
        [Tooltip("Tecla para pasarlo en el momento (para probar).")]
        public Key tecla = Key.M;
        [Tooltip("Segundos mínimos entre el final de un video y el comienzo de otro.")]
        public float separacion = 20f;

        static readonly List<VideoPantallas> todos = new List<VideoPantallas>();
        static float ultimoFinal = -999f;

        void OnEnable() => todos.Add(this);
        void OnDisable() => todos.Remove(this);

        /// <summary>¿Hay otro video en pantalla, o terminó hace menos de "separacion" segundos?</summary>
        bool TurnoOcupado() => todos.Exists(v => v != this && v.estado != Estado.Oculto) || Time.time - ultimoFinal < separacion;

        enum Estado { Oculto, Entrando, Video, Saliendo }

        class Capa
        {
            public GameObject contenido;          // video + costados
            public Image costadoIzq, costadoDer;
            public RectTransform verde, negro;
        }

        readonly List<Capa> capas = new List<Capa>();
        VideoPlayer reproductor;
        RenderTexture textura;
        Estado estado;
        float cambio, proximaVez;
        bool termino, arranco;
        Color colorBorde;
        bool leyendoBorde;

        void Start()
        {
            textura = new RenderTexture(tamanoVideo.x, tamanoVideo.y, 0) { name = "Video " + archivo };
            reproductor = gameObject.AddComponent<VideoPlayer>();
            reproductor.playOnAwake = false;
            reproductor.isLooping = false;
            reproductor.source = VideoSource.Url;
            reproductor.url = Path.Combine(Application.streamingAssetsPath, archivo);
            reproductor.renderMode = VideoRenderMode.RenderTexture;
            reproductor.targetTexture = textura;
            reproductor.audioOutputMode = VideoAudioOutputMode.None;
            reproductor.skipOnDrop = true;
            reproductor.waitForFirstFrame = true;
            reproductor.loopPointReached += _ => termino = true;
            reproductor.Prepare();   // queda listo de antemano: el primer barrido no espera

            foreach (var p in paredes) if (p != null) capas.Add(Armar(p));
            Mostrar(false);
            proximaVez = Time.time + primeraVez;
        }

        void OnDestroy()
        {
            if (textura != null) textura.Release();
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
            c.contenido = Hijo("Contenido", lienzo).gameObject;
            var cont = (RectTransform)c.contenido.transform;
            Estirar(cont);
            // Video a toda la altura, centrado, sin deformar, y los costados del color del borde.
            float proporcion = (float)tamanoVideo.x / tamanoVideo.y;
            float alto = p.alto * 1000f, ancho = Mathf.Min(alto * proporcion, p.largo * 1000f);
            alto = ancho / proporcion;
            float costado = Mathf.Max(0f, (p.largo * 1000f - ancho) / 2f) + 2f;
            c.costadoIzq = Bloque("Costado izquierdo", cont, PaletaPost.Oscuro);
            Anclar(c.costadoIzq.rectTransform, 0f, 0f, costado);
            c.costadoDer = Bloque("Costado derecho", cont, PaletaPost.Oscuro);
            Anclar(c.costadoDer.rectTransform, 1f, 1f, costado);
            var video = Hijo("Video", cont, typeof(RawImage)).GetComponent<RawImage>();
            video.texture = textura;
            video.raycastTarget = false;
            video.rectTransform.sizeDelta = new Vector2(ancho, alto);

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

        /// <summary>Arranca la secuencia (barrido verde → video → negro → barrido de salida).</summary>
        public void Arrancar()
        {
            if (estado != Estado.Oculto) return;
            estado = Estado.Entrando;
            cambio = Time.time;
            termino = arranco = false;
            colorBorde = verde;
            Mostrar(true);
            foreach (var c in capas)
            {
                Barrido(c.verde, 0f, 0f);
                c.costadoIzq.color = c.costadoDer.color = verde;
            }
            if (!reproductor.isPrepared) reproductor.Prepare();
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
                    // El verde entra desde la izquierda y cubre toda la pantalla en 2 s.
                    // Sin barrido: no se tapa nada y el video arranca apenas está listo.
                    foreach (var c in capas) Barrido(c.verde, 0f, conBarrido ? suave : 0f);
                    if ((p >= 1f || !conBarrido) && reproductor.isPrepared)
                    {
                        reproductor.time = 0;
                        reproductor.Play();
                        estado = Estado.Video;
                    }
                    break;

                case Estado.Video:
                    // El verde queda hasta que el video muestra su primer cuadro (que también es verde).
                    if (!arranco && reproductor.isPlaying && reproductor.frame > 0)
                    {
                        arranco = true;
                        foreach (var c in capas) { c.contenido.SetActive(true); c.verde.gameObject.SetActive(false); }
                    }
                    if (arranco) LeerBorde();
                    if (termino)
                    {
                        // Termina en negro: la pantalla queda en el negro de la marca y empieza a irse.
                        reproductor.Stop();
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
                    break;

                case Estado.Saliendo:
                    // El negro se retira hacia la derecha y deja ver la pantalla de post.
                    foreach (var c in capas) Barrido(c.negro, suave, 1f);
                    if (p >= 1f)
                    {
                        Mostrar(false);
                        estado = Estado.Oculto;
                        ultimoFinal = Time.time;
                        reproductor.Prepare();   // listo para la próxima vez
                    }
                    break;
            }
        }

        /// <summary>Lee el color del borde del video (arriba a la izquierda) para pintar los costados.</summary>
        void LeerBorde()
        {
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
