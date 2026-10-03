using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Reflectable
{
    public sealed class ReflectableMenuController : MonoBehaviour
    {
        [SerializeField] GameObject continueButton;
        [SerializeField] GameObject mainMenuPanel;
        [SerializeField] GameObject stageSelectPanel;
        [SerializeField] GameObject settingsPanel;
        FloatingIslandMenuNavigator islandNavigator;
        Image stageSelectBackground;
        Image lockedStageOverlay;
        GameObject menuBackdrop, activeStageBackground;
        SpriteRenderer stageBackgroundRenderer;
        Vector3 stageBackgroundBaseScale;
        Vector2 stageBackgroundBaseSize;
        int lastBackgroundWidth, lastBackgroundHeight;
        bool menuBackdropWasActive;
        const float LockedStageOverlayAlpha = .9f;
        [SerializeField] Text bestScore;
        [SerializeField] CanvasGroup group;
        [Header("Stage carousel")][SerializeField] Image previousIsland, selectedIsland, nextIsland;
        [SerializeField] CanvasGroup previousGroup, selectedGroup, nextGroup;
        [SerializeField] Text previousLabel, selectedLabel, nextLabel, stageNumber, stageName, difficulty, requirement, bestScoreText, status, description;
        [SerializeField] Button leftButton, rightButton, playButton;
        int carouselStage=1; bool transitioning, carouselReferenceErrorReported;

        string SavePath => Path.Combine(Application.persistentDataPath, "reflectable_run.json");

        void Start()
        {
            Time.timeScale = 1f;
            islandNavigator = FindFirstObjectByType<FloatingIslandMenuNavigator>();
            if (continueButton) continueButton.SetActive(true);
            if (bestScore) bestScore.text = "BEST SCORE: " + PlayerPrefs.GetInt("ReflectableBest", 0);
            BindSceneButtons();
            stageSelectBackground = stageSelectPanel ? stageSelectPanel.GetComponent<Image>() : null;
            menuBackdrop = GameObject.Find("Background");
            menuBackdropWasActive = menuBackdrop && menuBackdrop.activeSelf;
            EnsureLockedStageOverlay();
            ShowMainMenu();
            RefreshBestScore();
            if (group) StartCoroutine(Fade());
        }

        void OnEnable() => RefreshBestScore();

        void RefreshBestScore()
        {
            if (bestScore) bestScore.text = "BEST SCORE: " + PlayerPrefs.GetInt("ReflectableBest", 0);
        }

        void BindSceneButtons()
        {
            BindButton(FindButton(mainMenuPanel, "PlayGameButton"), OpenStageSelect);
            BindButton(continueButton ? continueButton.GetComponent<Button>() : FindButton(mainMenuPanel, "ResumeButton"), Continue);
            BindButton(FindButton(mainMenuPanel, "SettingsButton"), ToggleSettings);
            BindButton(FindButton(mainMenuPanel, "ExitButton"), Quit);

            if (!stageSelectPanel) return;
            if (!leftButton) leftButton = FindButton(stageSelectPanel, "LeftArrow");
            if (!rightButton) rightButton = FindButton(stageSelectPanel, "RightArrow");
            if (!playButton) playButton = FindButton(stageSelectPanel, "PlayButton");
            BindButton(leftButton, SelectPrevious);
            BindButton(rightButton, SelectNext);
            BindButton(playButton, PlaySelected);
            foreach (var button in stageSelectPanel.GetComponentsInChildren<Button>(true))
                if (button && button.gameObject.name == "BackButton") BindButton(button, ShowMainMenu);
        }

        static Button FindButton(GameObject root, string buttonName)
        {
            if (!root) return null;
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                if (button && button.gameObject.name == buttonName) return button;
            return null;
        }

        static void BindButton(Button button, UnityEngine.Events.UnityAction handler)
        {
            if (!button || handler == null) return;

            // Keep existing Hierarchy-authored callbacks and only provide a
            // runtime fallback when that exact callback is not serialized.
            UnityEngine.Object handlerTarget = handler.Target as UnityEngine.Object;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) == handlerTarget &&
                    button.onClick.GetPersistentMethodName(i) == handler.Method.Name)
                    return;
            }

            button.onClick.AddListener(handler);
        }

        IEnumerator Fade()
        {
            group.alpha = 0;
            for (float t = 0; t < .5f; t += Time.unscaledDeltaTime)
            {
                group.alpha = t / .5f;
                yield return null;
            }
            group.alpha = 1;
        }

        void Update()
        {
            if (stageSelectPanel && stageSelectPanel.activeInHierarchy) FitStageSelectionBackground();
            if (!stageSelectPanel || !stageSelectPanel.activeInHierarchy || transitioning || Keyboard.current == null) return;
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame) SelectPrevious();
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame) SelectNext();
        }

        public void OpenStageSelect()
        {
            if (islandNavigator && islandNavigator.NavigateTo(MenuDestination.Play)) return;
            OpenStageSelectAfterTravel();
        }

        public void OpenStageSelectAfterTravel()
        {
            if (mainMenuPanel) mainMenuPanel.SetActive(false);
            if (settingsPanel) settingsPanel.SetActive(false);
            if (stageSelectPanel) stageSelectPanel.SetActive(true);
            if (menuBackdrop) menuBackdrop.SetActive(false);
            if (islandNavigator) islandNavigator.SetMenuIslandsVisible(false);
            carouselStage=1;RefreshCarousel();
        }

        public void ShowMainMenu()
        {
            if (islandNavigator && islandNavigator.ReturnToMainMenu()) return;
            ShowMainMenuAfterTravel();
        }

        public void ShowMainMenuAfterTravel()
        {
            if (mainMenuPanel) mainMenuPanel.SetActive(true);
            if (stageSelectPanel) stageSelectPanel.SetActive(false);
            if (settingsPanel) settingsPanel.SetActive(false);
            if (activeStageBackground) Destroy(activeStageBackground);
            activeStageBackground = null;
            stageBackgroundRenderer = null;
            if (islandNavigator) islandNavigator.SetMenuIslandsVisible(true);
            if (menuBackdrop) menuBackdrop.SetActive(menuBackdropWasActive);
            if (stageSelectBackground) stageSelectBackground.color = Color.white;
        }

        public void PrepareForReturnTravel()
        {
            if (mainMenuPanel) mainMenuPanel.SetActive(false);
            if (stageSelectPanel) stageSelectPanel.SetActive(false);
            if (settingsPanel) settingsPanel.SetActive(false);
            if (activeStageBackground) Destroy(activeStageBackground);
            activeStageBackground = null;
            stageBackgroundRenderer = null;
            if (islandNavigator) islandNavigator.SetMenuIslandsVisible(true);
            if (menuBackdrop) menuBackdrop.SetActive(menuBackdropWasActive);
            if (stageSelectBackground) stageSelectBackground.color = Color.white;
        }

        public void OpenSettingsAfterTravel()
        {
            if (mainMenuPanel) mainMenuPanel.SetActive(false);
            if (stageSelectPanel) stageSelectPanel.SetActive(false);
            if (settingsPanel) settingsPanel.SetActive(true);
        }

        public void ReturnFromSettings() => ShowMainMenu();

        public void SelectStage(int stage)
        {
            carouselStage=Mathf.Clamp(stage,1,ReflectableStageConfig.StageCount);PlaySelected();
        }

        public void SelectPrevious(){if(!transitioning&&carouselStage>1)StartCoroutine(Slide(-1));}
        public void SelectNext(){if(!transitioning&&carouselStage<ReflectableStageConfig.StageCount)StartCoroutine(Slide(1));}
        public void PlaySelected(){if(!ReflectableStageSession.IsUnlocked(carouselStage))return;ReflectableStageSession.SelectedStage=carouselStage;SceneManager.LoadScene("Game");}
        IEnumerator Slide(int direction){if(!HasCarouselReferences())yield break;transitioning=true;var selectedRect=selectedIsland.rectTransform;var start=selectedRect.anchoredPosition;for(float t=0;t<.14f;t+=Time.unscaledDeltaTime){selectedRect.anchoredPosition=Vector2.Lerp(start,start+Vector2.left*direction*130,t/.14f);yield return null;}carouselStage+=direction;RefreshCarousel();for(float t=0;t<.14f;t+=Time.unscaledDeltaTime){selectedRect.anchoredPosition=Vector2.Lerp(start+Vector2.right*direction*130,start,t/.14f);yield return null;}selectedRect.anchoredPosition=start;transitioning=false;}
        void RefreshCarousel(){if(!HasCarouselReferences())return;SetSlot(previousIsland,previousGroup,previousLabel,carouselStage-1);SetSlot(selectedIsland,selectedGroup,selectedLabel,carouselStage);SetSlot(nextIsland,nextGroup,nextLabel,carouselStage+1);var info=ReflectableStageSession.GetPresentation(carouselStage);var selectedData=ReflectableStageConfig.DataFor(carouselStage);RefreshStageBackground(selectedData);bool unlocked=ReflectableStageSession.IsUnlocked(carouselStage);if(lockedStageOverlay)lockedStageOverlay.color=new Color(0f,0f,0f,unlocked?0f:LockedStageOverlayAlpha);stageNumber.text="STAGE "+carouselStage;stageName.text=info.Name;difficulty.text="Difficulty: "+info.Difficulty;requirement.text="Destroy "+ReflectableStageSession.ClearRequirement(carouselStage)+" Blocks";bestScoreText.text="Best Score: "+PlayerPrefs.GetInt("ReflectableStage"+carouselStage+"Best",0);status.text=unlocked?"UNLOCKED":"LOCKED\nClear Stage "+(carouselStage-1)+" to unlock.";description.text=info.Description;playButton.interactable=unlocked;leftButton.interactable=carouselStage>1;rightButton.interactable=carouselStage<ReflectableStageConfig.StageCount;}
        void RefreshStageBackground(ReflectableStageData data){if(activeStageBackground)Destroy(activeStageBackground);var camera=Camera.main;Vector3 position=camera?new Vector3(camera.transform.position.x,camera.transform.position.y,0f):Vector3.zero;activeStageBackground=data&&data.stageVisualPrefab?Instantiate(data.stageVisualPrefab,position,Quaternion.identity):null;stageBackgroundRenderer=null;if(activeStageBackground){float largestArea=0f;foreach(var renderer in activeStageBackground.GetComponentsInChildren<SpriteRenderer>(true)){if(!renderer||!renderer.sprite)continue;float area=renderer.bounds.size.x*renderer.bounds.size.y;if(area>largestArea){largestArea=area;stageBackgroundRenderer=renderer;}}if(stageBackgroundRenderer){stageBackgroundBaseScale=stageBackgroundRenderer.transform.localScale;stageBackgroundBaseSize=stageBackgroundRenderer.size;}}lastBackgroundWidth=lastBackgroundHeight=0;FitStageSelectionBackground();if(stageSelectBackground)stageSelectBackground.color=new Color(1f,1f,1f,0f);if(data&&!data.stageVisualPrefab)Debug.LogWarning("ReflectableMenuController: Stage "+carouselStage+" has no stage visual prefab for the selection background.",this);}
        void FitStageSelectionBackground(){var camera=Camera.main;if(!camera||!camera.orthographic||!stageBackgroundRenderer||!stageBackgroundRenderer.sprite||Screen.width<=0||Screen.height<=0)return;if(lastBackgroundWidth==Screen.width&&lastBackgroundHeight==Screen.height&&Mathf.Abs(camera.aspect-Screen.width/(float)Screen.height)<.001f)return;lastBackgroundWidth=Screen.width;lastBackgroundHeight=Screen.height;float viewHeight=camera.orthographicSize*2f;float viewWidth=viewHeight*camera.aspect;var spriteSize=stageBackgroundRenderer.sprite.bounds.size;if(stageBackgroundRenderer.drawMode==SpriteDrawMode.Tiled){float sx=Mathf.Max(.001f,Mathf.Abs(stageBackgroundRenderer.transform.lossyScale.x));float sy=Mathf.Max(.001f,Mathf.Abs(stageBackgroundRenderer.transform.lossyScale.y));stageBackgroundRenderer.size=new Vector2(Mathf.Max(stageBackgroundBaseSize.x,viewWidth/sx),Mathf.Max(stageBackgroundBaseSize.y,viewHeight/sy));}else if(spriteSize.x>0f&&spriteSize.y>0f){float cover=Mathf.Max(1f,viewWidth/spriteSize.x,viewHeight/spriteSize.y);stageBackgroundRenderer.transform.localScale=stageBackgroundBaseScale*cover;}}
        void EnsureLockedStageOverlay(){if(!stageSelectPanel)return;var existing=stageSelectPanel.transform.Find("LockedStageOverlay");if(existing)lockedStageOverlay=existing.GetComponent<Image>();if(!lockedStageOverlay){var overlay=new GameObject("LockedStageOverlay",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));overlay.transform.SetParent(stageSelectPanel.transform,false);lockedStageOverlay=overlay.GetComponent<Image>();}var rect=lockedStageOverlay.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;lockedStageOverlay.sprite=null;lockedStageOverlay.color=Color.clear;lockedStageOverlay.raycastTarget=false;rect.SetAsFirstSibling();}
        bool HasCarouselReferences(){bool valid=previousIsland&&selectedIsland&&nextIsland&&previousGroup&&selectedGroup&&nextGroup&&previousLabel&&selectedLabel&&nextLabel&&stageNumber&&stageName&&difficulty&&requirement&&bestScoreText&&status&&description&&leftButton&&rightButton&&playButton;if(!valid&&!carouselReferenceErrorReported){carouselReferenceErrorReported=true;Debug.LogError("ReflectableMenuController: Stage carousel references are incomplete. Rebuild MainMenu to assign the carousel slots.",this);}return valid;}
        static Sprite PreviewSprite(ReflectableStageData data,Sprite fallback){if(data&&data.stageSelectPreviewPrefab){var image=data.stageSelectPreviewPrefab.GetComponent<Image>();if(image&&image.sprite)return image.sprite;}return data&&data.stageSelectPreview?data.stageSelectPreview:fallback;}
        void SetSlot(Image image,CanvasGroup canvasGroup,Text label,int stage){bool visible=stage>=1&&stage<=ReflectableStageConfig.StageCount;image.gameObject.SetActive(visible);if(!visible)return;var info=ReflectableStageSession.GetPresentation(stage);var data=ReflectableStageConfig.DataFor(stage);Sprite preview=PreviewSprite(data,info.Preview);if(preview)image.sprite=preview;bool unlocked=ReflectableStageSession.IsUnlocked(stage);image.color=unlocked?Color.white:Color.black;label.text="STAGE "+stage+"\n"+info.Name+(unlocked?"":"\nLOCKED");canvasGroup.alpha=stage==carouselStage?1f:.55f;image.rectTransform.localScale=stage==carouselStage?Vector3.one:Vector3.one*.68f;}

        public void Continue()
        {
            if (islandNavigator && islandNavigator.NavigateTo(MenuDestination.Continue)) return;
            ContinueRunNow();
        }

        public void ContinueRunNow()
        {
            if (!File.Exists(SavePath) && !File.Exists(SavePath + ".bak")) return;
            PlayerPrefs.SetInt("ReflectableContinue", 1);
            PlayerPrefs.Save();
            SceneManager.LoadScene("Game");
        }

        public void ToggleSettings()
        {
            if (islandNavigator && islandNavigator.NavigateTo(MenuDestination.Settings)) return;
            OpenSettingsAfterTravel();
        }

        public void Quit()
        {
            if (islandNavigator && islandNavigator.NavigateTo(MenuDestination.Exit)) return;
            Application.Quit();
        }

        public GameObject MainMenuPanel => mainMenuPanel;
        public GameObject StageSelectPanel => stageSelectPanel;
        public GameObject SettingsPanel => settingsPanel;

    }
}
