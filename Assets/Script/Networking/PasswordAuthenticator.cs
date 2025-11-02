using System;
using System.Collections;
using Mirror;
using UnityEngine;
using YARG.Menu.Persistent;

namespace YARG.Networking
{
    /// <summary>
    /// Minimal password authenticator for Mirror used to enforce lobby passwords on join.
    /// Clients send an AuthRequestMessage containing the password (may be empty).
    /// The server validates against the current lobby password (if present) and accepts/rejects.
    /// </summary>
    public class PasswordAuthenticator : NetworkAuthenticator
    {
        public struct AuthRequestMessage : NetworkMessage
        {
            public string password;
        }

        public struct AuthResponseMessage : NetworkMessage
        {
            public bool success;
            public string message;
        }

        private static bool _waitingForAuthResponse;
        private static string _pendingFailureMessage;
        private const string DefaultFailureMessage = "Failed to join lobby. Authentication was rejected.";

        public override void OnStartServer()
        {
            // Register handler for clients sending auth requests
            NetworkServer.RegisterHandler<AuthRequestMessage>(OnAuthRequestMessage, false);
        }

        public override void OnStartClient()
        {
            // Register handler for server responses
            NetworkClient.RegisterHandler<AuthResponseMessage>(OnAuthResponseMessage, false);
        }

        public override void OnServerAuthenticate(NetworkConnectionToClient conn)
        {
            if (conn is LocalConnectionToClient)
            {
                ServerAccept(conn);
                return;
            }

            // Otherwise, wait for the client to send an AuthRequestMessage
            // Handled in OnAuthRequestMessage
        }

        public override void OnClientAuthenticate()
        {
            // Host-mode clients use a local connection; accept immediately so we don't wait on a response.
            if (NetworkClient.connection is LocalConnectionToServer)
            {
                ClientAccept();
                _waitingForAuthResponse = false;
                _pendingFailureMessage = null;
                return;
            }

            // When the client connects, send the password (may be empty)
            try
            {
                _waitingForAuthResponse = true;
                _pendingFailureMessage = DefaultFailureMessage;
                var manager = YargNetworkManager.Instance;
                string pw = manager != null ? manager.GetPendingJoinPassword() : string.Empty;
                var msg = new AuthRequestMessage { password = pw ?? string.Empty };
                NetworkClient.Send(msg);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PasswordAuthenticator] Failed to send auth request: {ex}");
                // If we can't send, reject locally
                _waitingForAuthResponse = false;
                _pendingFailureMessage = null;
                ClientReject();
            }
        }

        private void OnAuthRequestMessage(NetworkConnectionToClient conn, AuthRequestMessage msg)
        {
            var manager = YargNetworkManager.Instance;
            string expected = string.Empty;

            if (manager != null)
            {
                // Prefer the authoritative lobby password on the server-side if available
                var lobby = manager.CurrentLobby;
                if (lobby != null && lobby.hasPassword)
                {
                    expected = lobby.password ?? string.Empty;
                }
                else
                {
                    // Fall back to the inspector/runtime lobby password if available.
                    expected = manager.GetServerLobbyPassword() ?? string.Empty;
                }
            }

            // Safe debug logging (do not log actual password values). Log whether client provided a password and whether server expects one.
            bool clientProvidedPassword = !string.IsNullOrEmpty(msg.password);
            bool serverRequiresPassword = !string.IsNullOrEmpty(expected);
            Debug.Log($"[PasswordAuthenticator] Auth request from {conn.address}: clientProvidedPassword={clientProvidedPassword}, serverRequiresPassword={serverRequiresPassword}");

            bool allowed = string.IsNullOrEmpty(expected) || string.Equals(expected, msg.password ?? string.Empty, StringComparison.Ordinal);

            if (allowed)
            {
                // Accept the connection and inform the client
                conn.Send(new AuthResponseMessage { success = true, message = "OK" });
                ServerAccept(conn);
            }
            else
            {
                // Reject and disconnect the client after giving the transport a frame to flush the response.
                RejectWithMessage(conn, "Incorrect lobby password.");
            }
        }

        private void OnAuthResponseMessage(AuthResponseMessage msg)
        {
            if (msg.success)
            {
                ClientAccept();
                _waitingForAuthResponse = false;
                _pendingFailureMessage = null;
            }
            else
            {
                Debug.LogWarning($"[PasswordAuthenticator] Server rejected authentication: {msg.message}");
                string display = string.IsNullOrWhiteSpace(msg.message) ? "Authentication failed." : msg.message;
                ToastManager.ToastError(display);
                _waitingForAuthResponse = false;
                _pendingFailureMessage = null;
                ClientReject();
            }
        }

        private void RejectWithMessage(NetworkConnectionToClient conn, string message)
        {
            conn.Send(new AuthResponseMessage { success = false, message = message });
            StartCoroutine(DisconnectAfterFrame(conn));
        }

        private IEnumerator DisconnectAfterFrame(NetworkConnectionToClient conn)
        {
            // Allow the message to flush before forcefully disconnecting.
            yield return null;
            if (conn != null)
            {
                try
                {
                    ServerReject(conn);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PasswordAuthenticator] Exception while disconnecting rejected client: {ex.Message}");
                }
            }
        }

        internal static void HandleClientDisconnectFallback()
        {
            if (_waitingForAuthResponse && !string.IsNullOrEmpty(_pendingFailureMessage))
            {
                ToastManager.ToastError(_pendingFailureMessage);
            }

            _waitingForAuthResponse = false;
            _pendingFailureMessage = null;
        }
    }
}
