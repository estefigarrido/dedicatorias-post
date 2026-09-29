using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CuboPost.EditorTools
{
    /// <summary>
    /// Entorno del cubo: Plaza de la República (Obelisco, Buenos Aires), estilo low-poly facetado.
    /// Medidas tomadas del mapa (Google Maps, ~4 px por metro). Cubo en el origen, frente hacia -Z (sur).
    ///   · Plaza en cuña entre Av. 9 de Julio (oeste) y el Metrobus (este); borde sur = Av. Corrientes.
    ///   · Obelisco al sur, cruzando Corrientes. Cartel BA de plantas y mástil ~37 m al norte del cubo.
    ///   · Oeste: carriles centrales con bicisenda, cantero central con árboles, Cerrito, edificios.
    ///   · Este: Metrobus con parada, carriles de 9 de Julio, cantero, Carlos Pellegrini, edificios con carteles.
    /// </summary>
    public static class ConstruirEntorno
    {
        const string Carpeta = "Assets/CuboPost/Generado/Entorno";

        // Bordes (metros): X oeste-este, Z sur-norte.
        const float FachadaOeste = -78f, FachadaEste = 83f;       // línea de edificios
        const float AsfaltoOeste = -74f, AsfaltoEste = 79f;
        const float CorrientesNorte = -12.5f, CorrientesSur = -28f; // calzada entre plaza e isla del Obelisco
        static readonly Vector2 Obelisco = new Vector2(4f, -38f);

        // Plaza (cuña convexa, sentido antihorario visto desde arriba).
        static readonly Vector2[] Plaza = { new Vector2(-26f, -12.5f), new Vector2(35f, -12.5f), new Vector2(33f, 12f), new Vector2(1f, 100f), new Vector2(-19f, 100f) };

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static Mesh cubo, cilindro, cono;
        static readonly List<Mesh> icosferas = new List<Mesh>();
        static Transform raiz;

        // Paleta tomada de las fotos de Street View (tonos cálidos y apagados).
        static readonly string[] Paredes = { "#e7dfcf", "#d9cdb4", "#cfc5b3", "#e3d6bd", "#c9c1b3", "#d8c8a8", "#ece6da", "#bdb5a8", "#d4b996", "#c8b8a0" };
        static readonly string[] Verdes = { "#7d8f4e", "#93a35a", "#6f8a55", "#a2ae72", "#5f7a4a" };

        public static void Construir(Transform padre)
        {
            if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/CuboPost/Generado", "Entorno");
            mats.Clear();
            icosferas.Clear();
            Random.InitState(20260929);
            cubo = MallaPrimitiva(PrimitiveType.Cube);
            cilindro = Guardar(CilindroBajo(8), "Cilindro8.asset");
            cono = Guardar(ConoBajo(7), "Cono7.asset");
            for (int i = 0; i < 5; i++) icosferas.Add(Guardar(Icosfera(i * 17 + 3), $"Icosfera{i}.asset"));

            raiz = new GameObject("Entorno (Plaza de la República)").transform;
            raiz.SetParent(padre, false);

            Suelos();
            MarcasViales();
            Mobiliario();
            Arcos();
            Vegetacion();
            MastilYLetras();
            Edificios();
            Transito();
        }

        // =====================================================================
        //  Suelos
        // =====================================================================

        static void Suelos()
        {
            var suelos = new GameObject("Suelos").transform;
            suelos.SetParent(raiz, false);
            var vereda = Mat("Vereda", "#cbc4b8", 0.05f);
            var asfalto = Mat("Asfalto", "#5c5d62", 0.12f);
            var cordon = Mat("Cordón", "#d9d3c8", 0.1f);
            var pasto = Mat("Pasto", "#8fae5d", 0.02f);
            var adoquin = MatTex("Adoquines", "#ffffff", Guardar(TexturaAdoquines(), "Adoquines.asset"), 0.08f);

            var a = new Armador();
            // Veredas a los costados (con los cortes de Corrientes y Lavalle como asfalto al ras).
            a.Caja(new Vector3((AsfaltoOeste - 260f) / 2f, -0.15f, 25f), new Vector3(260f + AsfaltoOeste, 0.3f, 560f), vereda);
            a.Caja(new Vector3((AsfaltoEste + 260f) / 2f, -0.15f, 25f), new Vector3(260f - AsfaltoEste, 0.3f, 560f), vereda);
            a.Caja(new Vector3(0.5f * (AsfaltoOeste + AsfaltoEste), -0.13f, 25f), new Vector3(AsfaltoEste - AsfaltoOeste, 0.02f, 560f), asfalto);
            foreach (var (z0, z1) in new[] { (CorrientesSur + 1f, CorrientesNorte + 1.5f), (109f, 121f) })
            {
                a.Caja(new Vector3((AsfaltoOeste - 260f) / 2f, 0.005f, (z0 + z1) / 2f), new Vector3(260f + AsfaltoOeste, 0.02f, z1 - z0), asfalto);
                a.Caja(new Vector3((AsfaltoEste + 260f) / 2f, 0.005f, (z0 + z1) / 2f), new Vector3(260f - AsfaltoEste, 0.02f, z1 - z0), asfalto);
            }
            // Canteros centrales elevados (oeste: el "Cantero Central Paloma Efrón"; este: junto a Pellegrini).
            foreach (var (z0, z1) in new[] { (-8f, 104f), (-150f, -33f), (125f, 270f) })
            {
                Cantero(a, new Rect(-61f, z0, 11f, z1 - z0), pasto, cordon);
                Cantero(a, new Rect(66f, z0, 4f, z1 - z0), pasto, cordon);
                a.Caja(new Vector3(-34.25f, 0.075f - 0.12f, (z0 + z1) / 2f), new Vector3(1.5f, 0.15f, z1 - z0), cordon); // separador fino
                a.Caja(new Vector3(47f, 0.075f - 0.12f, (z0 + z1) / 2f), new Vector3(2f, 0.15f, z1 - z0), cordon);      // separador Metrobus
            }
            a.Crear("Veredas, asfalto y canteros", suelos);

            // Plaza de adoquines, isla del Obelisco y la continuación de la plaza al sur.
            Poligono("Plaza de la República", suelos, Plaza, 0f, 0.3f, adoquin, 2f);
            Poligono("Isla del Obelisco", suelos, Elipse(Obelisco, 13f, 10f, 28), 0f, 0.3f, adoquin, 2f);
            Poligono("Plaza sur", suelos, new[] { new Vector2(-26f, -150f), new Vector2(35f, -150f), new Vector2(35f, -52f), new Vector2(-26f, -52f) }, 0f, 0.3f, adoquin, 2f);

            // Canteros de pasto de la plaza (con borde de flores rojas en los dos grandes).
            var flores = Mat("Flores rojas", "#c24b3e", 0.05f);
            var canteros = new[]
            {
                new[] { new Vector2(-17.5f, 36f), new Vector2(-3.5f, 35f), new Vector2(-2.5f, 51f), new Vector2(-18.5f, 57f) },
                new[] { new Vector2(5f, 36f), new Vector2(22f, 35f), new Vector2(16f, 56f), new Vector2(3.5f, 51f) },
                new[] { new Vector2(-18f, 62f), new Vector2(-6f, 60f), new Vector2(-3f, 90f), new Vector2(-17f, 95f) },
                new[] { new Vector2(5f, 62f), new Vector2(14f, 62f), new Vector2(6f, 86f), new Vector2(3f, 86f) },
                new[] { new Vector2(-24.5f, 12f), new Vector2(-19.5f, 12f), new Vector2(-19.5f, 32f), new Vector2(-24f, 32f) },
            };
            var b = new Armador();
            for (int i = 0; i < canteros.Length; i++)
            {
                Poligono("Cantero", suelos, canteros[i], 0.14f, 0.3f, pasto, 0f);
                if (i < 2 || i == 4) Borde(b, canteros[i], 0.16f, 0.3f, 0.28f, flores);
            }
            b.Crear("Bordes de flores", suelos);
        }

        static void Cantero(Armador a, Rect r, Material pasto, Material cordon)
        {
            a.Caja(new Vector3(r.center.x, -0.12f + 0.09f, r.center.y), new Vector3(r.width, 0.18f, r.height), cordon);
            a.Caja(new Vector3(r.center.x, -0.12f + 0.1f, r.center.y), new Vector3(r.width - 0.5f, 0.18f, r.height - 0.5f), pasto);
        }

        // =====================================================================
        //  Marcas viales
        // =====================================================================

        static void MarcasViales()
        {
            var blanco = Mat("Pintura blanca", "#f1efe9", 0.25f);
            var amarillo = Mat("Pintura amarilla", "#e9b62c", 0.25f);
            var verde = Mat("Pintura verde bici", "#4f9a62", 0.2f);
            var a = new Armador();
            const float y = -0.112f;

            // Carriles con líneas discontinuas (3 m pintados cada 8 m).
            foreach (float x in new[] { -38.7f, -42.4f, -46.1f, -65.3f, -69.6f, 51.6f, 55.2f, 58.8f, 62.4f, 74.5f })
                Discontinua(a, new Vector3(x, y, -150f), new Vector3(x, y, 270f), blanco);
            // Bicisenda junto a la plaza (oeste) y línea del Metrobus.
            a.Caja(new Vector3(-28.4f, y, 45f), new Vector3(0.14f, 0.012f, 210f), blanco);
            a.Caja(new Vector3(39f, y, 45f), new Vector3(0.14f, 0.012f, 210f), amarillo);
            for (float z = -6f; z < 100f; z += 25f) Bicicleta(a, new Vector3(-27.3f, y, z), blanco);
            // Corrientes (entre la plaza y la isla del Obelisco).
            foreach (float z in new[] { -17.6f, -22.8f })
                Discontinua(a, new Vector3(-74f, y, z), new Vector3(79f, y, z), blanco);

            // Cordón amarillo alrededor de la plaza.
            for (int i = 0; i < Plaza.Length; i++)
            {
                var p = Plaza[i];
                var q = Plaza[(i + 1) % Plaza.Length];
                var afuera = new Vector2(q.y - p.y, -(q.x - p.x)).normalized * 0.35f;
                Segmento(a, new Vector3(p.x + afuera.x, y, p.y + afuera.y), new Vector3(q.x + afuera.x, y, q.y + afuera.y), 0.15f, 0.012f, amarillo);
            }

            // Cebras (barras paralelas al sentido de la marcha) y cruce verde de la bicisenda.
            Cebra(a, new Vector2(-50f, -15f), 48f, 5f, true, blanco);        // cruza 9 de Julio oeste
            Cebra(a, new Vector2(57f, 6f), 44f, 5f, true, blanco);           // cruza Metrobus y 9 de Julio este
            Cebra(a, new Vector2(29f, -20.2f), 15f, 5f, false, blanco);      // plaza → isla del Obelisco
            Cebra(a, new Vector2(-12f, -20.2f), 15f, 5f, false, blanco);
            for (int i = 0; i < 6; i++)                                       // cruce de bicisenda verde y blanco
                a.Caja(new Vector3(-27.3f, y + 0.001f, -14f - i * 0.6f), new Vector3(2.2f, 0.012f, 0.3f), i % 2 == 0 ? verde : blanco);
            // Flechas.
            foreach (var x in new[] { -40.5f, -44.2f, 53.4f, 57f }) Flecha(a, new Vector3(x, y, -9f), x < 0 ? 0f : 180f, blanco);
            // Isletas amarillas (cuñas) junto a los cruces.
            foreach (var p in new[] { new Vector3(-34.5f, 0f, -18f), new Vector3(47f, 0f, 1f), new Vector3(47f, 0f, 11f) })
                a.Caja(p + new Vector3(0, -0.02f, 0), new Vector3(1.1f, 0.2f, 3.2f), amarillo, Quaternion.Euler(8f, 0, 0));

            a.Crear("Marcas viales", raiz);
        }

        static void Discontinua(Armador a, Vector3 desde, Vector3 hasta, Material m)
        {
            var dir = hasta - desde;
            float largo = dir.magnitude;
            var d = dir / largo;
            for (float t = 0; t + 3f < largo; t += 8f)
                Segmento(a, desde + d * t, desde + d * (t + 3f), 0.13f, 0.012f, m);
        }

        static void Segmento(Armador a, Vector3 p, Vector3 q, float ancho, float alto, Material m)
        {
            var dir = q - p;
            a.Caja((p + q) / 2f, new Vector3(ancho, alto, dir.magnitude), m, Quaternion.LookRotation(dir.normalized, Vector3.up));
        }

        static void Cebra(Armador a, Vector2 centro, float largoCruce, float ancho, bool cruzaEnX, Material m)
        {
            for (float o = -ancho / 2f + 0.25f; o < ancho / 2f; o += 1f)
            {
                var pos = cruzaEnX ? new Vector3(centro.x, -0.111f, centro.y + o) : new Vector3(centro.x + o, -0.111f, centro.y);
                var tam = cruzaEnX ? new Vector3(largoCruce, 0.012f, 0.5f) : new Vector3(0.5f, 0.012f, largoCruce);
                a.Caja(pos, tam, m);
            }
        }

        static void Flecha(Armador a, Vector3 p, float giro, Material m)
        {
            var r = Quaternion.Euler(0, giro, 0);
            a.Caja(p + r * new Vector3(0, 0, -0.6f), new Vector3(0.18f, 0.012f, 1.6f), m, r);
            a.Caja(p + r * new Vector3(-0.22f, 0, 0.35f), new Vector3(0.16f, 0.012f, 0.8f), m, r * Quaternion.Euler(0, 35f, 0));
            a.Caja(p + r * new Vector3(0.22f, 0, 0.35f), new Vector3(0.16f, 0.012f, 0.8f), m, r * Quaternion.Euler(0, -35f, 0));
        }

        /// <summary>Símbolo de bicicleta pintado: dos ruedas (aros) y el cuadro.</summary>
        static void Bicicleta(Armador a, Vector3 p, Material m)
        {
            foreach (float dz in new[] { -0.55f, 0.55f })
                for (int i = 0; i < 10; i++)
                {
                    float ang = i * 36f;
                    a.Caja(p + new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad) * 0.32f, 0, dz + Mathf.Sin(ang * Mathf.Deg2Rad) * 0.32f),
                        new Vector3(0.07f, 0.012f, 0.22f), m, Quaternion.Euler(0, -ang, 0));
                }
            a.Caja(p, new Vector3(0.07f, 0.012f, 1.1f), m, Quaternion.Euler(0, 20f, 0));
        }

        // =====================================================================
        //  Mobiliario: bolardos, semáforos, faroles, bancos, parada del Metrobus
        // =====================================================================

        static void Mobiliario()
        {
            var negro = Mat("Bolardo", "#26272b", 0.35f);
            var poste = Mat("Poste", "#3a3c41", 0.35f);
            var cajaSem = Mat("Caja semáforo", "#1c1d20", 0.3f);
            var amarilloSem = Mat("Marco semáforo", "#e2b12f", 0.3f);
            var gris = Mat("Farol gris", "#8d9096", 0.4f);
            var madera = Mat("Madera banco", "#a37a52", 0.2f);
            var vidrio = Mat("Vidrio parada", "#a9c3cf", 0.7f);
            var luces = new[] { Emisiva("Luz roja", "#ff4a3d"), Emisiva("Luz amarilla", "#ffc93c"), Emisiva("Luz verde", "#44d17a"), Emisiva("Luz farol", "#fff1c8") };

            var a = new Armador();
            // Bolardos negros con la punta inclinada (como en Corrientes) junto al cordón.
            for (float x = -25f; x <= 34f; x += 1.9f)
            {
                if (x > 26.5f && x < 31.5f) continue;
                if (x > -14.5f && x < -9.5f) continue;
                Bolardo(a, new Vector3(x, 0f, CorrientesNorte + 0.55f), negro);
            }
            for (float z = -11f; z < 36f; z += 2f)
                Bolardo(a, new Vector3(-26f + 0.55f + (z + 12.5f) * (7f / 112.5f), 0f, z), negro);
            for (float z = -11f; z < 12f; z += 2f)
                Bolardo(a, new Vector3(34.4f - (z + 12.5f) * (2f / 24.5f), 0f, z), negro);

            // Semáforos (mirando hacia donde vienen los autos / peatones).
            var semaforos = new (Vector3 p, float giro)[]
            {
                (new Vector3(-25.3f, 0, -13.3f), 90f), (new Vector3(-73.3f, 0, -13f), -90f), (new Vector3(-49.5f, -0.12f, -11.5f), 180f),
                (new Vector3(31.8f, 0, -13.3f), 0f), (new Vector3(31.8f, 0, -27f), 180f), (new Vector3(-9.5f, 0, -13.3f), 0f),
                (new Vector3(35.5f, 0, 8.8f), -90f), (new Vector3(78.5f, 0, 3.5f), 90f), (new Vector3(-74.5f, 0, -30f), 0f),
            };
            foreach (var (p, g) in semaforos)
            {
                var r = Quaternion.Euler(0, g, 0);
                a.Cilindro(p, p + Vector3.up * 3.3f, 0.07f, poste);
                a.Caja(p + Vector3.up * 3.75f + r * new Vector3(0, 0, 0.02f), new Vector3(0.52f, 1.2f, 0.06f), amarilloSem, r);
                a.Caja(p + Vector3.up * 3.75f, new Vector3(0.38f, 1.05f, 0.3f), cajaSem, r);
                for (int i = 0; i < 3; i++)
                    a.Malla(icosferas[0], Matrix4x4.TRS(p + Vector3.up * (4.1f - i * 0.34f) + r * new Vector3(0, 0, -0.16f), r, Vector3.one * 0.22f), luces[i]);
                // Semáforo peatonal más bajo.
                a.Caja(p + Vector3.up * 2.3f + r * new Vector3(0, 0, -0.05f), new Vector3(0.3f, 0.55f, 0.22f), cajaSem, r);
            }

            // Faroles altos de doble brazo en los canteros centrales.
            for (float z = -140f; z < 260f; z += 24f)
            {
                if (z > -33f && z < -8f) continue;
                foreach (float x in new[] { -55.5f, 68f })
                {
                    var p = new Vector3(x, 0.06f, z);
                    a.Cilindro(p, p + Vector3.up * 10f, 0.13f, gris);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var codo = p + Vector3.up * 10f;
                        var punta = codo + new Vector3(s * 2.2f, 0.6f, 0);
                        a.Cilindro(codo, punta, 0.07f, gris);
                        a.Caja(punta + new Vector3(0, -0.12f, 0), new Vector3(0.9f, 0.18f, 0.4f), gris);
                        a.Caja(punta + new Vector3(0, -0.23f, 0), new Vector3(0.7f, 0.04f, 0.3f), luces[3]);
                    }
                }
            }

            // Bancos en el cantero oeste, frente a los pastos altos.
            for (float z = 13f; z < 32f; z += 4.6f)
            {
                var p = new Vector3(-18.6f, 0, z);
                a.Caja(p + new Vector3(0, 0.45f, 0), new Vector3(0.5f, 0.08f, 1.8f), madera);
                a.Caja(p + new Vector3(0.22f, 0.75f, 0), new Vector3(0.08f, 0.45f, 1.8f), madera);
                foreach (float dz in new[] { -0.75f, 0.75f }) a.Caja(p + new Vector3(0, 0.22f, dz), new Vector3(0.45f, 0.44f, 0.08f), poste);
            }

            // Parada del Metrobus (plataforma, techo liviano y vidrios).
            var parada = new Vector3(44.8f, 0, 12f);
            a.Caja(parada + new Vector3(0, -0.12f + 0.15f, 0), new Vector3(3.2f, 0.3f, 34f), Mat("Plataforma", "#c9c5bd", 0.1f));
            for (float z = -15f; z <= 15f; z += 5f)
                a.Cilindro(parada + new Vector3(1.1f, 0.18f, z), parada + new Vector3(1.1f, 3.3f, z), 0.07f, gris);
            a.Caja(parada + new Vector3(0.2f, 3.4f, 0), new Vector3(3.6f, 0.12f, 32f), vidrio, Quaternion.Euler(0, 0, -6f));
            a.Caja(parada + new Vector3(1.15f, 1.4f, 0), new Vector3(0.04f, 2f, 30f), vidrio);

            a.Crear("Mobiliario urbano", raiz);
        }

        static void Bolardo(Armador a, Vector3 p, Material m)
        {
            a.Caja(p + new Vector3(0, 0.38f, 0), new Vector3(0.24f, 0.76f, 0.18f), m);
            a.Caja(p + new Vector3(0, 0.8f, -0.02f), new Vector3(0.24f, 0.14f, 0.18f), m, Quaternion.Euler(-28f, 0, 0));
        }

        /// <summary>Arcos blancos de luces (dos caños curvos con focos escalonados), como en la plaza.</summary>
        static void Arcos()
        {
            var blanco = Mat("Arco blanco", "#f0eee8", 0.45f);
            var foco = Mat("Foco", "#8a8e95", 0.5f);
            var luz = Emisiva("Luz foco", "#fff1c8");
            var negro = Mat("Base arco", "#26272b", 0.3f);
            // (base, giro): el arco se curva hacia +X local, es decir hacia adentro de la plaza.
            var arcos = new (Vector3 p, float giro)[]
            {
                (new Vector3(-23f, 0, -7f), 0f), (new Vector3(-22.2f, 0, 6f), 0f), (new Vector3(-21.3f, 0, 20f), 0f),
                (new Vector3(31.8f, 0, -7f), 180f), (new Vector3(31.2f, 0, 5f), 180f),
                (new Vector3(-12f, 0, -10.5f), -90f), (new Vector3(20f, 0, -10.5f), -90f),
            };
            var a = new Armador();
            foreach (var (p, giro) in arcos)
            {
                var r = Quaternion.Euler(0, giro, 0);
                const float ancho = 4.2f, alto = 9f;
                Vector3 Punto(float ang, float lado) =>
                    p + r * new Vector3(ancho * (1f - Mathf.Cos(ang * Mathf.Deg2Rad)), alto * Mathf.Sin(ang * Mathf.Deg2Rad), lado);
                foreach (float lado in new[] { -0.22f, 0.22f })
                {
                    for (float ang = 0; ang < 105f; ang += 7.5f)
                        a.Cilindro(Punto(ang, lado), Punto(ang + 7.5f, lado), 0.085f, blanco);
                    a.Caja(Punto(0, lado) + Vector3.up * 0.15f, new Vector3(0.5f, 0.3f, 0.4f), negro, r);
                }
                for (float ang = 15f; ang <= 105f; ang += 10f)
                {
                    var c = Punto(ang, 0);
                    var tangente = (Punto(ang + 1f, 0) - Punto(ang - 1f, 0)).normalized;
                    var rotFoco = Quaternion.LookRotation(r * Vector3.forward, tangente);
                    a.Caja(c, new Vector3(0.62f, 0.2f, 0.36f), foco, rotFoco);
                    a.Caja(c + rotFoco * new Vector3(0.12f, -0.11f, 0), new Vector3(0.4f, 0.03f, 0.26f), luz, rotFoco);
                }
            }
            a.Crear("Arcos de luces", raiz);
        }

        // =====================================================================
        //  Vegetación low-poly (tipas, coníferas, arbustos, pastos altos)
        // =====================================================================

        static void Vegetacion()
        {
            var tronco = Mat("Tronco", "#8c6a4f", 0.1f);
            var copas = new Material[Verdes.Length];
            for (int i = 0; i < Verdes.Length; i++) copas[i] = Mat("Copa " + i, Verdes[i], 0.05f);
            var pastos = new[] { Mat("Pasto alto 1", "#b8a86a", 0.05f), Mat("Pasto alto 2", "#8e9a5a", 0.05f), Mat("Pasto alto 3", "#a7a05e", 0.05f) };
            var a = new Armador();

            // Tipas en los canteros centrales y veredas.
            for (float z = -145f; z < 265f; z += 9f)
            {
                if (z > -33f && z < -8f) continue;
                Tipa(a, new Vector3(-55.5f + Random.Range(-2.5f, 2.5f), 0.06f, z + Random.Range(-2f, 2f)), Random.Range(0.9f, 1.25f), tronco, copas);
                if (Random.value < 0.8f) Tipa(a, new Vector3(68f + Random.Range(-0.8f, 0.8f), 0.06f, z + 4f), Random.Range(0.75f, 1f), tronco, copas);
            }
            for (float z = -140f; z < 260f; z += 12f)
            {
                if ((z > -30f && z < -10f) || (z > 107f && z < 123f)) continue;
                Tipa(a, new Vector3(-75.8f, 0f, z), 0.7f, tronco, copas);
                Tipa(a, new Vector3(80.8f, 0f, z + 6f), 0.7f, tronco, copas);
            }
            // Plaza: cantero oeste con tipas y pastos altos (como la foto), canteros norte, plaza sur.
            foreach (var p in new[] { new Vector3(-22f, 0.14f, 15f), new Vector3(-22.5f, 0.14f, 27f), new Vector3(-14f, 0.14f, 44f), new Vector3(15f, 0.14f, 43f), new Vector3(-9f, 0.14f, 70f) })
                Tipa(a, p, 1.15f, tronco, copas);
            foreach (var p in new[] { new Vector3(-13f, 0.14f, 80f), new Vector3(-10f, 0.14f, 88f), new Vector3(8f, 0.14f, 74f), new Vector3(-15f, 0.14f, 66f) })
                Conifera(a, p, Random.Range(0.9f, 1.2f), tronco, copas);
            for (float z = -60f; z > -145f; z -= 12f)
            {
                Tipa(a, new Vector3(-19f, 0f, z), 1f, tronco, copas);
                Tipa(a, new Vector3(28f, 0f, z - 6f), 1f, tronco, copas);
            }
            // Pastos altos (mata de hojas finas) y arbustos redondos.
            for (int i = 0; i < 34; i++)
                Pasto(a, new Vector3(Random.Range(-24.2f, -19.8f), 0.14f, Random.Range(12.5f, 31.5f)), pastos);
            for (int i = 0; i < 40; i++)
            {
                var c = Random.Range(0, 2) == 0 ? new Vector2(Random.Range(-17f, -4f), Random.Range(37f, 50f)) : new Vector2(Random.Range(6f, 20f), Random.Range(37f, 50f));
                if (Random.value < 0.5f) Pasto(a, new Vector3(c.x, 0.14f, c.y), pastos);
                else Arbusto(a, new Vector3(c.x, 0.14f, c.y), Random.Range(0.7f, 1.3f), copas);
            }
            a.Crear("Vegetación", raiz);
        }

        /// <summary>Tipa porteña: tronco torcido, dos ramas y copa ancha de bloques facetados.</summary>
        static void Tipa(Armador a, Vector3 p, float esc, Material tronco, Material[] copas)
        {
            float h = Random.Range(3.4f, 4.6f) * esc;
            var cima = p + new Vector3(Random.Range(-0.4f, 0.4f), h, Random.Range(-0.4f, 0.4f));
            a.Cilindro(p, cima, 0.24f * esc, tronco);
            for (int i = 0; i < 2; i++)
            {
                var dir = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * new Vector3(1f, 0.9f, 0);
                a.Cilindro(cima - Vector3.up * 0.4f, cima + dir * 1.8f * esc, 0.11f * esc, tronco);
            }
            var copa = copas[Random.Range(0, copas.Length)];
            int n = Random.Range(5, 8);
            for (int i = 0; i < n; i++)
            {
                var off = new Vector3(Random.Range(-2.6f, 2.6f), Random.Range(0.6f, 2.2f), Random.Range(-2.6f, 2.6f)) * esc;
                float s = Random.Range(2.2f, 3.4f) * esc;
                var m = Random.value < 0.3f ? copas[Random.Range(0, copas.Length)] : copa;
                a.Malla(icosferas[Random.Range(0, icosferas.Count)], Matrix4x4.TRS(cima + off, Random.rotation, new Vector3(s, s * 0.72f, s)), m);
            }
        }

        /// <summary>Conífera de capas de conos, como la referencia low-poly.</summary>
        static void Conifera(Armador a, Vector3 p, float esc, Material tronco, Material[] copas)
        {
            a.Cilindro(p, p + Vector3.up * 2.2f * esc, 0.2f * esc, tronco);
            var m = copas[Random.Range(0, copas.Length)];
            for (int i = 0; i < 4; i++)
            {
                float r = (3.2f - i * 0.6f) * esc, alto = (3f - i * 0.35f) * esc;
                a.Malla(cono, Matrix4x4.TRS(p + Vector3.up * (1.6f + i * 1.7f) * esc, Quaternion.Euler(0, Random.Range(0f, 360f), 0), new Vector3(r, alto, r)), m);
            }
        }

        static void Arbusto(Armador a, Vector3 p, float esc, Material[] copas)
        {
            var m = copas[Random.Range(0, copas.Length)];
            for (int i = 0; i < 3; i++)
                a.Malla(icosferas[Random.Range(0, icosferas.Count)],
                    Matrix4x4.TRS(p + new Vector3(Random.Range(-0.5f, 0.5f), 0.45f * esc, Random.Range(-0.5f, 0.5f)), Random.rotation, Vector3.one * Random.Range(0.9f, 1.3f) * esc), m);
        }

        static void Pasto(Armador a, Vector3 p, Material[] pastos)
        {
            var m = pastos[Random.Range(0, pastos.Length)];
            int hojas = Random.Range(7, 12);
            for (int i = 0; i < hojas; i++)
            {
                var rot = Quaternion.Euler(Random.Range(-22f, 22f), Random.Range(0f, 360f), Random.Range(-22f, 22f));
                a.Malla(cono, Matrix4x4.TRS(p + new Vector3(Random.Range(-0.3f, 0.3f), 0, Random.Range(-0.3f, 0.3f)), rot,
                    new Vector3(0.16f, Random.Range(1.0f, 1.8f), 0.16f)), m);
            }
        }

        // =====================================================================
        //  Mástil con bandera y cartel BA de plantas
        // =====================================================================

        static void MastilYLetras()
        {
            var a = new Armador();
            var blanco = Mat("Mástil", "#eeebe4", 0.5f);
            var piedra = Mat("Base mástil", "#ddd6ca", 0.15f);
            var mastil = new Vector3(3f, 0f, 44f);
            a.Caja(mastil + new Vector3(0, 0.5f, 0), new Vector3(2.6f, 1f, 2.6f), piedra);
            a.Cilindro(mastil + Vector3.up, mastil + Vector3.up * 20f, 0.28f, blanco);
            a.Cilindro(mastil + Vector3.up * 20f, mastil + Vector3.up * 33f, 0.18f, blanco);
            a.Malla(icosferas[0], Matrix4x4.TRS(mastil + Vector3.up * 33.2f, Quaternion.identity, Vector3.one * 0.5f), blanco);
            a.Crear("Mástil", raiz);

            // Bandera: caja fina con la textura (se ve de los dos lados), un poco inclinada por el viento.
            var bandera = new GameObject("Bandera", typeof(MeshFilter), typeof(MeshRenderer));
            bandera.transform.SetParent(raiz, false);
            bandera.transform.SetPositionAndRotation(mastil + new Vector3(3.8f, 30.6f, 0), Quaternion.Euler(0, 8f, -4f));
            bandera.transform.localScale = new Vector3(7.2f, 4.6f, 0.04f);
            bandera.GetComponent<MeshFilter>().sharedMesh = cubo;
            bandera.GetComponent<MeshRenderer>().sharedMaterial = MatTex("Bandera", "#ffffff", Guardar(TexturaBandera(), "Bandera.asset"), 0.2f);

            // Letras BA de arbustos (cartel verde), mirando al sur.
            string[] b = { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." };
            string[] l = { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" };
            const float celda = 0.72f;
            var verdes = new[] { Mat("Seto 1", "#3f6b3a", 0.05f), Mat("Seto 2", "#4d7d44", 0.05f), Mat("Seto 3", "#355c33", 0.05f) };
            var c = new Armador();
            var centro = new Vector3(2.5f, 0f, 37.5f);
            c.Caja(centro + new Vector3(0, 0.2f, 0), new Vector3(9.5f, 0.4f, 3f), Mat("Cantero letras", "#8a7a6a", 0.1f));
            c.Caja(centro + new Vector3(0, 0.45f, -1.35f), new Vector3(9.5f, 0.25f, 0.3f), Mat("Flores rojas", "#c24b3e", 0.05f));
            void Letra(string[] mapa, float x0)
            {
                for (int fila = 0; fila < mapa.Length; fila++)
                for (int col = 0; col < mapa[fila].Length; col++)
                {
                    if (mapa[fila][col] != '#') continue;
                    for (int prof = 0; prof < 2; prof++)
                    {
                        var pos = centro + new Vector3(x0 + col * celda, 0.75f + (mapa.Length - 1 - fila) * celda, (prof - 0.5f) * celda);
                        c.Malla(icosferas[Random.Range(0, icosferas.Count)], Matrix4x4.TRS(pos, Random.rotation, Vector3.one * celda * Random.Range(1.35f, 1.6f)), verdes[Random.Range(0, verdes.Length)]);
                    }
                }
            }
            Letra(b, -4.1f);
            Letra(l, 0.9f);
            c.Crear("Cartel BA (plantitas)", raiz);
        }

        // =====================================================================
        //  Edificios con detalle de fachada
        // =====================================================================

        static void Edificios()
        {
            var edif = new GameObject("Edificios").transform;
            edif.SetParent(raiz, false);
            var huecos = new[] { (CorrientesSur + 1f, CorrientesNorte + 1.5f), (109f, 121f) };
            foreach (float lado in new[] { -1f, 1f })
            {
                float x = lado < 0 ? FachadaOeste : FachadaEste;
                float z = -150f;
                while (z < 262f)
                {
                    float ancho = Random.Range(11f, 24f);
                    bool enHueco = false;
                    foreach (var (h0, h1) in huecos)
                        if (z + ancho > h0 && z < h1) { enHueco = true; z = h1 + 1f; break; }
                    if (enHueco) continue;
                    // Esquina Corrientes / Pellegrini y Corrientes / Cerrito: carteles luminosos.
                    bool carteles = z > -80f && z < 20f && (lado > 0 || Random.value < 0.4f);
                    int detalle = Mathf.Abs(z) < 120f ? 2 : 1;
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

            var go = a.Crear("Edificio", padre);
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
            // (x, z, giro) por carril: oeste va al norte (0°), este va al sur (180°), Corrientes al este (90°).
            var autos = new List<(float x, float z, float g, int tipo)>();
            foreach (var x in new[] { -30.8f, -36.8f, -40.5f, -44.2f, -48f, -63.2f, -67.5f, -71.8f })
                for (int k = 0; k < 2; k++) autos.Add((x, Random.Range(-140f, 250f), 0f, Random.Range(0, 10)));
            foreach (var x in new[] { 37f, 41f, 49.8f, 53.4f, 57f, 60.6f, 64.2f, 72.2f, 76.8f })
                for (int k = 0; k < 2; k++) autos.Add((x, Random.Range(-140f, 250f), 180f, Random.Range(0, 10)));
            autos.Add((-40f, -20.2f, 90f, 1)); autos.Add((-5f, -15.2f, 90f, 0)); autos.Add((18f, -25.4f, -90f, 3)); autos.Add((50f, -15.2f, 90f, 5));
            foreach (var (x, z, g, tipo) in autos)
            {
                if (z > -30f && z < -10f && g % 180f == 0f) continue; // no pisar el cruce de Corrientes
                var r = Quaternion.Euler(0, g, 0);
                var p = new Vector3(x, -0.12f, z);
                bool colectivo = tipo == 9 || (x > 36f && x < 42f);
                bool taxi = !colectivo && tipo < 4;
                if (colectivo)
                {
                    var color = new[] { "#2e7d4f", "#b3413a", "#2f5f9e" }[Random.Range(0, 3)];
                    a.Caja(p + r * new Vector3(0, 1.75f, 0), new Vector3(2.5f, 2.9f, 12f), Mat("Colectivo " + color, color, 0.5f), r);
                    a.Caja(p + r * new Vector3(0, 2.3f, 0), new Vector3(2.54f, 1f, 11f), vidrio, r);
                    a.Caja(p + r * new Vector3(0, 0.75f, 0), new Vector3(2.54f, 0.5f, 12.02f), Mat("Franja blanca", "#f0eee8", 0.4f), r);
                    foreach (float sz in new[] { -4f, 3.8f }) Ruedas(a, p, r, 1.2f, sz, 0.5f, ruedas);
                }
                else
                {
                    var color = taxi ? "#1f1f22" : coloresAuto[Random.Range(0, coloresAuto.Length)];
                    var carroceria = Mat("Auto " + color, color, 0.6f);
                    a.Caja(p + r * new Vector3(0, 0.62f, 0), new Vector3(1.8f, 0.62f, 4.3f), carroceria, r);
                    a.Caja(p + r * new Vector3(0, 1.18f, -0.25f), new Vector3(1.62f, 0.55f, 2.2f), vidrio, r);
                    a.Caja(p + r * new Vector3(0, 1.48f, -0.25f), new Vector3(1.55f, 0.08f, 1.9f), taxi ? Mat("Techo taxi", "#f2c230", 0.5f) : carroceria, r);
                    foreach (float sz in new[] { -1.35f, 1.35f }) Ruedas(a, p, r, 0.88f, sz, 0.33f, ruedas);
                }
            }
            a.Crear("Tránsito", raiz);
        }

        static void Ruedas(Armador a, Vector3 p, Quaternion r, float sx, float sz, float radio, Material m)
        {
            foreach (float s in new[] { -1f, 1f })
                a.Malla(cilindro, Matrix4x4.TRS(p + r * new Vector3(s * sx, radio, sz), r * Quaternion.Euler(0, 0, 90f), new Vector3(radio * 2f, 0.12f, radio * 2f)), m);
        }

        // =====================================================================
        //  Armador: junta muchas piezas en una sola malla por grupo (una submalla por material)
        // =====================================================================

        class Armador
        {
            readonly Dictionary<Material, List<CombineInstance>> partes = new Dictionary<Material, List<CombineInstance>>();

            public void Caja(Vector3 c, Vector3 t, Material m, Quaternion? r = null) =>
                Malla(cubo, Matrix4x4.TRS(c, r ?? Quaternion.identity, t), m);

            public void Cilindro(Vector3 desde, Vector3 hasta, float radio, Material m)
            {
                var dir = hasta - desde;
                Malla(cilindro, Matrix4x4.TRS((desde + hasta) / 2f, Quaternion.FromToRotation(Vector3.up, dir), new Vector3(radio * 2f, dir.magnitude / 2f, radio * 2f)), m);
            }

            public void Malla(Mesh malla, Matrix4x4 mtx, Material m)
            {
                if (!partes.TryGetValue(m, out var lista)) partes[m] = lista = new List<CombineInstance>();
                lista.Add(new CombineInstance { mesh = malla, transform = mtx });
            }

            public GameObject Crear(string nombre, Transform padre)
            {
                var go = new GameObject(nombre, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(padre, false);
                var subs = new List<CombineInstance>();
                var materiales = new List<Material>();
                foreach (var kv in partes)
                {
                    var sub = new Mesh { indexFormat = IndexFormat.UInt32 };
                    sub.CombineMeshes(kv.Value.ToArray(), true, true);
                    subs.Add(new CombineInstance { mesh = sub, transform = Matrix4x4.identity });
                    materiales.Add(kv.Key);
                }
                var final = new Mesh { name = nombre, indexFormat = IndexFormat.UInt32 };
                final.CombineMeshes(subs.ToArray(), false, false);
                final.RecalculateBounds();
                foreach (var s in subs) Object.DestroyImmediate(s.mesh);
                go.GetComponent<MeshFilter>().sharedMesh = final;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterials = materiales.ToArray();
                partes.Clear();
                return go;
            }
        }

        // =====================================================================
        //  Mallas low-poly y polígonos
        // =====================================================================

        static Mesh MallaPrimitiva(PrimitiveType tipo)
        {
            var tmp = GameObject.CreatePrimitive(tipo);
            var m = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            return m;
        }

        /// <summary>Cada triángulo con sus propios vértices (sombreado facetado), orientado hacia afuera del centro.</summary>
        static Mesh Facetada(string nombre, List<Vector3> v, List<int> t, Vector3 centro)
        {
            var vv = new Vector3[t.Count];
            var tt = new int[t.Count];
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - centro) < 0) (b, c) = (c, b);
                vv[i] = a; vv[i + 1] = b; vv[i + 2] = c;
                tt[i] = i; tt[i + 1] = i + 1; tt[i + 2] = i + 2;
            }
            var m = new Mesh { name = nombre, vertices = vv, triangles = tt };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Icosfera irregular (radio ~0.5): la base de copas, arbustos y setos.</summary>
        static Mesh Icosfera(int semilla)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] f = { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                        3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
            var medios = new Dictionary<long, int>();
            int Medio(int a, int b)
            {
                long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (medios.TryGetValue(k, out var i)) return i;
                v.Add((v[a] + v[b]) * 0.5f);
                medios[k] = v.Count - 1;
                return v.Count - 1;
            }
            var tri = new List<int>();
            for (int i = 0; i < f.Length; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2];
                int ab = Medio(a, b), bc = Medio(b, c), ca = Medio(c, a);
                tri.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            var rnd = new System.Random(semilla);
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized * 0.5f * (0.84f + 0.3f * (float)rnd.NextDouble());
            return Facetada("Icosfera", v, tri, Vector3.zero);
        }

        /// <summary>Cilindro de pocas caras (alto 2, diámetro 1, como el primitivo de Unity).</summary>
        static Mesh CilindroBajo(int lados)
        {
            var v = new List<Vector3> { new Vector3(0, -1, 0), new Vector3(0, 1, 0) };
            var t = new List<int>();
            for (int i = 0; i < lados; i++)
            {
                float a = i * Mathf.PI * 2f / lados;
                v.Add(new Vector3(Mathf.Cos(a) * 0.5f, -1, Mathf.Sin(a) * 0.5f));
                v.Add(new Vector3(Mathf.Cos(a) * 0.5f, 1, Mathf.Sin(a) * 0.5f));
            }
            for (int i = 0; i < lados; i++)
            {
                int b0 = 2 + i * 2, t0 = b0 + 1, b1 = 2 + ((i + 1) % lados) * 2, t1 = b1 + 1;
                t.AddRange(new[] { b0, t0, t1, b0, t1, b1, 0, b0, b1, 1, t1, t0 });
            }
            return Facetada("Cilindro", v, t, Vector3.zero);
        }

        /// <summary>Cono de pocas caras: base de diámetro 1 en y = 0, punta en y = 1.</summary>
        static Mesh ConoBajo(int lados)
        {
            var v = new List<Vector3> { Vector3.zero, Vector3.up };
            var t = new List<int>();
            for (int i = 0; i < lados; i++)
            {
                float a = i * Mathf.PI * 2f / lados;
                v.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0, Mathf.Sin(a) * 0.5f));
            }
            for (int i = 0; i < lados; i++)
            {
                int p = 2 + i, q = 2 + (i + 1) % lados;
                t.AddRange(new[] { p, 1, q, 0, q, p });
            }
            return Facetada("Cono", v, t, new Vector3(0, 0.33f, 0));
        }

        static Vector2[] Elipse(Vector2 c, float rx, float rz, int n)
        {
            var p = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                p[i] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * rz);
            }
            return p;
        }

        /// <summary>Losa con forma de polígono convexo: arriba en y = alto, con costados (se ve de los dos lados).</summary>
        static void Poligono(string nombre, Transform padre, Vector2[] p, float alto, float espesor, Material m, float tileUV)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            // Sentido del polígono (visto desde arriba) para que la cara de arriba mire hacia +Y.
            float area = 0f;
            for (int i = 0; i < p.Length; i++) { var a0 = p[i]; var b0 = p[(i + 1) % p.Length]; area += a0.x * b0.y - b0.x * a0.y; }
            bool antihorario = area > 0f;
            foreach (var q in p)
            {
                v.Add(new Vector3(q.x, alto, q.y));
                uv.Add(tileUV > 0 ? q / tileUV : Vector2.zero);
            }
            for (int i = 1; i < p.Length - 1; i++)
                t.AddRange(antihorario ? new[] { 0, i + 1, i } : new[] { 0, i, i + 1 });
            // Costados: cada uno con sus propios vértices y en los dos sentidos (sin normales compartidas).
            for (int i = 0; i < p.Length; i++)
            {
                var a = p[i];
                var b = p[(i + 1) % p.Length];
                float largo = Vector2.Distance(a, b);
                for (int lado = 0; lado < 2; lado++)
                {
                    int k = v.Count;
                    v.Add(new Vector3(a.x, alto, a.y)); v.Add(new Vector3(b.x, alto, b.y));
                    v.Add(new Vector3(b.x, alto - espesor, b.y)); v.Add(new Vector3(a.x, alto - espesor, a.y));
                    uv.AddRange(new[] { Vector2.zero, new Vector2(largo * 0.5f, 0), new Vector2(largo * 0.5f, 0.1f), new Vector2(0, 0.1f) });
                    t.AddRange(lado == 0 ? new[] { k, k + 1, k + 2, k, k + 2, k + 3 } : new[] { k, k + 2, k + 1, k, k + 3, k + 2 });
                }
            }
            var malla = new Mesh { name = nombre };
            malla.SetVertices(v);
            malla.SetUVs(0, uv);
            malla.SetTriangles(t, 0);
            malla.RecalculateNormals();
            malla.RecalculateBounds();
            var go = new GameObject(nombre, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(padre, false);
            go.GetComponent<MeshFilter>().sharedMesh = malla;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        /// <summary>Borde (cantero de flores) siguiendo un polígono.</summary>
        static void Borde(Armador a, Vector2[] p, float y, float ancho, float alto, Material m)
        {
            for (int i = 0; i < p.Length; i++)
            {
                var q0 = p[i];
                var q1 = p[(i + 1) % p.Length];
                Segmento(a, new Vector3(q0.x, y + alto / 2f, q0.y), new Vector3(q1.x, y + alto / 2f, q1.y), ancho, alto, m);
            }
        }

        // =====================================================================
        //  Materiales y texturas
        // =====================================================================

        static Material Mat(string nombre, string hex, float suavidad) => MatTex(nombre, hex, null, suavidad);

        static Material MatTex(string nombre, string hex, Texture2D tex, float suavidad)
        {
            if (mats.TryGetValue(nombre, out var m)) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = nombre };
            ColorUtility.TryParseHtmlString(hex, out var c);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", suavidad);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            var archivo = string.Join("_", nombre.Split(System.IO.Path.GetInvalidFileNameChars())).Replace("#", "");
            mats[nombre] = Guardar(m, archivo + ".mat");
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
            return asset;
        }

        /// <summary>Adoquines grises cálidos en hileras trabadas (un módulo = 2 × 2 m).</summary>
        static Texture2D TexturaAdoquines()
        {
            const int n = 256, filas = 18, porFila = 11;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8, name = "Adoquines" };
            var tonos = new Color[filas * porFila];
            for (int i = 0; i < tonos.Length; i++)
            {
                float g = Random.Range(0.56f, 0.7f);
                tonos[i] = new Color(g * Random.Range(1.0f, 1.08f), g * 0.97f, g * Random.Range(0.9f, 0.96f));
            }
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float fy = y / (float)n * filas;
                int fila = Mathf.FloorToInt(fy);
                float fx = x / (float)n * porFila + (fila % 2) * 0.5f;
                int col = Mathf.FloorToInt(fx) % porFila;
                float dx = Mathf.Abs(fx - Mathf.Floor(fx) - 0.5f), dy = Mathf.Abs(fy - fila - 0.5f);
                px[y * n + x] = Mathf.Max(dx, dy) > 0.42f ? new Color(0.42f, 0.4f, 0.38f) : tonos[fila * porFila + col];
            }
            t.SetPixels(px);
            t.Apply(true);
            return t;
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
