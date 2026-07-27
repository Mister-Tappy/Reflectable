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
            public readonly Sprite Preview;
            public Presentation(string name,string difficulty,string description,Color theme,Sprite preview=null){Name=name;Difficulty=difficulty;Description=description;Theme=theme;Preview=preview;}
        }
        const string SelectedStageKey = "Reflectable.SelectedStage";
        const string HighestUnlockedStageKey = "Reflectable.HighestUnlockedStage";
        static int MaxStage => ReflectableStageConfig.StageCount;

        public static int SelectedStage
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(SelectedStageKey, 1), 1, MaxStage);
            set { PlayerPrefs.SetInt(SelectedStageKey, Mathf.Clamp(value, 1, MaxStage)); PlayerPrefs.Save(); }
        }

        public static int HighestUnlockedStage
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(HighestUnlockedStageKey, 1), 1, MaxStage);
            set { PlayerPrefs.SetInt(HighestUnlockedStageKey, Mathf.Clamp(value, 1, MaxStage)); PlayerPrefs.Save(); }
        }

        public static bool IsUnlocked(int stage) => stage <= HighestUnlockedStage;
        public static int ClearRequirement(int stage) => ReflectableStageConfig.For(stage).BlockTarget;
        public static Presentation GetPresentation(int stage)
        {
            var data=ReflectableStageConfig.DataFor(stage);
            if(data)return new Presentation(data.stageName,data.difficultyLabel,data.description,data.stageSelectTint,data.stageSelectPreview);
            return new[]{
                new Presentation("MEADOW","NORMAL","A calm floating meadow where the journey begins.",new Color(.42f,.78f,.48f)),
                new Presentation("COAST","NORMAL","Sunlit sea, sand, and small island horizons.",new Color(.30f,.75f,.88f)),
                new Presentation("COUNTRYSIDE","HARD","Warm fields and quiet rural skies.",new Color(.68f,.76f,.34f)),
                new Presentation("MOUNTAIN","HARD","Cool cliffs beneath drifting cloud layers.",new Color(.45f,.48f,.68f)),
                new Presentation("FOREST","EXPERT","A dense emerald canopy alive with soft glow.",new Color(.14f,.48f,.35f)),
                new Presentation("CITY","EXPERT","A pastel skyline filled with tall silhouettes.",new Color(.55f,.48f,.86f)),
                new Presentation("SNOWFIELD","MASTER","Frozen hills beneath an icy blue sky.",new Color(.68f,.90f,1f)),
                new Presentation("DESERT","MASTER","Warm dunes and distant sandstone ruins.",new Color(.95f,.65f,.30f)),
                new Presentation("SKY ISLANDS","MASTER","Floating landforms across a bright open sky.",new Color(.62f,.90f,1f)),
                new Presentation("ECLIPSE","FINAL","A fractured world beneath a violet eclipse.",new Color(.22f,.08f,.30f))
            }[Mathf.Clamp(stage,1,10)-1];
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
