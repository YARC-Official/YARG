using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Settings;

namespace YARG.PlayMode
{
    public class HideCursor : MonoBehaviour
    {
        private float _cursorHideTimer;

        private void Start()
        {
            Cursor.visible = false;
        }

        private void OnDestroy()
        {
            Cursor.visible = true;
        }

        private void Update()
        {
            float showCursorSetting = SettingsManager.Settings.ShowCursorTimer.Data;

            // Always show if paused, or if settings say so
            if (Play.Instance.Paused || Mathf.Approximately(showCursorSetting, 0f))
            {
                Cursor.visible = true;
                return;
            }

            // Show cursor until timer runs out
            if (_cursorHideTimer <= 0f)
            {
                Cursor.visible = false;
            }
            else
            {
                Cursor.visible = true;

                _cursorHideTimer -= Time.unscaledDeltaTime;
            }

            // If the cursor moves, then set the timer
            if (Mouse.current.delta.magnitude > 3f)
            {
                _cursorHideTimer = showCursorSetting;
            }
        }
    }
}