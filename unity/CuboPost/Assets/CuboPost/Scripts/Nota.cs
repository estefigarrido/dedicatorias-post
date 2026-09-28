using System;

namespace CuboPost
{
    /// <summary>Una dedicatoria, con los mismos campos que la tabla "notas" de Supabase.</summary>
    [Serializable]
    public class Nota
    {
        public long id;
        public string para;
        public string mensaje;
        public string color;
        public string hex;
        public string tamano;
        public string creada;

        /// <summary>Notas de ejemplo que se muestran mientras no haya suficientes reales.</summary>
        [NonSerialized] public bool demo;

        public string Tamano => string.IsNullOrEmpty(tamano)
            ? PaletaPost.TamanoPorLargo((mensaje ?? "").Length)
            : tamano;
    }

    [Serializable]
    internal class ListaNotas
    {
        public Nota[] items;
    }
}
