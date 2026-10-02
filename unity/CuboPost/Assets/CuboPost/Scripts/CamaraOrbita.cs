using UnityEngine;
using UnityEngine.InputSystem;

namespace CuboPost
{
    /// <summary>
    /// Cámara para mostrar el cubo:
    ///   arrastrar con el mouse = girar · rueda = acercar/alejar
    ///   1 frente · 2 derecha · 3 fondo · 4 izquierda (a la altura de la gente en la fila)
    ///   0 vista general · R giro automático on/off
    /// </summary>
    public class CamaraOrbita : MonoBehaviour
    {
        public Vector3 objetivo = new Vector3(0f, 1.6f, 0f);
        public float distancia = 24f;
        public float giro = -32f;      // yaw
        public float inclinacion = 14f; // pitch
        public float distanciaMin = 3f, distanciaMax = 120f;
        public bool giroAutomatico = true;
        public float velocidadAutomatica = 3f;
        public float suavizado = 4f;

        [Header("Medidas del cubo (para las vistas 1-4)")]
        public float ancho = 20f;
        public float profundidad = 10f;

        Vector3 objetivoActual;
        float distanciaActual, giroActual, inclinacionActual;
        float distanciaGeneral;   // la de la vista general (tecla 0): la que tenía al arrancar

        void Start()
        {
            distanciaGeneral = distancia;
            objetivoActual = objetivo;
            distanciaActual = distancia;
            giroActual = giro;
            inclinacionActual = inclinacion;
            Aplicar();
        }

        void Update()
        {
            var mouse = Mouse.current;
            var teclado = Keyboard.current;

            if (mouse != null)
            {
                if (mouse.leftButton.isPressed || mouse.rightButton.isPressed)
                {
                    var d = mouse.delta.ReadValue();
                    giro += d.x * 0.25f;
                    inclinacion = Mathf.Clamp(inclinacion - d.y * 0.2f, -5f, 80f);
                    giroAutomatico = false;
                }
                float rueda = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(rueda) > 0.01f)
                    distancia = Mathf.Clamp(distancia * (1f - Mathf.Sign(rueda) * 0.1f), distanciaMin, distanciaMax);
            }

            if (teclado != null)
            {
                if (teclado.digit1Key.wasPressedThisFrame) Vista(0f, profundidad / 2f);
                if (teclado.digit2Key.wasPressedThisFrame) Vista(-90f, ancho / 2f);
                if (teclado.digit3Key.wasPressedThisFrame) Vista(180f, profundidad / 2f);
                if (teclado.digit4Key.wasPressedThisFrame) Vista(90f, ancho / 2f);
                if (teclado.digit0Key.wasPressedThisFrame)
                {
                    objetivo = new Vector3(0f, 1.6f, 0f);
                    distancia = distanciaGeneral;
                    giro = -32f;
                    inclinacion = 14f;
                }
                if (teclado.rKey.wasPressedThisFrame) giroAutomatico = !giroAutomatico;
            }

            if (giroAutomatico) giro += velocidadAutomatica * Time.deltaTime;

            float k = 1f - Mathf.Exp(-suavizado * Time.deltaTime);
            objetivoActual = Vector3.Lerp(objetivoActual, objetivo, k);
            distanciaActual = Mathf.Lerp(distanciaActual, distancia, k);
            giroActual = Mathf.LerpAngle(giroActual, giro, k);
            inclinacionActual = Mathf.Lerp(inclinacionActual, inclinacion, k);
            Aplicar();
        }

        /// <summary>Vista frontal de una pared, como la ve alguien en la fila (a 6 m, ojos a 1,6 m).</summary>
        void Vista(float yaw, float medioLado)
        {
            giroAutomatico = false;
            giro = yaw;
            inclinacion = 4f;
            var haciaAfuera = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
            objetivo = new Vector3(0f, 1.6f, 0f) + haciaAfuera * medioLado;
            distancia = 6.5f;
        }

        void Aplicar()
        {
            var rot = Quaternion.Euler(inclinacionActual, giroActual, 0f);
            transform.position = objetivoActual - rot * Vector3.forward * distanciaActual;
            transform.rotation = rot;
        }
    }
}
