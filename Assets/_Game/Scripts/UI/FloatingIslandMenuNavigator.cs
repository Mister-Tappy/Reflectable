using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Reflectable
{
    public enum MenuDestination { Play, Continue, Settings, Exit }
    public enum MenuNavigationState { MainMenu, LaunchingBall, FollowingBall, ArrivingAtDestination, DestinationMenu, ReturningToStart, ExitSequence, Exiting }

    /// <summary>Builds the replaceable floating-island mockup and coordinates menu travel and panels.</summary>
    public sealed class FloatingIslandMenuNavigator : MonoBehaviour
    {
        [Serializable]
        sealed class IslandLayout
        {
            public MenuDestination destination;
            public Transform island;
            public Transform landingPoint;
            public Vector2 center;
            public Vector2 size = new Vector2(4.2f, 1.15f);
            public Color surfaceColor = Color.white;
            public Color sideColor = new Color(.4f, .45f, .68f);
        }

        [Header("Replaceable mockup artwork")]
        [SerializeField] GameObject worldRoot;
        [SerializeField] Transform startPoint;
        [SerializeField] Transform menuBall;
        [SerializeField] Sprite backgroundLayerSprite;
        [SerializeField] Sprite islandSurfaceSprite;
        [SerializeField] Sprite ballSprite;
        [Header("Responsive background")]
        [SerializeField] SpriteRenderer responsiveBackgroundCover;
        [SerializeField] SpriteRenderer landscapeBackgroundArt;
        [SerializeField] IslandLayout[] destinations =
        {
            new IslandLayout { destination = MenuDestination.Play },
            new IslandLayout { destination = MenuDestination.Continue },
            new IslandLayout { destination = MenuDestination.Settings },
            new IslandLayout { destination = MenuDestination.Exit }
        };
        [Header("Ball travel")]
        [SerializeField, Min(.2f)] float travelDuration = 1.18f;
        [SerializeField, Min(0f)] float arcHeight = 2.15f;
        [SerializeField, Min(.1f)] float travelCameraSize = 6.2f;
        [SerializeField, Min(.01f)] float destinationSettleTime = .2f;
        [SerializeField, Min(.01f)] float cameraSettleTimeout = .7f;
        [SerializeField, Min(0f)] float cameraFollowOffsetY = .15f;
        [SerializeField] Color ballColor = new Color(.82f, .82f, 1f, 1f);
        [SerializeField] Color ballGlowColor = new Color(.46f, .82f, 1f, .32f);

        static readonly Color[] CloudTints =
        {
            new Color(1f, 1f, 1f, .32f), new Color(.85f, .91f, 1f, .28f), new Color(1f, .82f, .92f, .22f)
        };

        readonly Dictionary<MenuDestination, Transform> landingTargets = new Dictionary<MenuDestination, Transform>();
        readonly List<Button> menuButtons = new List<Button>();
        readonly MenuWave[] wavePool = new MenuWave[10];
        readonly MenuParticle[] particlePool = new MenuParticle[18];
        ReflectableMenuController menuController;
        MenuCameraFollow2D cameraFollow;
        Camera sceneCamera;
        GameObject continuePanel, exitPanel;
        Transform farClouds, nearClouds;
        Transform ball;
        SpriteRenderer ballRenderer, ballGlow;
        TrailRenderer ballTrail;
        Sprite squareSprite, glowSprite, ringSprite, sphereSprite, diamondSprite;
        Vector2 responsiveBackgroundSpriteSize;
        Vector3 responsiveBackgroundBaseScale, responsiveBackgroundBasePosition;
        Material spriteMaterial, trailMaterial;
        Vector3 startPosition, initialCameraPosition;
        float overviewCameraSize;
        float spinDegrees;
        int waveCursor;
        int particleCursor;
        bool worldBuilt;

        public MenuNavigationState State { get; private set; } = MenuNavigationState.MainMenu;
        public MenuDestination CurrentDestination { get; private set; }

        void Awake()
        {
            menuController = GetComponent<ReflectableMenuController>() ?? FindFirstObjectByType<ReflectableMenuController>();
            sceneCamera = Camera.main;
            if (!sceneCamera) sceneCamera = FindFirstObjectByType<Camera>();
            if (!menuController || !sceneCamera)
            {
                Debug.LogError("FloatingIslandMenuNavigator requires the Main Menu controller and camera.", this);
                enabled = false;
                return;
            }
            BuildWorld();
            BindDestinationPanels();
        }

        void Start()
        {
            if (!worldBuilt) return;
            menuButtons.Clear();
            if (menuController.MainMenuPanel)
                menuButtons.AddRange(menuController.MainMenuPanel.GetComponentsInChildren<Button>(true));
            SetMenuButtons(true);
            var canvas = menuController.MainMenuPanel ? menuController.MainMenuPanel.GetComponentInParent<Canvas>() : null;
            if (canvas)
            {
                foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                    if (button && button.transform.IsChildOf(menuController.MainMenuPanel.transform))
                        button.interactable = true;
            }
            State = MenuNavigationState.MainMenu;
        }

        public bool NavigateTo(MenuDestination destination)
        {
            if (!enabled) return false;
            if (State != MenuNavigationState.MainMenu) return true;
            if (destination == MenuDestination.Continue && !menuController) return false;
            CurrentDestination = destination;
            StartCoroutine(TravelSequence(destination, false));
            return true;
        }

        public bool ReturnToMainMenu()
        {
            if (State == MenuNavigationState.MainMenu) return false;
            if (State != MenuNavigationState.DestinationMenu && !(State == MenuNavigationState.Exiting && ExitIsMocked)) return true;
            StartCoroutine(TravelSequence(CurrentDestination, true));
            return true;
        }

        void BuildWorld()
        {
            if (!worldRoot || !startPoint || !menuBall)
            {
                Debug.LogError("FloatingIslandMenuNavigator needs scene references for World Root, Start Point, and Menu Ball.", this);
                enabled = false;
                return;
            }

            initialCameraPosition = sceneCamera.transform.position;
            overviewCameraSize = Mathf.Max(sceneCamera.orthographicSize, OverviewSizeForAspect());
            if (sceneCamera.orthographic) sceneCamera.orthographicSize = overviewCameraSize;
            var follow = sceneCamera.GetComponent<MenuCameraFollow2D>() ?? sceneCamera.gameObject.AddComponent<MenuCameraFollow2D>();
            cameraFollow = follow;
            cameraFollow.Initialize(sceneCamera, initialCameraPosition, overviewCameraSize);

            startPosition = startPoint.position;
            ball = menuBall;
            ballRenderer = menuBall.GetComponent<SpriteRenderer>();
            var glow = menuBall.Find("BallSoftGlow");
            ballGlow = glow ? glow.GetComponent<SpriteRenderer>() : null;
            ballTrail = menuBall.GetComponent<TrailRenderer>();
            foreach (var layout in destinations)
            {
                if (layout == null || !layout.landingPoint) continue;
                landingTargets[layout.destination] = layout.landingPoint;
            }
            ball.position = startPosition;
            cameraFollow.SnapTo(initialCameraPosition, overviewCameraSize);
            if (responsiveBackgroundCover && responsiveBackgroundCover.sprite)
            {
                responsiveBackgroundSpriteSize = responsiveBackgroundCover.sprite.bounds.size;
                responsiveBackgroundBaseScale = responsiveBackgroundCover.transform.localScale;
                responsiveBackgroundBasePosition = responsiveBackgroundCover.transform.position;
            }
            worldBuilt = true;
        }

        GameObject CreateIsland(string objectName, Vector2 position, Vector2 size, Color topColor, Color sideColor, string label)
        {
            var island = new GameObject(objectName);
            island.transform.SetParent(worldRoot.transform, false);
            island.transform.position = new Vector3(position.x, position.y, 0f);

            var shadow = MakeSprite("SoftIslandShadow", island.transform, glowSprite, new Color(.3f, .35f, .56f, .25f), 0);
            shadow.transform.localPosition = new Vector3(0f, -.38f, .04f);
            shadow.transform.localScale = new Vector3(size.x * 1.18f, .66f, 1f);

            var lowerFace = MakeSprite("FloatingIslandSide", island.transform, squareSprite, sideColor, 10);
            lowerFace.transform.localPosition = new Vector3(0f, -.31f, 0f);
            lowerFace.transform.localScale = new Vector3(size.x, size.y * .68f, 1f);

            var frontFace = MakeSprite("IslandFrontFacet", island.transform, squareSprite, Color.Lerp(sideColor, Color.black, .12f), 11);
            frontFace.transform.localPosition = new Vector3(0f, -.48f, -.01f);
            frontFace.transform.localScale = new Vector3(size.x * .86f, size.y * .19f, 1f);

            var top = MakeSprite("FloatingIslandTop", island.transform, squareSprite, topColor, 20);
            top.transform.localPosition = Vector3.zero;
            top.transform.localScale = new Vector3(size.x, size.y * .26f, 1f);

            var highlight = MakeSprite("IslandTopHighlight", island.transform, squareSprite, Color.Lerp(topColor, Color.white, .44f), 21);
            highlight.transform.localPosition = new Vector3(0f, size.y * .075f, -.02f);
            highlight.transform.localScale = new Vector3(size.x * .94f, size.y * .035f, 1f);

            var crystal = MakeSprite("PastelCrystal", island.transform, diamondSprite, Color.Lerp(topColor, Color.white, .24f), 22);
            crystal.transform.localPosition = new Vector3(-size.x * .32f, size.y * .45f, -.04f);
            crystal.transform.localScale = new Vector3(.38f, .68f, 1f);
            var crystalGlow = MakeSprite("CrystalGlow", crystal.transform, glowSprite, new Color(.65f, .83f, 1f, .45f), -1);
            crystalGlow.transform.localPosition = new Vector3(0f, 0f, .04f);
            crystalGlow.transform.localScale = new Vector3(1.9f, 1.7f, 1f);

            var targetMarker = MakeSprite("LandingTargetGlow", island.transform, glowSprite, new Color(1f, .95f, .78f, .26f), 24);
            targetMarker.transform.localPosition = new Vector3(0f, size.y * .13f + .25f, -.08f);
            targetMarker.transform.localScale = Vector3.one * .78f;
            var markerRing = MakeSprite("LandingTargetRing", island.transform, ringSprite, new Color(.88f, .96f, 1f, .76f), 25);
            markerRing.transform.localPosition = targetMarker.transform.localPosition + new Vector3(0f, 0f, -.01f);
            markerRing.transform.localScale = Vector3.one * .55f;

            var name = new GameObject("IslandName").AddComponent<TextMesh>();
            name.transform.SetParent(island.transform, false);
            name.transform.localPosition = new Vector3(0f, size.y * .5f + .95f, -.1f);
            name.text = label;
            Font labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!labelFont) labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            name.font = labelFont;
            name.fontSize = 44;
            name.characterSize = .08f;
            name.anchor = TextAnchor.MiddleCenter;
            name.alignment = TextAlignment.Center;
            name.color = new Color(.25f, .31f, .5f, .95f);
            var textRenderer = name.GetComponent<MeshRenderer>();
            textRenderer.sortingOrder = 30;
            if (labelFont) textRenderer.sharedMaterial = labelFont.material;
            return island;
        }

        void BuildCloudLayers()
        {
            farClouds = new GameObject("FarPastelCloudLayer").transform;
            farClouds.SetParent(worldRoot.transform, false);
            nearClouds = new GameObject("NearPastelCloudLayer").transform;
            nearClouds.SetParent(worldRoot.transform, false);
            var random = new System.Random(104729);
            for (int i = 0; i < 10; i++)
            {
                Transform parent = i < 5 ? farClouds : nearClouds;
                float x = (float)random.NextDouble() * 28f - 14f;
                float y = (float)random.NextDouble() * 16f - 8f;
                float size = (float)random.NextDouble() * 1.7f + .9f;
                for (int lobe = 0; lobe < 3; lobe++)
                {
                    var cloud = MakeSprite("PastelCloud", parent, backgroundLayerSprite ? backgroundLayerSprite : glowSprite, CloudTints[i % CloudTints.Length], -20);
                    cloud.transform.localPosition = new Vector3(x + (lobe - 1) * size * .28f, y + (lobe == 1 ? .12f : 0f), 3f + i * .01f);
                    cloud.transform.localScale = new Vector3(size * (lobe == 1 ? 1.35f : .95f), size * .52f, 1f);
                }
            }
        }

        void BuildBall()
        {
            var ballObject = new GameObject("MenuMagicBall");
            ballObject.transform.SetParent(worldRoot.transform, false);
            ball = ballObject.transform;
            ball.position = startPosition;

            var glow = MakeSprite("BallSoftGlow", ball, glowSprite, ballGlowColor, 38);
            glow.transform.localScale = Vector3.one * 1.9f;
            ballGlow = glow.GetComponent<SpriteRenderer>();
            var body = MakeSprite("BallBody", ball, sphereSprite, ballColor, 42);
            body.transform.localScale = Vector3.one * .48f;
            ballRenderer = body.GetComponent<SpriteRenderer>();
            var highlight = MakeSprite("BallSpecular", ball, glowSprite, new Color(1f, 1f, 1f, .72f), 43);
            highlight.transform.localPosition = new Vector3(-.07f, .085f, -.02f);
            highlight.transform.localScale = new Vector3(.22f, .15f, 1f);

            ballTrail = ballObject.AddComponent<TrailRenderer>();
            ballTrail.material = trailMaterial;
            ballTrail.time = .42f;
            ballTrail.minVertexDistance = .035f;
            ballTrail.startWidth = .27f;
            ballTrail.endWidth = .015f;
            ballTrail.sortingOrder = 40;
            ballTrail.numCapVertices = 4;
            ballTrail.numCornerVertices = 3;
            ballTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(.8f, .86f, 1f), 0f), new GradientColorKey(new Color(.4f, .83f, 1f), 1f) },
                new[] { new GradientAlphaKey(.74f, 0f), new GradientAlphaKey(0f, 1f) });
            ballTrail.colorGradient = gradient;
            ballTrail.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            ballTrail.emitting = false;
        }

        void BuildWavePool()
        {
            for (int i = 0; i < wavePool.Length; i++)
            {
                var wave = MakeSprite("MenuTravelWave_" + i, worldRoot.transform, ringSprite, Color.white, 36);
                wavePool[i] = new MenuWave(wave.transform, wave.GetComponent<SpriteRenderer>());
                wavePool[i].Hide();
            }
        }

        void BuildAmbientMagic()
        {
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2f / 16f;
                float radius = 3.2f + (i % 4) * .58f;
                var particle = MakeSprite("AmbientStar", worldRoot.transform, i % 3 == 0 ? diamondSprite : glowSprite,
                    new Color(.94f, .97f, 1f, .54f), 34);
                particle.transform.position = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .66f, -.15f);
                float scale = i % 3 == 0 ? .12f : .085f;
                particle.transform.localScale = Vector3.one * scale;
            }
        }

        void BindDestinationPanels()
        {
            continuePanel = FindDestinationPanel("ContinueDestinationPanel");
            exitPanel = FindDestinationPanel("ExitDestinationPanel");
            if (!continuePanel || !exitPanel)
            {
                Debug.LogError("MainMenu scene needs editable ContinueDestinationPanel and ExitDestinationPanel objects under its Canvas.", this);
                enabled = false;
                return;
            }

            var hasSave = System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, "reflectable_run.json"));
            var saveStatus = continuePanel.transform.Find("Card/SaveStatusText")?.GetComponent<Text>();
            if (saveStatus) saveStatus.text = hasSave ? "A saved run is ready." : "No saved run found yet.";
            BindDestinationButton(continuePanel, "ContinueRunButton", hasSave ? "CONTINUE RUN" : "NO SAVED RUN", !hasSave, () => menuController?.ContinueRunNow());
            BindDestinationButton(continuePanel, "BackButton", "BACK", false, () => ReturnToMainMenu());
            continuePanel.SetActive(false);

            BindDestinationButton(exitPanel, "ExitBackButton", "BACK", false, () => ReturnToMainMenu());
            exitPanel.SetActive(false);
        }

        GameObject FindDestinationPanel(string panelName)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (!canvas) return null;
            var panel = canvas.transform.Find(panelName);
            return panel ? panel.gameObject : null;
        }

        void BindDestinationButton(GameObject panel, string buttonName, string label, bool disabled, Action clicked)
        {
            var buttonTransform = panel.transform.Find("Card/" + buttonName);
            if (!buttonTransform) return;
            var button = buttonTransform.GetComponent<Button>();
            if (!button) return;
            button.interactable = !disabled;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => clicked?.Invoke());
            button.onClick.AddListener(MenuSettingsAudioMockup.PlayButton);
            var text = button.GetComponentInChildren<Text>(true);
            if (text) text.text = label;
        }

        IEnumerator TravelSequence(MenuDestination destination, bool returning)
        {
            SetMenuButtons(false);
            if (returning)
            {
                State = MenuNavigationState.ReturningToStart;
                if (continuePanel) continuePanel.SetActive(false);
                if (exitPanel) exitPanel.SetActive(false);
                menuController.PrepareForReturnTravel();
                if (!worldRoot.activeSelf) worldRoot.SetActive(true);
                cameraFollow.Follow(ball, travelCameraSize, Vector3.up * cameraFollowOffsetY);
            }
            else
            {
                State = MenuNavigationState.LaunchingBall;
                if (menuController.MainMenuPanel) menuController.MainMenuPanel.SetActive(false);
                cameraFollow.Follow(ball, travelCameraSize, Vector3.up * cameraFollowOffsetY);
                CurrentDestination = destination;
            }

            if (ballTrail) { ballTrail.Clear(); ballTrail.emitting = true; }
            MenuSettingsAudioMockup.PlayBallShot();
            PlayWave(ball.position, new Color(.7f, .89f, 1f, .76f), .22f, .24f);

            Transform targetTransform = returning ? null : landingTargets.TryGetValue(destination, out var foundTarget) ? foundTarget : null;
            Vector3 destinationPosition = returning ? startPosition : targetTransform ? targetTransform.position : startPosition;
            Vector3 origin = ball.position;
            Vector3 delta = destinationPosition - origin;
            Vector3 controlA = origin + delta * .28f + Vector3.up * arcHeight;
            Vector3 controlB = destinationPosition - delta * .22f + Vector3.up * arcHeight;
            float elapsed = 0f;
            float nextWave = Time.unscaledTime;
            float nextParticle = Time.unscaledTime;
            Vector3 previous = origin;
            State = returning ? MenuNavigationState.ReturningToStart : MenuNavigationState.FollowingBall;

            while (elapsed < travelDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / travelDuration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                Vector3 point = CubicBezier(origin, controlA, controlB, destinationPosition, eased);
                ball.position = point;
                float distance = Vector3.Distance(previous, point);
                spinDegrees -= distance / (.48f * Mathf.PI) * 360f;
                ball.rotation = Quaternion.Euler(0f, 0f, spinDegrees);
                previous = point;
                if (Time.unscaledTime >= nextWave)
                {
                    PlayWave(point, new Color(.69f, .88f, 1f, .62f), .16f, .27f);
                    nextWave = Time.unscaledTime + .13f;
                }
                if (Time.unscaledTime >= nextParticle)
                {
                    EmitTravelParticle(point);
                    nextParticle = Time.unscaledTime + .075f;
                }
                yield return null;
            }

            ball.position = destinationPosition;
            ball.rotation = Quaternion.Euler(0f, 0f, spinDegrees);
            if (ballTrail) ballTrail.emitting = false;
            State = MenuNavigationState.ArrivingAtDestination;
            PlayWave(destinationPosition, new Color(1f, .95f, .79f, .94f), .38f, .42f);
            yield return new WaitForSecondsRealtime(destinationSettleTime);
            yield return SettleCamera(destinationPosition + Vector3.up * cameraFollowOffsetY);

            if (returning)
            {
                cameraFollow.MoveTo(initialCameraPosition, overviewCameraSize);
                yield return SettleCamera(initialCameraPosition);
                cameraFollow.SnapTo(initialCameraPosition, overviewCameraSize);
                menuController.ShowMainMenuAfterTravel();
                State = MenuNavigationState.MainMenu;
                SetMenuButtons(true);
                yield break;
            }

            State = destination == MenuDestination.Exit ? MenuNavigationState.ExitSequence : MenuNavigationState.DestinationMenu;
            switch (destination)
            {
                case MenuDestination.Play:
                    menuController.OpenStageSelectAfterTravel();
                    yield return ShowPanel(menuController.StageSelectPanel);
                    break;
                case MenuDestination.Settings:
                    menuController.OpenSettingsAfterTravel();
                    yield return ShowPanel(menuController.SettingsPanel);
                    break;
                case MenuDestination.Continue:
                    if (continuePanel) { continuePanel.transform.SetAsLastSibling(); yield return ShowPanel(continuePanel); }
                    break;
                case MenuDestination.Exit:
                    if (exitPanel) { exitPanel.transform.SetAsLastSibling(); yield return ShowPanel(exitPanel); }
                    if (ExitIsMocked) State = MenuNavigationState.DestinationMenu;
                    else { State = MenuNavigationState.Exiting; Application.Quit(); }
                    break;
            }
        }

        IEnumerator SettleCamera(Vector3 destination)
        {
            float elapsed = 0f;
            while (elapsed < cameraSettleTimeout)
            {
                if (Vector2.Distance(sceneCamera.transform.position, destination) < .055f) break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        static bool ExitIsMocked => Application.isEditor || Application.platform == RuntimePlatform.WebGLPlayer;

        IEnumerator ShowPanel(GameObject panel)
        {
            if (!panel) yield break;
            panel.SetActive(true);
            var group = panel.GetComponent<CanvasGroup>() ?? panel.AddComponent<CanvasGroup>();
            var rect = panel.transform as RectTransform;
            if (rect) rect.localScale = Vector3.one * .96f;
            for (float time = 0f; time < .24f; time += Time.unscaledDeltaTime)
            {
                float p = Mathf.SmoothStep(0f, 1f, time / .24f);
                group.alpha = p;
                if (rect) rect.localScale = Vector3.LerpUnclamped(Vector3.one * .96f, Vector3.one, p);
                yield return null;
            }
            group.alpha = 1f;
            if (rect) rect.localScale = Vector3.one;
        }

        void SetMenuButtons(bool interactable)
        {
            for (int i = 0; i < menuButtons.Count; i++)
                if (menuButtons[i]) menuButtons[i].interactable = interactable;
        }

        float OverviewSizeForAspect()
        {
            float maxX = Mathf.Abs(startPosition.x - initialCameraPosition.x) + 2f;
            float maxY = Mathf.Abs(startPosition.y - initialCameraPosition.y) + 2f;
            if (destinations != null)
                foreach (var layout in destinations)
                    if (layout != null && layout.island)
                    {
                        maxX = Mathf.Max(maxX, Mathf.Abs(layout.island.position.x - initialCameraPosition.x) + layout.size.x * .5f);
                        maxY = Mathf.Max(maxY, Mathf.Abs(layout.island.position.y - initialCameraPosition.y) + layout.size.y * .5f);
                    }
            return Mathf.Max(6.2f, (maxX + .6f) / Mathf.Max(.35f, sceneCamera.aspect), maxY + .85f);
        }

        void LateUpdate()
        {
            if (!worldBuilt || !sceneCamera) return;
            if (State == MenuNavigationState.MainMenu)
                cameraFollow.MoveTo(initialCameraPosition, OverviewSizeForAspect());
            FitResponsiveBackgroundCover();
            for (int i = 0; i < wavePool.Length; i++) wavePool[i]?.Tick(Time.unscaledDeltaTime);
            for (int i = 0; i < particlePool.Length; i++) particlePool[i]?.Tick(Time.unscaledDeltaTime);
            if (ballGlow && ballRenderer)
            {
                float pulse = .04f * Mathf.Sin(Time.unscaledTime * 3f);
                ballGlow.transform.localScale = Vector3.one * (1.9f + pulse);
                ballRenderer.transform.localScale = Vector3.one * (.48f + pulse * .14f);
            }
        }

        void FitResponsiveBackgroundCover()
        {
            if (!responsiveBackgroundCover || !sceneCamera || responsiveBackgroundSpriteSize.x <= 0f || responsiveBackgroundSpriteSize.y <= 0f)
                return;

            bool portrait = sceneCamera.aspect < .8f;
            if (landscapeBackgroundArt) landscapeBackgroundArt.enabled = !portrait;

            float viewWidth = sceneCamera.orthographicSize * 2f * sceneCamera.aspect;
            float viewHeight = sceneCamera.orthographicSize * 2f;
            float scaleX = Mathf.Max(.001f, Mathf.Abs(responsiveBackgroundBaseScale.x));
            float scaleY = Mathf.Max(.001f, Mathf.Abs(responsiveBackgroundBaseScale.y));
            float coverScale = Mathf.Max(viewWidth / (responsiveBackgroundSpriteSize.x * scaleX), viewHeight / (responsiveBackgroundSpriteSize.y * scaleY));
            responsiveBackgroundCover.transform.localScale = responsiveBackgroundBaseScale * coverScale;
            Vector3 coverPosition = responsiveBackgroundBasePosition;
            coverPosition.x = sceneCamera.transform.position.x;
            coverPosition.y = sceneCamera.transform.position.y;
            responsiveBackgroundCover.transform.position = coverPosition;
        }

        void PlayWave(Vector3 position, Color color, float size, float lifetime)
        {
            if (wavePool.Length == 0) return;
            var wave = wavePool[waveCursor++ % wavePool.Length];
            if (wave != null) wave.Play(position, color, size, lifetime);
        }

        void EmitTravelParticle(Vector3 position)
        {
            if (particlePool.Length == 0) return;
            Vector2 jitter = UnityEngine.Random.insideUnitCircle * .18f;
            var particle = particlePool[particleCursor++ % particlePool.Length];
            if (particle == null) return;
            particle.Play(position + new Vector3(jitter.x, jitter.y, -.04f), new Color(.69f, .86f, 1f, .82f), UnityEngine.Random.Range(.055f, .12f));
        }

        void BuildParticlePool()
        {
            for (int i = 0; i < particlePool.Length; i++)
            {
                var particle = MakeSprite("MenuTravelParticle_" + i, worldRoot.transform, i % 4 == 0 ? diamondSprite : glowSprite, Color.white, 37);
                particlePool[i] = new MenuParticle(particle.transform, particle.GetComponent<SpriteRenderer>());
                particlePool[i].Hide();
            }
        }

        GameObject MakeSprite(string objectName, Transform parent, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = color; renderer.sharedMaterial = spriteMaterial; renderer.sortingOrder = order;
            return go;
        }

        Sprite CreateSolidSprite(string spriteName, Color color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = spriteName + "Texture", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(new[] { color, color, color, color }); texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 2f);
        }

        Sprite CreateRadialSprite(string spriteName, bool ring)
        {
            const int dimension = 64;
            var texture = new Texture2D(dimension, dimension, TextureFormat.RGBA32, false) { name = spriteName + "Texture", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[dimension * dimension];
            Vector2 center = Vector2.one * (dimension - 1) * .5f;
            float radius = dimension * .47f;
            for (int y = 0; y < dimension; y++)
                for (int x = 0; x < dimension; x++)
                {
                    float r = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = ring ? Mathf.Clamp01(1f - Mathf.Abs(r - .73f) * 11f) : Mathf.Clamp01(1f - r);
                    pixels[y * dimension + x] = new Color(1f, 1f, 1f, alpha * alpha);
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, dimension, dimension), Vector2.one * .5f, dimension);
        }

        Sprite CreateSphereSprite()
        {
            const int dimension = 64;
            var texture = new Texture2D(dimension, dimension, TextureFormat.RGBA32, false) { name = "Menu Magic Ball Texture", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[dimension * dimension];
            Vector2 center = Vector2.one * (dimension - 1) * .5f;
            for (int y = 0; y < dimension; y++)
                for (int x = 0; x < dimension; x++)
                {
                    Vector2 uv = (new Vector2(x, y) - center) / (dimension * .48f);
                    float radius = uv.magnitude;
                    float alpha = Mathf.Clamp01((1.02f - radius) * 24f);
                    float light = Mathf.Clamp01(1f - Vector2.Distance(uv, new Vector2(-.3f, .38f)) * 1.2f);
                    pixels[y * dimension + x] = Color.Lerp(Color.white, new Color(.68f, .81f, 1f), Mathf.Clamp01(radius * .52f)) * new Color(1f, 1f, 1f, alpha);
                    pixels[y * dimension + x] = new Color(Mathf.Lerp(pixels[y * dimension + x].r, 1f, light * .42f), Mathf.Lerp(pixels[y * dimension + x].g, 1f, light * .42f), Mathf.Lerp(pixels[y * dimension + x].b, 1f, light * .42f), alpha);
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, dimension, dimension), Vector2.one * .5f, dimension);
        }

        Sprite CreateDiamondSprite()
        {
            const int dimension = 32;
            var texture = new Texture2D(dimension, dimension, TextureFormat.RGBA32, false) { name = "Menu Crystal Texture", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[dimension * dimension];
            Vector2 center = Vector2.one * (dimension - 1) * .5f;
            for (int y = 0; y < dimension; y++)
                for (int x = 0; x < dimension; x++)
                {
                    float edge = 1f - Mathf.Abs(x - center.x) / 15f - Mathf.Abs(y - center.y) / 15f;
                    float alpha = Mathf.SmoothStep(0f, .18f, edge);
                    pixels[y * dimension + x] = new Color(1f, 1f, 1f, alpha);
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, dimension, dimension), Vector2.one * .5f, dimension);
        }

        static Vector3 CubicBezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        static GameObject FindNamedObject(UnityEngine.SceneManagement.Scene scene, string objectName)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (var child in transforms) if (child.name == objectName) return child.gameObject;
            }
            return null;
        }

        void OnDestroy()
        {
            DestroyRuntimeSprite(squareSprite, islandSurfaceSprite);
            DestroyRuntimeSprite(glowSprite, null);
            DestroyRuntimeSprite(ringSprite, null);
            DestroyRuntimeSprite(sphereSprite, ballSprite);
            DestroyRuntimeSprite(diamondSprite, null);
            if (spriteMaterial) Destroy(spriteMaterial);
            if (trailMaterial) Destroy(trailMaterial);
        }

        static void DestroyRuntimeSprite(Sprite sprite, Sprite supplied)
        {
            if (!sprite || sprite == supplied) return;
            var texture = sprite.texture;
            Destroy(sprite);
            if (texture) Destroy(texture);
        }

        sealed class MenuWave
        {
            readonly Transform transform;
            readonly SpriteRenderer renderer;
            Color tint;
            float age, lifetime, startSize;
            bool playing;

            public MenuWave(Transform transform, SpriteRenderer renderer) { this.transform = transform; this.renderer = renderer; }
            public void Hide() { playing = false; if (transform) transform.gameObject.SetActive(false); }
            public void Play(Vector3 position, Color color, float size, float duration)
            {
                if (!transform || !renderer) return;
                transform.gameObject.SetActive(true); transform.position = position; transform.localScale = Vector3.one * size;
                tint = color; renderer.color = color; age = 0f; lifetime = Mathf.Max(.08f, duration); startSize = size; playing = true;
            }
            public void Tick(float deltaTime)
            {
                if (!playing || !transform || !renderer) return;
                age += deltaTime;
                float p = Mathf.Clamp01(age / lifetime);
                transform.localScale = Vector3.one * Mathf.Lerp(startSize, startSize * 3.8f, Mathf.SmoothStep(0f, 1f, p));
                var color = tint; color.a = tint.a * (1f - p); renderer.color = color;
                if (age >= lifetime) Hide();
            }
        }

        sealed class MenuParticle
        {
            readonly Transform transform;
            readonly SpriteRenderer renderer;
            Color tint;
            Vector3 drift;
            float age, lifetime, startSize;
            bool playing;

            public MenuParticle(Transform transform, SpriteRenderer renderer) { this.transform = transform; this.renderer = renderer; }
            public void Hide() { playing = false; if (transform) transform.gameObject.SetActive(false); }
            public void Play(Vector3 position, Color color, float size)
            {
                if (!transform || !renderer) return;
                transform.gameObject.SetActive(true); transform.position = position; transform.localScale = Vector3.one * size;
                tint = color; renderer.color = color; age = 0f; lifetime = UnityEngine.Random.Range(.28f, .52f); startSize = size;
                drift = new Vector3(UnityEngine.Random.Range(-.35f, .35f), UnityEngine.Random.Range(.35f, .85f), 0f); playing = true;
            }
            public void Tick(float deltaTime)
            {
                if (!playing || !transform || !renderer) return;
                age += deltaTime;
                float p = Mathf.Clamp01(age / lifetime);
                transform.position += drift * deltaTime;
                transform.localScale = Vector3.one * Mathf.Lerp(startSize, startSize * .2f, p);
                var color = tint; color.a = tint.a * (1f - p); renderer.color = color;
                if (age >= lifetime) Hide();
            }
        }
    }
}
