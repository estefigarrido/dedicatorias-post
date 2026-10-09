using UnityEngine;
using UnityEngine.InputSystem;

namespace CuboPost
{
    /// <summary>
    /// Música de la clase con la tecla C: arranca en loop y entra con un fade in suave de 5 s.
    /// Con la C otra vez se va bajando y se corta.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MusicaClase : MonoBehaviour
    {
        public AudioClip musica;
        [Range(0f, 1f)] public float volumen = 0.8f;
        [Tooltip("Segundos que tarda en llegar al volumen al arrancar.")]
        public float fadeIn = 5f;
        [Tooltip("Segundos que tarda en apagarse al cortarla.")]
        public float fadeOut = 1.5f;

        const Key tecla = Key.C;
        AudioSource fuente;
        bool sonando;

        void Awake()
        {
            fuente = GetComponent<AudioSource>();
            fuente.clip = musica;
            fuente.loop = true;
            fuente.playOnAwake = false;
            fuente.spatialBlend = 0f;   // se escucha igual en toda la escena
            fuente.volume = 0f;
        }

        void Update()
        {
            var teclado = Keyboard.current;
            if (teclado != null && teclado[tecla].wasPressedThisFrame)
            {
                sonando = !sonando;
                if (sonando && !fuente.isPlaying) { fuente.volume = 0f; fuente.Play(); }
            }

            if (!fuente.isPlaying) return;
            float objetivo = sonando ? volumen : 0f;
            float segundos = sonando ? fadeIn : fadeOut;
            fuente.volume = Mathf.MoveTowards(fuente.volume, objetivo, volumen * Time.deltaTime / Mathf.Max(0.05f, segundos));
            if (!sonando && fuente.volume <= 0f) fuente.Stop();
        }
    }
}
