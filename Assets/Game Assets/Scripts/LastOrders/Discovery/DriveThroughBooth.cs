using System.Collections.Generic;
using DG.Tweening;
using FoodStreet.Visitors;
using LastOrders.Audio;
using LastOrders.Data;
using LastOrders.Economy;
using LastOrders.Interaction;
using LastOrders.UI.Popups;
using LastOrders.UI.World;
using UnityEngine;

namespace LastOrders.Discovery
{
    /// <summary>
    /// The abandoned drive-through booth by the road, put back to work: cars turn off the road
    /// into the lane in front of it, wait in line with their engines running, and pull away with
    /// their bag once it is handed out of the window.
    ///
    /// The rules are the takeaway service's. This drives the cars it sends - the visitors' own
    /// cars, on the route laid out by the setup tool - slides the hatch open while a car is at the
    /// window, and throws the bag across to it.
    /// </summary>
    [RequireComponent(typeof(DiscoverySite))]
    public sealed class DriveThroughBooth : MonoBehaviour, IDiscoveryFeature
    {
        [Header("The lane")]
        [Tooltip("Where cars come from: on the road, out in the fog.")]
        [SerializeField] Transform spawn;

        [Tooltip("Points from the road into the lane, after the spawn and before the back of the queue.")]
        [SerializeField] Transform[] turnIn;

        [Tooltip("Where each car waits, the window first.")]
        [SerializeField] Transform[] slots;

        [Tooltip("Points from the window back onto the road and away, the last one out in the fog.")]
        [SerializeField] Transform[] driveOff;

        [Tooltip("Cars the window's customers come in. Picked by each order's look.")]
        [SerializeField] VehicleDefinition[] cars;

        [Header("The booth")]
        [Tooltip("The sliding hatch in the window, moved along its local X to open.")]
        [SerializeField] Transform hatch;

        [SerializeField] float hatchOpen = -0.42f;

        [Tooltip("Where a handed-over bag leaves from.")]
        [SerializeField] Transform window;

        [Tooltip("The paper bag thrown to the car.")]
        [SerializeField] GameObject bagPrefab;

        [SerializeField] Sprite promptIcon;

        [SerializeField] Color servedColour = new Color(0.55f, 0.95f, 0.55f);
        [SerializeField] Color gaveUpColour = new Color(1f, 0.55f, 0.4f);

        readonly Dictionary<TakeawayOrder, VisitorVehicle> driving = new Dictionary<TakeawayOrder, VisitorVehicle>();
        readonly Dictionary<VisitorVehicle, VehicleDefinition> kindOf = new Dictionary<VisitorVehicle, VehicleDefinition>();
        readonly List<VisitorVehicle> pool = new List<VisitorVehicle>();

        DiscoverySite site;
        TakeawayService takeaway;
        Vector3 hatchClosed;
        bool hatchIsOpen;
        bool wired;

        public bool WantsPrompt => takeaway != null && takeaway.IsOpen;

        public Sprite PromptIcon => promptIcon;

        Vector3 Forward => slots != null && slots.Length > 0 && slots[0] != null ? slots[0].forward : Vector3.left;

        void Awake()
        {
            site = GetComponent<DiscoverySite>();
            if (hatch != null) hatchClosed = hatch.localPosition;
        }

        public void OnFound(bool announce)
        {
            if (wired || site.Composition == null) return;

            takeaway = site.Composition.Takeaway;
            takeaway.OnArriving += HandleArriving;
            takeaway.OnLeaving += HandleLeaving;
            takeaway.OnLaneMoved += MoveUp;
            takeaway.HasWindow = true;
            wired = true;
        }

        void OnDestroy()
        {
            if (!wired) return;

            takeaway.OnArriving -= HandleArriving;
            takeaway.OnLeaving -= HandleLeaving;
            takeaway.OnLaneMoved -= MoveUp;
            takeaway.HasWindow = false;
        }

        void Update()
        {
            if (!wired) return;

            TakeawayOrder front = takeaway.AtWindow;
            SetHatch(front != null && front.State == TakeawayCarState.Waiting);
        }

