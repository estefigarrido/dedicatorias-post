// Archivo generado: trazado de la Plaza de la República (lado norte del Obelisco) hecho sobre la
// foto satelital que está en el Figma ("prueba mapa", implantación), a la misma escala que los
// planos del stand (100 px = 1 m). Metros; +X = este, +Z = norte; el stand está en el origen.
// Cada lista es x0, z0, x1, z1, ...
using UnityEngine;

namespace CuboPost.EditorTools
{
    public static partial class ConstruirEntorno
    {
        /// <summary>Eje del murete de piedra (el anillo): retorno oeste, lado oeste, arco, lado este y retorno este.</summary>
        static readonly float[] dAnillo =
        {
            -17.56f, -8.60f, -14.54f, -8.60f, -14.54f, -6.36f, -14.58f, -2.04f, -14.62f, 0.11f, -14.54f, 1.41f, -14.41f, 2.27f, -14.15f, 3.35f, -13.80f, 4.43f,
            -13.37f, 5.51f, -12.85f, 6.59f, -12.29f, 7.54f, -11.65f, 8.49f, -11.00f, 9.31f, -10.35f, 10.04f, -9.49f, 10.86f, -8.62f, 11.55f, -7.76f, 12.20f,
            -6.90f, 12.76f, -6.17f, 13.36f, -5.82f, 13.58f, -5.17f, 13.62f, -3.88f, 13.45f, -2.80f, 13.28f, -1.72f, 13.10f, -0.64f, 13.02f, 0.44f, 12.97f, 1.52f, 13.02f,
            2.60f, 13.15f, 3.67f, 13.36f, 4.75f, 13.62f, 5.83f, 13.97f, 6.48f, 14.18f, 6.91f, 14.27f, 7.77f, 14.01f, 9.07f, 13.28f, 10.36f, 12.28f, 11.23f, 11.55f,
            12.31f, 10.56f, 13.38f, 9.48f, 14.46f, 8.23f, 15.54f, 6.80f, 16.41f, 5.51f, 17.05f, 4.30f, 17.48f, 3.22f, 17.79f, 2.27f, 18.05f, 1.19f, 18.26f, 0.11f,
            18.39f, -2.04f, 18.52f, -4.20f, 18.61f, -7.01f, 21.45f, -6.96f
        };
        /// <summary>Puntos de <see cref="dAnillo"/> que son esquinas vivas (el resto se suaviza).</summary>
        static readonly int[] dAnilloEsquinas = { 1, 19, 33, 50 };
        /// <summary>Borde de afuera del césped que rodea al anillo, de la punta oeste a la punta este.</summary>
        static readonly float[] dCespedAnillo =
        {
            -17.34f, -8.60f, -17.43f, -6.58f, -19.59f, -5.50f, -19.59f, -2.04f, -19.50f, 1.41f, -19.33f, 4.43f, -19.15f, 6.59f, -19.33f, 8.96f, -18.77f, 9.61f,
            -16.82f, 11.33f, -14.67f, 13.28f, -12.51f, 15.22f, -10.35f, 16.81f, -8.19f, 17.94f, -6.47f, 18.58f, -3.45f, 19.27f, 1.09f, 19.40f, 5.62f, 19.36f,
            9.28f, 19.19f, 11.23f, 18.33f, 13.38f, 17.03f, 15.54f, 15.65f, 17.61f, 14.61f, 18.13f, 13.06f, 20.29f, 8.74f, 21.89f, 4.43f, 22.02f, 1.41f, 22.02f, -4.20f,
            21.24f, -5.93f, 21.15f, -6.88f
        };
        /// <summary>Cordón de toda la plaza (sentido antihorario visto desde arriba).</summary>
        static readonly float[] dCordon =
        {
            -17.36f, 70.83f, -17.87f, 64.35f, -19.03f, 57.40f, -19.45f, 52.77f, -20.05f, 48.14f, -20.47f, 45.82f, -20.88f, 41.19f, -21.58f, 36.56f, -22.18f, 31.92f,
            -22.97f, 27.29f, -23.82f, 23.85f, -24.16f, 19.53f, -24.51f, 15.22f, -24.77f, 10.90f, -24.89f, 6.59f, -24.89f, 2.27f, -24.77f, -2.04f, -24.51f, -6.36f,
            -24.16f, -10.68f, -23.82f, -14.13f, -23.51f, -17.15f, -23.08f, -19.31f, -22.43f, -20.82f, -21.14f, -22.11f, -19.41f, -23.10f, -17.26f, -23.54f,
            -14.67f, -23.54f, -12.51f, -23.06f, -10.35f, -22.41f, -8.19f, -21.90f, -6.04f, -21.42f, -3.88f, -21.12f, -1.72f, -20.95f, 0.44f, -20.86f, 2.60f, -20.82f,
            6.91f, -20.77f, 11.23f, -20.82f, 15.54f, -21.03f, 17.70f, -21.29f, 19.86f, -21.46f, 22.45f, -21.59f, 24.61f, -21.64f, 26.33f, -21.33f, 27.54f, -20.51f,
            28.06f, -19.31f, 28.10f, -17.15f, 28.06f, -16.50f, 26.94f, -14.56f, 26.94f, -10.68f, 26.89f, -6.36f, 26.85f, -2.04f, 26.76f, 1.41f, 26.50f, 4.43f,
            25.68f, 9.09f, 24.26f, 13.79f, 22.10f, 19.36f, 20.46f, 23.85f, 19.14f, 27.29f, 16.82f, 31.92f, 14.41f, 36.56f, 11.40f, 40.72f, 8.62f, 43.50f, 5.38f, 46.98f,
            3.06f, 49.90f, 0.75f, 52.63f, -1.43f, 57.40f, -6.20f, 62.50f, -11.76f, 67.13f, -15.93f, 69.54f
        };
        /// <summary>Explanada: del final del anillo (este) al principio (oeste), pasando por el borde sur.</summary>
        static readonly float[] dExplanadaSur =
        {
            18.91f, -8.52f, 19.12f, -12.83f, 19.30f, -17.15f, 19.43f, -21.29f, 17.70f, -21.29f, 15.54f, -21.03f, 11.23f, -20.82f, 6.91f, -20.77f, 2.60f, -20.82f,
            0.44f, -20.86f, -1.72f, -20.95f, -3.88f, -21.12f, -6.04f, -21.42f, -8.19f, -21.90f, -10.35f, -22.41f, -12.51f, -23.06f, -14.45f, -23.45f, -14.54f, -19.31f,
            -14.67f, -12.83f
        };
        /// <summary>Sendero de hormigón del borde oeste.</summary>
        static readonly float[] dSenderoOeste =
        {
            -15.77f, 66.66f, -15.77f, 57.40f, -15.83f, 45.97f, -16.85f, 41.19f, -18.03f, 36.56f, -19.01f, 31.92f, -19.94f, 27.75f, -21.14f, 23.85f, -21.23f, 17.38f,
            -21.57f, 13.06f, -22.09f, 8.74f, -22.52f, 4.43f, -22.87f, 0.11f, -22.95f, -3.77f, -19.41f, -5.71f, -19.59f, -2.04f, -19.50f, 1.41f, -19.33f, 4.43f,
            -19.15f, 6.59f, -19.33f, 8.96f, -19.50f, 9.61f, -19.33f, 13.06f, -19.07f, 17.38f, -18.98f, 23.85f, -16.85f, 27.75f, -15.77f, 32.70f, -14.78f, 37.64f,
            -14.69f, 41.19f, -13.61f, 45.82f, -13.76f, 57.40f, -13.76f, 66.66f
        };
        /// <summary>Sendero de hormigón del borde este.</summary>
        static readonly float[] dSenderoEste =
        {
            17.61f, 14.61f, 18.13f, 13.06f, 20.29f, 8.74f, 21.89f, 4.43f, 22.02f, 1.41f, 22.02f, -4.20f, 25.90f, -1.61f, 25.68f, 1.41f, 24.43f, 4.43f, 23.87f, 8.74f,
            21.89f, 13.06f, 19.43f, 18.24f, 17.40f, 23.85f, 11.71f, 29.61f, 5.54f, 38.87f, 0.29f, 48.14f, -2.03f, 52.77f, -4.35f, 57.40f, -6.97f, 62.65f, -8.67f, 62.65f,
            -6.66f, 57.40f, -4.35f, 52.77f, -2.18f, 48.14f, 2.45f, 38.87f, 8.01f, 29.61f, 15.63f, 23.85f
        };
        /// <summary>Sendero de polvo de ladrillo que rodea el césped del anillo y pasa frente al cartel BA.</summary>
        static readonly float[] dTerracotaArco =
        {
            -19.41f, 10.90f, -18.33f, 9.61f, -16.82f, 11.33f, -14.67f, 13.28f, -12.51f, 15.22f, -10.35f, 16.81f, -8.19f, 17.94f, -6.47f, 18.58f, -3.45f, 19.27f,
            1.09f, 19.40f, 5.62f, 19.36f, 9.28f, 19.19f, 11.23f, 18.33f, 13.38f, 17.03f, 15.54f, 15.65f, 17.61f, 14.61f, 15.76f, 19.02f, 13.82f, 19.97f, 11.44f, 21.48f,
            9.50f, 22.55f, 8.21f, 23.63f, 2.45f, 25.59f, -0.02f, 25.90f, -6.90f, 23.63f, -8.62f, 22.86f, -11.65f, 19.53f, -18.98f, 11.55f
        };
        /// <summary>Senderos de polvo de ladrillo del jardín norte.</summary>
        static readonly float[] dTerracotaTronco =
        {
            -12.84f, 66.66f, -12.84f, 57.40f, -12.38f, 54.31f, -11.45f, 51.22f, -10.52f, 48.14f, -9.60f, 45.05f, -8.67f, 42.58f, -7.13f, 40.11f, -5.89f, 37.64f,
            -7.43f, 38.10f, -10.83f, 39.18f, -14.85f, 39.64f, -15.46f, 37.64f, -11.45f, 37.33f, -7.43f, 36.40f, -4.66f, 35.17f, -3.11f, 33.00f, -2.49f, 31.77f,
            -6.20f, 25.90f, -4.96f, 25.59f, -1.88f, 30.84f, -1.26f, 29.61f, -0.33f, 27.75f, -0.02f, 25.28f, 1.83f, 25.28f, 2.14f, 28.06f, 2.45f, 31.15f, 2.45f, 35.17f,
            3.06f, 37.33f, 3.68f, 38.87f, 2.45f, 40.11f, 1.21f, 41.34f, 0.90f, 38.87f, -0.02f, 35.47f, -0.33f, 35.47f, -1.88f, 36.40f, -3.42f, 38.25f, -4.96f, 40.72f,
            -6.51f, 43.50f, -7.43f, 45.67f, -8.21f, 48.14f, -9.29f, 51.22f, -10.37f, 54.31f, -10.99f, 57.40f, -10.99f, 66.66f
        };
        /// <summary>Cantero oscuro del jardín norte (centro).</summary>
        static readonly float[] dCantero1 =
        {
            -6.20f, 43.50f, -4.96f, 41.34f, -3.11f, 38.87f, -0.80f, 35.63f, -0.02f, 35.78f, 1.21f, 41.34f, -1.26f, 45.05f, -3.11f, 46.59f, -6.51f, 45.05f
        };
        /// <summary>Cantero oscuro del jardín norte (oeste).</summary>
        static readonly float[] dCantero2 =
        {
            -15.46f, 34.86f, -11.45f, 33.62f, -7.43f, 32.70f, -3.73f, 31.77f, -3.42f, 32.70f, -4.66f, 34.86f, -7.43f, 36.09f, -11.45f, 37.02f, -15.46f, 37.33f
        };
        /// <summary>Césped del jardín norte (sudoeste).</summary>
        static readonly float[] dPastoSO =
        {
            -15.93f, 32.70f, -12.99f, 30.23f, -9.90f, 27.75f, -6.20f, 26.21f, -3.11f, 31.61f, -3.73f, 31.46f, -7.43f, 32.39f, -11.45f, 33.31f, -15.62f, 34.39f
        };
        /// <summary>Césped triangular junto al sendero central.</summary>
        static readonly float[] dPastoTri =
        {
            -4.35f, 25.65f, -0.08f, 25.90f, -0.33f, 27.60f, -1.72f, 30.69f
        };
        /// <summary>Césped del jardín norte (este).</summary>
        static readonly float[] dPastoE =
        {
            2.51f, 31.15f, 2.14f, 28.06f, 2.45f, 25.59f, 6.15f, 25.28f, 9.24f, 24.67f, 11.40f, 24.98f, 7.23f, 29.61f, 4.61f, 33.93f, 3.37f, 34.55f, 2.60f, 33.62f
        };
        /// <summary>Macizo de arbustos al noroeste del anillo.</summary>
        static readonly float[] dArbustosNO =
        {
            -18.98f, 23.85f, -18.98f, 11.55f, -7.68f, 23.85f, -6.20f, 26.06f, -9.90f, 27.60f, -12.99f, 30.07f, -15.93f, 32.54f, -17.01f, 27.75f
        };
        /// <summary>Vereda de baldosas de la esquina sudoeste.</summary>
        static readonly float[] dVeredaSO =
        {
            -24.68f, -3.77f, -24.42f, -6.36f, -24.07f, -10.68f, -23.73f, -14.13f, -23.43f, -17.15f, -23.00f, -19.31f, -22.39f, -20.73f, -21.14f, -22.03f, -19.41f, -23.02f,
            -17.26f, -23.45f, -14.45f, -23.45f, -14.54f, -19.31f, -14.67f, -12.83f, -14.54f, -8.60f, -17.34f, -8.60f, -17.43f, -6.58f, -19.41f, -5.71f, -23.95f, -3.77f
        };
        /// <summary>Vereda de baldosas de la esquina sudeste.</summary>
        static readonly float[] dVeredaSE =
        {
            18.91f, -8.52f, 19.12f, -12.83f, 19.30f, -17.15f, 19.43f, -21.29f, 19.86f, -21.38f, 22.45f, -21.51f, 24.61f, -21.55f, 26.33f, -21.25f, 27.50f, -20.47f,
            28.01f, -19.31f, 27.97f, -16.50f, 26.85f, -14.56f, 26.85f, -10.68f, 26.81f, -6.36f, 26.68f, -2.04f, 25.90f, -1.61f, 22.02f, -4.42f, 21.24f, -5.93f,
            21.15f, -6.88f, 19.86f, -6.96f, 18.61f, -7.01f
        };
        /// <summary>Franja de césped entre el cordón oeste y el sendero.</summary>
        static readonly float[] dFranjaOeste =
        {
            -17.08f, 66.66f, -18.43f, 57.40f, -18.84f, 52.77f, -19.45f, 48.14f, -19.86f, 45.82f, -20.28f, 41.19f, -20.98f, 36.56f, -21.58f, 31.92f, -22.37f, 27.29f,
            -23.25f, 23.85f, -23.60f, 19.53f, -23.95f, 15.22f, -24.20f, 10.90f, -24.33f, 6.59f, -24.33f, 2.27f, -24.20f, -2.04f, -24.07f, -3.77f, -22.95f, -3.77f,
            -22.87f, 0.11f, -22.52f, 4.43f, -22.09f, 8.74f, -21.57f, 13.06f, -21.23f, 17.38f, -21.14f, 23.85f, -19.94f, 27.75f, -19.01f, 31.92f, -18.03f, 36.56f,
            -16.85f, 41.19f, -15.83f, 45.97f, -15.77f, 57.40f, -15.77f, 66.66f
        };
        /// <summary>Franja de césped entre el sendero este y el cordón (tramo sur).</summary>
        static readonly float[] dFranjaEsteS =
        {
            17.40f, 23.85f, 19.43f, 18.24f, 21.89f, 13.06f, 23.87f, 8.74f, 24.43f, 4.43f, 25.68f, 1.41f, 25.90f, -1.61f, 26.33f, -1.87f, 26.24f, 1.41f, 25.99f, 4.43f,
            25.17f, 9.09f, 23.74f, 13.79f, 21.58f, 19.36f, 19.94f, 23.85f
        };
        /// <summary>Franja de plantas entre el sendero este y el cordón (tramo norte).</summary>
        static readonly float[] dFranjaEsteN =
        {
            17.40f, 23.85f, 19.94f, 23.85f, 18.58f, 27.29f, 16.27f, 31.92f, 13.86f, 36.56f, 10.85f, 40.72f, 8.07f, 43.50f, 4.83f, 46.98f, 2.51f, 49.90f, 0.19f, 52.63f,
            -1.98f, 57.40f, -6.66f, 62.50f, -6.97f, 62.65f, -4.35f, 57.40f, -2.03f, 52.77f, 0.29f, 48.14f, 5.54f, 38.87f, 11.71f, 29.61f
        };
        /// <summary>Suelo del jardín: todo lo que queda al norte de las veredas.</summary>
        static readonly float[] dJardinBase =
        {
            -17.36f, 70.60f, -17.69f, 66.66f, -18.71f, 57.40f, -19.12f, 52.77f, -19.72f, 48.14f, -20.14f, 45.82f, -20.56f, 41.19f, -21.25f, 36.56f, -21.86f, 31.92f,
            -22.64f, 27.29f, -23.51f, 23.85f, -23.86f, 19.53f, -24.20f, 15.22f, -24.46f, 10.90f, -24.59f, 6.59f, -24.59f, 2.27f, -24.46f, -2.04f, -24.33f, -3.77f,
            -19.41f, -5.71f, -17.43f, -6.58f, -17.34f, -8.60f, -14.54f, -8.60f, 18.61f, -7.01f, 21.15f, -6.96f, 21.24f, -5.93f, 22.02f, -4.42f, 25.90f, -1.61f,
            26.59f, -2.04f, 26.50f, 1.41f, 26.24f, 4.43f, 25.42f, 9.09f, 24.00f, 13.79f, 21.84f, 19.36f, 20.20f, 23.85f, 18.86f, 27.29f, 16.54f, 31.92f, 14.14f, 36.56f,
            11.12f, 40.72f, 8.35f, 43.50f, 5.10f, 46.98f, 2.79f, 49.90f, 0.47f, 52.63f, -1.71f, 57.40f, -6.43f, 62.50f, -11.94f, 67.13f, -15.93f, 69.35f
        };
        /// <summary>Rejilla de desagüe que cruza la explanada.</summary>
        static readonly float[] dRejilla =
        {
            -14.45f, -18.96f, 18.87f, -17.36f
        };
        /// <summary>Bolardos del borde sur y del cordón este.</summary>
        static readonly float[] dBolardos =
        {
            -13.89f, -23.06f, -11.30f, -22.24f, -8.41f, -21.46f, -5.73f, -20.86f, -3.01f, -20.51f, -0.30f, -20.34f, 2.47f, -20.17f, 5.18f, -20.13f, 7.86f, -20.17f,
            10.58f, -20.17f, 13.26f, -20.26f, 15.76f, -20.30f, 18.26f, -20.47f, 26.85f, -11.32f, 26.89f, -12.83f, 26.94f, -14.21f
        };
        /// <summary>Bases de las dos torres de reflectores de la explanada.</summary>
        static readonly float[] dArcos =
        {
            -12.16f, -9.38f, 16.41f, -8.17f
        };
        /// <summary>Cestos de residuos.</summary>
        static readonly float[] dCestos =
        {
            -13.89f, -2.04f, 17.01f, 2.88f, 11.05f, 10.99f, -15.62f, -9.16f, 19.34f, -7.53f
        };
        /// <summary>Extremos del cartel del cantero noroeste.</summary>
        static readonly float[] dCartel =
        {
            -18.33f, 9.95f, -13.80f, 9.95f
        };
        /// <summary>Dónde hay gente parada (figuras de referencia).</summary>
        static readonly float[] dGenteDePie =
        {
            0.01f, 22.55f, 1.39f, 22.81f, 25.38f, -19.57f, 26.16f, -20.51f, -18.55f, -16.29f, -3.45f, -19.31f, 22.71f, 7.45f, 23.31f, 6.89f, -20.71f, 5.72f
        };
        /// <summary>Arbustos en hilera junto al cordón este.</summary>
        static readonly float[] dMacetas =
        {
            25.12f, 2.70f, 25.12f, 1.62f, 25.12f, 0.55f, 25.12f, -0.53f, 25.12f, -1.48f, 26.12f, 2.70f, 26.12f, 1.62f, 26.12f, 0.55f, 26.12f, -0.53f, 26.12f, -1.48f
        };
        /// <summary>Semáforos de las esquinas.</summary>
        static readonly float[] dSemaforos =
        {
            -20.92f, -19.65f, 26.55f, -21.03f, 26.85f, -6.71f, -23.64f, -13.26f
        };
        /// <summary>Un punto de cada banda clara del piso de la explanada.</summary>
        static readonly float[] dBandas =
        {
            -9.06f, -14.13f, -3.53f, -14.13f, 2.16f, -14.13f, 7.56f, -14.13f, 12.74f, -14.13f
        };
        static readonly Vector2 dMastil = new Vector2(0.96f, 13.66f);
        static readonly Vector2 dGabinete = new Vector2(-16.48f, 9.09f);
        static readonly Vector2 dSenalEste = new Vector2(18.65f, 7.97f);
        static readonly Vector2 dFarolSO = new Vector2(-23.08f, -7.01f);
        /// <summary>Huella de cada letra del cartel BA (x, z, ancho, fondo). La A queda al oeste y la B al este.</summary>
        static readonly Rect dLetraA = new Rect(-6.47f, 19.66f, 6.47f, 1.94f);
        static readonly Rect dLetraB = new Rect(2.16f, 19.66f, 6.04f, 1.94f);
    }
}
