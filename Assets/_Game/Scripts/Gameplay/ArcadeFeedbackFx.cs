using UnityEngine;
using UnityEngine.UI;

namespace Reflectable
{
    public enum ArcadeHitKind { Direct, Beam, Splash, Burn, Explosion, Chain }

    public sealed class ArcadeSpriteFx : MonoBehaviour
    {
        SpriteRenderer spriteRenderer;
        Vector3 velocity;
        Vector3 startScale;
        Color startColor;
        float spin;
        float age;
        float lifetime;
        bool expanding;

        public void Initialize(Sprite sprite, Material material, int sortingOrder)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sharedMaterial = material;
            spriteRenderer.sortingOrder = sortingOrder;
            gameObject.SetActive(false);
        }

        public void Play(Vector3 position, Color color, float size, Vector3 movement, float rotationSpeed, float duration, bool expand)
        {
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            transform.localScale = startScale = Vector3.one * size;
            velocity = movement;
            spin = rotationSpeed;
            lifetime = Mathf.Max(.05f, duration);
            age = 0f;
            expanding = expand;
            startColor = color;
            spriteRenderer.color = color;
            gameObject.SetActive(true);
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(age / lifetime);
            transform.position += velocity * Time.unscaledDeltaTime;
            transform.Rotate(0f, 0f, spin * Time.unscaledDeltaTime);
            transform.localScale = expanding
                ? Vector3.Lerp(startScale * .35f, startScale * 2.8f, 1f - Mathf.Pow(1f - progress, 3f))
                : Vector3.Lerp(startScale, startScale * .3f, progress);
            var color = startColor;
            color.a *= 1f - progress;
            spriteRenderer.color = color;
            if (age >= lifetime) gameObject.SetActive(false);
        }
    }

    public sealed class ArcadeTextFx : MonoBehaviour
    {
        TextMesh label;
        Vector3 start;
        float age;
        float lifetime;
        float spin;
        float scale;
        Color color;
        bool critical;

        public void Initialize(Font font)
        {
            label = gameObject.AddComponent<TextMesh>();
            label.font = font;
            label.fontSize = 72;
            label.characterSize = .028f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontStyle = FontStyle.Bold;
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            label.GetComponent<MeshRenderer>().sortingOrder = 520;
            gameObject.SetActive(false);
        }

        public void Play(Vector3 position, string value, Color tint, float size, bool isCritical)
        {
            start = position + Vector3.up * .15f;
            transform.position = start;
            transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-12f, 12f));
            label.text = value;
            color = tint;
            label.color = tint;
            scale = size;
            transform.localScale = Vector3.one * size;
            critical = isCritical;
            spin = Random.Range(-38f, 38f);
            lifetime = isCritical ? .9f : .65f;
            age = 0f;
            gameObject.SetActive(true);
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(age / lifetime);
            float jump = Mathf.Sin(progress * Mathf.PI) * (critical ? 1.15f : .72f);
            transform.position = start + Vector3.up * (progress * .65f + jump);
            transform.Rotate(0f, 0f, spin * Time.unscaledDeltaTime);
            float punch = critical ? 1f + Mathf.Sin(progress * Mathf.PI * 3f) * .24f : 1f + Mathf.Sin(progress * Mathf.PI) * .14f;
            transform.localScale = Vector3.one * scale * punch;
            var tint = color;
            tint.a *= 1f - Mathf.SmoothStep(.55f, 1f, progress);
            label.color = tint;
            if (age >= lifetime) gameObject.SetActive(false);
        }
    }

    public sealed class ArcadeBeamFx : MonoBehaviour
    {
        LineRenderer line;
        Color color;
        float width;
        float age;
        float lifetime;

        public void Initialize(Material material)
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.sortingOrder = 510;
            gameObject.SetActive(false);
        }

        public void Play(Vector3 start, Vector3 end, Color tint, float beamWidth, float duration)
        {
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            color = tint;
            width = beamWidth;
            lifetime = duration;
            age = 0f;
            gameObject.SetActive(true);
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(age / lifetime);
            float pulse = 1f + Mathf.Sin(age * 90f) * .18f;
            line.startWidth = width * pulse * (1f - progress * .45f);
            line.endWidth = line.startWidth * .35f;
            var tint = color;
            tint.a *= 1f - progress;
            line.startColor = line.endColor = tint;
            if (age >= lifetime) gameObject.SetActive(false);
        }
    }

    public sealed class ArcadeUiParticle : MonoBehaviour
    {
        RectTransform rect;
        Image image;
        Vector2 velocity;
        Color color;
        float age;
        float lifetime;
        float spin;
        float size;

        public void Initialize(Sprite sprite, Material material)
        {
            rect = gameObject.AddComponent<RectTransform>();
            image = gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.material = material;
            image.raycastTarget = false;
            gameObject.SetActive(false);
        }

        public void Play(Vector2 position, Color tint, float particleSize, Vector2 movement, float duration)
        {
            rect.anchoredPosition = position;
            rect.sizeDelta = Vector2.one * particleSize;
            rect.localScale = Vector3.one;
            velocity = movement;
            color = tint;
            image.color = tint;
            size = particleSize;
            spin = Random.Range(-180f, 180f);
            lifetime = duration;
            age = 0f;
            gameObject.SetActive(true);
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(age / lifetime);
            rect.anchoredPosition += velocity * Time.unscaledDeltaTime;
            velocity += Vector2.up * 90f * Time.unscaledDeltaTime;
            rect.Rotate(0f, 0f, spin * Time.unscaledDeltaTime);
            rect.sizeDelta = Vector2.one * size * Mathf.Lerp(1f, .15f, progress);
            var tint = color;
            tint.a *= 1f - progress;
            image.color = tint;
            if (age >= lifetime) gameObject.SetActive(false);
        }
    }

    public sealed class ArcadeRainbowTextEffect : BaseMeshEffect
    {
        public bool animate;
        public float speed = .32f;

        void Update()
        {
            if (animate && graphic) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!IsActive() || !animate) return;
            UIVertex vertex = default;
            float phase = Time.unscaledTime * speed;
            for (int i = 0; i < vertexHelper.currentVertCount; i++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, i);
                float hue = Mathf.Repeat(phase + vertex.position.x * .0012f + vertex.position.y * .0005f, 1f);
                Color rainbow = Color.HSVToRGB(hue, .78f, 1f);
                rainbow.a = vertex.color.a / 255f;
                vertex.color = rainbow;
                vertexHelper.SetUIVertex(vertex, i);
            }
        }
    }

    public sealed class ArcadeCharacterReaction : MonoBehaviour
    {
        Transform visualRoot;
        Transform weaponRoot;
        SpriteRenderer[] renderers;
        Color[] baseColors;
        Vector3 baseScale;
        Quaternion baseRotation;
        Quaternion weaponRotation;
        SpriteRenderer aura;
        SpriteRenderer magicCircle;
        int combo;

        public void Initialize(Sprite glowSprite, Sprite ringSprite, Material additiveMaterial)
        {
            visualRoot = transform.Find("VisualRoot") ?? transform;
            weaponRoot = transform.Find("WeaponRoot");
            baseScale = visualRoot.localScale;
            baseRotation = visualRoot.localRotation;
            if (weaponRoot) weaponRotation = weaponRoot.localRotation;
            renderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;

            var auraObject = new GameObject("ComboAura");
            auraObject.transform.SetParent(transform, false);
            auraObject.transform.localPosition = new Vector3(0f, .1f, .1f);
            aura = auraObject.AddComponent<SpriteRenderer>();
            aura.sprite = glowSprite;
            aura.sharedMaterial = additiveMaterial;
            aura.sortingOrder = 45;
            aura.enabled = false;

            var circleObject = new GameObject("ComboMagicCircle");
            circleObject.transform.SetParent(transform, false);
            circleObject.transform.localPosition = new Vector3(0f, -.55f, .1f);
            circleObject.transform.localScale = new Vector3(1.4f, .38f, 1f);
            magicCircle = circleObject.AddComponent<SpriteRenderer>();
            magicCircle.sprite = ringSprite;
            magicCircle.sharedMaterial = additiveMaterial;
            magicCircle.sortingOrder = 44;
            magicCircle.enabled = false;
        }

        public void SetCombo(int value)
        {
            combo = Mathf.Max(0, value);
            if (aura) aura.enabled = combo >= 150;
            if (magicCircle) magicCircle.enabled = combo >= 150;
        }

        void Update()
        {
            if (!visualRoot) return;
            float time = Time.unscaledTime;
            float targetPower = Mathf.InverseLerp(30f, 300f, combo);
            if (combo < 30)
            {
                visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, baseScale, Time.unscaledDeltaTime * 10f);
                visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, baseRotation, Time.unscaledDeltaTime * 10f);
                if (weaponRoot) weaponRoot.localRotation = Quaternion.Slerp(weaponRoot.localRotation, weaponRotation, Time.unscaledDeltaTime * 10f);
                for (int i = 0; i < renderers.Length; i++) if (renderers[i]) renderers[i].color = Color.Lerp(renderers[i].color, baseColors[i], Time.unscaledDeltaTime * 8f);
                return;
            }

            float breathe = 1f + Mathf.Sin(time * Mathf.Lerp(4f, 8f, targetPower)) * Mathf.Lerp(.025f, .075f, targetPower);
            visualRoot.localScale = baseScale * breathe;
            visualRoot.localRotation = baseRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(time * 5f) * Mathf.Lerp(1.5f, 5f, targetPower));
            if (weaponRoot && combo >= 100) weaponRoot.localRotation = weaponRotation * Quaternion.Euler(0f, 0f, 12f + Mathf.Sin(time * 7f) * 5f);

            Color glow = combo >= 200 ? Color.Lerp(new Color(.35f,.75f,1f), new Color(.9f,.3f,1f), Mathf.PingPong(time, 1f)) : new Color(1f,.78f,.3f);
            float tint = combo >= 300 ? .52f : combo >= 200 ? .30f : .12f;
            for (int i = 0; i < renderers.Length; i++) if (renderers[i]) renderers[i].color = Color.Lerp(baseColors[i], glow, tint);
            if (aura)
            {
                aura.color = new Color(glow.r, glow.g, glow.b, combo >= 300 ? .22f : .12f);
                aura.transform.localScale = Vector3.one * (1.45f + targetPower * .55f + Mathf.Sin(time * 6f) * .08f);
                aura.transform.Rotate(0f, 0f, 18f * Time.unscaledDeltaTime);
            }
            if (magicCircle)
            {
                magicCircle.color = new Color(glow.r, glow.g, glow.b, combo >= 300 ? .34f : .2f);
                magicCircle.transform.Rotate(0f, 0f, (combo >= 300 ? 120f : 65f) * Time.unscaledDeltaTime);
            }
        }

        void OnDisable()
        {
            if (visualRoot) { visualRoot.localScale = baseScale; visualRoot.localRotation = baseRotation; }
            if (weaponRoot) weaponRoot.localRotation = weaponRotation;
        }
    }
}
