using System;
using System.IO;
using Reflectable;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Reflectable.Editor
{
    [InitializeOnLoad]
    public static class ArcadeHudInstaller
    {
        const int HudVersion = 11;
        const string ConfigFolder = "Assets/_Game/ScriptableObjects/UI";
        const string ConfigPath = ConfigFolder + "/ComboPresentationConfig.asset";
        const string PrefabFolder = "Assets/_Game/Prefabs/UI/ArcadeHUD";
        const string UpgradeCardPath = PrefabFolder + "/UpgradeCard.prefab";
        const string HudPrefabPath = PrefabFolder + "/ArcadeGameplayHUD.prefab";
        const string EffectsPrefabFolder = "Assets/_Game/Prefabs/Effects";
        const string ComboEffectsPrefabPath = EffectsPrefabFolder + "/ComboBattlefieldEffects.prefab";
        const string ProjectilePrefabPath = "Assets/Prefabs/Projectile.prefab";
        const string GameScenePath = "Assets/Scenes/Game.unity";
        static Sprite panelSprite;

        static ArcadeHudInstaller()
        {
            EditorApplication.delayCall += ApplyWhenNeeded;
        }

        [MenuItem("Tools/REFLECTABLE/Apply Arcade Gameplay HUD")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureFolders();
            panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            ComboPresentationConfig config = BuildConfig();
            BuildUpgradeCardPrefab();
            BuildHudPrefab(config);
            BuildComboEffectsPrefab();
            ConfigureProjectilePrefab();
            ApplyToGameScene();
            UpdateCharacterPresentationData();
            ValidateTargetResolutions();
            config.installedHudVersion = HudVersion;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            ProjectValidationTool.ValidateProject();
            AssetDatabase.Refresh();
            Debug.Log("[Arcade HUD] Persistent HUD, prefabs, combo tiers, cut-ins, and debug preview installed.");
        }

        static void ApplyWhenNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var config = AssetDatabase.LoadAssetAtPath<ComboPresentationConfig>(ConfigPath);
            if (config && config.installedHudVersion >= HudVersion) return;
            try { Apply(); }
            catch (Exception exception) { Debug.LogError("[Arcade HUD] Installation failed: " + exception); }
        }

        static void EnsureFolders()
        {
            EnsureFolder("Assets/_Game/ScriptableObjects", "UI");
            EnsureFolder("Assets/_Game/Prefabs/UI", "ArcadeHUD");
            EnsureFolder("Assets/_Game/Prefabs", "Effects");
        }

        static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }

        static ComboPresentationConfig BuildConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ComboPresentationConfig>(ConfigPath);
            if (!config)
            {
                config = ScriptableObject.CreateInstance<ComboPresentationConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            config.quality = ArcadeEffectQuality.High;
            config.punchInDuration = .065f;
            config.punchOutDuration = .15f;
            config.comboBreakFade = .48f;
            config.announcementDuration = .7f;
            config.orbBaseSize=.82f;config.orbHoverAmount=.09f;config.orbHorizontalDrift=.06f;config.orbHoverSpeed=1.7f;
            config.orbRotationSpeed=13f;config.orbPulseSpeed=3.2f;config.orbBreathAmount=.045f;
            config.orbFormationDuration=.32f;
            config.energyTravelDuration=.42f;config.energyParticlesPerBlock=2;
            config.maximumBloom = 1.35f;
            config.maximumSaturation = 12f;
            config.tiers = new[]
            {
                Tier(0, "", Color.white, new Color(.72f,.82f,1f), 1.08f, 1.5f, .18f, 0f, 0f, 0f, 0f),
                Tier(20, "NICE", new Color(.22f,.68f,1f), new Color(.72f,.9f,1f), 1.12f, 2.5f, .55f, .32f, 0f, .008f, 0f),
                Tier(50, "GREAT", new Color(.22f,1f,.48f), new Color(1f,.72f,.12f), 1.16f, 3f, .8f, .72f, 0f, .015f, 0f),
                Tier(100, "AMAZING", new Color(1f,.48f,.06f), new Color(1f,.75f,.18f), 1.20f, 3.5f, 1.1f, 1f, .18f, .025f, .03f),
                Tier(200, "UNSTOPPABLE", new Color(1f,.10f,.14f), new Color(1f,.34f,.20f), 1.23f, 4f, 1.35f, 1.15f, .75f, .04f, 0f),
                Tier(300, "DOMINATING", new Color(.78f,.12f,1f), new Color(1f,.32f,.08f), 1.25f, 4.5f, 1.65f, 1.5f, 1f, .06f, .05f),
                Tier(500, "LEGENDARY", new Color(1f,.58f,.06f), new Color(.22f,.92f,1f), 1.28f, 5f, 2f, 2f, 1.35f, .09f, .07f),
                Tier(1000, "HYPER COMBO", Color.white, new Color(.2f,1f,.95f), 1.30f, 5f, 2.4f, 2.2f, 1.8f, .12f, .10f)
            };
            config.milestones = new[]
            {
                Milestone(20,"NICE",false,.55f,0f,.008f),
                Milestone(50,"GREAT",false,.6f,0f,.015f),
                Milestone(100,"AMAZING",false,.7f,.03f,.025f),
                Milestone(200,"UNSTOPPABLE",true,1.05f,0f,.04f),
                Milestone(300,"DOMINATING",true,1.15f,.05f,.06f),
                Milestone(500,"LEGENDARY",true,1.3f,.07f,.09f),
                Milestone(1000,"HYPER COMBO",true,1.5f,.10f,.12f)
            };
            EditorUtility.SetDirty(config);
            return config;
        }

        static ComboTierSettings Tier(int threshold, string word, Color primary, Color secondary, float scale, float rotation, float glow, float fire, float lightning, float shake, float stop) =>
            new ComboTierSettings { minimumCombo=threshold, hypeWord=word, primaryColor=primary, secondaryColor=secondary, punchScale=scale, rotation=rotation, glow=glow, fire=fire, lightning=lightning, particleCount=threshold>=1000?28:threshold>=500?20:threshold>=300?16:threshold>=200?12:threshold>=100?9:threshold>=50?6:threshold>=20?4:2, cameraShake=shake, hitStop=stop };
        static ComboMilestoneSettings Milestone(int combo, string word, bool cutIn, float duration, float stop, float shake) =>
            new ComboMilestoneSettings { combo=combo, announcement=word, characterCutIn=cutIn, cutInDuration=duration, hitStop=stop, cameraShake=shake };

        static void BuildUpgradeCardPrefab()
        {
            var root = Panel("UpgradeCard", null, new Vector2(.5f,.5f), Vector2.zero, new Vector2(150f,92f), new Color(.15f,.11f,.25f,.91f));
            var button = root.AddComponent<Button>();
            root.AddComponent<ButtonJuice>();
            var card = root.AddComponent<HudUpgradeCard>();
            var outline = root.GetComponent<Image>();
            button.targetGraphic = outline;
            var inner = Panel("Background", root.transform, StretchAnchor(), Vector2.zero, Vector2.zero, new Color(.22f,.17f,.34f,.96f));
            Inset(inner.GetComponent<RectTransform>(), 2f);
            inner.GetComponent<Image>().raycastTarget = false;
            var icon = Panel("Icon", root.transform, new Vector2(0f,.5f), new Vector2(25f,9f), new Vector2(34f,34f), new Color(.72f,.58f,1f,.95f)).GetComponent<Image>();
            icon.type = Image.Type.Sliced;
            Text("IconGlyph", icon.transform, "+", 19f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(32f,32f));
            var title = Text("Name", root.transform, "POWER", 16f, TextAlignmentOptions.Left, new Vector2(74f,25f), new Vector2(92f,24f));
            var level = Text("Level", root.transform, "Lv.0", 13f, TextAlignmentOptions.Left, new Vector2(74f,4f), new Vector2(92f,20f));
            var effect = Text("Effect", root.transform, "+0%", 12f, TextAlignmentOptions.Left, new Vector2(74f,-16f), new Vector2(92f,18f));
            var cost = Text("Cost", root.transform, "1 SP", 13f, TextAlignmentOptions.Right, new Vector2(108f,-34f), new Vector2(56f,18f));
            card.Configure(icon, outline, title, level, effect, cost);
            PrefabUtility.SaveAsPrefabAsset(root, UpgradeCardPath);
            Object.DestroyImmediate(root);
        }

        static void BuildHudPrefab(ComboPresentationConfig config)
        {
            var root = Ui("ArcadeHUD", null, StretchAnchor(), Vector2.zero, Vector2.zero);
            root.AddComponent<ArcadeHudDebugPanel>();
            var layout = root.AddComponent<HudLayoutController>();
            var safe = Ui("SafeArea", root.transform, StretchAnchor(), Vector2.zero, Vector2.zero);
            layout.Configure(safe.GetComponent<RectTransform>());
            var edgeGlow = Panel("HighComboEdgeGlow", safe.transform, StretchAnchor(), Vector2.zero, Vector2.zero, Color.clear).GetComponent<Image>();
            edgeGlow.raycastTarget = false;

            var top = Ui("TopStatusBar", safe.transform, new Vector2(.5f,1f), new Vector2(0f,-14f), new Vector2(1884f,104f));
            top.GetComponent<RectTransform>().pivot = new Vector2(.5f,1f);
            var left = Panel("HealthPanel", top.transform, new Vector2(0f,.5f), new Vector2(190f,0f), new Vector2(350f,82f), new Color(.15f,.10f,.24f,.78f));
            Text("HeartIcon", left.transform, "♥", 31f, TextAlignmentOptions.Center, new Vector2(35f,4f), new Vector2(52f,52f)).color = new Color(1f,.35f,.55f);
            var hpBar = Panel("HPBar", left.transform, new Vector2(.5f,.5f), new Vector2(38f,-13f), new Vector2(235f,16f), new Color(.08f,.05f,.14f,.9f));
            var hpFill = Panel("Fill", hpBar.transform, StretchAnchor(), Vector2.zero, Vector2.zero, new Color(1f,.32f,.52f,.95f)).GetComponent<Image>();
            hpFill.type = Image.Type.Filled; hpFill.fillMethod = Image.FillMethod.Horizontal; hpFill.fillOrigin = 0; hpFill.fillAmount = 1f;
            var hpText = Text("HPValue", left.transform, "HP 100 / 100", 18f, TextAlignmentOptions.Left, new Vector2(48f,18f), new Vector2(245f,27f));

            var center = Panel("StagePanel", top.transform, new Vector2(.5f,.5f), Vector2.zero, new Vector2(520f,92f), new Color(.14f,.10f,.25f,.80f));
            var stageText = Text("StageName", center.transform, "STAGE 1  ·  MEADOW", 19f, TextAlignmentOptions.Center, new Vector2(0f,26f), new Vector2(470f,27f));
            var progressBar = Panel("ProgressBar", center.transform, new Vector2(.5f,.5f), new Vector2(0f,-4f), new Vector2(340f,13f), new Color(.07f,.05f,.13f,.9f));
            var progressFill = Panel("Fill", progressBar.transform, StretchAnchor(), Vector2.zero, Vector2.zero, new Color(.56f,.82f,1f,.95f)).GetComponent<Image>();
            progressFill.type = Image.Type.Filled; progressFill.fillMethod = Image.FillMethod.Horizontal; progressFill.fillOrigin = 0; progressFill.fillAmount = 0f;
            var sparkle = Panel("Sparkle", progressBar.transform, new Vector2(0f,.5f), Vector2.zero, new Vector2(22f,22f), Color.white).GetComponent<Image>();
            sparkle.gameObject.SetActive(false);
            var progressText = Text("Progress", center.transform, "0 / 200 BLOCKS", 14f, TextAlignmentOptions.Left, new Vector2(-86f,-29f), new Vector2(250f,22f));
            var turnText = Text("Turn", center.transform, "TURN 1", 14f, TextAlignmentOptions.Right, new Vector2(171f,-29f), new Vector2(130f,22f));

            var right = Panel("ScorePanel", top.transform, new Vector2(1f,.5f), new Vector2(-213f,0f), new Vector2(396f,82f), new Color(.15f,.10f,.24f,.78f));
            var scoreText = Text("Score", right.transform, "SCORE  0", 18f, TextAlignmentOptions.Left, new Vector2(30f,15f), new Vector2(225f,28f));
            var gemsText = Text("Gems", right.transform, "GEM  0", 17f, TextAlignmentOptions.Left, new Vector2(30f,-17f), new Vector2(175f,24f));
            var pause = CompactButton("PauseButton", right.transform, "II", new Vector2(1f,.5f), new Vector2(-42f,0f), new Vector2(58f,54f));

            var comboLayer = Ui("ComboPresentationLayer", safe.transform, StretchAnchor(), Vector2.zero, Vector2.zero);
            var cosmic = Panel("CosmicOverlay", comboLayer.transform, StretchAnchor(), Vector2.zero, Vector2.zero, Color.clear).GetComponent<Image>(); cosmic.raycastTarget=false;
            var comboRoot = Ui("ComboRoot", comboLayer.transform, new Vector2(.5f,1f), new Vector2(0f,-145f), new Vector2(680f,205f));
            comboRoot.GetComponent<RectTransform>().pivot = new Vector2(.5f,1f);
            var splash = Panel("ImpactSplash", comboRoot.transform, new Vector2(.5f,.5f), new Vector2(0f,10f), new Vector2(420f,145f), Color.clear).GetComponent<Image>(); splash.raycastTarget=false;
            var glow = Panel("Glow", comboRoot.transform, new Vector2(.5f,.5f), new Vector2(0f,10f), new Vector2(360f,150f), Color.clear).GetComponent<Image>(); glow.raycastTarget=false;
            var flame = Panel("FlameAura", comboRoot.transform, new Vector2(.5f,.5f), new Vector2(0f,20f), new Vector2(470f,180f), Color.clear).GetComponent<Image>(); flame.raycastTarget=false;
            var lightning = Panel("Lightning", comboRoot.transform, new Vector2(.5f,.5f), new Vector2(0f,8f), new Vector2(520f,150f), Color.clear).GetComponent<Image>(); lightning.raycastTarget=false;
            var comboNumber = Text("ComboNumber", comboRoot.transform, "", 82f, TextAlignmentOptions.Center, new Vector2(0f,42f), new Vector2(620f,102f));
            comboNumber.fontStyle = FontStyles.Bold; comboNumber.outlineWidth = .22f; comboNumber.outlineColor = new Color32(27,13,49,240);
            var caption = Text("ComboCaption", comboRoot.transform, "", 20f, TextAlignmentOptions.Center, new Vector2(0f,-19f), new Vector2(280f,32f));
            caption.characterSpacing = 12f;
            var hype = Text("HypeWord", comboRoot.transform, "", 24f, TextAlignmentOptions.Center, new Vector2(0f,-55f), new Vector2(520f,38f));
            var announcements = new TMP_Text[3];
            for (int i=0;i<announcements.Length;i++)
            {
                announcements[i] = Text("Announcement_"+i, comboLayer.transform, "", 48f, TextAlignmentOptions.Center, new Vector2(0f,-310f), new Vector2(900f,80f));
                announcements[i].fontStyle=FontStyles.Bold;announcements[i].outlineWidth=.18f;announcements[i].gameObject.SetActive(false);
            }

            var bottom = Ui("BottomActionBar", safe.transform, new Vector2(.5f,0f), new Vector2(0f,14f), new Vector2(1884f,118f));
            bottom.GetComponent<RectTransform>().pivot = new Vector2(.5f,0f);
            var characterPanel = Panel("CharacterIdentity", bottom.transform, new Vector2(0f,.5f), new Vector2(145f,0f), new Vector2(260f,104f), new Color(.14f,.10f,.24f,.86f));
            var portraitGlow = Panel("PortraitGlow", characterPanel.transform, new Vector2(0f,.5f), new Vector2(47f,0f), new Vector2(86f,86f), new Color(.6f,.4f,1f,.28f)).GetComponent<Image>();
            var portrait = Panel("Portrait", characterPanel.transform, new Vector2(0f,.5f), new Vector2(47f,0f), new Vector2(76f,76f), Color.white).GetComponent<Image>(); portrait.preserveAspect=true;
            var characterName = Text("Name", characterPanel.transform, "MARINA", 17f, TextAlignmentOptions.Left, new Vector2(164f,24f), new Vector2(140f,24f));
            var characterLevel = Text("Level", characterPanel.transform, "Lv.1", 14f, TextAlignmentOptions.Left, new Vector2(164f,1f), new Vector2(140f,21f));
            var spText = Text("SkillPoints", characterPanel.transform, "SP  0", 14f, TextAlignmentOptions.Left, new Vector2(164f,-24f), new Vector2(140f,21f));

            var upgradeCluster = Ui("UpgradeCluster", bottom.transform, new Vector2(.5f,.5f), Vector2.zero, new Vector2(500f,100f));
            var power = UpgradeCard("PowerCard", upgradeCluster.transform, -168f);
            var ricochet = UpgradeCard("RicochetCard", upgradeCluster.transform, 0f);
            var extraBall = UpgradeCard("ExtraBallCard", upgradeCluster.transform, 168f);
            var rightActions = Ui("RightActions", bottom.transform, new Vector2(1f,.5f), new Vector2(-140f,0f), new Vector2(270f,104f));
            var collection = CompactButton("CollectionButton", rightActions.transform, "CHARACTER", new Vector2(.5f,.5f), new Vector2(-67f,0f), new Vector2(122f,82f));
            var skip = CompactButton("SkipTurnButton", rightActions.transform, ">>\nSKIP", new Vector2(.5f,.5f), new Vector2(67f,0f), new Vector2(122f,82f));
            var badge = Panel("NewBadge", collection.transform, new Vector2(1f,1f), new Vector2(-5f,-5f), new Vector2(22f,22f), new Color(1f,.25f,.45f,1f));
            Text("Label", badge.transform, "!", 13f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(20f,20f));
            badge.SetActive(false);

            var cutInLayer = Ui("CharacterCutInLayer", safe.transform, StretchAnchor(), Vector2.zero, Vector2.zero);
            var cutInPanel = Ui("CutInPanel", cutInLayer.transform, new Vector2(0f,.5f), new Vector2(-720f,0f), new Vector2(680f,650f));
            var cutInGroup = cutInPanel.AddComponent<CanvasGroup>(); cutInGroup.alpha=0f;cutInGroup.blocksRaycasts=false;
            var strip = Panel("EnergyStrip", cutInPanel.transform, new Vector2(.5f,.5f), Vector2.zero, new Vector2(680f,360f), new Color(.1f,.04f,.2f,.75f)).GetComponent<Image>();
            var aura = Panel("Aura", cutInPanel.transform, new Vector2(.5f,.5f), new Vector2(-30f,20f), new Vector2(480f,480f), new Color(.6f,.3f,1f,.35f)).GetComponent<Image>();
            var afterimages = new Image[3];
            for(int i=0;i<afterimages.Length;i++){afterimages[i]=Panel("Afterimage_"+i,cutInPanel.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(500f,590f),Color.clear).GetComponent<Image>();afterimages[i].enabled=false;}
            var cutInImage = Panel("Character", cutInPanel.transform, new Vector2(.5f,.5f), new Vector2(-55f,0f), new Vector2(500f,590f), Color.white).GetComponent<Image>();cutInImage.enabled=false;cutInImage.preserveAspect=true;
            var cutInName = Text("CharacterName", cutInPanel.transform, "MARINA", 38f, TextAlignmentOptions.Left, new Vector2(420f,-185f), new Vector2(420f,50f));
            var cutInTitle = Text("CharacterTitle", cutInPanel.transform, "TIDE WHISPER", 19f, TextAlignmentOptions.Left, new Vector2(420f,-225f), new Vector2(420f,32f));
            var cutInAbility = Text("AbilityName", cutInPanel.transform, "TIDE OVERDRIVE", 25f, TextAlignmentOptions.Left, new Vector2(420f,-263f), new Vector2(420f,38f));
            var cutIn = cutInLayer.AddComponent<CharacterCutInController>();
            Set(cutIn,"panel",cutInPanel.GetComponent<RectTransform>());Set(cutIn,"canvasGroup",cutInGroup);Set(cutIn,"backgroundStrip",strip);Set(cutIn,"aura",aura);Set(cutIn,"characterImage",cutInImage);SetArray(cutIn,"afterimages",afterimages);Set(cutIn,"nameLabel",cutInName);Set(cutIn,"titleLabel",cutInTitle);Set(cutIn,"abilityLabel",cutInAbility);

            var hud = root.AddComponent<GameplayHudController>();
            Set(hud,"hpFill",hpFill);Set(hud,"hpText",hpText);Set(hud,"stageText",stageText);Set(hud,"progressText",progressText);Set(hud,"turnText",turnText);Set(hud,"stageProgressFill",progressFill);Set(hud,"stageProgressSparkle",sparkle);Set(hud,"scoreText",scoreText);Set(hud,"gemsText",gemsText);Set(hud,"portrait",portrait);Set(hud,"portraitGlow",portraitGlow);Set(hud,"characterName",characterName);Set(hud,"characterLevel",characterLevel);Set(hud,"skillPointsText",spText);Set(hud,"powerCard",power);Set(hud,"ricochetCard",ricochet);Set(hud,"extraBallCard",extraBall);Set(hud,"collectionButton",collection);Set(hud,"skipButton",skip);Set(hud,"pauseButton",pause);Set(hud,"collectionBadge",badge);Set(hud,"highComboEdgeGlow",edgeGlow);
            var combo = root.AddComponent<ComboPresentationController>();
            Set(combo,"config",config);Set(combo,"comboRoot",comboRoot.GetComponent<RectTransform>());Set(combo,"comboNumber",comboNumber);Set(combo,"comboCaption",caption);Set(combo,"hypeLabel",hype);Set(combo,"glow",glow);Set(combo,"flame",flame);Set(combo,"impactSplash",splash);Set(combo,"lightning",lightning);Set(combo,"cosmicOverlay",cosmic);SetArray(combo,"announcementPool",announcements);Set(combo,"cutIn",cutIn);Set(combo,"hud",hud);
            SetBool(combo,"worldOrbPrimary",true);
            comboRoot.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            Object.DestroyImmediate(root);
        }

        static void BuildComboEffectsPrefab()
        {
            var root=new GameObject("ComboBattlefieldEffects");
            var orbRoot=new GameObject("ComboOrb");orbRoot.transform.SetParent(root.transform,false);
            var orb=orbRoot.AddComponent<ComboOrbController>();
            var visual=new GameObject("Visual").transform;visual.SetParent(orbRoot.transform,false);
            SpriteRenderer glow=WorldLayer("Glow",visual,540);
            SpriteRenderer corona=WorldLayer("Corona",visual,541);
            SpriteRenderer flame=WorldLayer("Flame",visual,542);
            SpriteRenderer ring=WorldLayer("Ring",visual,543);
            SpriteRenderer lightning=WorldLayer("Lightning",visual,544);
            SpriteRenderer core=WorldLayer("Core",visual,545);
            var display=new GameObject("ComboDisplay").transform;display.SetParent(orbRoot.transform,false);display.localPosition=new Vector3(0f,1.5f,-.2f);
            TextMesh numberGlow=WorldText("NumberGlow",display,112,578,new Vector3(0f,.26f,.03f));
            numberGlow.characterSize=.081f;numberGlow.transform.localScale=Vector3.one*1.08f;numberGlow.color=new Color(1f,1f,1f,.16f);
            TextMesh number=WorldText("ComboNumber",display,112,580,new Vector3(0f,.26f,0f));
            number.characterSize=.075f;
            TextMesh caption=WorldText("ComboLabel",display,70,579,new Vector3(0f,-.34f,0f));
            caption.characterSize=.046f;caption.text="COMBO";
            TextMesh[] labels=new TextMesh[3];
            for(int i=0;i<labels.Length;i++){labels[i]=WorldText("Milestone_"+i,display,82,582+i,new Vector3(0f,1.08f,0f));labels[i].characterSize=.04f;labels[i].gameObject.SetActive(false);}
            Font arcadeFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/_Game/Font/Matcha Mint.ttf");
            AssignWorldFont(numberGlow,arcadeFont);AssignWorldFont(number,arcadeFont);AssignWorldFont(caption,arcadeFont);
            for(int i=0;i<labels.Length;i++)AssignWorldFont(labels[i],arcadeFont);
            Set(orb,"visualRoot",visual);Set(orb,"glow",glow);Set(orb,"core",core);Set(orb,"flame",flame);Set(orb,"ring",ring);Set(orb,"lightning",lightning);Set(orb,"corona",corona);Set(orb,"comboDisplayRoot",display);Set(orb,"comboNumber",number);Set(orb,"comboNumberGlow",numberGlow);Set(orb,"comboCaption",caption);SetArray(orb,"milestoneLabels",labels);Set(orb,"displayFont",arcadeFont);
            orbRoot.SetActive(false);

            var worldRoot=new GameObject("WorldReaction");worldRoot.transform.SetParent(root.transform,false);
            worldRoot.transform.localPosition=new Vector3(0f,-4.25f,.2f);
            var world=worldRoot.AddComponent<ComboWorldReactionController>();
            SpriteRenderer ground=WorldLayer("GroundGlow",worldRoot.transform,410);ground.enabled=false;
            SpriteRenderer energy=WorldLayer("UltimateEnergyCircle",worldRoot.transform,411);energy.enabled=false;
            Set(world,"groundGlow",ground);Set(world,"energyCircle",energy);

            PrefabUtility.SaveAsPrefabAsset(root,ComboEffectsPrefabPath);
            Object.DestroyImmediate(root);
        }

        static void ConfigureProjectilePrefab()
        {
            var root=PrefabUtility.LoadPrefabContents(ProjectilePrefabPath);
            try
            {
                var projectile=root.GetComponent<ReflectableProjectile>();
                if(!projectile)throw new InvalidOperationException("Projectile prefab is missing ReflectableProjectile.");
                Transform old=root.transform.Find("ComboVisual");if(old)Object.DestroyImmediate(old.gameObject);
                var visual=new GameObject("ComboVisual").transform;visual.SetParent(root.transform,false);
                SpriteRenderer glow=WorldLayer("Glow",visual,531);
                SpriteRenderer ring=WorldLayer("EnergyRing",visual,532);
                SpriteRenderer lightning=WorldLayer("Lightning",visual,533);
                Set(projectile,"comboGlow",glow);Set(projectile,"comboRing",ring);Set(projectile,"comboLightning",lightning);
                PrefabUtility.SaveAsPrefabAsset(root,ProjectilePrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }

        static void ApplyToGameScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            var controller = Object.FindFirstObjectByType<ReflectableGameController>(FindObjectsInactive.Include);
            var feedback = Object.FindFirstObjectByType<GameplayFeedbackManager>(FindObjectsInactive.Include);
            if (!canvas || !controller || !feedback) throw new InvalidOperationException("Production Game scene is missing Canvas, ReflectableGameController, or GameplayFeedbackManager.");
            var scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920f,1080f);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=.5f;
            Transform previous = canvas.transform.Find("ArcadeHUD");
            if (previous) Object.DestroyImmediate(previous.gameObject);
            DisableDirect(canvas.transform,"HUD");DisableDirect(canvas.transform,"TopHUD");DisableDirect(canvas.transform,"BottomHUD");DisableDirect(canvas.transform,"ComboText");DisableDirect(canvas.transform,"CharacterText");DisableDirect(canvas.transform,"PauseButton");DisableDirect(canvas.transform,"SkipTurnButton");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            instance.name = "ArcadeHUD";
            instance.transform.SetAsLastSibling();
            var hud = instance.GetComponent<GameplayHudController>();
            Transform effectsLayer=EnsureChild(feedback.transform,"EffectsLayer");
            Transform damageNumberLayer=EnsureChild(feedback.transform,"DamageNumberLayer");
            Transform previousEffects=effectsLayer.Find("ComboBattlefieldEffects");if(previousEffects)Object.DestroyImmediate(previousEffects.gameObject);
            var effectsPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(ComboEffectsPrefabPath);
            var battlefieldEffects=(GameObject)PrefabUtility.InstantiatePrefab(effectsPrefab,effectsLayer);
            battlefieldEffects.name="ComboBattlefieldEffects";
            var orb=battlefieldEffects.GetComponentInChildren<ComboOrbController>(true);
            var world=battlefieldEffects.GetComponentInChildren<ComboWorldReactionController>(true);
            Set(feedback,"effectsLayer",effectsLayer);Set(feedback,"damageNumberLayer",damageNumberLayer);Set(feedback,"comboOrb",orb);Set(feedback,"worldReaction",world);
            Set(controller,"gameplayHud",hud);Set(controller,"powerButton",hud.PowerButton);Set(controller,"ricochetButton",hud.RicochetButton);Set(controller,"extraBallButton",hud.ExtraBallButton);Set(controller,"characterButton",hud.CollectionButton);Set(controller,"skipTurnButton",hud.SkipButton);Set(controller,"pauseButton",hud.PauseButton);
            Hook(hud.PowerButton,controller,"Power");Hook(hud.RicochetButton,controller,"Ricochet");Hook(hud.ExtraBallButton,controller,"Extra Ball");Hook(hud.CollectionButton,controller.OpenCharacterPanel);Hook(hud.SkipButton,controller.SkipTurn);Hook(hud.PauseButton,controller.TogglePause);
            EditorUtility.SetDirty(controller);EditorUtility.SetDirty(feedback);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void UpdateCharacterPresentationData()
        {
            foreach(string guid in AssetDatabase.FindAssets("t:CharacterData",new[]{"Assets/_Game/ScriptableObjects/Characters"}))
            {
                var data=AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(guid));
                if(!data)continue;
                if(!data.fullBodyCutIn)data.fullBodyCutIn=data.frontSprite?data.frontSprite:data.portrait;
                if(string.IsNullOrWhiteSpace(data.cutInAbilityName))data.cutInAbilityName=data.title;
                if(data.cutInScale<=0f)data.cutInScale=1f;
                if(data.comboAuraColor.a<=0f)data.comboAuraColor=data.themeColor;
                EditorUtility.SetDirty(data);
            }
        }

        static void ValidateTargetResolutions()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            var layout=prefab?prefab.GetComponent<HudLayoutController>():null;
            if(!layout)throw new InvalidOperationException("HUD prefab has no layout controller.");
            var targets=new[]{new Vector2Int(1920,1080),new Vector2Int(1600,900),new Vector2Int(1366,768),new Vector2Int(1170,646)};
            foreach(var target in targets)
                if(!layout.FitsResolution(target.x,target.y))
                    throw new InvalidOperationException("HUD exceeds 13% bottom-height budget at "+target.x+"x"+target.y+".");
            Debug.Log("[Arcade HUD] Resolution layout checks passed: 1920x1080, 1600x900, 1366x768, 1170x646.");
        }

        static HudUpgradeCard UpgradeCard(string name, Transform parent, float x)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(UpgradeCardPath);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
            instance.name=name;
            var rect=instance.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(x,0f);rect.sizeDelta=new Vector2(150f,92f);
            return instance.GetComponent<HudUpgradeCard>();
        }

        static Button CompactButton(string name,Transform parent,string label,Vector2 anchor,Vector2 position,Vector2 size)
        {
            var root=Panel(name,parent,anchor,position,size,new Color(.25f,.18f,.39f,.92f));
            var button=root.AddComponent<Button>();root.AddComponent<ButtonJuice>();button.targetGraphic=root.GetComponent<Image>();
            var text=Text("Label",root.transform,label,14f,TextAlignmentOptions.Center,Vector2.zero,size-Vector2.one*8f);text.fontStyle=FontStyles.Bold;text.raycastTarget=false;
            return button;
        }

        static GameObject Ui(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));go.layer=LayerMask.NameToLayer("UI");if(parent)go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=anchor;rect.anchoredPosition=position;rect.sizeDelta=size;
            if(anchor==StretchAnchor()){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
            return go;
        }

        static GameObject Panel(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size,Color color)
        {
            var go=Ui(name,parent,anchor,position,size);var image=go.AddComponent<Image>();image.sprite=panelSprite;image.type=Image.Type.Sliced;image.color=color;return go;
        }

        static SpriteRenderer WorldLayer(string name,Transform parent,int sortingOrder)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sortingOrder=sortingOrder;renderer.enabled=false;return renderer;
        }

        static TextMesh WorldText(string name,Transform parent,int fontSize,int sortingOrder,Vector3 position)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;
            var text=go.AddComponent<TextMesh>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=fontSize;text.characterSize=.022f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontStyle=FontStyle.Bold;text.color=Color.white;
            text.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;text.GetComponent<MeshRenderer>().sortingOrder=sortingOrder;return text;
        }

        static void AssignWorldFont(TextMesh text,Font font)
        {
            if(!text||!font)return;
            text.font=font;
            text.fontStyle=FontStyle.Bold;
            text.GetComponent<MeshRenderer>().sharedMaterial=font.material;
        }

        static TextMeshProUGUI Text(string name,Transform parent,string value,float size,TextAlignmentOptions alignment,Vector2 position,Vector2 dimensions)
        {
            var go=Ui(name,parent,new Vector2(.5f,.5f),position,dimensions);var text=go.AddComponent<TextMeshProUGUI>();text.text=value;text.font=TMP_Settings.defaultFontAsset;text.fontSize=size;text.alignment=alignment;text.color=Color.white;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Overflow;return text;
        }

        static Vector2 StretchAnchor()=>new Vector2(-1f,-1f);
        static void Inset(RectTransform rect,float amount){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.one*amount;rect.offsetMax=-Vector2.one*amount;}
        static void DisableDirect(Transform parent,string name){var item=parent.Find(name);if(item)item.gameObject.SetActive(false);}
        static Transform EnsureChild(Transform parent,string name){var child=parent.Find(name);if(child)return child;var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        static void Set(Object target,string property,Object value){var serialized=new SerializedObject(target);var field=serialized.FindProperty(property);if(field==null)throw new MissingFieldException(target.GetType().Name,property);field.objectReferenceValue=value;serialized.ApplyModifiedPropertiesWithoutUndo();}
        static void SetBool(Object target,string property,bool value){var serialized=new SerializedObject(target);var field=serialized.FindProperty(property);if(field==null)throw new MissingFieldException(target.GetType().Name,property);field.boolValue=value;serialized.ApplyModifiedPropertiesWithoutUndo();}
        static void SetArray<T>(Object target,string property,T[] values) where T:Object{var serialized=new SerializedObject(target);var field=serialized.FindProperty(property);field.arraySize=values.Length;for(int i=0;i<values.Length;i++)field.GetArrayElementAtIndex(i).objectReferenceValue=values[i];serialized.ApplyModifiedPropertiesWithoutUndo();}
        static void Hook(Button button,UnityEngine.Events.UnityAction action){button.onClick.RemoveAllListeners();UnityEventTools.AddPersistentListener(button.onClick,action);EditorUtility.SetDirty(button);}
        static void Hook(Button button,ReflectableGameController controller,string value){button.onClick.RemoveAllListeners();UnityEventTools.AddStringPersistentListener(button.onClick,controller.BuyUpgrade,value);EditorUtility.SetDirty(button);}
    }
}
