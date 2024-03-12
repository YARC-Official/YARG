using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Menu.Persistent
{
    public class ToastManager : MonoSingleton<ToastManager>
    {
        /* Currently handles:
        Devices at startup
        Devices connected - not keyboard or mouse or microphone
        Device disconnected - not keyboard or mouse or microphone

        Colors:
        General: #00DDFB
        Success: #46E74F
        Information: #00AFF7
        Warning: #FFEE57
        Error: #FF0031
        Message Color: #FFFFFF
        */
        [Header("Colors")]
        [SerializeField]
        private Color generalColor;
        [SerializeField]
        private Color successColor;
        [SerializeField]
        private Color warningColor;
        [SerializeField]
        private Color informationColor;
        [SerializeField]
        private Color errorColor;
        [SerializeField]
        private Color messageColor;

        [Space]
        [Header("Icons")]
        [SerializeField]
        private Sprite iconGeneral;
        [SerializeField]
        private Sprite iconSuccess;
        [SerializeField]
        private Sprite iconWarning;
        [SerializeField]
        private Sprite iconInformation;
        [SerializeField]
        private Sprite iconError;

        [Space]
        [Header("UI Elements")]
        [SerializeField]
        private TMP_Text messageTypeText;
        [SerializeField]
        private Image messageTypeIcon;
        [SerializeField]
        private TMP_Text messageBody;
        [SerializeField]
        private GameObject toastFab;
        [SerializeField]
        private Animator toastAnimator;
        [SerializeField]
        private Image messageBackground;

        private int animationState = Animator.StringToHash("ToastAnimation");

        private static Queue<Toast> toastQueue = new();

        private enum ToastType
        {
            General,
            Information,
            Success,
            Warning,
            Error,
        }

        private struct Toast
        {
            //this is so we can pass 2 things into the queue
            public ToastType type;
            public string text;

            public Toast(ToastType type, string text)
            {
                this.type = type;
                this.text = text;
            }
        }

        private void Start()
        {
            toastFab.SetActive(false);
        }

        private void Update()
        {
            // Wait until loading is finished, to prevent toasts from not being seen due to startup/loading lag
            if (LoadingScreen.IsLoading)
                return;

            // Wait for animator to finish before doing anything else
            // Checking the queue before the animator state will always end up force-disabling the toast fab
            var animatorState = toastAnimator.GetCurrentAnimatorStateInfo(0);
            if (toastFab.activeSelf && !animatorState.IsTag("Idle"))
                return;

            if (toastQueue.Count < 1)
            {
                toastFab.SetActive(false);
                return;
            }

            var toast = toastQueue.Dequeue();
            ShowToast(toast.type, toast.text);
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
            toastQueue.Enqueue(new Toast(type, text));
        }

        private void ShowToast(ToastType type, string body)
        {
            // Get properties for this message type
            var (text, color, sprite) = type switch
            {
                ToastType.General     => ("General",     generalColor,     iconGeneral),
                ToastType.Information => ("Information", informationColor, iconInformation),
                ToastType.Success     => ("Success",     successColor,     iconSuccess),
                ToastType.Warning     => ("Warning",     warningColor,     iconWarning),
                ToastType.Error       => ("Error",       errorColor,       iconError),

                _ => throw new ArgumentException($"Invalid toast type {type}!")
            };

            // Background
            messageBackground.color = color;

            // Header
            messageTypeText.text = text;
            messageTypeText.color = color;
            messageTypeIcon.sprite = sprite;

            // Message
            messageBody.color = Color.white;
            messageBody.text = body;

            // Show toast
            toastFab.SetActive(true);
            toastAnimator.Play(animationState);
        }
    }
}