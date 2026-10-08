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
        public float brilloLente = 4f;

        const Key tecla = Key.L;
        Light[] luces;
        Renderer[] lentes;
        MaterialPropertyBlock bloque;

        void Start()
        {
            luces = GetComponentsInChildren<Light>(true);
            lentes = System.Array.FindAll(GetComponentsInChildren<Renderer>(true), r => r.name == "Lente");
            bloque = new MaterialPropertyBlock();
            Aplicar();
        }

        void Update()
        {
            var teclado = Keyboard.current;
            if (teclado == null || !teclado[tecla].wasPressedThisFrame) return;
            modo = modo == Modo.Apagadas || modo == Modo.Violeta ? Modo.Blanco : modo + 1;
            Aplicar();
        }

        Color ColorActual => modo == Modo.Verde ? verde : modo == Modo.Violeta ? violeta : blanco;

        void Aplicar()
        {
            bool prendidas = modo != Modo.Apagadas;
            var c = ColorActual;
            foreach (var l in luces) { l.enabled = prendidas; l.color = c; }
            foreach (var r in lentes)
            {
                r.GetPropertyBlock(bloque);
                bloque.SetColor("_EmissionColor", prendidas ? c * brilloLente : Color.black);
                bloque.SetColor("_BaseColor", prendidas ? Color.Lerp(c, Color.white, 0.5f) : new Color(0.25f, 0.25f, 0.25f));
                r.SetPropertyBlock(bloque);
            }
        }
    }
}
