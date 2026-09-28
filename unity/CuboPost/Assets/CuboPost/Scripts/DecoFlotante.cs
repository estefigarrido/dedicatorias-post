using UnityEngine;

namespace CuboPost
{
    /// <summary>Hace flotar suavemente un objeto de la pantalla (círculos y estrellas de la pieza post.).</summary>
    public class DecoFlotante : MonoBehaviour
    {
        public Vector2 amplitud = new Vector2(40f, 90f);   // unidades de diseño
        public float giro = 12f;                            // grados
        public float periodo = 7f;                          // segundos
        public float fase;

        RectTransform rt;
        Vector2 origen;
        float giroBase;

        void Start()
        {
            rt = (RectTransform)transform;
            origen = rt.anchoredPosition;
            giroBase = rt.localEulerAngles.z;
        }

        void Update()
        {
            float t = (Time.time / periodo + fase) * Mathf.PI * 2f;
            rt.anchoredPosition = origen + new Vector2(Mathf.Sin(t * 0.7f) * amplitud.x, Mathf.Sin(t) * amplitud.y);
            rt.localRotation = Quaternion.Euler(0, 0, giroBase + Mathf.Sin(t * 0.5f) * giro);
        }
    }
}
