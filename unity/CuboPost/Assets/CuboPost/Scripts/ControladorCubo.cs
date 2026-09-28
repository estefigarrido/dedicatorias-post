using System.Collections.Generic;
using UnityEngine;

namespace CuboPost
{
    /// <summary>
    /// Reparte las dedicatorias entre las 4 pantallas (siempre en la menos ocupada) y, mientras
    /// haya pocas reales, completa con notas de ejemplo que se van reemplazando.
    /// </summary>
    public class ControladorCubo : MonoBehaviour
    {
        public SupabaseNotas fuente;
        public ParedPantalla[] paredes;

        [Header("Notas de ejemplo")]
        public bool mostrarEjemplos = true;
        public int ejemplosPorPared = 7;

        readonly Dictionary<long, ParedPantalla> dondeEsta = new Dictionary<long, ParedPantalla>();
        int llegadasIniciales;

        static readonly (string para, string mensaje, string color)[] Ejemplos =
        {
            ("profe caro", "Por enseñarme que el descanso también es parte del entrenamiento.", "azul"),
            ("mamá", "Gracias por bancarme en cada carrera, aunque fuera a las 6 de la mañana.", "rosa"),
            ("nacho", "Gracias por esperarme en cada kilómetro. Sin vos no llegaba a los 21k.", "verde"),
            ("el grupo del parque", "Los martes a las 7 no serían lo mismo sin ustedes.", "violeta"),
            ("delfi", "Me enseñaste a respirar cuando quería largar todo.", "azul"),
            ("abuelo", "Por las caminatas de los domingos que me hicieron amar moverme.", "verde"),
            ("cami", "Primer 10k juntas. Van muchos más.", "rosa"),
            ("mi kine", "Volví a correr después de la lesión gracias a tu paciencia infinita.", "violeta"),
            ("juli", "Sos la razón por la que no me quedo en la cama los sábados.", "azul"),
            ("entrenador", "Cada estiramiento que me hiciste repetir valió la pena. Hoy corro sin dolor y te lo debo a vos.", "verde"),
            ("vos", "Gracias por acompañarme en cada entrenamiento bajo la lluvia, por los mates después de las series y por recordarme que parar también es avanzar. Nos vemos en la próxima carrera, compañero de ruta.", "violeta"),
            ("lu", "Por creer en mí antes que yo.", "rosa"),
            ("team peaks", "Arrancamos siendo desconocidos y hoy son mi familia de los domingos. Gracias por cada kilómetro compartido y cada abrazo al llegar.", "azul"),
            ("papá", "Me enseñaste a no rendirme en la última cuadra.", "verde"),
            ("sofi", "Gracias por esperarme siempre al final, aunque llegara última.", "violeta"),
            ("martín", "Ese empujón en el km 30 me salvó la maratón.", "rosa"),
        };

        void Start()
        {
            foreach (var p in paredes)
            {
                p.Inicializar();
                p.NotaRetirada += AlRetirar;
            }

            if (mostrarEjemplos) CargarEjemplos();

            if (fuente != null)
            {
                fuente.NotaRecibida += Recibir;
                fuente.NotaOcultada += Ocultar;
            }
        }

        void CargarEjemplos()
        {
            // Orden mezclado para que cada pared tenga notas de distintos colores y tamaños.
            var orden = new List<int>();
            for (int i = 0; i < Ejemplos.Length; i++) orden.Add(i);
            for (int i = orden.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (orden[i], orden[j]) = (orden[j], orden[i]);
            }
            string[] colores = { "azul", "verde", "violeta", "rosa" };

            int total = ejemplosPorPared * paredes.Length;
            for (int i = 0; i < total; i++)
            {
                var e = Ejemplos[orden[i % orden.Count]];
                // Si un ejemplo se repite, cambia de color.
                string color = i < orden.Count ? e.color : colores[Random.Range(0, colores.Length)];
                var n = new Nota { id = -(i + 1), para = e.para, mensaje = e.mensaje, color = color, demo = true };
                // La pared con más lugar libre que todavía no tenga ese mismo texto.
                var p = ElegirPared(n.mensaje);
                p.Agregar(n, false, 0.3f + i * 0.12f);
                dondeEsta[n.id] = p;
            }
        }

        void Recibir(Nota n, bool inicial)
        {
            var p = ElegirPared();
            // Cada dedicatoria real reemplaza un ejemplo, si quedan.
            p.QuitarUnEjemplo();
            float retardo = inicial ? 0.2f + (llegadasIniciales++) * 0.08f : 0f;
            p.Agregar(n, !inicial, retardo);
            dondeEsta[n.id] = p;
        }

        void Ocultar(long id)
        {
            if (dondeEsta.TryGetValue(id, out var p)) p.Quitar(id);
        }

        void AlRetirar(Nota n)
        {
            dondeEsta.Remove(n.id);
            if (!n.demo && fuente != null) fuente.Olvidar(n.id);
        }

        ParedPantalla ElegirPared(string evitarMensaje = null)
        {
            ParedPantalla mejor = paredes[0];
            float menor = float.MaxValue;
            foreach (var p in paredes)
            {
                float o = p.Ocupacion + Random.Range(0f, 0.03f);
                if (evitarMensaje != null && p.Contiene(evitarMensaje)) o += 10f;
                if (o < menor)
                {
                    menor = o;
                    mejor = p;
                }
            }
            return mejor;
        }
    }
}
