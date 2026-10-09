using DG.Tweening;
using LastOrders.Core;
using LastOrders.Defense;
using LastOrders.Interaction;
using LastOrders.Jobs;
using UnityEngine;

namespace LastOrders.Field
{
    /// <summary>
    /// A zombie lurking on land the restaurant wants back: it ambles about its patch until a
    /// worker sent to clear the land comes near, then goes for them.
    ///
    /// Different from the town's wanderers, which are scenery. This one fights - it closes in,
    /// swings every second and a half, and it can be killed. It never leaves its patch: it will
    /// not follow anyone back through the gap in the wall, which is what makes Recall Worker a
    /// way out. It leaves the downed alone, and whoever comes to carry them.
    ///
    /// Made by <see cref="AreaZombies"/>, which gives it its numbers. Dead ones stay dead: the
    /// kill is recorded, and the area never spawns it again.
    ///
    /// A tower or turret near enough shoots it like any other zombie, and a kill that way is
    /// recorded the same.
    /// </summary>
    [DefaultExecutionOrder(1170)]
    public sealed class AreaZombie : MonoBehaviour, IAreaZombie, IDefenseTarget, ITappable, IInteractableObject,
                                     IWorkStrikeReceiver
    {
        public struct Tuning
        {
            public float Health;
            public float Damage;
            public float AttackInterval;
            public float AttackRange;
            public float AggroRadius;
            public float ChaseSpeed;
            public Vector2 WanderSpeed;
            public Vector2 WanderPause;
            public Rect Patch;

            /// <summary>How far past the patch edge it will follow someone before stopping.</summary>
            public float Leash;
        }

        const float TurnSpeed = 240f;

        GameComposition composition;
        FieldWorkService field;
        Tuning tuning;
        string zombieId;
        string areaId;
        float health;
        bool dying;

        Vector3 wanderTarget;
        float wanderSpeed;
        float pause;
        float attackCooldown;

        public string ZombieId => zombieId;
        public string AreaId => areaId;
        public Vector3 Position => transform.position;
        public bool IsAlive => !dying && health > 0f;
        public float Health => health;
        public float MaxHealth => tuning.Health;

        public string DisplayName => "Zombie";

        /// <summary>Called once, straight after it is made.</summary>
        public void Configure(GameComposition game, string id, string area, Tuning numbers)
        {
            composition = game;
            field = game.Field;
            zombieId = id;
            areaId = area;
            tuning = numbers;
            health = Mathf.Max(1f, numbers.Health);

            PickWanderTarget();
            pause = Random.Range(0f, tuning.WanderPause.y);

            EnsureTapCollider();
            field.RegisterZombie(this);
        }

        void OnDestroy()
        {
            if (field != null) field.UnregisterZombie(this);
            DOTween.Kill(transform);
        }

        void Update()
        {
            if (field == null || dying) return;

            // The morning screen stops the world, and they are part of it.
            if (composition.Time.IsPaused) return;

            float dt = Time.deltaTime;
            if (attackCooldown > 0f) attackCooldown -= dt;

            IFieldWorker target = field.NearestExposed(transform.position, tuning.AggroRadius);

            if (target != null && InLeash(target.Position)) Attack(target, dt);
            else Wander(dt);
        }

        void Attack(IFieldWorker target, float dt)
        {
            Vector3 here = transform.position;
            Vector3 there = new Vector3(target.Position.x, here.y, target.Position.z);
            Vector3 offset = there - here;
            float distance = offset.magnitude;

            if (distance > 1e-3f) Turn(offset / distance, dt);

            if (distance > tuning.AttackRange)
            {
                Vector3 step = offset / distance * Mathf.Min(tuning.ChaseSpeed * dt, distance - tuning.AttackRange * 0.9f);
                transform.position = Clamp(here + step);
                return;
            }

            if (attackCooldown > 0f) return;

            attackCooldown = tuning.AttackInterval;

            // A lunge: the only sign of the swing there is, with no attack clip in the project.
            transform.DOKill(true);
            transform.DOPunchPosition(offset.normalized * 0.3f, 0.3f, 1, 0f);

            field.DamageWorker(target, tuning.Damage);
        }

        void Wander(float dt)
        {
            if (pause > 0f)
            {
                pause -= dt;
                return;
            }

            Vector3 here = transform.position;
            Vector3 goal = new Vector3(wanderTarget.x, here.y, wanderTarget.z);
            Vector3 offset = goal - here;
            float distance = offset.magnitude;

            if (distance <= 0.2f)
            {
                pause = Random.Range(tuning.WanderPause.x, tuning.WanderPause.y);
                PickWanderTarget();
                return;
            }

            Vector3 direction = offset / distance;
            transform.position = here + direction * Mathf.Min(wanderSpeed * dt, distance);
            Turn(direction, dt);
        }

        void PickWanderTarget()
        {
            Rect patch = tuning.Patch;
            wanderTarget = new Vector3(Random.Range(patch.xMin, patch.xMax), transform.position.y,
                                       Random.Range(patch.yMin, patch.yMax));
            wanderSpeed = Random.Range(tuning.WanderSpeed.x, tuning.WanderSpeed.y);
        }

        void Turn(Vector3 direction, float dt)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) return;

            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction),
                                                          TurnSpeed * dt);
        }

        bool InLeash(Vector3 point)
        {
            Rect wide = Widened();
            return wide.Contains(new Vector2(point.x, point.z));
        }

        Vector3 Clamp(Vector3 point)
        {
            Rect wide = Widened();
            return new Vector3(Mathf.Clamp(point.x, wide.xMin, wide.xMax), point.y, Mathf.Clamp(point.z, wide.yMin, wide.yMax));
        }

        Rect Widened()
        {
            Rect patch = tuning.Patch;
            float leash = tuning.Leash;
            return Rect.MinMaxRect(patch.xMin - leash, patch.yMin - leash, patch.xMax + leash, patch.yMax + leash);
        }

        // --- Taking hits --------------------------------------------------------------------

        public void ApplyDamage(float amount)
        {
            if (!IsAlive || amount <= 0f) return;

            health = Mathf.Max(0f, health - amount);
            if (health <= 0f) Die();
        }

        /// <summary>A defense's shot. Goes through the field service, so a kill is on record.</summary>
        void IDefenseTarget.ApplyDamage(float amount)
        {
            if (field != null) field.ShootZombie(this, amount);
            else ApplyDamage(amount);
        }

        /// <summary>A blow of the worker's tool landed: a stagger, so the hit reads.</summary>
        public void OnStrike()
        {
            if (dying) return;

            transform.DOKill(true);
            transform.DOPunchScale(Vector3.one * 0.12f, 0.2f, 6, 0.5f);
        }

        /// <summary>Falls flat and sinks away. The kill is already on record.</summary>
        void Die()
        {
            dying = true;

            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;

            transform.DOKill(true);

            Vector3 fallen = transform.eulerAngles + new Vector3(-90f, 0f, 0f);

            Sequence fall = DOTween.Sequence().SetTarget(transform);
            fall.Append(transform.DORotate(fallen, 0.45f).SetEase(Ease.InQuad));
            fall.AppendInterval(0.9f);
            fall.Append(transform.DOMove(transform.position + Vector3.down * 0.8f, 1.2f).SetEase(Ease.InQuad));
            fall.OnComplete(() => gameObject.SetActive(false));

            if (composition != null)
                composition.WorldUi.ShowReward(transform.position + Vector3.up * 1.6f, "Zombie down!");
        }

        /// <summary>Taken off the land without a fight - the area was claimed round it.</summary>
        public void Vanish()
        {
            dying = true;
            gameObject.SetActive(false);
        }

        // --- Tapping ------------------------------------------------------------------------

        public bool OnTouch() => TapFeedback.Report(OnTapped());

        public string OnTapped()
        {
            if (field == null) return "A zombie.";

            if (FieldPanels.OpenArea(composition, areaId)) return null;

            return "A zombie on land the restaurant wants back. Send a worker to clear the area.";
        }

        void EnsureTapCollider()
        {
            if (GetComponentInChildren<Collider>() != null) return;

            var capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.4f;
        }
    }
}
