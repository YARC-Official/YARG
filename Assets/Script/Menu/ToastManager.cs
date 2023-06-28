using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace YARG.UI
{
    public class ToastManager : MonoBehaviour
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

        private static Queue<Toast> toastQueue = new Queue<Toast>();
        private Coroutine queueChecker;

        public enum ToastType
        {
            General,
            Information,
            Success,
            Warning,
            Error,
        }

        public struct Toast
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

        public static ToastManager Instance { get; private set; }

        private void Start()
        {
            Instance = this;
            toastFab.SetActive(false);
            ToastInformation("Devices found: " + (Microphone.devices.Length + InputSystem.devices.Count));
            // Watch for added or removed devices
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        /// <summary>
        /// Adds a general message toast to the toast queue.
        /// </summary>
        /// <param name="text">Text of the toast.</param>
        public static void ToastMessage(string text)
        {
            toastQueue.Enqueue(new Toast(ToastType.General, text));
            if (Instance.queueChecker == null)
            {
                Instance.queueChecker = Instance.StartCoroutine(Instance.CheckQueue());
            }
        }

        /// <summary>
        /// Adds an information message toast to the toast queue.
        /// </summary>
        /// <param name="text">Text of the toast.</param>
        public static void ToastInformation(string text)
        {
            toastQueue.Enqueue(new Toast(ToastType.Information, text));
            if (Instance.queueChecker == null)
            {
                Instance.queueChecker = Instance.StartCoroutine(Instance.CheckQueue());
            }
        }

        /// <summary>
        /// Adds a success message toast to the toast queue.
        /// </summary>
        /// <param name="text">Text of the toast.</param>
        public static void ToastSuccess(string text)
        {
            toastQueue.Enqueue(new Toast(ToastType.Success, text));
            if (Instance.queueChecker == null)
            {
                Instance.queueChecker = Instance.StartCoroutine(Instance.CheckQueue());
            }
        }

        /// <summary>
        /// Adds a warning message toast to the toast queue.
        /// </summary>
        /// <param name="text">Text of the toast.</param>
        public static void ToastWarning(string text)
        {
            toastQueue.Enqueue(new Toast(ToastType.Warning, text));
            if (Instance.queueChecker == null)
            {
                Instance.queueChecker = Instance.StartCoroutine(Instance.CheckQueue());
            }
        }

        /// <summary>
        /// Adds an error message toast to the toast queue.
        /// </summary>
        /// <param name="text">Text of the toast.</param>
        public static void ToastError(string text)
        {
            toastQueue.Enqueue(new Toast(ToastType.Error, text));
            if (Instance.queueChecker == null)
            {
                Instance.queueChecker = Instance.StartCoroutine(Instance.CheckQueue());
            }
        }

        private void ShowToast(ToastType type, string body)
        {
            toastFab.SetActive(true);
            switch (type)
            {
                case ToastType.General:
                    messageTypeText.text = "General";
                    messageTypeText.color = generalColor;
                    messageTypeIcon.sprite = iconGeneral;
                    break;

                case ToastType.Success:
                    messageTypeText.text = "Success";
                    messageTypeText.color = successColor;
                    messageTypeIcon.sprite = iconSuccess;
                    break;

                case ToastType.Warning:
                    messageTypeText.text = "Warning";
                    messageTypeText.color = warningColor;
                    messageTypeIcon.sprite = iconWarning;
                    break;

                case ToastType.Information:
                    messageTypeText.text = "Information";
                    messageTypeText.color = informationColor;
                    messageTypeIcon.sprite = iconInformation;
                    break;

                case ToastType.Error:
                    messageTypeText.text = "Error";
                    messageTypeText.color = errorColor;
                    messageTypeIcon.sprite = iconError;
                    break;

                default:
                    messageTypeText.text = "Unknown";
                    messageTypeText.color = generalColor;
                    messageTypeIcon.sprite = iconGeneral;
                    break;
            }

            messageBody.color = new Color(0xFF, 0xFF, 0xFF, 0xFF); //set text color to white
            messageBody.text = body;
            messageBackground.color = messageTypeText.color;
            toastAnimator.Play("ToastAnimation");
        }

        private IEnumerator CheckQueue()
        {
            do
            {
                Toast thing = toastQueue.Dequeue();
                ShowToast(thing.type, thing.text);
                do
                {
                    yield return null;
                } while (!toastAnimator.GetCurrentAnimatorStateInfo(0).IsTag("Idle"));
            } while (toastQueue.Count > 0);

            toastFab.SetActive(false);
            queueChecker = null;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Added)
            {
                ToastMessage("Device added: " + device.displayName);
            }
            else if (change == InputDeviceChange.Removed)
            {
                ToastMessage("Device removed: " + device.displayName);
            }
        }
    }
}