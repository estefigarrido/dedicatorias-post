using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CuboPost
{
    /// <summary>
    /// Puerta que se abre y se cierra con la tecla P. Se apunta con el mouse: si el cursor está libre,
    /// la que está debajo del puntero; si se está caminando (cursor tomado), la del centro de la vista.
    /// La puerta apuntada se ilumina apenas. Va en la bisagra: gira entre las dos rotaciones.
    /// </summary>
    public class PuertaInteractiva : MonoBehaviour
    {
        public Quaternion cerrada = Quaternion.identity;
        public Quaternion abierta = Quaternion.identity;
        public bool estaAbierta;
        [Tooltip("Segundos que tarda en abrirse o cerrarse.")]
        public float segundos = 0.8f;

        const float alcanceCaminando = 4f, alcanceMouse = 40f;
        const Key tecla = Key.P;
        static readonly List<PuertaInteractiva> todas = new List<PuertaInteractiva>();
        static int ultimoCuadro = -1;
        static PuertaInteractiva apuntada;
        float avance;   // 0 cerrada, 1 abierta
        Renderer[] partes;
        MaterialPropertyBlock bloque;
        bool resaltada;

        void OnEnable() => todas.Add(this);
        void OnDisable() { todas.Remove(this); if (apuntada == this) apuntada = null; }

        void Start()
        {
            avance = estaAbierta ? 1f : 0f;
            transform.rotation = estaAbierta ? abierta : cerrada;
            partes = GetComponentsInChildren<Renderer>(true);
            bloque = new MaterialPropertyBlock();
        }

        public void Alternar() => estaAbierta = !estaAbierta;

        /// <summary>Qué puerta se está apuntando: con el puntero del mouse o con el centro de la vista.</summary>
        static PuertaInteractiva Apuntar()
        {
            var cam = Camera.main;
            if (cam == null) return null;
            bool cursorLibre = Cursor.lockState != CursorLockMode.Locked && Mouse.current != null;
            var rayo = cursorLibre ? cam.ScreenPointToRay(Mouse.current.position.ReadValue()) : cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float alcance = cursorLibre ? alcanceMouse : alcanceCaminando;
            // El primer objeto que toca el rayo: si es una puerta, esa (no se apunta a través de paredes).
            if (Physics.Raycast(rayo, out var hit, alcance, ~0, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<PuertaInteractiva>();
            return null;
        }

        void Update()
        {
            // Una sola puerta por cuadro busca a cuál se apunta y lee la tecla.
            if (ultimoCuadro != Time.frameCount)
            {
                ultimoCuadro = Time.frameCount;
                apuntada = Apuntar();
                var teclado = Keyboard.current;
                if (teclado != null && teclado[tecla].wasPressedThisFrame && apuntada != null) apuntada.Alternar();
            }
            Resaltar(apuntada == this);

            float objetivo = estaAbierta ? 1f : 0f;
            if (Mathf.Approximately(avance, objetivo)) return;
            avance = Mathf.MoveTowards(avance, objetivo, Time.deltaTime / Mathf.Max(0.05f, segundos));
            transform.rotation = Quaternion.Slerp(cerrada, abierta, Mathf.SmoothStep(0f, 1f, avance));
        }

        void Resaltar(bool si)
        {
            if (si == resaltada || partes == null) return;
            resaltada = si;
            foreach (var r in partes)
            {
                r.GetPropertyBlock(bloque);
                bloque.SetColor("_EmissionColor", si ? new Color(0.29f, 0.72f, 0.4f) * 0.35f : Color.black);
                r.SetPropertyBlock(bloque);
                foreach (var m in r.sharedMaterials) if (m != null && si) m.EnableKeyword("_EMISSION");
            }
        }
    }
}
