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
            if (continueButton) continueButton.SetActive(File.Exists(SavePath));
            if (bestScore) bestScore.text = "BEST SCORE: " + PlayerPrefs.GetInt("ReflectableBest", 0);
            ShowMainMenu();
            if (group) StartCoroutine(Fade());
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
            if (!stageSelectPanel || !stageSelectPanel.activeInHierarchy || transitioning || Keyboard.current == null) return;
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame) SelectPrevious();
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame) SelectNext();
        }

        public void OpenStageSelect()
        {
            if (mainMenuPanel) mainMenuPanel.SetActive(false);
            if (settingsPanel) settingsPanel.SetActive(false);
            if (stageSelectPanel) stageSelectPanel.SetActive(true);
            carouselStage=1;RefreshCarousel();
        }

        public void ShowMainMenu()
        {
            if (mainMenuPanel) mainMenuPanel.SetActive(true);
            if (stageSelectPanel) stageSelectPanel.SetActive(false);
            if (settingsPanel) settingsPanel.SetActive(false);
        }

        public void SelectStage(int stage)
        {
            carouselStage=Mathf.Clamp(stage,1,5);PlaySelected();
        }

        public void SelectPrevious(){if(!transitioning&&carouselStage>1)StartCoroutine(Slide(-1));}
        public void SelectNext(){if(!transitioning&&carouselStage<5)StartCoroutine(Slide(1));}
        public void PlaySelected(){if(!ReflectableStageSession.IsUnlocked(carouselStage))return;ReflectableStageSession.SelectedStage=carouselStage;SceneManager.LoadScene("Game");}
        IEnumerator Slide(int direction){if(!HasCarouselReferences())yield break;transitioning=true;var selectedRect=selectedIsland.rectTransform;var start=selectedRect.anchoredPosition;for(float t=0;t<.14f;t+=Time.unscaledDeltaTime){selectedRect.anchoredPosition=Vector2.Lerp(start,start+Vector2.left*direction*130,t/.14f);yield return null;}carouselStage+=direction;RefreshCarousel();for(float t=0;t<.14f;t+=Time.unscaledDeltaTime){selectedRect.anchoredPosition=Vector2.Lerp(start+Vector2.right*direction*130,start,t/.14f);yield return null;}selectedRect.anchoredPosition=start;transitioning=false;}
        void RefreshCarousel(){if(!HasCarouselReferences())return;SetSlot(previousIsland,previousGroup,previousLabel,carouselStage-1);SetSlot(selectedIsland,selectedGroup,selectedLabel,carouselStage);SetSlot(nextIsland,nextGroup,nextLabel,carouselStage+1);var info=ReflectableStageSession.GetPresentation(carouselStage);stageNumber.text="STAGE "+carouselStage;stageName.text=info.Name;difficulty.text="Difficulty: "+info.Difficulty;requirement.text="Destroy "+ReflectableStageSession.ClearRequirement(carouselStage)+" Blocks";bestScoreText.text="Best Score: "+PlayerPrefs.GetInt("ReflectableStage"+carouselStage+"Best",0);bool unlocked=ReflectableStageSession.IsUnlocked(carouselStage);status.text=unlocked?"UNLOCKED":"LOCKED\nClear Stage "+(carouselStage-1)+" to unlock.";description.text=info.Description;playButton.interactable=unlocked;leftButton.interactable=carouselStage>1;rightButton.interactable=carouselStage<5;}
        bool HasCarouselReferences(){bool valid=previousIsland&&selectedIsland&&nextIsland&&previousGroup&&selectedGroup&&nextGroup&&previousLabel&&selectedLabel&&nextLabel&&stageNumber&&stageName&&difficulty&&requirement&&bestScoreText&&status&&description&&leftButton&&rightButton&&playButton;if(!valid&&!carouselReferenceErrorReported){carouselReferenceErrorReported=true;Debug.LogError("ReflectableMenuController: Stage carousel references are incomplete. Rebuild MainMenu to assign the carousel slots.",this);}return valid;}
        void SetSlot(Image image,CanvasGroup canvasGroup,Text label,int stage){bool visible=stage>=1&&stage<=5;image.gameObject.SetActive(visible);if(!visible)return;var info=ReflectableStageSession.GetPresentation(stage);image.color=info.Theme;label.text="STAGE "+stage+"\n"+info.Name+(ReflectableStageSession.IsUnlocked(stage)?"":"\nLOCKED");canvasGroup.alpha=stage==carouselStage?1f:.55f;image.rectTransform.localScale=stage==carouselStage?Vector3.one:Vector3.one*.68f;}

        public void Continue()
        {
            PlayerPrefs.SetInt("ReflectableContinue", 1);
            SceneManager.LoadScene("Game");
        }

        public void ToggleSettings()
        {
            if (settingsPanel) settingsPanel.SetActive(!settingsPanel.activeSelf);
            if (settingsPanel && settingsPanel.activeSelf && mainMenuPanel) mainMenuPanel.SetActive(false);
        }

        public void Quit() => Application.Quit();

    }
}
