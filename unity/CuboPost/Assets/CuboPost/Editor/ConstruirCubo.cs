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
    /// Arma la instalación a escala real (metros) según la planta y las vistas del Figma:
    ///   planta 20 × 10 m · altura total 4 m (3,50 m de pared + 0,50 m de coronamiento)
    ///   · 4 pantallas LED exteriores (una por pared), fondo #252525 con los puntos POST dispersos
    ///   · gráfica post. centrada en las dos paredes largas (frente y fondo)
    ///   · entrada de 3 × 2,8 m centrada en el lateral izquierdo (oeste)
    ///   · Plaza de la República con el Obelisco de fondo.
    ///
    /// Menú post. → Construir escena del cubo: rearma TODA la escena desde cero (plaza incluida).
    /// Menú post. → Actualizar solo el cubo: cambia el cubo, la entrada y la fila en la escena
    ///   abierta y deja la plaza como está. Esto último también corre solo cuando Unity recompila
    ///   y encuentra que el cubo de la escena no coincide con las medidas de este archivo.
    /// </summary>
    public static class ConstruirCubo
    {
        // Medidas (metros)
        const float Ancho = 20f;           // largo: frente y fondo
        const float Profundidad = 10f;     // ancho: laterales
        const float AltoPared = 3.5f;      // pared con pantalla
        const float Coronamiento = 0.5f;   // franja oscura de arriba
        const float AltoTotal = AltoPared + Coronamiento;   // 4 m
        const float Zocalo = 0.2f;         // la pantalla arranca a 20 cm del piso
        const float AltoPantalla = AltoPared - Zocalo;      // 3,3 m de LED
        // Entrada: en el lateral izquierdo (-X), centrada. 3 m de ancho × 2,8 m de alto.
        const float EntradaAncho = 3f, EntradaAlto = 2.8f, EntradaProfundidad = 1.2f;
        // Vista general de la cámara: más lejos que antes porque el cubo es más largo.
        const float DistanciaCamara = 32f;

        // Piso rojo alrededor del cubo; corrido al oeste para cubrir la entrada y la fila.
        static readonly Vector3 PlazaRojaCentro = new Vector3(-1.5f, -0.045f, -1.5f);
        static readonly Vector3 PlazaRojaTamano = new Vector3(35f, 0.1f, 22f);

        const string Carpeta = "Assets/CuboPost/Generado";
        const string RutaEscena = "Assets/Scenes/CuboPost.unity";
        const string NombreCubo = "Cubo post.";

        /// <summary>
        /// Resumen de las medidas. Queda guardado en la escena (un objeto vacío dentro del cubo) y
        /// sirve para saber si el cubo de la escena está al día con este archivo.
        /// Si se cambia la disposición sin tocar ninguna constante, subir el número del final.
        /// r2: gráfica post. también en el frente y fondo de pantallas #252525.
        /// r3: las dedicatorias salen solo en la pantalla del frente.
        /// </summary>
        static string Firma => string.Format(CultureInfo.InvariantCulture,
            "Medidas {0}x{1}x{2} pantalla {3} entrada {4}x{5}x{6} oeste r3",
            Ancho, Profundidad, AltoTotal, AltoPantalla, EntradaAncho, EntradaAlto, EntradaProfundidad);

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
        /// Cambia solo el cubo (estructura, pantallas, entrada y fila) en la escena abierta.
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

        // ---------------- el cubo ----------------

        /// <summary>
        /// Crea "Cubo post." con estructura, 4 pantallas, entrada y fila, y devuelve las pantallas
        /// en orden (frente, derecha, fondo, izquierda). <paramref name="previas"/>: ajustes de las
        /// pantallas anteriores (por nombre) para conservarlos; null si se arma de cero.
        /// </summary>
        static List<ParedPantalla> ArmarCubo(Dictionary<string, string> previas)
        {
            var matGradiente = MatGradiente();
            var matEstructura = Mat("Estructura", "#252525", 0.35f);
            var matTunel = Mat("Tunel", "#2f2f2f", 0.3f);
            var matPuerta = Mat("Puerta", "#141414", 0.7f);
            var matPersonas = Mat("Personas", "#ede8db", 0.2f);

            var cubo = new GameObject(NombreCubo).transform;
            new GameObject(Firma).transform.SetParent(cubo, false);   // marca de medidas (objeto vacío)

            var estructura = new GameObject("Estructura").transform;
            estructura.SetParent(cubo, false);
            Caja("Zócalo", estructura, new Vector3(0, Zocalo / 2f, 0), new Vector3(Ancho + 0.06f, Zocalo, Profundidad + 0.06f), matEstructura);
            // Coronamiento: franja oscura de 0,50 m sobre las pantallas (de 3,50 a 4 m). Hace de techo.
            Caja("Coronamiento", estructura, new Vector3(0, AltoPared + Coronamiento / 2f, 0), new Vector3(Ancho + 0.16f, Coronamiento, Profundidad + 0.16f), matEstructura);
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Caja("Esquinero", estructura, new Vector3(sx * Ancho / 2f, AltoPared / 2f, sz * Profundidad / 2f), new Vector3(0.12f, AltoPared, 0.12f), matEstructura);

            // Pantallas: recorren el perímetro en orden (frente → derecha → fondo → izquierda) para que
            // el degradé pase de una a otra sin cortes.
            var pantallas = new GameObject("Pantallas LED").transform;
            pantallas.SetParent(cubo, false);
            float perimetro = 2f * (Ancho + Profundidad);
            float yCentro = Zocalo + AltoPantalla / 2f;
            var paredes = new List<ParedPantalla>
            {
                Pared("Pantalla frente", pantallas, new Vector3(0, yCentro, -Profundidad / 2f), Vector3.back, Ancho, 0f, perimetro, matGradiente, previas),
                Pared("Pantalla derecha", pantallas, new Vector3(Ancho / 2f, yCentro, 0), Vector3.right, Profundidad, Ancho, perimetro, matGradiente, previas),
                Pared("Pantalla fondo", pantallas, new Vector3(0, yCentro, Profundidad / 2f), Vector3.forward, Ancho, Ancho + Profundidad, perimetro, matGradiente, previas),
                Pared("Pantalla izquierda", pantallas, new Vector3(-Ancho / 2f, yCentro, 0), Vector3.left, Profundidad, 2f * Ancho + Profundidad, perimetro, matGradiente, previas),
            };
            // La entrada tapa la parte central de la pantalla izquierda: ahí no van notas.
            paredes[3].zonasBloqueadas.Add(new Rect(Profundidad / 2f - EntradaAncho / 2f - 0.15f, 0f, EntradaAncho + 0.3f, EntradaAlto - Zocalo + 0.1f));

            // Paredes largas (frente y fondo): gráfica post. centrada, como en el frame del Figma
            // (1622:4941). El alto del frame es el alto de la pantalla, y la elipse roja del frame
            // (todo el alto, 1286 px de ancho por cada 637 de alto) es donde no pueden ir notas ni
            // puntos sueltos, así nada tapa el logo; las notas van a los costados.
            float anchoElipse = AltoPantalla * ParedPantalla.FigmaAnchoElipse / ParedPantalla.FigmaAltoFrame;
            foreach (var larga in new[] { paredes[0], paredes[2] })
            {
                larga.composicionCentral = true;
                larga.zonasElipse.Add(new Rect((Ancho - anchoElipse) / 2f, 0f, anchoElipse, AltoPantalla));
            }

            // Por ahora las dedicatorias salen solo en la pantalla del frente (la que ve la cámara al
            // abrir). Las otras tres muestran gráfica y puntos. Para sumar otra, poner true acá.
            for (int i = 0; i < paredes.Count; i++) paredes[i].recibeNotas = i == 0;

            // Vista previa sin Play, ya con las medidas y las zonas definitivas.
            foreach (var p in paredes) p.VistaPrevia();

            // Entrada con doble puerta: 3 m de ancho × 2,8 m de alto, centrada en el lateral
            // izquierdo (oeste) y sobresaliendo 1,2 m hacia afuera.
            var tunel = new GameObject("Entrada").transform;
            tunel.SetParent(cubo, false);
            float xEntrada = -Ancho / 2f - EntradaProfundidad / 2f + 0.02f;
            float xPuertas = -Ancho / 2f - EntradaProfundidad - 0.005f;
            const float altoPuerta = 2.6f, anchoPuerta = 1.35f;
            Caja("Túnel", tunel, new Vector3(xEntrada, EntradaAlto / 2f, 0), new Vector3(EntradaProfundidad, EntradaAlto, EntradaAncho), matTunel);
            Caja("Puerta izquierda", tunel, new Vector3(xPuertas, altoPuerta / 2f, -(anchoPuerta / 2f + 0.01f)), new Vector3(0.03f, altoPuerta, anchoPuerta), matPuerta);
            Caja("Puerta derecha", tunel, new Vector3(xPuertas, altoPuerta / 2f, anchoPuerta / 2f + 0.01f), new Vector3(0.03f, altoPuerta, anchoPuerta), matPuerta);

            // Gente haciendo la fila (referencia de escala: 1,70 m). La fila sale derecho desde la
            // puerta hacia el oeste, así no tapa la pantalla izquierda.
            var fila = new GameObject("Fila (referencia 1,70 m)").transform;
            fila.SetParent(cubo, false);
            for (int i = 0; i < 7; i++)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                p.name = "Persona";
                p.transform.SetParent(fila, false);
                p.transform.localScale = new Vector3(0.45f, 0.85f, 0.45f);
                float xPuerta = -Ancho / 2f - EntradaProfundidad - 0.9f;
                p.transform.position = new Vector3(xPuerta - i * 0.85f, 0.85f, 0.05f + (i % 2) * 0.12f);
                p.GetComponent<MeshRenderer>().sharedMaterial = matPersonas;
            }

            return paredes;
        }

        static void AjustarCamara(CamaraOrbita orbita)
        {
            orbita.ancho = Ancho;
            orbita.profundidad = Profundidad;
            var rot = Quaternion.Euler(orbita.inclinacion, orbita.giro, 0f);
            orbita.transform.SetPositionAndRotation(orbita.objetivo - rot * Vector3.forward * orbita.distancia, rot);
        }

        // ---------------- ayudas ----------------

        static ParedPantalla Pared(string nombre, Transform padre, Vector3 centro, Vector3 haciaAfuera,
            float largo, float inicioPerimetro, float perimetro, Material mat, Dictionary<string, string> previas)
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
            if (previas != null && previas.TryGetValue(nombre, out var ajustes))
                JsonUtility.FromJsonOverwrite(ajustes, pared);
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
