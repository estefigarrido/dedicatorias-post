using UnityEngine;
using UnityEngine.InputSystem;

namespace CuboPost
{
    /// <summary>
    /// Dicroicas LED de la sala y los pasillos. Arrancan apagadas; con la tecla L se encienden en
    /// blanco y después pasan a verde, violeta y otra vez blanco (como el control remoto).
    /// Cada luz es un hijo con su Light y su artefacto (el lente brilla del color de la luz).
    /// </summary>
    public class LucesDicroicas : MonoBehaviour
    {
        public enum Modo { Apagadas, Blanco, Verde, Violeta }

        public Modo modo = Modo.Apagadas;
        public Color blanco = new Color(1f, 0.95f, 0.86f);
        public Color verde = new Color(0.29f, 0.85f, 0.42f);
        public Color violeta = new Color(0.62f, 0.38f, 1f);
        [Tooltip("Brillo del lente encendido.")]
        public float brilloLente = 14f;
        [Tooltip("Opacidad del halo que la luz deja en el techo alrededor del artefacto.")]
        [Range(0f, 1f)] public float halo = 0.55f;

        const Key tecla = Key.L;
        Light[] luces;
        Renderer[] lentes, halos;
        MaterialPropertyBlock bloque;

        void Start()
        {
            Preparar();
            Aplicar();
        }

        void Preparar()
        {
            if (bloque != null) return;
            luces = GetComponentsInChildren<Light>(true);
            lentes = System.Array.FindAll(GetComponentsInChildren<Renderer>(true), r => r.name == "Lente");
            halos = System.Array.FindAll(GetComponentsInChildren<Renderer>(true), r => r.name == "Halo");
            bloque = new MaterialPropertyBlock();
        }

        void Update()
        {
            var teclado = Keyboard.current;
            if (teclado == null || !teclado[tecla].wasPressedThisFrame) return;
            modo = modo == Modo.Apagadas || modo == Modo.Violeta ? Modo.Blanco : modo + 1;
            Aplicar();
        }

        Color ColorActual => modo == Modo.Verde ? verde : modo == Modo.Violeta ? violeta : blanco;

        void Aplicar() => Fijar(modo != Modo.Apagadas, ColorActual);

        /// <summary>Prende o apaga todas con un color cualquiera (lo usan los comandos de la clase, V y N).</summary>
        public void Fijar(bool prendidas, Color c)
        {
            Preparar();
            foreach (var l in luces) { l.enabled = prendidas; l.color = c; }
            foreach (var r in lentes)
            {
                r.GetPropertyBlock(bloque);
                bloque.SetColor("_EmissionColor", prendidas ? c * brilloLente : Color.black);
                bloque.SetColor("_BaseColor", prendidas ? Color.Lerp(c, Color.white, 0.5f) : new Color(0.25f, 0.25f, 0.25f));
                r.SetPropertyBlock(bloque);
            }
            foreach (var r in halos)
            {
                r.enabled = prendidas;
                r.GetPropertyBlock(bloque);
                bloque.SetColor("_BaseColor", new Color(c.r, c.g, c.b, halo));
                r.SetPropertyBlock(bloque);
            }
        }
    }
}
