using System.Collections.Generic;
using LastOrders.Core;
using UnityEngine;

namespace LastOrders.Effects
{
    /// <summary>
    /// Plays pooled one-shot effects - the dust where a wall section goes up.
    ///
    /// On its own pool for the same reason the world UI is: dawn recalls the shared pool, and
    /// an effect recalled mid-play would be a puff of dust cut off in its first frame. Keeps
    /// its own record of what is out, so an effect released twice is not destroyed.
    /// </summary>
    public sealed class EffectsService
    {
        readonly PoolService pools;
        readonly HashSet<PooledEffect> live = new HashSet<PooledEffect>();

        public EffectsService(Transform root)
        {
            pools = new PoolService(root);
        }

        public int LiveCount => live.Count;

        /// <summary>Plays the effect once at a point. Does nothing for a null prefab, so an
        /// effect left unassigned costs a missing puff rather than an error.</summary>
        public PooledEffect Play(PooledEffect prefab, Vector3 position)
        {
            if (prefab == null) return null;

            PooledEffect effect = pools.Take(prefab, position, Quaternion.identity, prewarm: 4, maxSize: 64);
            if (effect == null) return null;

            live.Add(effect);
            effect.Play(this);
            return effect;
        }

        public void Release(PooledEffect effect)
        {
            if (effect == null || !live.Remove(effect)) return;
            pools.Return(effect);
        }
    }
}
