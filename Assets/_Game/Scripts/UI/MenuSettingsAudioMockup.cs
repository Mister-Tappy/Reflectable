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
        static MenuSettingsAudioMockup instance;
        AudioSource musicSource;
        AudioSource sfxSource;
        AudioClip impactClip, shotClip, comboClip, buttonClip;

        public static float MusicVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, .65f));
        public static float SfxVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, .8f));
        public static float ScreenShakeIntensity => Mathf.Clamp01(PlayerPrefs.GetFloat(ShakeKey, .65f));

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
            musicSource.clip = CreateClip("Mockup_MenuMusic", 12f, true, (t,d) =>
            {
                float[] roots={130.81f,110f,174.61f,146.83f};
                float root=roots[Mathf.FloorToInt(t/3f)%4];
                float pad=Mathf.Sin(2*Mathf.PI*root*t)*.42f+Mathf.Sin(2*Mathf.PI*root*1.25f*t)*.22f+Mathf.Sin(2*Mathf.PI*root*1.5f*t)*.17f;
                float beat=t%1.5f;
                float pluck=Mathf.Exp(-beat*3.5f)*Mathf.Sin(2*Mathf.PI*root*4f*t)*.14f;
                return (pad+pluck)*Mathf.Min(1f,Mathf.Min(t*2f,(d-t)*2f))*.22f;
            });
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
            musicSource.volume=MusicVolume*.42f;
            if(menu&&!musicSource.isPlaying)musicSource.Play();
            else if(!menu&&musicSource.isPlaying)musicSource.Stop();

            GameObject settings=null,main=null;
            foreach(var root in scene.GetRootGameObjects())
                foreach(var item in root.GetComponentsInChildren<Transform>(true))
                {
                    if(item.name=="SettingsPanel")settings=item.gameObject;
                    else if(item.name=="MainMenuPanel")main=item.gameObject;
                }
            if(menu&&settings)
            {
                var ui=settings.GetComponent<MenuSettingsPanel>();
                if(!ui)ui=settings.AddComponent<MenuSettingsPanel>();
                ui.Build(main);
            }
            if(menu)
                foreach(var root in scene.GetRootGameObjects())
                    foreach(var button in root.GetComponentsInChildren<Button>(true))
                        button.onClick.AddListener(PlayButton);
        }

        public static void PlayImpact()=>Play(instance?instance.impactClip:null,.72f);
        public static void PlayBallShot()=>Play(instance?instance.shotClip:null,.55f);
        public static void PlayHighCombo()=>Play(instance?instance.comboClip:null,.85f);
        public static void PlayButton()=>Play(instance?instance.buttonClip:null,.55f);
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
            if(built)return;
            built=true;mainPanel=mainMenuPanel;
            Font font=Resources.GetBuiltinResource<Font>("Arial.ttf");
            var card=new GameObject("SettingsCard",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            card.transform.SetParent(transform,false);
            var cardRect=card.GetComponent<RectTransform>();
            cardRect.anchorMin=cardRect.anchorMax=new Vector2(.5f,.5f);
            cardRect.anchoredPosition=Vector2.zero;cardRect.sizeDelta=new Vector2(780,660);
            card.GetComponent<Image>().color=new Color(.035f,.055f,.11f,.97f);
            AddText(card.transform,font,"SETTINGS",new Vector2(0,266),new Vector2(700,52),36,TextAnchor.MiddleCenter,Color.white);
            AddText(card.transform,font,"DISPLAY  ·  AUDIO  ·  GAME FEEL",new Vector2(0,224),new Vector2(700,32),17,TextAnchor.MiddleCenter,new Color(.55f,.87f,1));

            int resolution=Mathf.Clamp(PlayerPrefs.GetInt("Reflectable.ResolutionIndex",2),0,Resolutions.Length-1);
            AddSlider(card.transform,font,"DISPLAY RESOLUTION",new Vector2(0,155),resolution,0,Resolutions.Length-1,true,
                value=>
                {
                    int i=Mathf.Clamp(Mathf.RoundToInt(value),0,Resolutions.Length-1);
                    PlayerPrefs.SetInt("Reflectable.ResolutionIndex",i);PlayerPrefs.Save();
                    Screen.SetResolution(Resolutions[i].x,Resolutions[i].y,Screen.fullScreenMode);
                },
                value=>{var r=Resolutions[Mathf.Clamp(Mathf.RoundToInt(value),0,Resolutions.Length-1)];return r.x+" × "+r.y;});
            AddSlider(card.transform,font,"MUSIC VOLUME",new Vector2(0,42),MenuSettingsAudioMockup.MusicVolume,0,1,false,
                MenuSettingsAudioMockup.SetMusicVolume,v=>Mathf.RoundToInt(v*100)+"%");
            AddSlider(card.transform,font,"SFX VOLUME",new Vector2(0,-70),MenuSettingsAudioMockup.SfxVolume,0,1,false,
                MenuSettingsAudioMockup.SetSfxVolume,v=>Mathf.RoundToInt(v*100)+"%");
            AddSlider(card.transform,font,"SCREEN SHAKE INTENSITY",new Vector2(0,-182),MenuSettingsAudioMockup.ScreenShakeIntensity,0,1,false,
                MenuSettingsAudioMockup.SetScreenShakeIntensity,v=>Mathf.RoundToInt(v*100)+"%");
            AddButton(card.transform,font,"DONE",new Vector2(0,-278),()=>
            {
                gameObject.SetActive(false);
                if(mainPanel)mainPanel.SetActive(true);
            });
        }

        static void AddSlider(Transform parent,Font font,string title,Vector2 position,float value,float min,float max,bool whole,Action<float> changed,Func<float,string> format)
        {
            AddText(parent,font,title,position+new Vector2(-310,28),new Vector2(430,30),20,TextAnchor.MiddleLeft,new Color(.88f,.93f,1));
            Text readout=AddText(parent,font,format(value),position+new Vector2(295,28),new Vector2(180,30),20,TextAnchor.MiddleRight,new Color(.45f,.88f,1));
            var go=new GameObject(title+"Slider",typeof(RectTransform),typeof(Slider));go.transform.SetParent(parent,false);
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

        static void AddButton(Transform parent,Font font,string title,Vector2 position,Action clicked)
        {
            var go=new GameObject(title+"Button",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=new Vector2(220,56);
            go.GetComponent<Image>().color=new Color(.12f,.58f,.82f,1);
            var button=go.GetComponent<Button>();button.onClick.AddListener(()=>clicked());
            AddText(go.transform,font,title,Vector2.zero,new Vector2(210,48),22,TextAnchor.MiddleCenter,Color.white);
        }
    }
}
