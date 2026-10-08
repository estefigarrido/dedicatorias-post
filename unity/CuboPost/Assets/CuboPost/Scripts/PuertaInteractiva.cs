using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CuboPost
{
    /// <summary>
    /// Puerta que se abre y se cierra con la tecla P cuando la vista apunta a ella (centro de la
    /// pantalla, hasta <see cref="alcance"/> metros). Va en la bisagra: gira entre las dos rotaciones.
    /// </summary>
    public class PuertaInteractiva : MonoBehaviour
    {
        public Quaternion cerrada = Quaternion.identity;
        public Quaternion abierta = Quaternion.identity;
        public bool estaAbierta;
        [Tooltip("Segundos que tarda en abrirse o cerrarse.")]
        public float segundos = 0.8f;

        const float alcance = 4f;
        const Key tecla = Key.P;
        static readonly List<PuertaInteractiva> todas = new List<PuertaInteractiva>();
        static int ultimoCuadro = -1;
        float avance;   // 0 cerrada, 1 abierta

        void OnEnable() => todas.Add(this);
        void OnDisable() => todas.Remove(this);

        void Start()
        {
            avance = estaAbierta ? 1f : 0f;
            transform.rotation = estaAbierta ? abierta : cerrada;
        }

        public void Alternar() => estaAbierta = !estaAbierta;

        void Update()
        {
            // Una sola puerta por cuadro lee la tecla y busca a cuál se está mirando.
            if (ultimoCuadro != Time.frameCount)
            {
                ultimoCuadro = Time.frameCount;
                var teclado = Keyboard.current;
                var cam = Camera.main;
                if (teclado != null && teclado[tecla].wasPressedThisFrame && cam != null)
                {
                    var rayo = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                    foreach (var hit in Physics.RaycastAll(rayo, alcance, ~0, QueryTriggerInteraction.Ignore))
                    {
                        var puerta = hit.collider.GetComponentInParent<PuertaInteractiva>();
                        if (puerta != null) { puerta.Alternar(); break; }
                    }
                }
            }

            float objetivo = estaAbierta ? 1f : 0f;
            if (Mathf.Approximately(avance, objetivo)) return;
            avance = Mathf.MoveTowards(avance, objetivo, Time.deltaTime / Mathf.Max(0.05f, segundos));
            transform.rotation = Quaternion.Slerp(cerrada, abierta, Mathf.SmoothStep(0f, 1f, avance));
        }
    }
}
