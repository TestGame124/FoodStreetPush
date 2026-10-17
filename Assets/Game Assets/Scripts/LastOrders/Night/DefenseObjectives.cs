using System;
using System.Collections.Generic;
using LastOrders.Consequence;
using LastOrders.Defense;
using LastOrders.Perimeter;
using UnityEngine;

namespace LastOrders.Night
{
    /// <summary>
    /// One defense as the horde sees it: where it stands, whether it is still worth going for, and
    /// what a blow on it does. A gun up on the wall is reached through its bastion; one on the ground
    /// is hit itself.
    /// </summary>
    public interface IDefenseObjective : IRaidTarget
    {
        string DisplayName { get; }

        /// <summary>Where the defense stands.</summary>
        Vector3 Position { get; }

        /// <summary>Whom it is aiming at right now, or null: the zombie it is shooting at.</summary>
        object AimingAt { get; }

        /// <summary>Up on a bastion over the compound wall. Reached only from outside the walls: a zombie
        /// that is already over the wall has gone past it.</summary>
        bool OnTheWall { get; }

        /// <summary>Which way a gun on the wall faces - out of the compound, flat and unit length. Only a
        /// zombie in front of that face goes for it: one round the corner would walk into the wall
        /// beside it. Zero for a defense on the ground.</summary>
        Vector3 Outward { get; }

        /// <summary>Built, working, and not yet knocked out: still worth going for.</summary>
        bool IsStanding { get; }

        /// <summary>What is left of whatever has to be brought down, 0 to 1.</summary>
        float HealthFraction { get; }

        /// <summary>One blow. True when it was the blow that knocked the defense out.</summary>
        bool TakeHit(float damage, Vector3 from);
    }

    /// <summary>A gun up on the compound wall. Zombies get at it by bringing its bastion down: when that
    /// falls, the gun goes over with it and is out of action until the bastion is rebuilt.</summary>
    public sealed class BastionObjective : IDefenseObjective
    {
        readonly DefensePost post;
        readonly CompoundWall bastion;

        /// <param name="isInside">Whether a point is inside the compound, to tell which face of the
        /// bastion is the outside one. Without it the bastion's +Z is taken as out, as it is built.</param>
        public BastionObjective(DefensePost post, CompoundWall bastion, Func<Vector3, bool> isInside = null)
        {
            this.post = post;
            this.bastion = bastion;
            Outward = MeasureOutward(bastion, isInside);
        }

        public Vector3 Outward { get; }

        /// <summary>Across the wall - a wall piece runs along its own X - turned to whichever side is outside.</summary>
        static Vector3 MeasureOutward(CompoundWall wall, Func<Vector3, bool> isInside)
        {
            if (wall == null) return Vector3.zero;

            Vector3 across = Quaternion.Euler(0f, wall.transform.eulerAngles.y, 0f) * Vector3.forward;
            if (isInside == null) return across;

            Vector3 at = wall.transform.position;
            bool ahead = isInside(at + across * 2f), behind = isInside(at - across * 2f);
            return ahead && !behind ? -across : across;
        }

        public DefensePost Post => post;

        public CompoundWall Bastion => bastion;

        public string DisplayName => post != null ? post.DisplayName : "defense";

        public Vector3 Position => post != null ? post.transform.position : bastion.transform.position;

        public object AimingAt => post != null && post.Unit != null ? post.Unit.Target : null;

        public bool OnTheWall => true;

        public bool IsStanding =>
            post != null && bastion != null && post.isActiveAndEnabled && bastion.IsInTheWay &&
            post.Unit != null && post.Unit.IsBuilt && !post.Unit.IsOutOfAction && !post.Unit.IsWrecked;

        public float HealthFraction => bastion != null ? bastion.HealthFraction : 0f;

        /// <summary>Hunting a gun is worth smashing a barricade on the way to it.</summary>
        public bool WorthBreakingThroughFor => true;

        public Vector3 ClosestPoint(Vector3 from) => bastion != null ? bastion.ClosestPoint(from) : from;

        public float FlatDistance(Vector3 from)
        {
            Vector3 closest = ClosestPoint(from);
            return new Vector2(closest.x - from.x, closest.z - from.z).magnitude;
        }

        public bool TakeHit(float damage, Vector3 from) => bastion != null && bastion.TakeHit(damage, from);
    }

    /// <summary>A defense standing on the ground - on a ring pad outside the walls or in the yard. Hit
    /// itself: knocked out, it slumps, darkens and stops firing until a repair kit mends it.</summary>
    public sealed class GroundDefenseObjective : IDefenseObjective
    {
        readonly DefensePost post;
        readonly OutdoorWreck wreck;
        readonly Action<OutdoorWreck> onKnockedOut;

        public GroundDefenseObjective(DefensePost post, OutdoorWreck wreck, Action<OutdoorWreck> onKnockedOut)
        {
            this.post = post;
            this.wreck = wreck;
            this.onKnockedOut = onKnockedOut;
        }

