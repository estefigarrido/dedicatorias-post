using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace CuboPost
{
    /// <summary>
    /// Visitante: la persona con la que se recorre la escena en Play.
    ///   W A S D (o las flechas) = caminar · mouse = mirar · Shift = correr
    ///   V = ver al personaje desde atrás / volver a sus ojos
    ///   Tab = pasar a la vista general (la cámara que gira alrededor del stand) y volver
    ///   Esc = soltar el mouse · clic = volver a tomarlo
    /// La cámara va a la altura de los ojos de una persona de 1,75 m (1,63 m sobre el piso).
    /// El personaje y sus medidas los arma el menú post. (ConstruirCubo).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class Caminante : MonoBehaviour
    {
        [Header("Persona")]
        [Tooltip("Altura de los ojos sobre el piso, en metros (1,63 m para alguien de 1,75 m).")]
        public float alturaOjos = 1.63f;

        [Header("Movimiento")]
        [Tooltip("Metros por segundo caminando.")]
        public float velocidad = 1.8f;
        [Tooltip("Metros por segundo con Shift apretado.")]
        public float velocidadCorriendo = 4.5f;
        [Tooltip("Grados que gira la vista por cada píxel que se mueve el mouse.")]
        public float sensibilidad = 0.12f;
        [Tooltip("Al dar Play arranca caminando. Si no, arranca en la vista general y se pasa con Tab.")]
        public bool arrancaCaminando = true;

        [Header("Vista")]
        public Camera camara;
        public CamaraOrbita orbita;
        [Tooltip("Ver al personaje desde atrás en vez de desde sus ojos (tecla V).")]
        public bool terceraPersona;
        public float distanciaTercera = 3.2f;
        [Tooltip("Campo visual de la cámara mientras se camina.")]
        public float campoVisual = 60f;

        [Header("Cuerpo (para el balanceo al caminar)")]
        public Transform cuerpo;
        public Transform piernaIzquierda, piernaDerecha, brazoIzquierdo, brazoDerecho;

        CharacterController control;
        Renderer[] visibles = new Renderer[0];
        Vector3 inicio;
        bool caminando;
        float giro, inclinacion;      // hacia dónde mira (grados)
        float giroCuerpo;             // hacia dónde apunta el cuerpo
        float caida;                  // velocidad vertical (m/s)
        float pisoSuave;              // altura de los pies, suavizada para la cámara
        float fase, amplitud;         // balanceo de piernas y brazos
        float campoOriginal = 50f;
        float ayudaHasta;
        GUIStyle estilo;

        void Start()
        {
            control = GetComponent<CharacterController>();
            if (cuerpo != null) visibles = cuerpo.GetComponentsInChildren<Renderer>(true);
            if (camara == null) camara = Camera.main;
            if (orbita == null && camara != null) orbita = camara.GetComponent<CamaraOrbita>();
            if (camara != null) campoOriginal = camara.fieldOfView;
            inicio = transform.position;
            giro = giroCuerpo = transform.eulerAngles.y;
            pisoSuave = transform.position.y;
            Modo(arrancaCaminando);
        }

        void OnDisable()
        {
            Soltar();
        }

        /// <summary>Caminar (cámara en el visitante) o vista general (cámara que gira alrededor del stand).</summary>
        void Modo(bool caminar)
        {
            caminando = caminar;
            if (orbita != null) orbita.enabled = !caminar;
            if (camara != null) camara.fieldOfView = caminar ? campoVisual : campoOriginal;
            if (caminar) Tomar(); else Soltar();
            ayudaHasta = Time.unscaledTime + 12f;
            MostrarCuerpo();
        }

        void Tomar()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Soltar()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void MostrarCuerpo()
        {
            // Desde los ojos no se ve el propio cuerpo, pero sí su sombra.
            var modo = caminando && !terceraPersona ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            foreach (var r in visibles)
                if (r != null) r.shadowCastingMode = modo;
        }

        void Update()
        {
            var teclado = Keyboard.current;
            var mouse = Mouse.current;

            if (teclado != null)
            {
                if (teclado.tabKey.wasPressedThisFrame) Modo(!caminando);
                if (caminando && teclado.vKey.wasPressedThisFrame)
                {
                    terceraPersona = !terceraPersona;
                    MostrarCuerpo();
                }
                if (caminando && teclado.escapeKey.wasPressedThisFrame) Soltar();
            }

            var direccion = Vector3.zero;
            bool corre = false;
            if (caminando)
            {
                bool tomado = Cursor.lockState == CursorLockMode.Locked;
                if (mouse != null)
                {
                    if (!tomado)
                    {
                        if (mouse.leftButton.wasPressedThisFrame) Tomar();
                    }
                    else
                    {
                        // Con un tope, por si el mouse pega un salto al tomarlo.
                        var d = Vector2.ClampMagnitude(mouse.delta.ReadValue(), 250f);
                        giro += d.x * sensibilidad;
                        inclinacion = Mathf.Clamp(inclinacion - d.y * sensibilidad, -80f, 80f);
                    }
                }
                if (teclado != null)
                {
                    if (teclado.wKey.isPressed || teclado.upArrowKey.isPressed) direccion.z += 1f;
                    if (teclado.sKey.isPressed || teclado.downArrowKey.isPressed) direccion.z -= 1f;
                    if (teclado.dKey.isPressed || teclado.rightArrowKey.isPressed) direccion.x += 1f;
                    if (teclado.aKey.isPressed || teclado.leftArrowKey.isPressed) direccion.x -= 1f;
                    corre = teclado.leftShiftKey.isPressed || teclado.rightShiftKey.isPressed;
                }
            }

            Mover(Vector3.ClampMagnitude(direccion, 1f), corre);
            Balancear();
        }

        void Mover(Vector3 direccion, bool corre)
        {
            // Adelante es hacia donde se mira (sin la inclinación: no se camina hacia el cielo).
            var horizontal = Quaternion.Euler(0f, giro, 0f) * direccion * (corre ? velocidadCorriendo : velocidad);

            // Gravedad. En el piso queda un empuje chico hacia abajo, para no despegarse al bajar un cordón.
            if (control.isGrounded && caida < 0f) caida = -2f;
            caida += Physics.gravity.y * Time.deltaTime;
            control.Move((horizontal + Vector3.up * caida) * Time.deltaTime);

            // Si se cae del mundo, vuelve al punto de partida.
            if (transform.position.y < -30f)
            {
                control.enabled = false;
                transform.position = inicio;
                control.enabled = true;
                caida = 0f;
                pisoSuave = inicio.y;
            }

            // Desde los ojos, el cuerpo mira hacia donde mira la cámara; desde atrás, hacia donde camina.
            if (caminando)
            {
                if (!terceraPersona) giroCuerpo = giro;
                else if (horizontal.sqrMagnitude > 0.01f)
                    giroCuerpo = Mathf.MoveTowardsAngle(giroCuerpo, Mathf.Atan2(horizontal.x, horizontal.z) * Mathf.Rad2Deg, 540f * Time.deltaTime);
            }
            transform.rotation = Quaternion.Euler(0f, giroCuerpo, 0f);
        }

        /// <summary>Piernas y brazos van y vienen según lo que avanza de verdad (si choca con algo, se queda quieto).</summary>
        void Balancear()
        {
            var v = control.velocity;
            v.y = 0f;
            float rapidez = v.magnitude;
            fase += rapidez * Time.deltaTime * (Mathf.PI * 2f / 1.5f);   // un ciclo cada dos pasos (1,50 m)
            float objetivo = rapidez > 0.15f ? Mathf.Lerp(20f, 36f, Mathf.InverseLerp(1f, 4.5f, rapidez)) : 0f;
            amplitud = Mathf.MoveTowards(amplitud, objetivo, 140f * Time.deltaTime);
            float a = Mathf.Sin(fase) * amplitud;
            if (piernaIzquierda != null) piernaIzquierda.localRotation = Quaternion.Euler(a, 0f, 0f);
            if (piernaDerecha != null) piernaDerecha.localRotation = Quaternion.Euler(-a, 0f, 0f);
            if (brazoIzquierdo != null) brazoIzquierdo.localRotation = Quaternion.Euler(-a * 0.8f, 0f, 0f);
            if (brazoDerecho != null) brazoDerecho.localRotation = Quaternion.Euler(a * 0.8f, 0f, 0f);
        }

        void LateUpdate()
        {
            if (!caminando || camara == null) return;

            // La altura de los pies se sigue con suavidad, así subir un cordón o el murete no es un salto.
            float y = transform.position.y;
            pisoSuave = Mathf.Abs(y - pisoSuave) > 1.5f ? y : Mathf.Lerp(pisoSuave, y, 1f - Mathf.Exp(-14f * Time.deltaTime));
            var ojos = new Vector3(transform.position.x, pisoSuave + alturaOjos, transform.position.z);
            var mirada = Quaternion.Euler(inclinacion, giro, 0f);

            if (!terceraPersona)
            {
                camara.transform.SetPositionAndRotation(ojos, mirada);
                return;
            }

            // Desde atrás: la cámara se aleja de la cabeza hasta donde no haya nada en el medio.
            var centro = ojos + Vector3.up * 0.12f;
            var haciaAtras = mirada * Vector3.back;
            float distancia = distanciaTercera;
            if (Physics.SphereCast(centro, 0.2f, haciaAtras, out RaycastHit choque, distanciaTercera, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                distancia = Mathf.Max(0.3f, choque.distance);
            camara.transform.SetPositionAndRotation(centro + haciaAtras * distancia, mirada);
        }

        void OnGUI()
        {
            bool tomado = Cursor.lockState == CursorLockMode.Locked;
            if (Time.unscaledTime > ayudaHasta && (tomado || !caminando)) return;
            string texto = !caminando ? "Tab: recorrer caminando"
                : tomado ? "W A S D caminar · mouse mirar · Shift correr · V ver al personaje · Tab vista general · Esc soltar el mouse"
                : "Clic para mirar con el mouse · W A S D caminar · Tab vista general";
            if (estilo == null) estilo = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            var lugar = new Rect(18f, Screen.height - 44f, Screen.width - 36f, 30f);
            estilo.normal.textColor = new Color(0f, 0f, 0f, 0.65f);
            GUI.Label(new Rect(lugar.x + 1f, lugar.y + 1f, lugar.width, lugar.height), texto, estilo);
            estilo.normal.textColor = Color.white;
            GUI.Label(lugar, texto, estilo);
        }
    }
}
