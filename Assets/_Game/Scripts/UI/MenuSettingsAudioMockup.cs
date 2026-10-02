using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Reflectable
{
    /// <summary>Procedural placeholder audio plus persistent menu settings for the prototype.</summary>
    public sealed class MenuSettingsAudioMockup : MonoBehaviour
    {
        const string MusicKey = "Reflectable.MusicVolume";
        const string SfxKey = "Reflectable.SfxVolume";
        const string ShakeKey = "Reflectable.ScreenShake";
        const string MasterKey = "Reflectable.MasterVolume";
        const string SensitivityKey = "Reflectable.MenuCameraSensitivity";
        const string FullscreenKey = "Reflectable.Fullscreen";
        const string QualityKey = "Reflectable.GraphicsQuality";
        static MenuSettingsAudioMockup instance;
        AudioSource musicSource;
        AudioSource sfxSource;
        AudioClip impactClip, shotClip, comboClip, buttonClip;
        AudioClip blockBreakClip, damageHitClip;
        AudioClip mainMenuMusicClip, inGameMusicClip;
        AudioClip[] ballCollisionClips;
        int lastButtonFrame = -1;

        public static float MusicVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, .65f));
        public static float SfxVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, .8f));
        public static float ScreenShakeIntensity => Mathf.Clamp01(PlayerPrefs.GetFloat(ShakeKey, .65f));
        public static float MasterVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, 1f));
        public static float CameraSensitivity => Mathf.Clamp01(PlayerPrefs.GetFloat(SensitivityKey, .5f));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (instance) return;
            instance = FindFirstObjectByType<MenuSettingsAudioMockup>();
            if (instance) return;
            new GameObject("ReflectableAudioMockup").AddComponent<MenuSettingsAudioMockup>();
        }

        void Awake()
        {
            if (instance && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            musicSource = gameObject.AddComponent<AudioSource>();
            sfxSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.spatialBlend = sfxSource.spatialBlend = 0f;
            AudioListener.volume = MasterVolume;
            Screen.fullScreen = Fullscreen;
            int qualityIndex = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            QualitySettings.SetQualityLevel(qualityIndex, true);
            impactClip = CreateClip("Mockup_Impact", .16f, false, (t,d) =>
            {
                float noise=(Mathf.Sin(2*Mathf.PI*1270*t)+Mathf.Sin(2*Mathf.PI*1930*t)*.63f+Mathf.Sin(2*Mathf.PI*2710*t)*.41f)*Mathf.Exp(-t*48f);
                return (Mathf.Sin(2*Mathf.PI*(110*t-230*t*t))*.7f+noise*.28f)*Mathf.Exp(-t*24f);
            });
            shotClip = CreateClip("Mockup_BallShot", .24f, false, (t,d) =>
            {
                float p=t/d;
                return Mathf.Sin(2*Mathf.PI*Mathf.Lerp(260f,1050f,p)*t)*Mathf.Sin(Mathf.PI*p)*Mathf.Exp(-p*1.3f)*.68f;
            });
            comboClip = CreateClip("Mockup_HighCombo", .72f, false, (t,d) =>
            {
                float[] notes={523.25f,659.25f,783.99f,1046.5f};
                int i=Mathf.Min(notes.Length-1,Mathf.FloorToInt(t/.15f));
                float local=t-i*.15f;
                return (Mathf.Sin(2*Mathf.PI*notes[i]*t)+.28f*Mathf.Sin(4*Mathf.PI*notes[i]*t))*Mathf.Exp(-local*7f)*Mathf.Clamp01((d-t)*12f)*.36f;
            });
            buttonClip = CreateClip("Mockup_ButtonPress", .075f, false, (t,d) =>
            {
                float p=t/d;
                return Mathf.Sin(2*Mathf.PI*Mathf.Lerp(740f,430f,p)*t)*Mathf.Exp(-p*4.2f)*.48f;
            });
            buttonClip = Resources.Load<AudioClip>("SFX/Click") ?? buttonClip;
            blockBreakClip = Resources.Load<AudioClip>("SFX/BlockBreak");
            damageHitClip = Resources.Load<AudioClip>("SFX/damagehit");
            ballCollisionClips = new[]
            {
                Resources.Load<AudioClip>("SFX/hit_a"),
                Resources.Load<AudioClip>("SFX/hit_d"),
                Resources.Load<AudioClip>("SFX/hit_d2"),
                Resources.Load<AudioClip>("SFX/hit_e2"),
                Resources.Load<AudioClip>("SFX/hit_f2")
            };
            mainMenuMusicClip = Resources.Load<AudioClip>("SFX/Bg/mainmenuBg");
            inGameMusicClip = Resources.Load<AudioClip>("SFX/Bg/InGameBg");
            SceneManager.sceneLoaded += SceneLoaded;
        }

        void OnDestroy()
        {
            if(instance!=this)return;
            SceneManager.sceneLoaded-=SceneLoaded;
            instance=null;
        }

        void SceneLoaded(Scene scene,LoadSceneMode mode)
        {
            bool menu=scene.name=="MainMenu";
            bool game=scene.name=="Game"||scene.name=="InGame";
            musicSource.volume=MusicVolume*.42f;
            AudioClip sceneMusic=menu?mainMenuMusicClip:game?inGameMusicClip:null;
            if(musicSource.clip!=sceneMusic)
            {
                musicSource.Stop();
                musicSource.clip=sceneMusic;
            }
            if(sceneMusic&&!musicSource.isPlaying)musicSource.Play();
            else if(!sceneMusic&&musicSource.isPlaying)musicSource.Stop();

            GameObject settings=null,pauseSettings=null,main=null;
            foreach(var root in scene.GetRootGameObjects())
                foreach(var item in root.GetComponentsInChildren<Transform>(true))
                {
                    if(item.name=="SettingsPanel")settings=item.gameObject;
                    else if(item.name=="PauseSettingsPanel")pauseSettings=item.gameObject;
                    else if(item.name=="MainMenuPanel")main=item.gameObject;
                }
            if(menu&&settings)
            {
                var ui=settings.GetComponent<MenuSettingsPanel>();
                if(!ui)ui=settings.AddComponent<MenuSettingsPanel>();
                ui.Build(main);
            }
            if(game&&pauseSettings)
            {
                var ui=pauseSettings.GetComponent<MenuSettingsPanel>();
                if(!ui)ui=pauseSettings.AddComponent<MenuSettingsPanel>();
                ui.Build(null);
            }
            if(menu || game)
                foreach(var root in scene.GetRootGameObjects())
                    foreach(var button in root.GetComponentsInChildren<Button>(true))
                        button.onClick.AddListener(PlayButton);
        }

        public static void PlayImpact()=>Play(instance?instance.impactClip:null,.72f);
        public static void PlayBallShot()=>Play(instance?instance.shotClip:null,.55f);
        public static void PlayHighCombo()=>Play(instance?instance.comboClip:null,.85f);
        public static void PlayBlockBreak()=>Play(instance?instance.blockBreakClip:null,.8f);
        public static void PlayDamageHit()=>Play(instance?instance.damageHitClip:null,.9f);
        public static void PlayBallCollision()
        {
            if(!instance||instance.ballCollisionClips==null||instance.ballCollisionClips.Length==0)return;
            var clip=instance.ballCollisionClips[UnityEngine.Random.Range(0,instance.ballCollisionClips.Length)];
            Play(clip,.75f);
        }
        public static void PlayButton()
        {
            if(!instance||instance.lastButtonFrame==Time.frameCount)return;
            instance.lastButtonFrame=Time.frameCount;
            Play(instance.buttonClip,.55f);
        }
        static void Play(AudioClip clip,float gain)
        {
            if(instance&&instance.sfxSource&&clip&&SfxVolume>.001f)instance.sfxSource.PlayOneShot(clip,gain*SfxVolume);
        }
        public static void SetMusicVolume(float value)
        {
            PlayerPrefs.SetFloat(MusicKey,Mathf.Clamp01(value));PlayerPrefs.Save();
            if(instance&&instance.musicSource)instance.musicSource.volume=MusicVolume*.42f;
        }
        public static void SetSfxVolume(float value){PlayerPrefs.SetFloat(SfxKey,Mathf.Clamp01(value));PlayerPrefs.Save();}
        public static void SetScreenShakeIntensity(float value){PlayerPrefs.SetFloat(ShakeKey,Mathf.Clamp01(value));PlayerPrefs.Save();}
        public static void SetMasterVolume(float value){PlayerPrefs.SetFloat(MasterKey,Mathf.Clamp01(value));PlayerPrefs.Save();AudioListener.volume=MasterVolume;}
        public static void SetCameraSensitivity(float value){PlayerPrefs.SetFloat(SensitivityKey,Mathf.Clamp01(value));PlayerPrefs.Save();}
        public static bool Fullscreen => PlayerPrefs.GetInt(FullscreenKey,Screen.fullScreen?1:0)==1;
        public static void SetFullscreen(bool value){PlayerPrefs.SetInt(FullscreenKey,value?1:0);PlayerPrefs.Save();Screen.fullScreen=value;}
        public static int GraphicsQualityIndex => Mathf.Clamp(PlayerPrefs.GetInt(QualityKey,QualitySettings.GetQualityLevel()),0,Mathf.Max(0,QualitySettings.names.Length-1));
        public static void SetGraphicsQuality(int value){if(QualitySettings.names.Length==0)return;int index=Mathf.Clamp(value,0,QualitySettings.names.Length-1);PlayerPrefs.SetInt(QualityKey,index);PlayerPrefs.Save();QualitySettings.SetQualityLevel(index,true);}

        static AudioClip CreateClip(string name,float duration,bool loop,Func<float,float,float> sample)
        {
            const int rate=22050;
            int count=Mathf.CeilToInt(duration*rate);
            var data=new float[count];
            for(int i=0;i<count;i++)data[i]=Mathf.Clamp(sample(i/(float)rate,duration),-1f,1f);
            var clip=AudioClip.Create(name,count,1,rate,false);
            clip.SetData(data,0);
            return clip;
        }
    }

    /// <summary>Builds the settings controls inside the scene's existing settings panel.</summary>
    public sealed class MenuSettingsPanel : MonoBehaviour
    {
        static readonly Vector2Int[] Resolutions={new Vector2Int(1280,720),new Vector2Int(1600,900),new Vector2Int(1920,1080),new Vector2Int(2560,1440)};
        bool built;
        GameObject mainPanel;

        public void Build(GameObject mainMenuPanel)
        {
            mainPanel=mainMenuPanel;
            Transform existingCard=null;
            foreach(var child in GetComponentsInChildren<Transform>(true))
                if(child.name=="SettingsCard"){existingCard=child;break;}
            if(existingCard)
            {
                built=true;
                BindExistingCard(existingCard);
                return;
            }
            if(built)return;
            built=true;
            Font font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var card=new GameObject("SettingsCard",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            card.transform.SetParent(transform,false);
            var cardRect=card.GetComponent<RectTransform>();
            cardRect.anchorMin=cardRect.anchorMax=new Vector2(.5f,.5f);
            cardRect.anchoredPosition=Vector2.zero;cardRect.sizeDelta=new Vector2(840,790);
            float uiScale=Application.isPlaying?Mathf.Min(1f,Screen.height/900f,Screen.width/1000f):1f;
            cardRect.localScale=Vector3.one*Mathf.Max(.62f,uiScale);
            card.GetComponent<Image>().color=new Color(.035f,.055f,.11f,.97f);
            AddText(card.transform,font,"SETTINGS",new Vector2(0,345),new Vector2(700,52),36,TextAnchor.MiddleCenter,Color.white);
            AddText(card.transform,font,"DISPLAY  ·  AUDIO  ·  GAME FEEL",new Vector2(0,306),new Vector2(700,32),17,TextAnchor.MiddleCenter,new Color(.55f,.87f,1));

            int resolution=Mathf.Clamp(PlayerPrefs.GetInt("Reflectable.ResolutionIndex",2),0,Resolutions.Length-1);
            if(Application.isPlaying)ApplyResolution(resolution);
            AddSlider(card.transform,font,"DISPLAY RESOLUTION",new Vector2(0,250),resolution,0,Resolutions.Length-1,true,
                value=>
                {
                    int i=Mathf.Clamp(Mathf.RoundToInt(value),0,Resolutions.Length-1);
                    PlayerPrefs.SetInt("Reflectable.ResolutionIndex",i);PlayerPrefs.Save();
                    ApplyResolution(i);
                },
                value=>{var r=Resolutions[Mathf.Clamp(Mathf.RoundToInt(value),0,Resolutions.Length-1)];return r.x+" × "+r.y;});
            AddSlider(card.transform,font,"MASTER VOLUME",new Vector2(0,168),MenuSettingsAudioMockup.MasterVolume,0,1,false,
                MenuSettingsAudioMockup.SetMasterVolume,v=>Mathf.RoundToInt(v*100)+"%");
            AddSlider(card.transform,font,"MUSIC VOLUME",new Vector2(0,86),MenuSettingsAudioMockup.MusicVolume,0,1,false,
                MenuSettingsAudioMockup.SetMusicVolume,v=>Mathf.RoundToInt(v*100)+"%");
            AddSlider(card.transform,font,"SFX VOLUME",new Vector2(0,4),MenuSettingsAudioMockup.SfxVolume,0,1,false,
                MenuSettingsAudioMockup.SetSfxVolume,v=>Mathf.RoundToInt(v*100)+"%");
            AddSlider(card.transform,font,"SCREEN SHAKE INTENSITY",new Vector2(0,-78),MenuSettingsAudioMockup.ScreenShakeIntensity,0,1,false,
                MenuSettingsAudioMockup.SetScreenShakeIntensity,v=>Mathf.RoundToInt(v*100)+"%");
            AddSlider(card.transform,font,"CAMERA SENSITIVITY",new Vector2(0,-160),MenuSettingsAudioMockup.CameraSensitivity,0,1,false,
                MenuSettingsAudioMockup.SetCameraSensitivity,v=>Mathf.RoundToInt(v*100)+"%");
            Text fullscreenLabel = null;
            fullscreenLabel = AddButton(card.transform,font,"FULLSCREEN: "+(MenuSettingsAudioMockup.Fullscreen?"ON":"OFF"),new Vector2(-165,-248),()=>
            {
                MenuSettingsAudioMockup.SetFullscreen(!MenuSettingsAudioMockup.Fullscreen);
                if(fullscreenLabel)fullscreenLabel.text="FULLSCREEN: "+(MenuSettingsAudioMockup.Fullscreen?"ON":"OFF");
            },300,"FullscreenButton");
            Text qualityLabel = null;
            qualityLabel = AddButton(card.transform,font,"QUALITY: "+CurrentQualityName(),new Vector2(165,-248),()=>
            {
                int count=QualitySettings.names.Length;
                if(count==0)return;
                MenuSettingsAudioMockup.SetGraphicsQuality((MenuSettingsAudioMockup.GraphicsQualityIndex+1)%count);
                if(qualityLabel)qualityLabel.text="QUALITY: "+CurrentQualityName();
            },300,"QualityButton");
            AddButton(card.transform,font,"DONE",new Vector2(0,-342),()=>
            {
                var menu=FindFirstObjectByType<ReflectableMenuController>();
                if(menu)menu.ReturnFromSettings();
                else {gameObject.SetActive(false);if(mainPanel)mainPanel.SetActive(true);}
            },220,"DoneButton");
        }

        void BindExistingCard(Transform card)
        {
            int resolution=Mathf.Clamp(PlayerPrefs.GetInt("Reflectable.ResolutionIndex",2),0,Resolutions.Length-1);
            if(Application.isPlaying)ApplyResolution(resolution);
            BindSlider(card,"DISPLAYRESOLUTIONSlider","DISPLAYRESOLUTIONValue",resolution,0,Resolutions.Length-1,true,
                value=>
                {
                    int i=Mathf.Clamp(Mathf.RoundToInt(value),0,Resolutions.Length-1);
                    PlayerPrefs.SetInt("Reflectable.ResolutionIndex",i);PlayerPrefs.Save();
                    ApplyResolution(i);
                },
                value=>{var r=Resolutions[Mathf.Clamp(Mathf.RoundToInt(value),0,Resolutions.Length-1)];return r.x+" × "+r.y;});
            BindSlider(card,"MASTERVOLUMESlider","MASTERVOLUMEValue",MenuSettingsAudioMockup.MasterVolume,0,1,false,
                MenuSettingsAudioMockup.SetMasterVolume,v=>Mathf.RoundToInt(v*100)+"%");
            BindSlider(card,"MUSICVOLUMESlider","MUSICVOLUMEValue",MenuSettingsAudioMockup.MusicVolume,0,1,false,
                MenuSettingsAudioMockup.SetMusicVolume,v=>Mathf.RoundToInt(v*100)+"%");
            BindSlider(card,"SFXVOLUMESlider","SFXVOLUMEValue",MenuSettingsAudioMockup.SfxVolume,0,1,false,
                MenuSettingsAudioMockup.SetSfxVolume,v=>Mathf.RoundToInt(v*100)+"%");
            BindSlider(card,"SCREENSHAKEINTENSITYSlider","SCREENSHAKEINTENSITYValue",MenuSettingsAudioMockup.ScreenShakeIntensity,0,1,false,
                MenuSettingsAudioMockup.SetScreenShakeIntensity,v=>Mathf.RoundToInt(v*100)+"%");
            BindSlider(card,"CAMERASENSITIVITYSlider","CAMERASENSITIVITYValue",MenuSettingsAudioMockup.CameraSensitivity,0,1,false,
                MenuSettingsAudioMockup.SetCameraSensitivity,v=>Mathf.RoundToInt(v*100)+"%");

            BindButton(card,"FullscreenButton",button=>
            {
                MenuSettingsAudioMockup.SetFullscreen(!MenuSettingsAudioMockup.Fullscreen);
                SetButtonLabel(button,"FULLSCREEN: "+(MenuSettingsAudioMockup.Fullscreen?"ON":"OFF"));
            });
            BindButton(card,"QualityButton",button=>
            {
                int count=QualitySettings.names.Length;
                if(count==0)return;
                MenuSettingsAudioMockup.SetGraphicsQuality((MenuSettingsAudioMockup.GraphicsQualityIndex+1)%count);
                SetButtonLabel(button,"QUALITY: "+CurrentQualityName());
            });
            BindButton(card,"DoneButton",_=>
            {
                var game=FindFirstObjectByType<ReflectableGameController>();
                if(game){game.ClosePauseSettings();return;}
                var menu=FindFirstObjectByType<ReflectableMenuController>();
                if(menu)menu.ReturnFromSettings();
                else {gameObject.SetActive(false);if(mainPanel)mainPanel.SetActive(true);}
            });
        }

        static void BindSlider(Transform card,string sliderName,string valueName,float value,float min,float max,bool whole,Action<float> changed,Func<float,string> format)
        {
            var sliderTransform=card.Find(sliderName);
            if(!sliderTransform)return;
            var slider=sliderTransform.GetComponent<Slider>();
            if(!slider)return;
            slider.minValue=min;slider.maxValue=max;slider.wholeNumbers=whole;slider.SetValueWithoutNotify(value);
            var readout=card.Find(valueName)?.GetComponent<Text>();
            if(readout)readout.text=format(value);
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(v=>
            {
                if(readout)readout.text=format(v);
                changed(v);
            });
        }

        static void BindButton(Transform card,string buttonName,Action<Button> clicked)
        {
            var buttonTransform=card.Find(buttonName);
            if(!buttonTransform)return;
            var button=buttonTransform.GetComponent<Button>();
            if(!button)return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(()=>clicked(button));
        }

        static void SetButtonLabel(Button button,string value)
        {
            var label=button.GetComponentInChildren<Text>(true);
            if(label)label.text=value;
        }

        static string CurrentQualityName(){var names=QualitySettings.names;return names.Length==0?"DEFAULT":names[MenuSettingsAudioMockup.GraphicsQualityIndex];}

        static void ApplyResolution(int index)
        {
            var resolution=Resolutions[Mathf.Clamp(index,0,Resolutions.Length-1)];
            Screen.SetResolution(resolution.x,resolution.y,Screen.fullScreenMode);
        }

        static void AddSlider(Transform parent,Font font,string title,Vector2 position,float value,float min,float max,bool whole,Action<float> changed,Func<float,string> format)
        {
            string key=SettingKey(title);
            var titleText=AddText(parent,font,title,position+new Vector2(-310,28),new Vector2(430,30),20,TextAnchor.MiddleLeft,new Color(.88f,.93f,1));
            titleText.gameObject.name=key+"Title";
            Text readout=AddText(parent,font,format(value),position+new Vector2(295,28),new Vector2(180,30),20,TextAnchor.MiddleRight,new Color(.45f,.88f,1));
            readout.gameObject.name=key+"Value";
            var go=new GameObject(key+"Slider",typeof(RectTransform),typeof(Slider));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=position+new Vector2(0,-6);rect.sizeDelta=new Vector2(620,24);
            var track=NewImage("Track",go.transform,new Color(.12f,.18f,.29f,1));
            track.raycastTarget=true;
            track.rectTransform.anchorMin=new Vector2(0,.35f);track.rectTransform.anchorMax=new Vector2(1,.65f);track.rectTransform.offsetMin=track.rectTransform.offsetMax=Vector2.zero;
            var fill=NewImage("Fill",track.transform,new Color(.14f,.72f,.95f,1));
            fill.rectTransform.anchorMin=Vector2.zero;fill.rectTransform.anchorMax=Vector2.one;fill.rectTransform.offsetMin=fill.rectTransform.offsetMax=Vector2.zero;
            var handle=NewImage("Handle",go.transform,new Color(.9f,.98f,1,1));handle.raycastTarget=true;handle.rectTransform.sizeDelta=new Vector2(28,28);
            var slider=go.GetComponent<Slider>();slider.minValue=min;slider.maxValue=max;slider.wholeNumbers=whole;slider.direction=Slider.Direction.LeftToRight;
            slider.fillRect=fill.rectTransform;slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;slider.value=value;
            slider.onValueChanged.AddListener(v=>{readout.text=format(v);changed(v);});
        }

        static Image NewImage(string name,Transform parent,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }

        static Text AddText(Transform parent,Font font,string value,Vector2 position,Vector2 size,int fontSize,TextAnchor align,Color color)
        {
            var go=new GameObject(value+"Label",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;
            var text=go.GetComponent<Text>();text.font=font;text.text=value;text.fontSize=fontSize;text.alignment=align;text.color=color;text.raycastTarget=false;return text;
        }

        static Text AddButton(Transform parent,Font font,string title,Vector2 position,Action clicked,float width=220,string objectName=null)
        {
            var go=new GameObject(string.IsNullOrEmpty(objectName)?title+"Button":objectName,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=new Vector2(width,56);
            go.GetComponent<Image>().color=new Color(.12f,.58f,.82f,1);
            var button=go.GetComponent<Button>();button.onClick.AddListener(()=>clicked());
            return AddText(go.transform,font,title,Vector2.zero,new Vector2(width-10,48),22,TextAnchor.MiddleCenter,Color.white);
        }

        static string SettingKey(string title)
        {
            var key=new System.Text.StringBuilder(title.Length);
            foreach(char character in title)if(char.IsLetterOrDigit(character))key.Append(char.ToUpperInvariant(character));
            return key.ToString();
        }
    }
}
