using System.Collections.Generic;
using UnityEngine;

namespace FoodStreet.Navigation
{
    /// <summary>
    /// The one place agent movement is tuned.
    ///
    /// Everything above — an A* follower chasing waypoints, a flow-field agent reading a
    /// direction off the field — reduces to the same thing: a unit vector saying "this way".
    /// This component turns that into motion, adds boids-style separation so crowds part
    /// instead of stacking, keeps bodies out of geometry, and turns the model to face
    /// where it is going. Change how agents feel here and every agent changes together.
    ///
    /// Movement is applied to the transform. There is no rigidbody and no physics step
    /// involved: the grid already knows what is solid, so pushing bodies around with
    /// colliders would only be a second, disagreeing opinion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteeringAgent : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] float speed = 3.5f;
        [SerializeField] float acceleration = 14f;
        [SerializeField] float angularSpeed = 720f;

        [Header("Body")]
        [Tooltip("Used both for separation and to pick which gaps the pathfinder is " +
                 "allowed to route this agent through.")]
        [SerializeField] float radius = 0.3f;

        [Header("Separation")]
        [SerializeField] float separationRadius = 0.75f;
        [SerializeField, Range(0f, 3f)] float separationWeight = 1.1f;

        /// <summary>Set false to stop contributing to and reacting to the crowd, e.g. while
        /// an agent is parked in a chair.</summary>
        public bool avoidsOthers = true;

        /// <summary>Who this agent is to a <see cref="IMovementBarrier"/>. A zombie is stopped by
        /// every wall and never let through a gate; anything friendly only waits at a shut one.</summary>
        public MoverKind Mover { get; set; } = MoverKind.Friendly;

        /// <summary>The barrier that refused this agent's last step, or null when it moved
        /// freely. Held until the next step is tried, so whatever reads it sees it whichever
        /// order the two run in.</summary>
        public IMovementBarrier BlockedBy { get; private set; }

        public float Speed => speed;
        public float Radius => radius;

        /// <summary>
        /// A share of its own speed it is held to, on top of whatever the follower asks for: wire a
        /// zombie is wading through, a trap that has it pinned. One is its full pace. Separate from
        /// <see cref="ConfigureBody"/>, so letting go never needs to know what its pace was.
        /// </summary>
        public float SpeedScale
        {
            get => speedScale;
            set => speedScale = Mathf.Clamp01(value);
        }

        float speedScale = 1f;

        /// <summary>
        /// Overrides the authored movement profile at runtime.
        ///
        /// Needed because pooled agents are shared across archetypes: one enemy prefab's
        /// instances serve as Shamblers at 1.0 and Runners at 2.2, and the pool hands back
        /// whichever instance is free. Without this the archetype's speed would be whatever
        /// the previous occupant happened to leave behind.
        /// </summary>
        public void ConfigureBody(float newSpeed, float newRadius)
        {
            speed = Mathf.Max(0.05f, newSpeed);
            radius = Mathf.Max(0.05f, newRadius);
        }

        /// <summary>Metres per second actually travelled last frame. Animators read this.</summary>
        public Vector3 Velocity => velocity;

        /// <summary>True while nothing is asking this agent to move.</summary>
        public bool IsIdle => desiredDirection.sqrMagnitude < 0.0001f;

        Vector3 desiredDirection;
        float desiredSpeedScale = 1f;
        Vector3 velocity;

        static readonly List<SteeringAgent> neighbourScratch = new List<SteeringAgent>(32);

        void OnEnable() => NavigationSystem.RegisterAgent(this);

        void OnDisable()
        {
            NavigationSystem.UnregisterAgent(this);
            desiredDirection = Vector3.zero;
            velocity = Vector3.zero;
            BlockedBy = null;
        }

        /// <summary>
        /// The single input. Callers hand over a world-space direction each frame they want
        /// movement; a zero vector means stop. Nothing is latched, so a follower that stops
        /// calling this stops the agent without needing to say so.
        /// </summary>
        public void SetDesiredDirection(Vector3 direction, float speedScale = 1f)
        {
            direction.y = 0f;
            float magnitude = direction.magnitude;

            desiredDirection = magnitude > 0.0001f ? direction / magnitude : Vector3.zero;
            desiredSpeedScale = Mathf.Clamp01(speedScale);
        }

