using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CuboPost.EditorTools
{
    /// <summary>
    /// Arma el stand a escala real (metros) según los planos con cotas del Figma ("prueba mapa",
    /// sección "post. · planos con cotas": planta general, acceso/ropero/salida, corte e implantación):
    ///   planta 21,50 × 7,42 m · altura total 4 m (3,50 m de pared + 0,50 m de coronamiento)
    ///   · sala de 20 × 7,42 m y, pegada al lado este, la franja de servicios de 1,50 m:
    ///     de norte a sur, técnico + depósito 1,72 · entrada 1,94 · ropero 1,77 · salida 1,99
    ///   · pantallas LED exteriores en todo el perímetro menos en los vanos de entrada y salida:
    ///     frente (sur) y fondo (norte) de 21,50 m, lateral oeste de 7,42 m y, al este, un tramo
    ///     sobre el técnico y otro sobre el ropero; fondo #252525 con los puntos POST dispersos
    ///   · gráfica post. centrada en las dos paredes largas (frente y fondo)
    ///   · piso relevado de 32 × 15 m, con el stand ubicado como en la implantación
    ///   · Plaza de la República con el Obelisco de fondo.
    /// Por ahora es solo el exterior: los vanos de entrada y salida tienen un cierre oscuro al fondo
    /// hasta que se arme el interior (sala, mats, cortina y pantallas interiores).
    ///
    /// Ejes: +X = este, +Z = norte. El stand queda centrado en el origen.
    ///
    /// Menú post. → Construir escena del cubo: rearma TODA la escena desde cero (plaza incluida).
    /// Menú post. → Actualizar solo el cubo: cambia el stand, los accesos y la fila en la escena
    ///   abierta y deja la plaza como está. Esto último también corre solo cuando Unity recompila
    ///   y encuentra que el cubo de la escena no coincide con las medidas de este archivo.
    /// </summary>
    public static class ConstruirCubo
    {
        // Medidas (metros), tomadas de los planos con cotas del Figma.
        const float LargoSala = 20f;       // sala, de oeste a este
        const float Franja = 1.5f;         // franja de servicios, pegada al lado este
        const float Ancho = LargoSala + Franja;             // 21,50 m: frente (sur) y fondo (norte)
        const float Profundidad = 7.42f;   // laterales: dos mitades de 3,71 m
        const float AltoPared = 3.5f;      // pared con pantalla
        const float Coronamiento = 0.5f;   // franja oscura de arriba
        const float AltoTotal = AltoPared + Coronamiento;   // 4 m
        const float Zocalo = 0.2f;         // la pantalla arranca a 20 cm del piso
        const float AltoPantalla = AltoPared - Zocalo;      // 3,3 m de LED
        // Franja de servicios, de norte a sur. Los cuatro tramos suman 7,42 m.
        const float Tecnico = 1.72f, Entrada = 1.94f, Ropero = 1.77f, Salida = 1.99f;
        // Alto libre de los vanos de entrada y salida. No figura en los planos: es el de la entrada anterior.
        const float AltoAcceso = 2.8f;
        // Ropero: una puerta de 0,50 m hacia la entrada y otra hacia la salida.
        const float PuertaRopero = 0.5f, AltoPuertaRopero = 2.05f;
        // Vista general de la cámara.
        const float DistanciaCamara = 32f;

        // Piso rojo = superficie relevada de 32 × 15 m (implantación). El stand queda a 3,43 m del
        // borde oeste, 7,07 m del este, 1,00 m del sur y 6,58 m del norte; por eso no está centrado.
        const float PisoLargo = 32f, PisoAncho = 15f, PisoMargenOeste = 3.43f, PisoMargenSur = 1f;
        static readonly Vector3 PlazaRojaCentro = new Vector3(
            -Ancho / 2f - PisoMargenOeste + PisoLargo / 2f, -0.045f,
            -Profundidad / 2f - PisoMargenSur + PisoAncho / 2f);
        static readonly Vector3 PlazaRojaTamano = new Vector3(PisoLargo, 0.1f, PisoAncho);

        const string Carpeta = "Assets/CuboPost/Generado";
        const string RutaEscena = "Assets/Scenes/CuboPost.unity";
        const string NombreCubo = "Cubo post.";

        /// <summary>
        /// Resumen de las medidas. Queda guardado en la escena (un objeto vacío dentro del cubo) y
        /// sirve para saber si el cubo de la escena está al día con este archivo.
        /// Si se cambia la disposición sin tocar ninguna constante, subir el número del final.
        /// r2: gráfica post. también en el frente y fondo de pantallas #252525.
        /// r3: las dedicatorias salen solo en la pantalla del frente.
        /// r4: stand de 21,50 × 7,42 m con la franja de servicios al este (solo exterior).
        /// </summary>
        static string Firma => string.Format(CultureInfo.InvariantCulture,
            "Medidas {0}x{1}x{2} pantalla {3} franja este {4} tramos {5}-{6}-{7}-{8} accesos {9} r4",
            Ancho, Profundidad, AltoTotal, AltoPantalla, Franja, Tecnico, Entrada, Ropero, Salida, AltoAcceso);

        // true = usar los materiales que ya existen (para no romper lo que sigue en la escena).
        static bool reusarMateriales;

        [MenuItem("post./Construir escena del cubo")]
        public static void Construir()
        {
            if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/CuboPost", "Generado");
            reusarMateriales = false;

            var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------- contexto ----------
            var matPlazaRoja = Mat("Plaza roja", "#b56a64", 0.08f);
            var matObelisco = Mat("Obelisco", "#ebe7de", 0.15f);
            Mat("Plaza", "#8d8a85", 0.08f);

            var contexto = new GameObject("Contexto (plaza)").transform;
            Caja("Plaza roja", contexto, PlazaRojaCentro, PlazaRojaTamano, matPlazaRoja);
            var obelisco = new GameObject("Obelisco", typeof(MeshFilter), typeof(MeshRenderer));
            obelisco.transform.SetParent(contexto, false);
            // En su isla, cruzando Av. Corrientes hacia el sur (según el mapa).
            obelisco.transform.position = new Vector3(4f, 0f, -38f);
            obelisco.GetComponent<MeshFilter>().sharedMesh = MallaObelisco();
            obelisco.GetComponent<MeshRenderer>().sharedMaterial = matObelisco;
            // Calles, adoquines, semáforos, faroles, letras BA, edificios y autos.
            ConstruirEntorno.Construir(contexto);

            // ---------- cubo ----------
            var paredes = ArmarCubo(null);

            // Que el modo Play siga animando aunque Unity no esté en primer plano.
            PlayerSettings.runInBackground = true;

            // ---------- sistema ----------
            var sistema = new GameObject("Sistema");
            var fuente = sistema.AddComponent<SupabaseNotas>();
            var controlador = sistema.AddComponent<ControladorCubo>();
            controlador.fuente = fuente;
            controlador.paredes = paredes.ToArray();

            // ---------- luz y cámara ----------
            var sol = new GameObject("Sol").AddComponent<Light>();
            sol.type = LightType.Directional;
            sol.intensity = 1.15f;
            sol.color = new Color(1f, 0.97f, 0.92f);
            sol.shadows = LightShadows.Soft;
            sol.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.82f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.62f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.33f, 0.31f);

            var camGo = new GameObject("Cámara");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 800f;
            camGo.AddComponent<AudioListener>();
            var orbita = camGo.AddComponent<CamaraOrbita>();
            orbita.distancia = DistanciaCamara;
            AjustarCamara(orbita);

            EditorSceneManager.SaveScene(escena, RutaEscena);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(RutaEscena, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[post.] Escena del cubo construida: " + RutaEscena);
        }

        /// <summary>
        /// Cambia solo el stand (estructura, pantallas, accesos y fila) en la escena abierta.
        /// La plaza, el sol y la cámara quedan como están; se conservan los ajustes de las pantallas.
        /// </summary>
        [MenuItem("post./Actualizar solo el cubo (medidas nuevas)")]
        public static void ActualizarCubo()
        {
            var escena = EditorSceneManager.GetActiveScene();
            if (escena.path != RutaEscena)
            {
                Debug.LogWarning("[post.] Para actualizar el cubo, abrí primero la escena " + RutaEscena);
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[post.] Salí del modo Play antes de actualizar el cubo.");
                return;
            }
            if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/CuboPost", "Generado");
            reusarMateriales = true;

            // Cubo anterior: se guardan los ajustes de sus pantallas antes de reemplazarlo.
            GameObject viejo = null;
            foreach (var raiz in escena.GetRootGameObjects())
                if (raiz.name == NombreCubo) viejo = raiz;
            var previas = new Dictionary<string, string>();
            if (viejo != null)
                foreach (var p in viejo.GetComponentsInChildren<ParedPantalla>(true))
                    previas[p.name] = JsonUtility.ToJson(p);

            var paredes = ArmarCubo(previas);
            if (viejo != null) Object.DestroyImmediate(viejo);

            // Lo que sigue en la escena y depende de las medidas.
            ControladorCubo controlador = null;
            CamaraOrbita orbita = null;
            Transform plazaRoja = null;
            foreach (var raiz in escena.GetRootGameObjects())
            {
                if (controlador == null) controlador = raiz.GetComponentInChildren<ControladorCubo>(true);
                if (orbita == null) orbita = raiz.GetComponentInChildren<CamaraOrbita>(true);
                if (plazaRoja == null && raiz.name == "Contexto (plaza)") plazaRoja = raiz.transform.Find("Plaza roja");
            }

            if (controlador == null)
            {
                var sistema = new GameObject("Sistema");
                var fuente = sistema.AddComponent<SupabaseNotas>();
                controlador = sistema.AddComponent<ControladorCubo>();
                controlador.fuente = fuente;
            }
            controlador.paredes = paredes.ToArray();
            EditorUtility.SetDirty(controlador);

            if (plazaRoja != null)
            {
                plazaRoja.position = PlazaRojaCentro;
                plazaRoja.localScale = PlazaRojaTamano;
            }

            if (orbita != null)
            {
                // Solo se aleja la vista general si seguía en el valor de fábrica (24 m).
                if (Mathf.Abs(orbita.distancia - 24f) < 0.01f) orbita.distancia = DistanciaCamara;
                AjustarCamara(orbita);
                EditorUtility.SetDirty(orbita);
            }

            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            AssetDatabase.SaveAssets();
            Debug.Log("[post.] Cubo actualizado: " + Firma);
        }

        /// <summary>Corre solo al recompilar: si el cubo de la escena abierta quedó viejo, lo actualiza.</summary>
        internal static void ActualizarSiHaceFalta()
        {
            if (Application.isBatchMode) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var escena = EditorSceneManager.GetActiveScene();
            if (escena.path != RutaEscena || CuboAlDia(escena)) return;
            Debug.Log("[post.] El cubo de la escena tiene medidas viejas: se actualiza solo.");
            ActualizarCubo();
        }

        /// <summary>¿El cubo de la escena ya tiene las medidas de este archivo? Sin cubo no se toca nada.</summary>
        static bool CuboAlDia(Scene escena)
        {
            foreach (var raiz in escena.GetRootGameObjects())
            {
                if (raiz.name != NombreCubo) continue;
                foreach (Transform hijo in raiz.transform)
                    if (hijo.name == Firma) return true;
                return false;
            }
            return true;
        }

        // ---------------- el stand ----------------

        /// <summary>
        /// Crea "Cubo post." con estructura, pantallas, franja de servicios y fila, y devuelve las
        /// pantallas en orden, recorriendo el perímetro (frente, derecha · ropero, derecha · técnico,
        /// fondo, izquierda). <paramref name="previas"/>: ajustes de las pantallas anteriores (por
        /// nombre) para conservarlos; null si se arma de cero.
        /// </summary>
        static List<ParedPantalla> ArmarCubo(Dictionary<string, string> previas)
        {
            var matGradiente = MatGradiente();
            var matEstructura = Mat("Estructura", "#252525", 0.35f);
            var matTunel = Mat("Tunel", "#2f2f2f", 0.3f);
            var matPuerta = Mat("Puerta", "#141414", 0.7f);
            var matPersonas = Mat("Personas", "#ede8db", 0.2f);

            // Bordes del stand y de la franja de servicios (el stand está centrado en el origen).
            float este = Ancho / 2f, oeste = -Ancho / 2f, norte = Profundidad / 2f, sur = -Profundidad / 2f;
            float xFranja = este - Franja;            // 9,25: donde termina la sala y empieza la franja
            float zTecnico = norte - Tecnico;         // técnico + depósito: de zTecnico a norte
            float zEntrada = zTecnico - Entrada;      // entrada: de zEntrada a zTecnico
            float zRopero = zEntrada - Ropero;        // ropero: de zRopero a zEntrada; salida: de sur a zRopero
            const float h = 0.02f;                    // holgura para no coincidir con el plano de las pantallas
            const float vuelo = 0.03f;                // cuánto sobresale el zócalo

            var cubo = new GameObject(NombreCubo).transform;
            new GameObject(Firma).transform.SetParent(cubo, false);   // marca de medidas (objeto vacío)

            var estructura = new GameObject("Estructura").transform;
            estructura.SetParent(cubo, false);
            // Zócalo: corre por todo el perímetro menos en los vanos de entrada y salida.
            CajaEntre("Zócalo sala", estructura, oeste - vuelo, xFranja, 0f, Zocalo, sur - vuelo, norte + vuelo, matEstructura);
            CajaEntre("Zócalo técnico", estructura, xFranja, este + vuelo, 0f, Zocalo, zTecnico + 0.01f, norte + vuelo, matEstructura);
            CajaEntre("Zócalo ropero", estructura, xFranja, este + vuelo, 0f, Zocalo, zRopero + 0.01f, zEntrada - 0.01f, matEstructura);
            CajaEntre("Zócalo salida (lado sur)", estructura, xFranja, este + vuelo, 0f, Zocalo, sur - vuelo, sur + 0.09f, matEstructura);
            // Coronamiento: franja oscura de 0,50 m sobre las pantallas (de 3,50 a 4 m). Hace de techo.
            Caja("Coronamiento", estructura, new Vector3(0, AltoPared + Coronamiento / 2f, 0), new Vector3(Ancho + 0.16f, Coronamiento, Profundidad + 0.16f), matEstructura);
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Caja("Esquinero", estructura, new Vector3(sx * Ancho / 2f, AltoPared / 2f, sz * Profundidad / 2f), new Vector3(0.12f, AltoPared, 0.12f), matEstructura);
            // Parantes del lado este, entre cada tramo de pantalla y el vano de al lado.
            foreach (var z in new[] { zTecnico, zEntrada, zRopero })
                Caja("Parante", estructura, new Vector3(este, AltoPared / 2f, z), new Vector3(0.12f, AltoPared, 0.12f), matEstructura);

            // Pantallas: recorren el perímetro en orden (frente → derecha → fondo → izquierda) para que
            // el degradé pase de una a otra sin cortes. El lado este tiene dos tramos (ropero y técnico):
            // entre ellos y hasta la esquina sur quedan los vanos de entrada y salida, sin pantalla.
            var pantallas = new GameObject("Pantallas LED").transform;
            pantallas.SetParent(cubo, false);
            float perimetro = 2f * (Ancho + Profundidad);
            float yCentro = Zocalo + AltoPantalla / 2f;
            var paredes = new List<ParedPantalla>
            {
                Pared("Pantalla frente", pantallas, new Vector3(0, yCentro, sur), Vector3.back, Ancho, 0f, perimetro, matGradiente, previas),
                Pared("Pantalla derecha (ropero)", pantallas, new Vector3(este, yCentro, zRopero + Ropero / 2f), Vector3.right, Ropero, Ancho + Salida, perimetro, matGradiente, previas, "Pantalla derecha"),
                Pared("Pantalla derecha (técnico)", pantallas, new Vector3(este, yCentro, zTecnico + Tecnico / 2f), Vector3.right, Tecnico, Ancho + Salida + Ropero + Entrada, perimetro, matGradiente, previas, "Pantalla derecha"),
                Pared("Pantalla fondo", pantallas, new Vector3(0, yCentro, norte), Vector3.forward, Ancho, Ancho + Profundidad, perimetro, matGradiente, previas),
                Pared("Pantalla izquierda", pantallas, new Vector3(oeste, yCentro, 0), Vector3.left, Profundidad, 2f * Ancho + Profundidad, perimetro, matGradiente, previas),
            };

            // Paredes largas (frente y fondo): gráfica post. centrada, como en el frame del Figma
            // (1622:4941). El alto del frame es el alto de la pantalla, y la elipse roja del frame
            // (todo el alto, 1286 px de ancho por cada 637 de alto) es donde no pueden ir notas ni
            // puntos sueltos, así nada tapa el logo; las notas van a los costados.
            float anchoElipse = AltoPantalla * ParedPantalla.FigmaAnchoElipse / ParedPantalla.FigmaAltoFrame;
            foreach (var larga in new[] { paredes[0], paredes[3] })
            {
                larga.composicionCentral = true;
                larga.zonasElipse.Add(new Rect((Ancho - anchoElipse) / 2f, 0f, anchoElipse, AltoPantalla));
            }

            // Por ahora las dedicatorias salen solo en la pantalla del frente (la que ve la cámara al
            // abrir). Las demás muestran gráfica y puntos. Para sumar otra, poner true acá.
            for (int i = 0; i < paredes.Count; i++) paredes[i].recibeNotas = i == 0;

            // Vista previa sin Play, ya con las medidas y las zonas definitivas.
            foreach (var p in paredes) p.VistaPrevia();

            // Franja de servicios (lado este, 1,50 m de fondo). De norte a sur: técnico + depósito,
            // entrada, ropero y salida. Desde afuera se ven dos volúmenes cerrados con pantalla
            // (técnico y ropero) y dos vanos (entrada y salida).
            var franja = new GameObject("Franja de servicios (este)").transform;
            franja.SetParent(cubo, false);
            CajaEntre("Técnico + depósito", franja, xFranja, este - h, 0f, AltoPared, zTecnico, norte - h, matTunel);
            CajaEntre("Ropero", franja, xFranja, este - h, 0f, AltoPared, zRopero, zEntrada, matPuerta);
            // Puertas del ropero, centradas en el fondo de la franja: una da a la entrada y otra a la salida.
            float xPuerta = (xFranja + este) / 2f;
            CajaEntre("Ropero · puerta a la entrada", franja, xPuerta - PuertaRopero / 2f, xPuerta + PuertaRopero / 2f, 0f, AltoPuertaRopero, zEntrada, zEntrada + 0.02f, matPersonas);
            CajaEntre("Ropero · puerta a la salida", franja, xPuerta - PuertaRopero / 2f, xPuerta + PuertaRopero / 2f, 0f, AltoPuertaRopero, zRopero - 0.02f, zRopero, matPersonas);
            Acceso("Entrada", franja, xFranja, este, zEntrada, zTecnico, matEstructura, matPuerta);
            Acceso("Salida", franja, xFranja, este, sur + h, zRopero, matEstructura, matPuerta);
            // La salida da contra la pared sur del stand: su cara de adentro (afuera es pantalla).
            CajaEntre("Salida · pared sur", franja, xFranja, este - h, 0f, AltoAcceso, sur + h, sur + 0.1f, matTunel);

            // Gente haciendo la fila (referencia de escala: 1,70 m). Sale derecho desde la entrada
            // hacia el este, donde está la zona de espera.
            var fila = new GameObject("Fila (referencia 1,70 m)").transform;
            fila.SetParent(cubo, false);
            float zFila = (zEntrada + zTecnico) / 2f;   // centro del vano de entrada
            for (int i = 0; i < 7; i++)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                p.name = "Persona";
                p.transform.SetParent(fila, false);
                p.transform.localScale = new Vector3(0.45f, 0.85f, 0.45f);
                p.transform.position = new Vector3(este + 0.9f + i * 0.85f, 0.85f, zFila - 0.06f + (i % 2) * 0.12f);
                p.GetComponent<MeshRenderer>().sharedMaterial = matPersonas;
            }

            return paredes;
        }

        /// <summary>
        /// Vano de entrada o de salida: del ancho de su tramo (de z0 a z1), con el fondo de la franja
        /// (de x0 a x1) y <see cref="AltoAcceso"/> de alto libre. Arriba, el dintel hasta la altura de
        /// las pantallas. Al fondo, un cierre oscuro provisorio: se saca cuando se arme el interior.
        /// </summary>
        static void Acceso(string nombre, Transform padre, float x0, float x1, float z0, float z1, Material matEstructura, Material matCierre)
        {
            var acceso = new GameObject(nombre).transform;
            acceso.SetParent(padre, false);
            CajaEntre("Dintel", acceso, x0, x1, AltoAcceso, AltoPared, z0, z1, matEstructura);
            CajaEntre("Piso", acceso, x0, x1, 0f, 0.02f, z0, z1, matEstructura);
            CajaEntre("Cierre provisorio (interior pendiente)", acceso, x0 - 0.04f, x0 + 0.01f, 0f, AltoAcceso, z0, z1, matCierre);
        }

        static void AjustarCamara(CamaraOrbita orbita)
        {
            orbita.ancho = Ancho;
            orbita.profundidad = Profundidad;
            var rot = Quaternion.Euler(orbita.inclinacion, orbita.giro, 0f);
            orbita.transform.SetPositionAndRotation(orbita.objetivo - rot * Vector3.forward * orbita.distancia, rot);
        }

        // ---------------- ayudas ----------------

        /// <summary>
        /// Una pantalla LED exterior. <paramref name="heredaDe"/>: si no había una pantalla con este
        /// nombre, de cuál de las anteriores toma los ajustes (para los tramos nuevos del lado este).
        /// </summary>
        static ParedPantalla Pared(string nombre, Transform padre, Vector3 centro, Vector3 haciaAfuera,
            float largo, float inicioPerimetro, float perimetro, Material mat, Dictionary<string, string> previas,
            string heredaDe = null)
        {
            var go = new GameObject(nombre, typeof(MeshFilter), typeof(MeshRenderer), typeof(ParedPantalla));
            go.transform.SetParent(padre, false);
            // +Z del objeto = hacia adentro del cubo (la dirección en la que mira el público).
            go.transform.SetPositionAndRotation(centro, Quaternion.LookRotation(-haciaAfuera, Vector3.up));

            float u0 = inicioPerimetro / perimetro, u1 = (inicioPerimetro + largo) / perimetro;
            float x = largo / 2f, y = AltoPantalla / 2f;
            var malla = new Mesh { name = nombre };
            malla.vertices = new[] { new Vector3(-x, -y, 0), new Vector3(x, -y, 0), new Vector3(-x, y, 0), new Vector3(x, y, 0) };
            malla.uv = new[] { new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u0, 1), new Vector2(u1, 1) };
            malla.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            malla.triangles = new[] { 0, 2, 3, 0, 3, 1 };
            malla.RecalculateBounds();
            go.GetComponent<MeshFilter>().sharedMesh = Guardar(malla, nombre + ".asset");

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var pared = go.GetComponent<ParedPantalla>();
            // Se conservan los ajustes de la pantalla anterior (tiempos, márgenes, decoración)...
            string ajustes = null;
            if (previas != null && !previas.TryGetValue(nombre, out ajustes) && heredaDe != null)
                previas.TryGetValue(heredaDe, out ajustes);
            if (ajustes != null) JsonUtility.FromJsonOverwrite(ajustes, pared);
            // ...pero las medidas y las zonas tapadas siempre salen de este archivo.
            pared.largo = largo;
            pared.alto = AltoPantalla;
            pared.zonasBloqueadas = new List<Rect>();
            pared.zonasElipse = new List<Rect>();
            pared.composicionCentral = false;
            return pared;
        }

        static GameObject Caja(string nombre, Transform padre, Vector3 centro, Vector3 tamano, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nombre;
            go.transform.SetParent(padre, false);
            go.transform.position = centro;
            go.transform.localScale = tamano;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>Caja definida por sus bordes: de x0 a x1, de y0 a y1 y de z0 a z1 (metros).</summary>
        static GameObject CajaEntre(string nombre, Transform padre, float x0, float x1, float y0, float y1, float z0, float z1, Material mat)
        {
            return Caja(nombre, padre,
                new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, (z0 + z1) / 2f),
                new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0)), mat);
        }

        /// <summary>Material liso. Al actualizar solo el cubo se usa el que ya existe, si existe.</summary>
        static Material Mat(string nombre, string hex, float suavidad)
        {
            if (reusarMateriales)
            {
                var existente = AssetDatabase.LoadAssetAtPath<Material>($"{Carpeta}/{nombre}.mat");
                if (existente != null) return existente;
            }
            return Lit(nombre, hex, suavidad);
        }

        static Material MatGradiente()
        {
            Material m = null;
            if (reusarMateriales) m = AssetDatabase.LoadAssetAtPath<Material>($"{Carpeta}/Pantallas LED.mat");
            if (m == null) m = Guardar(new Material(Shader.Find("CuboPost/GradienteParedes")) { name = "Pantallas LED" }, "Pantallas LED.mat");
            // El negro de las pantallas es el de la marca (#252525), igual que en el Figma.
            m.SetColor("_Base", PaletaPost.Oscuro);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Lit(string nombre, string hex, float suavidad)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = nombre };
            ColorUtility.TryParseHtmlString(hex, out var c);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", suavidad);
            return Guardar(m, nombre + ".mat");
        }

        static T Guardar<T>(T asset, string archivo) where T : Object
        {
            var ruta = $"{Carpeta}/{archivo}";
            if (AssetDatabase.LoadAssetAtPath<Object>(ruta) != null) AssetDatabase.DeleteAsset(ruta);
            AssetDatabase.CreateAsset(asset, ruta);
            return asset;
        }

        /// <summary>Obelisco de Buenos Aires simplificado: 67,5 m, base de 6,8 m, punta piramidal.</summary>
        static Mesh MallaObelisco()
        {
            const float baseLado = 6.8f, topeLado = 3.5f, alturaFuste = 63.5f, alturaTotal = 67.5f;
            float b = baseLado / 2f, t = topeLado / 2f;
            var abajo = new[] { new Vector3(-b, 0, -b), new Vector3(b, 0, -b), new Vector3(b, 0, b), new Vector3(-b, 0, b) };
            var arriba = new[] { new Vector3(-t, alturaFuste, -t), new Vector3(t, alturaFuste, -t), new Vector3(t, alturaFuste, t), new Vector3(-t, alturaFuste, t) };
            var punta = new Vector3(0, alturaTotal, 0);

            var v = new List<Vector3>();
            var tri = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                int k = v.Count;
                v.AddRange(new[] { abajo[i], arriba[i], arriba[j], abajo[j] });
                tri.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
                k = v.Count;
                v.AddRange(new[] { arriba[i], punta, arriba[j] });
                tri.AddRange(new[] { k, k + 1, k + 2 });
            }
            var m = new Mesh { name = "Obelisco" };
            m.SetVertices(v);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return Guardar(m, "Obelisco.asset");
        }
    }

    /// <summary>
    /// Cada vez que Unity recompila los scripts, revisa si el cubo de la escena abierta tiene las
    /// medidas de <see cref="ConstruirCubo"/> y, si no, lo actualiza sin tocar la plaza.
    /// </summary>
    [InitializeOnLoad]
    static class MedidasAlDia
    {
        static MedidasAlDia()
        {
            EditorApplication.delayCall += ConstruirCubo.ActualizarSiHaceFalta;
        }
    }
}
