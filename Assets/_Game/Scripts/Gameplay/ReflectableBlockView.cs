using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Reflectable
{
    public sealed class ReflectableBlockView : MonoBehaviour
    {
        [SerializeField] Text hpLabel;
        Transform visual; SpriteRenderer sprite; Color baseColor; bool damagedPlayer;
        public int Row { get; set; } public int Column { get; set; } public int HP { get; private set; } public int MaxHP { get; private set; } public ReflectableBlockType Type { get; private set; }
        public bool HasResolvedPlayerContact => damagedPlayer;
        public bool TryMarkPlayerDamage() { if (damagedPlayer) return false; damagedPlayer = true; return true; }
        void Awake() { visual = transform.Find("Visual") ?? transform; }
        void OnEnable() { damagedPlayer = false; }
        public void Setup(ReflectableGameController owner, int row, int col, int hp, int max, ReflectableBlockType type) { StopAllCoroutines(); damagedPlayer = false; Row = row; Column = col; HP = hp; MaxHP = max; Type = type; Style(); Refresh(); StartCoroutine(SpawnIn()); }
        void Style() { float size = Type == ReflectableBlockType.Boss ? 2.15f : Type == ReflectableBlockType.Anchor ? 1.35f : Type == ReflectableBlockType.Elite ? 1.22f : Type == ReflectableBlockType.Armored ? 1.12f : 1f; visual.localScale = Vector3.one * size; sprite = visual.GetComponentInChildren<SpriteRenderer>(); if (sprite) baseColor = sprite.color; }
        public void ApplyDamage(int amount) { HP -= amount; Refresh(); StartCoroutine(Punch()); }
        static string Number(int value) => value >= 1000 ? (value / 1000f).ToString("0.0") + "K" : value.ToString();
        void Refresh() { if (hpLabel) hpLabel.text = Type == ReflectableBlockType.Boss ? "BOSS\n" + Number(HP) + " / " + Number(MaxHP) : Type == ReflectableBlockType.Anchor ? "◆ " + Number(HP) : Number(HP); if (sprite) { float ratio = Mathf.Clamp01(HP / (float)MaxHP); sprite.color = Color.Lerp(baseColor * .45f, baseColor, ratio); } }
        IEnumerator SpawnIn() { var target = transform.position; transform.position = target + Vector3.up * .45f; var scale = visual.localScale; visual.localScale = scale * .86f; for (float t = 0; t < .22f; t += Time.deltaTime) { float p = 1f - Mathf.Pow(1f - t / .22f, 3f); transform.position = Vector3.Lerp(target + Vector3.up * .45f, target, p); visual.localScale = Vector3.Lerp(scale * .86f, scale, p); yield return null; } if (this) { transform.position = target; visual.localScale = scale; } }
        IEnumerator Punch() { var scale = visual.localScale; for (float t = 0; t < .06f; t += Time.deltaTime) { visual.localScale = Vector3.Lerp(scale, scale * 1.10f, t / .06f); yield return null; } for (float t = 0; t < .08f; t += Time.deltaTime) { visual.localScale = Vector3.Lerp(scale * 1.10f, scale, t / .08f); yield return null; } if (this) visual.localScale = scale; }
        public IEnumerator MoveTo(Vector2 target, float duration) { var start = (Vector2)transform.position; for (float t = 0; t < duration; t += Time.deltaTime) { transform.position = Vector2.Lerp(start, target, Mathf.SmoothStep(0, 1, t / duration)); yield return null; } if (this) transform.position = target; }
    }
}
