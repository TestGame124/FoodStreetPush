using LastOrders.Data;
using LastOrders.Interaction;
using UnityEngine;

namespace LastOrders.Restaurant
{
    /// <summary>
    /// One lit strip of ground the restaurant could grow into, as something to tap. The strip
    /// knows only which side it is; the shell decides whether that side can have it.
    /// </summary>
    public sealed class RestaurantExpansionHighlight : MonoBehaviour, ITappable, IInteractableObject
    {
        RestaurantShell shell;
        RestaurantSide side;

        public RestaurantSide Side => side;

        public string DisplayName => $"Expand {RestaurantService.Describe(side)}";

        public void Bind(RestaurantShell owner, RestaurantSide which)
        {
            shell = owner;
            side = which;
        }

        public bool OnTouch() => TapFeedback.Report(OnTapped());

        public string OnTapped() => shell != null ? shell.ChooseSide(side) : null;
    }
}
