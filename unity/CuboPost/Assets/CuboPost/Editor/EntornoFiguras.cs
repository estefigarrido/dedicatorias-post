using System.Collections.Generic;
using UnityEngine;

namespace CuboPost.EditorTools
{
    /// <summary>
    /// Figuras humanas sueltas, con la misma forma que las de la plaza ("Gente de referencia"):
    /// las usa el stand para la fila y para el visitante con el que se recorre la escena.
    /// No dependen de que la plaza esté armada.
    /// </summary>
    public static partial class ConstruirEntorno
    {
        /// <summary>Partes de una figura, en el orden en que las devuelve <see cref="MallasDeFigura"/>.</summary>
        internal static readonly string[] PartesDeFigura = { "Cuerpo", "Pierna izquierda", "Pierna derecha", "Brazo izquierdo", "Brazo derecho" };

        // Medidas de la figura sin escalar: de los pies a la coronilla y altura de la cadera.
        const float FiguraAlto = 1.735f, FiguraCadera = 0.9f;

        /// <summary>
        /// Figura de pie en cinco partes: pies en y = 0, mira hacia +Z y mide <paramref name="altura"/>.
        /// El cuerpo (tronco, hombros, cuello y cabeza) tiene el origen en los pies; cada pierna, en
        /// la cadera, y cada brazo, en el hombro, para poder balancearlos al caminar.
        /// <paramref name="pivotes"/>: dónde va cada parte, medido desde los pies.
        /// </summary>
        internal static Mesh[] MallasDeFigura(float altura, out Vector3[] pivotes)
        {
            Mesh tubo = CilindroBajo(10, true), tronco = CilindroBajo(12, true), bola = EsferaLisa();
            float k = altura / FiguraAlto;
            var piezas = PiezasDeFigura(tubo, tronco, bola, out pivotes);
            var mallas = new Mesh[piezas.Length];
            for (int i = 0; i < piezas.Length; i++)
            {
                // Cada parte queda con el origen en su pivote y ya a la escala pedida.
                var alPivote = Matrix4x4.Scale(Vector3.one * k) * Matrix4x4.Translate(-pivotes[i]);
                var lista = piezas[i].ToArray();
                for (int j = 0; j < lista.Length; j++) lista[j].transform = alPivote * lista[j].transform;
                mallas[i] = Unir("Figura · " + PartesDeFigura[i].ToLowerInvariant(), lista);
                pivotes[i] *= k;
            }
            foreach (var forma in new[] { tubo, tronco, bola }) Object.DestroyImmediate(forma);
            return mallas;
        }

        /// <summary>La misma figura en una sola malla (para la gente que no se mueve).</summary>
        internal static Mesh MallaDeFigura(float altura)
        {
            Mesh tubo = CilindroBajo(10, true), tronco = CilindroBajo(12, true), bola = EsferaLisa();
            var escala = Matrix4x4.Scale(Vector3.one * (altura / FiguraAlto));
            var todas = new List<CombineInstance>();
            foreach (var parte in PiezasDeFigura(tubo, tronco, bola, out _))
                foreach (var pieza in parte)
                    todas.Add(new CombineInstance { mesh = pieza.mesh, transform = escala * pieza.transform });
            var malla = Unir("Figura de pie", todas.ToArray());
            foreach (var forma in new[] { tubo, tronco, bola }) Object.DestroyImmediate(forma);
            return malla;
        }

        /// <summary>
        /// Las piezas de la figura (sin escalar), agrupadas por parte. Son las mismas proporciones
        /// que usa <see cref="Figura"/> para la gente de la plaza.
        /// </summary>
        static List<CombineInstance>[] PiezasDeFigura(Mesh tubo, Mesh tronco, Mesh bola, out Vector3[] pivotes)
        {
            const float c = FiguraCadera;
            pivotes = new[]
            {
                Vector3.zero,
                new Vector3(-0.09f, c, 0f), new Vector3(0.09f, c, 0f),                  // caderas
                new Vector3(-0.21f, c + 0.48f, 0f), new Vector3(0.21f, c + 0.48f, 0f),  // hombros
            };
            var piezas = new List<CombineInstance>[pivotes.Length];
            for (int i = 0; i < piezas.Length; i++) piezas[i] = new List<CombineInstance>();
            System.Action<int, Mesh, Matrix4x4> pieza = (parte, malla, m) => piezas[parte].Add(new CombineInstance { mesh = malla, transform = m });
            System.Func<Vector3, Vector3, float, Matrix4x4> entre = (a, b, radio) =>
                Matrix4x4.TRS((a + b) / 2f, Quaternion.FromToRotation(Vector3.up, b - a), new Vector3(radio * 2f, (b - a).magnitude / 2f, radio * 2f));

            for (int lado = 0; lado < 2; lado++)
            {
                float l = lado == 0 ? -1f : 1f;   // izquierda = -X (la figura mira hacia +Z)
                pieza(1 + lado, tubo, entre(new Vector3(l * 0.085f, 0f, 0f), new Vector3(l * 0.095f, c + 0.04f, 0f), 0.078f));
                pieza(3 + lado, tubo, entre(new Vector3(l * 0.21f, c + 0.5f, 0f), new Vector3(l * 0.24f, c - 0.06f, 0.03f), 0.048f));
            }
            // Tronco (cilindro achatado), hombros, cuello y cabeza.
            pieza(0, tronco, Matrix4x4.TRS(new Vector3(0f, c + 0.27f, 0f), Quaternion.identity, new Vector3(0.36f, 0.27f, 0.22f)));
            pieza(0, bola, Matrix4x4.TRS(new Vector3(0f, c + 0.5f, 0f), Quaternion.identity, new Vector3(0.44f, 0.18f, 0.24f)));
            pieza(0, tubo, entre(new Vector3(0f, c + 0.52f, 0f), new Vector3(0f, c + 0.64f, 0f), 0.05f));
            pieza(0, bola, Matrix4x4.TRS(new Vector3(0f, c + 0.72f, 0.01f), Quaternion.identity, new Vector3(0.19f, 0.23f, 0.21f)));
            return piezas;
        }

        static Mesh Unir(string nombre, CombineInstance[] piezas)
        {
            var malla = new Mesh { name = nombre };
            malla.CombineMeshes(piezas, true, true);
            // Material liso: alcanza con posiciones y normales.
            malla.uv = null;
            malla.uv2 = null;
            malla.tangents = null;
            malla.RecalculateBounds();
            return malla;
        }
    }
}
