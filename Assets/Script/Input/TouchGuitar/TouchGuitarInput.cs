using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Gameplay;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Player;
using YARG.Gameplay.Visuals;

namespace YARG.Input
{
    /// <summary>
    ///     Feeds <see cref="TouchGuitarDevice"/> from touches over the highway
    ///     of the player who bound it: the lanes at the frets press the frets
    ///     and strum on touch-down, the top of the screen above the highway
    ///     fires star power, and tilting the phone left or right works the
    ///     whammy. Runs only while a song is playing — every other screen is
    ///     driven by the touchscreen itself — and ignores touches that land on
    ///     UI such as the pause button.
    /// </summary>
    public class TouchGuitarInput : MonoBehaviour
    {
        // Fractions of the screen height: lanes accept touches this far above
        // the frets; star power lives above this line
        private const float LANE_ZONE_HEIGHT = 0.45f;
        private const float STAR_POWER_ZONE_BOTTOM = 0.72f;

        // Touches this far beyond the outer lanes, in lane widths, still count
        private const float LANE_REACH = 0.75f;

        private const int LAYOUT_REFRESH_FRAMES = 30;

        // Tilting the phone left or right works the whammy, measured against
        // however the player happens to be holding it
        private const float WHAMMY_DEADZONE_DEGREES = 6f;
        private const float WHAMMY_FULL_DEGREES = 26f;

        // Degrees per second the rest position drifts toward how the phone is
        // being held, while the tilt is small enough to be a hold rather than
        // a whammy
        private const float NEUTRAL_DRIFT_RATE = 4f;

        // Coarse enough that a resting hand does not queue an event a frame
        private const float WHAMMY_STEPS = 32f;

        private static TouchGuitarInput _instance;

        private TouchGuitarDevice _device;
        private GameManager _gameManager;
        private FiveFretGuitarPlayer _player;

        private float[] _laneCentersX = Array.Empty<float>();
        private float _laneWidth;
        private float _fretsY;
        private int _layoutFrame = -1;

        private byte _lastButtons;
        private byte _lastWhammy;

        private bool  _tiltReady;
        private float _neutralRoll;

        // Only exists while this device is the one being played
        private TouchStarPowerCue _cue;

        private readonly List<RaycastResult> _raycastResults = new();
        private PointerEventData _pointerData;

        public static void Start()
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject("Touch Controls")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<TouchGuitarInput>();
        }

        private void Awake()
        {
            _device = TouchGuitarDevice.Add();
        }

