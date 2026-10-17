using UnityEngine;

namespace FoodStreet.Visitors
{
    /// <summary>
    /// What a seated visitor wants, as the visitor drives it: shown when they sit down, kept honest
    /// about whether it can be served, pulsed when a tap could not fill it, and gone when it is.
    ///
    /// A base rather than one class, so the dish bubble and the original sprite bubble can sit in
    /// the same prefab and either one be the visitor's - which is how the original is kept.
    /// </summary>
    public abstract class OrderBubbleBase : MonoBehaviour
    {
        public abstract bool IsShowing { get; }

        public abstract void Show(Sprite item, int count, bool affordable);

        /// <summary>As <see cref="Show(Sprite,int,bool)"/>, with what serving it pays.</summary>
        public virtual void Show(Sprite item, int count, bool affordable, int coins) => Show(item, count, affordable);

        public abstract void SetAffordable(bool affordable);

        /// <summary>A waiter has the order and is bringing it.</summary>
        public virtual void SetWaiterComing(bool coming) { }

        public abstract void Hide();

        /// <summary>Served: on its way out. Plain bubbles simply go.</summary>
        public virtual void Served() => Hide();

        /// <summary>A tap that could not be filled.</summary>
        public abstract void FlashShort();
    }
}
