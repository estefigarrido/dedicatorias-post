using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CuboPost
{
    /// <summary>
    /// Reparte las dedicatorias entre las pantallas que reciben notas (<see cref="ParedPantalla.recibeNotas"/>),
    /// siempre en la menos ocupada. Por ahora solo las recibe la pantalla del frente.
    /// Cada nota se ve un rato y desaparece (ver <see cref="ParedPantalla"/>):
    ///   · las que llegan desde la web aparecen apenas se publican;
    ///   · mientras tanto rotan las que ya se habían enviado, de a una;
    ///   · si todavía no hay ninguna real, rotan las notas de ejemplo.
    /// </summary>
    public class ControladorCubo : MonoBehaviour
    {
        public SupabaseNotas fuente;
        public ParedPantalla[] paredes;

        [Header("Rotación")]
        [Tooltip("Cuántas notas de la rotación puede tener cada pantalla a la vez. Las recién publicadas aparecen igual.")]
        public int notasPorPantalla = 3;
        [Tooltip("Cada cuántos segundos entra la próxima nota de la rotación.")]
        public float segundosEntreNotas = 3f;

        [Header("Notas de ejemplo")]
        [Tooltip("Si todavía no hay dedicatorias reales, rotan estas.")]
        public bool mostrarEjemplos = true;

        readonly List<Nota> historial = new List<Nota>();
        readonly List<Nota> ejemplos = new List<Nota>();
        readonly Dictionary<long, ParedPantalla> enPantalla = new Dictionary<long, ParedPantalla>();
        int siguienteHistorial, siguienteEjemplo;
        bool avisoPrimera;

        static readonly (string para, string mensaje, string color)[] Ejemplos =
        {
            ("profe caro", "Por enseñarme que el descanso también es parte del entrenamiento.", "azul"),
            ("mamá", "Gracias por bancarme en cada carrera, aunque fuera a las 6 de la mañana.", "rosa"),
            ("nacho", "Gracias por esperarme en cada kilómetro. Sin vos no llegaba a los 21k.", "verde"),
            ("el grupo del parque", "Los martes a las 7 no serían lo mismo sin ustedes.", "violeta"),
            ("delfi", "Me enseñaste a respirar cuando quería largar todo.", "crema"),
            ("abuelo", "Por las caminatas de los domingos que me hicieron amar moverme.", "verde"),
            ("cami", "Primer 10k juntas. Van muchos más.", "rosa"),
            ("mi kine", "Volví a correr después de la lesión gracias a tu paciencia infinita.", "violeta"),
            ("juli", "Sos la razón por la que no me quedo en la cama los sábados.", "azul"),
            ("entrenador", "Cada estiramiento que me hiciste repetir valió la pena. Hoy corro sin dolor y te lo debo a vos.", "verde"),
            ("lu", "Por creer en mí antes que yo.", "crema"),
            ("team peaks", "Arrancamos siendo desconocidos y hoy son mi familia de los domingos. Gracias por cada kilómetro.", "azul"),
            ("papá", "Me enseñaste a no rendirme en la última cuadra.", "verde"),
            ("sofi", "Gracias por esperarme siempre al final, aunque llegara última.", "violeta"),
            ("martín", "Ese empujón en el km 30 me salvó la maratón.", "rosa"),
        };

        void Start()
        {
            foreach (var p in paredes)
            {
                p.Inicializar();
                p.NotaRetirada += n => enPantalla.Remove(n.id);
            }

            for (int i = 0; i < Ejemplos.Length; i++)
            {
                var e = Ejemplos[i];
                ejemplos.Add(new Nota { id = -(i + 1), para = e.para, mensaje = e.mensaje, color = e.color, demo = true });
            }
            Mezclar(ejemplos);

            if (fuente != null)
            {
                fuente.NotaRecibida += Recibir;
                fuente.NotaOcultada += Ocultar;
            }

            StartCoroutine(Rotar());
        }

        /// <summary>
        /// inicial = true: nota vieja (carga al arrancar), entra a la rotación sin mostrarse ya.
        /// Las de la carga inicial llegan de la más vieja a la más nueva y se ponen adelante, así la
        /// rotación arranca por las más recientes.
        /// </summary>
        void Recibir(Nota n, bool inicial)
        {
            if (historial.Exists(x => x.id == n.id)) return;
            if (inicial) historial.Insert(0, n);
            else historial.Add(n);
            if (!inicial) Mostrar(n, ElegirPared(n.mensaje));
        }

        void Ocultar(long id)
        {
            historial.RemoveAll(x => x.id == id);
            if (enPantalla.TryGetValue(id, out var p)) p.Quitar(id);
        }

        IEnumerator Rotar()
        {
            yield return new WaitForSeconds(1f);
            var espera = new WaitForSeconds(segundosEntreNotas);
            while (true)
            {
                var p = ParedConLugar();
                if (p != null)
                {
                    var n = historial.Count > 0 ? Proxima(historial, ref siguienteHistorial)
                          : mostrarEjemplos ? Proxima(ejemplos, ref siguienteEjemplo)
                          : null;
                    if (n != null) Mostrar(n, p);
                }
                yield return espera;
            }
        }

        void Mostrar(Nota n, ParedPantalla p)
        {
            if (p == null || enPantalla.ContainsKey(n.id)) return;
            enPantalla[n.id] = p;
            p.Agregar(n);
            if (!avisoPrimera && !n.demo)
            {
                avisoPrimera = true;
                Debug.Log($"[post.] Primera dedicatoria del sitio en pantalla: nota {n.id} en \"{p.name}\".");
            }
        }

        /// <summary>La siguiente de la lista (en orden, dando la vuelta) que no esté ya en pantalla.</summary>
        Nota Proxima(List<Nota> lista, ref int indice)
        {
            for (int i = 0; i < lista.Count; i++)
            {
                int k = (indice + i) % lista.Count;
                if (enPantalla.ContainsKey(lista[k].id)) continue;
                indice = k + 1;
                return lista[k];
            }
            return null;
        }

        /// <summary>La pantalla menos ocupada que todavía tenga lugar en la rotación.</summary>
        ParedPantalla ParedConLugar()
        {
            ParedPantalla mejor = null;
            float menor = float.MaxValue;
            foreach (var p in paredes)
            {
                if (!p.recibeNotas || p.Cantidad >= notasPorPantalla) continue;
                float o = p.Ocupacion + Random.Range(0f, 0.03f);
                if (o < menor)
                {
                    menor = o;
                    mejor = p;
                }
            }
            return mejor;
        }

        /// <summary>La pantalla menos ocupada entre las que reciben notas (null si ninguna las recibe).</summary>
        ParedPantalla ElegirPared(string evitarMensaje)
        {
            ParedPantalla mejor = null;
            float menor = float.MaxValue;
            foreach (var p in paredes)
            {
                if (!p.recibeNotas) continue;
                float o = p.Ocupacion + Random.Range(0f, 0.03f);
                if (p.Contiene(evitarMensaje)) o += 10f;
                if (o < menor)
                {
                    menor = o;
                    mejor = p;
                }
            }
            return mejor;
        }

        static void Mezclar(List<Nota> lista)
        {
            for (int i = lista.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (lista[i], lista[j]) = (lista[j], lista[i]);
            }
        }
    }
}
