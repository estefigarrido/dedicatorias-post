using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace CuboPost
{
    /// <summary>
    /// Lee las dedicatorias que la gente publica desde la web (Supabase).
    /// Solo lee notas visibles: las moderadas (visible = false) no llegan y, si ya estaban
    /// en pantalla, se avisa con <see cref="NotaOcultada"/>.
    /// </summary>
    public class SupabaseNotas : MonoBehaviour
    {
        [Header("Supabase (mismos datos que docs/config.js del sitio)")]
        public string supabaseUrl = "https://fohbovhqtjppdzghlujf.supabase.co";
        public string clavePublica = "sb_publishable_RPH-T9goUD0Q65lX3sjbqg_f0e2Te3t";

        [Header("Consultas")]
        [Tooltip("Cada cuántos segundos se buscan dedicatorias nuevas.")]
        public float segundosEntreConsultas = 2f;
        [Tooltip("Cada cuántos segundos se revisa si alguien moderó (ocultó) una nota.")]
        public float segundosRevisarModeracion = 20f;
        [Tooltip("Cuántas notas (las más recientes) se cargan al arrancar.")]
        public int cargaInicial = 60;

        /// <summary>Nota nueva. El bool es true si llegó en la carga inicial (sin animación de llegada).</summary>
        public event Action<Nota, bool> NotaRecibida;
        public event Action<long> NotaOcultada;

        public bool Conectado { get; private set; }

        const string Campos = "id,para,mensaje,color,hex,tamano,creada";
        long ultimoId;
        readonly HashSet<long> enPantalla = new HashSet<long>();
        bool avisoError;

        IEnumerator Start()
        {
            if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(clavePublica))
            {
                Debug.LogWarning("[post.] Falta la URL o la clave de Supabase: solo se muestran notas de ejemplo.");
                yield break;
            }

            yield return Consultar($"select={Campos}&order=id.desc&limit={cargaInicial}", notas =>
            {
                Array.Reverse(notas);
                foreach (var n in notas) Entregar(n, true);
            });

            StartCoroutine(BuscarNuevas());
            StartCoroutine(RevisarModeracion());
        }

        IEnumerator BuscarNuevas()
        {
            var espera = new WaitForSeconds(segundosEntreConsultas);
            while (true)
            {
                yield return espera;
                yield return Consultar($"select={Campos}&id=gt.{ultimoId}&order=id.asc&limit=20", notas =>
                {
                    foreach (var n in notas) Entregar(n, false);
                });
            }
        }

        IEnumerator RevisarModeracion()
        {
            var espera = new WaitForSeconds(segundosRevisarModeracion);
            while (true)
            {
                yield return espera;
                if (enPantalla.Count == 0) continue;
                yield return Consultar("select=id", notas =>
                {
                    var visibles = new HashSet<long>();
                    foreach (var n in notas) visibles.Add(n.id);
                    var ocultas = new List<long>();
                    foreach (var id in enPantalla) if (!visibles.Contains(id)) ocultas.Add(id);
                    foreach (var id in ocultas)
                    {
                        enPantalla.Remove(id);
                        NotaOcultada?.Invoke(id);
                    }
                });
            }
        }

        void Entregar(Nota n, bool inicial)
        {
            if (n == null || enPantalla.Contains(n.id)) return;
            ultimoId = Math.Max(ultimoId, n.id);
            enPantalla.Add(n.id);
            NotaRecibida?.Invoke(n, inicial);
        }

        IEnumerator Consultar(string query, Action<Nota[]> alRecibir)
        {
            var url = supabaseUrl.TrimEnd('/') + "/rest/v1/notas?" + query;
            using (var req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("apikey", clavePublica);
                if (clavePublica.StartsWith("eyJ")) req.SetRequestHeader("Authorization", "Bearer " + clavePublica);
                req.timeout = 10;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Conectado = false;
                    if (!avisoError) Debug.LogWarning($"[post.] No se pudo leer Supabase: {req.error}. Se sigue intentando.");
                    avisoError = true;
                    yield break;
                }

                Conectado = true;
                avisoError = false;
                var lista = JsonUtility.FromJson<ListaNotas>("{\"items\":" + req.downloadHandler.text + "}");
                alRecibir(lista?.items ?? Array.Empty<Nota>());
            }
        }

        /// <summary>Para que el controlador libere el id cuando una nota sale de pantalla.</summary>
        public void Olvidar(long id) => enPantalla.Remove(id);
    }
}
