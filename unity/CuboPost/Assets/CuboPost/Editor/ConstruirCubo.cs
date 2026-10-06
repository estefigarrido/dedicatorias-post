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
    /// sección "post. · planos con cotas") y el corte transversal nuevo que pasó Estefi:
    ///   planta 21,50 × 12,42 m · altura total 6 m (5,50 m de pared + 0,50 m de coronamiento)
    ///   · sala de 20 × 12,42 m en dos mitades de 6,21 m (de la pantalla al mat 3,00 · mat 0,71 ·
    ///     del mat a la cortina 2,50) y, pegada al lado este, la franja de servicios de 1,50 m:
    ///     de norte a sur, técnico + depósito 2,88 · entrada 3,25 · ropero 2,96 · salida 3,33
    ///   · pantallas LED exteriores en todo el perímetro: frente (sur) y fondo (norte) de 21,50 m
    ///     y los dos laterales de 12,42 m. La del lado este (el de la entrada) es una sola pantalla
    ///     corrida, con dos puertas de 0,90 × 2,10 m: la de entrada y la de salida, cada una
    ///     centrada en su tramo de la franja; fondo #252525 con los puntos POST dispersos
    ///   · gráfica post. centrada en las dos paredes largas (frente y fondo)
    ///   · Plaza de la República con el Obelisco de fondo (la arma <see cref="ConstruirEntorno"/>).
    ///     El stand apoya directo sobre los adoquines de la explanada (ya no hay piso rojo).
    /// Por ahora es solo el exterior: detrás de las puertas de entrada y salida hay un cierre oscuro
    /// al fondo hasta que se arme el interior (sala, mats, cortina y pantallas interiores).
    ///
    /// La escena se recorre caminando: en Play, el "Visitante (WASD)" es una persona de 1,75 m con
    /// la cámara a la altura de sus ojos (ver <see cref="Caminante"/>). La fila de la entrada son
    /// figuras iguales a las de la plaza.
    ///
    /// Ejes: +X = este, +Z = norte. El stand queda centrado en el origen.
    ///
    /// Menú post. → Construir escena del cubo: rearma TODA la escena desde cero (plaza incluida).
    /// Menú post. → Actualizar solo el cubo: cambia el stand, los accesos y la fila en la escena
    ///   abierta y deja la plaza como está.
    /// Menú post. → Actualizar solo la plaza: rearma la plaza y deja el stand como está.
    /// Las dos actualizaciones también corren solas cuando Unity recompila y encuentra que el cubo
    /// o la plaza de la escena no coinciden con lo que dicen estos archivos.
    /// </summary>
    public static class ConstruirCubo
    {
        // Medidas (metros), tomadas de los planos con cotas del Figma.
        const float LargoSala = 20f;       // sala, de oeste a este
        const float Franja = 1.5f;         // franja de servicios, pegada al lado este
        const float Ancho = LargoSala + Franja;             // 21,50 m: frente (sur) y fondo (norte)
        // Laterales: dos mitades de 6,21 m. Cada mitad, según el corte transversal: 3,00 m de la
        // pantalla al mat, 0,71 m de mat y 2,50 m del mat a la cortina (antes 2,00 · 0,71 · 1,00 = 7,42 m).
        const float Profundidad = 12.42f;
        const float AltoPared = 4.5f;      // pared con pantalla (fue 3,50 y 5,50 m; Estefi pidió 1 m menos: stand de 5 m)
        const float Coronamiento = 0.5f;   // franja oscura de arriba
        const float AltoTotal = AltoPared + Coronamiento;   // 6 m
        const float Zocalo = 0.2f;         // la pantalla arranca a 20 cm del piso
        const float AltoPantalla = AltoPared - Zocalo;      // 5,3 m de LED
        // Franja de servicios, de norte a sur. Los cuatro tramos suman 12,42 m. Los planos todavía
        // no tienen la franja para este fondo: son los tramos de antes (1,72 · 1,94 · 1,77 · 1,99)
        // agrandados en la misma proporción que el stand. Si se definen otros, se cambian acá.
        const float Tecnico = 2.88f, Entrada = 3.25f, Ropero = 2.96f, Salida = 3.33f;
        // Puertas de entrada y de salida: 0,90 m de ancho (lo pidió Estefi). El alto no figura en
        // los planos: 2,10 m, el de una puerta común. Alrededor, un marco oscuro de 5 cm.
        const float AnchoPuerta = 0.9f, AltoPuerta = 2.1f, Marco = 0.05f;
        // Alto libre de los pasillos de entrada y salida, detrás de la pantalla.
        const float AltoAcceso = 2.8f;
        // Ropero: una puerta de 0,50 m hacia la entrada y otra hacia la salida.
        const float PuertaRopero = 0.5f, AltoPuertaRopero = 2.05f;
        // Vista general de la cámara.
        const float DistanciaCamara = 32f;
        // Ancho de la nota más chica (S) en las pantallas; las demás crecen en la misma proporción.
        // Eran 0,70 m: con las pantallas más altas, Estefi pidió todas las notas un 20 % más grandes.
        const float AnchoNotaS = 0.70f * 1.2f;
        // Ejercicio de respiración (Figma, página "tareas", tarea 5): la carita mide lo que el círculo
        // violeta (Ø 1,47 m) y los anillos crecen en proporción. Centro de la carita, en metros desde
        // el extremo sur de la pantalla del lado este y desde su borde de abajo.
        const float CaraRespiracion = 1.47f, CentroRespiracionX = 10.50f, CentroRespiracionY = 1.85f;
        // Pantalla de espera (tarea 6): la zona verde, entre la puerta de salida y la de entrada.
        static readonly Rect ZonaEspera = new Rect(2.67f, 0.08f, 4.44f, 4.13f);

        // Visitante: la persona con la que se recorre la escena en Play (W A S D). Mide 1,75 m y
        // tiene los ojos unos 12 cm por debajo de la coronilla, como una persona de esa altura.
        const float AlturaVisitante = 1.75f, OjosVisitante = 1.63f;
        const string NombreVisitante = "Visitante (WASD)";
        // Arranca en la explanada, frente a la esquina sudeste del stand: desde ahí se ven la
        // pantalla del frente y el lado de la entrada, con la fila.
        static readonly Vector3 InicioVisitante = new Vector3(15.5f, 0f, -10.5f);
        const float GiroVisitante = -48f;
        // Gente de la fila: una sola malla de esta altura, que cada figura escala a la suya.
        const float AlturaFigura = 1.7f;
        const int CapaSinRayos = 2;   // capa "Ignore Raycast" de Unity

        // Las escenas anteriores tenían un piso rojo (el área relevada de 32 × 15 m). Ya no va: si
        // la escena lo tiene, se saca al actualizar el cubo.
        const string NombrePisoRojo = "Plaza roja";
        const string NombreContexto = "Contexto (plaza)";

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
        /// r5: fondo de 12,42 m (3,00 + 0,71 + 2,50 por mitad), franja en proporción y sin piso rojo.
        /// r6: 2 m más de altura (pantalla hasta 5,50 m, 6 m en total).
        /// r7: el lado este es una sola pantalla corrida con dos puertas de 0,90 m (entrada y salida).
        /// r8: visitante de 1,75 m para recorrer la escena con W A S D, fila con figuras y colisiones.
        /// r9: ejercicio de respiración junto a la entrada (tecla E) y notas un 20 % más grandes.
        /// r10: respiración más grande (tecla R, cada 5 min) y pantalla de espera entre las puertas (tecla E).
        /// </summary>
        static string Firma => string.Format(CultureInfo.InvariantCulture,
            "Medidas {0}x{1}x{2} pantalla {3} franja este {4} tramos {5}-{6}-{7}-{8} puertas {9}x{10} visitante {11} nota S {12} respiración {13} en {14},{15} espera {16} r10",
            Ancho, Profundidad, AltoTotal, AltoPantalla, Franja, Tecnico, Entrada, Ropero, Salida, AnchoPuerta, AltoPuerta, AlturaVisitante,
            AnchoNotaS, CaraRespiracion, CentroRespiracionX, CentroRespiracionY, ZonaEspera);

        // Mallas de las pantallas que ya no existen (el lado este tenía dos tramos): se borran al actualizar.
        static readonly string[] MallasViejas = { "Pantalla derecha (ropero).asset", "Pantalla derecha (técnico).asset" };

        // true = usar los materiales que ya existen (para no romper lo que sigue en la escena).
        static bool reusarMateriales;

        [MenuItem("post./Construir escena del cubo")]
        public static void Construir()
        {
            if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/CuboPost", "Generado");
            reusarMateriales = false;

            var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------- contexto ----------
            ArmarContexto();

            // ---------- cubo ----------
            var paredes = ArmarCubo(null);

            // ---------- velas de sombra (fila y lado norte) ----------
            ConstruirVelas.Armar();

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
            Ambiente(sol);

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

            // ---------- visitante ----------
            ArmarVisitante(cam, orbita);

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
            foreach (var malla in MallasViejas)
                if (AssetDatabase.LoadAssetAtPath<Object>($"{Carpeta}/{malla}") != null) AssetDatabase.DeleteAsset($"{Carpeta}/{malla}");

            // Lo que sigue en la escena y depende de las medidas.
            ControladorCubo controlador = null;
            CamaraOrbita orbita = null;
            Transform contexto = null;
            foreach (var raiz in escena.GetRootGameObjects())
            {
                if (controlador == null) controlador = raiz.GetComponentInChildren<ControladorCubo>(true);
                if (orbita == null) orbita = raiz.GetComponentInChildren<CamaraOrbita>(true);
                if (contexto == null && raiz.name == NombreContexto) contexto = raiz.transform;
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

            // El piso rojo del área relevada ya no va: si la escena lo tiene, se saca.
            if (contexto != null) QuitarPisoRojo(contexto);

            if (orbita != null)
            {
                // Solo se aleja la vista general si seguía en el valor de fábrica (24 m).
                if (Mathf.Abs(orbita.distancia - 24f) < 0.01f) orbita.distancia = DistanciaCamara;
                AjustarCamara(orbita);
                EditorUtility.SetDirty(orbita);
            }

            // El visitante se arma de nuevo, parado en el punto de partida.
            foreach (var raiz in escena.GetRootGameObjects())
                if (raiz.name == NombreVisitante) Object.DestroyImmediate(raiz);
            ArmarVisitante(orbita != null ? orbita.GetComponent<Camera>() : null, orbita);

            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            AssetDatabase.SaveAssets();
            Debug.Log("[post.] Cubo actualizado: " + Firma);
        }

        /// <summary>
        /// Lo mismo que "Actualizar solo el cubo", pero desde la consola, con Unity cerrado:
        /// Unity.exe -batchmode -projectPath unity/CuboPost -executeMethod CuboPost.EditorTools.ConstruirCubo.ActualizarCuboDesdeConsola -quit
        /// </summary>
        public static void ActualizarCuboDesdeConsola()
        {
            EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
            ActualizarCubo();
        }

        /// <summary>Corre solo al recompilar: si el cubo de la escena abierta quedó viejo, lo actualiza.</summary>
        internal static void ActualizarSiHaceFalta()
        {
            if (Application.isBatchMode) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var escena = EditorSceneManager.GetActiveScene();
            if (escena.path != RutaEscena) return;
            if (!CuboAlDia(escena))
            {
                Debug.Log("[post.] El cubo de la escena tiene medidas viejas: se actualiza solo.");
                ActualizarCubo();
            }
            if (!PlazaAlDia(escena))
            {
                Debug.Log("[post.] La plaza de la escena es de una versión anterior: se actualiza sola.");
                ActualizarPlaza();
            }
        }

        /// <summary>
        /// ¿La plaza de la escena es la de la versión actual y está completa? Si la escena no tiene
        /// contexto, no se toca nada.
        /// </summary>
        static bool PlazaAlDia(Scene escena)
        {
            foreach (var raiz in escena.GetRootGameObjects())
                if (raiz.name == NombreContexto) return ConstruirEntorno.AlDia(raiz.transform);
            return true;
        }

        /// <summary>
        /// Rearma solo la plaza (calles, explanada, anillo, jardines, edificios y Obelisco) en la
        /// escena abierta. El stand, el sol y la cámara quedan como están.
        /// </summary>
        [MenuItem("post./Actualizar solo la plaza")]
        public static void ActualizarPlaza()
        {
            var escena = EditorSceneManager.GetActiveScene();
            if (escena.path != RutaEscena)
            {
                Debug.LogWarning("[post.] Para actualizar la plaza, abrí primero la escena " + RutaEscena);
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[post.] Salí del modo Play antes de actualizar la plaza.");
                return;
            }
            if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/CuboPost", "Generado");
            reusarMateriales = true;

            Light sol = null;
            foreach (var raiz in escena.GetRootGameObjects())
            {
                if (raiz.name == NombreContexto) Object.DestroyImmediate(raiz);
                else if (sol == null) sol = raiz.GetComponentInChildren<Light>(true);
            }
            ArmarContexto();
            Ambiente(sol);

            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            AssetDatabase.SaveAssets();
            Debug.Log("[post.] Plaza actualizada: " + ConstruirEntorno.Version);
        }

        /// <summary>Todo lo que rodea al stand: el Obelisco y la plaza.</summary>
        static Transform ArmarContexto()
        {
            var matObelisco = Mat("Obelisco", "#ebe7de", 0.15f);
            var contexto = new GameObject(NombreContexto).transform;
            var obelisco = new GameObject("Obelisco", typeof(MeshFilter), typeof(MeshRenderer));
            obelisco.transform.SetParent(contexto, false);
            // En su isla, cruzando Av. Corrientes hacia el sur, sobre el eje de la plaza.
            obelisco.transform.position = ConstruirEntorno.PosicionObelisco;
            var mallaObelisco = MallaObelisco();
            obelisco.GetComponent<MeshFilter>().sharedMesh = mallaObelisco;
            obelisco.AddComponent<MeshCollider>().sharedMesh = mallaObelisco;
            obelisco.GetComponent<MeshRenderer>().sharedMaterial = matObelisco;
            // Calles, explanada, anillo, jardines, cartel BA, mobiliario, edificios y autos.
            ConstruirEntorno.Construir(contexto);
            return contexto;
        }

        /// <summary>Saca el piso rojo que tenían las escenas anteriores, y su material.</summary>
        static void QuitarPisoRojo(Transform contexto)
        {
            var piso = contexto.Find(NombrePisoRojo);
            if (piso != null) Object.DestroyImmediate(piso.gameObject);
            var material = $"{Carpeta}/{NombrePisoRojo}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(material) != null) AssetDatabase.DeleteAsset(material);
        }

        /// <summary>Luz ambiente, cielo y bruma de la escena.</summary>
        static void Ambiente(Light sol)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.82f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.62f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.33f, 0.31f);

            // Cielo de día (el que trae Unity) con el sol de la escena, y una bruma leve a lo lejos.
            var sombreador = Shader.Find("Skybox/Procedural");
            if (sombreador != null)
            {
                var cielo = AssetDatabase.LoadAssetAtPath<Material>($"{Carpeta}/Cielo.mat");
                if (cielo == null) cielo = Guardar(new Material(sombreador) { name = "Cielo" }, "Cielo.mat");
                cielo.SetFloat("_SunSize", 0.035f);
                cielo.SetFloat("_AtmosphereThickness", 0.85f);
                cielo.SetColor("_SkyTint", new Color(0.52f, 0.60f, 0.72f));
                cielo.SetColor("_GroundColor", new Color(0.62f, 0.62f, 0.60f));
                cielo.SetFloat("_Exposure", 1.15f);
                EditorUtility.SetDirty(cielo);
                RenderSettings.skybox = cielo;
            }
            if (sol != null) RenderSettings.sun = sol;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.80f, 0.85f, 0.91f);
            RenderSettings.fogStartDistance = 140f;
            RenderSettings.fogEndDistance = 900f;
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
        /// pantallas en orden, recorriendo el perímetro (frente, derecha, fondo, izquierda).
        /// <paramref name="previas"/>: ajustes de las pantallas anteriores (por nombre) para
        /// conservarlos; null si se arma de cero.
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

            // Puertas del lado este, cada una centrada en su tramo de la franja.
            float zPuertaEntrada = (zEntrada + zTecnico) / 2f;
            float zPuertaSalida = (sur + zRopero) / 2f;
            float medioVano = AnchoPuerta / 2f + Marco;       // la pantalla se abre para la puerta y su marco
            var puertas = new[] { zPuertaSalida, zPuertaEntrada };   // de sur a norte

            var estructura = new GameObject("Estructura").transform;
            estructura.SetParent(cubo, false);
            // Zócalo: corre por todo el perímetro menos en las dos puertas.
            CajaEntre("Zócalo sala", estructura, oeste - vuelo, xFranja, 0f, Zocalo, sur - vuelo, norte + vuelo, matEstructura);
            float zDesde = sur - vuelo, xZocalo = este - 0.12f;   // al este es angosto: detrás están los pasillos
            foreach (var z in puertas)
            {
                CajaEntre("Zócalo este", estructura, xZocalo, este + vuelo, 0f, Zocalo, zDesde, z - medioVano, matEstructura);
                zDesde = z + medioVano;
            }
            CajaEntre("Zócalo este", estructura, xZocalo, este + vuelo, 0f, Zocalo, zDesde, norte + vuelo, matEstructura);
            // Coronamiento: franja oscura de 0,50 m sobre las pantallas (de 5,50 a 6 m). Hace de techo.
            Caja("Coronamiento", estructura, new Vector3(0, AltoPared + Coronamiento / 2f, 0), new Vector3(Ancho + 0.16f, Coronamiento, Profundidad + 0.16f), matEstructura);
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Caja("Esquinero", estructura, new Vector3(sx * Ancho / 2f, AltoPared / 2f, sz * Profundidad / 2f), new Vector3(0.12f, AltoPared, 0.12f), matEstructura);

            // El interior todavía no está armado: la sala es un bloque macizo para el visitante (las
            // pantallas no chocan por sí solas). A los pasillos de la franja sí se entra por las puertas.
            var sala = new GameObject("Sala (colisión · interior pendiente)");
            sala.transform.SetParent(estructura, false);
            var bloque = sala.AddComponent<BoxCollider>();
            bloque.center = new Vector3((oeste + xFranja) / 2f, AltoPared / 2f, 0f);
            bloque.size = new Vector3(xFranja - oeste, AltoPared, Profundidad);

            // Pantallas: recorren el perímetro en orden (frente → derecha → fondo → izquierda) para que
            // el degradé pase de una a otra sin cortes. La del lado este es una sola, de punta a punta,
            // con el hueco de las dos puertas (medido desde su extremo sur y desde el borde de abajo).
            var pantallas = new GameObject("Pantallas LED").transform;
            pantallas.SetParent(cubo, false);
            float perimetro = 2f * (Ancho + Profundidad);
            float yCentro = Zocalo + AltoPantalla / 2f;
            var vanos = new List<Rect>();
            foreach (var z in puertas)
                vanos.Add(new Rect(z - medioVano - sur, 0f, 2f * medioVano, AltoPuerta + Marco - Zocalo));
            var frente = Pared("Pantalla frente", pantallas, new Vector3(0, yCentro, sur), Vector3.back, Ancho, 0f, perimetro, matGradiente, previas);
            var derecha = Pared("Pantalla derecha", pantallas, new Vector3(este, yCentro, 0), Vector3.right, Profundidad, Ancho, perimetro, matGradiente, previas, "Pantalla derecha (ropero)", vanos);
            var fondo = Pared("Pantalla fondo", pantallas, new Vector3(0, yCentro, norte), Vector3.forward, Ancho, Ancho + Profundidad, perimetro, matGradiente, previas);
            var izquierda = Pared("Pantalla izquierda", pantallas, new Vector3(oeste, yCentro, 0), Vector3.left, Profundidad, 2f * Ancho + Profundidad, perimetro, matGradiente, previas);
            var paredes = new List<ParedPantalla> { frente, derecha, fondo, izquierda };
            // Ni puntos ni notas sobre las puertas (con un margen alrededor del marco).
            foreach (var v in vanos)
                derecha.zonasBloqueadas.Add(new Rect(v.x - 0.1f, 0f, v.width + 0.2f, v.height + 0.1f));
            // Ejercicio de respiración para la fila: a la derecha de la puerta de entrada (vista de
            // frente). La zona es el cuadrado del anillo más grande; también queda bloqueada, así los
            // puntos que flotan no la cruzan.
            float ladoRespiracion = CaraRespiracion * EjercicioRespiracion.DiametroMayor / EjercicioRespiracion.DiametroCara;
            var zonaRespiracion = new Rect(CentroRespiracionX - ladoRespiracion / 2f, CentroRespiracionY - ladoRespiracion / 2f, ladoRespiracion, ladoRespiracion);
            derecha.zonasBloqueadas.Add(new Rect(zonaRespiracion.x - 0.1f, Mathf.Max(0f, zonaRespiracion.y - 0.1f), zonaRespiracion.width + 0.2f, zonaRespiracion.height + 0.2f));
            var respiracion = new GameObject("Ejercicio de respiración (tecla R, cada 5 min)").AddComponent<EjercicioRespiracion>();
            respiracion.transform.SetParent(derecha.transform, false);
            respiracion.zona = zonaRespiracion;
            respiracion.largoPantalla = derecha.largo;
            respiracion.altoPantalla = derecha.alto;
            respiracion.Armar();
            // Pantalla de espera (tecla E): entre las dos puertas. Cuando aparece tapa los puntos de
            // atrás con su fondo; mientras no está, la pantalla se ve como siempre.
            var espera = new GameObject("Pantalla de espera (tecla E)").AddComponent<PantallaEspera>();
            espera.transform.SetParent(derecha.transform, false);
            espera.zona = ZonaEspera;
            espera.largoPantalla = derecha.largo;
            espera.altoPantalla = derecha.alto;
            // Notas un 20 % más grandes que antes en todas las pantallas (la S manda, las demás siguen).
            foreach (var p in paredes) p.anchoMinimoNota = AnchoNotaS;

            // Paredes largas (frente y fondo): gráfica post. centrada, como en el frame del Figma
            // (1622:4941). El alto del frame es el alto de la pantalla, y la elipse roja del frame
            // (todo el alto, 1286 px de ancho por cada 637 de alto) es donde no pueden ir notas ni
            // puntos sueltos, así nada tapa el logo; las notas van a los costados.
            float anchoElipse = AltoPantalla * ParedPantalla.FigmaAnchoElipse / ParedPantalla.FigmaAltoFrame;
            foreach (var larga in new[] { frente, fondo })
            {
                larga.composicionCentral = true;
                larga.zonasElipse.Add(new Rect((Ancho - anchoElipse) / 2f, 0f, anchoElipse, AltoPantalla));
            }

            // Por ahora las dedicatorias salen solo en la pantalla del frente (la que ve la cámara al
            // abrir). Las demás muestran gráfica y puntos. Para sumar otra, poner true acá.
            foreach (var p in paredes) p.recibeNotas = p == frente;

            // Vista previa sin Play, ya con las medidas y las zonas definitivas.
            foreach (var p in paredes) p.VistaPrevia();

            // Franja de servicios (lado este, 1,50 m de fondo). De norte a sur: técnico + depósito,
            // entrada, ropero y salida. Desde afuera es todo pantalla: solo se ven las dos puertas.
            var franja = new GameObject("Franja de servicios (este)").transform;
            franja.SetParent(cubo, false);
            CajaEntre("Técnico + depósito", franja, xFranja, este - h, 0f, AltoPared, zTecnico, norte - h, matTunel);
            CajaEntre("Ropero", franja, xFranja, este - h, 0f, AltoPared, zRopero, zEntrada, matPuerta);
            // Puertas del ropero, centradas en el fondo de la franja: una da a la entrada y otra a la salida.
            float xPuerta = (xFranja + este) / 2f;
            CajaEntre("Ropero · puerta a la entrada", franja, xPuerta - PuertaRopero / 2f, xPuerta + PuertaRopero / 2f, 0f, AltoPuertaRopero, zEntrada, zEntrada + 0.02f, matPersonas);
            CajaEntre("Ropero · puerta a la salida", franja, xPuerta - PuertaRopero / 2f, xPuerta + PuertaRopero / 2f, 0f, AltoPuertaRopero, zRopero - 0.02f, zRopero, matPersonas);
            Acceso("Entrada", franja, xFranja, este, zEntrada, zTecnico, zPuertaEntrada, matEstructura, matTunel, matPuerta);
            Acceso("Salida", franja, xFranja, este, sur + h, zRopero, zPuertaSalida, matEstructura, matTunel, matPuerta);
            // La salida da contra la pared sur del stand: su cara de adentro (afuera es pantalla).
            CajaEntre("Salida · pared sur", franja, xFranja, este - h, 0f, AltoAcceso, sur + h, sur + 0.1f, matTunel);

            // Gente haciendo la fila: figuras como las de la plaza, de distintas alturas, mirando hacia
            // la puerta de entrada. La fila sale derecho hacia el este y deja libre el paso a la puerta.
            var fila = new GameObject("Fila").transform;
            fila.SetParent(cubo, false);
            var mallaFigura = Guardar(ConstruirEntorno.MallaDeFigura(AlturaFigura), "Figura de pie.asset");
            var tonos = new[] { Mat("Figura clara", "#d6d0c2", 0.15f), Mat("Figura media", "#bab3a6", 0.15f), Mat("Figura oscura", "#9d978d", 0.15f) };
            float[] alturas = { 1.68f, 1.76f, 1.6f, 1.82f, 1.71f, 1.57f, 1.78f };
            float[] giros = { -6f, 9f, -3f, 14f, 0f, -12f, 5f };
            int[] tono = { 0, 2, 1, 0, 1, 2, 0 };
            for (int i = 0; i < alturas.Length; i++)
            {
                var p = new GameObject("Persona", typeof(MeshFilter), typeof(MeshRenderer));
                p.transform.SetParent(fila, false);
                p.transform.SetPositionAndRotation(
                    new Vector3(este + 1.3f + i * 0.75f, 0f, zPuertaEntrada - 0.06f + (i % 2) * 0.12f),
                    Quaternion.Euler(0f, -90f + giros[i], 0f));   // -90° = mirando al oeste, hacia la puerta
                p.transform.localScale = Vector3.one * (alturas[i] / AlturaFigura);
                p.GetComponent<MeshFilter>().sharedMesh = mallaFigura;
                p.GetComponent<MeshRenderer>().sharedMaterial = tonos[tono[i]];
                var cuerpo = p.AddComponent<CapsuleCollider>();
                cuerpo.direction = 1;   // eje vertical
                cuerpo.radius = 0.22f;
                cuerpo.height = AlturaFigura;
                cuerpo.center = new Vector3(0f, AlturaFigura / 2f, 0f);
            }

            return paredes;
        }

        /// <summary>
        /// Pasillo de entrada o de salida: ocupa su tramo de la franja (de z0 a z1, de x0 a x1) con
        /// <see cref="AltoAcceso"/> de alto libre, detrás de la pantalla del lado este. Hacia afuera
        /// da una sola puerta de <see cref="AnchoPuerta"/> × <see cref="AltoPuerta"/>, centrada en
        /// <paramref name="zPuerta"/>, con su marco. Al fondo, un cierre oscuro provisorio: se saca
        /// cuando se arme el interior.
        /// </summary>
        static void Acceso(string nombre, Transform padre, float x0, float x1, float z0, float z1, float zPuerta,
            Material matEstructura, Material matPared, Material matCierre)
        {
            const float espesor = 0.1f;     // pared que sostiene la pantalla
            const float vuelo = 0.03f;      // cuánto sobresale el marco de la pantalla
            float p0 = zPuerta - AnchoPuerta / 2f, p1 = zPuerta + AnchoPuerta / 2f;
            float xPared = x1 - 0.02f;      // un pelo detrás del plano de la pantalla

            var acceso = new GameObject(nombre).transform;
            acceso.SetParent(padre, false);
            CajaEntre("Techo", acceso, x0, xPared, AltoAcceso, AltoPared, z0, z1, matEstructura);
            CajaEntre("Piso", acceso, x0, x1, 0f, 0.02f, z0, z1, matEstructura);
            CajaEntre("Cierre provisorio (interior pendiente)", acceso, x0 - 0.04f, x0 + 0.01f, 0f, AltoAcceso, z0, z1, matCierre);
            // Pared del frente (la que lleva la pantalla), con el hueco de la puerta.
            CajaEntre("Pared a un lado de la puerta", acceso, xPared - espesor, xPared, 0f, AltoAcceso, z0, p0 - Marco, matPared);
            CajaEntre("Pared al otro lado de la puerta", acceso, xPared - espesor, xPared, 0f, AltoAcceso, p1 + Marco, z1, matPared);
            CajaEntre("Pared sobre la puerta", acceso, xPared - espesor, xPared, AltoPuerta + Marco, AltoAcceso, p0 - Marco, p1 + Marco, matPared);
            // Marco: dos jambas y el dintel, del espesor de la pared y apenas salidos de la pantalla.
            var marco = new GameObject("Puerta · marco").transform;
            marco.SetParent(acceso, false);
            CajaEntre("Jamba", marco, xPared - espesor, x1 + vuelo, 0f, AltoPuerta + Marco, p0 - Marco, p0, matEstructura);
            CajaEntre("Jamba", marco, xPared - espesor, x1 + vuelo, 0f, AltoPuerta + Marco, p1, p1 + Marco, matEstructura);
            CajaEntre("Dintel", marco, xPared - espesor, x1 + vuelo, AltoPuerta, AltoPuerta + Marco, p0, p1, matEstructura);
        }

        /// <summary>
        /// El visitante: una figura como las de la plaza, de <see cref="AlturaVisitante"/>, con piernas
        /// y brazos sueltos para que se balanceen al caminar. Lo maneja <see cref="Caminante"/>, que en
        /// Play pone la cámara a la altura de sus ojos.
        /// </summary>
        static void ArmarVisitante(Camera camara, CamaraOrbita orbita)
        {
            var visitante = new GameObject(NombreVisitante);
            visitante.layer = CapaSinRayos;   // así la cámara que lo sigue de atrás no choca con él
            visitante.transform.SetPositionAndRotation(InicioVisitante, Quaternion.Euler(0f, GiroVisitante, 0f));

            var control = visitante.AddComponent<CharacterController>();
            control.height = AlturaVisitante;
            control.radius = 0.25f;
            control.skinWidth = 0.03f;
            control.center = new Vector3(0f, AlturaVisitante / 2f + control.skinWidth, 0f);
            control.stepOffset = 0.55f;    // sube cordones y el murete del anillo (0,50 m), como una persona
            control.slopeLimit = 50f;
            control.minMoveDistance = 0f;

            var cuerpo = new GameObject("Cuerpo").transform;
            cuerpo.SetParent(visitante.transform, false);
            var material = Mat("Visitante", "#ede8db", 0.2f);
            var mallas = ConstruirEntorno.MallasDeFigura(AlturaVisitante, out var pivotes);
            var partes = new Transform[mallas.Length];
            for (int i = 0; i < mallas.Length; i++)
            {
                string nombre = ConstruirEntorno.PartesDeFigura[i];
                var parte = new GameObject(nombre, typeof(MeshFilter), typeof(MeshRenderer));
                parte.layer = CapaSinRayos;
                parte.transform.SetParent(cuerpo, false);
                parte.transform.localPosition = pivotes[i];
                parte.GetComponent<MeshFilter>().sharedMesh = Guardar(mallas[i], "Visitante · " + nombre.ToLowerInvariant() + ".asset");
                parte.GetComponent<MeshRenderer>().sharedMaterial = material;
                partes[i] = parte.transform;
            }

            var caminante = visitante.AddComponent<Caminante>();
            caminante.alturaOjos = OjosVisitante;
            caminante.camara = camara;
            caminante.orbita = orbita;
            caminante.cuerpo = cuerpo;
            caminante.piernaIzquierda = partes[1];
            caminante.piernaDerecha = partes[2];
            caminante.brazoIzquierdo = partes[3];
            caminante.brazoDerecho = partes[4];
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
        /// nombre, de cuál de las anteriores toma los ajustes. <paramref name="vanos"/>: huecos de
        /// puertas, en metros desde el extremo izquierdo de la pantalla (vista de frente) y desde su
        /// borde de abajo; arrancan abajo y la pantalla sigue por encima.
        /// </summary>
        static ParedPantalla Pared(string nombre, Transform padre, Vector3 centro, Vector3 haciaAfuera,
            float largo, float inicioPerimetro, float perimetro, Material mat, Dictionary<string, string> previas,
            string heredaDe = null, List<Rect> vanos = null)
        {
            var go = new GameObject(nombre, typeof(MeshFilter), typeof(MeshRenderer), typeof(ParedPantalla));
            go.transform.SetParent(padre, false);
            // +Z del objeto = hacia adentro del cubo (la dirección en la que mira el público).
            go.transform.SetPositionAndRotation(centro, Quaternion.LookRotation(-haciaAfuera, Vector3.up));

            // La pantalla se arma por columnas: enteras donde no hay puerta y, donde la hay, solo el
            // paño de arriba. Las UV siguen el perímetro del stand (u) y el alto de la pantalla (v).
            float u0 = inicioPerimetro / perimetro, u1 = (inicioPerimetro + largo) / perimetro;
            var cortes = new List<float> { 0f, largo };
            if (vanos != null)
                foreach (var v in vanos) { cortes.Add(Mathf.Clamp(v.xMin, 0f, largo)); cortes.Add(Mathf.Clamp(v.xMax, 0f, largo)); }
            cortes.Sort();
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normales = new List<Vector3>();
            var triangulos = new List<int>();
            for (int i = 0; i + 1 < cortes.Count; i++)
            {
                float a = cortes[i], b = cortes[i + 1];
                if (b - a < 0.0005f) continue;
                float medio = (a + b) / 2f, desde = 0f;
                if (vanos != null)
                    foreach (var v in vanos)
                        if (medio > v.xMin && medio < v.xMax) desde = Mathf.Max(desde, v.yMax);
                if (desde >= AltoPantalla) continue;
                int k = vertices.Count;
                foreach (var y in new[] { desde, AltoPantalla })
                foreach (var x in new[] { a, b })
                {
                    vertices.Add(new Vector3(x - largo / 2f, y - AltoPantalla / 2f, 0f));
                    uvs.Add(new Vector2(Mathf.Lerp(u0, u1, x / largo), y / AltoPantalla));
                    normales.Add(Vector3.back);
                }
                triangulos.AddRange(new[] { k, k + 2, k + 3, k, k + 3, k + 1 });
            }
            var malla = new Mesh { name = nombre };
            malla.SetVertices(vertices);
            malla.SetUVs(0, uvs);
            malla.SetNormals(normales);
            malla.SetTriangles(triangulos, 0);
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
