using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Reflectable
{
    /// <summary>
    /// Scene-first gameplay controller.
    ///
    /// IMPORTANT:
    /// - Arena, Player, Camera, Canvas and HUD must already exist in the Unity Scene.
    /// - This script NEVER creates those presentation objects at runtime.
    /// - Only gameplay objects that are supposed to be dynamic are instantiated:
    ///   Blocks and Projectiles, and both must come from editable Prefabs.
    /// </summary>
    public sealed class ReflectableBootstrap : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Camera gameCamera;
        [SerializeField] private Transform player;
        [SerializeField] private Transform shotOrigin;

        [Tooltip("Transform positioned at the inside edge of the left gameplay wall.")]
        [SerializeField] private Transform leftBoundary;

        [Tooltip("Transform positioned at the inside edge of the right gameplay wall.")]
        [SerializeField] private Transform rightBoundary;

        [Tooltip("Transform positioned at the inside edge of the ceiling.")]
        [SerializeField] private Transform ceilingBoundary;

        [Tooltip("Transform positioned below the player. Projectiles crossing this Y are removed.")]
        [SerializeField] private Transform bottomBoundary;

        [Header("Block Grid")]
        [Tooltip("Top-left spawn position of column 0, row 0.")]
        [SerializeField] private Transform gridOrigin;
        [SerializeField, Min(1)] private int columns = 7;
        [SerializeField, Min(1)] private int rows = 7;
        [SerializeField] private Vector2 cellSpacing = new Vector2(1.85f, 1.25f);

        [Header("Editable Prefabs")]
        [SerializeField] private GameObject normalBlockPrefab;
        [SerializeField] private GameObject gemBlockPrefab;
        [SerializeField] private GameObject bombBlockPrefab;
        [SerializeField] private GameObject projectilePrefab;

        [Header("Runtime Parents")]
        [Tooltip("Empty object in the Scene. Runtime blocks will be children of this object.")]
        [SerializeField] private Transform blockContainer;

        [Tooltip("Empty object in the Scene. Runtime projectiles will be children of this object.")]
        [SerializeField] private Transform projectileContainer;

        [Header("HUD - Existing Scene UI")]
        [SerializeField] private Text hpText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text gemsText;
        [SerializeField] private Text turnText;
        [SerializeField] private Text comboText;
        [SerializeField] private Text expText;
        [SerializeField] private Text characterText;

        [Header("Gameplay")]
        [SerializeField, Min(0.01f)] private float projectileSpeed = 12f;
        [SerializeField, Min(0.01f)] private float projectileLifetime = 20f;
        [SerializeField, Min(0.01f)] private float blockHalfWidth = 0.73f;
        [SerializeField, Min(0.01f)] private float blockHalfHeight = 0.73f;
        [SerializeField, Min(0f)] private float launchInterval = 0.09f;
        [SerializeField, Min(0f)] private float turnEndDelay = 0.35f;
        [SerializeField, Min(0f)] private float rowMoveDuration = 0.25f;
        [SerializeField] private bool startAutomatically = true;

        private readonly List<Block> blocks = new List<Block>();
        private readonly List<Ball> balls = new List<Ball>();

        private readonly Dictionary<string, int> skills = new Dictionary<string, int>
        {
            { "POWER", 0 },
            { "RICOCHET", 0 },
            { "EXTRA BALL", 0 }
        };

        private bool aiming;
        private bool gameOver;
        private bool paused;
        private bool endingTurn;

        private int turn;
        private int hp = 100;
        private int maxHp = 100;
        private int score;
        private int gems;
        private int level = 1;
        private int exp;
        private int sp;
        private int combo;
        private int maxCombo;
        private int destroyed;
        private int ricochets;
        private int gachaCost = 2;
        private int rank = 1;
        private int ballsToLaunch;

        private float shotDamage = 5f;
        private string character = "MIMI";
        private string savePath;

        [Serializable]
        private class SaveData
        {
            public bool valid;
            public int turn;
            public int hp;
            public int maxHp;
            public int score;
            public int gems;
            public int level;
            public int exp;
            public int sp;
            public int gachaCost;
            public string character;
            public int rank;
            public List<int> skill = new List<int>();
            public List<CellSave> cells = new List<CellSave>();
        }

        [Serializable]
        private class CellSave
        {
            public int r;
            public int c;
            public int hp;
            public int max;
            public string type;
        }

        private sealed class Block
        {
            public int r;
            public int c;
            public int hp;
            public int max;
            public string type;
            public GameObject go;
            public Text label;
        }

        private sealed class Ball
        {
            public Vector2 p;
            public Vector2 v;
            public float life;
            public float damage;
            public int bounces;
            public GameObject go;
            public readonly HashSet<Block> recent = new HashSet<Block>();
        }

        private void Awake()
        {
            // Legacy prototype controller. SceneFirstSetup replaces it with
            // ReflectableGameController and removes this component from rebuilt scenes.
            // Keeping this guard makes older, already-open scenes safe during migration.
            if (Application.isPlaying && GetType() == typeof(ReflectableBootstrap))
            {
                enabled = false;
                return;
            }

            Application.targetFrameRate = 120;
            savePath = Path.Combine(Application.persistentDataPath, "reflectable_save.json");

            if (gameCamera == null)
                gameCamera = Camera.main;

            if (shotOrigin == null && player != null)
                shotOrigin = player;

            ValidateSceneReferences();
        }

        private void Start()
        {
            if (!startAutomatically)
                return;

            if (SceneManager.GetActiveScene().name != "Game")
                return;

            StartNewRun();
        }

        private void Update()
        {
            if (gameOver)
                return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                paused = !paused;
                RefreshHud();
            }

            if (!aiming || paused)
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            Fire();
        }

        private void FixedUpdate()
        {
            if (balls.Count == 0)
                return;

            for (int i = balls.Count - 1; i >= 0; i--)
            {
                Ball ball = balls[i];
                StepBall(ball, Time.fixedDeltaTime);

                if (ball.life <= 0f || ball.p.y < BottomY)
                    RemoveBallAt(i);
            }

            if (!aiming && balls.Count == 0 && !endingTurn && !gameOver)
                StartCoroutine(EndTurn());
        }

        public void StartNewRun()
        {
            StopAllCoroutines();
            ClearDynamicGameplay();

            turn = 0;
            hp = 100;
            maxHp = 100;
            score = 0;
            gems = 0;
            exp = 0;
            sp = 0;
            combo = 0;
            maxCombo = 0;
            destroyed = 0;
            ricochets = 0;
            level = 1;
            gachaCost = 2;
            character = "MIMI";
            rank = 1;
            paused = false;
            gameOver = false;
            endingTurn = false;

            List<string> keys = new List<string>(skills.Keys);
            foreach (string key in keys)
                skills[key] = 0;

            SpawnInitial();
            StartTurn();
        }

        public void ResumeRun()
        {
            if (!File.Exists(savePath))
            {
                StartNewRun();
                return;
            }

            StopAllCoroutines();
            ClearDynamicGameplay();
            LoadRun();

            paused = false;
            gameOver = false;
            endingTurn = false;
            aiming = true;

            RefreshHud();
        }

        public void UpgradePower()
        {
            Upgrade("POWER");
        }

        public void UpgradeRicochet()
        {
            Upgrade("RICOCHET");
        }

        public void UpgradeExtraBall()
        {
            Upgrade("EXTRA BALL");
        }

        public void CharacterDraw()
        {
            if (!aiming || gameOver || gems < gachaCost)
                return;

            gems -= gachaCost;
            gachaCost++;

            float roll = UnityEngine.Random.value;
            rank = roll < 0.35f ? 1 :
                   roll < 0.65f ? 2 :
                   roll < 0.85f ? 3 :
                   roll < 0.95f ? 4 : 5;

            string[] characters = { "MIMI", "ECHO", "LUNE" };
            character = characters[UnityEngine.Random.Range(0, characters.Length)];

            RefreshHud();
        }

        public void TogglePause()
        {
            paused = !paused;
            RefreshHud();
        }

        private void Fire()
        {
            if (gameCamera == null || shotOrigin == null)
                return;

            Vector3 mouseWorld3 = gameCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2 mouseWorld = new Vector2(mouseWorld3.x, mouseWorld3.y);
            Vector2 direction = (mouseWorld - (Vector2)shotOrigin.position).normalized;

            if (direction.y < 0.12f)
                return;

            aiming = false;

            ballsToLaunch = 1 + skills["EXTRA BALL"] + CharacterBalls();
            shotDamage = 5f *
                         (1f + 0.1f * skills["POWER"]) *
                         (character == "LUNE" ? 0.75f : 1f);

            StartCoroutine(Launch(direction));
        }

        private int CharacterBalls()
        {
            if (character != "LUNE")
                return 0;

            if (rank >= 5)
                return 3;

            if (rank >= 3)
                return 2;

            return 1;
        }

        private IEnumerator Launch(Vector2 direction)
        {
            for (int i = 0; i < ballsToLaunch; i++)
            {
                CreateBall(direction);

                if (launchInterval > 0f)
                    yield return new WaitForSeconds(launchInterval);
            }
        }

        private void CreateBall(Vector2 direction)
        {
            if (projectilePrefab == null || shotOrigin == null)
            {
                Debug.LogError(
                    "ReflectableBootstrap: Projectile Prefab or Shot Origin is not assigned.",
                    this);
                return;
            }

            GameObject go = Instantiate(
                projectilePrefab,
                shotOrigin.position,
                Quaternion.identity,
                projectileContainer);

            Ball ball = new Ball
            {
                p = shotOrigin.position,
                v = direction.normalized * projectileSpeed,
                damage = shotDamage,
                life = projectileLifetime,
                go = go
            };

            balls.Add(ball);
        }

        private void StepBall(Ball ball, float deltaTime)
        {
            if (ball.go == null)
            {
                ball.life = 0f;
                return;
            }

            ball.life -= deltaTime;
            Vector2 next = ball.p + ball.v * deltaTime;

            if (next.x < LeftX || next.x > RightX)
            {
                ball.v.x = -ball.v.x;
                next.x = Mathf.Clamp(next.x, LeftX, RightX);
                WallBounce(ball);
            }

            if (next.y > CeilingY)
            {
                ball.v.y = -Mathf.Abs(ball.v.y);
                next.y = CeilingY;
                WallBounce(ball);
            }

            Block[] blockSnapshot = blocks.ToArray();

            foreach (Block block in blockSnapshot)
            {
                if (block.go == null)
                    continue;

                if (!RectContains(block, next))
                    continue;

                if (ball.recent.Contains(block))
                    continue;

                ball.recent.Add(block);

                Damage(
                    block,
                    Mathf.Max(1, Mathf.RoundToInt(ball.damage)));

                Vector2 delta = next - CellPos(block.r, block.c);

                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                    ball.v.x = -ball.v.x;
                else
                    ball.v.y = -ball.v.y;

                break;
            }

            ball.p = next;

            ball.recent.RemoveWhere(
                block => block.go == null || !RectContains(block, ball.p));

            ball.go.transform.position = ball.p;
        }

        private void RemoveBallAt(int index)
        {
            if (index < 0 || index >= balls.Count)
                return;

            Ball ball = balls[index];

            if (ball.go != null)
                Destroy(ball.go);

            balls.RemoveAt(index);
        }

        private bool RectContains(Block block, Vector2 point)
        {
            Vector2 center = CellPos(block.r, block.c);

            return Mathf.Abs(point.x - center.x) < blockHalfWidth &&
                   Mathf.Abs(point.y - center.y) < blockHalfHeight;
        }

        private void WallBounce(Ball ball)
        {
            ball.bounces++;
            ricochets++;

            float bonus =
                0.03f * skills["RICOCHET"] +
                (character == "ECHO" ? 0.03f * rank : 0f);

            ball.damage *= 1f + bonus;
        }

        private void SpawnInitial()
        {
            int initialRows = Mathf.Min(3, rows);

            for (int r = 0; r < initialRows; r++)
                SpawnRow(r);
        }

        private void SpawnRow(int row)
        {
            if (columns <= 0)
                return;

            int count = Mathf.Min(
                Mathf.Max(1, columns - 1),
                UnityEngine.Random.Range(2, 5) + turn / 8);

            HashSet<int> chosen = new HashSet<int>();

            for (int i = 0; i < count; i++)
            {
                int column;

                do
                {
                    column = UnityEngine.Random.Range(0, columns);
                }
                while (!chosen.Add(column));

                float roll = UnityEngine.Random.value;
                string type =
                    roll < 0.10f ? "GEM" :
                    roll < 0.26f ? "BOMB" :
                    "NORMAL";

                int blockHp =
                    Mathf.RoundToInt(
                        5 + turn * 2 + UnityEngine.Random.Range(0, 10));

                CreateBlock(row, column, blockHp, type);
            }
        }

        private void CreateBlock(int row, int column, int blockHp, string type)
        {
            GameObject prefab = GetBlockPrefab(type);

            if (prefab == null)
            {
                Debug.LogError(
                    "ReflectableBootstrap: Missing prefab for block type " + type + ".",
                    this);
                return;
            }

            GameObject go = Instantiate(
                prefab,
                CellPos(row, column),
                prefab.transform.rotation,
                blockContainer);

            go.name = type + " Block [" + row + "," + column + "]";

            Text label = go.GetComponentInChildren<Text>(true);

            Block block = new Block
            {
                r = row,
                c = column,
                hp = blockHp,
                max = blockHp,
                type = type,
                go = go,
                label = label
            };

            blocks.Add(block);
            RefreshBlockLabel(block);
        }

        private GameObject GetBlockPrefab(string type)
        {
            switch (type)
            {
                case "GEM":
                    return gemBlockPrefab != null ? gemBlockPrefab : normalBlockPrefab;

                case "BOMB":
                    return bombBlockPrefab != null ? bombBlockPrefab : normalBlockPrefab;

                default:
                    return normalBlockPrefab;
            }
        }

        private Vector2 CellPos(int row, int column)
        {
            if (gridOrigin == null)
                return Vector2.zero;

            return (Vector2)gridOrigin.position +
                   new Vector2(
                       column * cellSpacing.x,
                       -row * cellSpacing.y);
        }

        private void Damage(Block block, int amount)
        {
            if (block == null || block.go == null)
                return;

            block.hp -= amount;

            combo++;
            maxCombo = Mathf.Max(maxCombo, combo);

            score += Mathf.RoundToInt(
                10f * (1f + Mathf.Min(combo, 50) * 0.02f));

            RefreshBlockLabel(block);
            StartCoroutine(Punch(block.go.transform));

            if (block.hp <= 0)
                DestroyBlock(block);

            RefreshHud();
        }

        private IEnumerator Punch(Transform target)
        {
            if (target == null)
                yield break;

            Vector3 originalScale = target.localScale;
            target.localScale = originalScale * 1.12f;

            yield return new WaitForSeconds(0.08f);

            if (target != null)
                target.localScale = originalScale;
        }

        private void RefreshBlockLabel(Block block)
        {
            if (block == null || block.label == null)
                return;

            string prefix =
                block.type == "BOMB" ? "✦\n" :
                block.type == "GEM" ? "◆\n" :
                string.Empty;

            block.label.text = prefix + Mathf.Max(0, block.hp);
        }

        private void DestroyBlock(Block block)
        {
            if (block == null || !blocks.Remove(block))
                return;

            destroyed++;

            score +=
                block.type == "GEM" ? 250 :
                block.type == "BOMB" ? 150 :
                100;

            GainExp(5 + block.max / 4);

            if (block.type == "GEM")
                gems++;

            if (block.type == "BOMB")
            {
                Block[] snapshot = blocks.ToArray();

                foreach (Block other in snapshot)
                {
                    if (Mathf.Abs(other.r - block.r) <= 1 &&
                        Mathf.Abs(other.c - block.c) <= 1)
                    {
                        Damage(other, Mathf.Max(4, block.max / 2));
                    }
                }
            }

            if (block.go != null)
                Destroy(block.go);
        }

        private void GainExp(int amount)
        {
            exp += amount;
            int needed = 25 + level * 15;

            while (exp >= needed)
            {
                exp -= needed;
                level++;
                sp++;
                needed = 25 + level * 15;
            }
        }

        private IEnumerator EndTurn()
        {
            endingTurn = true;

            if (turnEndDelay > 0f)
                yield return new WaitForSeconds(turnEndDelay);

            foreach (Block block in blocks)
            {
                block.r++;
                StartCoroutine(MoveBlock(block, CellPos(block.r, block.c)));
            }

            if (rowMoveDuration > 0f)
                yield return new WaitForSeconds(rowMoveDuration);

            Block[] snapshot = blocks.ToArray();

            foreach (Block block in snapshot)
            {
                if (block.r < rows)
                    continue;

                hp -= block.hp;
                blocks.Remove(block);

                if (block.go != null)
                    Destroy(block.go);
            }

            SpawnRow(0);

            if (hp <= 0)
            {
                GameOver();
                yield break;
            }

            SaveRun();

            endingTurn = false;
            StartTurn();
        }

        private IEnumerator MoveBlock(Block block, Vector2 target)
        {
            if (block.go == null)
                yield break;

            Transform targetTransform = block.go.transform;
            Vector2 start = targetTransform.position;

            if (rowMoveDuration <= 0f)
            {
                targetTransform.position = target;
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < rowMoveDuration)
            {
                if (targetTransform == null)
                    yield break;

                elapsed += Time.deltaTime;

                targetTransform.position = Vector2.Lerp(
                    start,
                    target,
                    Mathf.Clamp01(elapsed / rowMoveDuration));

                yield return null;
            }

            if (targetTransform != null)
                targetTransform.position = target;
        }

        private void StartTurn()
        {
            turn++;
            aiming = true;
            combo = 0;

            RefreshHud();

            Debug.Log("Reflectable: Turn " + turn + " started", this);
        }

        private void Upgrade(string name)
        {
            if (!aiming || paused || gameOver)
                return;

            int cost = name == "EXTRA BALL" ? 2 : 1;

            if (sp < cost)
                return;

            sp -= cost;
            skills[name]++;

            RefreshHud();
        }

        private void RefreshHud()
        {
            if (hpText != null)
                hpText.text = "HP  " + hp + " / " + maxHp;

            if (scoreText != null)
                scoreText.text = "SCORE  " + score;

            if (gemsText != null)
                gemsText.text = "◆  " + gems;

            if (turnText != null)
                turnText.text = "TURN " + turn + (paused ? "  |  PAUSED" : "");

            if (comboText != null)
                comboText.text = combo >= 10 ? combo + " HIT!" : "";

            if (expText != null)
            {
                expText.text =
                    "LV " + level +
                    "    EXP " + exp + " / " + (25 + level * 15) +
                    "    SP " + sp;
            }

            if (characterText != null)
            {
                characterText.text =
                    character + " " +
                    new string('★', rank) +
                    "   |   " +
                    skills["POWER"] + " POW  " +
                    skills["RICOCHET"] + " RIC";
            }
        }

        private void GameOver()
        {
            gameOver = true;
            aiming = false;
            endingTurn = false;

            PlayerPrefs.SetInt(
                "ReflectableBest",
                Mathf.Max(
                    PlayerPrefs.GetInt("ReflectableBest", 0),
                    score));

            PlayerPrefs.Save();

            if (File.Exists(savePath))
                File.Delete(savePath);

            RefreshHud();

            Debug.Log(
                "Reflectable: GAME OVER | Score " + score +
                " | Turn " + turn +
                " | Blocks " + destroyed +
                " | Max Combo " + maxCombo +
                " | Ricochets " + ricochets,
                this);
        }

        private void SaveRun()
        {
            SaveData data = new SaveData
            {
                valid = true,
                turn = turn,
                hp = hp,
                maxHp = maxHp,
                score = score,
                gems = gems,
                level = level,
                exp = exp,
                sp = sp,
                gachaCost = gachaCost,
                character = character,
                rank = rank
            };

            foreach (KeyValuePair<string, int> skill in skills)
                data.skill.Add(skill.Value);

            foreach (Block block in blocks)
            {
                data.cells.Add(
                    new CellSave
                    {
                        r = block.r,
                        c = block.c,
                        hp = block.hp,
                        max = block.max,
                        type = block.type
                    });
            }

            File.WriteAllText(
                savePath,
                JsonUtility.ToJson(data));

            Debug.Log("Reflectable: Run saved", this);
        }

        private void LoadRun()
        {
            SaveData data =
                JsonUtility.FromJson<SaveData>(
                    File.ReadAllText(savePath));

            if (data == null || !data.valid)
            {
                StartNewRun();
                return;
            }

            turn = data.turn;
            hp = data.hp;
            maxHp = data.maxHp;
            score = data.score;
            gems = data.gems;
            level = data.level;
            exp = data.exp;
            sp = data.sp;
            gachaCost = data.gachaCost;
            character = data.character;
            rank = data.rank;

            int index = 0;
            List<string> keys = new List<string>(skills.Keys);

            foreach (string key in keys)
            {
                skills[key] =
                    index < data.skill.Count
                        ? data.skill[index]
                        : 0;

                index++;
            }

            foreach (CellSave cell in data.cells)
            {
                CreateBlock(
                    cell.r,
                    cell.c,
                    cell.hp,
                    cell.type);

                Block created = blocks[blocks.Count - 1];
                created.max = cell.max;
                created.hp = cell.hp;
                RefreshBlockLabel(created);
            }
        }

        private void ClearDynamicGameplay()
        {
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                if (balls[i].go != null)
                    Destroy(balls[i].go);
            }

            for (int i = blocks.Count - 1; i >= 0; i--)
            {
                if (blocks[i].go != null)
                    Destroy(blocks[i].go);
            }

            balls.Clear();
            blocks.Clear();

            gameOver = false;
            endingTurn = false;
            aiming = false;
        }

        private void ValidateSceneReferences()
        {
            bool valid = true;

            valid &= CheckReference(gameCamera, "Game Camera");
            valid &= CheckReference(player, "Player");
            valid &= CheckReference(shotOrigin, "Shot Origin");
            valid &= CheckReference(leftBoundary, "Left Boundary");
            valid &= CheckReference(rightBoundary, "Right Boundary");
            valid &= CheckReference(ceilingBoundary, "Ceiling Boundary");
            valid &= CheckReference(bottomBoundary, "Bottom Boundary");
            valid &= CheckReference(gridOrigin, "Grid Origin");
            valid &= CheckReference(normalBlockPrefab, "Normal Block Prefab");
            valid &= CheckReference(projectilePrefab, "Projectile Prefab");

            if (!valid)
            {
                Debug.LogError(
                    "ReflectableBootstrap: Scene setup is incomplete. " +
                    "Select the GameManager object and assign the missing Inspector references.",
                    this);
            }
        }

        private bool CheckReference(UnityEngine.Object reference, string displayName)
        {
            if (reference != null)
                return true;

            Debug.LogError(
                "ReflectableBootstrap: Missing reference -> " + displayName,
                this);

            return false;
        }

        private float LeftX
        {
            get { return leftBoundary != null ? leftBoundary.position.x : -7f; }
        }

        private float RightX
        {
            get { return rightBoundary != null ? rightBoundary.position.x : 7f; }
        }

        private float CeilingY
        {
            get { return ceilingBoundary != null ? ceilingBoundary.position.y : 5.2f; }
        }

        private float BottomY
        {
            get { return bottomBoundary != null ? bottomBoundary.position.y : -5.4f; }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (gridOrigin != null)
            {
                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        Vector2 position =
                            (Vector2)gridOrigin.position +
                            new Vector2(
                                column * cellSpacing.x,
                                -row * cellSpacing.y);

                        Gizmos.DrawWireCube(
                            position,
                            new Vector3(
                                blockHalfWidth * 2f,
                                blockHalfHeight * 2f,
                                0.01f));
                    }
                }
            }

            if (shotOrigin != null)
                Gizmos.DrawWireSphere(shotOrigin.position, 0.15f);
        }
#endif
    }
}
