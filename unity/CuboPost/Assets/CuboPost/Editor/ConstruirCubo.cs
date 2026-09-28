using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CuboPost.EditorTools
{
    /// <summary>
    /// Menú post. → Construir escena del cubo.
    /// Arma la instalación a escala real (metros) según la planta del Figma:
    ///   planta 13,5 × 9,5 m · altura 3 m · 4 pantallas LED exteriores (una por pared)
    ///   · túnel de entrada de 1,8 m al frente · Plaza de la República con el Obelisco de fondo.
    /// </summary>
    public static class ConstruirCubo
    {
        // Medidas (metros)
        const float Ancho = 13.5f;         // frente y fondo
        const float Profundidad = 9.5f;    // laterales
        const float AltoTotal = 3.0f;      // bajo, para que se vean bien las poses de adentro
        const float Zocalo = 0.2f;         // la pantalla arranca a 20 cm del piso
        const float AltoPantalla = AltoTotal - Zocalo;   // 2,8 m de LED
        const float TunelAncho = 1.8f, TunelProfundidad = 1.45f, TunelAlto = 2.4f;

        const string Carpeta = "Assets/CuboPost/Generado";
        const string RutaEscena = "Assets/Scenes/CuboPost.unity";

        [MenuItem("post./Construir escena del cubo")]
        public static void Construir()
        {
            if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/CuboPost", "Generado");

            var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Materiales
            var matGradiente = Guardar(new Material(Shader.Find("CuboPost/GradienteParedes")) { name = "Pantallas LED" }, "Pantallas LED.mat");
            var matEstructura = Lit("Estructura", "#252525", 0.35f);
            var matTunel = Lit("Tunel", "#2f2f2f", 0.3f);
            var matPuerta = Lit("Puerta", "#141414", 0.7f);
            var matPlaza = Lit("Plaza", "#8d8a85", 0.08f);
            var matPlazaRoja = Lit("Plaza roja", "#b56a64", 0.08f);
            var matObelisco = Lit("Obelisco", "#ebe7de", 0.15f);
            var matPersonas = Lit("Personas", "#ede8db", 0.2f);

            // ---------- contexto ----------
            var contexto = new GameObject("Contexto (plaza)").transform;
            Caja("Plaza", contexto, new Vector3(0, -0.05f, 10f), new Vector3(240f, 0.1f, 240f), matPlaza);
            Caja("Plaza roja", contexto, new Vector3(0, -0.045f, -1.5f), new Vector3(26f, 0.1f, 22f), matPlazaRoja);
            var obelisco = new GameObject("Obelisco", typeof(MeshFilter), typeof(MeshRenderer));
            obelisco.transform.SetParent(contexto, false);
            obelisco.transform.position = new Vector3(0f, 0f, 48f);
            obelisco.GetComponent<MeshFilter>().sharedMesh = MallaObelisco();
            obelisco.GetComponent<MeshRenderer>().sharedMaterial = matObelisco;

            // ---------- cubo ----------
            var cubo = new GameObject("Cubo post.").transform;

            var estructura = new GameObject("Estructura").transform;
            estructura.SetParent(cubo, false);
            Caja("Zócalo", estructura, new Vector3(0, Zocalo / 2f, 0), new Vector3(Ancho + 0.06f, Zocalo, Profundidad + 0.06f), matEstructura);
            Caja("Techo", estructura, new Vector3(0, AltoTotal + 0.07f, 0), new Vector3(Ancho + 0.16f, 0.14f, Profundidad + 0.16f), matEstructura);
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Caja("Esquinero", estructura, new Vector3(sx * Ancho / 2f, AltoTotal / 2f, sz * Profundidad / 2f), new Vector3(0.12f, AltoTotal, 0.12f), matEstructura);

            // Pantallas: recorren el perímetro en orden (frente → derecha → fondo → izquierda) para que
            // el degradé pase de una a otra sin cortes.
            var pantallas = new GameObject("Pantallas LED").transform;
            pantallas.SetParent(cubo, false);
            float perimetro = 2f * (Ancho + Profundidad);
            float yCentro = Zocalo + AltoPantalla / 2f;
            var paredes = new List<ParedPantalla>
            {
                Pared("Pantalla frente", pantallas, new Vector3(0, yCentro, -Profundidad / 2f), Vector3.back, Ancho, 0f, perimetro, matGradiente),
                Pared("Pantalla derecha", pantallas, new Vector3(Ancho / 2f, yCentro, 0), Vector3.right, Profundidad, Ancho, perimetro, matGradiente),
                Pared("Pantalla fondo", pantallas, new Vector3(0, yCentro, Profundidad / 2f), Vector3.forward, Ancho, Ancho + Profundidad, perimetro, matGradiente),
                Pared("Pantalla izquierda", pantallas, new Vector3(-Ancho / 2f, yCentro, 0), Vector3.left, Profundidad, 2f * Ancho + Profundidad, perimetro, matGradiente),
            };
            // El túnel tapa la parte central de la pantalla del frente: ahí no van notas.
            paredes[0].zonasBloqueadas.Add(new Rect(Ancho / 2f - TunelAncho / 2f - 0.15f, 0f, TunelAncho + 0.3f, TunelAlto - Zocalo + 0.1f));

            // Pared de atrás: composición post. grande en el centro y una elipse (43 % del ancho,
            // todo el alto) donde no pueden ir notas; las notas van a los costados.
            var fondo = paredes[2];
            fondo.composicionCentral = true;
            fondo.anchoComposicion = Ancho * 0.283f;
            float anchoElipse = Ancho * 0.43f;
            fondo.zonasElipse.Add(new Rect((Ancho - anchoElipse) / 2f, 0f, anchoElipse, AltoPantalla));

            // Que el modo Play siga animando aunque Unity no esté en primer plano.
            PlayerSettings.runInBackground = true;

            // Túnel de entrada con doble puerta.
            var tunel = new GameObject("Túnel de entrada").transform;
            tunel.SetParent(cubo, false);
            float zTunel = -Profundidad / 2f - TunelProfundidad / 2f + 0.02f;
            Caja("Túnel", tunel, new Vector3(0, TunelAlto / 2f, zTunel), new Vector3(TunelAncho, TunelAlto, TunelProfundidad), matTunel);
            Caja("Puerta izquierda", tunel, new Vector3(-0.31f, 1.05f, -Profundidad / 2f - TunelProfundidad - 0.005f), new Vector3(0.6f, 2.1f, 0.03f), matPuerta);
            Caja("Puerta derecha", tunel, new Vector3(0.31f, 1.05f, -Profundidad / 2f - TunelProfundidad - 0.005f), new Vector3(0.6f, 2.1f, 0.03f), matPuerta);

            // Gente haciendo la fila (referencia de escala: 1,70 m). La fila sale derecho desde la
            // puerta del túnel, así no tapa la pantalla del frente.
            var fila = new GameObject("Fila (referencia 1,70 m)").transform;
            fila.SetParent(cubo, false);
            for (int i = 0; i < 7; i++)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                p.name = "Persona";
                p.transform.SetParent(fila, false);
                p.transform.localScale = new Vector3(0.45f, 0.85f, 0.45f);
                p.transform.position = new Vector3((i % 2 == 0 ? 0.12f : -0.1f), 0.85f, -Profundidad / 2f - TunelProfundidad - 0.9f - i * 0.85f);
                p.GetComponent<MeshRenderer>().sharedMaterial = matPersonas;
            }

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
            orbita.ancho = Ancho;
            orbita.profundidad = Profundidad;
            var rot = Quaternion.Euler(orbita.inclinacion, orbita.giro, 0f);
            camGo.transform.SetPositionAndRotation(orbita.objetivo - rot * Vector3.forward * orbita.distancia, rot);

            EditorSceneManager.SaveScene(escena, RutaEscena);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(RutaEscena, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[post.] Escena del cubo construida: " + RutaEscena);
        }

        // ---------------- ayudas ----------------

        static ParedPantalla Pared(string nombre, Transform padre, Vector3 centro, Vector3 haciaAfuera,
            float largo, float inicioPerimetro, float perimetro, Material mat)
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
            pared.largo = largo;
            pared.alto = AltoPantalla;
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
}
