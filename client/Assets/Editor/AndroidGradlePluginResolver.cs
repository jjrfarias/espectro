#if UNITY_EDITOR && UNITY_ANDROID
using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace Espectro.Editor
{
    public sealed class AndroidGradlePluginResolver : IPostGenerateGradleAndroidProject
    {
        private const string Marker = "// Espectro: resolve Android plugin IDs from Google's AGP module";

        public int callbackOrder => -1000;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            var gradleRoot = Directory.GetParent(unityLibraryPath)?.FullName;
            if (string.IsNullOrWhiteSpace(gradleRoot))
            {
                throw new DirectoryNotFoundException("Não foi possível localizar a raiz do projeto Gradle.");
            }

            var settingsPath = Path.Combine(gradleRoot, "settings.gradle");
            var settings = File.ReadAllText(settingsPath);
            if (settings.Contains(Marker))
            {
                return;
            }

            const string opening = "pluginManagement {";
            var resolution = opening + "\n" +
                "    " + Marker + "\n" +
                "    resolutionStrategy {\n" +
                "        eachPlugin {\n" +
                "            if (requested.id.id == 'com.android.application' || requested.id.id == 'com.android.library') {\n" +
                "                useModule(\"com.android.tools.build:gradle:${requested.version}\")\n" +
                "            }\n" +
                "        }\n" +
                "    }";

            settings = settings.Replace(opening, resolution);
            File.WriteAllText(settingsPath, settings);
            Debug.Log("Resolução do Android Gradle Plugin configurada.");
        }
    }
}
#endif

