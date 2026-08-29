using UnityEngine;
#if ENABLE_INPUT_SYSTEM && VLC_HAS_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if !ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
#warning VLCInput: neither ENABLE_INPUT_SYSTEM nor ENABLE_LEGACY_INPUT_MANAGER is defined. Demo input will be inert.
#endif

#if ENABLE_INPUT_SYSTEM && !VLC_HAS_INPUT_SYSTEM
#warning VLCInput: ENABLE_INPUT_SYSTEM is defined but the com.unity.inputsystem package is not installed. Install it via Package Manager, or set Active Input Handling to 'Input Manager (Old)'.
#endif

namespace LibVLCSharp
{
    /// <summary>
    /// A centralized wrapper to handle both Unity's New Input System and the Legacy Input Manager.
    /// </summary>
    public static class VLCInput
    {
#if ENABLE_INPUT_SYSTEM && !VLC_HAS_INPUT_SYSTEM
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void WarnInputSystemPackageMissing()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            const string fallback = "Falling back to the legacy Input Manager.";
#else
            const string fallback = "VLCInput will be inert until the package is installed or Active Input Handling is changed.";
#endif
            Debug.LogWarning(
                "[VLCUnity] Active Input Handling includes 'Input System Package (New)', but the " +
                "com.unity.inputsystem package is not installed. Install it via Package Manager, or " +
                "set Active Input Handling to 'Input Manager (Old)'. " + fallback);
        }
#endif

        public static bool FastMode()
        {
#if ENABLE_INPUT_SYSTEM && VLC_HAS_INPUT_SYSTEM
            return Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
            return false;
#endif
        }

        public static bool MoveLeft()
        {
#if ENABLE_INPUT_SYSTEM && VLC_HAS_INPUT_SYSTEM
            return Keyboard.current != null && (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
#else
            return false;
#endif
        }

        public static bool MoveRight()
        {
#if ENABLE_INPUT_SYSTEM && VLC_HAS_INPUT_SYSTEM
            return Keyboard.current != null && (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
#else
            return false;
#endif
        }

        public static bool MoveForward()
        {
#if ENABLE_INPUT_SYSTEM && VLC_HAS_INPUT_SYSTEM
            return Keyboard.current != null && (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
#else
            return false;
#endif
        }

        public static bool MoveBack()
        {
#if ENABLE_INPUT_SYSTEM && VLC_HAS_INPUT_SYSTEM
            return Keyboard.current != null && (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
#else
            return false;
#endif
        }

        public static bool MoveLocalUp()
        {
#if ENABLE_INPUT_SYSTEM && VLC_HAS_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.qKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.Q);
#else
            return false;
#endif
        }

        public static bool MoveLocalDown()
        {
#if ENABLE_INPUT_SYSTEM && VLC_HAS_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.E);
#else
            return false;
#endif
        }

        public static bool MoveWorldUp()
        {
#if ENABLE_INPUT_SYSTEM && VLC_HAS_INPUT_SYSTEM
            return Keyboard.current != null && (Keyboard.current.rKey.isPressed || Keyboard.current.pageUpKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.PageUp);
#else
            return false;
#endif
        }

        public static bool MoveWorldDown()
        {
#if ENABLE_INPUT_SYSTEM && VLC_HAS_INPUT_SYSTEM
            return Keyboard.current != null && (Keyboard.current.fKey.isPressed || Keyboard.current.pageDownKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.F) || Input.GetKey(KeyCode.PageDown);
#else
            return false;
#endif
        }

        public static Vector2 MouseDelta()
        {
#if ENABLE_INPUT_SYSTEM && VLC_HAS_INPUT_SYSTEM
            if (Mouse.current == null) return Vector2.zero;
            // Scale raw pixel delta down to roughly match legacy Input.GetAxis("Mouse X/Y") magnitudes.
            return new Vector2(Mouse.current.delta.x.ReadValue() * 0.1f, Mouse.current.delta.y.ReadValue() * 0.1f);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#else
            return Vector2.zero;
#endif
        }

        public static float ScrollDelta()
        {
#if ENABLE_INPUT_SYSTEM && VLC_HAS_INPUT_SYSTEM
            // Scale raw scroll (typically ~120/notch on Windows) toward legacy GetAxis("Mouse ScrollWheel") range (~0.1/notch).
            return Mouse.current != null ? Mouse.current.scroll.y.ReadValue() * 0.01f : 0f;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetAxis("Mouse ScrollWheel");
#else
            return 0f;
#endif
        }

        public static bool LookButtonDown()
        {
#if ENABLE_INPUT_SYSTEM && VLC_HAS_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Mouse1);
#else
            return false;
#endif
        }

        public static bool LookButtonUp()
        {
#if ENABLE_INPUT_SYSTEM && VLC_HAS_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.rightButton.wasReleasedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyUp(KeyCode.Mouse1);
#else
            return false;
#endif
        }
    }
}
