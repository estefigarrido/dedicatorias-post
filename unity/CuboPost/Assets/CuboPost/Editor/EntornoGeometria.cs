using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CuboPost.EditorTools
{
    /// <summary>
    /// Herramientas de geometría del entorno: curvas en planta (X, Z), triangulación de polígonos
    /// y armado de mallas (superficies con relieve, bandas entre dos curvas y perfiles barridos).
    /// En todos los Vector2 de este archivo, x = X (este) e y = Z (norte).
    /// </summary>
    public static partial class ConstruirEntorno
    {
        // =====================================================================
        //  Curvas y polígonos en planta
        // =====================================================================

        static List<Vector2> Puntos(float[] d)
        {
            var l = new List<Vector2>(d.Length / 2);
            for (int i = 0; i + 1 < d.Length; i += 2) l.Add(new Vector2(d[i], d[i + 1]));
            return l;
        }

        static Vector3 V3(Vector2 p, float y) => new Vector3(p.x, y, p.y);

        /// <summary>Área con signo: positiva si el polígono gira en sentido antihorario visto desde arriba (X a la derecha, Z arriba).</summary>
        static float AreaConSigno(IList<Vector2> p)
        {
            float a = 0f;
            for (int i = 0; i < p.Count; i++)
            {
                var q = p[i];
                var r = p[(i + 1) % p.Count];
                a += q.x * r.y - r.x * q.y;
            }
            return a / 2f;
        }

        static List<Vector2> Antihorario(IList<Vector2> p)
        {
            var l = new List<Vector2>(p);
            if (AreaConSigno(l) < 0f) l.Reverse();
            return l;
        }

        /// <summary>Longitud acumulada hasta cada punto (el primero vale 0).</summary>
        static float[] Acumulado(IList<Vector2> p)
        {
            var a = new float[p.Count];
            for (int i = 1; i < p.Count; i++) a[i] = a[i - 1] + Vector2.Distance(p[i - 1], p[i]);
            return a;
        }

        /// <summary>Punto de una polilínea abierta a la distancia s de su comienzo, y la dirección de avance en ese punto.</summary>
        static Vector2 PuntoEn(IList<Vector2> p, float[] acum, float s, out Vector2 direccion)
        {
            int n = p.Count;
            s = Mathf.Clamp(s, 0f, acum[n - 1]);
            int i = 1;
            while (i < n - 1 && acum[i] < s) i++;
            float tramo = acum[i] - acum[i - 1];
            float t = tramo > 1e-6f ? (s - acum[i - 1]) / tramo : 0f;
            direccion = (p[i] - p[i - 1]).normalized;
            if (direccion == Vector2.zero) direccion = Vector2.up;
            return p[i - 1] + (p[i] - p[i - 1]) * t;
        }

        /// <summary>Saca los puntos repetidos seguidos (y el último si coincide con el primero en una curva cerrada).</summary>
        static List<Vector2> SinRepetidos(IList<Vector2> p, bool cerrada)
        {
            var l = new List<Vector2>();
            foreach (var q in p)
                if (l.Count == 0 || Vector2.Distance(l[l.Count - 1], q) > 0.005f) l.Add(q);
            if (cerrada && l.Count > 1 && Vector2.Distance(l[0], l[l.Count - 1]) <= 0.005f) l.RemoveAt(l.Count - 1);
            return l;
        }

        /// <summary>
        /// Curva suave que pasa por todos los puntos (Catmull-Rom centrípeta), cortada en tramos de
        /// a lo sumo <paramref name="paso"/> metros.
        /// </summary>
        static List<Vector2> Suavizar(IList<Vector2> puntos, bool cerrada, float paso)
        {
            var p = SinRepetidos(puntos, cerrada);
            int n = p.Count;
            var r = new List<Vector2>();
            if (n < 3) { r.AddRange(p); return r; }
            int tramos = cerrada ? n : n - 1;
            for (int i = 0; i < tramos; i++)
            {
                Vector2 p1 = p[i], p2 = p[(i + 1) % n];
                Vector2 p0 = cerrada ? p[(i - 1 + n) % n] : (i > 0 ? p[i - 1] : p1 + (p1 - p2));
                Vector2 p3 = cerrada ? p[(i + 2) % n] : (i + 2 < n ? p[i + 2] : p2 + (p2 - p1));
                float t0 = 0f;
                float t1 = t0 + Mathf.Max(1e-3f, Mathf.Sqrt(Vector2.Distance(p0, p1)));
                float t2 = t1 + Mathf.Max(1e-3f, Mathf.Sqrt(Vector2.Distance(p1, p2)));
                float t3 = t2 + Mathf.Max(1e-3f, Mathf.Sqrt(Vector2.Distance(p2, p3)));
                int cortes = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(p1, p2) / paso));
                for (int k = 0; k < cortes; k++)
                {
                    float t = t1 + (t2 - t1) * k / cortes;
                    Vector2 a1 = p0 * ((t1 - t) / (t1 - t0)) + p1 * ((t - t0) / (t1 - t0));
                    Vector2 a2 = p1 * ((t2 - t) / (t2 - t1)) + p2 * ((t - t1) / (t2 - t1));
                    Vector2 a3 = p2 * ((t3 - t) / (t3 - t2)) + p3 * ((t - t2) / (t3 - t2));
                    Vector2 b1 = a1 * ((t2 - t) / (t2 - t0)) + a2 * ((t - t0) / (t2 - t0));
                    Vector2 b2 = a2 * ((t3 - t) / (t3 - t1)) + a3 * ((t - t1) / (t3 - t1));
                    r.Add(b1 * ((t2 - t) / (t2 - t1)) + b2 * ((t - t1) / (t2 - t1)));
                }
            }
            if (!cerrada) r.Add(p[n - 1]);
            return r;
        }

        /// <summary>Suaviza una polilínea abierta por tramos, respetando las esquinas vivas indicadas.</summary>
        static List<Vector2> SuavizarPorTramos(IList<Vector2> p, int[] esquinas, float paso)
        {
            var r = new List<Vector2>();
            int desde = 0;
            var cortes = new List<int>(esquinas) { p.Count - 1 };
            foreach (int hasta in cortes)
            {
                if (hasta <= desde) continue;
                var tramo = new List<Vector2>();
                for (int i = desde; i <= hasta; i++) tramo.Add(p[i]);
                var s = tramo.Count >= 3 ? Suavizar(tramo, false, paso) : Dividir(tramo, paso);
                if (r.Count > 0) s.RemoveAt(0);
                r.AddRange(s);
                desde = hasta;
            }
            return r;
        }

        /// <summary>Agrega puntos intermedios para que ningún tramo mida más que <paramref name="paso"/> (sin cambiar la forma).</summary>
        static List<Vector2> Dividir(IList<Vector2> p, float paso, bool cerrada = false)
        {
            var r = new List<Vector2>();
            int tramos = cerrada ? p.Count : p.Count - 1;
            for (int i = 0; i < tramos; i++)
            {
                Vector2 a = p[i], b = p[(i + 1) % p.Count];
                int cortes = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / paso));
                for (int k = 0; k < cortes; k++) r.Add(a + (b - a) * ((float)k / cortes));
            }
            if (!cerrada) r.Add(p[p.Count - 1]);
            return r;
        }

        /// <summary>
        /// Curva paralela a la distancia d. Con d positivo se corre hacia la izquierda del sentido de
        /// avance; con d negativo, hacia la derecha. En las esquinas alarga lo necesario (con tope).
        /// </summary>
        static List<Vector2> Desplazar(IList<Vector2> p, bool cerrada, float d)
        {
            int n = p.Count;
            var r = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                Vector2 antes, despues;
                if (cerrada) { antes = p[(i - 1 + n) % n]; despues = p[(i + 1) % n]; }
                else { antes = i > 0 ? p[i - 1] : p[i]; despues = i < n - 1 ? p[i + 1] : p[i]; }
                Vector2 d0 = (p[i] - antes).normalized, d1 = (despues - p[i]).normalized;
                if (d0 == Vector2.zero) d0 = d1;
                if (d1 == Vector2.zero) d1 = d0;
                Vector2 n0 = new Vector2(-d0.y, d0.x), n1 = new Vector2(-d1.y, d1.x);
                Vector2 nm = (n0 + n1).normalized;
                if (nm == Vector2.zero) nm = n0;
                float escala = 1f / Mathf.Max(0.4f, Vector2.Dot(nm, n0));
                r.Add(p[i] + nm * (d * escala));
            }
            return r;
        }

        static bool Dentro(IList<Vector2> poli, Vector2 q)
        {
            bool dentro = false;
            for (int i = 0, j = poli.Count - 1; i < poli.Count; j = i++)
            {
                Vector2 a = poli[i], b = poli[j];
                if ((a.y > q.y) != (b.y > q.y) && q.x < (b.x - a.x) * (q.y - a.y) / (b.y - a.y) + a.x) dentro = !dentro;
            }
            return dentro;
        }

        static float DistanciaASegmento(Vector2 q, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l2 = ab.sqrMagnitude;
            float t = l2 > 1e-10f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / l2) : 0f;
            return Vector2.Distance(q, a + ab * t);
        }

        static float DistanciaA(IList<Vector2> p, bool cerrada, Vector2 q)
        {
            float mejor = float.MaxValue;
            int tramos = cerrada ? p.Count : p.Count - 1;
            for (int i = 0; i < tramos; i++)
                mejor = Mathf.Min(mejor, DistanciaASegmento(q, p[i], p[(i + 1) % p.Count]));
            return mejor;
        }

        /// <summary>Recorta un polígono con un semiplano: se queda con el lado hacia donde apunta la normal.</summary>
        static List<Vector2> RecortarSemiplano(IList<Vector2> poli, Vector2 puntoDeLaRecta, Vector2 normalHaciaAdentro)
        {
            var r = new List<Vector2>();
            for (int i = 0; i < poli.Count; i++)
            {
                Vector2 a = poli[i], b = poli[(i + 1) % poli.Count];
                float da = Vector2.Dot(a - puntoDeLaRecta, normalHaciaAdentro), db = Vector2.Dot(b - puntoDeLaRecta, normalHaciaAdentro);
                if (da >= 0f) r.Add(a);
                if ((da >= 0f) != (db >= 0f)) r.Add(a + (b - a) * (da / (da - db)));
            }
            return SinRepetidos(r, true);
        }

        /// <summary>Parte de un polígono que queda dentro de una franja recta (centro, dirección y ancho).</summary>
        static List<Vector2> RecortarFranja(IList<Vector2> poli, Vector2 punto, Vector2 direccion, float ancho)
        {
            Vector2 lado = new Vector2(-direccion.y, direccion.x).normalized;
            var r = RecortarSemiplano(poli, punto - lado * (ancho / 2f), lado);
            return RecortarSemiplano(r, punto + lado * (ancho / 2f), -lado);
        }

        static List<Vector2> RecortarRect(IList<Vector2> poli, Rect r)
        {
            var p = RecortarSemiplano(poli, new Vector2(r.xMin, 0f), Vector2.right);
            p = RecortarSemiplano(p, new Vector2(r.xMax, 0f), -Vector2.right);
            p = RecortarSemiplano(p, new Vector2(0f, r.yMin), Vector2.up);
            return RecortarSemiplano(p, new Vector2(0f, r.yMax), -Vector2.up);
        }

        static bool EnTriangulo(Vector2 q, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (b.x - a.x) * (q.y - a.y) - (b.y - a.y) * (q.x - a.x);
            float d2 = (c.x - b.x) * (q.y - b.y) - (c.y - b.y) * (q.x - b.x);
            float d3 = (a.x - c.x) * (q.y - c.y) - (a.y - c.y) * (q.x - c.x);
            const float eps = 1e-7f;
            return d1 > eps && d2 > eps && d3 > eps;
        }

        /// <summary>
        /// Triangula un polígono simple (puede ser cóncavo) recortando "orejas". Devuelve índices de
        /// <paramref name="p"/> de a tres.
        /// </summary>
        static List<int> Triangular(IList<Vector2> p)
        {
            int n = p.Count;
            var tris = new List<int>();
            if (n < 3) return tris;
            var idx = new List<int>(n);
            bool antihorario = AreaConSigno(p) > 0f;
            for (int i = 0; i < n; i++) idx.Add(antihorario ? i : n - 1 - i);

            int vueltasSinCorte = 0;
            while (idx.Count > 3 && vueltasSinCorte < 2)
            {
                bool corto = false;
                for (int i = 0; i < idx.Count && idx.Count > 3; i++)
                {
                    int i0 = idx[(i + idx.Count - 1) % idx.Count], i1 = idx[i], i2 = idx[(i + 1) % idx.Count];
                    Vector2 a = p[i0], b = p[i1], c = p[i2];
                    float cruz = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                    if (cruz <= 1e-8f)
                    {
                        // Vértice alineado o repetido: se saca sin generar triángulo.
                        if (Mathf.Abs(cruz) <= 1e-8f) { idx.RemoveAt(i); i--; corto = true; }
                        continue;
                    }
                    bool tapado = false;
                    for (int j = 0; j < idx.Count && !tapado; j++)
                    {
                        int k = idx[j];
                        if (k == i0 || k == i1 || k == i2) continue;
                        if (EnTriangulo(p[k], a, b, c)) tapado = true;
                    }
                    if (tapado) continue;
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    idx.RemoveAt(i);
                    i--;
                    corto = true;
                }
                vueltasSinCorte = corto ? 0 : vueltasSinCorte + 1;
                if (!corto && idx.Count > 3)
                {
                    // Polígono con algún cruce: se corta la esquina más convexa para poder seguir.
                    int mejor = 0;
                    float mejorCruz = float.MinValue;
                    for (int i = 0; i < idx.Count; i++)
                    {
                        Vector2 a = p[idx[(i + idx.Count - 1) % idx.Count]], b = p[idx[i]], c = p[idx[(i + 1) % idx.Count]];
                        float cruz = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                        if (cruz > mejorCruz) { mejorCruz = cruz; mejor = i; }
                    }
                    tris.Add(idx[(mejor + idx.Count - 1) % idx.Count]); tris.Add(idx[mejor]); tris.Add(idx[(mejor + 1) % idx.Count]);
                    idx.RemoveAt(mejor);
                    vueltasSinCorte = 0;
                    cortesForzados++;
                }
            }
            if (idx.Count == 3) { tris.Add(idx[0]); tris.Add(idx[1]); tris.Add(idx[2]); }
            return tris;
        }

        /// <summary>Cuántas veces hubo que forzar un corte al triangular (0 si todos los polígonos son simples).</summary>
        static int cortesForzados;

        /// <summary>Puntos al azar dentro de un polígono, a más de <paramref name="margen"/> metros del borde.</summary>
        static List<Vector2> Dispersar(IList<Vector2> poli, int cantidad, float margen)
        {
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var q in poli) { x0 = Mathf.Min(x0, q.x); x1 = Mathf.Max(x1, q.x); z0 = Mathf.Min(z0, q.y); z1 = Mathf.Max(z1, q.y); }
            var r = new List<Vector2>();
            for (int intento = 0; intento < cantidad * 30 && r.Count < cantidad; intento++)
            {
                var q = new Vector2(Random.Range(x0, x1), Random.Range(z0, z1));
                if (Dentro(poli, q) && (margen <= 0f || DistanciaA(poli, true, q) >= margen)) r.Add(q);
            }
            return r;
        }

        // =====================================================================
        //  Armado de mallas
        // =====================================================================

        /// <summary>Malla en construcción: vértices con normal y UV, y triángulos orientados con la regla de Unity.</summary>
        class MallaB
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();

            public int Vert(Vector3 p, Vector3 normal, Vector2 coordUV)
            {
                v.Add(p); n.Add(normal); uv.Add(coordUV);
                return v.Count - 1;
            }

            /// <summary>Agrega el triángulo de modo que su cara visible mire hacia <paramref name="hacia"/>.</summary>
            public void Tri(int a, int b, int c, Vector3 hacia)
            {
                Vector3 g = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                if (g.sqrMagnitude < 1e-12f) return;
                if (Vector3.Dot(g, hacia) >= 0f) { t.Add(a); t.Add(b); t.Add(c); }
                else { t.Add(a); t.Add(c); t.Add(b); }
            }

            /// <summary>Cuadrilátero plano con sus cuatro vértices propios (a, b, c, d en orden, dando la vuelta).</summary>
            public void Cuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int k = v.Count;
                Vert(a, normal, ua); Vert(b, normal, ub); Vert(c, normal, uc); Vert(d, normal, ud);
                Tri(k, k + 1, k + 2, normal);
                Tri(k, k + 2, k + 3, normal);
            }

            /// <summary>Recalcula las normales de los vértices desde el índice dado, promediando las caras (sombreado suave).</summary>
            public void NormalesSuaves(int desdeVertice, int desdeTriangulo)
            {
                for (int i = desdeVertice; i < n.Count; i++) n[i] = Vector3.zero;
                for (int i = desdeTriangulo; i < t.Count; i += 3)
                {
                    Vector3 g = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                    n[t[i]] += g; n[t[i + 1]] += g; n[t[i + 2]] += g;
                }
                for (int i = desdeVertice; i < n.Count; i++) n[i] = n[i].sqrMagnitude > 1e-12f ? n[i].normalized : Vector3.up;
            }

            /// <summary>Arma la malla con los datos pedidos: lisa (posición y normal), con UV, o con UV y tangentes.</summary>
            public Mesh Malla(string nombre, Canales canales = Canales.ConRelieve)
            {
                var m = new Mesh { name = nombre };
                if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(v);
                m.SetNormals(n);
                if (canales != Canales.Lisos) m.SetUVs(0, uv);
                m.SetTriangles(t, 0);
                m.RecalculateBounds();
                if (canales == Canales.ConRelieve) m.RecalculateTangents();
                return m;
            }

            /// <summary>Crea el objeto con esta malla. Los datos por vértice salen de lo que necesita el material.</summary>
            public GameObject Crear(string nombre, Transform padre, Material material, bool proyectaSombra = true)
            {
                var canales = conRelieve.Contains(material) ? Canales.ConRelieve : (conTextura.Contains(material) ? Canales.ConUV : Canales.Lisos);
                var go = new GameObject(nombre, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(padre, false);
                go.GetComponent<MeshFilter>().sharedMesh = Malla(nombre, canales);
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                if (!proyectaSombra) mr.shadowCastingMode = ShadowCastingMode.Off;
                return go;
            }
        }

        /// <summary>Coordenadas de textura en planta: un mosaico cada <paramref name="mosaico"/> metros, girado <paramref name="giro"/> grados.</summary>
        static Vector2 UVPlanta(Vector2 p, float mosaico, float giro)
        {
            if (giro != 0f)
            {
                float c = Mathf.Cos(giro * Mathf.Deg2Rad), s = Mathf.Sin(giro * Mathf.Deg2Rad);
                p = new Vector2(p.x * c + p.y * s, -p.x * s + p.y * c);
            }
            return p / mosaico;
        }

        /// <summary>
        /// Superficie de un polígono (puede ser cóncavo), mirando hacia arriba. La altura de cada punto
        /// la da <paramref name="altura"/>; si hay relieve conviene pasar <paramref name="ladoMax"/> chico
        /// para que se subdivida. Devuelve cuántos triángulos agregó.
        /// </summary>
        static int Superficie(MallaB m, IList<Vector2> poli, System.Func<Vector2, float> altura, float ladoMax, float mosaico, float giroUV = 0f)
        {
            var pts = new List<Vector2>(SinRepetidos(poli, true));
            var tri = Triangular(pts);
            // Subdivisión: en cada pasada se parten por la mitad los lados más largos que ladoMax. Los
            // dos triángulos que comparten un lado usan el mismo punto medio, así no quedan rendijas.
            for (int pasada = 0; pasada < 12; pasada++)
            {
                var medios = new Dictionary<long, int>();
                System.Func<int, int, int> medio = (a, b) =>
                {
                    if (Vector2.Distance(pts[a], pts[b]) <= ladoMax) return -1;
                    long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    int i;
                    if (medios.TryGetValue(k, out i)) return i;
                    pts.Add((pts[a] + pts[b]) * 0.5f);
                    medios[k] = pts.Count - 1;
                    return pts.Count - 1;
                };
                var nuevo = new List<int>(tri.Count * 2);
                bool partio = false;
                for (int i = 0; i < tri.Count; i += 3)
                {
                    int a = tri[i], b = tri[i + 1], c = tri[i + 2];
                    int ab = medio(a, b), bc = medio(b, c), ca = medio(c, a);
                    int cuantos = (ab >= 0 ? 1 : 0) + (bc >= 0 ? 1 : 0) + (ca >= 0 ? 1 : 0);
                    if (cuantos == 0) { nuevo.Add(a); nuevo.Add(b); nuevo.Add(c); continue; }
                    partio = true;
                    if (cuantos == 3) nuevo.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                    else if (cuantos == 1)
                    {
                        // Se gira el triángulo para que el lado partido sea el primero.
                        if (bc >= 0) { int t = a; a = b; b = c; c = t; ab = bc; }
                        else if (ca >= 0) { int t = c; c = b; b = a; a = t; ab = ca; }
                        nuevo.AddRange(new[] { a, ab, c, ab, b, c });
                    }
                    else
                    {
                        // Dos lados partidos: se gira para que el entero sea el último (c → a).
                        if (ab < 0) { int t = a; a = b; b = c; c = t; ab = bc; bc = ca; }
                        else if (bc < 0) { int t = c; c = b; b = a; a = t; bc = ab; ab = ca; }
                        nuevo.AddRange(new[] { ab, b, bc, a, ab, bc, a, bc, c });
                    }
                }
                tri = nuevo;
                if (!partio) break;
            }
            int v0 = m.v.Count, t0 = m.t.Count;
            foreach (var q in pts) m.Vert(V3(q, altura(q)), Vector3.up, UVPlanta(q, mosaico, giroUV));
            for (int i = 0; i < tri.Count; i += 3) m.Tri(v0 + tri[i], v0 + tri[i + 1], v0 + tri[i + 2], Vector3.up);
            m.NormalesSuaves(v0, t0);
            return (m.t.Count - t0) / 3;
        }

        /// <summary>Costado vertical alrededor de un polígono, desde su borde (a la altura dada) hasta <paramref name="yAbajo"/>.</summary>
        static void Faldon(MallaB m, IList<Vector2> poli, System.Func<Vector2, float> altura, float yAbajo, float mosaico = 1f)
        {
            var p = Antihorario(SinRepetidos(poli, true));
            float u = 0f;
            for (int i = 0; i < p.Count; i++)
            {
                Vector2 a = p[i], b = p[(i + 1) % p.Count];
                Vector2 d = (b - a).normalized;
                var afuera = new Vector3(d.y, 0f, -d.x);   // a la derecha del avance = afuera (polígono antihorario)
                float largo = Vector2.Distance(a, b);
                float ya = altura(a), yb = altura(b);
                m.Cuad(V3(a, ya), V3(b, yb), V3(b, yAbajo), V3(a, yAbajo), afuera,
                    new Vector2(u / mosaico, ya / mosaico), new Vector2((u + largo) / mosaico, yb / mosaico),
                    new Vector2((u + largo) / mosaico, yAbajo / mosaico), new Vector2(u / mosaico, yAbajo / mosaico));
                u += largo;
            }
        }

        /// <summary>
        /// Barre un perfil a lo largo de un camino en planta. Perfil: puntos (desvío lateral, altura);
        /// el desvío positivo queda a la izquierda del sentido de avance. Cada tramo del perfil es una
        /// cara con aristas vivas, orientada hacia afuera del perfil.
        /// </summary>
        static void Barrido(MallaB m, IList<Vector2> camino, bool cerrado, Vector2[] perfil, bool perfilCerrado, float mosaicoU, float mosaicoV, bool tapas)
        {
            var c = SinRepetidos(camino, cerrado);
            int n = c.Count;
            if (n < 2) return;
            // Normal lateral (izquierda) y alargue en cada punto del camino.
            var lado = new Vector2[n];
            var alargue = new float[n];
            var tangente = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 antes, despues;
                if (cerrado) { antes = c[(i - 1 + n) % n]; despues = c[(i + 1) % n]; }
                else { antes = i > 0 ? c[i - 1] : c[i]; despues = i < n - 1 ? c[i + 1] : c[i]; }
                Vector2 d0 = (c[i] - antes).normalized, d1 = (despues - c[i]).normalized;
                if (d0 == Vector2.zero) d0 = d1;
                if (d1 == Vector2.zero) d1 = d0;
                Vector2 n0 = new Vector2(-d0.y, d0.x), n1 = new Vector2(-d1.y, d1.x);
                Vector2 nm = (n0 + n1).normalized;
                if (nm == Vector2.zero) nm = n0;
                lado[i] = nm;
                alargue[i] = 1f / Mathf.Max(0.4f, Vector2.Dot(nm, n0));
                tangente[i] = (d0 + d1).normalized;
                if (tangente[i] == Vector2.zero) tangente[i] = d1;
            }
            var acum = new float[n + 1];
            for (int i = 1; i <= n; i++) acum[i] = acum[i - 1] + Vector2.Distance(c[i - 1], c[i % n]);

            // Centro del perfil, para saber hacia dónde es "afuera".
            Vector2 centro = Vector2.zero;
            foreach (var q in perfil) centro += q;
            centro /= perfil.Length;

            int caras = perfilCerrado ? perfil.Length : perfil.Length - 1;
            int pasos = cerrado ? n : n - 1;
            float vAcum = 0f;
            for (int k = 0; k < caras; k++)
            {
                Vector2 q0 = perfil[k], q1 = perfil[(k + 1) % perfil.Length];
                Vector2 seg = q1 - q0;
                float largoSeg = seg.magnitude;
                if (largoSeg < 1e-5f) continue;
                Vector2 n2 = new Vector2(seg.y, -seg.x) / largoSeg;
                float haciaAfuera = Vector2.Dot(n2, (q0 + q1) * 0.5f - centro);
                // Perfil de un solo tramo (una cinta): no hay "afuera", así que mira hacia arriba.
                if (haciaAfuera < -1e-5f || (Mathf.Abs(haciaAfuera) <= 1e-5f && n2.y < 0f)) n2 = -n2;
                int v0 = m.v.Count;
                for (int i = 0; i <= pasos; i++)
                {
                    int j = i % n;
                    Vector3 normal = new Vector3(lado[j].x * n2.x, n2.y, lado[j].y * n2.x).normalized;
                    Vector2 a = c[j] + lado[j] * (q0.x * alargue[j]), b = c[j] + lado[j] * (q1.x * alargue[j]);
                    m.Vert(V3(a, q0.y), normal, new Vector2(acum[i] / mosaicoU, vAcum / mosaicoV));
                    m.Vert(V3(b, q1.y), normal, new Vector2(acum[i] / mosaicoU, (vAcum + largoSeg) / mosaicoV));
                }
                for (int i = 0; i < pasos; i++)
                {
                    int a = v0 + i * 2, b = a + 1, a2 = a + 2, b2 = a + 3;
                    Vector3 hacia = m.n[a] + m.n[a2];
                    m.Tri(a, b, b2, hacia);
                    m.Tri(a, b2, a2, hacia);
                }
                vAcum += largoSeg;
            }

            if (tapas && !cerrado)
            {
                var tri = Triangular(perfil);
                for (int extremo = 0; extremo < 2; extremo++)
                {
                    int j = extremo == 0 ? 0 : n - 1;
                    Vector2 d = extremo == 0 ? -tangente[j] : tangente[j];
                    var normal = new Vector3(d.x, 0f, d.y);
                    int v0 = m.v.Count;
                    foreach (var q in perfil) m.Vert(V3(c[j] + lado[j] * (q.x * alargue[j]), q.y), normal, new Vector2(q.x / mosaicoU, q.y / mosaicoV));
                    for (int i = 0; i < tri.Count; i += 3) m.Tri(v0 + tri[i], v0 + tri[i + 1], v0 + tri[i + 2], normal);
                }
            }
        }

        /// <summary>
        /// Banda de terreno entre dos curvas abiertas que arrancan y terminan juntas (o casi): la de
        /// adentro (v = 0) y la de afuera (v = 1). Las une con "costillas" que avanzan por las dos a la
        /// vez, buscando en la de afuera el punto más cercano. U = metros recorridos sobre la de adentro.
        /// </summary>
        static void Banda(MallaB m, IList<Vector2> adentro, IList<Vector2> afuera, int filas, System.Func<float, Vector2, float> altura, float mosaicoU, int puntasDobles = 8)
        {
            var a = SinRepetidos(adentro, false);
            var b = SinRepetidos(afuera, false);
            float[] sa = Acumulado(a), sb = Acumulado(b);
            float largoA = sa[a.Count - 1], largoB = sb[b.Count - 1];

            // Recorrido conjunto: pares (distancia en adentro, distancia en afuera), siempre hacia adelante.
            var pares = new List<Vector2> { Vector2.zero };
            const float pasoA = 0.45f, ventana = 7f;
            float tb = 0f;
            int cantidad = Mathf.CeilToInt(largoA / pasoA);
            for (int i = 1; i <= cantidad; i++)
            {
                float s = largoA * i / cantidad;
                Vector2 dir;
                Vector2 q = PuntoEn(a, sa, s, out dir);
                float mejorT = tb, mejorD = float.MaxValue;
                if (i == cantidad) mejorT = largoB;
                else
                {
                    for (int k = 1; k < b.Count; k++)
                    {
                        if (sb[k] < tb || sb[k - 1] > tb + ventana) continue;
                        Vector2 ab = b[k] - b[k - 1];
                        float l2 = ab.sqrMagnitude;
                        float u = l2 > 1e-10f ? Mathf.Clamp01(Vector2.Dot(q - b[k - 1], ab) / l2) : 0f;
                        float t = Mathf.Clamp(sb[k - 1] + (sb[k] - sb[k - 1]) * u, tb, tb + ventana);
                        Vector2 dirB;
                        float d = Vector2.Distance(q, PuntoEn(b, sb, t, out dirB));
                        if (d < mejorD) { mejorD = d; mejorT = t; }
                    }
                }
                // Tramos cortos en las dos curvas: si la de afuera avanza mucho, se intercalan costillas.
                Vector2 previo = pares[pares.Count - 1];
                int cortes = Mathf.Max(1, Mathf.CeilToInt((mejorT - previo.y) / 0.6f));
                for (int k = 1; k <= cortes; k++)
                    pares.Add(new Vector2(previo.x + (s - previo.x) * k / cortes, previo.y + (mejorT - previo.y) * k / cortes));
                tb = mejorT;
            }

            int v0 = m.v.Count, t0 = m.t.Count;
            foreach (var par in pares)
            {
                Vector2 d;
                Vector2 pa = PuntoEn(a, sa, par.x, out d), pb = PuntoEn(b, sb, par.y, out d);
                for (int f = 0; f <= filas; f++)
                {
                    float v = (float)f / filas;
                    Vector2 q = pa + (pb - pa) * v;
                    m.Vert(V3(q, altura(v, q)), Vector3.up, new Vector2(par.x / mosaicoU, v));
                }
            }
            int porCostilla = filas + 1;
            // En las dos puntas la banda es muy angosta y queda casi vertical: de esos tramos se
            // guardan los triángulos para agregarlos también del revés, así cierra desde cualquier lado.
            var enPuntas = new List<int>();
            for (int i = 0; i + 1 < pares.Count; i++)
            {
                int antes = m.t.Count;
                for (int f = 0; f < filas; f++)
                {
                    int p00 = v0 + i * porCostilla + f, p01 = p00 + 1, p10 = p00 + porCostilla, p11 = p10 + 1;
                    m.Tri(p00, p10, p11, Vector3.up);
                    m.Tri(p00, p11, p01, Vector3.up);
                }
                if (i < puntasDobles || i >= pares.Count - 1 - puntasDobles)
                    for (int k = antes; k < m.t.Count; k++) enPuntas.Add(m.t[k]);
            }
            m.NormalesSuaves(v0, t0);
            for (int k = 0; k + 2 < enPuntas.Count; k += 3)
            {
                m.t.Add(enPuntas[k]); m.t.Add(enPuntas[k + 2]); m.t.Add(enPuntas[k + 1]);
            }
        }
    }
}
