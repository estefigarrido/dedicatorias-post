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
        const string Version = "Velas v6";   // subir si cambia la disposición
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
            (7.88f, 13.14f, true), (15.94f, 7.45f, true), (18.21f, -2.96f, true), (9.57f, 6.99f, true),
        };
        // Palos comunes (además de los cargadores) por grupo. Solo van en el borde, fuera de la explanada.
        const int PalosPorGrupo = 3;

        // Medidas de la estructura (m).
        const float RadioAro = 0.03f, RadioPalo = 0.05f, RadioUnion = 0.03f, RadioTornapunta = 0.025f;
        const float LadoBase = 1f, AltoBase = 0.45f, LargoTornapunta = 0.9f;
        const float MargenTela = 0.12f, CadaTensor = 0.3f, Comba = 0.12f;
        const float OpacidadTela = 0.72f;

        // Los palos comunes van solo sobre el pasto: nunca en la explanada ni sobre el murete negro.
        // Se mira el piso real de la escena con rayos. Con lugar, la base va dentro de un puf de tela
        // outdoor (Ø 1,5 m); si el pasto es angosto, dentro de una "mesita inflada" (Ø 0,8 m).
        const float RadioPuf = 0.8f, RadioMesita = 0.45f;
        static readonly string[] Pasto = { "Césped", "Pasto" };
        // Si el aro no pasa por arriba del pasto, el palo se corre hacia el pasto (hasta 1,5 m) y lo toma con un brazo.
        static readonly float[] Corrimientos = { 0f, 0.4f, 0.8f, 1.2f, 1.5f };

        static Transform raizActual;

        // Contra la pared ciega del ropero (entre las dos puertas) también puede ir un palo: su base
        // es un banco acolchado largo, para dos personas, pegado a la pared.
        static readonly Rect ContraElRopero = Rect.MinMaxRect(11.2f, -2.6f, 12.2f, 0f);
        // Donde no van beanbags sueltos: el stand, la fila, el paso de las puertas y la pantalla de respiración.
        static readonly Rect[] Paso =
        {
            Rect.MinMaxRect(-11.5f, -7f, 11.5f, 7f),
            Rect.MinMaxRect(10.75f, 0.6f, 17.6f, 2.8f),
            Rect.MinMaxRect(10.75f, 2.4f, 13f, 4.9f),
            Rect.MinMaxRect(10.75f, -5.6f, 13.5f, -3.5f),
        };
        static readonly List<Vector3> ocupados = new List<Vector3>();   // x, z, radio

        static bool EsMurete(string nombre) => nombre.StartsWith("Murete", System.StringComparison.Ordinal);

        /// <summary>¿El círculo es piso parejo, sin murete en el medio? (para cargadores y beanbags)</summary>
        static bool Libre(Vector2 p, float r)
        {
            var c = Piso(p);
            if (c == null || EsMurete(c.Value.nombre)) return false;
            for (int k = 0; k < 12; k++)
            {
                float t = k * Mathf.PI / 6f;
                var s = Piso(p + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * r);
                if (s == null || EsMurete(s.Value.nombre) || Mathf.Abs(s.Value.y - c.Value.y) > 0.12f) return false;
            }
            return !ocupados.Any(o => Vector2.Distance(new Vector2(o.x, o.y), p) < o.z + r);
        }

        /// <summary>Qué hay en el piso en (x, z): el objeto más alto y su altura (sin contar las velas).</summary>
        static (string nombre, float y)? Piso(Vector2 p)
        {
            var golpes = Physics.RaycastAll(new Vector3(p.x, 60f, p.y), Vector3.down, 120f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            RaycastHit? mejor = null;
            foreach (var g in golpes)
            {
                if (raizActual != null && g.collider.transform.IsChildOf(raizActual)) continue;
                if (mejor == null || g.point.y > mejor.Value.point.y) mejor = g;
            }
            if (mejor == null) return null;
            return (mejor.Value.collider.name, mejor.Value.point.y);
        }

        static bool EsPasto(Vector2 p)
        {
            var s = Piso(p);
            return s != null && Pasto.Any(n => s.Value.nombre.StartsWith(n, System.StringComparison.Ordinal));
        }

        /// <summary>¿Hay pasto en todo el círculo de radio r alrededor de p? (así la base no pisa el murete)</summary>
        static bool PastoAlrededor(Vector2 p, float r)
        {
            if (!EsPasto(p)) return false;
            for (int k = 0; k < 12; k++)
            {
                float t = k * Mathf.PI / 6f;
                if (!EsPasto(p + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * r)) return false;
            }
            return true;
        }

        /// <summary>La altura más baja del piso en el círculo: así la base apoya entera, sin quedar en el aire.</summary>
        static float AlturaPiso(Vector2 p, float r)
        {
            float y = Piso(p)?.y ?? 0f;
            for (int k = 0; k < 8; k++)
            {
                float t = k * Mathf.PI / 4f;
                var s = Piso(p + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * r);
                if (s != null) y = Mathf.Min(y, s.Value.y);
            }
            return y;
        }

        // Color de cada vela, como en el plano: verde (#49b867) o violeta (#ab8ae6) de la marca.
        static readonly bool[] VelaVerde = { true, false, false, false, true, true, false, false };

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
            fundas = 0;
            var matCano = Lit("Caño aluminio blanco", "#c8cbcc", 0.45f, 0.35f);
            var matGoma = Lit("Goma de la base", "#262626", 0.1f, 0f);
            trama = null;
            // Materiales de versiones anteriores que ya no se usan.
            foreach (var viejo in new[] { "Tela vela PES.mat", "Base banco.mat", "Placas de contrapeso.mat" })
                if (AssetDatabase.LoadAssetAtPath<Object>($"{Carpeta}/{viejo}") != null) AssetDatabase.DeleteAsset($"{Carpeta}/{viejo}");
            var matVerde = Tela(PaletaPost.Verde, "Tela vela verde");
            var matVioleta = Tela(PaletaPost.NotaVioleta, "Tela vela violeta");
            var matPuf = TelaOutdoor();
            Physics.SyncTransforms();

            var raiz = new GameObject(NombreRaiz).transform;
            raizActual = raiz;
            new GameObject(Version).transform.SetParent(raiz, false);
            var informe = new List<string>();

            // Postes de carga: los que sostienen toman los aros que tienen a menos de 80 cm.
            var postes = new GameObject("Postes de carga").transform;
            postes.SetParent(raiz, false);
            var deCargadores = new List<Apoyo>();
            var matSolar = Lit("Panel solar", "#1c2433", 0.75f, 0.2f);
            ocupados.Clear();
            foreach (var c in Cargadores)
            {
                var original = new Vector2(c.x, c.z);
                var ap = new Apoyo { p = original };
                // Toma los aros que pasan a menos de 80 cm del lugar del plano.
                if (c.sostiene)
                    for (int i = 0; i < Velas.Length; i++)
                        if (Mathf.Abs(Vector2.Distance(original, Centro(i)) - Radio(i)) < 0.8f) ap.velas.Add(i);
                ap.alto = ap.velas.Count > 0 ? ap.velas.Max(i => Velas[i].h) : 0f;
                // Si la funda choca con el murete, el cargador se corre lo mínimo hacia un piso parejo,
                // sin meterse debajo de ninguna vela (el poste las pasa por al lado).
                ap.p = LugarLibre(original, 0.7f, 2.5f) ?? original;
                ap.suelo = AlturaPiso(ap.p, 0.6f);
                ocupados.Add(new Vector3(ap.p.x, ap.p.y, 0.7f));
                Cargador(postes, ap, matCano, matPuf, matGoma, matSolar);
                deCargadores.Add(ap);
            }

            for (int g = 0; g < 2; g++)
            {
                var grupo = new GameObject(g == 0 ? "Grupo norte" : "Grupo este (fila)").transform;
                grupo.SetParent(raiz, false);
                var indices = Enumerable.Range(0, Velas.Length).Where(i => Velas[i].grupo == g).ToList();

                foreach (int i in indices) Vela(grupo, i, matCano, VelaVerde[i] ? matVerde : matVioleta);
                var palos = ElegirPalos(indices, deCargadores);
                foreach (var p in palos)
                {
                    Palo(grupo, p, matCano, matPuf);
                    ocupados.Add(new Vector3(p.p.x, p.p.y, p.banco ? 0.9f : p.mesita ? 0.45f : 0.8f));
                }
                Uniones(grupo, indices, palos.Concat(deCargadores).ToList(), matCano);

                var apoyos = indices.ToDictionary(i => i, i => palos.Concat(deCargadores).Count(p => p.velas.Contains(i)));
                informe.Add($"{grupo.name}: {palos.Count} palos + cargadores; apoyos por vela: {string.Join(", ", apoyos.Values)}");
            }
            // Zonas de estar: dos beanbags junto a cada cargador, mirando a la mesa de carga.
            var estar = new GameObject("Beanbags (zonas de estar)").transform;
            estar.SetParent(raiz, false);
            int beanbags = 0;
            foreach (var ap in deCargadores)
            {
                var puestos = new List<Vector2>();
                for (int n = 0; n < 2; n++)
                {
                    Vector2? mejor = null;
                    float puntaje = float.MinValue;
                    for (int k = 0; k < 24; k++)
                    foreach (var d in new[] { 1.4f, 1.8f })
                    {
                        float t = k * Mathf.PI / 12f;
                        var q = ap.p + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * d;
                        if (Paso.Any(r => r.Contains(q)) || !Libre(q, 0.6f)) continue;
                        float s = -d + (puestos.Count > 0 ? Mathf.Min(Vector2.Distance(q, puestos[0]), 2f) : 0f);
                        if (s > puntaje) { puntaje = s; mejor = q; }
                    }
                    if (mejor == null) break;
                    puestos.Add(mejor.Value);
                    ocupados.Add(new Vector3(mejor.Value.x, mejor.Value.y, 0.65f));
                    Beanbag(estar, mejor.Value, ap.p, matPuf);
                    beanbags++;
                }
            }
            informe.Add($"{beanbags} beanbags");
            Debug.Log("[post.] Velas de sombra armadas. " + string.Join(" · ", informe));
            return raiz.gameObject;
        }

        // ---------------- palos ----------------

        class Apoyo
        {
            public Vector2 p;
            public List<int> velas = new List<int>();
            public float alto;
            public float suelo;      // altura del piso donde apoya
            public bool mesita;      // poco lugar: mesita inflada en vez de puf
            public float brazo;      // distancia del palo al aro (0 = el palo toca el aro)
            public bool banco;       // contra la pared del ropero: banco acolchado para dos
        }

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
            if (Cargadores.Any(c => Vector2.Distance(new Vector2(c.x, c.z), a.p) < 2f)) return false;
            // El palo no puede atravesar la tela de una vela más baja que no sostiene.
            for (int i = 0; i < Velas.Length; i++)
            {
                if (a.velas.Contains(i) || Velas[i].h >= a.alto - 0.01f) continue;
                if (Vector2.Distance(a.p, Centro(i)) < Radio(i) + 0.15f) return false;
            }
            if (ContraElRopero.Contains(a.p))
            {
                a.banco = true;
                a.suelo = AlturaPiso(a.p, 0.5f);
                return true;
            }
            // Solo sobre pasto, con la base entera adentro del pasto (sin tocar el murete).
            if (PastoAlrededor(a.p, RadioPuf)) a.mesita = false;
            else if (PastoAlrededor(a.p, RadioMesita)) a.mesita = true;
            else return false;
            a.suelo = AlturaPiso(a.p, a.mesita ? RadioMesita : RadioPuf);
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
                for (int k = 0; k < 72; k++)
                {
                    float ang = k * Mathf.PI * 2f / 72f;
                    var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    foreach (var corrido in Corrimientos)
                        candidatos.Add(new Apoyo { p = Centro(a) + dir * (Radio(a) + corrido), velas = { a }, alto = Velas[a].h, brazo = corrido });
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
                    // Mejor sin brazo y con puf (más estable); los cruces sostienen dos velas.
                    float s = falta * 10f + Mathf.Min(cerca, 6f) + (c.velas.Count - 1) * 3f - c.brazo * 2f - (c.mesita ? 1f : 0f);
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

        static void Palo(Transform padre, Apoyo a, Material cano, Material puf)
        {
            var palo = new GameObject(a.mesita ? "Palo (mesita inflada)" : "Palo (puf)").transform;
            palo.SetParent(padre, false);
            var pie = new Vector3(a.p.x, a.suelo, a.p.y);
            palo.position = pie;

            // Contrapeso (bloques de hormigón o placas de acero) tapado por una funda de tela outdoor
            // para que nadie se golpee: un puf donde hay lugar, una mesita inflada donde no.
            // Asiento redondo tapizado con respaldo cónico en el centro (el palo sale por arriba),
            // montado sobre el contrapeso: se sientan alrededor, de espaldas al palo.
            if (a.banco)
                Funda("Asiento redondo para dos o más (bloques de hormigón adentro)", palo, pie, MallaAsientoRedondo(0.8f, 0.45f, 0.4f, 0.24f, 0.42f), puf, 0.8f, 0.45f);
            else if (a.mesita)
                Funda("Asiento redondo chico (contrapeso de acero adentro)", palo, pie, MallaAsientoRedondo(0.5f, 0.45f, 0.26f, 0.15f, 0.32f), puf, 0.5f, 0.45f);
            else
                Funda("Asiento redondo (bloques de hormigón adentro)", palo, pie, MallaAsientoRedondo(0.85f, 0.45f, 0.42f, 0.25f, 0.45f), puf, 0.85f, 0.45f);

            var arriba = a.alto + RadioAro;
            var fuste = Tubo("Palo Ø100", palo, pie + Vector3.up * 0.3f, new Vector3(a.p.x, arriba, a.p.y), RadioPalo, cano);
            var choque = fuste.AddComponent<CapsuleCollider>();
            choque.direction = 1;
            choque.radius = 1f;
            choque.height = 1f;
            choque.center = new Vector3(0f, 0.5f, 0f);

            // Si el palo quedó corrido hacia el pasto, toma el aro con un brazo horizontal.
            if (a.brazo > 0.05f)
            {
                int v = a.velas[0];
                var c = Centro(v);
                float h = Velas[v].h;
                var dir = (a.p - c).normalized;
                var enAro = new Vector3(c.x + dir.x * Radio(v), h, c.y + dir.y * Radio(v));
                var enPalo = new Vector3(a.p.x, h, a.p.y);
                Tubo($"Brazo a la vela {v + 1}", palo, enPalo, enAro, RadioUnion, cano);
                Tubo("Tornapunta", palo, enPalo + Vector3.down * LargoTornapunta, Vector3.Lerp(enPalo, enAro, 0.7f), RadioTornapunta, cano);
                return;
            }

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
        static void Cargador(Transform padre, Apoyo a, Material cano, Material puf, Material oscuro, Material solar)
        {
            var poste = new GameObject(a.velas.Count > 0 ? "Poste de carga (sostiene las velas)" : "Poste de carga").transform;
            poste.SetParent(padre, false);
            var p = new Vector3(a.p.x, a.suelo, a.p.y);
            poste.position = p;

            // Contrapeso de placas de acero (Ø 1,2 m, 12 cm) tapado por una funda baja acolchada,
            // del mismo material que los pufs: no se ve el metal y no lastima a nadie.
            Funda("Funda acolchada (placas de acero adentro)", poste, p, MallaFunda(0.13f, 0.66f, 0.2f, 2.2f, true), puf, 0.66f, 0.2f);

            // El poste sube por encima de todas las velas: arriba va el panel solar, al sol.
            float techo = Velas.Max(v => v.h);
            float arriba = techo + 0.45f - a.suelo;   // medido desde el piso donde apoya
            var fuste = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fuste.name = "Poste 20 × 20";
            fuste.transform.SetParent(poste, false);
            fuste.transform.localPosition = new Vector3(0f, 0.12f + (arriba - 0.12f) / 2f, 0f);
            fuste.transform.localScale = new Vector3(0.2f, arriba - 0.12f, 0.2f);
            fuste.GetComponent<Renderer>().sharedMaterial = cano;

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel solar (sobre las velas)";
            panel.transform.SetParent(poste, false);
            panel.transform.localPosition = new Vector3(0f, arriba + 0.06f, 0f);
            panel.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);   // inclinado hacia el norte (el sol en Buenos Aires)
            panel.transform.localScale = new Vector3(1.2f, 0.04f, 0.8f);
            panel.GetComponent<Renderer>().sharedMaterial = solar;
            Object.DestroyImmediate(panel.GetComponent<Collider>());

            // Estación de carga abajo, cerca del piso: mesa redonda y caja de puertos USB.
            Tubo("Mesa Ø80", poste, p + Vector3.up * 0.8f, p + Vector3.up * 0.83f, 0.4f, cano);
            var caja = GameObject.CreatePrimitive(PrimitiveType.Cube);
            caja.name = "Puertos USB";
            caja.transform.SetParent(poste, false);
            caja.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            caja.transform.localScale = new Vector3(0.26f, 0.14f, 0.26f);
            caja.GetComponent<Renderer>().sharedMaterial = oscuro;
            Object.DestroyImmediate(caja.GetComponent<Collider>());
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

        /// <summary>El lugar libre más cercano (círculo de radio r, piso parejo, fuera de las velas), hasta "hasta" metros.</summary>
        static Vector2? LugarLibre(Vector2 desde, float r, float hasta)
        {
            bool BajoVela(Vector2 q) => Enumerable.Range(0, Velas.Length).Any(i => Vector2.Distance(q, Centro(i)) < Radio(i) + 0.15f);
            if (Libre(desde, r) && !BajoVela(desde)) return desde;
            for (float d = 0.2f; d <= hasta; d += 0.2f)
                for (int k = 0; k < 24; k++)
                {
                    float t = k * Mathf.PI / 12f;
                    var q = desde + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * d;
                    if (Libre(q, r) && !BajoVela(q)) return q;
                }
            return null;
        }

        /// <summary>Beanbag outdoor (como los de la referencia): asiento bajo y respaldo, mirando hacia "mira".</summary>
        static void Beanbag(Transform padre, Vector2 p, Vector2 mira, Material mat)
        {
            var pie = new Vector3(p.x, AlturaPiso(p, 0.55f), p.y);
            var hacia = mira - p;
            var bb = Funda("Beanbag", padre, pie, MallaBeanbag(), mat, 0.6f, 0.8f);
            bb.transform.rotation = Quaternion.LookRotation(new Vector3(hacia.x, 0f, hacia.y));
        }

        /// <summary>
        /// Beanbag orgánico (referencia: "Puff" de 3D Warehouse): una gota apoyada, con la base que se
        /// ensancha y se aplasta contra el piso, el respaldo más alto atrás y el asiento hundido
        /// adelante. Malla densa (64 × 48) con normales suaves, sin costuras. +Z = hacia adelante.
        /// </summary>
        static Mesh MallaBeanbag()
        {
            const int U = 64, V = 48;
            const float rx = 0.58f, rz = 0.62f, alto = 0.78f;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int j = 0; j <= V; j++)
            {
                float lat = -Mathf.PI / 2f + Mathf.PI * j / V;   // de abajo (-90°) a arriba (90°)
                float cy = Mathf.Cos(lat), sy = Mathf.Sin(lat);
                for (int i = 0; i <= U; i++)
                {
                    float lon = Mathf.PI * 2f * i / U;
                    var d = new Vector3(cy * Mathf.Sin(lon), sy, cy * Mathf.Cos(lon));
                    // Alto según la dirección: atrás (-z) sube el respaldo, adelante (+z) queda bajo.
                    float atras = Mathf.InverseLerp(1f, -1f, d.z);
                    // La parte de arriba es más chata (potencia < 1) para que se lea como sillón y no como huevo.
                    float h = sy > 0f ? Mathf.Pow(sy, 0.6f) * alto * Mathf.Lerp(0.38f, 1f, Mathf.SmoothStep(0f, 1f, atras)) : sy * 0.12f;
                    // Más ancho abajo (el relleno cae y se apoya), algo más angosto arriba.
                    float ancho = 1f + 0.16f * Mathf.Clamp01(1f - (sy + 0.3f)) - 0.1f * Mathf.Clamp01(sy);
                    var q = new Vector3(d.x * rx * ancho, h, d.z * rz * ancho);
                    // Asiento hundido: un pozo suave adelante del centro, solo en la parte de arriba.
                    if (sy > 0f)
                    {
                        // Pozo ancho y bien marcado, con el borde redondeado alrededor (como el de la referencia).
                        float dx = q.x / 0.4f, dz = (q.z - 0.1f) / 0.42f;
                        float pozo = Mathf.Exp(-Mathf.Pow(dx * dx + dz * dz, 2f));
                        q.y -= 0.36f * pozo * Mathf.SmoothStep(0f, 1f, sy * 1.4f);
                        // Respaldo apenas inclinado hacia atrás.
                        q.z -= 0.08f * sy * atras;
                    }
                    v.Add(q + Vector3.up * 0.12f);
                    uv.Add(new Vector2((float)i / U * 3.5f, (float)j / V * 2f));
                }
            }
            for (int j = 0; j < V; j++)
            for (int i = 0; i < U; i++)
            {
                int a = j * (U + 1) + i, b = a + U + 1;
                tri.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
            }
            var m = new Mesh { name = "Beanbag" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            // Sin costura: los vértices repetidos del borde (lon 0 y 360°) comparten normal.
            var normales = m.normals;
            for (int j = 0; j <= V; j++)
            {
                int a = j * (U + 1), b = a + U;
                var n = (normales[a] + normales[b]).normalized;
                normales[a] = normales[b] = n;
            }
            // Polos (centro del asiento y de la base): todos los vértices coinciden; normal limpia.
            for (int i = 0; i <= U; i++)
            {
                normales[i] = Vector3.down;
                normales[V * (U + 1) + i] = Vector3.up;
            }
            m.normals = normales;
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// Asiento redondo tapizado (referencia: banco circular con respaldo cónico alrededor de una
        /// columna): cilindro de radio R y alto H con el canto redondeado y, en el centro, un respaldo
        /// cónico de rBase a rTope y altoRespaldo, con el hueco del palo arriba.
        /// </summary>
        static Mesh MallaAsientoRedondo(float R, float H, float rBase, float rTope, float altoRespaldo)
        {
            const float canto = 0.07f;
            var perfil = new List<Vector2> { new Vector2(0f, 0f), new Vector2(R - 0.02f, 0f) };
            // Canto del asiento (abajo apenas, arriba bien redondeado).
            for (int k = 0; k <= 6; k++)
            {
                float t = -Mathf.PI / 2f + Mathf.PI / 2f * k / 6f;
                perfil.Add(new Vector2(R - 0.02f + 0.02f * Mathf.Cos(t), 0.02f + 0.02f * Mathf.Sin(t)));
            }
            for (int k = 0; k <= 10; k++)
            {
                float t = Mathf.PI / 2f * k / 10f;
                perfil.Add(new Vector2(R - canto + canto * Mathf.Cos(t), H - canto + canto * Mathf.Sin(t)));
            }
            // Almohadón del asiento: apenas abombado hacia el centro, y la unión con el respaldo.
            for (int k = 1; k <= 6; k++)
            {
                float f = k / 6f;
                perfil.Add(new Vector2(Mathf.Lerp(R - canto, rBase + 0.03f, f), H + 0.015f * Mathf.Sin(f * Mathf.PI)));
            }
            // Respaldo cónico, con el borde de arriba redondeado.
            const float cantoArriba = 0.05f;
            perfil.Add(new Vector2(rBase, H + 0.03f));
            perfil.Add(new Vector2(rTope + cantoArriba, H + altoRespaldo - cantoArriba));
            for (int k = 1; k <= 8; k++)
            {
                float t = Mathf.PI / 2f * k / 8f;
                perfil.Add(new Vector2(rTope + cantoArriba * Mathf.Cos(t), H + altoRespaldo - cantoArriba + cantoArriba * Mathf.Sin(t)));
            }
            perfil.Add(new Vector2(0.07f, H + altoRespaldo));   // hueco del palo
            return Revolucion(perfil, 72, "Asiento redondo");
        }

        /// <summary>Malla de revolución alrededor del eje Y (perfil de abajo hacia afuera y arriba).</summary>
        static Mesh Revolucion(List<Vector2> perfil, int sectores, string nombre)
        {
            int K = perfil.Count;
            // La textura sigue el largo real del perfil (en metros), así la tela no se estira en vetas.
            var largo = new float[K];
            for (int k = 1; k < K; k++) largo[k] = largo[k - 1] + Vector2.Distance(perfil[k - 1], perfil[k]);
            float vuelta = 2f * Mathf.PI * perfil.Max(p => p.x);
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i <= sectores; i++)
            {
                float t = i * Mathf.PI * 2f / sectores;
                for (int k = 0; k < K; k++)
                {
                    v.Add(new Vector3(perfil[k].x * Mathf.Cos(t), perfil[k].y, perfil[k].x * Mathf.Sin(t)));
                    uv.Add(new Vector2((float)i / sectores * vuelta, largo[k]));
                }
            }
            for (int i = 0; i < sectores; i++)
            for (int k = 0; k < K - 1; k++)
            {
                int a0 = i * K + k, b0 = a0 + 1, a1 = a0 + K, b1 = a1 + 1;
                tri.AddRange(new[] { a0, b0, a1, b0, b1, a1 });
            }
            var m = new Mesh { name = nombre };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            var normales = m.normals;
            for (int k = 0; k < K; k++)
            {
                int a = k, b = sectores * K + k;
                var n = (normales[a] + normales[b]).normalized;
                normales[a] = normales[b] = n;
            }
            m.normals = normales;
            m.RecalculateBounds();
            return m;
        }

        // ---------------- fundas de tela outdoor ----------------

        static int fundas;

        static GameObject Funda(string nombre, Transform padre, Vector3 pie, Mesh malla, Material mat, float radio, float alto)
        {
            var go = Nuevo(nombre, padre, Guardar(malla, $"Funda {++fundas}.asset"), mat);
            go.transform.position = pie;
            var caja = go.AddComponent<BoxCollider>();   // se puede sentar, apoyar o saltar encima
            caja.center = new Vector3(0f, alto / 2f, 0f);
            caja.size = new Vector3(radio * 1.6f, alto, radio * 1.6f);
            return go;
        }

        /// <summary>
        /// Funda inflada, de revolución, con perfil de superelipse (n más alto = más "cuadrada").
        /// anillo = false: puf redondeado (el palo sale por arriba). anillo = true: rosca con un hueco
        /// de radio ri para el palo y tapa casi plana (mesita inflada o funda baja del contrapeso).
        /// </summary>
        static Mesh MallaFunda(float ri, float ro, float alto, float n, bool anillo)
        {
            const int sectores = 48;
            var perfil = new List<Vector2>();
            float Pot(float v) => Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), 2f / n);
            if (!anillo)
                for (int k = 0; k <= 32; k++)
                {
                    float f = -Mathf.PI / 2f + Mathf.PI * k / 32f;
                    perfil.Add(new Vector2(ro * Mathf.Abs(Pot(Mathf.Cos(f))), alto / 2f + alto / 2f * Pot(Mathf.Sin(f))));
                }
            else
            {
                float rc = (ri + ro) / 2f, a = (ro - ri) / 2f;
                for (int k = 0; k <= 48; k++)
                {
                    float f = -Mathf.PI / 2f + 2f * Mathf.PI * k / 48f;
                    perfil.Add(new Vector2(rc + a * Pot(Mathf.Cos(f)), alto / 2f + alto / 2f * Pot(Mathf.Sin(f))));
                }
            }
            int K = perfil.Count;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i <= sectores; i++)
            {
                float t = i * Mathf.PI * 2f / sectores;
                for (int k = 0; k < K; k++)
                {
                    v.Add(new Vector3(perfil[k].x * Mathf.Cos(t), perfil[k].y, perfil[k].x * Mathf.Sin(t)));
                    uv.Add(new Vector2((float)i / sectores * 6f, (float)k / (K - 1) * 2f));
                }
            }
            for (int i = 0; i < sectores; i++)
            for (int k = 0; k < K - 1; k++)
            {
                int a0 = i * K + k, b0 = a0 + 1, a1 = a0 + K, b1 = a1 + 1;
                tri.AddRange(new[] { a0, b0, a1, b0, b1, a1 });
            }
            var m = new Mesh { name = "Funda" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>Tela outdoor tipo beanbag: verde salvia, mate, con textura de bouclé.</summary>
        static Material TelaOutdoor()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "Bouclé outdoor", wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float ruido = Mathf.PerlinNoise(x * 0.35f, y * 0.35f) * 0.6f + Mathf.PerlinNoise(x * 1.3f + 7f, y * 1.3f) * 0.4f;
                byte g = (byte)(255 * (0.82f + 0.18f * ruido));
                px[y * n + x] = new Color32(g, g, g, 255);
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            Guardar(tex, "Bouclé outdoor.asset");
            var m = Lit("Tela outdoor beanbag", "#49b867", 0.08f, 0f);   // verde de la marca
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
            EditorUtility.SetDirty(m);
            return m;
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

        static Texture2D trama;

        /// <summary>Tela PES del color de la marca, semitraslúcida (~70 %), con la trama apenas visible.</summary>
        static Material Tela(Color color, string nombre)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = nombre };
            var c = color;
            c.a = OpacidadTela;
            m.SetColor("_BaseColor", c);
            m.SetTexture("_BaseMap", Trama());
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
            return Guardar(m, nombre + ".mat");
        }

        static Texture2D Trama()
        {
            if (trama != null) return trama;
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
            return trama = Guardar(tex, "Trama PES.asset");
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
