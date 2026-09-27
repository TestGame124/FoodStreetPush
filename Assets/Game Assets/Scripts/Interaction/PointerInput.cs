using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FoodStreet.Interaction
{
    /// <summary>
    /// The one place the game reads the pointer, whether that pointer is a mouse or a finger.
    ///
    /// Three separate copies of "is the pointer over the UI?" used to live in the project, two of
    /// which allocated a PointerEventData and a result list on every single call. Everything here
    /// is computed at most once per frame and cached, so any number of systems can ask any number
    /// of questions for the cost of one graphic raycast.
    ///
    /// Nothing has to drive this: the first property read in a frame refreshes the state. Unity's
    /// per-frame input is stable for the whole frame, so every reader gets the same answer no
    /// matter what script execution order it runs in.
    /// </summary>
    public static class PointerInput
    {
        // --- Gesture -------------------------------------------------------------

        static int stateFrame = -1;
        static Vector2 position;
        static bool down;
        static bool downLast;
        static bool pressed;
        static bool released;
        static int touchCount;

        static Vector2 pressPosition;
        static float pressTime;
        static bool pressOverUI;
        static bool gestureMultiTouch;
        static int consumedFrame = -1;

        // --- UI raycast cache ----------------------------------------------------

        static readonly List<RaycastResult> uiHits = new List<RaycastResult>(16);
        static EventSystem uiOwner;
        static PointerEventData uiPointer;
        static int uiFrame = -1;
        static Vector2 uiQueryPosition;
        static bool uiBlocked;

        /// <summary>The pointer position in screen pixels.</summary>
        public static Vector2 Position { get { Refresh(); return position; } }

        /// <summary>The primary pointer went down this frame.</summary>
        public static bool Pressed { get { Refresh(); return pressed; } }

        /// <summary>The primary pointer is down.</summary>
        public static bool Held { get { Refresh(); return down; } }

        /// <summary>The primary pointer came up this frame.</summary>
        public static bool Released { get { Refresh(); return released; } }

        /// <summary>Two or more fingers are on the screen right now.</summary>
        public static bool MultiTouch { get { Refresh(); return touchCount >= 2; } }

        /// <summary>
        /// Two or more fingers were down at some point during the current gesture. A pinch that
        /// ends with one finger already lifted still must not read as a tap.
        /// </summary>
        public static bool GestureUsedMultiTouch { get { Refresh(); return gestureMultiTouch; } }

        /// <summary>
        /// The gesture began on top of the UI. A press that starts on a button belongs to that
        /// button for its whole life, even if the finger slides off the button before lifting.
        /// </summary>
        public static bool PressStartedOverUI { get { Refresh(); return pressOverUI; } }

        /// <summary>Pixels travelled since the press. Meaningless while nothing is pressed.</summary>
        public static float DragPixels { get { Refresh(); return Vector2.Distance(position, pressPosition); } }

        /// <summary>Seconds since the press, unscaled so a paused game still measures a tap.</summary>
        public static float HoldSeconds
        {
            get
            {
                Refresh();
                return down || released ? Time.unscaledTime - pressTime : 0f;
            }
        }

        /// <summary>The pointer is over a graphic that takes raycasts.</summary>
        public static bool OverUI => RaycastUI(Position);

        /// <summary>Another system has already acted on this frame's pointer event.</summary>
        public static bool Consumed { get { Refresh(); return consumedFrame == Time.frameCount; } }

        /// <summary>
        /// Claim this frame's pointer event, so systems that run later in the frame leave it
        /// alone. Tapping a placed object to pick it up, for example, must not also read as the
        /// release that drops it again.
        /// </summary>
        public static void Consume()
        {
            Refresh();
            consumedFrame = Time.frameCount;
        }

        /// <summary>
        /// Statics outlive a skipped domain reload, and play mode must not inherit the previous
        /// session's half finished gesture or a destroyed EventSystem.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            stateFrame = -1;
            uiFrame = -1;
            consumedFrame = -1;
            down = downLast = pressed = released = false;
            gestureMultiTouch = pressOverUI = uiBlocked = false;
            touchCount = 0;
            uiOwner = null;
            uiPointer = null;
            uiHits.Clear();
        }

        static void Refresh()
        {
            int frame = Time.frameCount;
            if (frame == stateFrame) return;
            stateFrame = frame;

            downLast = down;
            ReadDevice();

            // Edge detected against our own previous sample rather than read from
            // GetMouseButtonDown: a frame in which nobody happens to read the pointer then costs
            // a frame of latency instead of dropping the press entirely.
            pressed = down && !downLast;
            released = !down && downLast;

            if (pressed)
            {
                pressPosition = position;
                pressTime = Time.unscaledTime;
                pressOverUI = RaycastUI(position);
                gestureMultiTouch = touchCount >= 2;
            }
            else if (down)
            {
                gestureMultiTouch |= touchCount >= 2;
            }
        }

        static void ReadDevice()
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                touchCount = Input.touchCount;
                position = touch.position;

                // Ended and Canceled still report a position, which is the point the finger lifted
                // from -- exactly what a tap wants to be measured against.
                down = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                return;
            }

            touchCount = 0;
            position = Input.mousePosition;
            down = Input.GetMouseButton(0);
        }

        static bool RaycastUI(Vector2 screenPosition)
        {
            int frame = Time.frameCount;
            if (frame == uiFrame && screenPosition == uiQueryPosition) return uiBlocked;
            uiFrame = frame;
            uiQueryPosition = screenPosition;

            EventSystem current = EventSystem.current;

            // No EventSystem means there is no UI to be over. This used to throw every frame in
            // any scene that did not happen to contain one.
            if (current == null) return uiBlocked = false;

            if (uiOwner != current || uiPointer == null)
            {
                uiOwner = current;
                uiPointer = new PointerEventData(current);
            }

            uiPointer.Reset();
            uiPointer.position = screenPosition;
            uiHits.Clear();
            current.RaycastAll(uiPointer, uiHits);

            uiBlocked = uiHits.Count > 0;

            // "Unmask" is the tutorial cut-out: the overlay covers the screen so the rest of the
            // UI cannot be touched, but the hole punched in it has to let the world through.
            for (int i = 0; i < uiHits.Count; i++)
            {
                if (uiHits[i].gameObject.CompareTag("Unmask"))
                {
                    uiBlocked = false;
                    break;
                }
            }

            return uiBlocked;
        }
    }
}
