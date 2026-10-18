using System.Collections.Generic;
using LastOrders.Core;
using LastOrders.Field;
using LastOrders.Night;
using UnityEngine;

namespace LastOrders.Audio
{
    /// <summary>
    /// The horde's voice: a groan every second or two from one of the zombies on the map, picked
    /// from those nearest the middle of the screen.
    ///
    /// One timer for the whole horde rather than one per zombie. Forty zombies each groaning on
    /// their own clock is a wall of noise that says nothing; one voice at a time, coming more often
    /// as the horde grows, says how many there are and where. Hears the night's zombies and the ones
    /// lurking on land still to be cleared alike.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ZombieVoices : MonoBehaviour
    {
        [SerializeField] GameLifetimeScope scope;

        [Tooltip("Seconds between groans with a single zombie about, picked anew each time between these two.")]
        [SerializeField] Vector2 secondsBetween = new Vector2(2.2f, 5f);

        [Tooltip("How much faster the groans come with a crowd: the gap is divided by the square root " +
                 "of how many there are, but by no more than this.")]
        [SerializeField, Min(1f)] float mostCrowding = 3.5f;

        [Tooltip("Zombies drawn at random each time; the one nearest the listener groans.")]
        [SerializeField, Range(1, 8)] int candidates = 4;

        readonly List<Vector3> heard = new List<Vector3>();

        GameComposition composition;
        float wait;

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(GameLifetimeScope lifetimeScope) => scope = lifetimeScope;

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;
            if (composition == null) enabled = false;

            wait = Random.Range(secondsBetween.x, secondsBetween.y);
        }

        void Update()
        {
            wait -= Time.deltaTime;
            if (wait > 0f) return;

            Gather();
            wait = Random.Range(secondsBetween.x, secondsBetween.y) / Mathf.Clamp(Mathf.Sqrt(heard.Count), 1f, mostCrowding);

            if (heard.Count == 0) return;
            SoundController.Play(SoundId.ZombieGroan, Nearest());
        }

        void Gather()
        {
            heard.Clear();

            IReadOnlyList<EnemyAgent> night = composition.Night.LiveEnemies;
            for (int i = 0; i < night.Count; i++)
                if (night[i] != null && night[i].IsAlive) heard.Add(night[i].transform.position);

            IReadOnlyList<IAreaZombie> lurking = composition.Field != null ? composition.Field.Zombies : null;
            if (lurking == null) return;

            for (int i = 0; i < lurking.Count; i++)
                if (lurking[i] != null && lurking[i].IsAlive) heard.Add(lurking[i].Position);
        }

        Vector3 Nearest()
        {
            if (!SoundController.TryGetListenerPosition(out Vector3 ears)) return heard[Random.Range(0, heard.Count)];

            Vector3 best = heard[Random.Range(0, heard.Count)];
            float bestDistance = (best - ears).sqrMagnitude;

            for (int i = 1; i < candidates; i++)
            {
                Vector3 other = heard[Random.Range(0, heard.Count)];
                float distance = (other - ears).sqrMagnitude;
                if (distance >= bestDistance) continue;

                best = other;
                bestDistance = distance;
            }

            return best;
        }
    }
}
