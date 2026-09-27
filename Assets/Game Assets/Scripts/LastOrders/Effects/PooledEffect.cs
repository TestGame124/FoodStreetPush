using LastOrders.Core;
using UnityEngine;

namespace LastOrders.Effects
{
    /// <summary>
    /// A one-shot particle effect that plays and then returns itself to its pool.
    ///
    /// Every particle system under it plays together; the effect waits out the longest one,
    /// then goes back. Particles are cleared on the way in, because <see cref="PoolIntegrity"/>
    /// rejects an instance released with live particles - they would otherwise reappear at
    /// wherever it is played next.
    /// </summary>
    public sealed class PooledEffect : MonoBehaviour, IPoolable, IPoolRotationExempt
    {
        [Tooltip("Seconds added after the longest system finishes, so the tail is not cut off.")]
        [SerializeField] float tail = 0.1f;

        ParticleSystem[] systems;
        EffectsService owner;
        float lifetime;
        float remaining;

        void Awake()
        {
            systems = GetComponentsInChildren<ParticleSystem>(true);

            foreach (ParticleSystem system in systems)
            {
                ParticleSystem.MainModule main = system.main;
                lifetime = Mathf.Max(lifetime, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
            }

            lifetime += tail;
        }

        internal void Play(EffectsService service)
        {
            owner = service;
            remaining = lifetime;

            foreach (ParticleSystem system in systems)
            {
                system.Clear(true);
                system.Play(true);
            }
        }

        void Update()
        {
            if (owner == null) return;

            remaining -= Time.deltaTime;
            if (remaining > 0f) return;

            owner.Release(this);
        }

        public void OnTakenFromPool() { }

        public void OnReturnedToPool()
        {
            if (systems != null)
            {
                foreach (ParticleSystem system in systems)
                {
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    system.Clear(true);
                }
            }

            owner = null;
            remaining = 0f;
        }
    }
}
