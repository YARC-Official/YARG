using System;
using System.Net;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using YARG.Core.Logging;
using YARG.Networking;
using YARG.Menu.Persistent;
using YARG.Menu.Navigation;

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

        private int _focusedFieldCount;
        private bool _navigationSuppressed;
        private bool _isConnecting;

        private void OnEnable()
        {
            SubscribeToNetworkEvents();
            SynchronizeButtonState();
        }

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
                if (string.IsNullOrWhiteSpace(ipAddressInput.text))
                {
                    ipAddressInput.text = "127.0.0.1"; // sensible default
                }

                ipAddressInput.onSelect.AddListener(OnIpFieldSelected);
                ipAddressInput.onDeselect.AddListener(_ => RestoreMenuNavigation());
                ipAddressInput.onSubmit.AddListener(_ => OnConnectClicked());
            }

            if (passwordInput != null)
            {
                passwordInput.onSelect.AddListener(_ => SuppressMenuNavigation());
                passwordInput.onDeselect.AddListener(_ => RestoreMenuNavigation());
                passwordInput.onSubmit.AddListener(_ => OnConnectClicked());
            }

            OnPasswordToggleChanged(hasPasswordToggle != null && hasPasswordToggle.isOn);
        }

        private void OnDisable()
        {
            UnsubscribeFromNetworkEvents();
            _isConnecting = false;
            SetConnectInteractable(true);
            ForceRestoreNavigation();
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
            if (_isConnecting)
            {
                return;
            }

            if (YargNetworkManager.Instance != null && YargNetworkManager.Instance.IsJoinInProgress)
            {
                YargLogger.LogWarning("[DirectConnectDialog] Join already in progress; ignoring duplicate connect click.");
                return;
            }

            string endpoint = ipAddressInput != null ? ipAddressInput.text : string.Empty;

            string password = "";
            if (hasPasswordToggle != null && hasPasswordToggle.isOn && passwordInput != null)
            {
                password = passwordInput.text;
            }

            if (!TryNormalizeEndpoint(endpoint, out var normalizedEndpoint, out var errorMessage))
            {
                if (!string.IsNullOrEmpty(errorMessage) && DialogManager.Instance != null)
                {
                    DialogManager.Instance.ShowMessage("Invalid Endpoint", errorMessage);
                }
                return;
            }

            ForceRestoreNavigation();

            _isConnecting = true;
            SetConnectInteractable(false);

            // Close this dialog first
            gameObject.SetActive(false);

            // Show connecting message only if no dialog is showing
            if (DialogManager.Instance != null && !DialogManager.Instance.IsDialogShowing)
            {
                DialogManager.Instance.ShowMessage(
                    "Connecting",
                    $"Attempting to connect to {normalizedEndpoint}...\nPlease wait."
                );
            }

            // Connect
            if (YargNetworkManager.Instance != null)
            {
                YargLogger.LogFormatInfo("[DirectConnectDialog] Attempting direct connect to {0}", normalizedEndpoint);
                YargNetworkManager.Instance.JoinLobby(normalizedEndpoint, password);
            }
        }

        public void OnCancelClicked()
        {
            _isConnecting = false;
            SetConnectInteractable(true);
            ForceRestoreNavigation();
            gameObject.SetActive(false);
        }

        private bool TryNormalizeEndpoint(string input, out string normalized, out string error)
        {
            normalized = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Please enter a valid host or IP address.";
                return false;
            }

            string endpoint = input.Trim();
            string host = endpoint;
            int port = GetDefaultPort();

            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            {
                host = uri.Host;
                if (uri.Port > 0)
                {
                    port = uri.Port;
                }
            }
            else if (!endpoint.Contains(" "))
            {
                if (endpoint.StartsWith("[", StringComparison.Ordinal))
                {
                    int closing = endpoint.IndexOf(']');
                    if (closing <= 0)
                    {
                        error = "IPv6 address is missing the closing ']'.";
                        return false;
                    }

                    host = endpoint.Substring(1, closing - 1);

                    if (closing + 1 < endpoint.Length)
                    {
                        if (endpoint[closing + 1] != ':' || !TryParsePort(endpoint[(closing + 2)..], out port, out error))
                        {
                            if (string.IsNullOrEmpty(error))
                            {
                                error = "Invalid port specified.";
                            }
                            return false;
                        }
                    }
                }
                else
                {
                    int colon = endpoint.LastIndexOf(':');
                    if (colon > -1 && endpoint.IndexOf(':') == colon)
                    {
                        host = endpoint[..colon];
                        if (!TryParsePort(endpoint[(colon + 1)..], out port, out error))
                        {
                            if (string.IsNullOrEmpty(error))
                            {
                                error = "Invalid port specified.";
                            }
                            return false;
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(host))
            {
                error = "Host cannot be empty.";
                return false;
            }

            host = host.Trim();

            bool isIpAddress = IPAddress.TryParse(host, out _);
            if (!isIpAddress && LooksLikeIpv4(host))
            {
                error = "IPv4 address appears to be malformed. Please double-check for extra digits or dots.";
                return false;
            }

            if (!isIpAddress && Uri.CheckHostName(host) == UriHostNameType.Unknown)
            {
                error = "Host must be a valid IP address or domain.";
                return false;
            }

            string formattedHost = host.Contains(':') && !host.StartsWith("[")
                ? $"[{host}]"
                : host;

            normalized = port > 0 ? $"{formattedHost}:{port}" : formattedHost;
            return true;
        }

        private void OnIpFieldSelected(string _)
        {
            SuppressMenuNavigation();
            if (ipAddressInput != null)
            {
                SelectAll(ipAddressInput);
            }
        }

        private static bool LooksLikeIpv4(string host)
        {
            if (string.IsNullOrEmpty(host))
            {
                return false;
            }

            bool hasDigit = false;
            foreach (char c in host)
            {
                if (c >= '0' && c <= '9')
                {
                    hasDigit = true;
                    continue;
                }

                if (c != '.')
                {
                    return false;
                }
            }

            return hasDigit;
        }

        private static void SelectAll(TMP_InputField field)
        {
            if (field == null)
            {
                return;
            }

            int length = field.text != null ? field.text.Length : 0;
            field.selectionAnchorPosition = 0;
            field.selectionFocusPosition = length;
            field.caretPosition = length;
        }

        private bool TryParsePort(string text, out int port, out string error)
        {
            error = string.Empty;
            if (!int.TryParse(text.Trim(), out port) || port <= 0 || port > 65535)
            {
                error = "Port must be between 1 and 65535.";
                return false;
            }

            return true;
        }

        private int GetDefaultPort()
        {
            if (YargNetworkManager.Instance != null)
            {
                return YargNetworkManager.Instance.SuggestedDirectConnectPort;
            }

            return NetworkTransportDefaults.DefaultUdpPort;
        }

        private void SuppressMenuNavigation()
        {
            _focusedFieldCount++;
            if (!_navigationSuppressed && _focusedFieldCount > 0)
            {
                Navigator.Instance?.PushScheme(NavigationScheme.Empty);
                _navigationSuppressed = true;
            }
        }

        private void RestoreMenuNavigation()
        {
            if (_focusedFieldCount > 0)
            {
                _focusedFieldCount--;
            }

            if (_navigationSuppressed && _focusedFieldCount <= 0)
            {
                Navigator.Instance?.PopScheme();
                _navigationSuppressed = false;
                _focusedFieldCount = 0;
            }
        }

        private void ForceRestoreNavigation()
        {
            _focusedFieldCount = 0;
            if (_navigationSuppressed)
            {
                Navigator.Instance?.PopScheme();
                _navigationSuppressed = false;
            }
        }

        private void SubscribeToNetworkEvents()
        {
            if (YargNetworkManager.Instance == null)
            {
                return;
            }

            YargNetworkManager.Instance.OnLobbyJoined += HandleLobbyJoined;
            YargNetworkManager.Instance.OnLobbyLeft += HandleLobbyLeft;
            YargNetworkManager.Instance.OnNetworkError += HandleNetworkError;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (YargNetworkManager.Instance == null)
            {
                return;
            }

            YargNetworkManager.Instance.OnLobbyJoined -= HandleLobbyJoined;
            YargNetworkManager.Instance.OnLobbyLeft -= HandleLobbyLeft;
            YargNetworkManager.Instance.OnNetworkError -= HandleNetworkError;
        }

        private void HandleLobbyJoined(YargNetworkManager.LobbyInfo _)
        {
            HandleConnectionComplete();
        }

        private void HandleLobbyLeft()
        {
            HandleConnectionComplete();
        }

        private void HandleNetworkError(string _)
        {
            HandleConnectionComplete();
        }

        private void HandleConnectionComplete()
        {
            _isConnecting = false;
            SetConnectInteractable(true);
        }

        private void SynchronizeButtonState()
        {
            bool joinActive = YargNetworkManager.Instance != null && YargNetworkManager.Instance.IsJoinInProgress;
            _isConnecting = joinActive;
            SetConnectInteractable(!joinActive);
        }

        private void SetConnectInteractable(bool interactable)
        {
            if (connectButton != null)
            {
                connectButton.interactable = interactable;
            }
        }
    }
}