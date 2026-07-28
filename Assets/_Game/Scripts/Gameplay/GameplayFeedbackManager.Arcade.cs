using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Reflectable
{
    public sealed partial class GameplayFeedbackManager
    {
        const int ShockwavePoolSize = 32;
        const int DebrisPoolSize = 96;
        const int PopupPoolSize = 48;
        const int BeamPoolSize = 16;
        const int UiParticlePoolSize = 80;
        const int EnergyParticlePoolSize = 48;

        [Header("Persistent arcade layers")]
        [SerializeField] Transform effectsLayer;
        [SerializeField] Transform damageNumberLayer;
        [SerializeField] ComboOrbController comboOrb;
        [SerializeField] ComboWorldReactionController worldReaction;
        [System.NonSerialized] AudioSource voiceSource;
        [System.NonSerialized] AudioClip combo30Voice;
        [System.NonSerialized] AudioClip combo100Voice;
        [System.NonSerialized] AudioClip combo200Voice;
        [System.NonSerialized] AudioSource[] highComboMusicLayers = new AudioSource[0];

        readonly Dictionary<SpriteRenderer, Color> stageColors = new Dictionary<SpriteRenderer, Color>();
        ArcadeSpriteFx[] shockwaves;
        ArcadeSpriteFx[] debris;
        ArcadeTextFx[] popups;
        ArcadeBeamFx[] beams;
        ArcadeUiParticle[] uiParticles;
        ComboEnergyParticleFx[] energyParticles;
        readonly HashSet<ReflectableProjectile> activeProjectiles = new HashSet<ReflectableProjectile>();
        ParticleSystem sparkSystem;
        Text announcer;
        Text[] comboGhosts;
        Image screenFlash;
        ArcadeRainbowTextEffect rainbow;
        ArcadeCharacterReaction characterReaction;
        PlayerCharacterPresenter characterPresenter;
        ComboPresentationController comboPresentation;
        Sprite circleSprite;
        Sprite ringSprite;
        Material additiveMaterial;
        Material uiAdditiveMaterial;
        Material lineMaterial;
        AudioSource arcadeAudioSource;
        AudioSource ambienceSource;
        VolumeProfile runtimeProfile;
        Bloom bloom;
        ColorAdjustments colorAdjustments;
        Coroutine comboPunch;
        Coroutine comboBreak;
        Coroutine announceRoutine;
        Coroutine hitStopRoutine;
        int shockwaveCursor;
        int popupCursor;
        int beamCursor;
        int uiCursor;
        int energyCursor;
        int currentCombo;
        int lastAnnouncedMilestone;
        float fireTimer;
        float ambientTimer;
        float characterSparkTimer;
        bool arcadeInitialized;
        ArcadeEffectQuality? previewQuality;

        public ArcadeEffectQuality EffectQuality => previewQuality ?? (comboPresentation ? comboPresentation.Quality : ArcadeEffectQuality.High);

        public void BindCharacter(PlayerCharacterPresenter presenter)
        {
            characterPresenter = presenter;
            if (comboPresentation) comboPresentation.BindCharacter(presenter);
            RefreshCharacterReaction();
        }

        public void ConfigureVoiceHooks(AudioSource source, AudioClip at30, AudioClip at100, AudioClip at200)
        {
            voiceSource = source;
            combo30Voice = at30;
            combo100Voice = at100;
            combo200Voice = at200;
        }

        public void ConfigureMusicLayers(params AudioSource[] layers)
        {
            highComboMusicLayers = layers ?? new AudioSource[0];
        }

        public void BindStageVisual(GameObject stageVisual)
        {
            ResetStageColors();
            stageColors.Clear();
            if (!stageVisual) return;
            foreach (var renderer in stageVisual.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer) stageColors[renderer] = renderer.color;
        }

        void InitializeArcadeFeedback()
        {
            if (!gameCamera)
            {
                enabled = false;
                Debug.LogError("GameplayFeedbackManager requires a serialized Game Camera reference.", this);
                return;
            }

            cameraBase = gameCamera.transform.localPosition;
            BuildRuntimeAssets();
            BuildPools();
            comboPresentation = FindFirstObjectByType<ComboPresentationController>(FindObjectsInactive.Include);
            if (comboPresentation)
            {
                comboPresentation.BindCharacter(characterPresenter);
                if (comboLabel) comboLabel.gameObject.SetActive(false);
            }
            else BuildComboUi();
            var config = comboPresentation ? comboPresentation.Config : null;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (comboOrb) comboOrb.Initialize(config, circleSprite, ringSprite, lineMaterial, font);
            if (worldReaction) worldReaction.Initialize(config, circleSprite, ringSprite, lineMaterial);
            BuildGpuParticles();
            BuildPostProcessing();
            SetComboPresentation(0, true);
            arcadeInitialized = true;
        }

        void Update()
        {
            if (!arcadeInitialized) return;
            UpdateEscalation();
            UpdateMusicLayers();
            UpdateCharacterSparks();
        }

        public void Hit(int combo, Vector3 position, int damage, bool critical, ArcadeHitKind kind, bool destroyed)
        {
            if (comboBreak != null) { StopCoroutine(comboBreak); comboBreak = null; }
            currentCombo = Mathf.Max(0, combo);
            RefreshCharacterReaction();
            if (characterReaction) characterReaction.SetCombo(currentCombo);
            if (comboPresentation) comboPresentation.SetCombo(currentCombo);
            else
            {
                SetComboPresentation(currentCombo, false);
                PunchArcadeCombo(currentCombo);
            }
            ApplyProjectileEvolution();
            if (comboOrb && comboOrb.IsSummoned)
            {
                ComboTierSettings orbTier = comboPresentation && comboPresentation.Config
                    ? comboPresentation.Config.TierFor(currentCombo)
                    : default;
                comboOrb.SetCombo(currentCombo);
                Shockwave(comboOrb.WorldPosition, orbTier.secondaryColor, .22f + ComboIntensity(currentCombo) * .18f);
                EmitSparks(comboOrb.WorldPosition, orbTier.primaryColor, QualityCount(2 + Mathf.Min(4, currentCombo / 150)), 1.15f);
            }
            worldReaction?.SetCombo(currentCombo);

            Color hitColor = HitColor(kind, critical);
            if (!critical && kind == ArcadeHitKind.Direct && comboPresentation && comboPresentation.Config)
                hitColor = currentCombo >= 1000 ? RainbowColor(Time.unscaledTime) : comboPresentation.Config.TierFor(currentCombo).primaryColor;
            EmitImpact(position, hitColor, critical || destroyed, kind);
            if (damage > 0) Popup(position, damage, hitColor, critical, kind);

            float intensity = ComboIntensity(currentCombo);
            ComboMilestoneSettings milestone = default;
            bool milestoneHit = comboPresentation && comboPresentation.Config && comboPresentation.Config.TryGetMilestone(currentCombo, out milestone);
            if (milestoneHit)
            {
                ComboTierSettings tier = comboPresentation.Config.TierFor(currentCombo);
                comboOrb?.ShowMilestone(milestone.announcement, tier.secondaryColor);
            }
            PlayConfiguredAudio(critical, destroyed, milestoneHit, milestoneHit && currentCombo >= 1000);
            if (milestoneHit && milestone.characterCutIn) PlayCharacterVoice(currentCombo);
            if (milestoneHit && milestone.cameraShake > 0f) Shake(milestone.cameraShake, .09f);
            Flash(hitColor, Mathf.Lerp(.025f, .13f, intensity) * (critical || destroyed ? 1.7f : 1f));
            if (milestoneHit && milestone.hitStop > 0f) HitStop(milestone.hitStop, false);
            if (!comboPresentation) CheckAnnouncement(currentCombo);
            CheckVoiceCue(currentCombo);
            if (milestoneHit && currentCombo >= 500) TriggerSpectacle(position);
        }

        public void Destroyed(Vector3 position, int combo, ArcadeHitKind kind)
        {
            ComboPresentationConfig config = comboPresentation ? comboPresentation.Config : null;
            ComboTierSettings tier = config ? config.TierFor(combo) : default;
            Color color = config ? tier.primaryColor : HitColor(kind, false);
            if (comboOrb && !comboOrb.IsSummoned)
            {
                comboOrb.Summon(position, combo);
                int formationCount = QualityCount(config ? Mathf.Clamp(tier.particleCount + 6, 10, 28) : 12);
                float formationDuration = config ? config.orbFormationDuration : .32f;
                for (int i = 0; i < formationCount; i++)
                {
                    Vector2 direction = Random.insideUnitCircle.normalized;
                    Vector3 origin = position + (Vector3)(direction * Random.Range(.35f, .92f));
                    energyParticles[energyCursor++ % energyParticles.Length].Play(
                        origin,
                        position,
                        Color.Lerp(tier.primaryColor, tier.secondaryColor, Random.value),
                        formationDuration + Random.Range(-.035f, .05f),
                        Random.Range(.07f, .14f));
                }
                Shockwave(position, color, .58f);
                EmitSparks(position, color, QualityCount(7), 2.2f);
            }
            else if (comboOrb && comboOrb.IsSummoned)
            {
                int count = QualityCount(config ? config.energyParticlesPerBlock : 2);
                for (int i = 0; i < count; i++)
                    energyParticles[energyCursor++ % energyParticles.Length].Play(
                        position + (Vector3)Random.insideUnitCircle * .14f,
                        comboOrb.WorldPosition,
                        Color.Lerp(tier.primaryColor, tier.secondaryColor, Random.value),
                        (config ? config.energyTravelDuration : .42f) + Random.Range(-.06f, .08f),
                        Random.Range(.08f, .15f));
                comboOrb.AbsorbPulse(tier.secondaryColor);
            }
            EmitSparks(position, color, 18 + Mathf.Min(28, combo / 10), 3.8f + ComboIntensity(combo) * 3f);
            Shockwave(position, color, .75f + ComboIntensity(combo) * .7f);
            Flash(color, .08f + ComboIntensity(combo) * .09f);
        }

        public void Ricochet(Vector3 position, int combo)
        {
            Color color = combo >= 200 ? RainbowColor(Time.unscaledTime * .5f) : new Color(.55f, .88f, 1f, 1f);
            EmitSparks(position, color, 4 + Mathf.Min(8, combo / 30), 2.3f);
            if (combo >= 30) Shockwave(position, color, .28f + ComboIntensity(combo) * .22f);
        }

        public void Beam(Vector3 start, Vector3 end, int combo)
        {
            Color color = combo >= 200 ? RainbowColor(Time.unscaledTime) : new Color(.35f, .9f, 1f, 1f);
            beams[beamCursor++ % beams.Length].Play(start, end, color, Mathf.Lerp(.08f, .2f, ComboIntensity(combo)), .22f);
            EmitSparks(end, color, 10, 3f);
            Flash(color, .045f + ComboIntensity(combo) * .05f);
        }

        public void ComboEnded(int previousCombo)
        {
            if (previousCombo <= 0 && currentCombo <= 0) return;
            currentCombo = 0;
            lastAnnouncedMilestone = 0;
            if (characterReaction) characterReaction.SetCombo(0);
            if (comboOrb && comboOrb.IsSummoned)
            {
                Vector3 orbPosition = comboOrb.WorldPosition;
                EmitSparks(orbPosition, RainbowColor(Time.unscaledTime), QualityCount(30), 4.5f);
                Shockwave(orbPosition, Color.white, 1.1f);
                comboOrb.Despawn();
            }
            worldReaction?.SetCombo(0);
            foreach (var projectile in activeProjectiles) if (projectile) projectile.ResetComboVisual();
            if (uiParticles != null) foreach (var particle in uiParticles) if (particle) particle.gameObject.SetActive(false);
            if (comboPunch != null) { StopCoroutine(comboPunch); comboPunch = null; }
            if (comboPresentation) comboPresentation.ResetCombo();
            else
            {
                if (comboBreak != null) StopCoroutine(comboBreak);
                comboBreak = StartCoroutine(ComboBreakRoutine(previousCombo));
            }
        }

        public void RegisterProjectile(ReflectableProjectile projectile)
        {
            if (!projectile) return;
            activeProjectiles.Add(projectile);
            projectile.ConfigureComboVisual(circleSprite, ringSprite, lineMaterial);
            if (currentCombo > 0) projectile.ApplyComboVisual(currentCombo, CurrentTier());
        }

        public void UnregisterProjectile(ReflectableProjectile projectile)
        {
            if (projectile) activeProjectiles.Remove(projectile);
        }

        void ApplyProjectileEvolution()
        {
            ComboTierSettings tier = CurrentTier();
            activeProjectiles.RemoveWhere(item => !item);
            foreach (var projectile in activeProjectiles)
                projectile.ApplyComboVisual(currentCombo, tier);
        }

        ComboTierSettings CurrentTier() =>
            comboPresentation && comboPresentation.Config ? comboPresentation.Config.TierFor(currentCombo) : default;

        void StartArcadeShake(float strength, float duration)
        {
            if (shake != null) StopCoroutine(shake);
            shake = StartCoroutine(ShakeRoutine(Mathf.Min(.28f, strength), duration));
        }

        void BuildRuntimeAssets()
        {
            circleSprite = BuildSprite(false);
            ringSprite = BuildSprite(true);
            var additiveShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default");
            additiveMaterial = new Material(additiveShader) { name = "Runtime Arcade Additive" };
            additiveMaterial.mainTexture = circleSprite.texture;
            if (additiveMaterial.HasProperty("_Surface")) additiveMaterial.SetFloat("_Surface", 1f);
            if (additiveMaterial.HasProperty("_Blend")) additiveMaterial.SetFloat("_Blend", 1f);
            uiAdditiveMaterial = new Material(Shader.Find("UI/Default")) { name = "Runtime Arcade UI Additive" };
            if (uiAdditiveMaterial.HasProperty("_SrcBlend")) uiAdditiveMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            if (uiAdditiveMaterial.HasProperty("_DstBlend")) uiAdditiveMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            lineMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Runtime Arcade Line" };
        }

        static Sprite BuildSprite(bool ring)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = ring ? "Runtime Arcade Ring" : "Runtime Arcade Glow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[size * size];
            Vector2 center = Vector2.one * (size - 1) * .5f;
            float radius = size * .48f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float normalized = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = ring
                        ? Mathf.Clamp01(1f - Mathf.Abs(normalized - .72f) * 12f)
                        : Mathf.Clamp01(1f - normalized);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * alpha);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 64f);
        }

        void BuildPools()
        {
            EnsurePersistentLayers();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            shockwaves = new ArcadeSpriteFx[ShockwavePoolSize];
            for (int i = 0; i < shockwaves.Length; i++)
            {
                var item = new GameObject("Shockwave_" + i).AddComponent<ArcadeSpriteFx>();
                item.transform.SetParent(effectsLayer, false);
                item.Initialize(ringSprite, lineMaterial, 500);
                shockwaves[i] = item;
            }
            debris = new ArcadeSpriteFx[DebrisPoolSize];
            for (int i = 0; i < debris.Length; i++)
            {
                var item = new GameObject("FlyingFragment_" + i).AddComponent<ArcadeSpriteFx>();
                item.transform.SetParent(effectsLayer, false);
                item.Initialize(circleSprite, lineMaterial, 501);
                debris[i] = item;
            }
            popups = new ArcadeTextFx[PopupPoolSize];
            for (int i = 0; i < popups.Length; i++)
            {
                var item = new GameObject("DamagePopup_" + i).AddComponent<ArcadeTextFx>();
                item.transform.SetParent(damageNumberLayer, false);
                item.Initialize(font);
                popups[i] = item;
            }
            beams = new ArcadeBeamFx[BeamPoolSize];
            for (int i = 0; i < beams.Length; i++)
            {
                var item = new GameObject("Beam_" + i).AddComponent<ArcadeBeamFx>();
                item.transform.SetParent(effectsLayer, false);
                item.Initialize(lineMaterial);
                beams[i] = item;
            }
            energyParticles = new ComboEnergyParticleFx[EnergyParticlePoolSize];
            for (int i = 0; i < energyParticles.Length; i++)
            {
                var item = new GameObject("ComboEnergy_" + i).AddComponent<ComboEnergyParticleFx>();
                item.transform.SetParent(effectsLayer, false);
                item.Initialize(circleSprite, lineMaterial);
                energyParticles[i] = item;
            }
        }

        void EnsurePersistentLayers()
        {
            if (!effectsLayer)
            {
                effectsLayer = transform.Find("EffectsLayer");
                if (!effectsLayer)
                {
                    effectsLayer = new GameObject("EffectsLayer").transform;
                    effectsLayer.SetParent(transform, false);
                }
            }
            if (!damageNumberLayer)
            {
                damageNumberLayer = transform.Find("DamageNumberLayer");
                if (!damageNumberLayer)
                {
                    damageNumberLayer = new GameObject("DamageNumberLayer").transform;
                    damageNumberLayer.SetParent(transform, false);
                }
            }
        }

        void BuildComboUi()
        {
            if (!comboLabel) return;
            comboLabel.fontSize = 76;
            comboLabel.fontStyle = FontStyle.Bold;
            comboLabel.alignment = TextAnchor.MiddleCenter;
            comboLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            comboLabel.verticalOverflow = VerticalWrapMode.Overflow;
            comboLabel.raycastTarget = false;
            comboLabel.rectTransform.sizeDelta = new Vector2(980f, 190f);
            var outline = comboLabel.GetComponent<Outline>() ?? comboLabel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.15f, .04f, .24f, .9f);
            outline.effectDistance = new Vector2(4f, -4f);
            rainbow = comboLabel.GetComponent<ArcadeRainbowTextEffect>() ?? comboLabel.gameObject.AddComponent<ArcadeRainbowTextEffect>();

            Canvas canvasComponent = comboLabel.GetComponentInParent<Canvas>();
            if (!canvasComponent) return;
            Transform canvas = canvasComponent.transform;
            var flashObject = new GameObject("ArcadeScreenFlash", typeof(RectTransform), typeof(Image));
            flashObject.transform.SetParent(canvas, false);
            flashObject.transform.SetAsLastSibling();
            var flashRect = flashObject.GetComponent<RectTransform>();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = flashRect.offsetMax = Vector2.zero;
            screenFlash = flashObject.GetComponent<Image>();
            screenFlash.raycastTarget = false;
            screenFlash.color = Color.clear;

            comboGhosts = new Text[2];
            for (int i = 0; i < comboGhosts.Length; i++)
            {
                var ghostObject = new GameObject("ComboMotionGhost_" + i, typeof(RectTransform), typeof(Text));
                ghostObject.transform.SetParent(comboLabel.transform.parent, false);
                ghostObject.transform.SetSiblingIndex(comboLabel.transform.GetSiblingIndex());
                var ghostRect = ghostObject.GetComponent<RectTransform>();
                CopyRect(comboLabel.rectTransform, ghostRect);
                var ghost = ghostObject.GetComponent<Text>();
                ghost.font = comboLabel.font;
                ghost.fontSize = comboLabel.fontSize;
                ghost.fontStyle = FontStyle.Bold;
                ghost.alignment = TextAnchor.MiddleCenter;
                ghost.horizontalOverflow = HorizontalWrapMode.Overflow;
                ghost.verticalOverflow = VerticalWrapMode.Overflow;
                ghost.raycastTarget = false;
                ghost.color = Color.clear;
                comboGhosts[i] = ghost;
            }

            var announceObject = new GameObject("ComboAnnouncer", typeof(RectTransform), typeof(Text), typeof(Outline));
            announceObject.transform.SetParent(canvas, false);
            announceObject.transform.SetAsLastSibling();
            var announceRect = announceObject.GetComponent<RectTransform>();
            announceRect.anchorMin = announceRect.anchorMax = new Vector2(.5f, .5f);
            announceRect.sizeDelta = new Vector2(1500f, 260f);
            announcer = announceObject.GetComponent<Text>();
            announcer.font = comboLabel.font;
            announcer.fontSize = 96;
            announcer.fontStyle = FontStyle.Bold;
            announcer.alignment = TextAnchor.MiddleCenter;
            announcer.raycastTarget = false;
            announcer.color = Color.clear;
            var announceOutline = announceObject.GetComponent<Outline>();
            announceOutline.effectColor = new Color(.08f, .02f, .15f, .95f);
            announceOutline.effectDistance = new Vector2(6f, -6f);

            var uiRoot = new GameObject("ComboFirePool", typeof(RectTransform)).transform;
            uiRoot.SetParent(comboLabel.transform.parent, false);
            uiRoot.SetSiblingIndex(comboLabel.transform.GetSiblingIndex());
            uiParticles = new ArcadeUiParticle[UiParticlePoolSize];
            for (int i = 0; i < uiParticles.Length; i++)
            {
                var item = new GameObject("ComboFire_" + i).AddComponent<ArcadeUiParticle>();
                item.transform.SetParent(uiRoot, false);
                item.Initialize(circleSprite, uiAdditiveMaterial);
                uiParticles[i] = item;
            }
        }

        static void CopyRect(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.localScale = source.localScale;
        }

        void BuildGpuParticles()
        {
            EnsurePersistentLayers();
            var particleObject = new GameObject("ArcadeGpuParticles");
            particleObject.transform.SetParent(effectsLayer, false);
            sparkSystem = particleObject.AddComponent<ParticleSystem>();
            var main = sparkSystem.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2500;
            main.startLifetime = .55f;
            main.startSpeed = 3f;
            main.startSize = .12f;
            main.gravityModifier = .22f;
            var emission = sparkSystem.emission;
            emission.enabled = false;
            var shape = sparkSystem.shape;
            shape.enabled = false;
            var colorOverLifetime = sparkSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;
            var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = lineMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 505;
        }

        void BuildPostProcessing()
        {
            EnsurePersistentLayers();
            var volumeObject = new GameObject("ArcadeComboPostProcessing");
            volumeObject.transform.SetParent(effectsLayer, false);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 90f;
            runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            runtimeProfile.name = "Runtime Arcade Combo Profile";
            volume.sharedProfile = runtimeProfile;
            bloom = runtimeProfile.Add<Bloom>(true);
            colorAdjustments = runtimeProfile.Add<ColorAdjustments>(true);
            bloom.intensity.Override(0f);
            bloom.threshold.Override(.72f);
            colorAdjustments.saturation.Override(0f);
            colorAdjustments.postExposure.Override(0f);
            var cameraData = gameCamera.GetComponent<UniversalAdditionalCameraData>() ?? gameCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
        }

        void SetComboPresentation(int combo, bool immediate)
        {
            if (!comboLabel) return;
            comboLabel.text = combo > 0 ? combo + "\nCOMBO" : "";
            comboLabel.color = ComboColor(combo);
            if (rainbow) rainbow.animate = combo >= 200;
            if (immediate)
            {
                comboLabel.rectTransform.localScale = Vector3.one;
                comboLabel.rectTransform.localRotation = Quaternion.identity;
            }
        }

        void PunchArcadeCombo(int combo)
        {
            if (!comboLabel) return;
            if (comboPunch != null) StopCoroutine(comboPunch);
            comboPunch = StartCoroutine(ArcadeComboPunchRoutine(combo));
        }

        IEnumerator ArcadeComboPunchRoutine(int combo)
        {
            var rect = comboLabel.rectTransform;
            Vector3 startScale = Vector3.one;
            float direction = combo % 2 == 0 ? 1f : -1f;
            for (float time = 0f; time < .075f; time += Time.unscaledDeltaTime)
            {
                float progress = time / .075f;
                rect.localScale = Vector3.Lerp(startScale, startScale * 1.5f, 1f - Mathf.Pow(1f - progress, 3f));
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-8f * direction, 8f * direction, progress));
                UpdateComboGhosts(combo, direction, progress);
                yield return null;
            }
            for (float time = 0f; time < .16f; time += Time.unscaledDeltaTime)
            {
                float progress = time / .16f;
                rect.localScale = Vector3.Lerp(startScale * 1.5f, startScale, Mathf.SmoothStep(0f, 1f, progress));
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(8f * direction, 0f, progress));
                UpdateComboGhosts(combo, direction, 1f - progress);
                yield return null;
            }
            rect.localScale = startScale;
            rect.localRotation = Quaternion.identity;
            ClearComboGhosts();
            comboPunch = null;
        }

        void UpdateComboGhosts(int combo, float direction, float alpha)
        {
            if (comboGhosts == null) return;
            Color baseColor = ComboColor(combo);
            for (int i = 0; i < comboGhosts.Length; i++)
            {
                var ghost = comboGhosts[i];
                ghost.text = comboLabel.text;
                ghost.rectTransform.anchoredPosition = comboLabel.rectTransform.anchoredPosition + new Vector2(direction * (i + 1) * 14f, (i + 1) * -5f);
                ghost.rectTransform.localScale = comboLabel.rectTransform.localScale;
                ghost.rectTransform.localRotation = comboLabel.rectTransform.localRotation;
                Color tint = combo >= 200 ? RainbowColor(Time.unscaledTime + i * .3f) : baseColor;
                tint.a = alpha * (.22f - i * .06f);
                ghost.color = tint;
            }
        }

        void ClearComboGhosts()
        {
            if (comboGhosts == null) return;
            foreach (var ghost in comboGhosts) if (ghost) ghost.color = Color.clear;
        }

        void EmitImpact(Vector3 position, Color color, bool heavy, ArcadeHitKind kind)
        {
            int count = heavy ? 22 : 9;
            if (kind == ArcadeHitKind.Explosion) count += 18;
            EmitSparks(position, color, count, heavy ? 5.5f : 3.4f);
            int fragmentCount = EffectQuality == ArcadeEffectQuality.Low ? (heavy ? 4 : 1) : EffectQuality == ArcadeEffectQuality.Medium ? (heavy ? 8 : 3) : (heavy ? 12 : 5);
            for (int i = 0; i < fragmentCount; i++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                debris[(shockwaveCursor * 13 + i) % debris.Length].Play(
                    position,
                    Color.Lerp(color, Color.white, .35f),
                    Random.Range(.08f, .18f),
                    direction * Random.Range(1.4f, heavy ? 4.5f : 2.8f),
                    Random.Range(-480f, 480f),
                    Random.Range(.25f, .55f),
                    false);
            }
            Shockwave(position, color, heavy ? .8f : .42f);
        }

        void EmitSparks(Vector3 position, Color color, int count, float speed)
        {
            if (!sparkSystem) return;
            count = QualityCount(count);
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                var emit = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = direction * Random.Range(speed * .45f, speed),
                    startColor = color,
                    startLifetime = Random.Range(.28f, .72f),
                    startSize = Random.Range(.055f, .18f)
                };
                sparkSystem.Emit(emit, 1);
            }
        }

        void Shockwave(Vector3 position, Color color, float size)
        {
            shockwaves[shockwaveCursor++ % shockwaves.Length].Play(position, color, size, Vector3.zero, Random.Range(-55f, 55f), .32f, true);
        }

        void Popup(Vector3 position, int damage, Color color, bool critical, ArcadeHitKind kind)
        {
            string prefix = critical ? "CRIT! " : kind == ArcadeHitKind.Beam ? "BEAM " : kind == ArcadeHitKind.Splash ? "SPLASH " : kind == ArcadeHitKind.Explosion ? "BOOM " : "";
            float damageScale = Mathf.Clamp(.72f + Mathf.Log10(Mathf.Max(1, damage)) * .18f, .75f, 1.45f);
            if (kind == ArcadeHitKind.Burn) damageScale *= .72f;
            damageScale *= 1f + Mathf.Min(7, currentCombo >= 1000 ? 7 : currentCombo >= 500 ? 6 : currentCombo >= 300 ? 5 : currentCombo >= 200 ? 4 : currentCombo >= 100 ? 3 : currentCombo >= 50 ? 2 : currentCombo >= 20 ? 1 : 0) * .04f;
            popups[popupCursor++ % popups.Length].Play(position, prefix + damage, color, damageScale * (critical ? 1.28f : 1f), critical);
        }

        void Flash(Color color, float alpha)
        {
            if (!screenFlash) return;
            var tint = color;
            tint.a = Mathf.Clamp01(Mathf.Max(screenFlash.color.a, alpha));
            screenFlash.color = tint;
        }

        void CheckAnnouncement(int combo)
        {
            string message = combo >= 700 ? "ABSOLUTE CHAOS!" :
                combo >= 400 ? "LEGENDARY!" :
                combo >= 250 ? "DOMINATING!" :
                combo >= 150 ? "UNSTOPPABLE!" :
                combo >= 100 ? "AMAZING!" :
                combo >= 50 ? "GREAT!" :
                combo >= 20 ? "NICE!" : null;
            int milestone = combo >= 700 ? 700 : combo >= 400 ? 400 : combo >= 250 ? 250 : combo >= 150 ? 150 : combo >= 100 ? 100 : combo >= 50 ? 50 : combo >= 20 ? 20 : 0;
            if (milestone == 0 || milestone <= lastAnnouncedMilestone) return;
            lastAnnouncedMilestone = milestone;
            if (announceRoutine != null) StopCoroutine(announceRoutine);
            announceRoutine = StartCoroutine(AnnounceRoutine(message, ComboColor(combo)));
        }

        void CheckVoiceCue(int combo)
        {
            if (combo == 30) PlayVoice(combo30Voice);
            else if (combo == 100) PlayVoice(combo100Voice);
            else if (combo == 200) PlayVoice(combo200Voice);
        }

        IEnumerator AnnounceRoutine(string message, Color color)
        {
            if (!announcer) yield break;
            announcer.text = message;
            var rect = announcer.rectTransform;
            rect.localRotation = Quaternion.Euler(0f, 0f, -9f);
            for (float time = 0f; time < .12f; time += Time.unscaledDeltaTime)
            {
                float progress = time / .12f;
                rect.localScale = Vector3.one * Mathf.Lerp(.25f, 1.45f, 1f - Mathf.Pow(1f - progress, 3f));
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-9f, 4f, progress));
                var tint = color; tint.a = progress; announcer.color = tint;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(.35f);
            for (float time = 0f; time < .25f; time += Time.unscaledDeltaTime)
            {
                float progress = time / .25f;
                rect.localScale = Vector3.one * Mathf.Lerp(1.45f, 2.1f, progress);
                var tint = color; tint.a = 1f - progress; announcer.color = tint;
                yield return null;
            }
            announcer.color = Color.clear;
            announceRoutine = null;
        }

        void TriggerSpectacle(Vector3 origin)
        {
            Vector3 center = comboOrb && comboOrb.IsSummoned ? comboOrb.WorldPosition : origin;
            int arcs = QualityCount(currentCombo >= 1000 ? 7 : 4);
            for (int i = 0; i < arcs; i++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                Beam(center + (Vector3)direction * .22f, center + (Vector3)direction * Random.Range(1.1f,2.1f), currentCombo);
            }
            int waves = currentCombo >= 1000 ? 4 : 2;
            for (int i = 0; i < waves; i++) Shockwave(center, RainbowColor(i/(float)waves),.7f+i*.28f);
            EmitSparks(center,RainbowColor(Random.value),QualityCount(currentCombo>=1000?70:38),currentCombo>=1000?6f:4.5f);
            Flash(Color.white,currentCombo>=1000?.14f:.07f);
        }

        void UpdateEscalation()
        {
            float intensity = ComboIntensity(currentCombo);
            float pulse = currentCombo >= 30 ? .5f + Mathf.Sin(Time.unscaledTime * 5f) * .5f : 0f;
            if (screenFlash)
            {
                var tint = screenFlash.color;
                tint.a = Mathf.MoveTowards(tint.a, 0f, Time.unscaledDeltaTime * 1.8f);
                screenFlash.color = tint;
            }
            var config = comboPresentation ? comboPresentation.Config : null;
            float bloomLimit = config ? config.maximumBloom : 1.8f;
            float saturationLimit = config ? config.maximumSaturation : 12f;
            if (EffectQuality == ArcadeEffectQuality.Low) bloomLimit *= .45f;
            else if (EffectQuality == ArcadeEffectQuality.Medium) bloomLimit *= .75f;
            if (bloom) bloom.intensity.value = Mathf.Lerp(bloom.intensity.value, bloomLimit * intensity * .55f + pulse * intensity * .08f, Time.unscaledDeltaTime * 5f);
            if (colorAdjustments)
            {
                colorAdjustments.saturation.value = Mathf.Lerp(colorAdjustments.saturation.value, intensity * saturationLimit * .45f, Time.unscaledDeltaTime * 3f);
                colorAdjustments.postExposure.value = Mathf.Lerp(colorAdjustments.postExposure.value, intensity * .08f, Time.unscaledDeltaTime * 3f);
            }
            foreach (var pair in stageColors)
                if (pair.Key) pair.Key.color = Color.Lerp(pair.Value, Saturate(pair.Value, 1f + intensity * .18f), intensity * .22f);

            if (!comboPresentation && currentCombo >= 30 && uiParticles != null)
            {
                fireTimer -= Time.unscaledDeltaTime;
                if (fireTimer <= 0f)
                {
                    int count = Mathf.Clamp(1 + currentCombo / 25, 2, 9);
                    for (int i = 0; i < count; i++) EmitComboFire();
                    fireTimer = Mathf.Lerp(.09f, .025f, intensity);
                }
            }

            if (currentCombo >= 200)
            {
                ambientTimer -= Time.unscaledDeltaTime;
                if (ambientTimer <= 0f)
                {
                    Color starColor = currentCombo >= 200 ? RainbowColor(Time.unscaledTime * .2f) : new Color(.75f, .9f, 1f, 1f);
                    int stars = Mathf.Clamp(1 + currentCombo / 180, 1, 4);
                    for (int i = 0; i < stars; i++)
                        EmitSparks(new Vector3(Random.Range(-9f, 9f), Random.Range(-4.3f, 5.2f)), starColor, 1, .45f);
                    if (currentCombo >= 500 && Random.value < .18f && debris != null && debris.Length > 0)
                        debris[Random.Range(0, debris.Length)].Play(
                            new Vector3(Random.Range(-7f,7f),-4.1f),starColor,Random.Range(.08f,.16f),
                            new Vector3(Random.Range(-.08f,.08f),Random.Range(.18f,.38f)),Random.Range(-25f,25f),1.2f,false);
                    ambientTimer = Mathf.Lerp(.32f, .1f, intensity);
                }
            }
        }

        void EmitComboFire()
        {
            if (!comboLabel || uiParticles == null) return;
            Color color = currentCombo >= 200
                ? Color.Lerp(new Color(.2f, .65f, 1f, .9f), new Color(.8f, .2f, 1f, .9f), Random.value)
                : Color.Lerp(new Color(1f, .22f, .02f, .85f), new Color(1f, .9f, .1f, .9f), Random.value);
            Vector2 origin = comboLabel.rectTransform.anchoredPosition + new Vector2(Random.Range(-220f, 220f), Random.Range(-35f, 35f));
            float growth = 1f + Mathf.Floor(Mathf.Max(0, currentCombo - 30) / 25f) * .08f;
            uiParticles[uiCursor++ % uiParticles.Length].Play(origin, color, Random.Range(18f, 38f) * growth, new Vector2(Random.Range(-28f, 28f), Random.Range(45f, 110f) * growth), Random.Range(.35f, .7f));
        }

        void UpdateMusicLayers()
        {
            var config = comboPresentation ? comboPresentation.Config : null;
            if (config && config.highComboAmbience)
            {
                if (!ambienceSource)
                {
                    ambienceSource = gameObject.AddComponent<AudioSource>();
                    ambienceSource.loop = true;
                    ambienceSource.playOnAwake = false;
                    ambienceSource.clip = config.highComboAmbience;
                    ambienceSource.volume = 0f;
                }
                float target = currentCombo >= 300 ? Mathf.InverseLerp(300f, 1000f, currentCombo) * .65f : 0f;
                ambienceSource.volume = Mathf.MoveTowards(ambienceSource.volume, target, Time.unscaledDeltaTime * .8f);
                if (target > .01f && !ambienceSource.isPlaying) ambienceSource.Play();
                else if (target <= 0f && ambienceSource.isPlaying && ambienceSource.volume <= .001f) ambienceSource.Stop();
            }
            if (highComboMusicLayers == null) return;
            float intensity = ComboIntensity(currentCombo);
            for (int i = 0; i < highComboMusicLayers.Length; i++)
            {
                var layer = highComboMusicLayers[i];
                if (!layer) continue;
                float threshold = (i + 1f) / (highComboMusicLayers.Length + 1f);
                float target = Mathf.InverseLerp(threshold, Mathf.Min(1f, threshold + .25f), intensity);
                layer.volume = Mathf.MoveTowards(layer.volume, target, Time.unscaledDeltaTime * 1.5f);
                if (target > .01f && !layer.isPlaying) layer.Play();
            }
        }

        void UpdateCharacterSparks()
        {
            if (currentCombo < 150 || !characterPresenter || !characterPresenter.CurrentVisual) return;
            characterSparkTimer -= Time.unscaledDeltaTime;
            if (characterSparkTimer > 0f) return;
            Color color = currentCombo >= 300 ? RainbowColor(Time.unscaledTime) : new Color(.85f, .55f, 1f, 1f);
            EmitSparks(characterPresenter.CurrentVisual.position + (Vector3)Random.insideUnitCircle * .7f, color, currentCombo >= 300 ? 4 : 2, 1.2f);
            characterSparkTimer = currentCombo >= 300 ? .045f : .1f;
        }

        void RefreshCharacterReaction()
        {
            if (!characterPresenter || !characterPresenter.CurrentVisual || !circleSprite) return;
            var visual = characterPresenter.CurrentVisual;
            var reaction = visual.GetComponent<ArcadeCharacterReaction>();
            if (!reaction)
            {
                reaction = visual.gameObject.AddComponent<ArcadeCharacterReaction>();
                reaction.Initialize(circleSprite, ringSprite, lineMaterial);
            }
            if (characterReaction != reaction)
            {
                characterReaction = reaction;
                characterReaction.SetCombo(currentCombo);
            }
        }

        IEnumerator ComboBreakRoutine(int previousCombo)
        {
            float duration = previousCombo >= 100 ? .7f : .42f;
            for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                if (comboLabel)
                {
                    float progress = time / duration;
                    comboLabel.rectTransform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * .7f, progress);
                    var color = comboLabel.color; color.a = 1f - progress; comboLabel.color = color;
                }
                yield return null;
            }
            if (comboLabel)
            {
                comboLabel.text = "";
                comboLabel.rectTransform.localScale = Vector3.one;
                comboLabel.rectTransform.localRotation = Quaternion.identity;
                comboLabel.color = ComboColor(0);
            }
            ClearComboGhosts();
            comboBreak = null;
        }

        void HitStop(float duration, bool slowMotionTail = false)
        {
            if (Time.timeScale <= 0f) return;
            if (hitStopRoutine != null) StopCoroutine(hitStopRoutine);
            hitStopRoutine = StartCoroutine(HitStopRoutine(duration, slowMotionTail));
        }

        IEnumerator HitStopRoutine(float duration, bool slowMotionTail)
        {
            float restore = Time.timeScale;
            Time.timeScale = Mathf.Min(restore, .08f);
            yield return new WaitForSecondsRealtime(duration);
            if (Time.timeScale > 0f && Time.timeScale <= .081f)
            {
                if (slowMotionTail)
                {
                    Time.timeScale = Mathf.Min(restore, .35f);
                    yield return new WaitForSecondsRealtime(.18f);
                }
                if (Time.timeScale > 0f) Time.timeScale = restore;
            }
            hitStopRoutine = null;
        }

        void PlayVoice(AudioClip clip)
        {
            if (!clip) return;
            if (!voiceSource) voiceSource = gameObject.GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            voiceSource.PlayOneShot(clip);
        }

        void PlayConfiguredAudio(bool critical, bool destroyed, bool milestone, bool hyper)
        {
            var config = comboPresentation ? comboPresentation.Config : null;
            if (!config) return;
            if (!arcadeAudioSource) arcadeAudioSource = gameObject.AddComponent<AudioSource>();
            if (config.comboIncrease) arcadeAudioSource.PlayOneShot(config.comboIncrease, .18f);
            if (critical && config.criticalHit) arcadeAudioSource.PlayOneShot(config.criticalHit, .7f);
            if (destroyed && config.blockDestruction) arcadeAudioSource.PlayOneShot(config.blockDestruction, .55f);
            if (milestone && config.comboMilestone) arcadeAudioSource.PlayOneShot(config.comboMilestone, .8f);
            if (hyper && config.hyperCombo) arcadeAudioSource.PlayOneShot(config.hyperCombo, 1f);
            if (milestone && currentCombo >= 200 && config.characterCutIn) arcadeAudioSource.PlayOneShot(config.characterCutIn, .85f);
        }

        void PlayCharacterVoice(int milestone)
        {
            CharacterData data = characterPresenter ? characterPresenter.CurrentData : null;
            if (!data || data.comboVoiceClips == null || data.comboVoiceClips.Length == 0)
            {
                var config = comboPresentation ? comboPresentation.Config : null;
                if (config) PlayVoice(config.characterVoice);
                return;
            }
            int index = milestone >= 1000 ? data.comboVoiceClips.Length - 1 : milestone >= 500 ? Mathf.Min(1, data.comboVoiceClips.Length - 1) : 0;
            PlayVoice(data.comboVoiceClips[index]);
        }

        void ResetStageColors()
        {
            foreach (var pair in stageColors) if (pair.Key) pair.Key.color = pair.Value;
        }

        static float ComboIntensity(int combo) => Mathf.Clamp01(combo / 300f);
        int QualityCount(int value) => EffectQuality == ArcadeEffectQuality.Low ? Mathf.Max(1, Mathf.CeilToInt(value * .35f)) : EffectQuality == ArcadeEffectQuality.Medium ? Mathf.Max(1, Mathf.CeilToInt(value * .68f)) : value;

        public void PreviewCombo(int value)
        {
            currentCombo = Mathf.Max(0, value);
            Debug.Log("[Combo Preview] Request " + currentCombo + " | Orb assigned: " + (comboOrb ? "YES" : "NO"), this);
            if (currentCombo > 0 && comboOrb && !comboOrb.IsSummoned) comboOrb.Summon(Vector3.zero, currentCombo);
            else if (currentCombo > 0) comboOrb?.SetCombo(currentCombo);
            else comboOrb?.Despawn();
            if (comboPresentation) comboPresentation.SetCombo(currentCombo);
            else { SetComboPresentation(currentCombo, false); PunchArcadeCombo(currentCombo); }
            RefreshCharacterReaction();
            if (characterReaction) characterReaction.SetCombo(currentCombo);
            worldReaction?.SetCombo(currentCombo);
            ApplyProjectileEvolution();
        }

        public void PreviewCharacterCutIn(int milestone) => comboPresentation?.PreviewCutIn(milestone);
        public void PreviewCriticalHit() => Hit(Mathf.Max(1, currentCombo), Vector3.zero, 999, true, ArcadeHitKind.Direct, false);
        public void PreviewBlockExplosion() => Destroyed(Vector3.zero, Mathf.Max(1, currentCombo), ArcadeHitKind.Explosion);
        public void SetPreviewQuality(ArcadeEffectQuality quality) => previewQuality = quality;
        public void ResetArcadeEffects()
        {
            previewQuality = null;
            ComboEnded(currentCombo);
            if (screenFlash) screenFlash.color = Color.clear;
            if (sparkSystem) sparkSystem.Clear(true);
        }
        static Color HitColor(ArcadeHitKind kind, bool critical)
        {
            if (critical) return new Color(1f, .72f, .08f, 1f);
            if (kind == ArcadeHitKind.Beam) return new Color(.2f, .85f, 1f, 1f);
            if (kind == ArcadeHitKind.Splash) return new Color(.45f, .82f, 1f, 1f);
            if (kind == ArcadeHitKind.Burn) return new Color(1f, .24f, .04f, 1f);
            if (kind == ArcadeHitKind.Explosion) return new Color(1f, .35f, .08f, 1f);
            if (kind == ArcadeHitKind.Chain) return new Color(.75f, .35f, 1f, 1f);
            return new Color(1f, .88f, .3f, 1f);
        }
        static Color ComboColor(int combo)
        {
            if (combo >= 200) return Color.white;
            if (combo >= 100) return new Color(1f, .12f, .08f, 1f);
            if (combo >= 50) return new Color(1f, .38f, .05f, 1f);
            if (combo >= 20) return new Color(1f, .9f, .08f, 1f);
            return Color.white;
        }
        static Color RainbowColor(float phase) => Color.HSVToRGB(Mathf.Repeat(phase, 1f), .82f, 1f);
        static Color Saturate(Color color, float amount)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            Color result = Color.HSVToRGB(hue, Mathf.Clamp01(saturation * amount), value);
            result.a = color.a;
            return result;
        }

        void OnDestroy()
        {
            ResetStageColors();
            if (runtimeProfile) Destroy(runtimeProfile);
            if (additiveMaterial) Destroy(additiveMaterial);
            if (uiAdditiveMaterial) Destroy(uiAdditiveMaterial);
            if (lineMaterial) Destroy(lineMaterial);
            if (circleSprite)
            {
                var texture = circleSprite.texture;
                Destroy(circleSprite);
                if (texture) Destroy(texture);
            }
            if (ringSprite)
            {
                var texture = ringSprite.texture;
                Destroy(ringSprite);
                if (texture) Destroy(texture);
            }
        }
    }
}
