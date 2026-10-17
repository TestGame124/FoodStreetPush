using System.Collections.Generic;
using LastOrders.Jobs;
using UnityEngine;

namespace LastOrders.Crew
{
    /// <summary>
    /// Who a crew member is and what they are doing right now.
    ///
    /// Their duties - serving, helping at a station, chores, being carried by the player - are
    /// components beside this one (<see cref="ICrewDuty"/>). Every frame the body goes to the most
    /// important duty that has something to do; when that changes, the one losing it is told to drop
    /// what it holds and the body is put back to rest - arms down, prop away, eyes free - before the
    /// next one starts. With nothing to do anywhere the least important duty keeps it, so a crew
    /// member is never left standing with nobody in charge of them.
    ///
    /// It also owns the body parts the duties share: the legs (<see cref="NavWalker"/>), the eyes
    /// (<see cref="CharacterLookAt"/>), the hands (<see cref="CharacterReach"/>), what they hold
    /// (<see cref="CrewProps"/>) and their gestures.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavWalker))]
    public sealed class CrewBrain : MonoBehaviour
    {
        [Tooltip("What the panels and messages call them.")]
        [SerializeField] string displayName = "Mia";

        [Tooltip("How messages refer to them: \"Hold her and drag her...\".")]
        [SerializeField] string objectPronoun = "her";

        static readonly List<CrewBrain> all = new List<CrewBrain>();

        readonly List<ICrewDuty> duties = new List<ICrewDuty>();
        ICrewDuty active;
        bool collected;

        NavWalker legs;
        Animator animator;
        CharacterLookAt look;
        CharacterReach reach;
        CrewProps props;
        GestureDriver gestures;
        bool canWork;
        bool working;
        int workStyle = -1;

        /// <summary>Every crew member on duty.</summary>
        public static IReadOnlyList<CrewBrain> All => all;

        public string DisplayName => displayName;

        /// <summary>"her", "him", "them" - for messages about them.</summary>
        public string Them => string.IsNullOrEmpty(objectPronoun) ? "them" : objectPronoun;

        public NavWalker Legs => legs;
        public Animator Animator => animator;
        public CharacterLookAt Look => look;
        public CharacterReach Reach => reach;
        public CrewProps Props => props;
        public GestureDriver Gestures => gestures;

        public ICrewDuty Active => active;

        public bool IsWorking => working;

        /// <summary>What they are doing, in words.</summary>
        public string Activity
        {
            get
            {
                string doing = active != null ? active.Activity : null;
                return string.IsNullOrEmpty(doing) ? "Taking a breather" : doing;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => all.Clear();

        void Awake()
        {
            legs = GetComponent<NavWalker>();
            animator = legs.Animator != null ? legs.Animator : GetComponentInChildren<Animator>();

            if (animator != null)
            {
                look = Ensure<CharacterLookAt>(animator.gameObject);
                reach = Ensure<CharacterReach>(animator.gameObject);
            }

            props = Ensure<CrewProps>(gameObject);
            gestures = new GestureDriver(animator);
            canWork = gestures.CanGesture;
        }

        void OnEnable() => all.Add(this);

        void OnDisable()
        {
            all.Remove(this);
            Switch(null);
        }

        /// <summary>The duty of type <typeparamref name="T"/> on this crew member, if they have one.</summary>
        public T Duty<T>() where T : class, ICrewDuty
        {
            Collect();
            for (int i = 0; i < duties.Count; i++)
                if (duties[i] is T duty) return duty;

            return null;
        }

        /// <summary>True when no duty more important than <paramref name="duty"/> wants the body.</summary>
        public bool IsFreeFor(ICrewDuty duty)
        {
            Collect();
            for (int i = 0; i < duties.Count; i++)
            {
                ICrewDuty other = duties[i];
                if (other == duty) return true;
                if (other.Priority > duty.Priority && IsOn(other) && other.HasWork) return false;
            }

            return true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Collect();

            ICrewDuty next = Pick();
            if (next != active) Switch(next);

            gestures.Tick(dt, legs.IsMoving);
            active?.Tick(dt);
        }

        ICrewDuty Pick()
        {
            ICrewDuty fallback = null;
            for (int i = 0; i < duties.Count; i++)
            {
                ICrewDuty duty = duties[i];
                if (!IsOn(duty)) continue;
                if (duty.HasWork) return duty;
                fallback = duty;
            }

            return fallback;
        }

        void Switch(ICrewDuty next)
        {
            ICrewDuty was = active;
            active = next;

            was?.SetActive(false);
            Rest();
            next?.SetActive(true);
        }

        /// <summary>Arms down, nothing in hand, eyes free, standing still: between one duty and the next.</summary>
        public void Rest()
        {
            if (legs != null) legs.Stop();
            gestures?.LetGo();
            SetWorking(false);
            if (props != null) props.Hold(CrewProp.None);
            if (reach != null) reach.Release();
            if (look != null) look.Release();
        }

        /// <summary>Plays the controller's work loop in <paramref name="style"/> (see <see cref="WorkerGestures"/>), or stops it.</summary>
        public void SetWorking(bool on, int style = WorkerGestures.WorkHands)
        {
            if (!canWork || animator == null) return;

            if (on && style != workStyle)
            {
                workStyle = style;
                animator.SetInteger(WorkerGestures.WorkStyleId, style);
            }

            if (on == working) return;

            working = on;
            animator.SetBool(WorkerGestures.WorkingId, on);
        }

        void Collect()
        {
            if (collected) return;
            collected = true;

            duties.Clear();
            foreach (MonoBehaviour behaviour in GetComponents<MonoBehaviour>())
                if (behaviour is ICrewDuty duty) duties.Add(duty);

            // Most important first; the last is the fallback.
            duties.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        /// <summary>Picks the duties up again - for one added while running.</summary>
        public void Refresh() => collected = false;

        static bool IsOn(ICrewDuty duty) => !(duty is Behaviour behaviour) || behaviour.isActiveAndEnabled;

        static T Ensure<T>(GameObject on) where T : Component
        {
            T found = on.GetComponent<T>();
            return found != null ? found : on.AddComponent<T>();
        }
    }
}
