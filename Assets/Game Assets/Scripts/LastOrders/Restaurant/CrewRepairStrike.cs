using LastOrders.Audio;
using LastOrders.Jobs;
using UnityEngine;

namespace LastOrders.Restaurant
{
    /// <summary>Where a crew member stands to patch the front wall: each blow of their tool lands here.</summary>
    public sealed class CrewRepairStrike : MonoBehaviour, IWorkStrikeReceiver
    {
        EncounterWorldView view;

        public void Bind(EncounterWorldView owner) => view = owner;

        public void OnStrike()
        {
            if (view != null) view.OnCrewStrike(transform);
            SoundController.Play(SoundId.WorkHammer, transform.position);
        }
    }
}
