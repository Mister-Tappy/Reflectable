using UnityEngine;

namespace Reflectable
{
    /// <summary>Small persistent selection store; no DontDestroyOnLoad object is required.</summary>
    public static class ReflectableStageSession
    {
        public readonly struct Presentation
        {
            public readonly string Name, Difficulty, Description;
            public readonly Color Theme;
            public Presentation(string name,string difficulty,string description,Color theme){Name=name;Difficulty=difficulty;Description=description;Theme=theme;}
        }
        const string SelectedStageKey = "Reflectable.SelectedStage";
        const string HighestUnlockedStageKey = "Reflectable.HighestUnlockedStage";

        public static int SelectedStage
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(SelectedStageKey, 1), 1, 5);
            set { PlayerPrefs.SetInt(SelectedStageKey, Mathf.Clamp(value, 1, 5)); PlayerPrefs.Save(); }
        }

        public static int HighestUnlockedStage
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(HighestUnlockedStageKey, 1), 1, 5);
            set { PlayerPrefs.SetInt(HighestUnlockedStageKey, Mathf.Clamp(value, 1, 5)); PlayerPrefs.Save(); }
        }

        public static bool IsUnlocked(int stage) => stage <= HighestUnlockedStage;
        public static int ClearRequirement(int stage) => new[] { 150, 250, 400, 600, 850 }[Mathf.Clamp(stage, 1, 5) - 1];
        public static Presentation GetPresentation(int stage)
        {
            return new[]{
                new Presentation("MEADOW","NORMAL","A calm floating meadow where the journey begins.",new Color(.42f,.78f,.48f)),
                new Presentation("TWILIGHT","HARD","A fading island beneath a permanent twilight.",new Color(.42f,.35f,.76f)),
                new Presentation("EMBER","HARD","A burning fragment above a restless sea of sparks.",new Color(.93f,.40f,.20f)),
                new Presentation("CRYSTAL","EXPERT","A cold island shaped by luminous crystal growth.",new Color(.25f,.78f,.91f)),
                new Presentation("ECLIPSE","MASTER","A dangerous island swallowed by a violet eclipse.",new Color(.20f,.12f,.32f))
            }[Mathf.Clamp(stage,1,5)-1];
        }

        public static int ResolveSelectedStage()
        {
            if (PlayerPrefs.HasKey(SelectedStageKey)) return SelectedStage;
            Debug.LogWarning("Reflectable: no stage selection found; falling back to Stage 1.");
            SelectedStage = 1;
            return 1;
        }
    }
}