        public DefensePost Post => post;

        public OutdoorWreck Wreck => wreck;

        public string DisplayName => post != null ? post.DisplayName : "defense";

        public Vector3 Position => post != null ? post.transform.position : wreck.transform.position;

        public object AimingAt => post != null && post.Unit != null ? post.Unit.Target : null;

        public bool OnTheWall => false;

        public Vector3 Outward => Vector3.zero;

        public bool IsStanding =>
            post != null && wreck != null && post.isActiveAndEnabled && !wreck.IsWrecked &&
            post.Unit != null && post.Unit.IsBuilt && !post.Unit.IsOutOfAction && !post.Unit.IsWrecked;

        public float HealthFraction => wreck != null && wreck.MaxHealth > 0f ? Mathf.Clamp01(wreck.Health / wreck.MaxHealth) : 0f;

        public bool WorthBreakingThroughFor => true;

        public Vector3 ClosestPoint(Vector3 from) => wreck != null ? wreck.ClosestPoint(from) : from;

        public float FlatDistance(Vector3 from) => wreck != null ? wreck.FlatDistance(from) : float.MaxValue;

        public bool TakeHit(float damage, Vector3 from)
        {
            if (wreck == null || !wreck.TakeHit(damage, from)) return false;

            onKnockedOut?.Invoke(wreck);
            return true;
        }
    }

    /// <summary>
    /// Finds every built defense in the scene and hands the horde one objective for each - the same
    /// object for the same defense from one look to the next, so a zombie sent at it is still sent at
    /// it after a defense is built elsewhere.
    /// </summary>
    public sealed class DefenseObjectiveScanner
    {
        readonly OutdoorDamageService outdoor;
        readonly Func<float> groundHealth;
        readonly Func<Vector3, bool> isInside;

        readonly Dictionary<DefensePost, IDefenseObjective> known = new Dictionary<DefensePost, IDefenseObjective>();
        readonly List<IDefenseObjective> found = new List<IDefenseObjective>();
        readonly List<DefensePost> gone = new List<DefensePost>();

        public DefenseObjectiveScanner(OutdoorDamageService outdoor, Func<float> groundHealth, Func<Vector3, bool> isInside)
        {
            this.outdoor = outdoor;
            this.groundHealth = groundHealth;
            this.isInside = isInside;
        }

        public IReadOnlyList<IDefenseObjective> Scan()
        {
            found.Clear();

            foreach (DefensePost post in DefensePost.Standing)
            {
                if (post == null || post.Unit == null || !post.Unit.IsBuilt || !post.isActiveAndEnabled) continue;

                CompoundWall bastion = BastionUnder(post);
                known.TryGetValue(post, out IDefenseObjective objective);

                // Moved off the wall or onto it since the last look: what has to be hit changed.
                bool stale = objective == null ||
                             (bastion != null && !(objective is BastionObjective b && b.Bastion == bastion)) ||
                             (bastion == null && !(objective is GroundDefenseObjective));

                if (stale)
                {
                    objective = bastion != null ? new BastionObjective(post, bastion, isInside) : Ground(post);
                    known[post] = objective;
                }

                found.Add(objective);
            }

            gone.Clear();
            foreach (DefensePost post in known.Keys)
                if (post == null) gone.Add(post);

            for (int i = 0; i < gone.Count; i++) known.Remove(gone[i]);

            return found;
        }

        /// <summary>The attack is locked in: every ground defense back to full health unless it is still
        /// knocked out from an earlier night, and every one known to the yard's repair.</summary>
        public void PrepareForAttack()
        {
            foreach (IDefenseObjective objective in Scan())
            {
                if (!(objective is GroundDefenseObjective ground) || ground.Wreck == null) continue;

                ground.Wreck.Configure(groundHealth != null ? groundHealth() : 90f, ground.DisplayName, isDefense: true);
                ground.Wreck.Heal();
            }
        }

        GroundDefenseObjective Ground(DefensePost post)
        {
            if (!post.TryGetComponent(out OutdoorWreck wreck)) wreck = post.gameObject.AddComponent<OutdoorWreck>();

            wreck.Configure(groundHealth != null ? groundHealth() : 90f, post.DisplayName, isDefense: true);

            // Known to the yard so the one repair kit mends it with everything else.
            outdoor?.Register(wreck);

            return new GroundDefenseObjective(post, wreck, outdoor != null ? outdoor.ReportDefenseWrecked : (Action<OutdoorWreck>)null);
        }

        /// <summary>The bastion a defense stands on: at a wall site, or mounted from the shop. Null on the ground.</summary>
        static CompoundWall BastionUnder(DefensePost post)
        {
            WallMount mount = post.GetComponentInParent<WallMount>();
            if (mount != null && mount.IsMounted && mount.Bastion != null) return mount.Bastion;

            WallSite site = post.GetComponentInParent<WallSite>();
            return site != null && !site.OnGround ? site.Bastion : null;
        }
    }
}
