using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LastOrders.Core
{
    /// <summary>
    /// Catches pooled objects that are released dirty, at the moment of release.
    ///
    /// The failure this exists to prevent is specific and expensive: a pooled object
    /// reset incompletely does not break anything at the point of the bug. It breaks
    /// later, in a different object, on a different night, as an enemy that spawns
    /// already-dead or a customer that walks to a table it was heading for ten minutes
    /// ago. By then the stack trace points at innocent code.
    ///
    /// So every release is checked against the prefab in development builds. Compiled
    /// out entirely in release via <see cref="ConditionalAttribute"/> — the calls
    /// disappear, so this costs nothing in a shipping build.
    /// </summary>
    public static class PoolIntegrity
    {
        /// <summary>Turn off to silence the checks without recompiling, when profiling
        /// a night and the verification itself is distorting the frame.</summary>
        public static bool Enabled = true;

        /// <summary>Counts violations found this session. Surfaced by the debug overlay so
        /// a soak test can be judged without reading the console.</summary>
        public static int ViolationCount { get; private set; }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Verify(Component instance, Component prefab)
        {
            if (!Enabled || instance == null || prefab == null) return;

            Transform t = instance.transform;

            // Scale is the canary. Almost every hit/death effect in a game like this
            // punches scale, and a tween killed mid-flight leaves it wrong. An enemy
            // that spawns at 1.4x is the single most common pooling artefact.
            if ((t.localScale - prefab.transform.localScale).sqrMagnitude > 0.0001f)
            {
                Report(instance, $"localScale is {t.localScale}, prefab is {prefab.transform.localScale}. " +
                                 "A scale tween was probably not killed in OnReturnedToPool.");
            }

            if (!Mathf.Approximately(t.localRotation.w, prefab.transform.localRotation.w) &&
                Quaternion.Angle(t.localRotation, prefab.transform.localRotation) > 0.01f &&
                !AllowsResidualRotation(instance))
            {
                Report(instance, "localRotation was not reset.");
            }

            // An object released while a coroutine is still running will resume that
            // coroutine the instant it is reactivated, mid-way through whatever it was
            // doing. MonoBehaviours cannot be asked what coroutines they hold, so the
            // best available check is that the behaviour was not left disabled — the
            // other half of the same class of bug.
            if (instance is Behaviour behaviour && !behaviour.enabled &&
                prefab is Behaviour prefabBehaviour && prefabBehaviour.enabled)
            {
                Report(instance, "the component was left disabled but the prefab's is enabled. " +
                                 "It will spawn inert next time it is taken.");
            }

            VerifyParticles(instance);
            VerifyTrails(instance);
        }

        static void VerifyParticles(Component instance)
        {
            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i].particleCount > 0)
                {
                    Report(instance, $"particle system '{systems[i].name}' still holds " +
                                     $"{systems[i].particleCount} particles. Call Clear(true) " +
                                     "in OnReturnedToPool or they will appear at the next spawn point.");
                    return;
                }
            }
        }

        static void VerifyTrails(Component instance)
        {
            var trails = instance.GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i].positionCount > 0)
                {
                    Report(instance, $"trail renderer '{trails[i].name}' was not cleared. " +
                                     "It will draw a line from the despawn point to the next spawn point.");
                    return;
                }
            }
        }

        /// <summary>Some objects are pooled precisely because their facing is set on spawn
        /// and resetting it would be wasted work. They opt out by implementing this.</summary>
        static bool AllowsResidualRotation(Component instance) => instance is IPoolRotationExempt;

        static void Report(Component instance, string problem)
        {
            ViolationCount++;
            Debug.LogError(
                $"[PoolIntegrity] '{instance.name}' was released to the pool dirty: {problem}",
                instance);
        }

        public static void ResetCounters() => ViolationCount = 0;
    }

    /// <summary>Marks a pooled component whose rotation is always assigned on spawn, so
    /// leaving it stale is harmless and need not be reported.</summary>
    public interface IPoolRotationExempt { }
}
