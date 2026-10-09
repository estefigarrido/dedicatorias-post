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
    ///   · La misma tecla otra vez vuelve al reposo.
    ///   · Reposo (sin comando): pantallas en negro con los vectores flotando, luces apagadas y las
    ///     personas de siempre en la zona de stretching.
    /// Las dicroicas son las de LucesDicroicas (la tecla L las sigue cambiando a mano).
    /// </summary>
    public class ControlClase : MonoBehaviour
    {
        enum Momento { Reposo, Calibracion, PreparandoPareja, Pareja }

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
        VideoPlayer reproductor;
        RenderTexture texturaVideo;
        Material[] materiales;

        void Start()
        {
            materiales = new Material[pantallas.Length];
            for (int i = 0; i < pantallas.Length; i++) materiales[i] = pantallas[i].material;   // copia propia

            texturaVideo = new RenderTexture(tamanoVideo.x, tamanoVideo.y, 0) { name = "Video en pareja", wrapMode = TextureWrapMode.Repeat };
            reproductor = gameObject.AddComponent<VideoPlayer>();
            reproductor.playOnAwake = false;
            reproductor.isLooping = false;
            reproductor.source = VideoSource.Url;
            reproductor.url = Path.Combine(Application.streamingAssetsPath, videoPareja);
            reproductor.renderMode = VideoRenderMode.RenderTexture;
            reproductor.targetTexture = texturaVideo;
            reproductor.audioOutputMode = VideoAudioOutputMode.None;
            reproductor.skipOnDrop = true;
            reproductor.loopPointReached += Termino;
            reproductor.Prepare();   // listo de antemano: arranca justo a los 2 s

            Ir(Momento.Reposo);
        }

        void Termino(VideoPlayer _) => videoTermino = true;

        void OnDestroy()
        {
            if (texturaVideo != null) texturaVideo.Release();
        }

        void Update()
        {
            var teclado = Keyboard.current;
            if (teclado != null)
            {
                if (teclado.vKey.wasPressedThisFrame) Ir(momento == Momento.Calibracion ? Momento.Reposo : Momento.Calibracion);
                if (teclado.nKey.wasPressedThisFrame) Ir(momento == Momento.PreparandoPareja || momento == Momento.Pareja ? Momento.Reposo : Momento.PreparandoPareja);
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
            }
        }

        void Ir(Momento nuevo)
        {
            momento = nuevo;
            desde = Time.time;
            if (nuevo != Momento.Pareja && reproductor != null && reproductor.isPlaying) { reproductor.Stop(); reproductor.Prepare(); }
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
