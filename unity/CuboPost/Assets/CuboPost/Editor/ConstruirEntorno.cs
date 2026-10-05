using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CuboPost.EditorTools
{
    /// <summary>
    /// Entorno del stand: la Plaza de la República (lado norte del Obelisco, Buenos Aires), armada
    /// sobre la foto satelital que está en el Figma, a la misma escala que los planos del stand.
    /// Ejes: +X = este, +Z = norte. El stand está en el origen.
    ///   · Explanada de adoquines en forma de arco, con sus bandas claras y la rejilla de desagüe.
    ///   · El anillo: murete de piedra de 50 cm que la rodea. Atrás, el césped arranca al ras del
    ///     murete, sube apenas y baja hasta los senderos.
    ///   · Senderos de polvo de ladrillo y de hormigón, canteros, arbustos, el cartel BA y el mástil.
    ///   · Veredas, cordones, bolardos, cestos, las dos torres de reflectores y los semáforos.
    ///   · Alrededor: Av. 9 de Julio, el Metrobús, Cerrito, Carlos Pellegrini, Corrientes, el Obelisco
    ///     en su isla, edificios y tránsito.
    /// Los trazados están en EntornoDatos.cs; la geometría, en EntornoGeometria.cs; las texturas, en
    /// EntornoTexturas.cs.
    /// </summary>
    public static partial class ConstruirEntorno
    {
        const string Carpeta = "Assets/CuboPost/Generado/Entorno";
        const string NombreRaiz = "Entorno (Plaza de la República)";

        /// <summary>
        /// Versión del entorno. Queda guardada en la escena (un objeto vacío dentro del entorno): si
        /// no coincide con la de este archivo, la plaza se rearma sola cuando Unity recompila.
        /// Subir el número cada vez que se cambie algo de la plaza.
        /// </summary>
        internal const string Version = "Entorno v2 · plaza real";

        // Niveles (metros). El piso de la explanada es el cero, igual que el del stand.
        const float YAsfalto = -0.12f, YBase = -0.06f, YJardin = -0.035f, YSendero = -0.022f, YVereda = -0.015f, YTerracota = 0f, YBordeCesped = 0.06f;
        // Lo pintado o apoyado sobre otra superficie va apenas más arriba, para que no se mezclen al dibujarse.
        const float YPintura = YAsfalto + 0.01f;
        // El anillo.
        const float AnilloAlto = 0.5f, AnilloAncho = 0.54f;
        // Giro de la plaza respecto del norte de la foto (grados, antihorario): las bandas del piso siguen ese eje.
        const float GiroPlaza = 2.6f;

        // El Obelisco: sobre el eje de la plaza, cruzando Corrientes hacia el sur.
        static readonly Vector2 Obelisco = new Vector2(4f, -44.5f);
        public static Vector3 PosicionObelisco => new Vector3(Obelisco.x, 0f, Obelisco.y);
        const float RadioIsla = 9.5f;

        // Avenida: de la línea de edificios de Cerrito a la de Carlos Pellegrini hay unos 140 m.
        const float FachadaOeste = -68f, FachadaEste = 72.5f;
        const float AsfaltoOeste = -63f, AsfaltoEste = 67.5f;
        // Calles que cruzan (de z a z): Sarmiento, Corrientes y Lavalle.
        static readonly float[] Cruces = { -175f, -163f, -57.5f, -31.5f, 76f, 86f };

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static readonly HashSet<string> creados = new HashSet<string>();   // archivos generados en esta pasada
        static readonly HashSet<Object> creadosAhora = new HashSet<Object>();   // y su contenido
        // Qué materiales llevan textura y cuáles además mapa de normales: de eso depende qué datos
        // se guardan en cada malla (las mallas van dentro de la escena y conviene que pesen poco).
        static readonly HashSet<Material> conTextura = new HashSet<Material>();
        static readonly HashSet<Material> conRelieve = new HashSet<Material>();
        static Mesh cubo, cilindro, cono, esfera, tronco8, tubo10, autoCarroceria, autoVidrios;
        static readonly List<Mesh> icosferas = new List<Mesh>();      // masas de follaje detalladas
        static readonly List<Mesh> matasSimples = new List<Mesh>();   // las mismas con menos caras (plantas chicas y árboles lejanos)
        static Transform raiz;

        // Trazados ya suavizados (ver Trazar).
        static List<Vector2> anillo, explanada, cordon, cespedAnilloAdentro, cespedAnilloAfuera, caminoOeste, caminoEste, bordeSur;
        static Vector2 ejePlaza;   // dirección "norte" de la plaza
        static List<Vector2> isletaNordeste;

        // Paleta de las fachadas, tomada de las fotos de Street View (tonos cálidos y apagados).
        static readonly string[] Paredes = { "#e7dfcf", "#d9cdb4", "#cfc5b3", "#e3d6bd", "#c9c1b3", "#d8c8a8", "#ece6da", "#bdb5a8", "#d4b996", "#c8b8a0" };

        // =====================================================================
        //  Entrada
        // =====================================================================

        public static void Construir(Transform padre)
        {
            if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/CuboPost/Generado", "Entorno");
            creados.Clear();
            creadosAhora.Clear();
            texturas.Clear();
            mats.Clear();
            conTextura.Clear();
            conRelieve.Clear();
            icosferas.Clear();
            matasSimples.Clear();
            cortesForzados = 0;
            Random.InitState(20261005);
            cubo = MallaPrimitiva(PrimitiveType.Cube);
            cilindro = Guardar(CilindroBajo(12, true), "Cilindro12.asset");
            cono = Guardar(ConoBajo(8), "Cono8.asset");
            tronco8 = Guardar(CilindroBajo(8, true), "Tronco8.asset");
            tubo10 = Guardar(CilindroBajo(10, true), "Tubo10.asset");
            esfera = Guardar(EsferaLisa(), "Esfera lisa.asset");
            MallasDeAuto();
            for (int i = 0; i < 5; i++) icosferas.Add(Guardar(Mata(i * 17 + 3, 2), $"Mata{i}.asset"));
            for (int i = 0; i < 4; i++) matasSimples.Add(Guardar(Mata(i * 23 + 5, 1), $"Mata simple{i}.asset"));

            raiz = new GameObject(NombreRaiz).transform;
            raiz.SetParent(padre, false);

            Trazar();
            Calles();
            Plaza();
            Explanada();
            Anillo();
            Jardines();
            Mobiliario();
            Gente();
            MastilYCartel();
            Edificios();
            Transito();
            if (cortesForzados > 0) Debug.LogWarning("[post.] Plaza: hubo " + cortesForzados + " polígonos con cruces al triangular.");

            // Recién ahora, con todo armado: se borra lo que quedó de versiones anteriores en la
            // carpeta y se deja la marca de versión (un objeto vacío). Si algo falla antes, la marca
            // no queda y la plaza se vuelve a armar en la próxima recompilación.
            foreach (var guid in AssetDatabase.FindAssets("t:Object", new[] { Carpeta }))
            {
                var ruta = AssetDatabase.GUIDToAssetPath(guid);
                if (creados.Contains(ruta)) continue;
                // Doble control: nunca se borra un archivo cuyo contenido se creó en esta pasada.
                var contenido = AssetDatabase.LoadMainAssetAtPath(ruta);
                if (contenido != null && creadosAhora.Contains(contenido)) continue;
                AssetDatabase.DeleteAsset(ruta);
            }
            new GameObject(Version).transform.SetParent(raiz, false);
        }

        /// <summary>¿El entorno que está bajo <paramref name="contexto"/> es el de esta versión y quedó completo?</summary>
        internal static bool AlDia(Transform contexto)
        {
            var entorno = contexto.Find(NombreRaiz);
            return entorno != null && entorno.Find(Version) != null;
        }

        /// <summary>Piso del área relevada (32 × 15 m en los planos): el rectángulo, recortado contra la explanada.</summary>
        public static GameObject PisoRelevado(Transform padre, Rect area, Material material, string nombre)
        {
            if (explanada == null) Trazar();
            var poli = RecortarRect(explanada, area);
            var m = new MallaB();
            if (poli.Count >= 3) Superficie(m, poli, q => 0.014f, 100f, 1f);
            var go = m.Crear(nombre, padre, material, false);
            return go;
        }

        // =====================================================================
        //  Trazados
        // =====================================================================

        static void Trazar()
        {
            float g = GiroPlaza * Mathf.Deg2Rad;
            ejePlaza = new Vector2(-Mathf.Sin(g), Mathf.Cos(g));

            anillo = SuavizarPorTramos(Puntos(dAnillo), dAnilloEsquinas, 0.4f);

            // Explanada: por el eje del murete (el piso sigue por debajo) y después el borde sur.
            // El primer y el último punto del anillo son las puntas de los retornos: no entran.
            explanada = new List<Vector2>();
            var crudo = Puntos(dAnillo);
            Vector2 esquinaO = crudo[dAnilloEsquinas[0]], esquinaE = crudo[dAnilloEsquinas[dAnilloEsquinas.Length - 1]];
            bool adentro = false;
            foreach (var q in anillo)
            {
                if (!adentro && Vector2.Distance(q, esquinaO) < 0.01f) adentro = true;
                if (adentro) explanada.Add(q);
                if (adentro && Vector2.Distance(q, esquinaE) < 0.01f) break;
            }
            explanada.AddRange(Puntos(dExplanadaSur));

            cordon = Suavizar(Puntos(dCordon), true, 1.2f);

            // Césped del anillo: por adentro, el lomo del murete; por afuera, el borde trazado.
            cespedAnilloAdentro = Desplazar(anillo, false, AnilloAncho / 2f - 0.01f);
            // (el murete se recorre de oeste a este pasando por el norte: el césped queda a su izquierda)
            // Las dos curvas arrancan y terminan en el mismo punto (las puntas de los retornos del murete).
            var afuera = Puntos(dCespedAnillo);
            afuera[0] = cespedAnilloAdentro[0];
            afuera[afuera.Count - 1] = cespedAnilloAdentro[cespedAnilloAdentro.Count - 1];
            cespedAnilloAfuera = Suavizar(afuera, false, 0.6f);

            // Calzadas: el cordón oeste y el cordón este, de sur a norte, prolongados derecho.
            var c = Puntos(dCordon);
            caminoOeste = new List<Vector2> { new Vector2(c[20].x, -260f), new Vector2(c[20].x, -40f) };
            for (int i = 20; i >= 0; i--) caminoOeste.Add(c[i]);
            caminoOeste.Add(new Vector2(c[0].x, 95f));
            caminoOeste.Add(new Vector2(c[0].x, 310f));
            caminoOeste = Suavizar(caminoOeste, false, 2.5f);

            caminoEste = new List<Vector2> { new Vector2(c[47].x + 0.4f, -260f), new Vector2(c[47].x + 0.4f, -40f) };
            for (int i = 47; i <= 65; i++) caminoEste.Add(c[i]);
            caminoEste.Add(new Vector2(-8.5f, 72f));
            caminoEste.Add(new Vector2(-12.6f, 92f));
            caminoEste.Add(new Vector2(-13f, 310f));
            caminoEste = Suavizar(caminoEste, false, 2.5f);

            bordeSur = new List<Vector2>();
            for (int i = 25; i <= 42; i++) bordeSur.Add(c[i]);
            bordeSur = Suavizar(bordeSur, false, 1.5f);
        }

        /// <summary>Espejo respecto del Obelisco (para la mitad sur de la plaza).</summary>
        static List<Vector2> AlSur(IList<Vector2> p)
        {
            var r = new List<Vector2>(p.Count);
            foreach (var q in p) r.Add(new Vector2(q.x, 2f * Obelisco.y - q.y));
            return r;
        }

        /// <summary>Tramo de un camino (que va de sur a norte) entre dos valores de Z.</summary>
        static List<Vector2> Entre(IList<Vector2> camino, float zDesde, float zHasta)
        {
            var r = new List<Vector2>();
            for (int i = 0; i < camino.Count; i++)
            {
                bool dentro = camino[i].y >= zDesde && camino[i].y <= zHasta;
                if (i > 0)
                {
                    Vector2 a = camino[i - 1], b = camino[i];
                    foreach (float z in new[] { zDesde, zHasta })
                        if ((a.y < z) != (b.y < z) && Mathf.Abs(b.y - a.y) > 1e-5f)
                        {
                            var corte = a + (b - a) * ((z - a.y) / (b.y - a.y));
                            if (r.Count == 0 || Vector2.Distance(r[r.Count - 1], corte) > 0.01f) r.Add(corte);
                        }
                }
                if (dentro && (r.Count == 0 || Vector2.Distance(r[r.Count - 1], camino[i]) > 0.01f)) r.Add(camino[i]);
            }
            return r;
        }

        // =====================================================================
        //  Calles: asfalto, veredas de los edificios, canteros y marcas viales
        // =====================================================================

        static void Calles()
        {
            var calles = new GameObject("Calles").transform;
            calles.SetParent(raiz, false);
            var asfalto = MatSuelo("Asfalto", TexturaAsfalto(), null, 0.18f);
            var vereda = MatSuelo("Baldosas", TexturaBaldosas(), NormalesBaldosas(), 0.12f);
            var cordonMat = MatSuelo("Granito claro", TexturaGranitoClaro(), null, 0.15f);
            var hormigon = MatSuelo("Hormigón", TexturaHormigon(), null, 0.1f);
            var pasto = MatSuelo("Césped", TexturaCesped(), null, 0.02f);
            var blanco = Mat("Pintura blanca", "#e9e7e0", 0.25f);
            var amarillo = Mat("Pintura amarilla", "#d9a82a", 0.25f);
            var verde = Mat("Pintura verde bici", "#3f9263", 0.2f);
            var rojizo = Mat("Adoquín rojizo", "#9c6a55", 0.1f);

            // --- Asfalto y veredas de las líneas de edificios (con los cruces al ras) ---
            var m = new MallaB();
            Losa(m, new Rect(AsfaltoOeste, -260f, AsfaltoEste - AsfaltoOeste, 570f), YAsfalto, 4f);
            for (int i = 0; i < Cruces.Length; i += 2)
            {
                Losa(m, new Rect(-260f, Cruces[i], 260f + AsfaltoOeste, Cruces[i + 1] - Cruces[i]), YAsfalto, 4f);
                Losa(m, new Rect(AsfaltoEste, Cruces[i], 260f - AsfaltoEste, Cruces[i + 1] - Cruces[i]), YAsfalto, 4f);
            }
            m.Crear("Asfalto", calles, asfalto, false);

            m = new MallaB();
            var bordes = new MallaB();
            float z0 = -260f;
            for (int i = 0; i <= Cruces.Length; i += 2)
            {
                float z1 = i < Cruces.Length ? Cruces[i] : 310f;
                Losa(m, new Rect(-260f, z0, 260f + AsfaltoOeste, z1 - z0), 0f, 1.6f);
                Losa(m, new Rect(AsfaltoEste, z0, 260f - AsfaltoEste, z1 - z0), 0f, 1.6f);
                // Cordón de cada manzana, del lado de la avenida y de las calles que cruzan.
                foreach (var r in new[] { new Rect(-260f, z0, 260f + AsfaltoOeste, z1 - z0), new Rect(AsfaltoEste, z0, 260f - AsfaltoEste, z1 - z0) })
                {
                    var contorno = new List<Vector2> { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };
                    Faldon(bordes, contorno, q => 0f, YAsfalto, 1f);
                }
                if (i < Cruces.Length) z0 = Cruces[i + 1];
            }
            m.Crear("Veredas de los edificios", calles, vereda, false);
            bordes.Crear("Cordones de las veredas", calles, cordonMat, false);

            // --- Canteros con árboles: entre 9 de Julio y Cerrito, y entre 9 de Julio y Pellegrini ---
            m = new MallaB();
            var pastoCantero = new MallaB();
            var canteros = new List<Rect>();
            z0 = -260f;
            for (int i = 0; i <= Cruces.Length; i += 2)
            {
                float z1 = i < Cruces.Length ? Cruces[i] - 3f : 310f;
                float desde = i == 0 ? z0 : z0 + 3f;
                canteros.Add(new Rect(-57.5f, desde, 5f, z1 - desde));
                if (i < Cruces.Length) z0 = Cruces[i + 1];
            }
            // El del lado este no llega hasta la plaza: ahí las calzadas se abren en abanico.
            canteros.Add(new Rect(49.5f, -260f, 2.2f, 82f));
            canteros.Add(new Rect(49.5f, -160f, 2.2f, 99.5f));
            foreach (var r in canteros)
            {
                var contorno = new List<Vector2> { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };
                Faldon(m, contorno, q => 0.03f, YAsfalto, 1f);
                Superficie(m, contorno, q => 0.03f, 100f, 1f);
                var interior = Desplazar(contorno, true, 0.3f);
                Faldon(pastoCantero, interior, q => 0.07f, 0.02f, 4f);
                Superficie(pastoCantero, interior, q => 0.07f, 100f, 4f);
            }
            m.Crear("Canteros (cordón)", calles, cordonMat, false);
            pastoCantero.Crear("Canteros (césped)", calles, pasto, false);

            // --- Separadores que siguen la curva de la plaza ---
            // Oeste: separador angosto entre las dos calzadas de 9 de Julio.
            var sep = new MallaB();
            foreach (var tramo in TramosDeAvenida(caminoOeste, 10.6f))
                Barrido(sep, tramo, false, PerfilCaja(-0.7f, 0.7f, YAsfalto, 0.04f), true, 1f, 1f, true);
            // Este: entre el Metrobús y la calzada, y entre la calzada y la que sigue.
            foreach (var tramo in TramosDeAvenida(caminoEste, -8.4f))
                Barrido(sep, tramo, false, PerfilCaja(-1.1f, 1.1f, YAsfalto, 0.04f), true, 1f, 1f, true);
            foreach (var tramo in TramosDeAvenida(caminoEste, -21.05f))
                Barrido(sep, tramo, false, PerfilCaja(-0.75f, 0.75f, YAsfalto, 0.04f), true, 1f, 1f, true);
            sep.Crear("Separadores", calles, hormigon, false);

            // Isleta del nordeste: donde las calzadas que rodean la plaza se separan de Carlos Pellegrini.
            {
                var borde = Entre(Desplazar(caminoEste, false, -22.3f), -30f, 60f);
                var isleta = new List<Vector2>();
                foreach (var q in borde) if (q.x < 49.2f - 2.4f) isleta.Add(q);
                if (isleta.Count >= 2)
                {
                    isleta.Add(new Vector2(49.2f, isleta[isleta.Count - 1].y));
                    isleta.Add(new Vector2(49.2f, isleta[0].y));
                    var pastoIsleta = new MallaB();
                    Superficie(pastoIsleta, isleta, q => 0.07f, 100f, 4f);
                    pastoIsleta.Crear("Isleta nordeste (césped)", calles, pasto, false);
                    var cordonIsleta = new MallaB();
                    Barrido(cordonIsleta, isleta, true, PerfilCaja(-0.14f, 0.14f, YAsfalto, 0.1f), true, 1f, 1f, false);
                    cordonIsleta.Crear("Isleta nordeste (cordón)", calles, cordonMat, false);
                    isletaNordeste = isleta;
                }
            }

            // Carriles del Metrobús: hormigón más claro que el asfalto.
            var metro = new MallaB();
            foreach (var tramo in TramosDeAvenida(caminoEste, -3.8f))
                Barrido(metro, tramo, false, new[] { new Vector2(-3.45f, YAsfalto + 0.004f), new Vector2(3.45f, YAsfalto + 0.004f) }, false, 2f, 2f, false);
            metro.Crear("Carriles del Metrobús", calles, MatSuelo("Hormigón del Metrobús", TexturaHormigon(), null, 0.1f, "#b9b8b2"), false);

            // --- Isla del Obelisco y mitad sur de la plaza (espejada, sin el detalle del lado norte) ---
            m = new MallaB();
            var isla = Elipse(Obelisco, RadioIsla, RadioIsla, 48);
            Superficie(m, isla, q => YVereda, 100f, 1.6f);
            Superficie(m, AlSur(Desplazar(cordon, true, 0.18f)), q => YVereda, 100f, 1.6f);
            m.Crear("Isla del Obelisco y plaza sur", calles, vereda, false);
            m = new MallaB();
            Barrido(m, isla, true, PerfilCordon(), false, 1f, 1f, false);
            Barrido(m, AlSur(cordon), true, PerfilCordonEspejado(), false, 1f, 1f, false);
            m.Crear("Cordones de la isla y la plaza sur", calles, cordonMat, false);
            m = new MallaB();
            Superficie(m, AlSur(explanada), q => 0f, 100f, 2f, GiroPlaza);
            m.Crear("Explanada sur", calles, MatSuelo("Adoquines", TexturaAdoquines(), NormalesAdoquines(), 0.1f), false);
            m = new MallaB();
            Superficie(m, AlSur(Puntos(dJardinBase)), q => -0.007f, 100f, 4f);
            m.Crear("Jardines del sur", calles, pasto, false);

            // --- Marcas viales ---
            var a = new Pintura();
            var bloques = new Armador();
            // 9 de Julio oeste: tres carriles, separador y cinco carriles más.
            LineaVial(a, caminoOeste, 0.35f, 0.14f, blanco, false);
            foreach (float d in new[] { 3.55f, 6.75f }) LineaVial(a, caminoOeste, d, 0.13f, blanco, true);
            LineaVial(a, caminoOeste, 9.7f, 0.14f, amarillo, false);
            LineaVial(a, caminoOeste, 11.5f, 0.14f, amarillo, false);
            foreach (float d in new[] { 14.75f, 18f, 21.25f, 24.5f }) LineaVial(a, caminoOeste, d, 0.13f, blanco, true);
            LineaVial(a, caminoOeste, 27.8f, 0.14f, blanco, false);
            // 9 de Julio este: Metrobús (dos carriles), separador y tres carriles.
            LineaVial(a, caminoEste, -0.4f, 0.14f, amarillo, false);
            LineaVial(a, caminoEste, -3.8f, 0.13f, blanco, true);
            LineaVial(a, caminoEste, -7.15f, 0.14f, blanco, false);
            LineaVial(a, caminoEste, -9.65f, 0.14f, blanco, false);
            foreach (float d in new[] { -13.2f, -16.75f }) LineaVial(a, caminoEste, d, 0.13f, blanco, true);
            LineaVial(a, caminoEste, -20.15f, 0.14f, blanco, false);
            // Cerrito y Carlos Pellegrini (rectas).
            var cerrito = new List<Vector2> { new Vector2(-60.2f, -260f), new Vector2(-60.2f, 310f) };
            LineaVial(a, cerrito, 0f, 0.13f, blanco, true);
            foreach (float x in new[] { 55.2f, 58.7f, 62.2f, 65.3f })
                LineaVial(a, new List<Vector2> { new Vector2(x, -260f), new Vector2(x, 310f) }, 0f, 0.13f, blanco, true);
            LineaVial(a, new List<Vector2> { new Vector2(51.9f, -60f), new Vector2(51.9f, 310f) }, 0f, 0.14f, blanco, false);

            // Corrientes, al norte y al sur del Obelisco: separadores amarillos y blancos junto al
            // carril de la plaza, y dos líneas discontinuas.
            var surDeLaPlaza = Desplazar(bordeSur, false, -4.4f);
            var sa = Acumulado(surDeLaPlaza);
            for (float s = 2f; s + 2.2f < sa[sa.Length - 1] - 6f; s += 2.9f)
            {
                Vector2 dir;
                Vector2 p = PuntoEn(surDeLaPlaza, sa, s, out dir), q = PuntoEn(surDeLaPlaza, sa, s + 2.2f, out dir);
                bool esAmarillo = Mathf.RoundToInt(s / 2.9f) % 2 == 0;
                Segmento(bloques, new Vector3(p.x, YAsfalto + 0.07f, p.y), new Vector3(q.x, YAsfalto + 0.07f, q.y), 0.42f, 0.14f, esAmarillo ? amarillo : blanco);
                var p2 = AlSur(new[] { p, q });
                Segmento(bloques, new Vector3(p2[0].x, YAsfalto + 0.07f, p2[0].y), new Vector3(p2[1].x, YAsfalto + 0.07f, p2[1].y), 0.42f, 0.14f, esAmarillo ? amarillo : blanco);
            }
            foreach (float d in new[] { -8f, -11.4f })
            {
                var linea = Desplazar(bordeSur, false, d);
                Trazo(a, linea, 0.13f, blanco, true);
                Trazo(a, AlSur(linea), 0.13f, blanco, true);
            }
            // Fuera de la plaza, Corrientes sigue derecha hacia los dos lados.
            foreach (float z in new[] { -40.9f, -44.5f, -48.1f })
            {
                Trazo(a, new List<Vector2> { new Vector2(-260f, z), new Vector2(AsfaltoOeste - 2f, z) }, 0.13f, blanco, true);
                Trazo(a, new List<Vector2> { new Vector2(AsfaltoEste + 2f, z), new Vector2(260f, z) }, 0.13f, blanco, true);
            }

            // Sendas peatonales (medidas de la foto): hacia el oeste desde la vereda sudoeste y hacia
            // el este cruzando el Metrobús.
            Cebra(a, -52.3f, -24.4f, -20.6f, -14.1f, true, blanco);
            Cebra(a, 27.6f, 48.6f, -14.1f, -10.2f, true, blanco);
            // Cruce de adoquín rojizo hacia el este, y bicisenda verde de Corrientes con su cruce.
            a.Rect(new Vector3(31.7f, YPintura, -19.1f), 8.4f, 5.6f, 0f, rojizo);
            a.Rect(new Vector3(47f, YPintura, -23.8f), 40f, 3f, 0f, verde);
            for (int i = 0; i < 12; i++)
            {
                float z = -22.2f - i * 1.05f;
                a.Rect(new Vector3(23.1f, YPintura, z), 5.2f, 1.05f, 0f, verde);
                a.Rect(new Vector3(23.1f, YPintura + 0.004f, z), 4.8f, 0.45f, 0f, blanco);
            }
            // Isleta pintada (cebrado) que rodea la esquina sudoeste.
            var esquina = new List<Vector2>();
            var crudo = Puntos(dCordon);
            for (int i = 21; i <= 26; i++) esquina.Add(crudo[i]);
            esquina = Suavizar(esquina, false, 0.5f);
            var borde1 = Desplazar(esquina, false, -0.35f);
            var borde2 = Desplazar(esquina, false, -1.25f);
            Trazo(a, borde1, 0.1f, blanco, false);
            Trazo(a, borde2, 0.1f, blanco, false);
            for (int i = 2; i < esquina.Count - 2; i += 2)
                a.Cinta(new Vector3(borde1[i].x, YPintura, borde1[i].y), new Vector3(borde2[i + 1].x, YPintura, borde2[i + 1].y), 0.1f, blanco);

            // Flechas de sentido y rombos del Metrobús.
            foreach (float d in new[] { 1.95f, 5.15f, 8.2f, 13.1f, 16.4f, 19.6f, 22.9f })
                foreach (float z in new[] { -6f, 44f })
                {
                    Vector2 dir;
                    var carril = Desplazar(caminoOeste, false, d);
                    Vector2 p = PuntoEn(carril, Acumulado(carril), AvanceHastaZ(carril, z), out dir);
                    Flecha(a, new Vector3(p.x, YPintura, p.y), Mathf.Atan2(-dir.x, -dir.y) * Mathf.Rad2Deg, blanco);
                }
            foreach (float d in new[] { -11.4f, -15f, -18.5f })
                foreach (float z in new[] { 2f, 40f })
                {
                    Vector2 dir;
                    var carril = Desplazar(caminoEste, false, d);
                    Vector2 p = PuntoEn(carril, Acumulado(carril), AvanceHastaZ(carril, z), out dir);
                    Flecha(a, new Vector3(p.x, YPintura, p.y), Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, blanco);
                }
            foreach (float z in new[] { -2f, 22f, 48f })
            {
                Vector2 dir;
                var carril = Desplazar(caminoEste, false, -2.05f);
                Vector2 p = PuntoEn(carril, Acumulado(carril), AvanceHastaZ(carril, z), out dir);
                Rombo(a, new Vector3(p.x, YPintura + 0.004f, p.y), Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, blanco);
            }
            a.Crear("Marcas viales", calles);
            bloques.Crear("Separadores de Corrientes", calles, false);
        }

        /// <summary>Tramos de una curva paralela a la avenida, cortados donde cruzan las calles y las sendas.</summary>
        static List<List<Vector2>> TramosDeAvenida(List<Vector2> camino, float desvio)
        {
            var paralela = Desplazar(camino, false, desvio);
            var r = new List<List<Vector2>>();
            float z0 = -258f;
            foreach (var corte in new[] { new Vector2(-178f, -160f), new Vector2(-60f, -7.5f), new Vector2(73f, 89f), new Vector2(308f, 400f) })
            {
                var tramo = Entre(paralela, z0, corte.x);
                if (tramo.Count >= 2) r.Add(tramo);
                z0 = corte.y;
            }
            return r;
        }

        /// <summary>Distancia recorrida sobre un camino (de sur a norte) hasta llegar a ese Z.</summary>
        static float AvanceHastaZ(List<Vector2> camino, float z)
        {
            var s = Acumulado(camino);
            for (int i = 1; i < camino.Count; i++)
                if ((camino[i - 1].y <= z) != (camino[i].y <= z))
                    return s[i - 1] + (s[i] - s[i - 1]) * Mathf.InverseLerp(camino[i - 1].y, camino[i].y, z);
            return z <= camino[0].y ? 0f : s[s.Length - 1];
        }

        static Vector2[] PerfilCaja(float x0, float x1, float y0, float y1) =>
            new[] { new Vector2(x0, y0), new Vector2(x0, y1), new Vector2(x1, y1), new Vector2(x1, y0) };

        /// <summary>Cordón de la plaza: va sobre el borde, con la calle a la derecha del avance (polígono antihorario).</summary>
        static Vector2[] PerfilCordon() => new[] { new Vector2(0.2f, YVereda + 0.005f), new Vector2(0f, YVereda + 0.005f), new Vector2(-0.02f, YAsfalto) };
        static Vector2[] PerfilCordonEspejado() => new[] { new Vector2(-0.2f, YVereda + 0.005f), new Vector2(0f, YVereda + 0.005f), new Vector2(0.02f, YAsfalto) };

        static void Losa(MallaB m, Rect r, float y, float mosaico)
        {
            m.Cuad(new Vector3(r.xMin, y, r.yMin), new Vector3(r.xMin, y, r.yMax), new Vector3(r.xMax, y, r.yMax), new Vector3(r.xMax, y, r.yMin), Vector3.up,
                new Vector2(r.xMin, r.yMin) / mosaico, new Vector2(r.xMin, r.yMax) / mosaico, new Vector2(r.xMax, r.yMax) / mosaico, new Vector2(r.xMax, r.yMin) / mosaico);
        }

        /// <summary>Línea pintada paralela a un camino (salteando los cruces de calles).</summary>
        static void LineaVial(Pintura a, List<Vector2> camino, float desvio, float ancho, Material m, bool discontinua)
        {
            foreach (var tramo in TramosDeAvenida(camino, desvio)) Trazo(a, tramo, ancho, m, discontinua);
        }

        /// <summary>Línea pintada sobre el asfalto siguiendo una polilínea: continua, o 3 m pintados cada 8.</summary>
        static void Trazo(Pintura a, IList<Vector2> linea, float ancho, Material m, bool discontinua)
        {
            const float y = YPintura;
            if (!discontinua)
            {
                for (int i = 0; i + 1 < linea.Count; i++)
                    a.Cinta(new Vector3(linea[i].x, y, linea[i].y), new Vector3(linea[i + 1].x, y, linea[i + 1].y), ancho, m);
                return;
            }
            var s = Acumulado(linea);
            for (float d = 0f; d + 3f < s[s.Length - 1]; d += 8f)
            {
                Vector2 dir;
                Vector2 p = PuntoEn(linea, s, d, out dir), q = PuntoEn(linea, s, d + 3f, out dir);
                a.Cinta(new Vector3(p.x, y, p.y), new Vector3(q.x, y, q.y), ancho, m);
            }
        }

        static void Segmento(Armador a, Vector3 p, Vector3 q, float ancho, float alto, Material m)
        {
            var dir = q - p;
            if (dir.sqrMagnitude < 1e-8f) return;
            a.Caja((p + q) / 2f, new Vector3(ancho, alto, dir.magnitude), m, Quaternion.LookRotation(dir.normalized, Vector3.up));
        }

        /// <summary>
        /// Senda peatonal. Las franjas van a lo largo del sentido de los autos: si la senda cruza una
        /// calle que corre de norte a sur (<paramref name="cruzaEnX"/>), son largas en Z y se repiten en X.
        /// </summary>
        static void Cebra(Pintura a, float x0, float x1, float z0, float z1, bool cruzaEnX, Material m)
        {
            const float y = YPintura;
            if (cruzaEnX)
                for (float x = x0 + 0.3f; x + 0.5f <= x1; x += 1f)
                    a.Rect(new Vector3(x + 0.25f, y, (z0 + z1) / 2f), 0.5f, z1 - z0, 0f, m);
            else
                for (float z = z0 + 0.3f; z + 0.5f <= z1; z += 1f)
                    a.Rect(new Vector3((x0 + x1) / 2f, y, z + 0.25f), x1 - x0, 0.5f, 0f, m);
        }

        static void Flecha(Pintura a, Vector3 p, float giro, Material m)
        {
            var r = Quaternion.Euler(0, giro, 0);
            a.Rect(p + r * new Vector3(0, 0, -0.6f), 0.18f, 1.6f, giro, m);
            a.Rect(p + r * new Vector3(-0.22f, 0, 0.35f), 0.16f, 0.8f, giro + 35f, m);
            a.Rect(p + r * new Vector3(0.22f, 0, 0.35f), 0.16f, 0.8f, giro - 35f, m);
        }

        /// <summary>Rombo de carril exclusivo.</summary>
        static void Rombo(Pintura a, Vector3 p, float giro, Material m)
        {
            var r = Quaternion.Euler(0, giro, 0);
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                a.Rect(p + r * new Vector3(sx * 0.4f, 0, sz * 0.75f), 0.12f, 1.7f, giro + sx * sz * 28f, m);
        }

        // =====================================================================
        //  La plaza: base, cordón, veredas y senderos
        // =====================================================================

        static void Plaza()
        {
            var plaza = new GameObject("Plaza").transform;
            plaza.SetParent(raiz, false);
            var vereda = mats["Baldosas"];
            var hormigon = mats["Hormigón"];
            var cordonMat = mats["Granito claro"];
            var amarillo = Mat("Cordón amarillo", "#cfa238", 0.2f);
            var ralo = MatSuelo("Pasto ralo", TexturaPastoRalo(), null, 0.02f);
            var ladrillo = MatSuelo("Polvo de ladrillo", TexturaPolvoDeLadrillo(), null, 0.03f);

            // Base de toda la cuña (lo que no tapa nada de arriba se ve como hormigón).
            var m = new MallaB();
            Superficie(m, Desplazar(cordon, true, 0.18f), q => YBase, 100f, 2f);
            m.Crear("Base", plaza, hormigon, false);

            // Cordón: granito en los costados y pintado de amarillo sobre Corrientes.
            m = new MallaB();
            Barrido(m, cordon, true, PerfilCordon(), false, 1f, 1f, false);
            m.Crear("Cordón", plaza, cordonMat, false);
            m = new MallaB();
            Barrido(m, Desplazar(bordeSur, false, -0.012f), false, new[] { new Vector2(0.13f, YVereda + 0.009f), new Vector2(-0.01f, YVereda + 0.009f), new Vector2(-0.03f, YAsfalto + 0.01f) }, false, 1f, 1f, false);
            m.Crear("Cordón amarillo (Corrientes)", plaza, amarillo, false);

            // Veredas de baldosas de las dos esquinas del sur.
            m = new MallaB();
            Superficie(m, Puntos(dVeredaSO), q => YVereda, 100f, 1.6f);
            Superficie(m, Puntos(dVeredaSE), q => YVereda, 100f, 1.6f);
            m.Crear("Veredas", plaza, vereda, false);

            // Suelo del jardín y senderos.
            m = new MallaB();
            Superficie(m, Puntos(dJardinBase), q => YJardin, 100f, 5f);
            m.Crear("Suelo del jardín", plaza, ralo, false);
            m = new MallaB();
            Superficie(m, Suavizar(Puntos(dSenderoOeste), true, 1.5f), q => YSendero, 100f, 2f);
            Superficie(m, Suavizar(Puntos(dSenderoEste), true, 1.5f), q => YSendero, 100f, 2f, -24f);
            m.Crear("Senderos de hormigón", plaza, hormigon, false);
            m = new MallaB();
            Superficie(m, Suavizar(Puntos(dTerracotaArco), true, 0.8f), q => YTerracota, 100f, 3f);
            Superficie(m, Suavizar(Puntos(dTerracotaTronco), true, 1.2f), q => YTerracota + 0.004f, 100f, 3f, 35f);
            m.Crear("Senderos de polvo de ladrillo", plaza, ladrillo, false);
        }

        // =====================================================================
        //  La explanada
        // =====================================================================

        static void Explanada()
        {
            var grupo = new GameObject("Explanada").transform;
            grupo.SetParent(raiz, false);
            var adoquin = mats["Adoquines"];
            // Las bandas son del mismo granito que los cordones, pero más gastadas: apenas se distinguen.
            var granito = MatSuelo("Granito de las bandas", TexturaGranitoClaro(), null, 0.15f, "#cfc9b9");

            var m = new MallaB();
            Superficie(m, explanada, q => 0f, 100f, 2f, GiroPlaza);
            m.Crear("Piso de adoquines", grupo, adoquin, false);

            // Bandas de granito claro a lo largo del eje de la plaza (cada 5,5 m, como en la foto).
            m = new MallaB();
            foreach (var punto in Puntos(dBandas))
            {
                var banda = RecortarFranja(explanada, punto, ejePlaza, 0.62f);
                if (banda.Count >= 3) Superficie(m, banda, q => 0.006f, 100f, 1f, GiroPlaza + 90f);
            }
            m.Crear("Bandas de granito", grupo, granito, false);

            // Rejilla de desagüe que cruza la explanada cerca del borde sur.
            var rejilla = Puntos(dRejilla);
            m = new MallaB();
            Barrido(m, rejilla, false, new[] { new Vector2(-0.14f, 0.01f), new Vector2(0.14f, 0.01f) }, false, 0.25f, 0.28f, false);
            m.Crear("Rejilla de desagüe", grupo, MatTex("Rejilla", "#ffffff", TexturaRejilla(), 0.35f), false);
        }

        static Texture2D TexturaRejilla()
        {
            return Pintar("Rejilla", 64, 64, (u, v) =>
            {
                float ranura = Mathf.Abs(u * 8f - Mathf.Round(u * 8f));
                bool marco = v < 0.12f || v > 0.88f;
                return marco ? Gris(0.26f) : (ranura < 0.22f ? Gris(0.06f) : Gris(0.22f));
            });
        }

        // =====================================================================
        //  El anillo: murete de piedra y el césped que arranca en su lomo
        // =====================================================================

        static void Anillo()
        {
            var grupo = new GameObject("Anillo").transform;
            grupo.SetParent(raiz, false);
            var piedra = MatSuelo("Piedra del murete", TexturaPiedra(), NormalesPiedra(), 0.12f);
            var cespedAnillo = MatSuelo("Césped del anillo", TexturaCespedAnillo(), null, 0.02f);

            // Murete: cara hacia la explanada, canto apenas matado, lomo y cara de atrás (la tapa el césped).
            float a = AnilloAncho / 2f;
            var perfil = new[]
            {
                new Vector2(-a, 0f), new Vector2(-a, AnilloAlto - 0.035f), new Vector2(-a + 0.035f, AnilloAlto),
                new Vector2(a, AnilloAlto), new Vector2(a, 0.2f),
            };
            var m = new MallaB();
            Barrido(m, anillo, false, perfil, false, 1.2f, 0.5f, true);
            m.Crear("Murete de piedra", grupo, piedra);

            // Césped: sale al ras del lomo, sube unos centímetros y cae hasta el borde de los senderos.
            m = new MallaB();
            Banda(m, cespedAnilloAdentro, cespedAnilloAfuera, 7, AlturaCespedAnillo, 6f);
            m.Crear("Césped del anillo", grupo, cespedAnillo, false);

            // Borde de hormigón del césped (la línea clara que se ve en la foto) y cierre del costado.
            m = new MallaB();
            Barrido(m, cespedAnilloAfuera, false, PerfilCaja(-0.07f, 0.07f, YJardin, YBordeCesped + 0.03f), true, 1f, 1f, true);
            m.Crear("Borde del césped del anillo", grupo, mats["Hormigón"], false);
        }

        /// <summary>Perfil del césped del anillo: v = 0 contra el murete, v = 1 en el borde de afuera.</summary>
        static float AlturaCespedAnillo(float v, Vector2 q)
        {
            float lomo = AnilloAlto - 0.012f;
            float caida = Escalon(0.30f, 1f, v);
            float loma = 0.08f * Mathf.Sin(Mathf.Clamp01(v / 0.55f) * Mathf.PI);
            float ondas = (Mathf.PerlinNoise(q.x * 0.35f + 31.7f, q.y * 0.35f + 12.3f) - 0.5f) * 0.05f * Mathf.Sin(v * Mathf.PI);
            return Mathf.Lerp(lomo, YBordeCesped, caida) + loma + ondas;
        }

        // =====================================================================
        //  Jardines: césped, canteros, arbustos
        // =====================================================================

        static void Jardines()
        {
            var grupo = new GameObject("Jardines").transform;
            grupo.SetParent(raiz, false);
            var pasto = mats["Césped"];
            var mantillo = MatSuelo("Mantillo", TexturaMantillo(), null, 0.02f);
            var ralo = mats["Pasto ralo"];
            var hormigon = mats["Hormigón"];
            var follajeTex = TexturaFollaje();
            var follajeNormales = NormalesFollaje();
            // Con relieve para lo que se ve de cerca (macizo, seto, cartel); liso para las matas y la arboleda.
            var follajes = new[]
            {
                MatSuelo("Follaje oscuro", follajeTex, follajeNormales, 0.05f, "#55733f"),
                MatSuelo("Follaje medio", follajeTex, follajeNormales, 0.05f, "#6d8f47"),
                MatSuelo("Follaje claro", follajeTex, follajeNormales, 0.05f, "#89a352"),
            };
            var lisos = new[]
            {
                MatSuelo("Follaje oscuro liso", follajeTex, null, 0.05f, "#55733f"),
                MatSuelo("Follaje medio liso", follajeTex, null, 0.05f, "#6d8f47"),
                MatSuelo("Follaje claro liso", follajeTex, null, 0.05f, "#89a352"),
                MatSuelo("Follaje seco liso", follajeTex, null, 0.05f, "#9a8e58"),
                MatSuelo("Follaje rojizo liso", follajeTex, null, 0.05f, "#7a5140"),
            };
            var copas = new[] { lisos[0], lisos[1], lisos[2] };
            var tronco = Mat("Tronco", "#6e5846", 0.1f);
            var perfilBorde = PerfilCaja(-0.06f, 0.06f, YJardin, YBordeCesped + 0.03f);

            // --- Césped con una loma suave y borde de hormigón ---
            var cesped = new MallaB();
            var bordes = new MallaB();
            foreach (var datos in new[] { dPastoSO, dPastoE, dPastoTri, dFranjaOeste, dFranjaEsteS })
            {
                var poli = datos == dPastoTri ? Puntos(datos) : Suavizar(Puntos(datos), true, 1.2f);
                System.Func<Vector2, float> altura = q => YBordeCesped + 0.14f * (1f - Mathf.Exp(-DistanciaA(poli, true, q) / 1.3f));
                Superficie(cesped, poli, altura, 1.6f, 4f);
                Faldon(cesped, poli, q => YBordeCesped, YJardin, 4f);
                Barrido(bordes, poli, true, perfilBorde, true, 1f, 1f, false);
            }
            cesped.Crear("Césped", grupo, pasto, false);

            // --- Franja de plantas del borde este (tramo norte): pasto ralo ---
            var franja = new MallaB();
            var franjaNorte = Suavizar(Puntos(dFranjaEsteN), true, 1.5f);
            Superficie(franja, franjaNorte, q => YBordeCesped - 0.02f, 100f, 5f);
            Faldon(franja, franjaNorte, q => YBordeCesped - 0.02f, YJardin, 5f);
            franja.Crear("Franja de plantas", grupo, ralo, false);

            // --- Canteros oscuros (mantillo) con plantas, y suelo del macizo de arbustos ---
            var matas = new Armador();     // plantas chicas: follaje liso
            var cerca = new Armador();     // arbustos grandes y cercanos: follaje con relieve
            var canteros = new MallaB();
            foreach (var datos in new[] { dCantero1, dCantero2 })
            {
                var poli = Suavizar(Puntos(datos), true, 1f);
                System.Func<Vector2, float> altura = q => YBordeCesped + 0.10f * (1f - Mathf.Exp(-DistanciaA(poli, true, q) / 1.0f));
                Superficie(canteros, poli, altura, 1.6f, 2f);
                Faldon(canteros, poli, q => YBordeCesped, YJardin, 2f);
                Barrido(bordes, poli, true, perfilBorde, true, 1f, 1f, false);
                foreach (var q in Dispersar(poli, Mathf.RoundToInt(Mathf.Abs(AreaConSigno(poli)) * 1.3f), 0.35f))
                    Arbusto(matas, V3(q, altura(q)), Random.Range(0.35f, 0.75f), new[] { lisos[0], lisos[4], lisos[4], lisos[3] });
            }
            var macizo = Puntos(dArbustosNO);
            Superficie(canteros, macizo, q => YBordeCesped, 100f, 2f);
            Faldon(canteros, macizo, q => YBordeCesped, YJardin, 2f);
            canteros.Crear("Canteros", grupo, mantillo, false);
            bordes.Crear("Bordes de hormigón", grupo, hormigon, false);

            // --- Macizo de arbustos del noroeste (sin árboles: ahí la foto muestra una masa baja y pareja) ---
            foreach (var q in Dispersar(macizo, Mathf.RoundToInt(Mathf.Abs(AreaConSigno(macizo)) * 0.5f), 0.5f))
                Arbusto(cerca, V3(q, YBordeCesped), Random.Range(1.1f, 2.6f), new[] { follajes[0], follajes[0], follajes[1] });

            // --- Matas sueltas en el suelo del jardín (donde no hay césped ni senderos) ---
            var ocupados = new List<List<Vector2>>();
            foreach (var datos in new[] { dPastoSO, dPastoE, dPastoTri, dFranjaOeste, dFranjaEsteS, dCantero1, dCantero2, dArbustosNO, dSenderoOeste, dSenderoEste, dTerracotaArco, dTerracotaTronco })
                ocupados.Add(Puntos(datos));
            var cespedAnilloPoli = new List<Vector2>(cespedAnilloAfuera);
            for (int i = anillo.Count - 1; i >= 0; i--) cespedAnilloPoli.Add(anillo[i]);
            ocupados.Add(cespedAnilloPoli);
            ocupados.Add(explanada);
            var jardin = Puntos(dJardinBase);
            var arboles = new Armador();
            int cuantosArboles = 0;
            foreach (var q in Dispersar(jardin, 520, 0.6f))
            {
                bool libre = true;
                foreach (var o in ocupados)
                    if (Dentro(o, q) || DistanciaA(o, true, q) < 0.5f) { libre = false; break; }
                if (!libre) continue;
                // Al norte del cartel hay más plantas y algunos árboles; cerca de las veredas, casi nada.
                // Los árboles van lejos del stand, para que no tapen la cámara cuando gira alrededor.
                if (q.y < 14f && Random.value < 0.7f) continue;
                if (q.y > 42f && cuantosArboles < 9 && Random.value < 0.09f && DistanciaA(jardin, true, q) > 2.5f)
                {
                    Arbol(arboles, V3(q, YJardin), Random.Range(0.75f, 1.05f), tronco, copas, false);
                    cuantosArboles++;
                    continue;
                }
                Arbusto(matas, V3(q, YJardin), Random.Range(0.3f, 0.8f), new[] { lisos[1], lisos[2], lisos[3], lisos[3] });
            }
            // Franja del borde este (norte): matas secas, como en la foto.
            foreach (var q in Dispersar(Puntos(dFranjaEsteN), 110, 0.4f))
                Arbusto(matas, V3(q, YBordeCesped - 0.02f), Random.Range(0.3f, 0.7f), new[] { lisos[3], lisos[3], lisos[1], lisos[4] });
            // Hilera de arbustos junto al cordón este.
            foreach (var q in Puntos(dMacetas))
                Arbusto(matas, V3(q, YVereda), Random.Range(0.75f, 0.9f), new[] { lisos[0], lisos[4] });
            matas.Crear("Matas y plantas", grupo, true, Canales.ConUV);
            cerca.Crear("Macizo de arbustos", grupo, true, Canales.ConRelieve);

            // Seto bajo junto al cordón oeste.
            var seto = Entre(Desplazar(caminoOeste, false, -1.15f), -5.2f, 6.4f);
            if (seto.Count >= 2)
            {
                var sm = new MallaB();
                Barrido(sm, seto, false, new[] { new Vector2(-0.4f, YBordeCesped), new Vector2(-0.42f, 0.75f), new Vector2(-0.2f, 0.95f), new Vector2(0.2f, 0.95f), new Vector2(0.42f, 0.75f), new Vector2(0.4f, YBordeCesped) }, false, 1.5f, 1.5f, true);
                sm.Crear("Seto del cordón oeste", grupo, follajes[0]);
            }

            // --- Árboles: jardín norte, veredas, canteros centrales, separador del Metrobús, isleta y plaza sur ---
            for (float z = -250f; z < 300f; z += 11f)
            {
                if (EnCruce(z, 5f)) continue;
                Arbol(arboles, new Vector3(-55f + Random.Range(-1.2f, 1.2f), 0.07f, z + Random.Range(-2f, 2f)), Random.Range(1.0f, 1.35f), tronco, copas, true);
                Arbol(arboles, new Vector3(AsfaltoOeste - 1.6f, 0f, z + 5f), Random.Range(0.7f, 0.9f), tronco, copas, true);
                Arbol(arboles, new Vector3(AsfaltoEste + 1.6f, 0f, z + 2f), Random.Range(0.7f, 0.9f), tronco, copas, true);
                if (z < -62f) Arbol(arboles, new Vector3(50.6f, 0.07f, z + 3f), Random.Range(0.8f, 1.0f), tronco, copas, true);
            }
            foreach (var tramo in TramosDeAvenida(caminoEste, -8.4f))
            {
                var s = Acumulado(tramo);
                for (float d = 4f; d < s[s.Length - 1] - 3f; d += 9.5f)
                {
                    Vector2 dir;
                    Vector2 p = PuntoEn(tramo, s, d, out dir);
                    Arbol(arboles, new Vector3(p.x, 0.04f, p.y), Random.Range(0.55f, 0.75f), tronco, copas, true);
                }
            }
            if (isletaNordeste != null)
                foreach (var q in Dispersar(isletaNordeste, 9, 1.6f))
                    Arbol(arboles, V3(q, 0.07f), Random.Range(0.8f, 1.05f), tronco, copas, true);
            var explanadaSur = AlSur(explanada);
            foreach (var q in Dispersar(AlSur(Puntos(dJardinBase)), 26, 3f))
                if (!Dentro(explanadaSur, q)) Arbol(arboles, V3(q, 0f), Random.Range(0.9f, 1.2f), tronco, copas, true);
            arboles.Crear("Árboles", grupo, true, Canales.ConUV);
        }

        static bool EnCruce(float z, float margen)
        {
            for (int i = 0; i < Cruces.Length; i += 2)
                if (z > Cruces[i] - margen && z < Cruces[i + 1] + margen) return true;
            return false;
        }

        /// <summary>Árbol: tronco con dos ramas y copa de varias masas de follaje. <paramref name="lejano"/>: copa con menos piezas.</summary>
        static void Arbol(Armador a, Vector3 p, float esc, Material tronco, Material[] follajes, bool lejano)
        {
            float h = Random.Range(3.2f, 4.4f) * esc;
            var cima = p + new Vector3(Random.Range(-0.4f, 0.4f), h, Random.Range(-0.4f, 0.4f));
            a.Cilindro(p - Vector3.up * 0.1f, cima, 0.2f * esc, tronco, tronco8);
            for (int i = 0; i < 2; i++)
            {
                var dir = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * new Vector3(1f, 0.9f, 0);
                a.Cilindro(cima - Vector3.up * 0.4f, cima + dir * 1.7f * esc, 0.1f * esc, tronco, tronco8);
            }
            var copa = follajes[Random.Range(0, follajes.Length)];
            int n = lejano ? Random.Range(5, 7) : Random.Range(7, 10);
            for (int i = 0; i < n; i++)
            {
                var off = new Vector3(Random.Range(-2.4f, 2.4f), Random.Range(0.5f, 2.3f), Random.Range(-2.4f, 2.4f)) * esc;
                float s = Random.Range(2.5f, 3.6f) * esc;
                var mat = Random.value < 0.3f ? follajes[Random.Range(0, follajes.Length)] : copa;
                a.Malla(lejano ? matasSimples[Random.Range(0, matasSimples.Count)] : icosferas[Random.Range(0, icosferas.Count)],
                    Matrix4x4.TRS(cima + off, Random.rotation, new Vector3(s, s * 0.75f, s)), mat);
            }
        }

        /// <summary>Arbusto o mata: dos o tres masas de follaje apoyadas en el suelo. esc = alto aproximado en metros.</summary>
        static void Arbusto(Armador a, Vector3 p, float esc, Material[] follajes)
        {
            var m = follajes[Random.Range(0, follajes.Length)];
            int n = esc > 0.9f ? 3 : 2;
            for (int i = 0; i < n; i++)
            {
                float s = Random.Range(0.85f, 1.25f) * esc;
                a.Malla(matasSimples[Random.Range(0, matasSimples.Count)],
                    Matrix4x4.TRS(p + new Vector3(Random.Range(-0.35f, 0.35f) * esc, s * 0.36f, Random.Range(-0.35f, 0.35f) * esc), Random.rotation, new Vector3(s * 1.15f, s, s * 1.15f)), m);
            }
        }

        // =====================================================================
        //  Mobiliario: bolardos, cestos, torres de reflectores, cartel, semáforos, faroles, parada
        // =====================================================================

        static void Mobiliario()
        {
            var grupo = new GameObject("Mobiliario").transform;
            grupo.SetParent(raiz, false);
            var negro = Mat("Hierro negro", "#232427", 0.35f);
            var grisOscuro = Mat("Hierro gris", "#3a3c41", 0.35f);
            var gris = Mat("Farol gris", "#8d9096", 0.4f);
            var blanco = Mat("Caño blanco", "#eceae4", 0.45f);
            var cajaSem = Mat("Caja semáforo", "#1c1d20", 0.3f);
            var amarilloSem = Mat("Marco semáforo", "#e2b12f", 0.3f);
            var vidrio = Mat("Vidrio parada", "#a9c3cf", 0.7f);
            var crema = Mat("Gabinete", "#d9d2bc", 0.2f);
            var luces = new[] { Emisiva("Luz roja", "#ff4a3d"), Emisiva("Luz amarilla", "#ffc93c"), Emisiva("Luz verde", "#44d17a"), Emisiva("Luz farol", "#fff1c8") };
            var a = new Armador();

            // Bolardos del borde sur de la explanada y del cordón este.
            foreach (var q in Puntos(dBolardos)) Bolardo(a, V3(q, q.x > 22f ? YVereda : 0f), negro, q.x > 22f ? 90f : 0f);

            // Cestos de residuos: cilindro de flejes negros sobre un pie.
            foreach (var q in Puntos(dCestos)) Cesto(a, V3(q, Dentro(explanada, q) ? 0f : YVereda), negro, grisOscuro);

            // Torres de reflectores que iluminan el Obelisco: dos caños blancos sobre bases negras,
            // apenas echados hacia atrás, con los reflectores entre los dos mirando al Obelisco.
            var losa = Mat("Losa de hormigón", "#b9b4a5", 0.1f);
            foreach (var q in Puntos(dArcos)) TorreDeReflectores(a, q, blanco, negro, grisOscuro, luces[3], losa);

            // Cartel del cantero noroeste (dos postes y el panel) y gabinete.
            {
                var c = Puntos(dCartel);
                Vector3 p0 = V3(c[0], YBordeCesped), p1 = V3(c[1], YBordeCesped);
                Vector3 centro = (p0 + p1) / 2f;
                var rot = Quaternion.LookRotation((p1 - p0).normalized, Vector3.up);
                float largo = Vector3.Distance(p0, p1);
                foreach (float t in new[] { 0.28f, 0.72f })
                    a.Cilindro(Vector3.Lerp(p0, p1, t), Vector3.Lerp(p0, p1, t) + Vector3.up * 3.1f, 0.09f, blanco);
                a.Caja(centro + Vector3.up * 2.25f, new Vector3(0.16f, 1.5f, largo), grisOscuro, rot);
                a.Caja(centro + Vector3.up * 2.25f, new Vector3(0.2f, 1.3f, largo - 0.2f), Mat("Panel del cartel", "#56606a", 0.5f), rot);
                a.Caja(V3(dGabinete, YBordeCesped) + Vector3.up * 0.55f, new Vector3(1.0f, 1.1f, 0.6f), crema);
            }

            // Señal sobre el césped del lado este.
            {
                var p = V3(dSenalEste, 0.3f);
                a.Cilindro(p, p + Vector3.up * 2.5f, 0.035f, gris);
                a.Caja(p + Vector3.up * 2.25f, new Vector3(0.5f, 0.5f, 0.03f), Mat("Señal azul", "#2f5f9e", 0.4f), Quaternion.Euler(0, 20f, 0));
            }

            // Semáforos de las esquinas.
            var semaforos = Puntos(dSemaforos);
            float[] giros = { 90f, 180f, -90f, 90f };
            for (int i = 0; i < semaforos.Count; i++) Semaforo(a, V3(semaforos[i], YVereda), giros[i], grisOscuro, cajaSem, amarilloSem, luces);
            Semaforo(a, new Vector3(AsfaltoOeste - 0.6f, 0f, -30.5f), -90f, grisOscuro, cajaSem, amarilloSem, luces);
            Semaforo(a, new Vector3(AsfaltoEste + 0.6f, 0f, -30.5f), 90f, grisOscuro, cajaSem, amarilloSem, luces);

            // Faroles altos de doble brazo: canteros de la avenida y veredas de la plaza.
            var faroles = new List<Vector3>();
            for (float z = -245f; z < 300f; z += 26f)
            {
                if (EnCruce(z, 4f)) continue;
                faroles.Add(new Vector3(-55f, 0.07f, z));
            }
            foreach (var tramo in TramosDeAvenida(caminoEste, -8.4f))
            {
                var s = Acumulado(tramo);
                for (float d = 9f; d < s[s.Length - 1] - 3f; d += 28f)
                {
                    Vector2 dir;
                    Vector2 p = PuntoEn(tramo, s, d, out dir);
                    faroles.Add(new Vector3(p.x, 0.04f, p.y));
                }
            }
            faroles.Add(V3(dFarolSO, YVereda));
            faroles.Add(new Vector3(24.4f, YVereda, -17.5f));
            foreach (var p in faroles) Farol(a, p, gris, luces[3]);

            // Parada del Metrobús, sobre el separador (al norte, donde lo muestra la foto).
            {
                var separador = Desplazar(caminoEste, false, -8.4f);
                var tramo = Entre(separador, 36f, 66f);
                var s = Acumulado(tramo);
                var plataforma = new MallaB();
                Barrido(plataforma, tramo, false, PerfilCaja(-1.75f, 1.75f, YAsfalto, 0.18f), true, 2f, 2f, true);
                plataforma.Crear("Plataforma del Metrobús", grupo, mats["Hormigón"]);
                for (float d = 1.5f; d < s[s.Length - 1] - 1f; d += 4.5f)
                {
                    Vector2 dir;
                    Vector2 p = PuntoEn(tramo, s, d, out dir);
                    var rot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.y), Vector3.up);
                    var b = new Vector3(p.x, 0.18f, p.y);
                    a.Cilindro(b, b + Vector3.up * 3.1f, 0.07f, gris);
                    a.Caja(b + Vector3.up * 3.2f, new Vector3(3.3f, 0.1f, 4.6f), vidrio, rot * Quaternion.Euler(0, 0, 5f));
                    a.Caja(b + Vector3.up * 1.3f + rot * new Vector3(0.9f, 0, 0), new Vector3(0.04f, 1.9f, 4.2f), vidrio, rot);
                }
            }

            // Reja baja alrededor del Obelisco.
            {
                const float lado = 5.6f;
                var c = PosicionObelisco;
                for (int k = 0; k < 4; k++)
                {
                    var rot = Quaternion.Euler(0, k * 90f, 0);
                    foreach (float y in new[] { 0.35f, 1.15f })
                        a.Caja(c + rot * new Vector3(0, y, lado), new Vector3(lado * 2f, 0.05f, 0.05f), negro, rot);
                    for (float x = -lado; x <= lado + 0.01f; x += 0.35f)
                        a.Caja(c + rot * new Vector3(x, 0.65f, lado), new Vector3(0.035f, 1.3f, 0.035f), negro, rot);
                }
            }

            a.Crear("Mobiliario urbano", grupo, true);
        }

        // =====================================================================
        //  Gente de referencia
        // =====================================================================

        /// <summary>
        /// Figuras de escala en tonos neutros, como las de una maqueta: algunas sentadas en el anillo
        /// mirando la explanada y otras paradas. Van en un objeto aparte ("Gente de referencia") para
        /// poder apagarlas desde la Jerarquía si molestan.
        /// </summary>
        static void Gente()
        {
            var tonos = new[] { Mat("Figura clara", "#d6d0c2", 0.15f), Mat("Figura media", "#bab3a6", 0.15f), Mat("Figura oscura", "#9d978d", 0.15f) };
            var a = new Armador();

            // Sentadas en el murete (a veces de a dos), mirando hacia adentro.
            var s = Acumulado(anillo);
            float largo = s[s.Length - 1];
            foreach (float t in new[] { 0.115f, 0.125f, 0.21f, 0.33f, 0.345f, 0.47f, 0.62f, 0.71f, 0.72f, 0.83f, 0.9f })
            {
                Vector2 dir;
                Vector2 q = PuntoEn(anillo, s, largo * t, out dir);
                var adentro = new Vector2(dir.y, -dir.x);   // la explanada queda a la derecha del recorrido
                float giro = Mathf.Atan2(adentro.x, adentro.y) * Mathf.Rad2Deg + Random.Range(-18f, 18f);
                Figura(a, V3(q + adentro * 0.05f, AnilloAlto), giro, true, tonos[Random.Range(0, tonos.Length)]);
            }
            // Paradas: frente al cartel BA, en las esquinas y junto al anillo.
            foreach (var q in Puntos(dGenteDePie))
                Figura(a, V3(q, Dentro(explanada, q) ? 0f : YVereda), Random.Range(0f, 360f), false, tonos[Random.Range(0, tonos.Length)]);
            a.Crear("Gente de referencia", raiz, true);
        }

        /// <summary>
        /// Figura humana simple de 1,70 m. <paramref name="giro"/>: hacia dónde mira. Sentada: el punto
        /// dado es el asiento y las piernas cuelgan hacia adelante.
        /// </summary>
        static void Figura(Armador a, Vector3 p, float giro, bool sentada, Material m)
        {
            var r = Quaternion.Euler(0, giro, 0);
            float e = Random.Range(0.93f, 1.05f);
            System.Func<float, float, float, Vector3> en = (x, y, z) => p + r * (new Vector3(x, y, z) * e);
            // Altura de la cadera sobre el punto dado y cuánto se echa el cuerpo hacia atrás.
            float cadera = sentada ? 0.1f : 0.88f, atras = sentada ? -0.06f : 0f;
            foreach (float l in new[] { -1f, 1f })
            {
                if (sentada)
                {
                    a.Cilindro(en(l * 0.09f, 0.09f, -0.1f), en(l * 0.1f, 0.09f, 0.4f), 0.085f * e, m, tubo10);    // muslo
                    a.Cilindro(en(l * 0.1f, 0.12f, 0.4f), en(l * 0.1f, -0.46f, 0.43f), 0.065f * e, m, tubo10);    // pierna
                    a.Cilindro(en(l * 0.2f, cadera + 0.5f, atras), en(l * 0.17f, 0.2f, 0.2f), 0.048f * e, m, tubo10);  // brazo apoyado
                }
                else
                {
                    a.Cilindro(en(l * 0.085f, -0.02f, 0f), en(l * 0.095f, cadera + 0.04f, 0f), 0.078f * e, m, tubo10);
                    a.Cilindro(en(l * 0.21f, cadera + 0.5f, 0f), en(l * 0.24f, cadera - 0.06f, 0.03f), 0.048f * e, m, tubo10);
                }
            }
            // Tronco (cilindro achatado), hombros, cuello y cabeza.
            a.Malla(cilindro, Matrix4x4.TRS(en(0, cadera + 0.27f, atras), r, new Vector3(0.36f, 0.27f, 0.22f) * e), m);
            a.Malla(esfera, Matrix4x4.TRS(en(0, cadera + 0.5f, atras), r, new Vector3(0.44f, 0.18f, 0.24f) * e), m);
            a.Cilindro(en(0, cadera + 0.52f, atras), en(0, cadera + 0.64f, atras), 0.05f * e, m, tubo10);
            a.Malla(esfera, Matrix4x4.TRS(en(0, cadera + 0.72f, atras + 0.01f), r, new Vector3(0.19f, 0.23f, 0.21f) * e), m);
        }

        /// <summary>Bolardo negro con la punta inclinada (como los de Corrientes).</summary>
        static void Bolardo(Armador a, Vector3 p, Material m, float giro)
        {
            var r = Quaternion.Euler(0, giro, 0);
            a.Caja(p + new Vector3(0, 0.38f, 0), new Vector3(0.24f, 0.76f, 0.18f), m, r);
            a.Caja(p + r * new Vector3(0, 0.8f, -0.02f), new Vector3(0.24f, 0.14f, 0.18f), m, r * Quaternion.Euler(-28f, 0, 0));
        }

        static void Cesto(Armador a, Vector3 p, Material negro, Material gris)
        {
            a.Cilindro(p, p + Vector3.up * 0.03f, 0.2f, negro);
            a.Cilindro(p, p + Vector3.up * 0.14f, 0.06f, negro);
            a.Cilindro(p + Vector3.up * 0.12f, p + Vector3.up * 0.88f, 0.25f, gris);
            a.Cilindro(p + Vector3.up * 0.84f, p + Vector3.up * 0.91f, 0.275f, negro);
            a.Cilindro(p + Vector3.up * 0.12f, p + Vector3.up * 0.18f, 0.265f, negro);
            // Flejes verticales.
            for (int i = 0; i < 12; i++)
            {
                float ang = i * 30f * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * 0.255f;
                a.Caja(p + d + Vector3.up * 0.51f, new Vector3(0.045f, 0.66f, 0.02f), negro, Quaternion.Euler(0, 90f - i * 30f, 0));
            }
        }

        static void TorreDeReflectores(Armador a, Vector2 basePlanta, Material blanco, Material negro, Material gris, Material luz, Material losa)
        {
            Vector2 alObelisco = (Obelisco - basePlanta).normalized;
            var frente = new Vector3(alObelisco.x, 0, alObelisco.y);
            var rot = Quaternion.LookRotation(frente, Vector3.up);     // +Z local = hacia el Obelisco
            var p = new Vector3(basePlanta.x, 0f, basePlanta.y);
            const float alto = 9.2f, separacion = 1.0f, atras = 1.6f;
            // Losa de apoyo.
            a.Caja(p + new Vector3(0, 0.02f, 0) + rot * new Vector3(0, 0, -0.2f), new Vector3(2.2f, 0.04f, 2.6f), losa, rot);
            System.Func<float, float, Vector3> punto = (t, lado) => p + rot * new Vector3(lado, alto * t, -atras * t * t);
            foreach (float lado in new[] { -separacion / 2f, separacion / 2f })
            {
                // Base negra con pinches (para que nadie se siente) y caño.
                a.Caja(punto(0, lado) + Vector3.up * 0.22f, new Vector3(0.52f, 0.44f, 0.52f), negro, rot);
                a.Malla(cono, Matrix4x4.TRS(punto(0, lado) + Vector3.up * 0.44f, rot, new Vector3(0.5f, 0.3f, 0.5f)), negro);
                for (int i = 0; i < 4; i++)
                    a.Malla(cono, Matrix4x4.TRS(punto(0, lado) + Vector3.up * 0.44f + rot * new Vector3((i % 2 - 0.5f) * 0.36f, 0, (i / 2 - 0.5f) * 0.36f), rot, new Vector3(0.07f, 0.16f, 0.07f)), negro);
                const int tramos = 12;
                for (int i = 0; i < tramos; i++)
                    a.Cilindro(punto((float)i / tramos, lado), punto((float)(i + 1) / tramos, lado), 0.1f, blanco);
                a.Malla(esfera, Matrix4x4.TRS(punto(1f, lado), Quaternion.identity, Vector3.one * 0.22f), blanco);
            }
            // Travesaños y reflectores (seis, escalonados hacia arriba).
            for (int i = 0; i < 6; i++)
            {
                float t = 0.36f + i * 0.118f;
                Vector3 c = punto(t, 0f);
                a.Caja(c, new Vector3(separacion, 0.06f, 0.06f), blanco, rot);
                var apunta = rot * Quaternion.Euler(-12f - i * 3f, 0, 0);
                a.Caja(c + rot * new Vector3(0, 0.02f, 0.22f), new Vector3(0.74f, 0.34f, 0.3f), gris, apunta);
                a.Caja(c + rot * new Vector3(0, 0.02f, 0.22f) + apunta * new Vector3(0, 0, 0.16f), new Vector3(0.64f, 0.26f, 0.02f), luz, apunta);
            }
        }

        static void Semaforo(Armador a, Vector3 p, float giro, Material poste, Material caja, Material marco, Material[] luces)
        {
            var r = Quaternion.Euler(0, giro, 0);
            a.Cilindro(p, p + Vector3.up * 3.3f, 0.07f, poste);
            a.Caja(p + Vector3.up * 3.75f + r * new Vector3(0, 0, 0.02f), new Vector3(0.52f, 1.2f, 0.06f), marco, r);
            a.Caja(p + Vector3.up * 3.75f, new Vector3(0.38f, 1.05f, 0.3f), caja, r);
            for (int i = 0; i < 3; i++)
                a.Malla(esfera, Matrix4x4.TRS(p + Vector3.up * (4.1f - i * 0.34f) + r * new Vector3(0, 0, -0.16f), r, Vector3.one * 0.22f), luces[i]);
            a.Caja(p + Vector3.up * 2.3f + r * new Vector3(0, 0, -0.05f), new Vector3(0.3f, 0.55f, 0.22f), caja, r);
        }

        static void Farol(Armador a, Vector3 p, Material gris, Material luz)
        {
            a.Cilindro(p, p + Vector3.up * 10f, 0.13f, gris);
            foreach (float s in new[] { -1f, 1f })
            {
                var codo = p + Vector3.up * 10f;
                var punta = codo + new Vector3(s * 2.2f, 0.6f, 0);
                a.Cilindro(codo, punta, 0.07f, gris);
                a.Caja(punta + new Vector3(0, -0.12f, 0), new Vector3(0.9f, 0.18f, 0.4f), gris);
                a.Caja(punta + new Vector3(0, -0.23f, 0), new Vector3(0.7f, 0.04f, 0.3f), luz);
            }
        }

        // =====================================================================
        //  Mástil con bandera y cartel BA de plantas
        // =====================================================================

        static void MastilYCartel()
        {
            var grupo = new GameObject("Mástil y cartel BA").transform;
            grupo.SetParent(raiz, false);
            var a = new Armador();
            var blanco = Mat("Mástil", "#eeebe4", 0.5f);
            var piedra = Mat("Base mástil", "#e2ddd2", 0.15f);
            // El mástil está pegado al murete, del lado del césped, en lo más hondo de la curva.
            var mastil = new Vector3(dMastil.x, 0f, dMastil.y + 0.55f);
            a.Caja(mastil + new Vector3(0, 0.3f, 0), new Vector3(2.5f, 0.6f, 2.0f), piedra);
            a.Caja(mastil + new Vector3(0, 0.85f, 0), new Vector3(1.9f, 0.5f, 1.5f), piedra);
            a.Caja(mastil + new Vector3(0, 1.3f, 0), new Vector3(1.1f, 0.4f, 1.1f), piedra);
            a.Cilindro(mastil + Vector3.up * 1.5f, mastil + Vector3.up * 13f, 0.2f, blanco);
            a.Cilindro(mastil + Vector3.up * 13f, mastil + Vector3.up * 23f, 0.15f, blanco);
            a.Cilindro(mastil + Vector3.up * 23f, mastil + Vector3.up * 31f, 0.1f, blanco);
            a.Malla(esfera, Matrix4x4.TRS(mastil + Vector3.up * 31.2f, Quaternion.identity, Vector3.one * 0.45f), blanco);
            a.Crear("Mástil", grupo, true);

            // Bandera: caja fina con la textura (se ve de los dos lados), un poco inclinada por el viento.
            var bandera = new GameObject("Bandera", typeof(MeshFilter), typeof(MeshRenderer));
            bandera.transform.SetParent(grupo, false);
            bandera.transform.SetPositionAndRotation(mastil + new Vector3(3.3f, 28.6f, 0), Quaternion.Euler(0, 8f, -4f));
            bandera.transform.localScale = new Vector3(6.4f, 4.0f, 0.04f);
            bandera.GetComponent<MeshFilter>().sharedMesh = cubo;
            bandera.GetComponent<MeshRenderer>().sharedMaterial = MatTex("Bandera", "#ffffff", Guardar(TexturaBandera(), "Bandera.asset"), 0.2f);

            // Cartel BA: dos letras de plantas sobre un cantero bajo, mirando al norte (hacia los
            // senderos; desde ahí se lee "BA" con el Obelisco de fondo).
            var c = new Armador();
            var follaje = mats["Follaje oscuro"];
            var follaje2 = mats["Follaje medio"];
            var cantero = Mat("Cantero del cartel", "#8a7a6a", 0.1f);
            string[] b = { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." };
            string[] l = { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" };
            const float altoLetra = 4.0f;
            Rect todo = new Rect(dLetraA.xMin - 0.5f, dLetraA.yMin - 0.4f, dLetraB.xMax - dLetraA.xMin + 1f, dLetraA.height + 0.8f);
            c.Caja(new Vector3(todo.center.x, 0.16f, todo.center.y), new Vector3(todo.width, 0.32f, todo.height), cantero);
            Letra(c, l, dLetraA, altoLetra, follaje, follaje2);
            Letra(c, b, dLetraB, altoLetra, follaje, follaje2);
            c.Crear("Cartel BA (plantas)", grupo, true, Canales.ConRelieve);
        }

        /// <summary>
        /// Letra de plantas: cada celda del dibujo es un bloque de seto con bultos de follaje. El
        /// cartel se lee desde el norte, así que la primera columna del dibujo queda al este.
        /// </summary>
        static void Letra(Armador c, string[] mapa, Rect huella, float alto, Material follaje, Material follaje2)
        {
            int filas = mapa.Length, columnas = mapa[0].Length;
            float cx = huella.width / columnas, cy = alto / filas;
            for (int fila = 0; fila < filas; fila++)
            for (int col = 0; col < columnas; col++)
            {
                if (mapa[fila][col] != '#') continue;
                float x = huella.xMax - (col + 0.5f) * cx;
                float y = 0.32f + (filas - 1 - fila + 0.5f) * cy;
                var centro = new Vector3(x, y, huella.center.y);
                c.Caja(centro, new Vector3(cx + 0.02f, cy + 0.02f, huella.height), follaje);
                for (int k = 0; k < 5; k++)
                {
                    // Cuatro bultos sobre las caras norte y sur, y uno adentro para que no quede hueco entre celdas.
                    var off = k < 4
                        ? new Vector3(Random.Range(-0.5f, 0.5f) * cx, Random.Range(-0.5f, 0.5f) * cy, (k % 2 == 0 ? -1f : 1f) * huella.height * 0.5f * Random.Range(0.75f, 1f))
                        : new Vector3(Random.Range(-0.4f, 0.4f) * cx, Random.Range(-0.4f, 0.4f) * cy, Random.Range(-0.4f, 0.4f) * huella.height);
                    float s = Random.Range(0.55f, 0.85f);
                    c.Malla(matasSimples[Random.Range(0, matasSimples.Count)], Matrix4x4.TRS(centro + off, Random.rotation, new Vector3(s, s, s * 0.7f)), Random.value < 0.6f ? follaje : follaje2);
                }
            }
        }

        // =====================================================================
        //  Edificios con detalle de fachada
        // =====================================================================

        static void Edificios()
        {
            var edif = new GameObject("Edificios").transform;
            edif.SetParent(raiz, false);
            foreach (float lado in new[] { -1f, 1f })
            {
                float x = lado < 0 ? FachadaOeste : FachadaEste;
                float z = -250f;
                while (z < 292f)
                {
                    float ancho = Random.Range(11f, 24f);
                    bool enHueco = false;
                    for (int i = 0; i < Cruces.Length; i += 2)
                        if (z + ancho > Cruces[i] && z < Cruces[i + 1]) { enHueco = true; z = Cruces[i + 1] + 0.5f; break; }
                    if (enHueco) continue;
                    // Esquinas de Corrientes: carteles luminosos.
                    bool carteles = z > -100f && z < 10f && (lado > 0 || Random.value < 0.4f);
                    int detalle = Mathf.Abs(z) < 130f ? 2 : 1;
                    Edificio(edif, new Vector3(x, 0, z + ancho / 2f), lado < 0 ? 90f : -90f, ancho, detalle, carteles);
                    z += ancho;
                }
            }
        }

        /// <summary>
        /// Edificio con la fachada mirando a +Z local (hacia la avenida) y el volumen hacia -Z.
        /// Tipos: clásico porteño (marcos, balcones, cornisas, mansarda), moderno (bandas), torre vidriada.
        /// </summary>
        static void Edificio(Transform padre, Vector3 frente, float giro, float ancho, int detalle, bool carteles)
        {
            float tipo = Random.value;
            string pared = Paredes[Random.Range(0, Paredes.Length)];
            ColorUtility.TryParseHtmlString(pared, out var colorPared);
            var mPared = Mat("Pared " + pared, pared, 0.12f);
            var mMolduras = Mat("Moldura " + pared, "#" + ColorUtility.ToHtmlStringRGB(Color.Lerp(colorPared, Color.white, 0.35f)), 0.15f);
            var mBase = Mat("Basamento " + pared, "#" + ColorUtility.ToHtmlStringRGB(colorPared * 0.78f), 0.12f);
            var vidrio = Mat("Vidrio ventana", "#5d6b7a", 0.75f);
            var metal = Mat("Herrería", "#3b3a39", 0.4f);
            var aire = Mat("Aire acondicionado", "#e6e5df", 0.3f);
            var persiana = Mat("Persiana metálica", "#8e8c88", 0.35f);
            var techo = Mat("Techo", "#7c746c", 0.1f);

            const float pb = 4.6f, piso = 3.3f, bahia = 3.4f, prof = 18f;
            int pisos = tipo < 0.15f ? Random.Range(16, 26) : Random.Range(6, 13);
            float alto = pb + pisos * piso;
            int bahias = Mathf.Max(2, Mathf.FloorToInt((ancho - 1f) / bahia));
            float paso = (ancho - 1f) / bahias;
            float x0 = -ancho / 2f + 0.5f + paso / 2f;

            var a = new Armador();
            a.Caja(new Vector3(0, alto / 2f, -prof / 2f), new Vector3(ancho - 0.2f, alto, prof), tipo < 0.15f ? Mat("Torre vidriada", "#9aaab6", 0.8f) : mPared);

            // Planta baja: basamento, vidrieras o persianas, toldos.
            a.Caja(new Vector3(0, pb / 2f, 0.12f), new Vector3(ancho - 0.2f, pb, 0.25f), mBase);
            for (int i = 0; i < bahias; i++)
            {
                float x = x0 + i * paso;
                bool cerrado = Random.value < 0.3f;
                a.Caja(new Vector3(x, 1.7f, 0.27f), new Vector3(paso - 0.8f, 3f, 0.06f), cerrado ? persiana : vidrio);
                if (cerrado && detalle > 1)
                    for (float y = 0.4f; y < 3.1f; y += 0.3f) a.Caja(new Vector3(x, y, 0.31f), new Vector3(paso - 0.85f, 0.04f, 0.02f), mBase);
                if (!cerrado && Random.value < 0.35f)
                {
                    var toldo = new[] { "#b8664b", "#5f7d55", "#4b5d7a", "#c9a24a" }[Random.Range(0, 4)];
                    a.Caja(new Vector3(x, 3.55f, 0.8f), new Vector3(paso - 0.6f, 0.08f, 1.3f), Mat("Toldo " + toldo, toldo, 0.2f), Quaternion.Euler(-14f, 0, 0));
                }
            }
            a.Caja(new Vector3(0, pb + 0.15f, 0.25f), new Vector3(ancho, 0.3f, 0.5f), mMolduras);

            if (tipo < 0.15f)
            {
                // Torre vidriada: parantes verticales y líneas de losa.
                for (float x = -ancho / 2f + 0.8f; x < ancho / 2f - 0.5f; x += 1.6f)
                    a.Caja(new Vector3(x, (pb + alto) / 2f, 0.05f), new Vector3(0.1f, alto - pb, 0.15f), Mat("Parante", "#cfd6db", 0.5f));
                for (int f = 1; f <= pisos; f++)
                    a.Caja(new Vector3(0, pb + f * piso, 0.05f), new Vector3(ancho - 0.2f, 0.12f, 0.18f), Mat("Parante", "#cfd6db", 0.5f));
            }
            else if (tipo < 0.4f)
            {
                // Moderno: bandas de ventanas y balcones corridos.
                for (int f = 0; f < pisos; f++)
                {
                    float y = pb + f * piso;
                    a.Caja(new Vector3(0, y + 1.55f, 0.03f), new Vector3(ancho - 1.2f, 1.9f, 0.05f), vidrio);
                    if (f % 2 == 1)
                    {
                        a.Caja(new Vector3(0, y + 0.1f, 0.6f), new Vector3(ancho - 0.8f, 0.2f, 1.2f), mMolduras);
                        a.Caja(new Vector3(0, y + 0.65f, 1.18f), new Vector3(ancho - 0.8f, 0.9f, 0.04f), Mat("Baranda vidrio", "#b7cad4", 0.8f));
                    }
                }
            }
            else
            {
                // Clásico porteño: ventanas con marco, dintel y alféizar; balcones; bandas cada dos pisos.
                for (int f = 0; f < pisos; f++)
                {
                    float y = pb + f * piso;
                    bool balcones = detalle > 1 && (f == 0 || f == pisos - 1 || (f % 3 == 1 && Random.value < 0.5f));
                    for (int i = 0; i < bahias; i++)
                    {
                        float x = x0 + i * paso;
                        a.Caja(new Vector3(x, y + 1.6f, 0.03f), new Vector3(1.3f, 2f, 0.05f), vidrio);
                        if (detalle > 1)
                        {
                            a.Caja(new Vector3(x, y + 0.55f, 0.12f), new Vector3(1.65f, 0.12f, 0.24f), mMolduras);      // alféizar
                            a.Caja(new Vector3(x, y + 2.72f, 0.1f), new Vector3(1.7f, 0.22f, 0.2f), mMolduras);        // dintel
                            a.Caja(new Vector3(x - 0.72f, y + 1.6f, 0.06f), new Vector3(0.14f, 2.1f, 0.12f), mMolduras); // marcos
                            a.Caja(new Vector3(x + 0.72f, y + 1.6f, 0.06f), new Vector3(0.14f, 2.1f, 0.12f), mMolduras);
                            if (Random.value < 0.12f) // aire acondicionado debajo de la ventana
                            {
                                a.Caja(new Vector3(x + 0.25f, y + 0.25f, 0.22f), new Vector3(0.8f, 0.5f, 0.38f), aire);
                                a.Malla(cilindro, Matrix4x4.TRS(new Vector3(x + 0.25f, y + 0.25f, 0.42f), Quaternion.Euler(90, 0, 0), new Vector3(0.36f, 0.01f, 0.36f)), metal);
                            }
                        }
                        if (balcones)
                        {
                            a.Caja(new Vector3(x, y + 0.08f, 0.45f), new Vector3(1.9f, 0.16f, 0.9f), mMolduras);
                            a.Caja(new Vector3(x, y + 0.6f, 0.88f), new Vector3(1.9f, 0.9f, 0.04f), metal);
                        }
                    }
                    if (f % 2 == 1) a.Caja(new Vector3(0, y + piso, 0.1f), new Vector3(ancho - 0.2f, 0.22f, 0.2f), mMolduras);
                }
                // Caños de desagüe en los bordes.
                if (detalle > 1)
                    foreach (float s in new[] { -1f, 1f })
                        a.Caja(new Vector3(s * (ancho / 2f - 0.35f), alto / 2f, 0.08f), new Vector3(0.12f, alto, 0.12f), mBase);
            }

            // Cornisa con dentículos y remate.
            a.Caja(new Vector3(0, alto + 0.3f, -prof / 2f + 0.35f), new Vector3(ancho + 0.2f, 0.6f, prof + 0.7f), mMolduras);
            if (detalle > 1 && tipo >= 0.4f)
                for (float x = -ancho / 2f + 0.4f; x < ancho / 2f - 0.2f; x += 0.6f)
                    a.Caja(new Vector3(x, alto - 0.12f, 0.2f), new Vector3(0.22f, 0.25f, 0.25f), mMolduras);

            // Techo: mansarda en los clásicos, tanques y aires en todos.
            if (tipo >= 0.4f && Random.value < 0.5f)
            {
                a.Caja(new Vector3(0, alto + 2.1f, -prof / 2f), new Vector3(ancho - 1.4f, 3f, prof - 2f), techo);
                for (int i = 0; i < bahias; i += 2)
                    a.Caja(new Vector3(x0 + i * paso, alto + 2.1f, 0.3f - 1f), new Vector3(1.1f, 1.6f, 0.9f), mMolduras);
            }
            else
            {
                a.Caja(new Vector3(-ancho * 0.2f, alto + 1.5f, -prof * 0.6f), new Vector3(2.4f, 2.4f, 2.4f), aire);
                a.Malla(cilindro, Matrix4x4.TRS(new Vector3(ancho * 0.2f, alto + 1.4f, -prof * 0.4f), Quaternion.identity, new Vector3(1.8f, 0.9f, 1.8f)), techo);
            }

            // Carteles luminosos (como los de la esquina del Obelisco).
            if (carteles)
            {
                var colores = new[] { "#e0453b", "#2f6fd6", "#f2c230", "#1d1d1f", "#f0f0f0", "#7b3fd1", "#e2622e" };
                int n = Random.Range(1, 4);
                for (int i = 0; i < n; i++)
                {
                    var c = colores[Random.Range(0, colores.Length)];
                    float w = Mathf.Min(ancho - 1.5f, Random.Range(6f, 12f)), h = Random.Range(3.5f, 7f);
                    float y = i == 0 ? alto + h / 2f + 1f : pb + 2f + Random.Range(0.2f, 0.8f) * (alto - pb - h - 2f) + h / 2f;
                    a.Caja(new Vector3(Random.Range(-1f, 1f) * (ancho - w) / 2f, y, 0.5f), new Vector3(w, h, 0.4f), EmisivaSuave("Cartel " + c, c));
                }
            }

            var go = a.Crear("Edificio", padre, true);
            go.transform.SetPositionAndRotation(frente, Quaternion.Euler(0, giro, 0));
        }

        // =====================================================================
        //  Tránsito: autos, taxis y colectivos
        // =====================================================================

        static void Transito()
        {
            var a = new Armador();
            var ruedas = Mat("Rueda", "#26262a", 0.3f);
            var vidrio = Mat("Vidrio auto", "#6f8796", 0.8f);
            var coloresAuto = new[] { "#f0eee8", "#b8bcc2", "#6d7178", "#a8423c", "#3f5f8a", "#d8d2c4" };

            // Carriles que siguen la curva de la plaza. Mano derecha: el lado oeste va hacia el sur
            // y el este hacia el norte.
            var carriles = new List<(List<Vector2> camino, bool haciaElNorte, bool colectivos, int cantidad)>();
            foreach (float d in new[] { 1.95f, 5.15f, 8.2f, 13.1f, 16.4f, 19.6f, 22.9f, 26.1f }) carriles.Add((Desplazar(caminoOeste, false, d), false, false, 9));
            foreach (float d in new[] { -2.05f, -5.5f }) carriles.Add((Desplazar(caminoEste, false, d), true, true, 4));
            foreach (float d in new[] { -11.4f, -15f, -18.5f }) carriles.Add((Desplazar(caminoEste, false, d), true, false, 9));
            foreach (float x in new[] { 53.5f, 57f, 60.5f, 63.7f, 66.4f }) carriles.Add((new List<Vector2> { new Vector2(x, -255f), new Vector2(x, 305f) }, true, false, 8));
            foreach (float x in new[] { -58.9f, -61.6f }) carriles.Add((new List<Vector2> { new Vector2(x, -255f), new Vector2(x, 305f) }, false, false, 8));

            foreach (var (camino, haciaElNorte, colectivos, cantidad) in carriles)
            {
                var s = Acumulado(camino);
                float total = s[s.Length - 1];
                var usados = new List<float>();
                for (int k = 0; k < cantidad; k++)
                {
                    float d = Random.Range(6f, total - 6f);
                    bool libre = true;
                    foreach (float u in usados) if (Mathf.Abs(u - d) < (colectivos ? 16f : 7f)) libre = false;
                    if (!libre) continue;
                    Vector2 dir;
                    Vector2 p = PuntoEn(camino, s, d, out dir);
                    // Nada parado sobre las sendas ni en el cruce de Corrientes.
                    if (p.y > -58f && p.y < -8f) continue;
                    usados.Add(d);
                    if (!haciaElNorte) dir = -dir;
                    var r = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.y), Vector3.up);
                    Vehiculo(a, new Vector3(p.x, YAsfalto, p.y), r, colectivos || Random.value < 0.07f, Random.value < 0.4f, coloresAuto, ruedas, vidrio);
                }
            }
            // Corrientes: va hacia el este, rodeando el Obelisco.
            foreach (var (d, s) in new[] { (-2.2f, 9f), (-6.2f, 21f), (-9.7f, 33f), (-6.2f, 40f) })
            {
                var carril = Desplazar(bordeSur, false, d);
                Vector2 dir;
                Vector2 p = PuntoEn(carril, Acumulado(carril), s, out dir);
                Vehiculo(a, new Vector3(p.x, YAsfalto, p.y), Quaternion.LookRotation(new Vector3(dir.x, 0, dir.y), Vector3.up), false, d < -5f, coloresAuto, ruedas, vidrio);
            }
            foreach (var (x, z) in new[] { (-48f, -42.7f), (-38f, -46.3f), (44f, -42.7f), (58f, -46.3f), (-90f, -42.7f), (95f, -46.3f) })
                Vehiculo(a, new Vector3(x, YAsfalto, z), Quaternion.Euler(0, 90f, 0), false, Random.value < 0.5f, coloresAuto, ruedas, vidrio);
            a.Crear("Tránsito", raiz, true);
        }

        static void Vehiculo(Armador a, Vector3 p, Quaternion r, bool colectivo, bool taxi, string[] coloresAuto, Material ruedas, Material vidrio)
        {
            if (colectivo)
            {
                var color = new[] { "#2e7d4f", "#b3413a", "#2f5f9e", "#d8b23a" }[Random.Range(0, 4)];
                a.Caja(p + r * new Vector3(0, 1.75f, 0), new Vector3(2.5f, 2.9f, 12f), Mat("Colectivo " + color, color, 0.5f), r);
                a.Caja(p + r * new Vector3(0, 2.3f, 0), new Vector3(2.54f, 1f, 11f), vidrio, r);
                a.Caja(p + r * new Vector3(0, 0.75f, 0), new Vector3(2.54f, 0.5f, 12.02f), Mat("Franja blanca", "#f0eee8", 0.4f), r);
                foreach (float sz in new[] { -4f, 3.8f }) Ruedas(a, p, r, 1.2f, sz, 0.5f, ruedas);
            }
            else
            {
                var color = taxi ? "#1f1f22" : coloresAuto[Random.Range(0, coloresAuto.Length)];
                var carroceria = Mat("Auto " + color, color, 0.6f);
                var mtx = Matrix4x4.TRS(p, r, Vector3.one);
                a.Malla(autoCarroceria, mtx, carroceria);
                a.Malla(autoVidrios, mtx, vidrio);
                if (taxi) a.Caja(p + r * new Vector3(0, 1.45f, -0.18f), new Vector3(1.5f, 0.04f, 1.3f), Mat("Techo taxi", "#f2c230", 0.5f), r);
                foreach (float sz in new[] { -1.32f, 1.38f }) Ruedas(a, p, r, 0.8f, sz, 0.32f, ruedas);
            }
        }

        /// <summary>
        /// Auto de tres volúmenes (4,30 × 1,75 × 1,43 m, con el frente hacia +Z): la carrocería sale de
        /// un perfil lateral y los vidrios son paños apoyados encima.
        /// </summary>
        static void MallasDeAuto()
        {
            const float w = 0.875f;
            // Perfil lateral: (z, y), del paragolpes trasero al delantero pasando por el techo.
            var perfil = new[]
            {
                new Vector2(-2.15f, 0.26f), new Vector2(-2.15f, 0.78f), new Vector2(-1.98f, 0.98f), new Vector2(-1.3f, 1.02f), new Vector2(-0.85f, 1.43f),
                new Vector2(0.5f, 1.43f), new Vector2(1.05f, 1.0f), new Vector2(1.95f, 0.9f), new Vector2(2.15f, 0.72f), new Vector2(2.15f, 0.26f),
            };
            Vector2 centro = Vector2.zero;
            foreach (var q in perfil) centro += q;
            centro /= perfil.Length;
            var m = new MallaB();
            var tri = Triangular(perfil);
            foreach (float lado in new[] { -1f, 1f })
            {
                var normal = new Vector3(lado, 0, 0);
                int k = m.v.Count;
                foreach (var q in perfil) m.Vert(new Vector3(lado * w, q.y, q.x), normal, q);
                for (int i = 0; i < tri.Count; i += 3) m.Tri(k + tri[i], k + tri[i + 1], k + tri[i + 2], normal);
            }
            for (int i = 0; i < perfil.Length; i++)
            {
                Vector2 q0 = perfil[i], q1 = perfil[(i + 1) % perfil.Length];
                Vector2 d = (q1 - q0).normalized;
                var n2 = new Vector2(d.y, -d.x);
                if (Vector2.Dot(n2, (q0 + q1) * 0.5f - centro) < 0f) n2 = -n2;
                var normal = new Vector3(0, n2.y, n2.x);
                m.Cuad(new Vector3(-w, q0.y, q0.x), new Vector3(w, q0.y, q0.x), new Vector3(w, q1.y, q1.x), new Vector3(-w, q1.y, q1.x), normal,
                    new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
            }
            autoCarroceria = Guardar(m.Malla("Auto (carrocería)"), "Auto carrocería.asset");

            var v = new MallaB();
            // Luneta (tramo 3-4 del perfil) y parabrisas (tramo 5-6), apenas despegados de la chapa.
            foreach (var par in new[] { (perfil[3], perfil[4]), (perfil[5], perfil[6]) })
            {
                Vector2 q0 = Vector2.Lerp(par.Item1, par.Item2, 0.12f), q1 = Vector2.Lerp(par.Item1, par.Item2, 0.9f);
                Vector2 d = (q1 - q0).normalized;
                var n2 = new Vector2(d.y, -d.x);
                if (Vector2.Dot(n2, (q0 + q1) * 0.5f - centro) < 0f) n2 = -n2;
                var normal = new Vector3(0, n2.y, n2.x);
                Vector3 sep = normal * 0.012f;
                v.Cuad(new Vector3(-w + 0.1f, q0.y, q0.x) + sep, new Vector3(w - 0.1f, q0.y, q0.x) + sep, new Vector3(w - 0.1f, q1.y, q1.x) + sep, new Vector3(-w + 0.1f, q1.y, q1.x) + sep, normal,
                    new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
            }
            // Ventanillas de los costados.
            foreach (float lado in new[] { -1f, 1f })
            {
                var normal = new Vector3(lado, 0, 0);
                float x = lado * (w + 0.012f);
                v.Cuad(new Vector3(x, 1.06f, -1.18f), new Vector3(x, 1.38f, -0.83f), new Vector3(x, 1.38f, 0.46f), new Vector3(x, 1.06f, 0.9f), normal,
                    new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            }
            autoVidrios = Guardar(v.Malla("Auto (vidrios)"), "Auto vidrios.asset");
        }

        static void Ruedas(Armador a, Vector3 p, Quaternion r, float sx, float sz, float radio, Material m)
        {
            foreach (float s in new[] { -1f, 1f })
                a.Malla(tubo10, Matrix4x4.TRS(p + r * new Vector3(s * sx, radio, sz), r * Quaternion.Euler(0, 0, 90f), new Vector3(radio * 2f, 0.12f, radio * 2f)), m);
        }

        // =====================================================================
        //  Armador: junta muchas piezas en una sola malla por grupo (una submalla por material)
        // =====================================================================

        class Armador
        {
            readonly Dictionary<Material, List<CombineInstance>> partes = new Dictionary<Material, List<CombineInstance>>();

            public void Caja(Vector3 c, Vector3 t, Material m, Quaternion? r = null) =>
                Malla(cubo, Matrix4x4.TRS(c, r ?? Quaternion.identity, t), m);

            public void Cilindro(Vector3 desde, Vector3 hasta, float radio, Material m, Mesh forma = null)
            {
                var dir = hasta - desde;
                Malla(forma ?? cilindro, Matrix4x4.TRS((desde + hasta) / 2f, Quaternion.FromToRotation(Vector3.up, dir), new Vector3(radio * 2f, dir.magnitude / 2f, radio * 2f)), m);
            }

            public void Malla(Mesh malla, Matrix4x4 mtx, Material m)
            {
                if (!partes.TryGetValue(m, out var lista)) partes[m] = lista = new List<CombineInstance>();
                lista.Add(new CombineInstance { mesh = malla, transform = mtx });
            }

            public GameObject Crear(string nombre, Transform padre, bool proyectaSombra, Canales canales = Canales.Lisos)
            {
                var go = new GameObject(nombre, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(padre, false);
                int vertices = 0;
                foreach (var kv in partes)
                    foreach (var parte in kv.Value) vertices += parte.mesh.vertexCount;
                var formato = vertices > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                var subs = new List<CombineInstance>();
                var materiales = new List<Material>();
                foreach (var kv in partes)
                {
                    var sub = new Mesh { indexFormat = formato };
                    sub.CombineMeshes(kv.Value.ToArray(), true, true);
                    subs.Add(new CombineInstance { mesh = sub, transform = Matrix4x4.identity });
                    materiales.Add(kv.Key);
                }
                var final = new Mesh { name = nombre, indexFormat = formato };
                final.CombineMeshes(subs.ToArray(), false, false);
                // Solo quedan los datos que usan los materiales de este grupo.
                final.uv2 = null;
                if (canales == Canales.Lisos) final.uv = null;
                if (canales != Canales.ConRelieve) final.tangents = null;
                final.RecalculateBounds();
                foreach (var s in subs) Object.DestroyImmediate(s.mesh);
                go.GetComponent<MeshFilter>().sharedMesh = final;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterials = materiales.ToArray();
                if (!proyectaSombra) mr.shadowCastingMode = ShadowCastingMode.Off;
                partes.Clear();
                return go;
            }
        }

        /// <summary>Qué datos lleva cada vértice además de posición y normal.</summary>
        enum Canales { Lisos, ConUV, ConRelieve }

        /// <summary>
        /// Pintura sobre el asfalto: cada marca es un rectángulo plano (dos triángulos), agrupado por color.
        /// </summary>
        class Pintura
        {
            readonly Dictionary<Material, MallaB> capas = new Dictionary<Material, MallaB>();

            MallaB Capa(Material m)
            {
                if (!capas.TryGetValue(m, out var capa)) capas[m] = capa = new MallaB();
                return capa;
            }

            /// <summary>Rectángulo de <paramref name="ancho"/> (en X local) por <paramref name="largo"/> (en Z local), centrado y girado en planta.</summary>
            public void Rect(Vector3 centro, float ancho, float largo, float giro, Material m)
            {
                var r = Quaternion.Euler(0, giro, 0);
                Vector3 x = r * new Vector3(ancho / 2f, 0, 0), z = r * new Vector3(0, 0, largo / 2f);
                Capa(m).Cuad(centro - x - z, centro - x + z, centro + x + z, centro + x - z, Vector3.up, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            }

            /// <summary>Cinta recta de un punto a otro.</summary>
            public void Cinta(Vector3 p, Vector3 q, float ancho, Material m)
            {
                var d = q - p;
                d.y = 0f;
                if (d.sqrMagnitude < 1e-8f) return;
                var lado = new Vector3(-d.z, 0, d.x).normalized * (ancho / 2f);
                Capa(m).Cuad(p - lado, q - lado, q + lado, p + lado, Vector3.up, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            }

            public void Crear(string nombre, Transform padre)
            {
                foreach (var kv in capas) kv.Value.Crear(nombre + " (" + kv.Key.name + ")", padre, kv.Key, false);
                capas.Clear();
            }
        }

        // =====================================================================
        //  Mallas de base
        // =====================================================================

        static Mesh MallaPrimitiva(PrimitiveType tipo)
        {
            var tmp = GameObject.CreatePrimitive(tipo);
            var m = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            return m;
        }

        /// <summary>Icosaedro subdividido (radio 0,5). Devuelve vértices compartidos y triángulos.</summary>
        static void Icosaedro(int subdivisiones, out List<Vector3> v, out List<int> tri)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var vv = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            var f = new List<int> { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                                    3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
            for (int pasada = 0; pasada < subdivisiones; pasada++)
            {
                var medios = new Dictionary<long, int>();
                System.Func<int, int, int> medio = (a, b) =>
                {
                    long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    int i;
                    if (medios.TryGetValue(k, out i)) return i;
                    vv.Add((vv[a] + vv[b]) * 0.5f);
                    medios[k] = vv.Count - 1;
                    return vv.Count - 1;
                };
                var nuevo = new List<int>();
                for (int i = 0; i < f.Count; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2];
                    int ab = medio(a, b), bc = medio(b, c), ca = medio(c, a);
                    nuevo.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                f = nuevo;
            }
            for (int i = 0; i < vv.Count; i++) vv[i] = vv[i].normalized * 0.5f;
            v = vv; tri = f;
        }

        /// <summary>Malla cerrada con normales suaves hacia afuera y UV envolvente (para follaje y esferas).</summary>
        static Mesh Redonda(string nombre, List<Vector3> v, List<int> tri, float vueltasUV)
        {
            var m = new MallaB();
            foreach (var p in v)
            {
                var d = p.normalized;
                m.Vert(p, d, new Vector2(Mathf.Atan2(d.z, d.x) / (2f * Mathf.PI) * vueltasUV, d.y * 0.5f * vueltasUV * 0.5f + p.y));
            }
            for (int i = 0; i < tri.Count; i += 3)
            {
                Vector3 centro = (v[tri[i]] + v[tri[i + 1]] + v[tri[i + 2]]) / 3f;
                m.Tri(tri[i], tri[i + 1], tri[i + 2], centro);
            }
            m.NormalesSuaves(0, 0);
            return m.Malla(nombre);
        }

        static Mesh EsferaLisa()
        {
            Icosaedro(2, out var v, out var tri);
            return Redonda("Esfera lisa", v, tri, 2f);
        }

        /// <summary>Masa de follaje: esfera con bultos (radio ~0,5), de sombreado suave. Base de copas, arbustos y setos.</summary>
        static Mesh Mata(int semilla, int subdivisiones)
        {
            Icosaedro(subdivisiones, out var v, out var tri);
            float ox = semilla * 1.37f, oy = semilla * 2.11f;
            for (int i = 0; i < v.Count; i++)
            {
                var d = v[i].normalized;
                float bulto = Mathf.PerlinNoise(d.x * 2.3f + ox + 5f, d.y * 2.3f + oy + 5f) * 0.6f + Mathf.PerlinNoise(d.z * 4.1f + ox + 9f, d.x * 4.1f + oy + 3f) * 0.4f;
                v[i] = d * 0.5f * (0.78f + 0.44f * bulto);
            }
            return Redonda("Mata", v, tri, 3f);
        }

        /// <summary>Cilindro (alto 2, diámetro 1, como el primitivo de Unity) con costado suave y tapas planas.</summary>
        static Mesh CilindroBajo(int lados, bool conTapas)
        {
            var m = new MallaB();
            for (int i = 0; i <= lados; i++)
            {
                float ang = i * Mathf.PI * 2f / lados;
                var d = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                m.Vert(d * 0.5f + Vector3.down, d, new Vector2((float)i / lados, 0));
                m.Vert(d * 0.5f + Vector3.up, d, new Vector2((float)i / lados, 1));
            }
            for (int i = 0; i < lados; i++)
            {
                int b0 = i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                Vector3 hacia = m.n[b0] + m.n[b1];
                m.Tri(b0, t0, t1, hacia);
                m.Tri(b0, t1, b1, hacia);
            }
            if (conTapas)
            foreach (float y in new[] { -1f, 1f })
            {
                int centro = m.Vert(new Vector3(0, y, 0), new Vector3(0, y, 0), new Vector2(0.5f, 0.5f));
                int k = m.v.Count;
                for (int i = 0; i <= lados; i++)
                {
                    float ang = i * Mathf.PI * 2f / lados;
                    m.Vert(new Vector3(Mathf.Cos(ang) * 0.5f, y, Mathf.Sin(ang) * 0.5f), new Vector3(0, y, 0), new Vector2(Mathf.Cos(ang) * 0.5f + 0.5f, Mathf.Sin(ang) * 0.5f + 0.5f));
                }
                for (int i = 0; i < lados; i++) m.Tri(centro, k + i, k + i + 1, new Vector3(0, y, 0));
            }
            return m.Malla("Cilindro");
        }

        /// <summary>Cono: base de diámetro 1 en y = 0, punta en y = 1.</summary>
        static Mesh ConoBajo(int lados)
        {
            var m = new MallaB();
            for (int i = 0; i < lados; i++)
            {
                float a0 = i * Mathf.PI * 2f / lados, a1 = (i + 1) * Mathf.PI * 2f / lados;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0, Mathf.Sin(a0) * 0.5f);
                var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0, Mathf.Sin(a1) * 0.5f);
                var normal = Vector3.Cross(p1 - p0, Vector3.up - p0).normalized;
                if (Vector3.Dot(normal, p0 + p1) < 0) normal = -normal;
                int k = m.v.Count;
                m.Vert(p0, normal, new Vector2(0, 0)); m.Vert(p1, normal, new Vector2(1, 0)); m.Vert(Vector3.up, normal, new Vector2(0.5f, 1));
                m.Tri(k, k + 1, k + 2, normal);
                k = m.v.Count;
                m.Vert(Vector3.zero, Vector3.down, new Vector2(0.5f, 0.5f)); m.Vert(p0, Vector3.down, new Vector2(0, 0)); m.Vert(p1, Vector3.down, new Vector2(1, 0));
                m.Tri(k, k + 1, k + 2, Vector3.down);
            }
            return m.Malla("Cono");
        }

        static List<Vector2> Elipse(Vector2 c, float rx, float rz, int n)
        {
            var p = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                p.Add(c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * rz));
            }
            return p;
        }

        // =====================================================================
        //  Materiales
        // =====================================================================

        static Material Mat(string nombre, string hex, float suavidad) => MatTex(nombre, hex, null, suavidad);

        static Material MatTex(string nombre, string hex, Texture2D tex, float suavidad)
        {
            if (mats.TryGetValue(nombre, out var m)) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = nombre };
            ColorUtility.TryParseHtmlString(hex, out var c);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", suavidad);
            if (tex != null) { m.SetTexture("_BaseMap", tex); conTextura.Add(m); }
            var archivo = string.Join("_", nombre.Split(System.IO.Path.GetInvalidFileNameChars())).Replace("#", "");
            mats[nombre] = Guardar(m, archivo + ".mat");
            return m;
        }

        /// <summary>Material de suelo o follaje: textura, relieve opcional (mapa de normales) y un tinte.</summary>
        static Material MatSuelo(string nombre, Texture2D tex, Texture2D normales, float suavidad, string tinte = "#ffffff")
        {
            if (mats.TryGetValue(nombre, out var existente)) return existente;
            var m = MatTex(nombre, tinte, tex, suavidad);
            if (normales != null)
            {
                m.SetTexture("_BumpMap", normales);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
                conRelieve.Add(m);
            }
            m.SetFloat("_SpecularHighlights", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Emisiva(string nombre, string hex) => ConEmision(MatTex(nombre, hex, null, 0.6f), hex, 1.6f);
        static Material EmisivaSuave(string nombre, string hex) => ConEmision(MatTex(nombre, hex, null, 0.5f), hex, 0.55f);

        static Material ConEmision(Material m, string hex, float fuerza)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * fuerza);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        static T Guardar<T>(T asset, string archivo) where T : Object
        {
            var ruta = $"{Carpeta}/{archivo}";
            if (AssetDatabase.LoadAssetAtPath<Object>(ruta) != null) AssetDatabase.DeleteAsset(ruta);
            AssetDatabase.CreateAsset(asset, ruta);
            creados.Add(ruta);
            creadosAhora.Add(asset);
            return asset;
        }

        /// <summary>Bandera argentina: celeste, blanco, celeste y el sol.</summary>
        static Texture2D TexturaBandera()
        {
            const int w = 256, h = 160;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, name = "Bandera" };
            var celeste = new Color(0.455f, 0.675f, 0.875f);
            var sol = new Color(0.96f, 0.72f, 0.2f);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = (y < h / 3 || y >= 2 * h / 3) ? celeste : Color.white;
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(w / 2f, h / 2f));
                if (d < h * 0.1f) c = sol;
                else if (d < h * 0.15f && Mathf.Repeat(Mathf.Atan2(y - h / 2f, x - w / 2f) * 16f / Mathf.PI, 2f) < 1f) c = sol;
                px[y * w + x] = c;
            }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }
    }
}
