using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CuboPost.EditorTools
{
    /// <summary>
    /// Velas de sombra sobre la fila y el lado norte del stand (plano "Telas de vela · altura 6,50 m"
    /// del Figma, referencias: Kengo Kuma "Oceano" y las velas circulares en aro).
    ///   · 8 velas circulares (Ø 6,4 · 4,1 · 3,7 m) en dos grupos: norte y este (la fila).
    ///   · Alturas escalonadas de 6,30 a 6,90 m (promedio 6,50): las que se pisan en planta quedan a
    ///     30 cm o más una de otra, así no se chocan, y todas pasan por encima del stand (6 m).
    ///   · Cada vela: aro de caño de aluminio Ø 60 mm y tela PES 220 g/m² semitraslúcida (crema de la
    ///     marca, ~70 % de opacidad) tensada con tensores cada 30 cm.
    ///   · Los aros de cada grupo están unidos entre sí: comparten palo donde se cruzan y, donde se
    ///     cruzan sin palo, los une un caño vertical corto. Así cada grupo es una sola estructura y
    ///     alcanza con pocos palos (Ø 100 mm), cada uno con tornapuntas arriba.
    ///   · 3 de los 4 postes de carga del plano hacen de soporte: suben hasta los aros y los toman con
    ///     un brazo. Apoyan en un disco bajo de placas de acero, que no molesta a la circulación.
    ///   · Los demás palos van solo en el borde este (contra el pasto) y en el pasto del lado norte:
    ///     ninguna base queda en la explanada. Cada uno sobre una base de 1 × 1 m con bloques de
    ///     hormigón (~1000 kg) y goma abajo, forrada con un cajón gris de 45 cm que sirve de banco.
    /// Menú post. → Construir velas de sombra. También se arman solas al recompilar si faltan o son
    /// de una versión anterior.
    /// </summary>
    public static class ConstruirVelas
    {
        const string Version = "Velas v3";   // subir si cambia la disposición
        const string NombreRaiz = "Velas de sombra";
        const string RutaEscena = "Assets/Scenes/CuboPost.unity";
        const string Carpeta = "Assets/CuboPost/Generado/Velas";

        // Velas del plano "01 · Implantación" (página "prueba mapa", grupo 1803:4793; 100 px = 1 m):
        // centro (x este, z norte), diámetro y altura del aro, en metros. Grupo 0 = norte, 1 = este.
        static readonly (float x, float z, float d, float h, int grupo)[] Velas =
        {
            (7.50f, 10.08f, 4.14f, 6.5f, 0), (10.37f, 10.29f, 6.40f, 6.9f, 0),
            (11.64f, 8.70f, 4.14f, 6.3f, 0), (13.68f, 8.98f, 4.14f, 6.6f, 0),
            (15.64f, 3.71f, 6.40f, 6.8f, 1), (12.44f, 1.64f, 4.14f, 6.4f, 1),
            (17.26f, -0.43f, 4.14f, 6.4f, 1), (13.95f, -2.96f, 6.40f, 6.7f, 1),
        };
        // Postes de carga del plano (grupo 1803:4828). sostiene = true: el poste sube hasta los aros
        // cercanos y los toma con un brazo (reemplaza a un palo de soporte).
        static readonly (float x, float z, bool sostiene)[] Cargadores =
        {
            (7.88f, 13.14f, true), (15.94f, 7.45f, true), (18.21f, -2.96f, true), (9.57f, 6.99f, false),
        };
        // Palos comunes (además de los cargadores) por grupo. Solo van en el borde, fuera de la explanada.
        const int PalosPorGrupo = 3;

        // Medidas de la estructura (m).
        const float RadioAro = 0.03f, RadioPalo = 0.05f, RadioUnion = 0.03f, RadioTornapunta = 0.025f;
        const float LadoBase = 1f, AltoBase = 0.45f, LargoTornapunta = 0.9f;
        const float MargenTela = 0.12f, CadaTensor = 0.3f, Comba = 0.12f;
        const float OpacidadTela = 0.72f;

        // Los palos comunes (con su base de hormigón) solo van fuera de la explanada por donde circula
        // la gente: en el borde este, contra el pasto, o en el pasto del lado norte.
        const float BordeEste = 17.3f, BordeNorte = 11.0f;
        // También contra la pared ciega del ropero (entre las dos puertas): la base queda como banco
        // pegado a la pared, lejos de la fila y del paso de la salida.
        static readonly Rect ContraElRopero = Rect.MinMaxRect(11.2f, -2.6f, 12.2f, 0f);
        static bool FueraDeLaCirculacion(Vector2 p) => p.x >= BordeEste || p.y >= BordeNorte || ContraElRopero.Contains(p);

        static Mesh cilindro;

        [MenuItem("post./Construir velas de sombra")]
        public static void Construir()
        {
            var escena = EditorSceneManager.GetActiveScene();
            foreach (var raiz in escena.GetRootGameObjects())
                if (raiz.name == NombreRaiz) Object.DestroyImmediate(raiz);
            Armar();
            EditorSceneManager.MarkSceneDirty(escena);
            if (!string.IsNullOrEmpty(escena.path)) EditorSceneManager.SaveScene(escena);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Para la consola: abre la escena y arma las velas.</summary>
        public static void ConstruirDesdeConsola()
        {
            EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
            Construir();
        }

        internal static void ConstruirSiHaceFalta()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var escena = EditorSceneManager.GetActiveScene();
            if (escena.path != RutaEscena) return;
            foreach (var raiz in escena.GetRootGameObjects())
                if (raiz.name == NombreRaiz && raiz.transform.Find(Version) != null) return;
            Debug.Log("[post.] Faltan las velas de sombra (o son de otra versión): se arman solas.");
            Construir();
        }

        /// <summary>Arma las velas en la escena abierta (sin guardar).</summary>
        public static GameObject Armar()
        {
            if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/CuboPost/Generado", "Velas");
            cilindro = Guardar(MallaCilindro(16), "Caño.asset");
            var matCano = Lit("Caño aluminio blanco", "#c8cbcc", 0.45f, 0.35f);
            var matBase = Lit("Base banco", "#b4b7b8", 0.2f, 0f);
            var matGoma = Lit("Goma de la base", "#262626", 0.1f, 0f);
            var matTela = Tela();

            var raiz = new GameObject(NombreRaiz).transform;
            new GameObject(Version).transform.SetParent(raiz, false);
            var informe = new List<string>();
            var matAcero = Lit("Placas de contrapeso", "#5d6062", 0.3f, 0.5f);

            // Postes de carga: los que sostienen toman los aros que tienen a menos de 80 cm.
            var postes = new GameObject("Postes de carga").transform;
            postes.SetParent(raiz, false);
            var deCargadores = new List<Apoyo>();
            foreach (var c in Cargadores)
            {
                var ap = new Apoyo { p = new Vector2(c.x, c.z) };
                if (c.sostiene)
                    for (int i = 0; i < Velas.Length; i++)
                        if (Mathf.Abs(Vector2.Distance(ap.p, Centro(i)) - Radio(i)) < 0.8f) ap.velas.Add(i);
                ap.alto = ap.velas.Count > 0 ? ap.velas.Max(i => Velas[i].h) : 0f;
                Cargador(postes, ap, matCano, matAcero, matGoma);
                deCargadores.Add(ap);
            }

            for (int g = 0; g < 2; g++)
            {
                var grupo = new GameObject(g == 0 ? "Grupo norte" : "Grupo este (fila)").transform;
                grupo.SetParent(raiz, false);
                var indices = Enumerable.Range(0, Velas.Length).Where(i => Velas[i].grupo == g).ToList();

                foreach (int i in indices) Vela(grupo, i, matCano, matTela);
                var palos = ElegirPalos(indices, deCargadores);
                foreach (var p in palos) Palo(grupo, p, matCano, matBase, matGoma);
                Uniones(grupo, indices, palos.Concat(deCargadores).ToList(), matCano);

                var apoyos = indices.ToDictionary(i => i, i => palos.Concat(deCargadores).Count(p => p.velas.Contains(i)));
                informe.Add($"{grupo.name}: {palos.Count} palos + cargadores; apoyos por vela: {string.Join(", ", apoyos.Values)}");
            }
            Debug.Log("[post.] Velas de sombra armadas. " + string.Join(" · ", informe));
            return raiz.gameObject;
        }

        // ---------------- palos ----------------

        class Apoyo { public Vector2 p; public List<int> velas = new List<int>(); public float alto; }

        static Vector2 Centro(int i) => new Vector2(Velas[i].x, Velas[i].z);
        static float Radio(int i) => Velas[i].d / 2f;

        static bool Pisan(int a, int b) => Vector2.Distance(Centro(a), Centro(b)) < Radio(a) + Radio(b) - 0.05f;

        /// <summary>Los dos puntos donde se cruzan los aros a y b (en planta).</summary>
        static IEnumerable<Vector2> Cruces(int a, int b)
        {
            Vector2 c0 = Centro(a), c1 = Centro(b);
            float r0 = Radio(a), r1 = Radio(b), d = Vector2.Distance(c0, c1);
            if (d >= r0 + r1 || d <= Mathf.Abs(r0 - r1)) yield break;
            float l = (r0 * r0 - r1 * r1 + d * d) / (2f * d), h = Mathf.Sqrt(Mathf.Max(0f, r0 * r0 - l * l));
            var u = (c1 - c0) / d;
            var m = c0 + u * l;
            var n = new Vector2(-u.y, u.x);
            yield return m + n * h;
            yield return m - n * h;
        }

        static bool Valido(Apoyo a)
        {
            if (!FueraDeLaCirculacion(a.p)) return false;
            if (Cargadores.Any(c => Vector2.Distance(new Vector2(c.x, c.z), a.p) < 2f)) return false;
            // El palo no puede atravesar la tela de una vela más baja que no sostiene.
            for (int i = 0; i < Velas.Length; i++)
            {
                if (a.velas.Contains(i) || Velas[i].h >= a.alto - 0.01f) continue;
                if (Vector2.Distance(a.p, Centro(i)) < Radio(i) + 0.15f) return false;
            }
            return true;
        }

        /// <summary>
        /// Elige los palos de un grupo: primero donde se cruzan dos aros (un palo sostiene dos velas),
        /// hasta que cada vela tenga al menos dos apoyos, y después los reparte para que queden separados.
        /// </summary>
        static List<Apoyo> ElegirPalos(List<int> indices, List<Apoyo> cargadores)
        {
            var candidatos = new List<Apoyo>();
            foreach (int a in indices)
            foreach (int b in indices)
            {
                if (b <= a || !Pisan(a, b)) continue;
                foreach (var p in Cruces(a, b))
                    candidatos.Add(new Apoyo { p = p, velas = { a, b }, alto = Mathf.Max(Velas[a].h, Velas[b].h) });
            }
            foreach (int a in indices)
                for (int k = 0; k < 144; k++)
                {
                    float ang = k * Mathf.PI * 2f / 144f;   // cada 2,5°: la franja contra el ropero es angosta
                    var p = Centro(a) + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Radio(a);
                    // Si el punto del aro cae justo sobre otro aro, también lo sostiene.
                    var ap = new Apoyo { p = p, velas = { a }, alto = Velas[a].h };
                    candidatos.Add(ap);
                }
            candidatos = candidatos.Where(Valido).ToList();

            var elegidos = new List<Apoyo>();
            var apoyos = indices.ToDictionary(i => i, i => cargadores.Count(c => c.velas.Contains(i)));
            while (elegidos.Count < PalosPorGrupo)
            {
                Apoyo mejor = null;
                float puntaje = float.MinValue;
                foreach (var c in candidatos)
                {
                    float cerca = elegidos.Count == 0 ? 6f : elegidos.Min(e => Vector2.Distance(e.p, c.p));
                    if (cerca < 2f) continue;
                    float falta = c.velas.Sum(v => Mathf.Max(0, 2 - apoyos[v]));
                    float s = falta * 10f + Mathf.Min(cerca, 6f) + (c.velas.Count - 1) * 3f;
                    if (s > puntaje) { puntaje = s; mejor = c; }
                }
                if (mejor == null) break;
                elegidos.Add(mejor);
                foreach (var v in mejor.velas) apoyos[v]++;
            }
            foreach (var v in apoyos.Where(kv => kv.Value < 2))
                Debug.LogWarning($"[post.] La vela {v.Key + 1} quedó con {v.Value} palo(s) propio(s); se sostiene por las uniones con las otras.");
            return elegidos;
        }

        static void Palo(Transform padre, Apoyo a, Material cano, Material matBase, Material goma)
        {
            var palo = new GameObject("Palo").transform;
            palo.SetParent(padre, false);
            palo.position = new Vector3(a.p.x, 0f, a.p.y);

            // Base con contrapeso: bloques de hormigón forrados (banco de 45 cm), goma abajo.
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "Base · goma";
            g.transform.SetParent(palo, false);
            g.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            g.transform.localScale = new Vector3(LadoBase + 0.02f, 0.03f, LadoBase + 0.02f);
            g.GetComponent<Renderer>().sharedMaterial = goma;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = "Base · bloques de hormigón forrados (banco)";
            b.transform.SetParent(palo, false);
            b.transform.localPosition = new Vector3(0f, 0.03f + (AltoBase - 0.03f) / 2f, 0f);
            b.transform.localScale = new Vector3(LadoBase, AltoBase - 0.03f, LadoBase);
            b.GetComponent<Renderer>().sharedMaterial = matBase;

            var arriba = a.alto + RadioAro;
            var fuste = Tubo("Palo Ø100", palo, new Vector3(a.p.x, AltoBase, a.p.y), new Vector3(a.p.x, arriba, a.p.y), RadioPalo, cano);
            var choque = fuste.AddComponent<CapsuleCollider>();
            choque.direction = 1;
            choque.radius = 1f;
            choque.height = 1f;
            choque.center = new Vector3(0f, 0.5f, 0f);

            // Tornapuntas: del palo al aro, 90 cm a cada lado (o una por aro si sostiene dos).
            foreach (int v in a.velas)
            {
                var c = Centro(v);
                float r = Radio(v), h = Velas[v].h;
                float ang = Mathf.Atan2(a.p.y - c.y, a.p.x - c.x), paso = LargoTornapunta / r;
                var lados = a.velas.Count == 1 ? new[] { -1f, 1f } : new[] { v == a.velas[0] ? -1f : 1f };
                foreach (var s in lados)
                {
                    float t = ang + s * paso;
                    var enAro = new Vector3(c.x + Mathf.Cos(t) * r, h, c.y + Mathf.Sin(t) * r);
                    Tubo("Tornapunta", palo, new Vector3(a.p.x, h - LargoTornapunta, a.p.y), enAro, RadioTornapunta, cano);
                }
            }
        }

        /// <summary>
        /// Poste de carga (referencia: poste con mesa redonda y puertos USB). Si sostiene, sube hasta
        /// los aros y los toma con un brazo horizontal y una tornapunta. Apoya en un disco bajo de
        /// placas de acero (Ø 1,2 m, 12 cm, ~1000 kg) que no molesta a la circulación.
        /// </summary>
        static void Cargador(Transform padre, Apoyo a, Material cano, Material acero, Material oscuro)
        {
            var poste = new GameObject(a.velas.Count > 0 ? "Poste de carga (sostiene las velas)" : "Poste de carga").transform;
            poste.SetParent(padre, false);
            var p = new Vector3(a.p.x, 0f, a.p.y);
            poste.position = p;

            Tubo("Contrapeso · placas de acero Ø1,2", poste, p, p + Vector3.up * 0.10f, 0.6f, acero);
            Tubo("Contrapeso · bisel", poste, p + Vector3.up * 0.10f, p + Vector3.up * 0.12f, 0.56f, acero);

            float arriba = a.velas.Count > 0 ? a.alto + RadioAro : 2.6f;
            var fuste = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fuste.name = "Poste 20 × 20";
            fuste.transform.SetParent(poste, false);
            fuste.transform.localPosition = new Vector3(0f, 0.12f + (arriba - 0.12f) / 2f, 0f);
            fuste.transform.localScale = new Vector3(0.2f, arriba - 0.12f, 0.2f);
            fuste.GetComponent<Renderer>().sharedMaterial = cano;

            // Mesa redonda para apoyar el teléfono, con la caja de los puertos USB.
            Tubo("Mesa Ø80", poste, p + Vector3.up * 1.0f, p + Vector3.up * 1.03f, 0.4f, cano);
            var caja = GameObject.CreatePrimitive(PrimitiveType.Cube);
            caja.name = "Puertos USB";
            caja.transform.SetParent(poste, false);
            caja.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            caja.transform.localScale = new Vector3(0.26f, 0.14f, 0.26f);
            caja.GetComponent<Renderer>().sharedMaterial = oscuro;
            Object.DestroyImmediate(caja.GetComponent<Collider>());

            if (a.velas.Count == 0)
            {
                // Suelto: panel solar arriba, como el de la referencia.
                var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "Panel solar";
                panel.transform.SetParent(poste, false);
                panel.transform.localPosition = new Vector3(0f, arriba + 0.05f, 0f);
                panel.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
                panel.transform.localScale = new Vector3(0.9f, 0.04f, 0.6f);
                panel.GetComponent<Renderer>().sharedMaterial = oscuro;
                Object.DestroyImmediate(panel.GetComponent<Collider>());
                return;
            }
            // Brazos a cada aro, a la altura del aro, con su tornapunta.
            foreach (int v in a.velas)
            {
                var c = Centro(v);
                float h = Velas[v].h;
                var dir = (a.p - c).normalized;
                var enAro = new Vector3(c.x + dir.x * Radio(v), h, c.y + dir.y * Radio(v));
                var enPoste = new Vector3(p.x, h, p.z);
                Tubo($"Brazo a la vela {v + 1}", poste, enPoste, enAro, RadioUnion, cano);
                Tubo("Tornapunta", poste, enPoste + Vector3.down * LargoTornapunta, Vector3.Lerp(enPoste, enAro, 0.6f), RadioTornapunta, cano);
            }
        }

        /// <summary>Donde dos aros se cruzan sin palo, un caño vertical corto los une.</summary>
        static void Uniones(Transform padre, List<int> indices, List<Apoyo> palos, Material cano)
        {
            foreach (int a in indices)
            foreach (int b in indices)
            {
                if (b <= a || !Pisan(a, b)) continue;
                foreach (var p in Cruces(a, b))
                {
                    if (palos.Any(x => Vector2.Distance(x.p, p) < 0.3f)) continue;
                    float h0 = Mathf.Min(Velas[a].h, Velas[b].h), h1 = Mathf.Max(Velas[a].h, Velas[b].h);
                    Tubo($"Unión vela {a + 1} – vela {b + 1}", padre, new Vector3(p.x, h0 - RadioAro, p.y), new Vector3(p.x, h1 + RadioAro, p.y), RadioUnion, cano);
                }
            }
        }

        // ---------------- velas ----------------

        static void Vela(Transform padre, int i, Material cano, Material tela)
        {
            var v = Velas[i];
            float r = v.d / 2f;
            var go = new GameObject($"Vela {i + 1} · Ø {v.d:0.0} m · {v.h:0.00} m");
            go.transform.SetParent(padre, false);
            go.transform.position = new Vector3(v.x, v.h, v.z);

            var aro = Nuevo("Aro Ø60", go.transform, Guardar(MallaAro(r, RadioAro, 128, 10), $"Aro {i + 1}.asset"), cano);
            aro.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;

            var mallaTela = Guardar(MallaTela(r - MargenTela, Comba), $"Tela {i + 1}.asset");
            var lona = Nuevo("Tela PES 220 g/m²", go.transform, mallaTela, tela);
            lona.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            // Las telas transparentes no proyectan sombra en URP: la sombra la da una copia invisible y opaca.
            var sombra = Nuevo("Sombra de la tela", go.transform, mallaTela, cano);
            sombra.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;

            // Tensores: del borde de la tela al aro, cada 30 cm (un solo objeto por vela).
            int n = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * r / CadaTensor));
            var partes = new List<CombineInstance>();
            for (int k = 0; k < n; k++)
            {
                float t = k * Mathf.PI * 2f / n;
                var dir = new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t));
                partes.Add(new CombineInstance { mesh = cilindro, transform = MatrizTubo(dir * (r - MargenTela), dir * r, 0.006f) });
            }
            var tensores = new Mesh { name = $"Tensores {i + 1}" };
            tensores.CombineMeshes(partes.ToArray(), true, true);
            Nuevo("Tensores", go.transform, Guardar(tensores, $"Tensores {i + 1}.asset"), cano);
        }

        // ---------------- mallas y materiales ----------------

        static GameObject Nuevo(string nombre, Transform padre, Mesh malla, Material mat)
        {
            var go = new GameObject(nombre, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(padre, false);
            go.GetComponent<MeshFilter>().sharedMesh = malla;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Matrix4x4 MatrizTubo(Vector3 a, Vector3 b, float radio)
        {
            var d = b - a;
            return Matrix4x4.TRS(a, Quaternion.FromToRotation(Vector3.up, d.normalized), new Vector3(radio, d.magnitude, radio));
        }

        static GameObject Tubo(string nombre, Transform padre, Vector3 a, Vector3 b, float radio, Material mat)
        {
            var go = Nuevo(nombre, padre, cilindro, mat);
            var d = b - a;
            go.transform.SetPositionAndRotation(a, Quaternion.FromToRotation(Vector3.up, d.normalized));
            go.transform.localScale = new Vector3(radio, d.magnitude, radio);
            return go;
        }

        /// <summary>Cilindro de radio 1 y alto 1 (de y = 0 a y = 1), con tapas.</summary>
        static Mesh MallaCilindro(int lados)
        {
            var v = new List<Vector3>(); var nor = new List<Vector3>(); var tri = new List<int>();
            for (int i = 0; i <= lados; i++)
            {
                float t = i * Mathf.PI * 2f / lados;
                var d = new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t));
                v.Add(d); v.Add(d + Vector3.up); nor.Add(d); nor.Add(d);
            }
            for (int i = 0; i < lados; i++)
            {
                int k = i * 2;
                tri.AddRange(new[] { k, k + 1, k + 3, k, k + 3, k + 2 });
            }
            foreach (var y in new[] { 0f, 1f })
            {
                int c = v.Count;
                v.Add(new Vector3(0f, y, 0f)); nor.Add(y > 0 ? Vector3.up : Vector3.down);
                for (int i = 0; i <= lados; i++)
                {
                    float t = i * Mathf.PI * 2f / lados;
                    v.Add(new Vector3(Mathf.Cos(t), y, Mathf.Sin(t))); nor.Add(y > 0 ? Vector3.up : Vector3.down);
                }
                for (int i = 0; i < lados; i++)
                    if (y > 0) tri.AddRange(new[] { c, c + i + 2, c + i + 1 });
                    else tri.AddRange(new[] { c, c + i + 1, c + i + 2 });
            }
            var m = new Mesh { name = "Caño" };
            m.SetVertices(v); m.SetNormals(nor); m.SetTriangles(tri, 0); m.RecalculateBounds();
            return m;
        }

        /// <summary>Toro: el aro de la vela (radio mayor R, caño de radio r).</summary>
        static Mesh MallaAro(float R, float r, int segU, int segV)
        {
            var v = new List<Vector3>(); var nor = new List<Vector3>(); var tri = new List<int>();
            for (int i = 0; i <= segU; i++)
            {
                float u = i * Mathf.PI * 2f / segU;
                var c = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));
                for (int j = 0; j <= segV; j++)
                {
                    float w = j * Mathf.PI * 2f / segV;
                    var n = c * Mathf.Cos(w) + Vector3.up * Mathf.Sin(w);
                    v.Add(c * R + n * r); nor.Add(n);
                }
            }
            for (int i = 0; i < segU; i++)
            for (int j = 0; j < segV; j++)
            {
                int a = i * (segV + 1) + j, b = a + segV + 1;
                tri.AddRange(new[] { a, a + 1, b + 1, a, b + 1, b });
            }
            var m = new Mesh { name = "Aro" };
            m.SetVertices(v); m.SetNormals(nor); m.SetTriangles(tri, 0); m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// Tela tensada: disco con una comba suave hacia el centro, con caras arriba y abajo (se ve
        /// desde la fila y desde lejos). UV en metros para la trama.
        /// </summary>
        static Mesh MallaTela(float r, float comba)
        {
            const int anillos = 10, sectores = 72;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int cara = 0; cara < 2; cara++)
            {
                int k0 = v.Count;
                v.Add(new Vector3(0f, -comba + (cara == 0 ? 0.002f : 0f), 0f)); uv.Add(Vector2.zero);
                for (int a = 1; a <= anillos; a++)
                {
                    float f = (float)a / anillos, rr = r * f;
                    for (int s = 0; s < sectores; s++)
                    {
                        float t = s * Mathf.PI * 2f / sectores;
                        var p = new Vector3(Mathf.Cos(t) * rr, -comba * (1f - f * f) + (cara == 0 ? 0.002f : 0f), Mathf.Sin(t) * rr);
                        v.Add(p); uv.Add(new Vector2(p.x, p.z));
                    }
                }
                for (int s = 0; s < sectores; s++)
                {
                    int a = k0 + 1 + s, b = k0 + 1 + (s + 1) % sectores;
                    if (cara == 0) tri.AddRange(new[] { k0, b, a }); else tri.AddRange(new[] { k0, a, b });
                }
                for (int an = 1; an < anillos; an++)
                for (int s = 0; s < sectores; s++)
                {
                    int a = k0 + 1 + (an - 1) * sectores + s, b = k0 + 1 + (an - 1) * sectores + (s + 1) % sectores;
                    int c = a + sectores, d = b + sectores;
                    if (cara == 0) tri.AddRange(new[] { a, b, d, a, d, c }); else tri.AddRange(new[] { a, d, b, a, c, d });
                }
            }
            var m = new Mesh { name = "Tela" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static Material Lit(string nombre, string hex, float suavidad, float metal)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = nombre };
            ColorUtility.TryParseHtmlString(hex, out var c);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", suavidad);
            m.SetFloat("_Metallic", metal);
            return Guardar(m, nombre + ".mat");
        }

        /// <summary>Tela PES crema, semitraslúcida (~70 %), con la trama apenas visible.</summary>
        static Material Tela()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "Trama PES", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // Hilos de urdimbre y trama que se cruzan por arriba y por abajo.
                bool urdimbre = ((x / 4) + (y / 4)) % 2 == 0;
                float hilo = urdimbre ? Mathf.Abs(Mathf.Sin((y % 4 + 0.5f) / 4f * Mathf.PI)) : Mathf.Abs(Mathf.Sin((x % 4 + 0.5f) / 4f * Mathf.PI));
                byte g = (byte)(255 * (0.86f + 0.14f * hilo));
                byte a = (byte)(255 * Mathf.Lerp(0.8f, 1f, hilo));
                px[y * n + x] = new Color32(g, g, g, a);
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            Guardar(tex, "Trama PES.asset");

            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Tela vela PES" };
            var c = PaletaPost.Crema;
            c.a = OpacidadTela;
            m.SetColor("_BaseColor", c);
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", new Vector2(6f, 6f));   // 6 repeticiones de la trama por metro
            m.SetFloat("_Smoothness", 0.15f);
            m.SetFloat("_Metallic", 0f);
            // Transparente (mezcla alfa), sin escribir profundidad.
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return Guardar(m, "Tela vela PES.mat");
        }

        static T Guardar<T>(T asset, string archivo) where T : Object
        {
            var ruta = $"{Carpeta}/{archivo}";
            if (AssetDatabase.LoadAssetAtPath<Object>(ruta) != null) AssetDatabase.DeleteAsset(ruta);
            AssetDatabase.CreateAsset(asset, ruta);
            return asset;
        }
    }

    /// <summary>Al recompilar, si la escena del cubo no tiene las velas (o son viejas), las arma.</summary>
    [InitializeOnLoad]
    static class VelasAlDia
    {
        static VelasAlDia()
        {
            EditorApplication.delayCall += ConstruirVelas.ConstruirSiHaceFalta;
        }
    }
}
