using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;

namespace CuboPost
{
    /// <summary>
    /// Comandos de teclado para los momentos de la clase, dentro de la cabina (los activa la persona de
    /// sistemas):
    ///   · V · calibración: las 8 pantallas individuales de cada pared muestran "Extendé los brazos…"
    ///     2 s y después "¡Calibración lista!", hasta que se toque otro comando. Dicroicas en blanco y
    ///     personas 3D con los brazos estirados (en T).
    ///   · N · en pareja: dicroicas en verde (las personas no cambian); a los 2 s las
    ///     pantallas pasan el video "en pareja" y las dicroicas van de verde a violeta en 1 s. Cuando el
    ///     video termina, vuelve al reposo.
    ///   · B · estirando hombro: dicroicas en verde y en cada pantalla individual el video de Lucas, del
    ///     segundo 9 al 14, en loop hasta que se toque otro comando (las personas no cambian).
    ///   · La misma tecla otra vez vuelve al reposo.
    ///   · Reposo (sin comando): pantallas en negro con los vectores flotando, luces apagadas y las
    ///     personas de siempre en la zona de stretching.
    /// Las dicroicas son las de LucesDicroicas (la tecla L las sigue cambiando a mano).
    /// </summary>
    public class ControlClase : MonoBehaviour
    {
        enum Momento { Reposo, Calibracion, PreparandoPareja, Pareja, Hombro }

        [Header("Pantallas (las dos paredes largas de la sala)")]
        public Renderer[] pantallas;                 // las 8 pantallas individuales de cada pared (un quad)
        public GameObject[] vectores;                // vectores flotando del reposo
        public Texture calibrandoTextura;            // "Extendé los brazos hacia los costados…"
        public Texture calibracionListaTextura;      // "¡Calibración lista!"
        public float segundosCalibrando = 2f;
        [Tooltip("Video de StreamingAssets: 4 pantallas individuales; se repite 2 veces a lo ancho de cada pared.")]
        public string videoPareja = "en-pareja.mp4";
        public Vector2Int tamanoVideo = new Vector2Int(4096, 1732);
        public float segundosAntesDelVideo = 2f;
        public float segundosVerdeAVioleta = 1f;
        [Tooltip("Video de StreamingAssets: una sola pantalla individual (1080 × 1824); se repite en las 8 de cada pared.")]
        public string videoHombro = "estirando-hombro.mp4";
        public Vector2Int tamanoVideoHombro = new Vector2Int(1080, 1824);
        [Tooltip("Tramo del video que se pasa en loop, en segundos.")]
        public float inicioHombro = 9f, finHombro = 14f;

        [Header("Dicroicas (las mismas de la tecla L)")]
        public LucesDicroicas dicroicas;

        Color blanco => dicroicas != null ? dicroicas.blanco : Color.white;
        Color verde => dicroicas != null ? dicroicas.verde : Color.green;
        Color violeta => dicroicas != null ? dicroicas.violeta : Color.magenta;

        [Header("Personas 3D")]
        public GameObject personasReposo;            // las de siempre
        [Tooltip("Brazos estirados (calibración). Si está vacío, quedan las de reposo.")]
        public GameObject personasEnT;
        public GameObject personasParadas;           // brazos al costado (en pareja)

        Momento momento;
        float desde;
        bool videoTermino;
        VideoPlayer reproductor, reproductorHombro;
        RenderTexture texturaVideo, texturaHombro;
        bool buscando;
        float buscandoDesde;
        Material[] materiales;

        void Start()
        {
            materiales = new Material[pantallas.Length];
            for (int i = 0; i < pantallas.Length; i++) materiales[i] = pantallas[i].material;   // copia propia

            reproductor = Reproductor(videoPareja, tamanoVideo, "Video en pareja", false, out texturaVideo);
            reproductor.loopPointReached += Termino;
            reproductorHombro = Reproductor(videoHombro, tamanoVideoHombro, "Video estirando hombro", true, out texturaHombro);
            reproductorHombro.seekCompleted += Salto;

            Ir(Momento.Reposo);
        }

        /// <summary>Un VideoPlayer de StreamingAssets que dibuja en su propia textura, preparado de antemano.</summary>
        VideoPlayer Reproductor(string archivo, Vector2Int tamano, string nombre, bool loop, out RenderTexture textura)
        {
            textura = new RenderTexture(tamano.x, tamano.y, 0) { name = nombre, wrapMode = TextureWrapMode.Repeat };
            var r = gameObject.AddComponent<VideoPlayer>();
            r.playOnAwake = false;
            r.isLooping = loop;
            r.source = VideoSource.Url;
            r.url = Path.Combine(Application.streamingAssetsPath, archivo);
            r.renderMode = VideoRenderMode.RenderTexture;
            r.targetTexture = textura;
            r.audioOutputMode = VideoAudioOutputMode.None;
            r.skipOnDrop = true;
            r.Prepare();   // listo de antemano: arranca sin demora
            return r;
        }

        void Termino(VideoPlayer _) => videoTermino = true;
        void Salto(VideoPlayer _) => buscando = false;