        public void Halt()
        {
            desiredDirection = Vector3.zero;
            velocity = Vector3.zero;
            BlockedBy = null;
        }

        /// <summary>Teleport. Clears momentum, because carrying velocity across a jump
        /// makes the agent leave the new spot the instant it arrives.</summary>
        public void Warp(Vector3 position)
        {
            transform.position = position;
            Halt();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float pace = speed * speedScale;
            Vector3 target = desiredDirection * (pace * desiredSpeedScale);

            if (avoidsOthers && separationWeight > 0f)
            {
                target += Separation() * (pace * separationWeight);
                target = Vector3.ClampMagnitude(target, pace);
            }

            velocity = Vector3.MoveTowards(velocity, target, acceleration * dt);

            if (velocity.sqrMagnitude > 0.000001f)
            {
                transform.position = Resolve(transform.position, velocity * dt);
                Face(velocity, dt);
            }
            else
            {
                BlockedBy = null;
            }

            // Consumed. A follower that goes quiet this frame coasts to a stop rather than
            // driving off with a stale direction.
            desiredDirection = Vector3.zero;
        }

        Vector3 Separation()
        {
            NavigationSystem nav = NavigationSystem.Instance;
            if (nav == null) return Vector3.zero;

            nav.CollectNeighbours(transform.position, neighbourScratch);
            if (neighbourScratch.Count <= 1) return Vector3.zero;

            Vector3 position = transform.position;
            Vector3 push = Vector3.zero;

            for (int i = 0; i < neighbourScratch.Count; i++)
            {
                SteeringAgent other = neighbourScratch[i];
                if (other == null || other == this || !other.avoidsOthers) continue;

                Vector3 offset = position - other.transform.position;
                offset.y = 0f;

                float sqr = offset.sqrMagnitude;
                float range = separationRadius + other.separationRadius;
                if (sqr >= range * range) continue;

                if (sqr < 0.0001f)
                {
                    // Exactly co-located, usually the frame two agents spawn on the same
                    // point. Any consistent nudge beats dividing by zero.
                    push += new Vector3(other.GetInstanceID() < GetInstanceID() ? 1f : -1f, 0f, 0f);
                    continue;
                }

                float distance = Mathf.Sqrt(sqr);
                push += offset / distance * (1f - distance / range);
            }

            return Vector3.ClampMagnitude(push, 1f);
        }

        /// <summary>
        /// Applies a step, refusing any component of it that would end inside geometry.
        /// Blocked axes are dropped independently so an agent brushing a wall slides along
        /// it instead of stopping dead.
        /// </summary>
        Vector3 Resolve(Vector3 position, Vector3 step)
        {
            GridService grid = NavigationSystem.Instance != null ? NavigationSystem.Instance.Grid : null;

            // The straight step is the one that says what is in the way. A slide along a wall
            // afterwards does not clear it: the wall is still what this agent walked into.
            Vector3 candidate = position + step;
            BlockedBy = MovementBarriers.FindBlocking(position, candidate, radius, Mover);

            if (BlockedBy == null && Standable(grid, candidate)) return candidate;

            Vector3 alongX = position + new Vector3(step.x, 0f, 0f);
            if (Passable(grid, position, alongX)) return alongX;

            Vector3 alongZ = position + new Vector3(0f, 0f, step.z);
            if (Passable(grid, position, alongZ)) return alongZ;

            velocity = Vector3.zero;
            return position;
        }

        bool Passable(GridService grid, Vector3 from, Vector3 to) =>
            Standable(grid, to) && MovementBarriers.FindBlocking(from, to, radius, Mover) == null;

        bool Standable(GridService grid, Vector3 world)
        {
            if (grid == null) return true;

            Vector2Int cell = grid.Grid.WorldToCell(world);

            // Outside the build grid is open world, not a wall. Agents spawn and despawn
            // out there and must be able to walk back in.
            if (!grid.Grid.InBounds(cell)) return true;

            return grid.IsWalkable(cell);
        }

        void Face(Vector3 direction, float dt)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, angularSpeed * dt);
        }
    }
}
