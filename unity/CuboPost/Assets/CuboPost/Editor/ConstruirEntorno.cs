using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CuboPost.EditorTools
{
    /// <summary>
    /// Contexto físico alrededor del cubo: Plaza de la República simplificada, estilo "maqueta" pastel.
    /// Plaza de adoquines, calles con líneas y cebras, bolardos, semáforos, faroles curvos, árboles,
    /// letras BA de plantitas y edificios como cajas con ventanas.
    /// Coordenadas: el cubo en el origen (frente hacia -Z), el Obelisco en (0, 0, 48).
    /// </summary>
    public static class ConstruirEntorno
    {
        const string Carpeta = "Assets/CuboPost/Generado/Entorno";

        // Plaza (adoquines) y calles alrededor, en metros.
        const float PlazaX = 34f, PlazaSur = -20f, PlazaNorte = 82f;
        const float CalleAncho = 28f;       // 9 de Julio, a cada lado
        const float CalleSurNorte = 20f;    // calles al sur y al norte
        const float Vereda = 6f;

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static Texture2D texVentanas, texAdoquines;
        static Transform raiz;

        static readonly string[] Pasteles =
        {
            "#f4b8a8", "#f7d58a", "#f3ece0", "#b9d8cf", "#f6c6d3", "#cfd6f2", "#e9d3b8", "#f2c9a0", "#d9e7b8",
        };

        public static void Construir(Transform padre)
        {
            if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/CuboPost/Generado", "Entorno");
            mats.Clear();
            Random.InitState(20260928);
            raiz = new GameObject("Entorno (Plaza de la República)").transform;
            raiz.SetParent(padre, false);

            texVentanas = GuardarTextura(TexturaVentanas(), "Ventanas.asset");
            texAdoquines = GuardarTextura(TexturaAdoquines(), "Adoquines.asset");

            Suelos();
            Lineas();
            Bolardos();
            Semaforos();
            Faroles();
            Arboles();
            LetrasBA();
            Edificios();
            Autos();
        }

        // ---------------- suelos ----------------

        static void Suelos()
        {
            float xCalle = PlazaX + CalleAncho, zSur = PlazaSur - CalleSurNorte, zNorte = PlazaNorte + CalleSurNorte;
            // Veredas exteriores a nivel 0, rodeando la zona de calles (4 piezas, sin tapar el asfalto).
            var vereda = Mat("Vereda", "#d8d1c5", 0.05f);
            const float lejos = 220f;
            Caja("Vereda oeste", raiz, new Vector3(-(xCalle + lejos) / 2f, -0.15f, 20f), new Vector3(lejos - xCalle, 0.3f, 2f * lejos), vereda);
            Caja("Vereda este", raiz, new Vector3((xCalle + lejos) / 2f, -0.15f, 20f), new Vector3(lejos - xCalle, 0.3f, 2f * lejos), vereda);
            Caja("Vereda sur", raiz, new Vector3(0, -0.15f, (zSur - lejos + 20f) / 2f), new Vector3(2f * xCalle, 0.3f, zSur + lejos - 20f), vereda);
            Caja("Vereda norte", raiz, new Vector3(0, -0.15f, (zNorte + lejos + 20f) / 2f), new Vector3(2f * xCalle, 0.3f, lejos + 20f - zNorte), vereda);
            // Asfalto: 12 cm más abajo que las veredas y la plaza.
            Caja("Asfalto", raiz, new Vector3(0, -0.13f, (zSur + zNorte) / 2f), new Vector3(2f * xCalle, 0.02f, zNorte - zSur), Mat("Asfalto", "#55555c", 0.15f));
            // Plaza de adoquines a nivel 0, con cordón claro.
            var adoquin = MatTex("Adoquines", "#ffffff", texAdoquines, 0.1f);
            Caja("Plaza (adoquines)", raiz, new Vector3(0, -0.15f, (PlazaSur + PlazaNorte) / 2f), new Vector3(2f * PlazaX, 0.3f, PlazaNorte - PlazaSur), adoquin, uvMetros: 2f);
            var cordon = Mat("Cordón", "#e8e2d6", 0.1f);
            Caja("Cordón sur", raiz, new Vector3(0, -0.06f, PlazaSur), new Vector3(2f * PlazaX + 0.3f, 0.14f, 0.3f), cordon);
            Caja("Cordón norte", raiz, new Vector3(0, -0.06f, PlazaNorte), new Vector3(2f * PlazaX + 0.3f, 0.14f, 0.3f), cordon);
            Caja("Cordón oeste", raiz, new Vector3(-PlazaX, -0.06f, (PlazaSur + PlazaNorte) / 2f), new Vector3(0.3f, 0.14f, PlazaNorte - PlazaSur), cordon);
            Caja("Cordón este", raiz, new Vector3(PlazaX, -0.06f, (PlazaSur + PlazaNorte) / 2f), new Vector3(0.3f, 0.14f, PlazaNorte - PlazaSur), cordon);
            // Canteros de pasto (como los de la plaza).
            var pasto = Mat("Pasto", "#9fcf86", 0.05f);
            Caja("Cantero", raiz, new Vector3(-22f, 0.05f, 60f), new Vector3(16f, 0.12f, 30f), pasto);
            Caja("Cantero", raiz, new Vector3(22f, 0.05f, 60f), new Vector3(16f, 0.12f, 30f), pasto);
            Caja("Cantero", raiz, new Vector3(-24f, 0.05f, 18f), new Vector3(12f, 0.12f, 14f), pasto);
            Caja("Cantero", raiz, new Vector3(24f, 0.05f, 18f), new Vector3(12f, 0.12f, 14f), pasto);
        }

        static void Lineas()
        {
            var blanca = Mat("Línea blanca", "#f4f1ea", 0.2f);
            var amarilla = Mat("Línea amarilla", "#f2c230", 0.2f);
            float y = -0.115f, zSur = PlazaSur - CalleSurNorte, zNorte = PlazaNorte + CalleSurNorte;
            var lineas = new GameObject("Líneas y cebras").transform;
            lineas.SetParent(raiz, false);

            // 9 de Julio: carriles de 3,5 m a cada lado, amarilla junto al cordón.
            foreach (float lado in new[] { -1f, 1f })
            {
                Caja("Amarilla", lineas, new Vector3(lado * (PlazaX + 0.6f), y, (zSur + zNorte) / 2f), new Vector3(0.15f, 0.01f, zNorte - zSur), amarilla);
                for (int i = 1; i < 8; i++)
                    Caja("Carril", lineas, new Vector3(lado * (PlazaX + i * 3.5f), y, (zSur + zNorte) / 2f), new Vector3(0.12f, 0.01f, zNorte - zSur), blanca);
            }
            // Calles sur y norte.
            foreach (float zc in new[] { PlazaSur - CalleSurNorte / 2f, PlazaNorte + CalleSurNorte / 2f })
            {
                Caja("Carril", lineas, new Vector3(0, y, zc - 3.5f), new Vector3(2f * PlazaX, 0.01f, 0.12f), blanca);
                Caja("Carril", lineas, new Vector3(0, y, zc + 3.5f), new Vector3(2f * PlazaX, 0.01f, 0.12f), blanca);
            }
            Caja("Amarilla", lineas, new Vector3(0, y, PlazaSur - 0.6f), new Vector3(2f * PlazaX, 0.01f, 0.15f), amarilla);

            // Cebras: frente al cubo (cruzando la calle sur) y en las dos esquinas del sur.
            Cebra(lineas, new Vector3(0, y, PlazaSur - CalleSurNorte / 2f), CalleSurNorte, 6f, false, blanca);
            Cebra(lineas, new Vector3(-(PlazaX + CalleAncho / 2f), y, PlazaSur + 4f), CalleAncho, 5f, true, blanca);
            Cebra(lineas, new Vector3(PlazaX + CalleAncho / 2f, y, PlazaSur + 4f), CalleAncho, 5f, true, blanca);
        }

        /// <summary>Barras paralelas al sentido en que se camina.</summary>
        static void Cebra(Transform padre, Vector3 centro, float largoCruce, float ancho, bool cruzaEnX, Material mat)
        {
            int barras = Mathf.FloorToInt(ancho / 1f);
            for (int i = 0; i < barras; i++)
            {
                float o = -ancho / 2f + 0.25f + i * 1f;
                var pos = cruzaEnX ? centro + new Vector3(0, 0, o) : centro + new Vector3(o, 0, 0);
                var tam = cruzaEnX ? new Vector3(largoCruce - 1f, 0.012f, 0.5f) : new Vector3(0.5f, 0.012f, largoCruce - 1f);
                Caja("Cebra", padre, pos, tam, mat);
            }
        }

        // ---------------- mobiliario ----------------

        static void Bolardos()
        {
            var negro = Mat("Bolardo", "#2a2a2e", 0.4f);
            var bolardos = new GameObject("Bolardos (palos del piso)").transform;
            bolardos.SetParent(raiz, false);
            for (float x = -PlazaX + 1f; x <= PlazaX - 1f; x += 2.2f)
            {
                if (Mathf.Abs(x) < 3.5f) continue; // paso frente a la cebra
                Cilindro("Bolardo", bolardos, new Vector3(x, 0.4f, PlazaSur + 0.6f), new Vector3(0.2f, 0.4f, 0.2f), negro);
            }
            foreach (float lado in new[] { -1f, 1f })
                for (float z = PlazaSur + 2f; z <= PlazaNorte - 2f; z += 2.6f)
                    Cilindro("Bolardo", bolardos, new Vector3(lado * (PlazaX - 0.6f), 0.4f, z), new Vector3(0.2f, 0.4f, 0.2f), negro);
        }

        static void Semaforos()
        {
            var poste = Mat("Poste semáforo", "#3b3d42", 0.4f);
            var caja = Mat("Caja semáforo", "#1f2023", 0.3f);
            var luces = new[] { Emisiva("Luz roja", "#ff4a3d"), Emisiva("Luz amarilla", "#ffc93c"), Emisiva("Luz verde", "#44d17a") };
            var sem = new GameObject("Semáforos").transform;
            sem.SetParent(raiz, false);
            var puntos = new[]
            {
                // giro: hacia dónde miran las luces (0 = sur / -Z).
                (new Vector3(-PlazaX + 0.8f, 0, PlazaSur + 0.8f), 0f), (new Vector3(PlazaX - 0.8f, 0, PlazaSur + 0.8f), 0f),
                (new Vector3(-PlazaX + 0.8f, 0, PlazaNorte - 0.8f), 180f), (new Vector3(PlazaX - 0.8f, 0, PlazaNorte - 0.8f), 180f),
                (new Vector3(-4f, 0, PlazaSur + 0.8f), 0f), (new Vector3(4f, 0, PlazaSur + 0.8f), 0f),
                (new Vector3(-(PlazaX + CalleAncho + 1f), 0, PlazaSur - 1f), -90f), (new Vector3(PlazaX + CalleAncho + 1f, 0, PlazaSur - 1f), 90f),
            };
            foreach (var (pos, giro) in puntos)
            {
                var s = new GameObject("Semáforo").transform;
                s.SetParent(sem, false);
                s.SetPositionAndRotation(pos, Quaternion.Euler(0, giro, 0));
                Cilindro("Poste", s, new Vector3(0, 1.6f, 0), new Vector3(0.12f, 1.6f, 0.12f), poste, local: true);
                Caja("Caja", s, new Vector3(0, 3.55f, 0), new Vector3(0.36f, 1.0f, 0.3f), caja, local: true);
                for (int i = 0; i < 3; i++)
                    Esfera("Luz", s, new Vector3(0, 3.87f - i * 0.32f, -0.16f), 0.22f, luces[i], local: true);
            }
        }

        /// <summary>Faroles blancos curvos, como los arcos de la plaza.</summary>
        static void Faroles()
        {
            var blanco = Mat("Farol", "#f2f0ea", 0.5f);
            var luz = Emisiva("Farol luz", "#fff3cf");
            var far = new GameObject("Faroles").transform;
            far.SetParent(raiz, false);
            var puntos = new List<(Vector3, float)>();
            for (float x = -PlazaX + 5f; x <= PlazaX - 5f; x += 11f) puntos.Add((new Vector3(x, 0, PlazaSur + 2.2f), 0f));
            foreach (float lado in new[] { -1f, 1f })
                for (float z = PlazaSur + 12f; z <= PlazaNorte - 6f; z += 14f) puntos.Add((new Vector3(lado * (PlazaX - 2.2f), 0, z), lado * -90f));
            foreach (var (pos, giro) in puntos)
            {
                var f = new GameObject("Farol").transform;
                f.SetParent(far, false);
                f.SetPositionAndRotation(pos, Quaternion.Euler(0, giro, 0));
                Cilindro("Poste", f, new Vector3(0, 3.5f, 0), new Vector3(0.14f, 3.5f, 0.14f), blanco, local: true);
                // Curva: tres tramos inclinados hacia la calle (-Z local).
                Vector3 p = new Vector3(0, 7f, 0);
                for (int i = 0; i < 3; i++)
                {
                    float ang = 25f + i * 30f;
                    var dir = Quaternion.Euler(-ang, 0, 0) * Vector3.up;
                    var fin = p + dir * 0.9f;
                    var t = Cilindro("Curva", f, (p + fin) / 2f, new Vector3(0.12f, 0.45f, 0.12f), blanco, local: true);
                    t.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
                    p = fin;
                }
                Esfera("Luz", f, p + new Vector3(0, -0.15f, 0), 0.35f, luz, local: true);
            }
        }

        static void Arboles()
        {
            var tronco = Mat("Tronco", "#9a7657", 0.1f);
            var copas = new[] { Mat("Copa 1", "#8fc48b", 0.05f), Mat("Copa 2", "#a8d49a", 0.05f), Mat("Copa 3", "#7fb88a", 0.05f) };
            var arb = new GameObject("Árboles").transform;
            arb.SetParent(raiz, false);
            var puntos = new List<Vector3>();
            for (float z = 44f; z <= 78f; z += 8f) { puntos.Add(new Vector3(-28f, 0, z)); puntos.Add(new Vector3(28f, 0, z)); }
            for (float z = 12f; z <= 26f; z += 7f) { puntos.Add(new Vector3(-29f, 0, z)); puntos.Add(new Vector3(29f, 0, z)); }
            foreach (var pos in puntos)
            {
                float h = Random.Range(3.2f, 4.5f);
                var a = new GameObject("Árbol").transform;
                a.SetParent(arb, false);
                a.position = pos;
                Cilindro("Tronco", a, new Vector3(0, h / 2f, 0), new Vector3(0.35f, h / 2f, 0.35f), tronco, local: true);
                var copa = copas[Random.Range(0, copas.Length)];
                Esfera("Copa", a, new Vector3(0, h + 1.2f, 0), Random.Range(3.2f, 4.2f), copa, local: true);
                Esfera("Copa", a, new Vector3(Random.Range(-1f, 1f), h + 0.6f, Random.Range(-1f, 1f)), Random.Range(2.2f, 2.8f), copa, local: true);
            }
        }

        /// <summary>Letras "BA" hechas de arbustos, entre el cubo y el Obelisco, mirando al sur.</summary>
        static void LetrasBA()
        {
            string[] b = { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." };
            string[] a = { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" };
            const float celda = 0.5f;
            var verdes = new[] { Mat("Arbusto 1", "#5f9e5a", 0.05f), Mat("Arbusto 2", "#6fae63", 0.05f), Mat("Arbusto 3", "#4f8c55", 0.05f) };
            var letras = new GameObject("Letras BA (plantitas)").transform;
            letras.SetParent(raiz, false);
            letras.position = new Vector3(0, 0, 26f);
            Caja("Cantero", letras, new Vector3(0, 0.15f, 0), new Vector3(7.2f, 0.3f, 2.2f), Mat("Tierra", "#8a6f5a", 0.05f), local: true);

            void Letra(string[] mapa, float x0)
            {
                for (int fila = 0; fila < mapa.Length; fila++)
                for (int col = 0; col < mapa[fila].Length; col++)
                {
                    if (mapa[fila][col] != '#') continue;
                    for (int prof = 0; prof < 2; prof++)
                    {
                        var pos = new Vector3(x0 + col * celda, 0.55f + (mapa.Length - 1 - fila) * celda, (prof - 0.5f) * celda);
                        pos += new Vector3(Random.Range(-0.05f, 0.05f), Random.Range(-0.04f, 0.04f), Random.Range(-0.05f, 0.05f));
                        Esfera("Arbusto", letras, pos, celda * Random.Range(1.2f, 1.45f), verdes[Random.Range(0, verdes.Length)], local: true);
                    }
                }
            }
            // Vistas desde el sur (-Z): la "B" queda a la izquierda del espectador → x negativo.
            Letra(b, -3.0f);
            Letra(a, 0.5f);
        }

        // ---------------- edificios ----------------

        static void Edificios()
        {
            var edif = new GameObject("Edificios").transform;
            edif.SetParent(raiz, false);
            float xFrente = PlazaX + CalleAncho + Vereda;              // 68
            float zSurFrente = PlazaSur - CalleSurNorte - Vereda;       // -46
            float zNorteFrente = PlazaNorte + CalleSurNorte + Vereda;   // 108

            // Oeste y este: fachadas mirando a la plaza (hacia ±X).
            foreach (float lado in new[] { -1f, 1f })
                Fila(edif, desde: zSurFrente - 20f, hasta: zNorteFrente + 20f, (pos, ancho) =>
                    new Vector3(lado * xFrente, 0, pos), lado > 0 ? -90f : 90f);
            // Sur y norte: fachadas mirando a la plaza (hacia ±Z).
            Fila(edif, -xFrente + 4f, xFrente - 4f, (pos, ancho) => new Vector3(pos, 0, zSurFrente), 0f);
            Fila(edif, -xFrente + 4f, xFrente - 4f, (pos, ancho) => new Vector3(pos, 0, zNorteFrente), 180f);
        }

        /// <summary>Una hilera de edificios de ancho variable entre dos posiciones.</summary>
        static void Fila(Transform padre, float desde, float hasta, System.Func<float, float, Vector3> ubicar, float giro)
        {
            float pos = desde;
            while (pos < hasta)
            {
                float ancho = Mathf.Min(Random.Range(10f, 22f), hasta - pos);
                if (ancho < 6f) break;
                Edificio(padre, ubicar(pos + ancho / 2f, ancho), giro, ancho);
                pos += ancho;
            }
        }

        /// <summary>Caja con ventanas + planta baja con toldo + cornisa. giro: hacia dónde mira la fachada (0 = hacia -Z... ver abajo).</summary>
        static void Edificio(Transform padre, Vector3 frente, float giro, float ancho)
        {
            float alto = Random.Range(15f, 42f), prof = 16f;
            var colorHex = Pasteles[Random.Range(0, Pasteles.Length)];
            ColorUtility.TryParseHtmlString(colorHex, out var color);

            var e = new GameObject("Edificio").transform;
            e.SetParent(padre, false);
            // Local: fachada en z = 0 mirando a +Z hacia la plaza; el volumen va hacia -Z.
            // giro 0 → fachada mira a +Z (edificios del sur). Se rota para el resto.
            e.SetPositionAndRotation(frente, Quaternion.Euler(0, giro, 0));

            var cuerpo = new GameObject("Cuerpo", typeof(MeshFilter), typeof(MeshRenderer));
            cuerpo.transform.SetParent(e, false);
            cuerpo.transform.localPosition = new Vector3(0, alto / 2f, -prof / 2f);
            cuerpo.GetComponent<MeshFilter>().sharedMesh = MallaCaja(new Vector3(ancho - 0.3f, alto, prof), 3.2f, 3.3f);
            cuerpo.GetComponent<MeshRenderer>().sharedMaterial = MatTex("Fachada " + colorHex, colorHex, texVentanas, 0.1f);

            var oscuro = ColorUtility.ToHtmlStringRGB(color * 0.82f);
            Caja("Planta baja", e, new Vector3(0, 1.8f, 0.25f), new Vector3(ancho - 0.6f, 3.6f, 0.6f), Mat("Planta baja #" + oscuro, "#" + oscuro, 0.2f), local: true);
            var toldoHex = Pasteles[Random.Range(0, Pasteles.Length)];
            var toldo = Caja("Toldo", e, new Vector3(0, 3.7f, 1.0f), new Vector3(ancho * 0.7f, 0.12f, 1.4f), Mat("Toldo " + toldoHex, toldoHex, 0.2f), local: true);
            toldo.transform.localRotation = Quaternion.Euler(-12f, 0, 0);
            var claro = ColorUtility.ToHtmlStringRGB(Color.Lerp(color, Color.white, 0.45f));
            Caja("Cornisa", e, new Vector3(0, alto + 0.25f, -prof / 2f + 0.2f), new Vector3(ancho, 0.5f, prof + 0.8f), Mat("Cornisa #" + claro, "#" + claro, 0.2f), local: true);

            // Algunos carteles en la terraza, como las pantallas de la plaza.
            if (Random.value < 0.3f)
            {
                var cartel = new[] { "#8f6ad6", "#e0585e", "#3f7fd6", "#262a33" }[Random.Range(0, 4)];
                Caja("Cartel", e, new Vector3(0, alto + 3.2f, -2f), new Vector3(Mathf.Min(ancho * 0.8f, 12f), 5f, 0.5f), Mat("Cartel " + cartel, cartel, 0.6f), local: true);
            }
        }

        static void Autos()
        {
            var ruedas = Mat("Rueda", "#26262a", 0.3f);
            var vidrio = Mat("Vidrio", "#b8d4e6", 0.8f);
            var autos = new GameObject("Autos").transform;
            autos.SetParent(raiz, false);
            var lista = new (Vector3 pos, float giro, string color, bool taxi)[]
            {
                (new Vector3(-PlazaX - 5f, 0, -8f), 0f, "#26262a", true), (new Vector3(-PlazaX - 12f, 0, 22f), 0f, "#e36a6a", false),
                (new Vector3(-PlazaX - 19f, 0, 55f), 0f, "#26262a", true), (new Vector3(PlazaX + 5f, 0, 35f), 180f, "#f3ece0", false),
                (new Vector3(PlazaX + 12f, 0, -2f), 180f, "#26262a", true), (new Vector3(PlazaX + 20f, 0, 70f), 180f, "#7fa8e0", false),
                (new Vector3(-14f, 0, PlazaSur - 5f), 90f, "#26262a", true), (new Vector3(16f, 0, PlazaSur - 13f), -90f, "#f7d58a", false),
            };
            foreach (var (pos, giro, colorHex, taxi) in lista)
            {
                var a = new GameObject(taxi ? "Taxi" : "Auto").transform;
                a.SetParent(autos, false);
                a.SetPositionAndRotation(pos + new Vector3(0, -0.12f, 0), Quaternion.Euler(0, giro, 0));
                Caja("Carrocería", a, new Vector3(0, 0.65f, 0), new Vector3(1.8f, 0.7f, 4.2f), Mat("Auto " + colorHex, colorHex, 0.6f), local: true);
                Caja("Cabina", a, new Vector3(0, 1.25f, -0.2f), new Vector3(1.6f, 0.55f, 2.2f), taxi ? Mat("Techo taxi", "#f2c230", 0.5f) : vidrio, local: true);
                foreach (float sx in new[] { -0.85f, 0.85f })
                foreach (float sz in new[] { -1.35f, 1.35f })
                {
                    var r = Cilindro("Rueda", a, new Vector3(sx, 0.35f, sz), new Vector3(0.6f, 0.12f, 0.6f), ruedas, local: true);
                    r.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                }
            }
        }

        // ---------------- ayudas ----------------

        static GameObject Caja(string nombre, Transform padre, Vector3 pos, Vector3 tam, Material mat, bool local = false, float uvMetros = -1f)
        {
            var go = new GameObject(nombre, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(padre, false);
            if (local) go.transform.localPosition = pos; else go.transform.position = pos;
            // Malla con UV en metros (para los adoquines) o cubo común.
            go.GetComponent<MeshFilter>().sharedMesh = uvMetros > 0f ? MallaCaja(tam, uvMetros, uvMetros, techoConTextura: true) : CuboUnidad();
            if (uvMetros <= 0f) go.transform.localScale = tam;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return go;
        }

        static Transform Cilindro(string nombre, Transform padre, Vector3 pos, Vector3 escala, Material mat, bool local = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = nombre;
            go.transform.SetParent(padre, false);
            if (local) go.transform.localPosition = pos; else go.transform.position = pos;
            go.transform.localScale = escala;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        static void Esfera(string nombre, Transform padre, Vector3 pos, float diametro, Material mat, bool local = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = nombre;
            go.transform.SetParent(padre, false);
            if (local) go.transform.localPosition = pos; else go.transform.position = pos;
            go.transform.localScale = Vector3.one * diametro;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static Mesh cuboUnidad;
        static Mesh CuboUnidad()
        {
            if (cuboUnidad != null) return cuboUnidad;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cuboUnidad = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            return cuboUnidad;
        }

        /// <summary>Caja centrada con UV en metros: cada módulo de textura mide tileX × tileY.</summary>
        static Mesh MallaCaja(Vector3 t, float tileX, float tileY, bool techoConTextura = false)
        {
            float x = t.x / 2f, y = t.y / 2f, z = t.z / 2f;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            // a = abajo-izquierda, b = abajo-derecha, c = arriba-derecha, d = arriba-izquierda,
            // tal como se ve la cara desde afuera. Triángulos en sentido horario (cara visible).
            void Cara(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float anchoM, float altoM, bool plano)
            {
                int k = v.Count;
                v.AddRange(new[] { a, b, c, d });
                if (plano) uv.AddRange(new[] { new Vector2(0.02f, 0.02f), new Vector2(0.03f, 0.02f), new Vector2(0.03f, 0.03f), new Vector2(0.02f, 0.03f) });
                else uv.AddRange(new[] { new Vector2(0, 0), new Vector2(anchoM / tileX, 0), new Vector2(anchoM / tileX, altoM / tileY), new Vector2(0, altoM / tileY) });
                tri.AddRange(new[] { k, k + 3, k + 2, k, k + 2, k + 1 });
            }
            Cara(new Vector3(x, -y, z), new Vector3(-x, -y, z), new Vector3(-x, y, z), new Vector3(x, y, z), t.x, t.y, false);     // +Z
            Cara(new Vector3(-x, -y, -z), new Vector3(x, -y, -z), new Vector3(x, y, -z), new Vector3(-x, y, -z), t.x, t.y, false); // -Z
            Cara(new Vector3(x, -y, -z), new Vector3(x, -y, z), new Vector3(x, y, z), new Vector3(x, y, -z), t.z, t.y, false);     // +X
            Cara(new Vector3(-x, -y, z), new Vector3(-x, -y, -z), new Vector3(-x, y, -z), new Vector3(-x, y, z), t.z, t.y, false); // -X
            Cara(new Vector3(-x, y, -z), new Vector3(x, y, -z), new Vector3(x, y, z), new Vector3(-x, y, z), t.x, t.z, !techoConTextura); // techo
            Cara(new Vector3(x, -y, -z), new Vector3(-x, -y, -z), new Vector3(-x, -y, z), new Vector3(x, -y, z), t.x, t.z, true);  // piso
            var m = new Mesh { name = "Caja" };
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Material Mat(string nombre, string hex, float suavidad) => MatTex(nombre, hex, null, suavidad);

        static Material MatTex(string nombre, string hex, Texture2D tex, float suavidad)
        {
            if (mats.TryGetValue(nombre, out var m)) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = nombre };
            ColorUtility.TryParseHtmlString(hex, out var c);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", suavidad);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            mats[nombre] = GuardarMaterial(m, nombre);
            return mats[nombre];
        }

        static Material Emisiva(string nombre, string hex)
        {
            var m = MatTex(nombre, hex, null, 0.6f);
            ColorUtility.TryParseHtmlString(hex, out var c);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 1.6f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material GuardarMaterial(Material m, string nombre)
        {
            var archivo = string.Join("_", nombre.Split(System.IO.Path.GetInvalidFileNameChars())).Replace("#", "");
            var ruta = $"{Carpeta}/{archivo}.mat";
            if (AssetDatabase.LoadAssetAtPath<Object>(ruta) != null) AssetDatabase.DeleteAsset(ruta);
            AssetDatabase.CreateAsset(m, ruta);
            return m;
        }

        static Texture2D GuardarTextura(Texture2D t, string archivo)
        {
            var ruta = $"{Carpeta}/{archivo}";
            if (AssetDatabase.LoadAssetAtPath<Object>(ruta) != null) AssetDatabase.DeleteAsset(ruta);
            AssetDatabase.CreateAsset(t, ruta);
            return t;
        }

        // ---------------- texturas ----------------

        /// <summary>Un módulo de fachada: pared clara con una ventana (se tiñe con el color del edificio).</summary>
        static Texture2D TexturaVentanas()
        {
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                var c = Color.white;
                bool marco = u > 0.27f && u < 0.73f && v > 0.2f && v < 0.8f;
                bool vidrio = u > 0.31f && u < 0.69f && v > 0.25f && v < 0.76f;
                bool alfeizar = u > 0.24f && u < 0.76f && v > 0.16f && v < 0.21f;
                if (marco) c = new Color(1f, 1f, 1f);
                if (alfeizar) c = new Color(0.86f, 0.86f, 0.86f);
                if (vidrio)
                {
                    float brillo = Mathf.Lerp(0.42f, 0.62f, v) + (u > 0.49f && u < 0.51f ? 0.3f : 0f);
                    c = new Color(brillo * 0.85f, brillo * 0.95f, brillo * 1.15f);
                }
                px[y * n + x] = c;
            }
            t.SetPixels(px);
            t.Apply(true);
            t.name = "Ventanas";
            return t;
        }

        /// <summary>Adoquines: filas de piedras grises con juntas oscuras (un módulo = 2 × 2 m).</summary>
        static Texture2D TexturaAdoquines()
        {
            const int n = 256;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            var px = new Color[n * n];
            const int filas = 16, porFila = 10;
            var tonos = new float[filas * porFila];
            for (int i = 0; i < tonos.Length; i++) tonos[i] = Random.Range(0.62f, 0.76f);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float fy = y / (float)n * filas;
                int fila = Mathf.FloorToInt(fy);
                float fx = x / (float)n * porFila + (fila % 2) * 0.5f;
                int col = Mathf.FloorToInt(fx) % porFila;
                float dx = Mathf.Abs(fx - Mathf.Floor(fx) - 0.5f), dy = Mathf.Abs(fy - fila - 0.5f);
                // Piedra con bordes redondeados.
                float borde = Mathf.Max(dx * 1.0f, dy * 1.0f);
                float g = tonos[fila * porFila + col];
                var c = borde > 0.43f ? new Color(0.45f, 0.43f, 0.41f) : new Color(g, g * 0.98f, g * 0.95f);
                px[y * n + x] = c;
            }
            t.SetPixels(px);
            t.Apply(true);
            t.name = "Adoquines";
            return t;
        }
    }
}
