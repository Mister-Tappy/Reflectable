using System;
using System.IO;
using Reflectable;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Reflectable.Editor
{
    public static class ReflectableProjectBuilder
    {
        const string Root="Assets/_Game", Scenes=Root+"/Scenes", Prefabs=Root+"/Prefabs";
        static readonly Color Cream=new Color(1f,.961f,.914f), Pink=new Color(.969f,.718f,.824f), Blue=new Color(.663f,.867f,.961f), Lavender=new Color(.784f,.714f,.91f), Purple=new Color(.486f,.416f,.651f), Ink=new Color(.294f,.271f,.388f);
        static Sprite sprite; static Font font;
        [MenuItem("Tools/REFLECTABLE/Build Demo Project")]
        public static void Build()
        {
            EnsureFolders(); sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildPrefabs(); BuildMainMenu(); BuildGame();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); Debug.Log("REFLECTABLE: scenes and prefabs assembled.");
        }
        static void EnsureFolders(){foreach(var p in new[]{Root,Scenes,Prefabs,Prefabs+"/Blocks",Prefabs+"/Projectiles",Prefabs+"/Characters"})if(!AssetDatabase.IsValidFolder(p)){var parent=Path.GetDirectoryName(p).Replace('\\','/');AssetDatabase.CreateFolder(parent,Path.GetFileName(p));}}
        static GameObject Go(string name,Transform parent=null){var g=new GameObject(name);if(parent)g.transform.SetParent(parent,false);return g;}
        static void Sprite(GameObject g,Color color,Vector2 scale){var sr=g.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.color=color;g.transform.localScale=scale;}
        static Text Text(string n,Transform p,string v,Vector2 pos,Vector2 size,int fs=28){var g=Go(n,p);var rt=g.AddComponent<RectTransform>();rt.anchoredPosition=pos;rt.sizeDelta=size;var t=g.AddComponent<Text>();t.font=font;t.text=v;t.fontSize=fs;t.alignment=TextAnchor.MiddleCenter;t.color=Ink;return t;}
        static Button Button(string n,Transform p,Vector2 pos){var g=Go(n,p);var rt=g.AddComponent<RectTransform>();rt.anchoredPosition=pos;rt.sizeDelta=new Vector2(330,58);var im=g.AddComponent<Image>();im.sprite=sprite;im.color=Lavender;var b=g.AddComponent<Button>();b.targetGraphic=im;Text("Label",g.transform,n.Replace("Button",""),Vector2.zero,new Vector2(320,54),24).raycastTarget=false;return b;}
        static void CameraAndEvent(){var c=Go("Main Camera");c.tag="MainCamera";c.transform.position=new Vector3(0,0,-10);var cam=c.AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=6.2f;cam.backgroundColor=Cream;var e=Go("EventSystem");e.AddComponent<EventSystem>();e.AddComponent<InputSystemUIInputModule>();}
        static Transform CanvasRoot(){var g=Go("Canvas");var c=g.AddComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;var s=g.AddComponent<CanvasScaler>();s.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;s.referenceResolution=new Vector2(1920,1080);g.AddComponent<GraphicRaycaster>();return g.transform;}
        static void BuildMainMenu()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); CameraAndEvent();
            var root=Go("MainMenu"); var bg=Go("Background",root.transform);Sprite(bg,Cream,new Vector2(20,12));
            foreach(var x in new[]{new Vector2(-6,3),new Vector2(6,-2),new Vector2(-5,-3)}){var d=Go("DecorativeCircle",bg.transform);Sprite(d,x.x<0?Pink:Blue,Vector2.one*1.5f);d.transform.position=x;}
            var mascot=Go("MenuCharacter",root.transform);mascot.transform.position=new Vector3(-4,-2,0);Sprite(Go("Body",mascot.transform),Pink,new Vector2(1.3f,1));var head=Go("Head",mascot.transform);head.transform.localPosition=new Vector3(0,.7f);Sprite(head,Blue,new Vector2(1.5f,1.25f));foreach(float x in new[]{-.25f,.25f}){var eye=Go("Eye",head.transform);eye.transform.localPosition=new Vector3(x,0);Sprite(eye,Ink,Vector2.one*.12f);}
            var canvas=CanvasRoot();var logo=Go("Logo",canvas);Text("REFLECTABLE",logo.transform,"REFLECTABLE",new Vector2(0,300),new Vector2(900,110),70).color=Purple;Text("Subtitle",logo.transform,"A pastel ricochet adventure",new Vector2(0,235),new Vector2(700,45),24);
            var main=Go("MainMenuPanel",canvas);Button("ResumeButton",main.transform,new Vector2(0,110));Button("PlayGameButton",main.transform,new Vector2(0,35));Text("BestScoreText",main.transform,"BEST SCORE : 0",new Vector2(0,-55),new Vector2(400,40),22);Button("SettingsButton",main.transform,new Vector2(0,-125));Button("ExitButton",main.transform,new Vector2(0,-200));
            var maps=Go("MapSelectPanel",canvas);Text("Title",maps.transform,"MAP SELECT",new Vector2(0,300),new Vector2(700,70),50);Button("LeftArrow",maps.transform,new Vector2(-470,0));var card=Go("MapCard",maps.transform);var ci=card.AddComponent<Image>();ci.sprite=sprite;ci.color=Pink;var cr=card.GetComponent<RectTransform>();cr.sizeDelta=new Vector2(620,300);Text("MapName",card.transform,"01 BLOOM GARDEN",new Vector2(0,50),new Vector2(560,65),36);Text("LockStatus",card.transform,"UNLOCKED",new Vector2(0,-30),new Vector2(500,50),22);Button("RightArrow",maps.transform,new Vector2(470,0));Button("PlayButton",maps.transform,new Vector2(0,-230));Button("BackButton",maps.transform,new Vector2(0,-305));maps.SetActive(false);
            var settings=Go("SettingsPanel",canvas);Text("Title",settings.transform,"SETTINGS",new Vector2(0,170),new Vector2(500,60),44);Text("Options",settings.transform,"MASTER VOLUME  100%\nMUSIC VOLUME  70%\nSFX VOLUME  85%\nSCREEN SHAKE  60%",Vector2.zero,new Vector2(500,180),25);Button("BackButton",settings.transform,new Vector2(0,-180));settings.SetActive(false);
            Go("MenuManager",root.transform);
            EditorSceneManager.SaveScene(scene,Scenes+"/MainMenu.unity");
        }
        static void BuildGame()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); CameraAndEvent();var root=Go("Game");var bg=Go("Background",root.transform);Sprite(bg,Cream,new Vector2(20,12));bg.GetComponent<SpriteRenderer>().sortingOrder=-100;
            var arena=Go("Arena",root.transform); foreach(var a in new[]{("TopWall",new Vector2(0,5.4f),new Vector2(14.6f,.25f)),("LeftWall",new Vector2(-7.2f,0),new Vector2(.25f,11.5f)),("RightWall",new Vector2(7.2f,0),new Vector2(.25f,11.5f))}){var w=Go(a.Item1,arena.transform);w.transform.position=a.Item2;Sprite(w,Lavender,a.Item3);w.AddComponent<BoxCollider2D>();}var kill=Go("BottomKillZone",arena.transform);kill.transform.position=new Vector2(0,-5.5f);var kc=kill.AddComponent<BoxCollider2D>();kc.size=new Vector2(14,.2f);kc.isTrigger=true;var danger=Go("DangerZone",arena.transform);danger.transform.position=new Vector2(0,-4.2f);Sprite(danger,Pink,new Vector2(13,.08f));Go("GridRoot",arena.transform);Go("ProjectileRoot",arena.transform);Go("VFXRoot",arena.transform);arena.SetActive(false);
            var player=Go("Player",root.transform);player.transform.position=new Vector2(0,-4.65f);var visual=Go("Visual",player.transform);Sprite(Go("Body",visual.transform),Pink,new Vector2(.85f,.7f));var head=Go("Head",visual.transform);head.transform.localPosition=new Vector3(0,.5f);Sprite(head,Blue,new Vector2(1.1f,.9f));foreach(float x in new[]{-.18f,.18f}){var eye=Go("Eye",head.transform);eye.transform.localPosition=new Vector3(x,0);Sprite(eye,Ink,Vector2.one*.1f);}var weapon=Go("Weapon",visual.transform);weapon.transform.localPosition=new Vector3(.55f,.25f);Sprite(weapon,Purple,new Vector2(.55f,.12f));var fire=Go("FirePoint",player.transform);fire.transform.localPosition=new Vector3(.72f,.25f);player.AddComponent<PlayerController>();player.AddComponent<AimController>();player.SetActive(false);
            var managers=Go("Managers",root.transform);foreach(var n in new[]{"GameManager","TurnManager","BlockManager","ProjectileManager","ComboManager","ScoreManager","ExperienceManager","CharacterManager","GemManager","SaveManager"})Go(n,managers.transform);managers.transform.Find("GameManager").gameObject.AddComponent<GameManager>();
            var canvas=CanvasRoot();var top=Go("TopHUD",canvas);Text("HP",top.transform,"HP 100 / 100",new Vector2(-760,470),new Vector2(300,50),27);Text("Turn",top.transform,"TURN 1",new Vector2(-100,470),new Vector2(230,50),27);Text("Score",top.transform,"SCORE 0",new Vector2(180,470),new Vector2(250,50),27);Text("Gems",top.transform,"GEM 0",new Vector2(760,470),new Vector2(220,50),27);Text("ComboText",canvas,"",new Vector2(0,330),new Vector2(600,70),40);var bottom=Go("BottomHUD",canvas);Text("Level",bottom.transform,"LV 1",new Vector2(-760,-465),new Vector2(120,40),22);Text("EXPBar",bottom.transform,"EXP 0 / 40",new Vector2(-620,-465),new Vector2(220,40),22);Text("SkillPoints",bottom.transform,"SP 0",new Vector2(-420,-465),new Vector2(120,40),22);foreach(var n in new[]{"PowerButton","RicochetButton","ExtraBallButton","CharacterButton"})Button(n,bottom.transform,new Vector2(-120+Array.IndexOf(new[]{"PowerButton","RicochetButton","ExtraBallButton","CharacterButton"},n)*180,-465));foreach(var n in new[]{"CharacterDrawPanel","PausePanel","GameOverPanel"}){var p=Go(n,canvas);p.SetActive(false);}canvas.gameObject.SetActive(false);
            EditorSceneManager.SaveScene(scene,Scenes+"/Game.unity");
        }
        static void BuildPrefabs()
        {
            MakeBlock("Block_Normal",BlockType.Normal,Lavender);MakeBlock("Block_Bomb",BlockType.Bomb,Pink);MakeBlock("Block_Gem",BlockType.Gem,Blue);
            var p=Go("Projectile");Sprite(p,Blue,Vector2.one*.25f);var rb=p.AddComponent<Rigidbody2D>();rb.gravityScale=0;rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;rb.interpolation=RigidbodyInterpolation2D.Interpolate;p.AddComponent<CircleCollider2D>();p.AddComponent<ProjectileController>();SavePrefab(p,Prefabs+"/Projectiles/Projectile.prefab");
        }
        static void MakeBlock(string name,BlockType type,Color color){var r=Go(name);r.AddComponent<BoxCollider2D>();var visual=Go("Visual",r.transform);Sprite(Go("Body",visual.transform),color,Vector2.one*1.35f);var icon=Go("Icon",visual.transform);Sprite(icon,type==BlockType.Gem?Blue:type==BlockType.Bomb?Pink:Lavender,Vector2.one*.28f);var label=Go("HPText",visual.transform);var c=label.AddComponent<Canvas>();c.renderMode=RenderMode.WorldSpace;var t=label.AddComponent<Text>();t.font=font;t.text="10";t.alignment=TextAnchor.MiddleCenter;t.color=Ink;label.GetComponent<RectTransform>().sizeDelta=new Vector2(120,50);label.transform.localScale=Vector3.one*.012f;r.AddComponent<BlockController>();SavePrefab(r,Prefabs+"/Blocks/"+name+".prefab");}
        static void SavePrefab(GameObject g,string path){PrefabUtility.SaveAsPrefabAsset(g,path);UnityEngine.Object.DestroyImmediate(g);}
    }
}
