using System.IO;
using UnityEditor;
using UnityEngine;

namespace CuboPost.EditorTools
{
    /// <summary>
    /// Arma el ejecutable de Windows (no hace falta tener Unity para abrirlo) en Builds/post-cubo.
    /// Desde el menú post. o desde la consola:
    ///   Unity.exe -batchmode -quit -projectPath unity/CuboPost -executeMethod CuboPost.EditorTools.EmpaquetarWindows.Empaquetar
    /// </summary>
    public static class EmpaquetarWindows
    {
        const string Carpeta = "Builds/post-cubo";

        [MenuItem("post./Empaquetar para Windows")]
        public static void Empaquetar()
        {
            var carpeta = Path.GetFullPath(Carpeta);
            if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true);
            var op = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/CuboPost.unity" },
                locationPathName = Path.Combine(carpeta, "post-cubo.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var r = BuildPipeline.BuildPlayer(op).summary;
            Debug.Log($"[post.] Empaquetado: {r.result} · {r.totalSize / 1048576} MB · {r.totalTime} · errores {r.totalErrors} → {carpeta}");
            if (Application.isBatchMode && r.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