        void OnDestroy()
        {
            if (texturaVideo != null) texturaVideo.Release();
            if (texturaHombro != null) texturaHombro.Release();
        }

        void Update()
        {
            var teclado = Keyboard.current;
            if (teclado != null)
            {
                if (teclado.vKey.wasPressedThisFrame) Ir(momento == Momento.Calibracion ? Momento.Reposo : Momento.Calibracion);
                if (teclado.nKey.wasPressedThisFrame) Ir(momento == Momento.PreparandoPareja || momento == Momento.Pareja ? Momento.Reposo : Momento.PreparandoPareja);
                if (teclado.bKey.wasPressedThisFrame) Ir(momento == Momento.Hombro ? Momento.Reposo : Momento.Hombro);
            }

            float t = Time.time - desde;
            switch (momento)
            {
                case Momento.Calibracion:
                    Pantallas(t < segundosCalibrando ? calibrandoTextura : calibracionListaTextura, Vector2.one);
                    break;
                case Momento.PreparandoPareja:
                    if (t >= segundosAntesDelVideo && reproductor.isPrepared)
                    {
                        reproductor.time = 0;
                        reproductor.Play();
                        videoTermino = false;
                        momento = Momento.Pareja;
                        desde = Time.time;
                    }
                    break;
                case Momento.Pareja:
                    // Verde → violeta en 1 s, apenas se encienden las pantallas.
                    Luces(true, Color.Lerp(verde, violeta, Mathf.SmoothStep(0f, 1f, t / segundosVerdeAVioleta)));
                    if (reproductor.frame > 0) Pantallas(texturaVideo, new Vector2(2f, 1f));
                    if (videoTermino) Ir(Momento.Reposo);
                    break;
                case Momento.Hombro:
                    Hombro();
                    break;
            }
        }

        /// <summary>Pasa solo el tramo inicioHombro–finHombro, en loop: al llegar al final vuelve al inicio.</summary>
        void Hombro()
        {
            var r = reproductorHombro;
            if (buscando && Time.time - buscandoDesde > 1.5f) buscando = false;   // por si el aviso del salto no llega
            if (!r.isPrepared || buscando) return;   // mientras salta al segundo 9, se espera
            if (!r.isPlaying) { r.Play(); Saltar(); return; }
            if (r.frame < 0) return;
            double t = r.time;
            // Al final del tramo se reinicia (saltar hacia atrás con el video andando no siempre responde).
            if (t >= finHombro || t < inicioHombro - 0.25) { r.Stop(); r.Play(); Saltar(); return; }
            // Recién cuando el video ya está en el tramo se muestra (antes la textura tiene el primer cuadro).
            Pantallas(texturaHombro, new Vector2(8f, 1f));
        }

        void Saltar()
        {
            buscando = true;
            buscandoDesde = Time.time;
            reproductorHombro.time = inicioHombro;
        }

        void Ir(Momento nuevo)
        {
            momento = nuevo;
            desde = Time.time;
            if (nuevo != Momento.Pareja && reproductor != null && reproductor.isPlaying) { reproductor.Stop(); reproductor.Prepare(); }
            if (nuevo != Momento.Hombro && reproductorHombro != null && reproductorHombro.isPlaying) { reproductorHombro.Stop(); reproductorHombro.Prepare(); }
            switch (nuevo)
            {
                case Momento.Reposo:
                    Pantallas(null, Vector2.one);
                    Luces(false, blanco);
                    Personas(personasReposo);
                    break;
                case Momento.Calibracion:
                    Pantallas(calibrandoTextura, Vector2.one);
                    Luces(true, blanco);
                    Personas(personasEnT);
                    break;
                case Momento.PreparandoPareja:
                    Pantallas(null, Vector2.one);   // las pantallas siguen en reposo estos 2 s
                    Luces(true, verde);
                    Personas(personasReposo);   // en pareja solo cambian las pantallas y las luces: las personas siguen siendo las mismas
                    break;
                case Momento.Hombro:
                    Pantallas(null, Vector2.one);   // hasta que el video llega al segundo 9
                    Luces(true, verde);
                    Personas(personasReposo);   // por ahora las personas no cambian
                    buscando = false;
                    break;
            }
        }

        /// <summary>Muestra una textura en las pantallas individuales (null = reposo: negro con vectores).</summary>
        void Pantallas(Texture tex, Vector2 repeticion)
        {
            bool activa = tex != null;
            for (int i = 0; i < pantallas.Length; i++)
            {
                pantallas[i].enabled = activa;
                if (!activa) continue;
                if (materiales[i].mainTexture != tex) materiales[i].mainTexture = tex;
                materiales[i].mainTextureScale = repeticion;
            }
            foreach (var v in vectores) if (v != null && v.activeSelf == activa) v.SetActive(!activa);
        }

        void Luces(bool prendidas, Color c)
        {
            if (dicroicas != null) dicroicas.Fijar(prendidas, c);
        }

        void Personas(GameObject activas)
        {
            if (activas == null) activas = personasReposo;   // mientras no estén las de pose en T
            foreach (var g in new[] { personasReposo, personasEnT, personasParadas })
                if (g != null) g.SetActive(g == activas);
        }
    }
}