        private void Update()
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen == null || _device == null || !_device.added)
            {
                return;
            }

            if (!TryGetPlayingGuitarist(out var player))
            {
                Publish(0);

                // Leaving gameplay ends the session; a pause only idles it
                if (_gameManager == null)
                {
                    EndSession();
                }
                else
                {
                    HideCue();
                }

                return;
            }

            if (Time.frameCount - _layoutFrame >= LAYOUT_REFRESH_FRAMES)
            {
                RefreshLayout(player);
            }

            if (_laneCentersX.Length == 0)
            {
                Publish(0);
                HideCue();
                return;
            }

            EnableTiltSensor();
            ShowCue(player);

            byte buttons = 0;
            foreach (var touch in touchscreen.touches)
            {
                if (!touch.press.isPressed)
                {
                    continue;
                }

                var position = touch.position.ReadValue();
                if (IsOverControl(position))
                {
                    continue;
                }

                if (position.y >= STAR_POWER_ZONE_BOTTOM * Screen.height)
                {
                    if (IsOverHighway(position.x))
                    {
                        buttons |= TouchGuitarState.STAR_POWER;
                    }
                    continue;
                }

                if (position.y > _fretsY + LANE_ZONE_HEIGHT * Screen.height)
                {
                    continue;
                }

                int lane = LaneAt(position.x);
                if (lane < 0)
                {
                    continue;
                }

                buttons |= (byte) (1 << lane);
                if (touch.press.wasPressedThisFrame)
                {
                    buttons |= TouchGuitarState.STRUM;
                }
            }

            Publish(buttons, ReadWhammy());
        }

        /// <summary>
        ///     Turns how far the phone is tilted from its resting angle into a
        ///     whammy amount. Only the roll matters — the phone tipping like a
        ///     steering wheel — so the sign of the tilt is irrelevant and
        ///     either direction whammies.
        /// </summary>
        private byte ReadWhammy()
        {
            if (!TryReadRoll(out float roll))
            {
                return 0;
            }

            if (!_tiltReady)
            {
                _tiltReady = true;
                _neutralRoll = roll;
            }

            float offset = Mathf.Abs(roll - _neutralRoll);
            if (offset < WHAMMY_DEADZONE_DEGREES)
            {
                // Follow the way the phone is being held, so a player who
                // settles at a new angle still starts from zero
                _neutralRoll = Mathf.MoveTowards(_neutralRoll, roll, NEUTRAL_DRIFT_RATE * Time.deltaTime);
            }

            float amount = Mathf.InverseLerp(WHAMMY_DEADZONE_DEGREES, WHAMMY_FULL_DEGREES, offset);
            return (byte) Mathf.RoundToInt(Mathf.Round(amount * WHAMMY_STEPS) / WHAMMY_STEPS * byte.MaxValue);
        }

        /// <summary>
        ///     The angle the screen is rolled about its own centre, from
        ///     gravity. Sensors report in the device's portrait frame, so in
        ///     landscape the screen's left-right axis is the device's y.
        /// </summary>
        private static bool TryReadRoll(out float degrees)
        {
            degrees = 0f;

            Vector3 gravity;
            var gravitySensor = GravitySensor.current;
            if (gravitySensor != null && gravitySensor.enabled)
            {
                gravity = gravitySensor.gravity.ReadValue();
            }
            else if (Accelerometer.current != null && Accelerometer.current.enabled)
            {
                gravity = Accelerometer.current.acceleration.ReadValue();
            }
            else
            {
                return false;
            }

            // A phone in freefall, or a sensor with nothing to say yet
            if (gravity.sqrMagnitude < 0.04f)
            {
                return false;
            }

            gravity.Normalize();
            bool landscape = Screen.orientation is ScreenOrientation.LandscapeLeft
                or ScreenOrientation.LandscapeRight;
            float acrossScreen = landscape ? gravity.y : gravity.x;

            degrees = Mathf.Asin(Mathf.Clamp(acrossScreen, -1f, 1f)) * Mathf.Rad2Deg;
            return true;
        }

        // The sensors are off until something asks for them
        private static void EnableTiltSensor()
        {
            if (GravitySensor.current != null)
            {
                if (!GravitySensor.current.enabled)
                {
                    InputSystem.EnableDevice(GravitySensor.current);
                }

                return;
            }

            if (Accelerometer.current != null && !Accelerometer.current.enabled)
            {
                InputSystem.EnableDevice(Accelerometer.current);
            }
        }

        private static void DisableTiltSensor()
        {
            if (GravitySensor.current != null && GravitySensor.current.enabled)
            {
                InputSystem.DisableDevice(GravitySensor.current);
            }

            if (Accelerometer.current != null && Accelerometer.current.enabled)
            {
                InputSystem.DisableDevice(Accelerometer.current);
            }
        }

        // The star power zone is a bare stretch of screen, so mark its edge
        // while this device is the one playing
        private void ShowCue(FiveFretGuitarPlayer player)
        {
            float left = _laneCentersX[0] - _laneWidth;
            float right = _laneCentersX[^1] + _laneWidth;

            // The highway's first layout sample lands far off screen while the
            // intro camera is still moving. Lanes are forgiving about that,
            // since they only pick the nearest one, but a cue drawn from it
            // would stretch off both edges
            if (left < -Screen.width || right > Screen.width * 2f)
            {
                HideCue();
                return;
            }

            if (_cue == null)
            {
                _cue = TouchStarPowerCue.Create();
#if YARG_TEST_BUILD
                YARG.Core.Logging.YargLogger.LogInfo($"[TOUCH] star power cue spans x {left:0} to {right:0}, " +
                    $"above y {STAR_POWER_ZONE_BOTTOM * Screen.height:0} of {Screen.width}x{Screen.height}");
#endif
            }

            _cue.SetZone(left, right, STAR_POWER_ZONE_BOTTOM * Screen.height);

            // While star power is already running, tapping the zone does
            // nothing, so the cue has nothing to offer
            _cue.SetAvailable(player.BaseEngine.CanStarPowerActivate && !player.BaseStats.IsStarPowerActive);
        }

        private void HideCue()
        {
            if (_cue != null)
            {
                _cue.SetAvailable(false);
            }
        }

        private void EndSession()
        {
            if (_cue != null)
            {
                Destroy(_cue.gameObject);
                _cue = null;
            }

            DisableTiltSensor();

            // The next song starts from however the phone is held then
            _tiltReady = false;
        }

        private void OnDestroy()
        {
            EndSession();
        }

        private void Publish(byte buttons, byte whammy = 0)
        {
            if (buttons == _lastButtons && whammy == _lastWhammy)
            {
                return;
            }

            _lastButtons = buttons;
            _lastWhammy = whammy;
            InputSystem.QueueStateEvent(_device, new TouchGuitarState { buttons = buttons, whammy = whammy });
#if YARG_TEST_BUILD
            if (buttons != 0 || whammy != 0)
            {
                YARG.Core.Logging.YargLogger.LogInfo($"[TOUCH] buttons {System.Convert.ToString(buttons, 2).PadLeft(7, '0')} " +
                    $"whammy {whammy}");
            }
#endif
        }

        private bool TryGetPlayingGuitarist(out FiveFretGuitarPlayer player)
        {
            player = null;

            if (_gameManager == null)
            {
                _gameManager = FindAnyObjectByType<GameManager>();
                _player = null;
                if (_gameManager == null)
                {
                    return false;
                }
            }

            if (!_gameManager.IsSongStarted || _gameManager.Paused)
            {
                return false;
            }

            if (_player == null)
            {
                foreach (var candidate in FindObjectsByType<FiveFretGuitarPlayer>(FindObjectsSortMode.None))
                {
                    if (candidate.Player != null && candidate.Player.Bindings.ContainsDevice(_device))
                    {
                        _player = candidate;
                        _layoutFrame = -1;
                        break;
                    }
                }
            }

            player = _player;
            return player != null;
        }

        private void RefreshLayout(FiveFretGuitarPlayer player)
        {
            _layoutFrame = Time.frameCount;

            var fretArray = player.GetComponentInChildren<FretArray>();
            var frets = fretArray != null ? fretArray.GetComponentsInChildren<Fret>() : Array.Empty<Fret>();
            if (frets.Length < 2)
            {
                _laneCentersX = Array.Empty<float>();
                return;
            }

            // The highway matrices are GPU-space, so on top-origin APIs the
            // viewport y runs from the top
            bool flipY = SystemInfo.graphicsUVStartsAtTop;

            var centers = new float[frets.Length];
            float fretsY = 0f;
            for (int i = 0; i < frets.Length; i++)
            {
                var viewport = player.WorldToViewport(frets[i].transform.position);
                centers[i] = viewport.x * Screen.width;
                fretsY += (flipY ? 1f - viewport.y : viewport.y) * Screen.height;
            }

            Array.Sort(centers);
            float laneWidth = (centers[^1] - centers[0]) / (centers.Length - 1);
            if (!(laneWidth > 1f) || float.IsNaN(fretsY))
            {
                // The renderer has not laid the highway out yet
                _laneCentersX = Array.Empty<float>();
                return;
            }

            _laneCentersX = centers;
            _laneWidth = laneWidth;
            _fretsY = fretsY / frets.Length;
#if YARG_TEST_BUILD
            if (!_layoutLogged)
            {
                _layoutLogged = true;
                YARG.Core.Logging.YargLogger.LogInfo($"[TOUCH] lanes at x {string.Join(", ", Array.ConvertAll(centers, c => c.ToString("0")))} " +
                    $"(width {_laneWidth:0}), frets y {_fretsY:0} of {Screen.width}x{Screen.height}");
            }
#endif
        }

#if YARG_TEST_BUILD
        private bool _layoutLogged;
#endif

        private int LaneAt(float x)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _laneCentersX.Length; i++)
            {
                float distance = Mathf.Abs(x - _laneCentersX[i]);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return bestDistance <= _laneWidth * LANE_REACH ? best : -1;
        }

        private bool IsOverHighway(float x)
        {
            return x >= _laneCentersX[0] - _laneWidth && x <= _laneCentersX[^1] + _laneWidth;
        }

        // A touch on an actual control (the pause button, a dialog) belongs
        // to the UI; the full-screen highway and venue images beneath every
        // touch do not count
        private bool IsOverControl(Vector2 position)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            _pointerData ??= new PointerEventData(eventSystem);
            _pointerData.position = position;
            _raycastResults.Clear();
            eventSystem.RaycastAll(_pointerData, _raycastResults);

            foreach (var result in _raycastResults)
            {
                if (result.gameObject.GetComponentInParent<Selectable>() != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
