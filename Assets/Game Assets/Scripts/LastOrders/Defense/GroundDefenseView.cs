using DG.Tweening;
using UnityEngine;

namespace LastOrders.Defense
{
    /// <summary>
    /// How a ground defense model shows what is happening to it: standing, damaged or wrecked; a
    /// barricade shaking under each blow; a trap's spikes punching up when it springs and sticking
    /// half up once it jams; wire trembling while something is caught in it.
    ///
    /// On the model's prefab. The slot it stands on tells it what to show - it knows nothing about
    /// the rules - and every part is optional, so one component serves all three kinds.
    /// </summary>
    public sealed class GroundDefenseView : MonoBehaviour
    {
        [Header("States")]
        [Tooltip("Shown while it is in good shape.")]
        [SerializeField] GameObject intact;

        [Tooltip("Optional. Shown instead once it is below its damaged share of health.")]
        [SerializeField] GameObject damaged;

        [Tooltip("Optional. Shown instead once it is worn out: battered down, torn.")]
        [SerializeField] GameObject wrecked;

        [Tooltip("Optional. Shaken on every blow and every worker's hammer.")]
        [SerializeField] Transform shaker;

        [SerializeField] float shakeStrength = 0.06f;

        [Header("Trap")]
        [Tooltip("Optional. The spike bed: raised along its own Y when the trap springs.")]
        [SerializeField] Transform spikes;

        [Tooltip("Metres the spikes come up on a strike.")]
        [SerializeField] float spikeRise = 0.24f;

        [Tooltip("Metres they stick up once the trap has jammed.")]
        [SerializeField] float jammedRise = 0.1f;

        [Header("Snare")]
        [Tooltip("Optional. Trembled about their own X while anything is caught.")]
        [SerializeField] Transform[] tremblers;

        [Tooltip("Degrees either way they tremble with one zombie caught; more with more.")]
        [SerializeField] float trembleDegrees = 5f;

        Vector3 spikesRest;
        Quaternion[] trembleRest;
        float trembleWeight;
        int caught;
        bool jammed;
        bool started;

        void Awake()
        {
            if (spikes != null) spikesRest = spikes.localPosition;

            trembleRest = new Quaternion[tremblers != null ? tremblers.Length : 0];
            for (int i = 0; i < trembleRest.Length; i++)
                if (tremblers[i] != null) trembleRest[i] = tremblers[i].localRotation;

            started = true;
        }

        /// <summary>Shows the state it is in now: <paramref name="fraction"/> of its health left, or worn out.</summary>
        public void Show(float fraction, bool worn, float damagedBelow)
        {
            if (!started) Awake();

            bool showWrecked = worn && wrecked != null;
            bool showDamaged = !showWrecked && !worn && fraction < damagedBelow && damaged != null;

            SetActive(intact, !showWrecked && !showDamaged);
            SetActive(damaged, showDamaged);
            SetActive(wrecked, showWrecked);

            jammed = worn;
            if (spikes == null) return;

            spikes.DOKill();
            spikes.localPosition = spikesRest + Vector3.up * (worn ? jammedRise : 0f);
        }

        /// <summary>A blow landed on it.</summary>
        public void Hit()
        {
            Transform t = shaker != null ? shaker : transform;
            t.DOKill(true);
            t.DOShakePosition(0.22f, new Vector3(shakeStrength, 0f, shakeStrength), 18, 90f, false, true);
        }

        /// <summary>The trap sprang: the spikes punch up, hold, and sink back.</summary>
        public void Strike()
        {
            if (spikes == null || jammed) return;

            spikes.DOKill();
            spikes.localPosition = spikesRest;

            Sequence strike = DOTween.Sequence().SetTarget(spikes);
            strike.Append(spikes.DOLocalMove(spikesRest + Vector3.up * spikeRise, 0.06f).SetEase(Ease.OutQuad));
            strike.AppendInterval(0.35f);
            strike.Append(spikes.DOLocalMove(spikesRest, 0.4f).SetEase(Ease.InQuad));
        }

        /// <summary>How many zombies are caught in it now. Read every frame.</summary>
        public void Hold(int count) => caught = count;

        void Update()
        {
            if (trembleRest == null || trembleRest.Length == 0) return;

            // Eases in and out rather than snapping, so a zombie stepping free leaves the wire swaying.
            float goal = Mathf.Clamp01(caught * 0.6f);
            trembleWeight = Mathf.MoveTowards(trembleWeight, goal, Time.deltaTime * 2.5f);
            if (trembleWeight <= 0f && goal <= 0f) return;

            float t = Time.time;
            for (int i = 0; i < tremblers.Length; i++)
            {
                if (tremblers[i] == null) continue;

                float angle = Mathf.Sin(t * 23f + i * 1.7f) * Mathf.Sin(t * 7.3f + i) * trembleDegrees * trembleWeight;
                tremblers[i].localRotation = trembleRest[i] * Quaternion.Euler(angle, 0f, angle * 0.4f);
            }
        }

        void OnDisable()
        {
            if (spikes != null) spikes.DOKill();
            if (shaker != null) shaker.DOKill(true);
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }

        /// <summary>Editor wiring, for the model builder.</summary>
        public void Configure(GameObject standing, GameObject battered, GameObject down, Transform shake, Transform spikeBed,
                              Transform[] wire)
        {
            intact = standing;
            damaged = battered;
            wrecked = down;
            shaker = shake;
            spikes = spikeBed;
            tremblers = wire;
        }
    }
}
