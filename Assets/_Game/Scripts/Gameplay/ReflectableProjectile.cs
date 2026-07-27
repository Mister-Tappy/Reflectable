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
        TrailRenderer trail;
        Vector2 lastSamplePosition, lastCollisionNormal;
        float nextStuckSample, lastCollisionAt;
        int lastColliderId, repeatedCollisionCount;

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
            int projectileLayer = LayerMask.NameToLayer("Projectile");
            if (projectileLayer >= 0) { gameObject.layer = projectileLayer; Physics2D.IgnoreLayerCollision(projectileLayer, projectileLayer, true); }
            visual = transform.Find("Visual");
            trail = GetComponent<TrailRenderer>() ?? gameObject.AddComponent<TrailRenderer>();
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
            lastSamplePosition = transform.position;
            nextStuckSample = Time.time + .7f;
            StartCoroutine(VisualPulse(1.22f));
        }

        public int Damage => damage;
        public void AddDamage(int amount) => damage += amount;

        void FixedUpdate()
        {
            if (!launched || exiting) return;
            float age = Time.time - startedAt;
            float multiplier = age < 5f ? 1f : age < 7f ? 1.15f : age < 9f ? 1.35f : age < 11f ? 1.6f : age < 13f ? 2f : 2.5f;
            if (game && game.IsFinalProjectile(this) && age > 2f) multiplier = Mathf.Max(multiplier, 1.35f);
            var direction = body.linearVelocity.sqrMagnitude > .001f ? body.linearVelocity.normalized : lastVelocity.sqrMagnitude > .001f ? lastVelocity.normalized : Vector2.up;
            lastVelocity = direction * Mathf.Min(speed * multiplier, speed * 2.5f);
            body.linearVelocity = lastVelocity;
            if (trail) { trail.time = Mathf.Lerp(.16f, .42f, (multiplier - 1f) / 1.5f); trail.startWidth = Mathf.Lerp(.12f, .2f, (multiplier - 1f) / 1.5f); }
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

            var normal = GetImpactNormal(hit, incoming);
            Reflect(incoming, normal);
            lastCollisionNormal = normal;
            var colliderId = hit.collider.GetInstanceID();
            repeatedCollisionCount = colliderId == lastColliderId && Time.time - lastCollisionAt < .5f ? repeatedCollisionCount + 1 : 1;
            lastColliderId = colliderId;
            lastCollisionAt = Time.time;
            if (repeatedCollisionCount >= 4) RecoverFromStuck();
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
            float currentSpeed = body.linearVelocity.sqrMagnitude > .001f ? body.linearVelocity.magnitude : speed;
            lastVelocity = reflected * currentSpeed;
            body.linearVelocity = lastVelocity;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.isTrigger && (other.name == "BottomBoundary" || other.CompareTag("ProjectileKill")))
                Exit();
        }

        void Update()
        {
            if (launched && !exiting && Time.time >= nextStuckSample)
            {
                float moved = Vector2.Distance(transform.position, lastSamplePosition);
                if (moved < .08f || body.linearVelocity.magnitude < speed * .25f) RecoverFromStuck();
                lastSamplePosition = transform.position;
                nextStuckSample = Time.time + .7f;
            }
            if (launched && transform.position.y < -5.2f) Exit();
            else if (launched && Time.time - startedAt > 18f) { Debug.Log("[Projectile] Timeout ID "+GetInstanceID()); ForceResolve(true); }
        }

        void RecoverFromStuck()
        {
            var direction = body.linearVelocity.sqrMagnitude > .001f ? body.linearVelocity.normalized : lastVelocity.sqrMagnitude > .001f ? lastVelocity.normalized : Vector2.up;
            if (lastCollisionNormal.sqrMagnitude > .01f) body.position += lastCollisionNormal.normalized * .08f;
            lastVelocity = direction * Mathf.Max(speed, body.linearVelocity.magnitude);
            body.linearVelocity = lastVelocity;
            repeatedCollisionCount = 0;
            Debug.Log("[Projectile] Stuck detected ID " + GetInstanceID());
            Debug.Log("[Projectile] Recovery applied ID " + GetInstanceID());
        }

        public void ForceResolve(bool notifyOwner = true)
        {
            if (exiting) return;
            exiting = true;
            body.simulated = false;
            if (notifyOwner && game) game.ProjectileFinished(this, transform.position.x);
            Destroy(gameObject);
        }

        void Exit()
        {
            if (exiting)
                return;
            exiting = true;
            body.simulated = false;
            if (game) game.ProjectileFinished(this, transform.position.x);
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
