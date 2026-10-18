using System.Collections.Generic;
using LastOrders.Audio;
using LastOrders.Effects;
using UnityEngine;

namespace LastOrders.Crew
{
    /// <summary>
    /// What a diner leaves on the table when they go: crumbs, a smear where the plate was, a screwed-up
    /// napkin. It stays until somebody wipes it away - the crew see to a mess before anything else - and
    /// goes with a little puff when they do. A new diner sat down in front of one pushes it aside.
    ///
    /// Pooled: <see cref="Leave"/> takes one, <see cref="Clean"/> puts it back.
    /// </summary>
    public sealed class TableMess : MonoBehaviour
    {
        /// <summary>Seconds for the mess to shrink away as it is wiped up.</summary>
        const float VanishSeconds = 0.3f;

        static readonly List<TableMess> spots = new List<TableMess>();
        static readonly Stack<TableMess> pool = new Stack<TableMess>();

        MeshFilter filter;
        float vanishing = -1f;

        public TableBehaviour Table { get; private set; }
        public int Chair { get; private set; }

        /// <summary>Still there to be wiped.</summary>
        public bool IsDirty => isActiveAndEnabled && vanishing < 0f;

        /// <summary>Every mess still on a table.</summary>
        public static IReadOnlyList<TableMess> All => spots;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            spots.Clear();
            pool.Clear();
        }

        /// <summary>
        /// Leaves a mess on the table top at <paramref name="at"/>, in front of chair
        /// <paramref name="chair"/> - or tops up the one already there.
        /// </summary>
        public static TableMess Leave(TableBehaviour table, int chair, Vector3 at)
        {
            if (table == null) return null;

            TableMess existing = At(table, chair);
            if (existing != null) return existing;

            TableMess mess = null;
            while (pool.Count > 0 && mess == null) mess = pool.Pop();
            if (mess == null) mess = Make();

            mess.Table = table;
            mess.Chair = chair;
            mess.vanishing = -1f;
            mess.filter.sharedMesh = CrewMeshes.Mess(Random.Range(0, 3));

            Transform t = mess.transform;
            t.SetParent(table.transform, true);
            t.SetPositionAndRotation(at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            t.localScale = Vector3.one;
            mess.gameObject.SetActive(true);

            spots.Add(mess);
            return mess;
        }

        /// <summary>The mess in front of chair <paramref name="chair"/> of <paramref name="table"/>, or null.</summary>
        public static TableMess At(TableBehaviour table, int chair)
        {
            for (int i = 0; i < spots.Count; i++)
                if (spots[i] != null && spots[i].Table == table && spots[i].Chair == chair && spots[i].IsDirty) return spots[i];
            return null;
        }

        public static bool IsDirtyAt(TableBehaviour table, int chair) => At(table, chair) != null;

        /// <summary>Wiped up: it shrinks away with a puff, or, <paramref name="quietly"/>, just goes.</summary>
        public void Clean(bool quietly = false)
        {
            if (!IsDirty) return;

            spots.Remove(this);
            if (quietly)
            {
                Recycle();
                return;
            }

            vanishing = 0f;
            PlacementJuice.Burst(transform.position + Vector3.up * 0.05f, 0.45f);
            SoundController.Play(SoundId.TableWipe, transform.position);
        }

        void Update()
        {
            if (vanishing < 0f) return;

            vanishing += Time.deltaTime / VanishSeconds;
            float s = 1f - vanishing;
            transform.localScale = Vector3.one * Mathf.Max(0.01f, s * (1f + 0.3f * Mathf.Sin(vanishing * Mathf.PI)));
            if (vanishing >= 1f) Recycle();
        }

        void Recycle()
        {
            vanishing = -1f;
            Table = null;
            gameObject.SetActive(false);
            transform.SetParent(null, false);
            pool.Push(this);
        }

        void OnDestroy() => spots.Remove(this);

        static TableMess Make()
        {
            var go = new GameObject("TableMess");
            var mess = go.AddComponent<TableMess>();
            mess.filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CrewMeshes.Paint;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            return mess;
        }
    }
}