        public string OnSiteTapped()
        {
            if (takeaway == null) return "The drive-through is not connected to the game.";
            if (!takeaway.IsOpen) return "The drive-through opens with the dining room, in the daytime.";

            WorldUiCatalog catalog = site.Composition.WorldUi.Catalog;
            TakeawayPopupView prefab = catalog != null ? catalog.takeawayPopup : null;
            if (prefab == null) return $"{takeaway.Lane.Count} cars in the lane.";

            TakeawayPopupView popup = site.Composition.Popups.Show(prefab);
            if (popup == null) return null;

            popup.Bind(this, takeaway, HandOver);
            return null;
        }

        /// <summary>The popup's hand-over: the coins fly up from the window, and the bag across to the car.</summary>
        bool HandOver(TakeawayOrder order)
        {
            if (window != null) RewardSource.At(window.position + Vector3.up * 0.6f);

            bool done = takeaway.TryHandOver(order, out string message);
            TapFeedback.Report(message);
            return done;
        }

        // --- Cars ----------------------------------------------------------------------------------

        void HandleArriving(TakeawayOrder order)
        {
            if (spawn == null || slots == null || slots.Length == 0) return;

            VisitorVehicle car = TakeCar(order.Look);
            if (car == null) return;

            driving[order] = car;

            var path = new List<Vector3> { spawn.position };
            if (turnIn != null)
                foreach (Transform point in turnIn)
                    if (point != null) path.Add(point.position);
            path.Add(SlotPosition(takeaway.PlaceOf(order)));

            car.OnParked += _ => Parked(order, car);
            car.DriveAlong(path, Forward, 2);
        }

        /// <summary>
        /// Pulled up somewhere in the lane. The place it was sent to may have moved up while it was
        /// still out on the road; if so it creeps on to where it belongs before it counts as waiting.
        /// </summary>
        void Parked(TakeawayOrder order, VisitorVehicle car)
        {
            int place = takeaway.PlaceOf(order);
            if (place < 0) return;

            Vector3 target = SlotPosition(place);
            if ((car.transform.position - target).sqrMagnitude > 0.25f)
            {
                car.DriveAlong(new List<Vector3> { car.transform.position, target }, Forward, 1);
                return;
            }

            takeaway.MarkArrived(order);
        }

        /// <summary>A car left: everyone behind it creeps up a place.</summary>
        void MoveUp()
        {
            foreach (KeyValuePair<TakeawayOrder, VisitorVehicle> entry in driving)
            {
                TakeawayOrder order = entry.Key;
                VisitorVehicle car = entry.Value;
                int place = takeaway.PlaceOf(order);
                if (place < 0 || car == null) continue;

                Vector3 target = SlotPosition(place);
                if ((car.transform.position - target).sqrMagnitude < 0.04f) continue;

                // Waiting or still on its way in, it heads straight for the nearer place. A car still out
                // on the road keeps to its route and is re-aimed when it reaches the lane.
                if (order.State == TakeawayCarState.Arriving && !InLane(car.transform.position)) continue;

                car.DriveAlong(new List<Vector3> { car.transform.position, target }, Forward, 1);
            }
        }

        void HandleLeaving(TakeawayOrder order, bool served)
        {
            if (!driving.TryGetValue(order, out VisitorVehicle car) || car == null)
            {
                driving.Remove(order);
                return;
            }

            driving.Remove(order);

            Vector3 at = car.transform.position + Vector3.up * 2f;
            if (served)
            {
                ThrowBag(car.transform);
                site.Composition.WorldUi.ShowFloatingText(at, "Thanks!", servedColour);
            }
            else if (site.Composition.Machine.CurrentPhase == DayCycle.DayPhase.Day)
            {
                site.Composition.WorldUi.ShowFloatingText(at, "Gave up waiting", gaveUpColour);
            }

            var path = new List<Vector3>();

            // From the window it drives on down the lane; from further back it pulls straight out onto the road.
            bool atWindow = slots.Length > 0 && (car.transform.position - slots[0].position).sqrMagnitude < 4f;
            if (!atWindow && driveOff != null && driveOff.Length > 1)
            {
                Vector3 road = driveOff[driveOff.Length - 2].position;
                Vector3 here = car.transform.position;
                path.Add(new Vector3(here.x + Forward.x * 4f, 0f, here.z + (road.z - here.z) * 0.6f));
                path.Add(new Vector3(here.x + Forward.x * 9f, 0f, road.z));
                path.Add(driveOff[driveOff.Length - 1].position);
            }
            else if (driveOff != null)
            {
                foreach (Transform point in driveOff)
                    if (point != null) path.Add(point.position);
            }

            if (path.Count == 0) path.Add(car.transform.position + Forward * 40f);

            car.DriveAway(path, 0);
        }

