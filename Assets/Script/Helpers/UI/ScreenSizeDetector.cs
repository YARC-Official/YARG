using UnityEngine;

namespace YARG.Helpers.UI
{
    public class ScreenSizeDetector : MonoSingleton<ScreenSizeDetector>
    {
        public static bool HasScreenSizeChanged { get; private set; }

        private int _lastWidth;
        private int _lastHeight;

        protected override void SingletonAwake()
        {
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            HasScreenSizeChanged = false;
        }

        private void Update()
        {
            CheckScreenSize();
        }

        private void CheckScreenSize()
        {
            if (Screen.width != _lastWidth || Screen.height != _lastHeight)
            {
                HasScreenSizeChanged = true;
            }
            else
            {
                HasScreenSizeChanged = false;
            }

            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
        }
    }
}