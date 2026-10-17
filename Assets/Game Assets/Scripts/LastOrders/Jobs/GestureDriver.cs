using UnityEngine;

namespace LastOrders.Jobs
{
    /// <summary>
    /// Plays <see cref="Gesture"/>s on any animator that has the gesture contract
    /// (<see cref="WorkerGestures"/>): a worker fidgeting, the crew between chores, a diner patting
    /// their belly after a meal. One gesture at a time; a held one is let go after its hold, a one-shot
    /// once it has played out.
    ///
    /// Plain C#, owned by whoever decides what to gesture. On a controller whose gesture layer rests at
    /// weight 0 - the visitors', where the arms otherwise belong to the seated IK - it also brings the
    /// layer up while a gesture plays and lets it down after.
    /// </summary>
    public sealed class GestureDriver
    {
        /// <summary>Seconds for a managed gesture layer to come up, or go down.</summary>
        const float LayerSeconds = 0.25f;

        readonly Animator animator;
        readonly bool canGesture;
        readonly int managedLayer = -1;
        float layerWeight;

        public GestureDriver(Animator animator)
        {
            this.animator = animator;
            canGesture = WorkerGestures.CanGesture(animator);
            if (!canGesture) return;

            int layer = animator.GetLayerIndex(WorkerGestures.LayerName);
            if (layer > 0 && animator.GetLayerWeight(layer) <= 0f) managedLayer = layer;
        }

        /// <summary>The animator has the gesture parameters.</summary>
        public bool CanGesture => canGesture;

        public Gesture Current { get; private set; }

        /// <summary>Seconds left of the gesture playing.</summary>
        public float Left { get; private set; }

        public bool Busy => Current != Gesture.None;

        /// <summary>Starts a gesture; a held one is let go after <paramref name="hold"/> seconds. False when
        /// the animator cannot gesture.</summary>
        public bool Play(Gesture gesture, float hold = 0f)
        {
            if (!canGesture || gesture == Gesture.None) return false;

            animator.SetInteger(WorkerGestures.GestureId, (int)gesture);
            animator.SetTrigger(WorkerGestures.ActId);
            Current = gesture;
            Left = WorkerGestures.IsHeld(gesture) ? Mathf.Max(1f, hold) : WorkerGestures.Seconds(gesture) + 0.15f;
            return true;
        }

        public void LetGo()
        {
            if (Current == Gesture.None) return;

            Current = Gesture.None;
            Left = 0f;
            if (!canGesture) return;

            animator.SetInteger(WorkerGestures.GestureId, 0);
            animator.ResetTrigger(WorkerGestures.ActId);
        }

        /// <summary>
        /// Counts the gesture down, letting it go when it is done - or at once when
        /// <paramref name="moving"/> and it is a held one: nobody keeps their arms folded on the move.
        /// </summary>
        public void Tick(float deltaTime, bool moving = false)
        {
            if (Current != Gesture.None)
            {
                Left -= deltaTime;
                if (Left <= 0f || (moving && WorkerGestures.IsHeld(Current))) LetGo();
            }

            if (managedLayer < 0 || animator == null) return;

            // Up as the gesture starts; down only once it has had time to blend back out.
            float want = Current != Gesture.None ? 1f : 0f;
            layerWeight = Mathf.MoveTowards(layerWeight, want, deltaTime / LayerSeconds);
            animator.SetLayerWeight(managedLayer, layerWeight * layerWeight * (3f - 2f * layerWeight));
        }

        /// <summary>Gesture dropped and the managed layer down at once - for the pool.</summary>
        public void Reset()
        {
            LetGo();
            layerWeight = 0f;
            if (managedLayer >= 0 && animator != null) animator.SetLayerWeight(managedLayer, 0f);
        }
    }
}