        VisitorVehicle TakeCar(int look)
        {
            if (cars == null || cars.Length == 0) return null;

            VehicleDefinition kind = cars[(look & 0x7fffffff) % cars.Length];
            if (kind == null || kind.prefab == null) return null;

            VisitorVehicle car = null;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] == null || pool[i].gameObject.activeSelf || kindOf[pool[i]] != kind) continue;

                car = pool[i];
                break;
            }

            if (car == null)
            {
                GameObject made = Instantiate(kind.prefab, spawn.position, Quaternion.identity, transform);
                made.name = $"TakeawayCar_{kind.name}";
                car = made.GetComponent<VisitorVehicle>();
                if (car == null)
                {
                    Destroy(made);
                    return null;
                }

                car.Configure(kind);
                kindOf[car] = kind;
                pool.Add(car);
            }

            car.ResetForPool();
            car.gameObject.SetActive(true);
            car.OnDeparted += Park;

            var animation = car.GetComponent<VehicleAnimation>();
            if (animation != null) animation.DriverStaysIn = true;

            return car;
        }

        /// <summary>Off the map: back in the pool.</summary>
        void Park(VisitorVehicle car)
        {
            car.ResetForPool();
            car.gameObject.SetActive(false);
        }

        /// <summary>Along the lane in front of the booth, between its back place and the window.</summary>
        bool InLane(Vector3 at)
        {
            if (slots == null || slots.Length == 0 || slots[0] == null) return false;

            Transform back = slots[slots.Length - 1] != null ? slots[slots.Length - 1] : slots[0];
            Vector3 along = back.position - slots[0].position;
            float length = along.magnitude;
            if (length < 0.01f) return (at - slots[0].position).sqrMagnitude < 9f;

            float t = Vector3.Dot(at - slots[0].position, along / length);
            Vector3 nearest = slots[0].position + along / length * Mathf.Clamp(t, 0f, length);
            return t > -1f && t < length + 2f && (at - nearest).sqrMagnitude < 4f;
        }

        Vector3 SlotPosition(int place)
        {
            if (slots == null || slots.Length == 0) return transform.position;

            place = Mathf.Clamp(place, 0, slots.Length - 1);
            return slots[place] != null ? slots[place].position : transform.position;
        }

        // --- The window ----------------------------------------------------------------------------

        void SetHatch(bool open)
        {
            if (hatch == null || open == hatchIsOpen) return;

            hatchIsOpen = open;
            SoundController.Play(open ? SoundId.HatchOpen : SoundId.HatchClose, hatch.position);
            hatch.DOKill();
            hatch.DOLocalMove(open ? hatchClosed + Vector3.right * hatchOpen : hatchClosed, 0.35f).SetEase(open ? Ease.OutBack : Ease.InOutSine);
        }

        void ThrowBag(Transform car)
        {
            if (bagPrefab == null || window == null) return;

            GameObject bag = Instantiate(bagPrefab, window.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            Vector3 into = car.position + Vector3.up * 1.1f;

            Sequence s = DOTween.Sequence();
            s.Append(bag.transform.DOJump(into, 0.8f, 1, 0.45f).SetEase(Ease.Linear));
            s.Join(bag.transform.DORotate(new Vector3(0f, 360f, 0f), 0.45f, RotateMode.FastBeyond360));
            s.Append(bag.transform.DOScale(0.01f, 0.12f));
            s.OnComplete(() => Destroy(bag));
        }

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(Transform from, Transform[] into, Transform[] places, Transform[] away, VehicleDefinition[] customers,
                              Transform slidingHatch, Transform serveFrom, GameObject bag, Sprite icon)
        {
            spawn = from;
            turnIn = into;
            slots = places;
            driveOff = away;
            cars = customers;
            hatch = slidingHatch;
            window = serveFrom;
            bagPrefab = bag;
            promptIcon = icon;
        }
    }
}
