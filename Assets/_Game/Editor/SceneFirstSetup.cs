using Reflectable;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

namespace Reflectable.Editor
{
    [InitializeOnLoad]
    internal static class SceneFirstAutoSetup
    {
        static SceneFirstAutoSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("Reflectable.SceneFirstSetupRan.v5", false)) return;
                SessionState.SetBool("Reflectable.SceneFirstSetupRan.v5", true);
                SceneFirstSetup.Rebuild();
            };
        }
    }

    /// <summary>Editor-only assembly step. It saves editable scenes/prefabs; never runs in play mode.</summary>
    public static class SceneFirstSetup
    {
        const string Root = "Assets/_Game";
        const string Scenes = Root + "/Scenes";
        const string Prefabs = Root + "/Prefabs";
        [MenuItem("Tools/REFLECTABLE/Rebuild Scene-First Demo")]
        public static void Rebuild()
        {
            ReflectableProjectBuilder.Build();
            UpgradePrefabs(); UpgradeGame(); UpgradeMenu();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true), new EditorBuildSettingsScene("Assets/Scenes/Game.unity", true) };
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("REFLECTABLE: scene-first demo rebuilt.");
        }
        static void UpgradePrefabs()
        {
            foreach (var name in new[] { "Block_Normal", "Block_Gem", "Block_Bomb" })
            {
                var path=Prefabs+"/Blocks/"+name+".prefab"; var root=PrefabUtility.LoadPrefabContents(path);
                var old=root.GetComponent<BlockController>(); if(old) Object.DestroyImmediate(old);
                var view=root.GetComponent<ReflectableBlockView>() ?? root.AddComponent<ReflectableBlockView>();
                var label=root.GetComponentInChildren<Text>(true); Set(view,"hpLabel",label);
                var collider=root.GetComponent<BoxCollider2D>(); if(collider) collider.size=new Vector2(2.12f,1.18f);
                var body=root.transform.Find("Visual/Body"); if(body){body.localScale=Vector3.one;var renderer=body.GetComponent<SpriteRenderer>();if(renderer){renderer.drawMode=SpriteDrawMode.Sliced;renderer.size=new Vector2(2.12f,1.18f);}}
                if(label){label.fontSize=42;label.transform.localScale=Vector3.one*.018f;}
                PrefabUtility.SaveAsPrefabAsset(root,path); PrefabUtility.UnloadPrefabContents(root);
            }
            var projectilePath=Prefabs+"/Projectiles/Projectile.prefab"; var projectile=PrefabUtility.LoadPrefabContents(projectilePath);
            var oldProjectile=projectile.GetComponent<ProjectileController>(); if(oldProjectile)Object.DestroyImmediate(oldProjectile);
            if(!projectile.GetComponent<ReflectableProjectile>())projectile.AddComponent<ReflectableProjectile>();
            var trail=projectile.GetComponent<TrailRenderer>();
            if (!trail) trail=projectile.AddComponent<TrailRenderer>();
            trail.time=.18f; trail.startWidth=.14f;trail.endWidth=.02f;
            var trailShader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Unlit/Color");
            if (trailShader != null) trail.material=new Material(trailShader);
            PrefabUtility.SaveAsPrefabAsset(projectile,projectilePath); PrefabUtility.UnloadPrefabContents(projectile);
            if(!AssetDatabase.IsValidFolder("Assets/Prefabs"))AssetDatabase.CreateFolder("Assets","Prefabs");
            if(!AssetDatabase.IsValidFolder("Assets/Prefabs/Blocks"))AssetDatabase.CreateFolder("Assets/Prefabs","Blocks");
            AssetDatabase.CopyAsset(Prefabs+"/Blocks/Block_Normal.prefab","Assets/Prefabs/Blocks/NormalBlock.prefab");
            AssetDatabase.CopyAsset(Prefabs+"/Blocks/Block_Gem.prefab","Assets/Prefabs/Blocks/GemBlock.prefab");
            AssetDatabase.CopyAsset(Prefabs+"/Blocks/Block_Bomb.prefab","Assets/Prefabs/Blocks/BombBlock.prefab");
            AssetDatabase.CopyAsset(Prefabs+"/Projectiles/Projectile.prefab","Assets/Prefabs/Projectile.prefab");
        }
        static void UpgradeGame()
        {
            var scene=EditorSceneManager.OpenScene(Scenes+"/Game.unity",OpenSceneMode.Single); var game=GameObject.Find("Game");
            foreach(var bootstrap in Object.FindObjectsByType<ReflectableBootstrap>(FindObjectsSortMode.None))Object.DestroyImmediate(bootstrap);
            var arena=game.transform.Find("Arena"); arena.gameObject.SetActive(true); var player=game.transform.Find("Player");player.gameObject.SetActive(true); var canvasComponent=Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include); var canvas=canvasComponent.gameObject;canvas.SetActive(true);
            Rename(arena,"TopWall","Ceiling"); Rename(arena,"BottomKillZone","BottomBoundary"); Rename(arena,"DangerZone","DangerLine"); var grid=arena.Find("GridRoot");grid.name="GridOrigin"; grid.position=new Vector2(0,4.45f);
            var background=game.transform.Find("Background"); if(background) background.SetParent(arena, true);
            ConfigureWall(arena.Find("LeftWall"),new Vector2(-9.4f,.25f),new Vector2(.24f,10.3f)); ConfigureWall(arena.Find("RightWall"),new Vector2(9.4f,.25f),new Vector2(.24f,10.3f)); ConfigureWall(arena.Find("Ceiling"),new Vector2(0,5.4f),new Vector2(19.05f,.24f));
            var exit=arena.Find("BottomBoundary"); exit.position=new Vector2(0,-4.9f);var exitCollider=exit.GetComponent<BoxCollider2D>();exitCollider.size=new Vector2(19f,.35f);exitCollider.isTrigger=true;
            var danger=arena.Find("DangerLine");danger.position=new Vector2(0,-4.45f);danger.localScale=new Vector3(18.8f,.08f,1f);
            player.position=new Vector2(0,-4.2f); player.Find("FirePoint").localPosition=new Vector3(0,.8f,0);
            var runtime=GetOrCreate(game.transform,"Runtime"); var blocks=GetOrCreate(runtime,"Blocks"); var projectiles=MoveChild(arena,"ProjectileRoot",runtime,"Projectiles"); MoveChild(arena,"VFXRoot",runtime,"Effects");
            var systems=GetOrCreate(game.transform,"Systems"); foreach(var systemName in new[]{"GameManager","TurnManager","ScoreManager","BlockManager","ProjectileManager","SaveManager","AudioManager"})GetOrCreate(systems,systemName); var manager=systems.Find("GameManager"); foreach(var duplicate in game.GetComponentsInChildren<ReflectableGameController>())Object.DestroyImmediate(duplicate);
            var controller=manager.gameObject.AddComponent<ReflectableGameController>();
            Rename(player,"Visual","CharacterVisual"); var visual=player.Find("CharacterVisual"); Rename(visual,"Weapon","WeaponVisual"); var previewRoot=GetOrCreate(player,"AimPreview");var preview=previewRoot.GetComponent<LineRenderer>();if(!preview)preview=previewRoot.gameObject.AddComponent<LineRenderer>();var lineShader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")??Shader.Find("Unlit/Color");if(lineShader!=null)preview.material=new Material(lineShader);preview.startWidth=.035f;preview.endWidth=.015f;preview.startColor=new Color(1,.8f,.9f,.85f);preview.endColor=new Color(.7f,.8f,1,.2f);preview.sortingOrder=10;
            var fire=player.Find("FirePoint"); var hud=GetOrCreate(canvas.transform,"HUD"); var top=canvas.transform.Find("TopHUD"); top.SetParent(hud,false); var bottom=canvas.transform.Find("BottomHUD"); bottom.SetParent(hud,false); var oldLevel=bottom.Find("Level");if(oldLevel)Object.DestroyImmediate(oldLevel.gameObject);var oldSp=bottom.Find("SkillPoints");if(oldSp)Object.DestroyImmediate(oldSp.gameObject);
            var hp=top.Find("HP").GetComponent<Text>();var turn=top.Find("Turn").GetComponent<Text>();var score=top.Find("Score").GetComponent<Text>();var gems=top.Find("Gems").GetComponent<Text>();var combo=canvas.transform.Find("ComboText").GetComponent<Text>(); var level=bottom.Find("EXPBar").GetComponent<Text>();
            var character=CreateText(hud,"CharacterText",new Vector2(0,-365),new Vector2(420,40),26,"MIMI ★");
            var over=BuildGameOver(canvas.transform,out var overText);var pause=BuildPause(canvas.transform);var upgrade=BuildUpgrade(canvas.transform);var chars=BuildCharacters(canvas.transform);
            controller.Configure(player,fire,grid,blocks,projectiles,preview,AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Blocks/NormalBlock.prefab"),AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Blocks/GemBlock.prefab"),AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Blocks/BombBlock.prefab"),AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Projectile.prefab"),hp,score,gems,turn,combo,level,character,overText,pause,over,upgrade,chars);
            Hook(bottom.Find("PowerButton").GetComponent<Button>(),controller,"BuyUpgrade", "Power");Hook(bottom.Find("RicochetButton").GetComponent<Button>(),controller,"BuyUpgrade","Ricochet");Hook(bottom.Find("ExtraBallButton").GetComponent<Button>(),controller,"BuyUpgrade","Extra Ball");Hook(bottom.Find("MaxHPButton").GetComponent<Button>(),controller,"BuyUpgrade","Max HP");
            var buttonNames=new[]{"PowerButton","RicochetButton","ExtraBallButton","MaxHPButton","CharacterButton"}; var buttonLabels=new[]{"POWER","RICOCHET","EXTRA BALL","MAX HP","CHARACTER"}; for(int i=0;i<buttonNames.Length;i++){var button=bottom.Find(buttonNames[i]);var rect=button.GetComponent<RectTransform>();rect.anchoredPosition=new Vector2(-360+i*180,-445);rect.sizeDelta=new Vector2(160,52);var label=button.GetComponentInChildren<Text>();if(label){label.text=buttonLabels[i];label.fontSize=18;}}
            Hook(bottom.Find("CharacterButton").GetComponent<Button>(),controller.OpenCharacterPanel);
            SetFloat(controller,"left",-9.28f);SetFloat(controller,"right",9.28f);SetFloat(controller,"ceiling",5.28f);SetFloat(controller,"bottom",-4.9f);SetVector2(controller,"cellSpacing",new Vector2(2.42f,1.32f));
            Hook(over.transform.Find("RetryButton").GetComponent<Button>(), controller.Retry); Hook(over.transform.Find("MenuButton").GetComponent<Button>(), controller.MainMenu);
            Hook(pause.transform.Find("ResumeButton").GetComponent<Button>(), controller.TogglePause); Hook(pause.transform.Find("RestartButton").GetComponent<Button>(), controller.Retry); Hook(pause.transform.Find("MenuButton").GetComponent<Button>(), controller.MainMenu);
            Hook(chars.transform.Find("Window/DrawButton").GetComponent<Button>(),controller.DrawCharacter); Hook(chars.transform.Find("Window/CloseButton").GetComponent<Button>(),controller.CloseCharacterPanel);
            EditorSceneManager.SaveScene(scene);
            CopyDeliverableScenes();
        }
        static void UpgradeMenu()
        {
            var scene=EditorSceneManager.OpenScene(Scenes+"/MainMenu.unity",OpenSceneMode.Single);foreach(var bootstrap in Object.FindObjectsByType<ReflectableBootstrap>(FindObjectsSortMode.None))Object.DestroyImmediate(bootstrap);var root=GameObject.Find("MainMenu");var canvas=GameObject.Find("Canvas");var panel=canvas.transform.Find("MainMenuPanel");var controller=root.GetComponent<ReflectableMenuController>()??root.AddComponent<ReflectableMenuController>();var group=canvas.GetComponent<CanvasGroup>()??canvas.AddComponent<CanvasGroup>();var settings=canvas.transform.Find("SettingsPanel").gameObject;Set(controller,"continueButton",panel.Find("ResumeButton").gameObject);Set(controller,"settingsPanel",settings);Set(controller,"bestScore",panel.Find("BestScoreText").GetComponent<Text>());Set(controller,"group",group);
            Hook(panel.Find("ResumeButton").GetComponent<Button>(),controller.Continue);Hook(panel.Find("PlayGameButton").GetComponent<Button>(),controller.NewGame);Hook(panel.Find("SettingsButton").GetComponent<Button>(),controller.ToggleSettings);Hook(panel.Find("ExitButton").GetComponent<Button>(),controller.Quit);EditorSceneManager.SaveScene(scene);CopyDeliverableScenes();
        }
        static GameObject BuildGameOver(Transform parent,out Text message){var p=Panel(parent,"GameOverPanel");message=CreateText(p.transform,"Message",Vector2.zero,new Vector2(700,360),32,"GAME OVER");var retry=Button(p.transform,"RetryButton",new Vector2(-120,-260),"RETRY");var menu=Button(p.transform,"MenuButton",new Vector2(120,-260),"MAIN MENU");p.SetActive(false);return p;}
        static GameObject BuildPause(Transform parent){var p=Panel(parent,"PausePanel");CreateText(p.transform,"Title",new Vector2(0,200),new Vector2(500,70),44,"PAUSED");Button(p.transform,"ResumeButton",new Vector2(0,80),"RESUME");Button(p.transform,"RestartButton",new Vector2(0,0),"RESTART");Button(p.transform,"MenuButton",new Vector2(0,-80),"MAIN MENU");p.SetActive(false);return p;}
        static GameObject BuildUpgrade(Transform parent){var p=Panel(parent,"UpgradePanel");CreateText(p.transform,"Title",new Vector2(0,180),new Vector2(600,60),32,"UPGRADES");p.SetActive(false);return p;}
        static GameObject BuildCharacters(Transform parent){var p=Panel(parent,"CharacterPanel");var dim=new GameObject("DimBackground",typeof(RectTransform),typeof(Image));dim.transform.SetParent(p.transform,false);var dimRect=dim.GetComponent<RectTransform>();dimRect.anchorMin=Vector2.zero;dimRect.anchorMax=Vector2.one;dimRect.offsetMin=dimRect.offsetMax=Vector2.zero;dim.GetComponent<Image>().color=new Color(.08f,.06f,.14f,.82f);var window=new GameObject("Window",typeof(RectTransform),typeof(Image));window.transform.SetParent(p.transform,false);window.GetComponent<Image>().color=new Color(.28f,.23f,.42f,1f);var wr=window.GetComponent<RectTransform>();wr.anchorMin=wr.anchorMax=new Vector2(.5f,.5f);wr.sizeDelta=new Vector2(720,500);CreateText(window.transform,"Title",new Vector2(0,205),new Vector2(640,55),32,"CHARACTER COLLECTION");CreateText(window.transform,"CurrentCharacter",new Vector2(0,125),new Vector2(600,40),25,"CURRENT: MIMI");CreateText(window.transform,"CharacterRank",new Vector2(0,80),new Vector2(600,35),23,"RANK ★");CreateText(window.transform,"CharacterDescription",new Vector2(0,35),new Vector2(620,35),18,"MIMI — balanced ricochet specialist");CreateText(window.transform,"GemAmount",new Vector2(0,-25),new Vector2(620,35),20,"YOUR GEMS: 0");CreateText(window.transform,"DrawCost",new Vector2(0,-60),new Vector2(620,35),20,"DRAW COST: 2");CreateText(window.transform,"ResultArea",new Vector2(0,-135),new Vector2(620,100),24,"DRAW A CHARACTER");Button(window.transform,"DrawButton",new Vector2(-120,-220),"DRAW");Button(window.transform,"CloseButton",new Vector2(120,-220),"CLOSE");p.SetActive(false);return p;}
        static GameObject Panel(Transform p,string name){var old=p.Find(name);if(old)Object.DestroyImmediate(old.gameObject);var g=new GameObject(name,typeof(RectTransform),typeof(Image));g.transform.SetParent(p,false);var r=g.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;g.GetComponent<Image>().color=new Color(.15f,.12f,.25f,.88f);return g;}
        static Text CreateText(Transform p,string name,Vector2 pos,Vector2 size,int fontSize,string value){var g=new GameObject(name,typeof(RectTransform),typeof(Text));g.transform.SetParent(p,false);var r=g.GetComponent<RectTransform>();r.anchoredPosition=pos;r.sizeDelta=size;var t=g.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=value;t.fontSize=fontSize;t.alignment=TextAnchor.MiddleCenter;t.color=Color.white;return t;}
        static Button Button(Transform p,string name,Vector2 pos,string label){var g=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));g.transform.SetParent(p,false);var r=g.GetComponent<RectTransform>();r.anchoredPosition=pos;r.sizeDelta=new Vector2(210,55);g.GetComponent<Image>().color=new Color(.7f,.6f,.9f);CreateText(g.transform,"Label",Vector2.zero,new Vector2(200,50),21,label).raycastTarget=false;return g.GetComponent<Button>();}
        static void Hook(Button button,UnityEngine.Events.UnityAction action){button.onClick.RemoveAllListeners();UnityEventTools.AddPersistentListener(button.onClick,action);}
        static void Hook(Button button,ReflectableGameController target,string method,string value){button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>target.BuyUpgrade(value));}
        static void CopyDeliverableScenes(){if(!AssetDatabase.IsValidFolder("Assets/Scenes"))AssetDatabase.CreateFolder("Assets","Scenes");AssetDatabase.CopyAsset(Scenes+"/Game.unity","Assets/Scenes/Game.unity");AssetDatabase.CopyAsset(Scenes+"/MainMenu.unity","Assets/Scenes/MainMenu.unity");}
        static void ConfigureWall(Transform wall,Vector2 position,Vector2 size){wall.position=position;wall.localScale=new Vector3(size.x,size.y,1f);var collider=wall.GetComponent<BoxCollider2D>();if(collider)collider.size=Vector2.one;var renderer=wall.GetComponent<SpriteRenderer>();if(renderer){renderer.drawMode=SpriteDrawMode.Sliced;renderer.size=Vector2.one;}}
        static void SetFloat(Object target,string property,float value){var so=new SerializedObject(target);so.FindProperty(property).floatValue=value;so.ApplyModifiedPropertiesWithoutUndo();} static void SetVector2(Object target,string property,Vector2 value){var so=new SerializedObject(target);so.FindProperty(property).vector2Value=value;so.ApplyModifiedPropertiesWithoutUndo();}
        static Transform GetOrCreate(Transform parent,string name){var x=parent.Find(name);if(x)return x;var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;} static Transform MoveChild(Transform from,string child,Transform to,string name){var x=from.Find(child);x.SetParent(to,true);x.name=name;return x;} static void Rename(Transform parent,string oldName,string newName){if(parent==null)return;var x=parent.Find(oldName);if(x)x.name=newName;} static void Set(Object o,string prop,Object value){var so=new SerializedObject(o);so.FindProperty(prop).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    }
}
