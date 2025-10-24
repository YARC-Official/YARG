using UnityEngine;
using UnityEngine.UI;
using TMPro;
using YARG.Networking;
using YARG.Menu.Persistent;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// Dialog for directly connecting to a lobby by IP address.
    /// </summary>
    public class DirectConnectDialog : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TMP_InputField ipAddressInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Toggle hasPasswordToggle;
        [SerializeField] private GameObject passwordPanel;
        [SerializeField] private GameObject passwordLabelPanel; // Optional: separate panel for password label
        [SerializeField] private Button connectButton;
        [SerializeField] private Button cancelButton;

        private void Start()
        {
            if (connectButton != null)
            {
                connectButton.onClick.AddListener(OnConnectClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(OnCancelClicked);
            }

            if (hasPasswordToggle != null)
            {
                hasPasswordToggle.onValueChanged.AddListener(OnPasswordToggleChanged);
            }

            // Set default IP
            if (ipAddressInput != null)
            {
                ipAddressInput.text = "127.0.0.1"; // localhost for testing
            }

            OnPasswordToggleChanged(hasPasswordToggle != null && hasPasswordToggle.isOn);
        }

        private void OnPasswordToggleChanged(bool hasPassword)
        {
            if (passwordPanel != null)
            {
                passwordPanel.SetActive(hasPassword);
            }
            
            // Also show/hide password label panel if it exists
            if (passwordLabelPanel != null)
            {
                passwordLabelPanel.SetActive(hasPassword);
            }
        }

        public void OnConnectClicked()
        {
            string ipAddress = ipAddressInput != null ? ipAddressInput.text : "127.0.0.1";
            
            if (string.IsNullOrEmpty(ipAddress))
            {
                if (DialogManager.Instance != null)
                {
                    DialogManager.Instance.ShowMessage("Invalid IP", "Please enter a valid IP address.");
                }
                return;
            }

            // Validate IP format
            if (!System.Net.IPAddress.TryParse(ipAddress, out _))
            {
                if (DialogManager.Instance != null)
                {
                    DialogManager.Instance.ShowMessage("Invalid IP", "Please enter a valid IP address.");
                }
                return;
            }

            string password = "";
            if (hasPasswordToggle != null && hasPasswordToggle.isOn && passwordInput != null)
            {
                password = passwordInput.text;
            }

            // Close this dialog first
            gameObject.SetActive(false);
            
            // Show connecting message only if no dialog is showing
            if (DialogManager.Instance != null && !DialogManager.Instance.IsDialogShowing)
            {
                DialogManager.Instance.ShowMessage(
                    "Connecting", 
                    $"Attempting to connect to {ipAddress}...\nPlease wait."
                );
            }
            
            // Connect
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.JoinLobby(ipAddress, password);
            }
        }

        public void OnCancelClicked()
        {
            gameObject.SetActive(false);
        }
    }
}