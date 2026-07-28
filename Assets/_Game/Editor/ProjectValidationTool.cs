using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Reflectable.Editor
{
    public static class ProjectValidationTool
    {
        static readonly List<string> Errors = new List<string>();
        static readonly List<string> Warnings = new List<string>();

        [MenuItem("Tools/Project Validation")]
        public static void ValidateProject()
        {
            Errors.Clear();
            Warnings.Clear();

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Project Validation] Exit Play Mode before validating saved assets.");
                return;
            }

            ValidateBuildScenes();
            ValidatePrefabs();
            ValidateDataAssets();
            ValidateSaveFile();

            foreach (string warning in Warnings) Debug.LogWarning("[Project Validation] " + warning);
            foreach (string error in Errors) Debug.LogError("[Project Validation] " + error);

            if (Errors.Count == 0)
                Debug.Log($"[Project Validation] PASS — 0 errors, {Warnings.Count} warning(s). Build scenes, prefabs, UI, cameras, managers, databases, and save data are persistent.");
            else
                Debug.LogError($"[Project Validation] FAIL — {Errors.Count} error(s), {Warnings.Count} warning(s).");
        }

        static void ValidateBuildScenes()
        {
            var enabledScenes = EditorBuildSettings.scenes.Where(x => x.enabled).ToArray();
            if (enabledScenes.Length == 0)
            {
                Error("Build Settings contains no enabled scenes.");
                return;
            }

            foreach (var buildScene in enabledScenes)
            {
                if (string.IsNullOrEmpty(buildScene.path) || !File.Exists(buildScene.path))
                {
                    Error("Build scene is missing: " + buildScene.path);
                    continue;
                }

                Scene scene = SceneManager.GetSceneByPath(buildScene.path);
                bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
                if (openedForValidation) scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Additive);
                try { ValidateScene(scene); }
                finally { if (openedForValidation) EditorSceneManager.CloseScene(scene, true); }
            }
        }

        static void ValidateScene(Scene scene)
        {
            string context = scene.path;
            var objects = SceneObjects(scene).ToArray();
            foreach (var go in objects)
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (missing > 0) Error($"{context}: {HierarchyPath(go.transform)} has {missing} missing script(s).");
            }

            var cameras = Components<Camera>(scene).Where(x => x.enabled && x.gameObject.activeInHierarchy).ToArray();
            if (cameras.Length == 0) Error(context + ": no active Camera.");
            if (cameras.Count(x => x.CompareTag("MainCamera")) != 1) Error(context + ": expected exactly one active MainCamera.");
            foreach (var camera in cameras)
            {
                if (camera.targetDisplay < 0 || camera.targetDisplay > 7) Error(context + ": invalid Camera target display.");
                if (camera.cullingMask == 0) Error(context + ": Camera culling mask renders nothing.");
            }

            var canvases = Components<Canvas>(scene).Where(x => x.enabled && x.gameObject.activeInHierarchy).ToArray();
            if (canvases.Length == 0) Error(context + ": no active Canvas.");
            foreach (var canvas in canvases)
            {
                if (!canvas.GetComponent<GraphicRaycaster>()) Error(context + ": Canvas is missing GraphicRaycaster.");
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay && !canvas.worldCamera)
                    Error(context + ": non-overlay Canvas is missing its UI Camera.");
            }

            var eventSystems = Components<EventSystem>(scene).Where(x => x.gameObject.activeInHierarchy).ToArray();
            if (eventSystems.Length != 1) Error(context + $": expected one EventSystem, found {eventSystems.Length}.");
            else
            {
                if (!eventSystems[0].GetComponent<InputSystemUIInputModule>())
                    Error(context + ": EventSystem is missing InputSystemUIInputModule.");
                if (eventSystems[0].GetComponents<BaseInputModule>().Length != 1)
                    Error(context + ": EventSystem must have exactly one input module.");
            }

            var gameControllers = Components<ReflectableGameController>(scene).ToArray();
            var menuControllers = Components<ReflectableMenuController>(scene).ToArray();
            if (scene.name == "Game" && gameControllers.Length != 1)
                Error(context + $": expected one ReflectableGameController, found {gameControllers.Length}.");
            if (scene.name == "MainMenu" && menuControllers.Length != 1)
                Error(context + $": expected one ReflectableMenuController, found {menuControllers.Length}.");
            if (Components<GameManager>(scene).Any())
                Error(context + ": obsolete GameManager singleton remains beside ReflectableGameController.");
            if (Components<ReflectableBootstrap>(scene).Any())
                Error(context + ": obsolete ReflectableBootstrap remains in a production scene.");
            foreach (var behaviour in Components<MonoBehaviour>(scene).Where(x => x && x.GetType().Namespace == "Reflectable"))
                ValidateSerializedReferences(behaviour, context + ": " + HierarchyPath(behaviour.transform));

            foreach (var button in Components<Button>(scene))
            {
                if (!button.targetGraphic) Error(context + ": " + HierarchyPath(button.transform) + " Button has no Target Graphic.");
                if (button.onClick.GetPersistentEventCount() == 0)
                    Error(context + ": " + HierarchyPath(button.transform) + " Button has no persistent listener.");
                if (!button.gameObject.activeInHierarchy) continue;
                foreach (var group in button.GetComponentsInParent<CanvasGroup>(true))
                    if (!group.interactable || !group.blocksRaycasts)
                        Error(context + ": " + HierarchyPath(button.transform) + " is blocked by CanvasGroup " + HierarchyPath(group.transform) + ".");
            }
        }

        static void ValidatePrefabs()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!root) { Error("Missing or unreadable prefab: " + path); continue; }
                if (PrefabUtility.GetPrefabAssetType(root) == PrefabAssetType.MissingAsset)
                    Error("Broken nested prefab dependency: " + path);

                foreach (var go in root.GetComponentsInChildren<Transform>(true).Select(x => x.gameObject))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0)
                        Error(path + ": " + HierarchyPath(go.transform) + " has a missing script.");

                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true).Where(x => x && x.GetType().Namespace == "Reflectable"))
                    ValidateSerializedReferences(behaviour, path + ": " + HierarchyPath(behaviour.transform));

                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    if (!button.targetGraphic) Error(path + ": Button has no Target Graphic.");
                    bool isRuntimeWiredHudButton =
                        path == "Assets/_Game/Prefabs/UI/ArcadeHUD/ArcadeGameplayHUD.prefab" ||
                        button.GetComponent<HudUpgradeCard>() ||
                        button.GetComponentInParent<GameplayHudController>();
                    if (!button.GetComponent("CharacterCollectionRow") &&
                        !isRuntimeWiredHudButton &&
                        button.onClick.GetPersistentEventCount() == 0)
                        Warning(path + ": Button has no persistent listener.");
                }
            }
        }

        static void ValidateDataAssets()
        {
            var database = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/_Game/ScriptableObjects/Characters/CharacterDatabase.asset");
            if (!database) Error("CharacterDatabase asset is missing.");
            else
            {
                if (database.characters == null || database.characters.Count != 10)
                    Error("CharacterDatabase must contain exactly 10 saved CharacterData references.");
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var data in database.characters)
                {
                    if (!data) { Error("CharacterDatabase contains a missing CharacterData reference."); continue; }
                    string path = AssetDatabase.GetAssetPath(data);
                    if (string.IsNullOrWhiteSpace(data.characterId) || !ids.Add(data.characterId)) Error(path + ": empty or duplicate characterId.");
                    if (!data.prefab) Error(path + ": missing character prefab.");
                    if (!data.portrait || !data.icon || !data.frontSprite || !data.sideSprite || !data.backSprite)
                        Error(path + ": missing saved portrait/icon/directional sprite reference.");
                }
            }

            var stageCatalog = AssetDatabase.LoadAssetAtPath<ReflectableStageCatalog>("Assets/_Game/Resources/ReflectableStageCatalog.asset");
            if (!stageCatalog) Error("Resources/ReflectableStageCatalog.asset is missing.");
            else
            {
                var serialized = new SerializedObject(stageCatalog);
                var stages = serialized.FindProperty("stages");
                if (stages == null || stages.arraySize != 10) Error("Stage catalog must contain exactly 10 saved stage references.");
                else
                {
                    var stageNumbers = new HashSet<int>();
                    for (int i = 0; i < stages.arraySize; i++)
                    {
                        var data = stages.GetArrayElementAtIndex(i).objectReferenceValue as ReflectableStageData;
                        if (!data) { Error($"Stage catalog entry {i + 1} is missing."); continue; }
                        string path = AssetDatabase.GetAssetPath(data);
                        if (!stageNumbers.Add(data.stageNumber) || data.stageNumber < 1 || data.stageNumber > 10)
                            Error(path + ": invalid or duplicate stage number.");
                        if (!data.stageVisualPrefab || !data.stageSelectPreview)
                            Error(path + ": missing saved stage visual prefab or stage-select preview.");
                        else
                        {
                            string visualPath = AssetDatabase.GetAssetPath(data.stageVisualPrefab);
                            if (string.IsNullOrWhiteSpace(visualPath) ||
                                !visualPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                                !PrefabUtility.IsPartOfPrefabAsset(data.stageVisualPrefab))
                                Error(path + ": stage visual reference does not resolve to a valid prefab GameObject.");
                        }
                    }
                }
            }

            var blockCatalog = AssetDatabase.LoadAssetAtPath<ReflectableBlockVisualCatalog>("Assets/_Game/Resources/ReflectableBlockVisualCatalog.asset");
            if (!blockCatalog) Error("Resources/ReflectableBlockVisualCatalog.asset is missing.");
            else if (!blockCatalog.normalBlock || !blockCatalog.gemBlock || !blockCatalog.bombBlock)
                Error("Block visual catalog has a missing saved block-data reference.");
        }

        static void ValidateSaveFile()
        {
            string path = Path.Combine(Application.persistentDataPath, "reflectable_run.json");
            if (!File.Exists(path)) return;
            try
            {
                var save = JsonUtility.FromJson<ReflectableRunSave>(File.ReadAllText(path));
                if (save == null || !save.valid) Error("Existing run save is unreadable or invalid: " + path);
                else if (save.blocks == null) Error("Existing run save has no block collection: " + path);
                else if (!string.IsNullOrWhiteSpace(save.character))
                {
                    var database = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/_Game/ScriptableObjects/Characters/CharacterDatabase.asset");
                    if (database && !database.Find(save.character.ToLowerInvariant()))
                        Warning("Existing run save contains legacy character '" + save.character + "'; Continue will migrate it to the saved starter character.");
                }
            }
            catch (Exception exception) { Error("Existing run save cannot be parsed: " + exception.Message); }
        }

        static void ValidateSerializedReferences(UnityEngine.Object target, string context)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.name == "m_Script" || property.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (IsOptionalPresentationReference(target, property.name)) continue;
                if (property.objectReferenceValue == null && property.objectReferenceInstanceIDValue == 0)
                    Error(context + " has null serialized reference: " + property.propertyPath);
                else if (property.objectReferenceValue == null)
                    Error(context + " has a broken serialized reference: " + property.propertyPath);
            }
        }

        static bool IsOptionalPresentationReference(UnityEngine.Object target, string propertyName)
        {
            if (target is CharacterData)
                return propertyName == "fullBodyCutIn" || propertyName == "comboEffectPrefab";
            if (target is ComboPresentationConfig)
                return propertyName == "comboIncrease" || propertyName == "comboMilestone" || propertyName == "criticalHit" ||
                    propertyName == "blockDestruction" || propertyName == "characterCutIn" || propertyName == "characterVoice" ||
                    propertyName == "highComboAmbience" || propertyName == "hyperCombo";
            return false;
        }

        static IEnumerable<GameObject> SceneObjects(Scene scene) =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Select(x => x.gameObject);

        static IEnumerable<T> Components<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

        static string HierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent) { transform = transform.parent; path = transform.name + "/" + path; }
            return path;
        }

        static void Error(string message) { if (!Errors.Contains(message)) Errors.Add(message); }
        static void Warning(string message) { if (!Warnings.Contains(message)) Warnings.Add(message); }
    }
}
