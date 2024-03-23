using System;
using System.Collections.Generic;
using UnityEngine;

namespace YARG.Menu.Persistent
{
    public class ToastManager : MonoSingleton<ToastManager>
    {
        private const int MAX_TOAST_COUNT = 5;

        [SerializeField]
        private Toast _toastPrefab;

        [Header("Colors")]
        [SerializeField]
        private Color _generalColor;
        [SerializeField]
        private Color _successColor;
        [SerializeField]
        private Color _warningColor;
        [SerializeField]
        private Color _informationColor;
        [SerializeField]
        private Color _errorColor;

        [Space]
        [Header("Icons")]
        [SerializeField]
        private Sprite _iconGeneral;
        [SerializeField]
        private Sprite _iconSuccess;
        [SerializeField]
        private Sprite _iconWarning;
        [SerializeField]
        private Sprite _iconInformation;
        [SerializeField]
        private Sprite _iconError;

        private static readonly Queue<ToastInfo> _toastQueue = new();

        private enum ToastType
        {
            General,
            Information,
            Success,
            Warning,
            Error,
        }

        private readonly struct ToastInfo
        {
            public readonly ToastType Type;
            public readonly string Text;

            public ToastInfo(ToastType type, string text)
            {
                Type = type;
                Text = text;
            }
        }

        private void Update()
        {
            if (LoadingScreen.IsActive)
            {
                return;
            }

            while (transform.childCount < MAX_TOAST_COUNT && _toastQueue.TryDequeue(out var toast))
            {
                ShowToast(toast.Type, toast.Text);
            }
        }

        /// <summary>
        /// Adds a general message toast to the toast queue.
        /// </summary>
        /// <param name="text">Text of the toast.</param>
        public static void ToastMessage(string text)
            => AddToast(ToastType.General, text);

        /// <summary>
        /// Adds an information message toast to the toast queue.
        /// </summary>
        /// <param name="text">Text of the toast.</param>
        public static void ToastInformation(string text)
            => AddToast(ToastType.Information, text);

        /// <summary>
        /// Adds a success message toast to the toast queue.
        /// </summary>
        /// <param name="text">Text of the toast.</param>
        public static void ToastSuccess(string text)
            => AddToast(ToastType.Success, text);

        /// <summary>
        /// Adds a warning message toast to the toast queue.
        /// </summary>
        /// <param name="text">Text of the toast.</param>
        public static void ToastWarning(string text)
            => AddToast(ToastType.Warning, text);

        /// <summary>
        /// Adds an error message toast to the toast queue.
        /// </summary>
        /// <param name="text">Text of the toast.</param>
        public static void ToastError(string text)
            => AddToast(ToastType.Error, text);

        private static void AddToast(ToastType type, string text)
        {
            _toastQueue.Enqueue(new ToastInfo(type, text));
        }

        private void ShowToast(ToastType type, string body)
        {
            // Get properties for this message type
            var (text, color, icon) = type switch
            {
                ToastType.General     => ("General",     _generalColor,     _iconGeneral),
                ToastType.Information => ("Information", _informationColor, _iconInformation),
                ToastType.Success     => ("Success",     _successColor,     _iconSuccess),
                ToastType.Warning     => ("Warning",     _warningColor,     _iconWarning),
                ToastType.Error       => ("Error",       _errorColor,       _iconError),
                _ => throw new ArgumentException($"Invalid toast type {type}!")
            };

            var toast = Instantiate(_toastPrefab, transform);
            toast.Initialize(text, body, icon, color);
        }
    }
}