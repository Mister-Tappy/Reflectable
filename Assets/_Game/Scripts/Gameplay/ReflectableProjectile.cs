using System.Collections.Generic;
using UnityEngine;

namespace Reflectable
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class ReflectableProjectile : MonoBehaviour
    {
        Rigidbody2D body;
        CircleCollider2D circle;
        ReflectableGameController game;
        readonly HashSet<int> contactedBlocks = new HashSet<int>();
        Vector2 lastVelocity;
        int damage;
        float speed;
        float startedAt;
        bool launched;
        bool exiting;
        Transform visual;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            circle = GetComponent<CircleCollider2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            circle.isTrigger = false;
            visual = transform.Find("Visual");
            var trail = GetComponent<TrailRenderer>() ?? gameObject.AddComponent<TrailRenderer>();
            trail.time = .16f; trail.startWidth = .12f; trail.endWidth = .01f; trail.startColor = new Color(.95f,.78f,1f,.8f); trail.endColor = new Color(.65f,.85f,1f,0f);
        }

        public void Launch(ReflectableGameController owner, Vector2 direction, float launchSpeed, int initialDamage)
        {
            game = owner;
            damage = initialDamage;
            speed = launchSpeed;
            startedAt = Time.time;
            launched = true;
            lastVelocity = direction.normalized * speed;
            body.linearVelocity = lastVelocity;
            StartCoroutine(VisualPulse(1.22f));
        }

        public void AddDamage(int amount) => damage += amount;

        void FixedUpdate()
        {
            if (launched && body.linearVelocity.sqrMagnitude > .001f)
                lastVelocity = body.linearVelocity;
        }

        void OnCollisionEnter2D(Collision2D hit)
        {
            if (!launched || exiting || hit.contactCount == 0)
                return;

            var incoming = lastVelocity.sqrMagnitude > .001f ? lastVelocity : body.linearVelocity;
            if (incoming.sqrMagnitude < .001f)
                return;

            var block = hit.collider.GetComponentInParent<ReflectableBlockView>();
            if (block && contactedBlocks.Add(block.GetInstanceID()))
                game.HitBlock(block, damage);

            Reflect(incoming, GetImpactNormal(hit, incoming));
            StartCoroutine(VisualPulse(1.16f));

            if (!block)
                game.RegisterRicochet(this);
        }

        void OnCollisionExit2D(Collision2D hit)
        {
            var block = hit.collider.GetComponentInParent<ReflectableBlockView>();
            if (block)
                contactedBlocks.Remove(block.GetInstanceID());
        }

        Vector2 GetImpactNormal(Collision2D hit, Vector2 incoming)
        {
            var direction = incoming.normalized;
            var normal = hit.GetContact(0).normal.normalized;
            var bestScore = Vector2.Dot(-direction, normal);
            for (var i = 1; i < hit.contactCount; i++)
            {
                var candidate = hit.GetContact(i).normal.normalized;
                var score = Vector2.Dot(-direction, candidate);
                if (score > bestScore)
                {
                    normal = candidate;
                    bestScore = score;
                }
            }

            return Vector2.Dot(direction, normal) > 0f ? -normal : normal;
        }

        void Reflect(Vector2 incoming, Vector2 normal)
        {
            var reflected = Vector2.Reflect(incoming.normalized, normal.normalized).normalized;
            lastVelocity = reflected * speed;
            body.linearVelocity = lastVelocity;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.isTrigger && (other.name == "BottomBoundary" || other.CompareTag("ProjectileKill")))
                Exit();
        }

        void Update()
        {
            if (launched && (transform.position.y < -5.2f || Time.time - startedAt > 20f))
                Exit();
        }

        void Exit()
        {
            if (exiting)
                return;
            exiting = true;
            body.simulated = false;
            if (game)
                game.ProjectileExited();
            Destroy(gameObject);
        }

        System.Collections.IEnumerator VisualPulse(float amount)
        {
            if (!visual) yield break;
            var start = visual.localScale;
            for (float t=0;t<.045f;t+=Time.deltaTime){ if(visual) visual.localScale=Vector3.Lerp(start,start*amount,t/.045f); yield return null; }
            for (float t=0;t<.07f;t+=Time.deltaTime){ if(visual) visual.localScale=Vector3.Lerp(start*amount,start,t/.07f); yield return null; }
            if(visual) visual.localScale=start;
        }
    }
}
