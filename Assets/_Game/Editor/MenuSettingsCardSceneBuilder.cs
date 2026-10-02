using Reflectable;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reflectable.Editor
{
    public static class MenuSettingsCardSceneBuilder
    {
        [MenuItem("Tools/REFLECTABLE/Build Settings Card Hierarchy")]
        public static void Build()
        {
            const string scenePath = "Assets/Scenes/MainMenu.unity";
            var scene = SceneManager.GetActiveScene();
            if (scene.path != scenePath)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameObject settingsPanel = null;
            GameObject mainMenuPanel = null;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "SettingsPanel") settingsPanel = child.gameObject;
                else if (child.name == "MainMenuPanel") mainMenuPanel = child.gameObject;
            }

            if (!settingsPanel)
            {
                Debug.LogError("Could not find SettingsPanel in MainMenu.unity.");
                return;
            }

            var settings = settingsPanel.GetComponent<MenuSettingsPanel>();
            if (!settings) settings = settingsPanel.AddComponent<MenuSettingsPanel>();
            settings.Build(mainMenuPanel);
            EditorUtility.SetDirty(settingsPanel);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("SettingsCard hierarchy saved to MainMenu.unity.");
        }
    }
}
