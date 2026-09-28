using UnityEngine;

namespace CuboPost
{
    /// <summary>Colores de la marca post. / SPOT! y medidas de las notitas del Figma.</summary>
    public static class PaletaPost
    {
        public static readonly Color Oscuro = Hex("#252525");
        public static readonly Color Crema = Hex("#ede8db");
        public static readonly Color Verde = Hex("#49b867");

        // Colores de las notas (los que eligen en la web).
        public static readonly Color NotaAzul = Hex("#7aa1ff");
        public static readonly Color NotaVerde = Hex("#49b867");
        public static readonly Color NotaVioleta = Hex("#ab8ae6");
        public static readonly Color NotaRosa = Hex("#f79ee1");

        // Paleta "sprout" para los puntitos (estilo de las pantallas SPOT!).
        public static readonly Color SproutLima = Hex("#b4fa5a");
        public static readonly Color SproutAqua = Hex("#14dcf0");
        public static readonly Color SproutVioleta = Hex("#9b64ff");

        /// <summary>Opacidad del relleno de las notas (en el Figma es 80 %).</summary>
        public const float OpacidadNota = 0.8f;

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }

        public static Color ColorNota(string nombre)
        {
            switch (nombre)
            {
                case "azul": return NotaAzul;
                case "violeta": return NotaVioleta;
                case "rosa": return NotaRosa;
                default: return NotaVerde;
            }
        }

        /// <summary>Ancho de tarjetita1..4 en unidades de diseño (Figma). El alto es HUG.</summary>
        public static float AnchoNota(string tamano)
        {
            switch (tamano)
            {
                case "S": return 619f;
                case "L": return 1044f;
                case "XL": return 1299f;
                default: return 956f;
            }
        }

        /// <summary>Mismo corte que la web y la base: S ≤ 40, M ≤ 120, L ≤ 170, XL ≤ 250 caracteres.</summary>
        public static string TamanoPorLargo(int largo)
        {
            if (largo <= 40) return "S";
            if (largo <= 120) return "M";
            if (largo <= 170) return "L";
            return "XL";
        }
    }
}
