using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Reflectable
{
    public sealed class ArcadeHudDebugPanel : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameplayFeedbackManager feedback;
        static readonly int[] PreviewValues = { 0, 10, 28, 50, 100, 126, 200, 356, 500, 1000 };
        int previewCombo = 100;
        bool visible;

        void Awake() => feedback = FindFirstObjectByType<GameplayFeedbackManager>(FindObjectsInactive.Include);
        IEnumerator Start()
        {
#if UNITY_EDITOR
            string previewFlag = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/PreviewComboOrb.flag"));
            if (File.Exists(previewFlag))
            {
                File.Delete(previewFlag);
                yield return new WaitForSecondsRealtime(4.5f);
                if (feedback) feedback.PreviewCombo(500);
                else Debug.LogError("[Combo Preview] GameplayFeedbackManager was not found.");
            }
#else
            yield break;
#endif
        }
        void Update()
        {
            if (Keyboard.current?.f8Key.wasPressedThisFrame == true) visible = !visible;
            if (Keyboard.current?.f9Key.wasPressedThisFrame == true)
            {
                visible = true;
                previewCombo = 500;
                feedback?.PreviewCombo(previewCombo);
            }
            if (Keyboard.current?.f10Key.wasPressedThisFrame == true) feedback?.ResetArcadeEffects();
        }

        void OnGUI()
        {
            if (!visible) return;
            GUILayout.BeginArea(new Rect(16f, 70f, 250f, 510f), GUI.skin.box);
            GUILayout.Label("COMBO ORB PREVIEW  [F8]");
            GUILayout.Label("F9: Combo 500   F10: Reset");
            GUILayout.Label("Combo: " + previewCombo);
            previewCombo = Mathf.RoundToInt(GUILayout.HorizontalSlider(previewCombo, 0f, 1000f));
            if (GUILayout.Button("Set Combo")) feedback?.PreviewCombo(previewCombo);
            foreach (int value in PreviewValues)
                if (GUILayout.Button("Preview " + value)) feedback?.PreviewCombo(value);
            if (GUILayout.Button("Character Cut-In")) feedback?.PreviewCharacterCutIn(Mathf.Max(200, previewCombo));
            if (GUILayout.Button("Critical Hit")) feedback?.PreviewCriticalHit();
            if (GUILayout.Button("Block Explosion")) feedback?.PreviewBlockExplosion();
            GUILayout.Label("Effect quality");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Low")) feedback?.SetPreviewQuality(ArcadeEffectQuality.Low);
            if (GUILayout.Button("Medium")) feedback?.SetPreviewQuality(ArcadeEffectQuality.Medium);
            if (GUILayout.Button("High")) feedback?.SetPreviewQuality(ArcadeEffectQuality.High);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Reset Effects")) feedback?.ResetArcadeEffects();
            GUILayout.EndArea();
        }
#endif
    }
}
