using UnityEngine;

namespace FoodStreet.Navigation
{
    /// <summary>
    /// Attaches the navigation components an agent needs, at runtime.
    ///
    /// The character prefabs used to carry a NavMeshAgent; they now carry nothing, and
    /// adding the replacement here rather than re-authoring every prefab keeps the two
    /// components' defaults in one readable place instead of spread across serialised YAML.
    /// Idempotent, so pooled agents can call it on every reuse.
    /// </summary>
    public static class NavAgentSetup
    {
        public static PathFollower Ensure(GameObject target)
        {
            if (!target.TryGetComponent(out SteeringAgent _))
            {
                target.AddComponent<SteeringAgent>();
            }

            if (!target.TryGetComponent(out PathFollower follower))
            {
                follower = target.AddComponent<PathFollower>();
            }

            return follower;
        }

        public static FlowFieldFollower EnsureFlowFollower(GameObject target, FlowFieldId field)
        {
            if (!target.TryGetComponent(out SteeringAgent _))
            {
                target.AddComponent<SteeringAgent>();
            }

            if (!target.TryGetComponent(out FlowFieldFollower follower))
            {
                follower = target.AddComponent<FlowFieldFollower>();
            }

            follower.Field = field;
            return follower;
        }
    }
}
