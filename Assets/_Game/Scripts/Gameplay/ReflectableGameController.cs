using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Reflectable
{
    public enum ReflectableBlockType { Normal, Gem, Bomb }
    [Serializable] public class ReflectableCellSave { public int row, column, hp, maxHp; public ReflectableBlockType type; }
    [Serializable] public class ReflectableRunSave { public bool valid; public int turn, hp, maxHp, score, gems, level, exp, skillPoints, power, ricochet, extraBall, maxHpUpgrade, gachaCost, maxCombo, destroyed, ricochets; public string character; public int rank; public List<ReflectableCellSave> blocks = new List<ReflectableCellSave>(); }
    public sealed class ReflectableGameController : MonoBehaviour
    {
        [Header("Scene references")][SerializeField] Transform player, firePoint, gridOrigin, blocksRoot, projectilesRoot; [SerializeField] LineRenderer aimPreview;
        [SerializeField] GameObject normalBlockPrefab, gemBlockPrefab, bombBlockPrefab, projectilePrefab;
        [Header("Arena")][SerializeField] float left = -7f, right = 7f, ceiling = 5.2f, bottom = -5.4f;
        [Header("Grid")][SerializeField, Min(1)] int columns = 7, rows = 10; [SerializeField] Vector2 cellSpacing = new Vector2(1.85f, 1.22f);
        [Header("UI")][SerializeField] Text hpText, scoreText, gemsText, turnText, comboText, levelText, characterText, gameOverText; [SerializeField] GameObject pausePanel, gameOverPanel, upgradePanel, characterPanel, stageIntro; [SerializeField] Button powerButton, ricochetButton, extraBallButton, maxHpButton, characterButton, skipTurnButton; [SerializeField] PlayerCharacterPresenter characterPresenter;
        [Header("Balance")][SerializeField] float projectileSpeed = 12f; [SerializeField] int baseDamage = 5; [SerializeField] float launchSpacing = .09f; [SerializeField] GameplayFeedbackManager feedback;
        enum TurnState { Aiming, Firing, Resolving, Paused, GameOver }
        readonly List<ReflectableBlockView> blocks = new List<ReflectableBlockView>();
        readonly HashSet<int> activeProjectileIds = new HashSet<int>();
        int turn,hp,maxHp,score,gems,level,exp,skillPoints,power,ricochet,extraBall,maxHpUpgrade,gachaCost,combo,maxCombo,destroyed,ricochets,rank,stage; string character="MIMI"; bool aiming,ending,gameOver,turnFired,stageCleared,skippingTurn; int activeProjectiles; TurnState state, pausedState;
        string SavePath => Path.Combine(Application.persistentDataPath,"reflectable_run.json");
        public void Configure(Transform scenePlayer,Transform sceneFirePoint,Transform sceneGridOrigin,Transform sceneBlocksRoot,Transform sceneProjectilesRoot,LineRenderer scenePreview,GameObject normal,GameObject gem,GameObject bomb,GameObject projectile,Text hpLabel,Text scoreLabel,Text gemLabel,Text turnLabel,Text comboLabel,Text levelLabel,Text characterLabel,Text overLabel,GameObject pause,GameObject over,GameObject upgrades,GameObject characters){player=scenePlayer;firePoint=sceneFirePoint;gridOrigin=sceneGridOrigin;blocksRoot=sceneBlocksRoot;projectilesRoot=sceneProjectilesRoot;aimPreview=scenePreview;normalBlockPrefab=normal;gemBlockPrefab=gem;bombBlockPrefab=bomb;projectilePrefab=projectile;hpText=hpLabel;scoreText=scoreLabel;gemsText=gemLabel;turnText=turnLabel;comboText=comboLabel;levelText=levelLabel;characterText=characterLabel;gameOverText=overLabel;pausePanel=pause;gameOverPanel=over;upgradePanel=upgrades;characterPanel=characters;}
        void Start(){Application.targetFrameRate=120;EnsureSkipTurnButton();if(pausePanel)pausePanel.SetActive(false);if(gameOverPanel)gameOverPanel.SetActive(false);if(upgradePanel)upgradePanel.SetActive(false);if(characterPanel)characterPanel.SetActive(false);if(PlayerPrefs.GetInt("ReflectableContinue",0)==1){PlayerPrefs.DeleteKey("ReflectableContinue");ContinueGame();}else StartNewGame();}
        void EnsureSkipTurnButton(){var parent=characterButton&&characterButton.transform.parent&&characterButton.transform.parent.parent?characterButton.transform.parent.parent.parent as RectTransform:null;if(!parent)return;if(skipTurnButton){skipTurnButton.transform.SetParent(parent,false);var existingRect=skipTurnButton.GetComponent<RectTransform>();existingRect.anchorMin=existingRect.anchorMax=new Vector2(1,0);existingRect.pivot=new Vector2(1,0);existingRect.anchoredPosition=new Vector2(-28,28);existingRect.sizeDelta=new Vector2(150,64);if(!skipTurnButton.GetComponent<ButtonJuice>())skipTurnButton.gameObject.AddComponent<ButtonJuice>();return;}var go=new GameObject("SkipTurnButton",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button));go.layer=parent.gameObject.layer;var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(1,0);rect.pivot=new Vector2(1,0);rect.anchoredPosition=new Vector2(-28,28);rect.sizeDelta=new Vector2(150,64);var image=go.GetComponent<Image>();image.color=new Color(.784f,.714f,.91f,1f);skipTurnButton=go.GetComponent<Button>();skipTurnButton.targetGraphic=image;skipTurnButton.onClick.AddListener(SkipTurn);go.AddComponent<ButtonJuice>();var labelGo=new GameObject("Label",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));labelGo.layer=go.layer;var labelRect=labelGo.GetComponent<RectTransform>();labelRect.SetParent(rect,false);labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;var label=labelGo.GetComponent<Text>();var sample=characterButton?characterButton.GetComponentInChildren<Text>():null;label.font=sample?sample.font:Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=18;label.alignment=TextAnchor.MiddleCenter;label.color=sample?sample.color:new Color(.28f,.25f,.42f,1f);label.text="SKIP\nTURN";Debug.Log("[Turn] Created runtime Skip Turn button");}
        void Update(){if(gameOver)return;var mouse=Mouse.current; if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)TogglePause();if(!GameplayInputEnabled||mouse==null)return;Vector3 world=Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());Vector2 dir=(Vector2)(world-firePoint.position);dir.y=Mathf.Max(.2f,dir.y);dir.Normalize();DrawPreview(dir);if(mouse.leftButton.wasPressedThisFrame&&!PointerOverUI())StartCoroutine(Fire(dir));}
        bool GameplayInputEnabled=>state==TurnState.Aiming&&aiming&&Time.timeScale>0&&!AnyModalOpen();
        bool AnyModalOpen()=>(pausePanel&&pausePanel.activeInHierarchy)||(gameOverPanel&&gameOverPanel.activeInHierarchy)||(upgradePanel&&upgradePanel.activeInHierarchy)||(characterPanel&&characterPanel.activeInHierarchy);
        static bool PointerOverUI()=>EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject();
        public void StartNewGame(){StopAllCoroutines();ResolveActiveProjectiles(false);ClearBlocks();turn=0;hp=maxHp=100;score=gems=exp=skillPoints=power=ricochet=extraBall=maxHpUpgrade=combo=maxCombo=destroyed=ricochets=0;level=rank=1;character="MIMI";characterPresenter?.Present(character.ToLower());gachaCost=2;gameOver=false;stageCleared=false;aiming=false;if(gameOverPanel)gameOverPanel.SetActive(false);stage=ReflectableStageSession.ResolveSelectedStage();SpawnStartingBlocks(stage);StartCoroutine(StageIntroRoutine());}
        public void ContinueGame(){if(!File.Exists(SavePath)){StartNewGame();return;}try{var d=JsonUtility.FromJson<ReflectableRunSave>(File.ReadAllText(SavePath));if(d==null||!d.valid)throw new Exception();Restore(d);}catch{StartNewGame();}}
        void Restore(ReflectableRunSave d){ClearBlocks();turn=d.turn;hp=d.hp;maxHp=d.maxHp;score=d.score;gems=d.gems;level=d.level;exp=d.exp;skillPoints=d.skillPoints;power=d.power;ricochet=d.ricochet;extraBall=d.extraBall;maxHpUpgrade=d.maxHpUpgrade;gachaCost=d.gachaCost;character=string.IsNullOrEmpty(d.character)?"MIMI":d.character;characterPresenter?.Present(character.ToLower());rank=d.rank;foreach(var c in d.blocks)CreateBlock(c.row,c.column,c.hp,c.maxHp,c.type);BeginTurn();}
        void BeginTurn(){turn++;combo=0;turnFired=false;activeProjectileIds.Clear();activeProjectiles=0;ending=false;state=TurnState.Aiming;aiming=true;if(aimPreview)aimPreview.enabled=true;RefreshUI();Debug.Log("[Turn] Ready Turn "+turn);}
        IEnumerator Fire(Vector2 direction){if(state!=TurnState.Aiming)yield break;aiming=false;state=TurnState.Firing;turnFired=true;if(aimPreview)aimPreview.enabled=false;StartCoroutine(PlayerRecoil());int count=1+extraBall+(character=="LUNE"?rank:0);activeProjectileIds.Clear();activeProjectiles=0;Debug.Log("[Turn] Shooting");for(int i=0;i<count&&state==TurnState.Firing&&turnFired;i++){var go=Instantiate(projectilePrefab,firePoint.position,Quaternion.identity,projectilesRoot);var projectile=go.GetComponent<ReflectableProjectile>();RegisterProjectile(projectile);projectile.Launch(this,direction,projectileSpeed,baseDamage+power*2);yield return new WaitForSeconds(launchSpacing);}Debug.Log("[Turn] Active projectiles: "+activeProjectiles);}
        void RegisterProjectile(ReflectableProjectile projectile){if(!projectile)return;activeProjectileIds.Add(projectile.GetInstanceID());activeProjectiles=activeProjectileIds.Count;Debug.Log("[Projectile] Registered ID "+projectile.GetInstanceID());}
        public bool IsFinalProjectile(ReflectableProjectile projectile)=>projectile&&activeProjectileIds.Count==1&&activeProjectileIds.Contains(projectile.GetInstanceID());
        public void ProjectileFinished(ReflectableProjectile projectile,float returnX){if(!projectile||!activeProjectileIds.Remove(projectile.GetInstanceID()))return;activeProjectiles=activeProjectileIds.Count;Debug.Log("[Projectile] Finished ID "+projectile.GetInstanceID()+" | [Turn] Active projectiles: "+activeProjectiles);if(!turnFired||state!=TurnState.Firing)return;StartCoroutine(MovePlayerToReturn(returnX));if(activeProjectiles==0&&!ending&&!gameOver){state=TurnState.Resolving;Debug.Log("[Turn] Completing Turn "+turn);StartCoroutine(EndTurn());}}
        IEnumerator EndTurn(){if(ending||state!=TurnState.Resolving)yield break;ending=true;Debug.Log("[Turn] Advancing board");yield return new WaitForSeconds(.35f);float moveDuration=Mathf.Lerp(.48f,.25f,Mathf.Clamp01(turn/80f));foreach(var b in blocks.ToArray()){if(!b)continue;b.Row++;StartCoroutine(b.MoveTo(CellPosition(b.Row,b.Column),moveDuration));}yield return new WaitForSeconds(moveDuration);foreach(var b in blocks.ToArray())if(b&&b.Row>=rows){hp-=b.HP;blocks.Remove(b);Destroy(b.gameObject);}if(hp<=0){GameOver();yield break;}SpawnRow(0);Save();ending=false;BeginTurn();}
        void SpawnRow(int row){var patterns=new[]{new[]{1,5},new[]{0,2,5},new[]{1,3,5},new[]{0,1,5,6},new[]{0,3,6},new[]{0,2,4,6}};var cols=patterns[UnityEngine.Random.Range(0,patterns.Length)];int allowed=Mathf.Clamp(2+turn/18,2,5);var used=new HashSet<int>();foreach(var col in cols){if(used.Count>=allowed)break;used.Add(col);}while(used.Count<Mathf.Min(allowed,cols.Length)&&UnityEngine.Random.value<.35f)used.Add(UnityEngine.Random.Range(0,columns));foreach(var col in used){float roll=UnityEngine.Random.value;var type=roll<.12f?ReflectableBlockType.Gem:roll<.22f?ReflectableBlockType.Bomb:ReflectableBlockType.Normal;int h=4+turn+UnityEngine.Random.Range(0,5);CreateBlock(row,col,h,h,type);}}
        void SpawnStartingBlocks(int stage){var hp=5+stage*2;CreateBlock(0,1,hp,hp,ReflectableBlockType.Normal);CreateBlock(0,5,hp+2,hp+2,ReflectableBlockType.Normal);CreateBlock(1,3,hp+1,hp+1,ReflectableBlockType.Gem);Debug.Log("Reflectable: Stage "+stage+" spawned 3 starting blocks.");}
        void CreateBlock(int row,int column,int currentHp,int maximumHp,ReflectableBlockType type){var prefab=type==ReflectableBlockType.Gem?gemBlockPrefab:type==ReflectableBlockType.Bomb?bombBlockPrefab:normalBlockPrefab;var view=Instantiate(prefab,CellPosition(row,column),Quaternion.identity,blocksRoot).GetComponent<ReflectableBlockView>();view.Setup(this,row,column,currentHp,maximumHp,type);blocks.Add(view);}
        Vector2 CellPosition(int row,int col)=>(Vector2)gridOrigin.position+new Vector2((col-(columns-1)*.5f)*cellSpacing.x,-row*cellSpacing.y);
        public void HitBlock(ReflectableBlockView block,int damage){if(!block||gameOver)return;combo++;maxCombo=Mathf.Max(maxCombo,combo);score+=10+combo/5;feedback?.Hit(combo);block.ApplyDamage(damage);if(block.HP<=0)DestroyBlock(block);RefreshUI();}
        void DestroyBlock(ReflectableBlockView block){if(!blocks.Remove(block))return;destroyed++;CheckStageClear();score+=block.Type==ReflectableBlockType.Gem?250:block.Type==ReflectableBlockType.Bomb?150:100;float comboBonus=combo>=30?1.30f:combo>=20?1.20f:combo>=10?1.10f:combo>=5?1.05f:1f;exp+=Mathf.RoundToInt((8+block.MaxHP/3f)*comboBonus);if(block.Type==ReflectableBlockType.Gem)gems+=2;if(block.Type==ReflectableBlockType.Bomb)foreach(var other in blocks.ToArray())if(other&&Mathf.Abs(other.Row-block.Row)<=1&&Mathf.Abs(other.Column-block.Column)<=1)HitBlock(other,Mathf.Max(3,block.MaxHP/2));while(exp>=ExpNeeded){exp-=ExpNeeded;level++;skillPoints++;}StartCoroutine(FadeDestroy(block));} int ExpNeeded=>10+level*8;
        void CheckStageClear(){if(stageCleared||destroyed<ReflectableStageSession.ClearRequirement(stage))return;stageCleared=true;if(stage<5)ReflectableStageSession.HighestUnlockedStage=Mathf.Max(ReflectableStageSession.HighestUnlockedStage,stage+1);Debug.Log("Reflectable: Stage "+stage+" clear requirement reached.");}
        public void RegisterRicochet(ReflectableProjectile projectile){ricochets++;projectile.AddDamage(1+ricochet+(character=="ECHO"?rank:0));}
        public void BuyUpgrade(string id){int cost=id=="Extra Ball"?2:1;if(!GameplayInputEnabled||skillPoints<cost){RefreshUI();return;}skillPoints-=cost;if(id=="Power")power++;else if(id=="Ricochet")ricochet++;else if(id=="Extra Ball")extraBall++;else if(id=="Max HP"){maxHpUpgrade++;maxHp+=15;hp=Mathf.Min(maxHp,hp+15);}RefreshUI();}
        public void SkipTurn(){if(skippingTurn||gameOver||ending||state==TurnState.Paused)return;StartCoroutine(SkipTurnRoutine());}
        IEnumerator SkipTurnRoutine(){skippingTurn=true;if(skipTurnButton)skipTurnButton.interactable=false;aiming=false;state=TurnState.Resolving;Debug.Log("[Turn] SKIP requested");ResolveActiveProjectiles(false);turnFired=false;combo=0;ending=false;yield return EndTurn();skippingTurn=false;if(skipTurnButton)skipTurnButton.interactable=!gameOver;Debug.Log("[Turn] Skip completed");}
        void ResolveActiveProjectiles(bool notifyOwner){var projectiles=FindObjectsByType<ReflectableProjectile>(FindObjectsSortMode.None);Debug.Log("[Turn] Cleaning active projectiles: "+projectiles.Length);foreach(var projectile in projectiles)projectile.ForceResolve(notifyOwner);activeProjectileIds.Clear();activeProjectiles=0;}
        public void OpenCharacterPanel(){if(gameOver||characterPanel==null)return;pausedState=state;state=TurnState.Paused;Time.timeScale=0;characterPanel.SetActive(true);SetCharacterResult($"CURRENT CHARACTER\n{character} {new string('★',rank)}\nYOUR GEMS: {gems}\nDRAW COST: {gachaCost}");}
        public void CloseCharacterPanel(){if(characterPanel)characterPanel.SetActive(false);if(!gameOver){Time.timeScale=1;state=pausedState;}}
        public void DrawCharacter(){if(gems<gachaCost){SetCharacterResult($"NOT ENOUGH GEMS\nYOUR GEMS: {gems}\nDRAW COST: {gachaCost}");return;}gems-=gachaCost;gachaCost+=2;float r=UnityEngine.Random.value;rank=r<.02f?5:r<.08f?4:r<.25f?3:r<.6f?2:1;character=new[]{"MIMI","ECHO","LUNE"}[UnityEngine.Random.Range(0,3)];SetCharacterResult($"NEW CHARACTER\n{character}\n{new string('★',rank)}");RefreshUI();}
        public void TogglePause(){if(gameOver)return;bool pause=Time.timeScale>0;if(pause){pausedState=state;state=TurnState.Paused;Time.timeScale=0;}else{state=pausedState;Time.timeScale=1;}if(pausePanel)pausePanel.SetActive(pause);}public void MainMenu(){Time.timeScale=1;SceneManager.LoadScene("MainMenu");}public void Retry(){Time.timeScale=1;StartNewGame();}
        void GameOver(){gameOver=true;state=TurnState.GameOver;aiming=false;if(aimPreview)aimPreview.enabled=false;PlayerPrefs.SetInt("ReflectableBest",Mathf.Max(PlayerPrefs.GetInt("ReflectableBest",0),score));PlayerPrefs.Save();if(File.Exists(SavePath))File.Delete(SavePath);if(gameOverText)gameOverText.text=$"GAME OVER\nScore {score}\nBest {PlayerPrefs.GetInt("ReflectableBest",0)}\nTurn {turn}\nBlocks {destroyed}\nMax Combo {maxCombo}\nRicochets {ricochets}";if(gameOverPanel)gameOverPanel.SetActive(true);}
        void Save(){var d=new ReflectableRunSave{valid=true,turn=turn,hp=hp,maxHp=maxHp,score=score,gems=gems,level=level,exp=exp,skillPoints=skillPoints,power=power,ricochet=ricochet,extraBall=extraBall,maxHpUpgrade=maxHpUpgrade,gachaCost=gachaCost,character=character,rank=rank};foreach(var b in blocks)if(b)d.blocks.Add(new ReflectableCellSave{row=b.Row,column=b.Column,hp=b.HP,maxHp=b.MaxHP,type=b.Type});File.WriteAllText(SavePath,JsonUtility.ToJson(d));}
        void RefreshUI(){if(hpText)hpText.text=$"HP {hp} / {maxHp}";if(scoreText)scoreText.text=$"SCORE {score}";if(gemsText)gemsText.text=$"GEMS {gems}";if(turnText)turnText.text=$"TURN {turn}";if(comboText)comboText.text=combo>0?$"{combo} HIT!":"";if(levelText)levelText.text=$"LV {level}  EXP {exp}/{ExpNeeded}  SP {skillPoints}";if(characterText)characterText.text=$"{character} {new string('★',rank)}";RefreshUpgradeButtons();}
        void RefreshUpgradeButtons(){SetUpgradeButton(powerButton,"POWER",power,1);SetUpgradeButton(ricochetButton,"RICOCHET",ricochet,1);SetUpgradeButton(extraBallButton,"EXTRA BALL",extraBall,2);SetUpgradeButton(maxHpButton,"MAX HP",maxHpUpgrade,1);if(characterButton){characterButton.interactable=!gameOver;SetButtonText(characterButton,"CHARACTER\nOPEN COLLECTION");}}
        void SetUpgradeButton(Button button,string title,int value,int cost){if(!button)return;button.interactable=GameplayInputEnabled&&skillPoints>=cost;SetButtonText(button,$"{title}\nLv. {value}\n{cost} SP");}
        static void SetButtonText(Button button,string value){var label=button.GetComponentInChildren<Text>();if(label)label.text=value;}
        IEnumerator StageIntroRoutine(){if(stageIntro){stageIntro.SetActive(true);var label=stageIntro.GetComponentInChildren<Text>();if(label)label.text=$"STAGE {stage}\n{ReflectableStageSession.GetPresentation(stage).Name}";var group=stageIntro.GetComponent<CanvasGroup>();if(group){for(float t=0;t<.18f;t+=Time.unscaledDeltaTime){group.alpha=t/.18f;yield return null;}yield return new WaitForSecondsRealtime(.42f);for(float t=0;t<.18f;t+=Time.unscaledDeltaTime){group.alpha=1f-t/.18f;yield return null;}}stageIntro.SetActive(false);}BeginTurn();}
        IEnumerator PlayerRecoil(){var visual=player?player.Find("CharacterVisual"):null;if(!visual)yield break;var start=visual.localPosition;for(float t=0;t<.05f;t+=Time.deltaTime){visual.localPosition=Vector3.Lerp(start,start+Vector3.down*.10f,t/.05f);yield return null;}for(float t=0;t<.10f;t+=Time.deltaTime){visual.localPosition=Vector3.Lerp(start+Vector3.down*.10f,start,t/.10f);yield return null;}if(visual)visual.localPosition=start;}
        IEnumerator MovePlayerToReturn(float returnX){if(!player)yield break;var start=player.position;var target=new Vector3(Mathf.Clamp(returnX,left+.45f,right-.45f),start.y,start.z);for(float t=0;t<.18f;t+=Time.deltaTime){player.position=Vector3.Lerp(start,target,Mathf.SmoothStep(0,1,t/.18f));yield return null;}if(player)player.position=target;}
        IEnumerator FadeDestroy(ReflectableBlockView block){var collider=block.GetComponent<Collider2D>();if(collider)collider.enabled=false;var visual=block.transform.Find("Visual")??block.transform;var renderers=visual.GetComponentsInChildren<SpriteRenderer>();var start=visual.localScale;for(float t=0;t<.18f;t+=Time.deltaTime){var p=t/.18f;if(visual)visual.localScale=Vector3.Lerp(start*1.12f,Vector3.zero,p);foreach(var item in renderers)if(item){var c=item.color;c.a=1f-p;item.color=c;}yield return null;}if(block)Destroy(block.gameObject);}
        void SetCharacterResult(string value)
        {
            if(characterPanel==null)return;
            var window=characterPanel.transform.Find("Window");
            if(window==null)return;
            SetPanelText(window,"CurrentCharacter",$"CURRENT: {character}");
            SetPanelText(window,"CharacterRank",$"RANK: {new string('★',rank)}");
            SetPanelText(window,"CharacterDescription",character=="ECHO"?"ECHO — ricochet specialist":character=="LUNE"?"LUNE — multi-projectile specialist":"MIMI — balanced specialist");
            SetPanelText(window,"GemAmount",$"YOUR GEMS: {gems}");
            SetPanelText(window,"DrawCost",$"DRAW COST: {gachaCost}");
            SetPanelText(window,"ResultArea",value);
        }
        void SetPanelText(Transform window,string name,string value){var item=window.Find(name);if(item){var label=item.GetComponent<UnityEngine.UI.Text>();if(label)label.text=value;}}
        void DrawPreview(Vector2 direction)
        {
            if (!aimPreview || !firePoint)
                return;

            const int maximumSegments = 5;
            var points = new Vector3[maximumSegments + 1];
            var point = (Vector2)firePoint.position;
            var velocity = direction.normalized;
            points[0] = point;
            var count = 1;

            for (var i = 0; i < maximumSegments; i++)
            {
                // Offset the origin by the projectile radius so this ray starts outside the player.
                var hit = Physics2D.Raycast(point + velocity * .14f, velocity, 20f);
                if (!hit.collider)
                {
                    points[count++] = point + velocity * 4f;
                    break;
                }

                point = hit.point;
                points[count++] = point;
                if (hit.collider.isTrigger || hit.collider.name == "BottomBoundary")
                    break;

                velocity = Vector2.Reflect(velocity, hit.normal).normalized;
            }

            aimPreview.positionCount = count;
            aimPreview.SetPositions(points);
        }
        void ClearBlocks(){foreach(var b in blocks)if(b)Destroy(b.gameObject);blocks.Clear();}void OnDrawGizmosSelected(){if(!gridOrigin)return;Gizmos.color=Color.cyan;for(int r=0;r<rows;r++)for(int c=0;c<columns;c++)Gizmos.DrawWireCube(CellPosition(r,c),new Vector3(cellSpacing.x*.9f,cellSpacing.y*.85f,.05f));}
    }
}
