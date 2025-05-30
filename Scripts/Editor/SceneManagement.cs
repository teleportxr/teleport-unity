﻿using UnityEditor;
using UnityEditor.SceneManagement;

namespace teleport
{
    public class SceneManagement 
    {
        [MenuItem("Teleport XR/Open Default Scene", false, 2003)]
		public static void OpenResourceWindow()
        {
            var settings = TeleportSettings.GetOrCreateSettings();

            OpenScene(settings.defaultScene);
            OpenScene(settings.additiveScene, OpenSceneMode.Additive); 
        }

        private static void OpenScene(string scene, OpenSceneMode mode = OpenSceneMode.Single)
        {
            string extension = ".unity";
            if (scene.Length > 0)
            {
                if (!scene.EndsWith(extension))
                {
                    scene += extension;
                }
                EditorSceneManager.OpenScene(scene, mode);
            }
        }
    }
}