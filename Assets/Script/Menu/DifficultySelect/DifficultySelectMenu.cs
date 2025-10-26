using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Extensions;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Core.Song;
using YARG.Core.Utility;
using YARG.Helpers.Extensions;
using YARG.Localization;
using YARG.Menu.Data;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Player;
using YARG.Song;

namespace YARG.Menu.DifficultySelect
{
    public class DifficultySelectMenu : MonoBehaviour
    {
        /// <summary>
        /// The saved song speed value
        /// </summary>
        private static float _songSpeed = 1f;

        private enum State
        {
            Main,
            Instrument,
            Difficulty,
            Modifiers,
            Harmony
        }

        [SerializeField]
        private TextMeshProUGUI _subHeader;
        [SerializeField]
        private Transform _container;
        [SerializeField]
        private NavigationGroup _navGroup;
        [SerializeField]
        private TextMeshProUGUI _text;
        [SerializeField]
        private TMP_InputField _speedInput;
        [SerializeField]
        private TextMeshProUGUI _loadingPhrase;
        [SerializeField]
        private TextMeshProUGUI _warningMessage;
        [SerializeField]
        private GameObject _warningMessageContainer;
        [SerializeField]
        private TextMeshProUGUI _waitingForPlayersText;
        [SerializeField]
        private TextMeshProUGUI _readyStatusText;
        
        [Header("Multiplayer Player Status")]
        [SerializeField]
        private GameObject _multiplayerPlayerListContainer;
        [SerializeField]
        private Transform _multiplayerPlayerListContent;
        [SerializeField]
        private GameObject _multiplayerPlayerEntryPrefab;
        
        [Header("Song Queue")]
        [SerializeField]
        private GameObject _songQueueContainer;
        [SerializeField]
        private Transform _songQueueContent;
        [SerializeField]
        private GameObject _songQueueEntryPrefab;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _songTitleText;
        [SerializeField]
        private TextMeshProUGUI _artistText;
        [SerializeField]
        private Image _sourceIcon;

        [Space]
        [SerializeField]
        private DifficultyItem _difficultyItemPrefab;
        [SerializeField]
        private DifficultyItem _difficultyGreenPrefab;
        [SerializeField]
        private DifficultyItem _difficultyRedPrefab;
        [SerializeField]
        private ModifierItem _modifierItemPrefab;

        private int _playerIndex;
        private int _vocalModifierSelectIndex = -1;
        private readonly HashSet<int> _readyPlayerIndices = new();

        private State _lastMenuState;
        private State _menuState;

        private readonly List<Instrument> _possibleInstruments  = new();
        private readonly List<Difficulty> _possibleDifficulties = new();
        private readonly List<Modifier>   _possibleModifiers    = new();

        [NonSerialized]
        private Modifier _excusableModifiers;

        private int _maxHarmonyIndex = 3;

        private readonly List<ModifierItem> _modifierItems = new();
        private readonly Dictionary<Networking.NetworkPlayerData, MultiplayerPlayerEntry> _playerEntries = new();
        private readonly List<SongQueueEntry> _songQueueEntries = new();

        private List<SongEntry> _songList;

        private YargPlayer CurrentPlayer => PlayerContainer.Players[_playerIndex];
        
        private Multiplayer.MultiplayerDifficultySync _multiplayerSync;

        private void OnEnable()
        {
            // Get or create multiplayer sync component
            _multiplayerSync = GetComponent<Multiplayer.MultiplayerDifficultySync>();
            if (_multiplayerSync == null)
            {
                _multiplayerSync = gameObject.AddComponent<Multiplayer.MultiplayerDifficultySync>();
            }
            
            // Subscribe to waiting event
            _multiplayerSync.OnWaitingForPlayers += ShowWaitingForPlayersMessage;
            
            // Hide waiting message initially
            if (_waitingForPlayersText != null)
            {
                _waitingForPlayersText.gameObject.SetActive(false);
            }
            
            // Update ready status initially
            UpdateReadyStatus();
            
            // Subscribe to network player ready events
            SubscribeToNetworkPlayerEvents();
            
            // Subscribe to player left event to update player list
            if (Networking.YargNetworkManager.Instance != null)
            {
                Networking.YargNetworkManager.Instance.OnPlayerLeft += OnPlayerLeftLobby;
            }
            
            // Update multiplayer player list with delay to allow NetworkPlayerData objects to spawn
            if (Networking.YargNetworkManager.Instance != null && Networking.YargNetworkManager.Instance.isNetworkActive)
            {
                StartCoroutine(DelayedUpdateMultiplayerPlayerList());
            }
            
            // Update song queue if playing a show
            UpdateSongQueue();
            
            // Start coroutine to update ready status periodically
            StartCoroutine(UpdateReadyStatusPeriodically());
            
            string subHeaderKey = GlobalVariables.State.IsPractice ? "Practice" : "Quickplay";
            _subHeader.text = Localize.Key("Menu.Main.Options", subHeaderKey);

            // Set navigation scheme
            Navigator.Instance.PushScheme(CreateNavigationScheme());

            _speedInput.text = $"{Mathf.RoundToInt(_songSpeed * 100f)}%";
            _songTitleText.text = GlobalVariables.State.CurrentSong.Name;
            _artistText.text = GlobalVariables.State.CurrentSong.Artist;

            if (GlobalVariables.State.PlayingAShow)
            {
                _songList = GlobalVariables.State.ShowSongs;
            }
            else
            {
                _songList = new List<SongEntry> { GlobalVariables.State.CurrentSong };
            }

            // ChangePlayer(0) will update for the current player
            _playerIndex = 0;
            _vocalModifierSelectIndex = -1;
            
            // Sync initial player profile to multiplayer
            if (_multiplayerSync != null && PlayerContainer.Players.Count > 0)
            {
                _multiplayerSync.SyncPlayerProfileOnEntry(PlayerContainer.Players[0]);
            }
            
            ChangePlayer(0);

            _loadingPhrase.text = RichTextUtils.StripRichTextTags(
                GlobalVariables.State.CurrentSong.LoadingPhrase, RichTextTags.BadTags);

            _sourceIcon.sprite = SongSources.SourceToIcon(GlobalVariables.State.CurrentSong.Source);
            _sourceIcon.gameObject.SetActive(_sourceIcon.sprite != null);
        }

        private NavigationScheme CreateNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Up", context =>
                {
                    if (!IsNavigationContextAllowed(context))
                    {
                        return;
                    }

                    _navGroup.SelectPrevious(context.IsRepeat);
                }),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Down", context =>
                {
                    if (!IsNavigationContextAllowed(context))
                    {
                        return;
                    }

                    _navGroup.SelectNext(context.IsRepeat);
                }),
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm", context =>
                {
                    if (!IsNavigationContextAllowed(context))
                    {
                        return;
                    }

                    _navGroup.ConfirmSelection();
                }),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", context =>
                {
                    if (!IsNavigationContextAllowed(context))
                    {
                        return;
                    }

                    HandleBackAction();
                })
            }, false);
        }

        private bool IsNavigationContextAllowed(NavigationContext context)
        {
            if (_playerIndex < 0 || _playerIndex >= PlayerContainer.Players.Count)
            {
                return context.Player == null;
            }

            if (context.Player is null)
            {
                return true;
            }

            if (context.Player == CurrentPlayer)
            {
                return true;
            }

            int targetIndex = -1;
            for (int i = 0; i < PlayerContainer.Players.Count; i++)
            {
                if (PlayerContainer.Players[i] == context.Player)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex < 0)
            {
                Debug.LogWarning($"[DifficultySelectMenu] Received menu input for untracked player '{context.Player?.Profile?.Name}'");
                return false;
            }

            Debug.Log($"[DifficultySelectMenu] Switching active player from index {_playerIndex} to {targetIndex} for action {context.Action} by '{context.Player.Profile.Name}'");
            SetActivePlayer(targetIndex);

            return context.Player == CurrentPlayer;
        }

        private void HandleBackAction()
        {
            if (_menuState == State.Main)
            {
                // If current player is ready, unmark them and refresh
                if (_readyPlayerIndices.Contains(_playerIndex))
                {
                    int currentIndex = _playerIndex;
                    _readyPlayerIndices.Remove(currentIndex);

                    // Notify network that player is no longer ready
                    SetLocalPlayerReadyState(currentIndex, false);

                    UpdateForPlayer();
                }
                else if (_playerIndex == 0)
                {
                    // Check if in multiplayer
                    if (Networking.YargNetworkManager.Instance != null && Networking.YargNetworkManager.Instance.isNetworkActive)
                    {
                        // Host can go back (takes everyone with them), client shows confirmation
                        if (Networking.YargNetworkManager.Instance.IsHosting)
                        {
                            Debug.Log($"[DifficultySelectMenu] Host pressing back - Menu stack count: {MenuManager.Instance.MenuStackCount}");

                            // Sync menu navigation to all clients first
                            Networking.YargNetworkManager.Instance.SyncMenuNavigation(popMenu: true);

                            Debug.Log("[DifficultySelectMenu] Host synced, now navigating locally");

                            // Then host navigates back to music library
                            MenuManager.Instance.PopMenu();

                            Debug.Log($"[DifficultySelectMenu] Host navigation complete - Menu stack count: {MenuManager.Instance.MenuStackCount}");
                        }
                        else
                        {
                            // Client shows leave lobby confirmation dialog
                            ShowLeaveLobbyDialog();
                        }
                    }
                    else
                    {
                        // Not in multiplayer - just go back
                        MenuManager.Instance.PopMenu();
                    }
                }
                else
                {
                    // Go to previous player
                    ChangePlayer(-1);
                }
            }
            else
            {
                _menuState = State.Main;
                UpdateForPlayer();
            }
        }

        private void EnsureNavigationSelection()
        {
            if (_navGroup.Count > 0 && _navGroup.SelectedBehaviour == null)
            {
                _navGroup.SelectFirst();
            }
        }

        private void UpdateForPlayer()
        {
            // Set player text
            var profile = CurrentPlayer.Profile;
            _text.text = $"<sprite name=\"{profile.GameMode.ToResourceName()}\"> {profile.Name}";

            // Reset content
            _navGroup.ClearNavigatables();
            _container.DestroyChildren();
            StatsManager.Instance.UpdateActivePlayers();

            // Create the menu
            switch (_menuState)
            {
                case State.Main:
                    CreateMainMenu();
                    break;
                case State.Instrument:
                    CreateInstrumentMenu();
                    break;
                case State.Difficulty:
                    CreateDifficultyMenu();
                    break;
                case State.Modifiers:
                    CreateModifierMenu();
                    break;
                case State.Harmony:
                    CreateHarmonyMenu();
                    break;
            }

                    EnsureNavigationSelection();

            _lastMenuState = _menuState;
        }

        private void CreateMainMenu()
        {
            var player = CurrentPlayer;

            if (player.IsMissingMicrophone)
            {
                ShowWarning(Localize.Key("Menu.DifficultySelect.WarningVocalistNoMicrophone"));
            }
            else if (player.IsMissingInputDevice)
            {
                ShowWarning(Localize.Key("Menu.DifficultySelect.WarningPlayerNoInputDevice"));
            }
            else
            {
                ShowWarning(null);
            }

            // If this player is already ready, show their ready state
            if (_readyPlayerIndices.Contains(_playerIndex))
            {
                CreateReadyState(player);
                return;
            }
            
            // Show ready players side-by-side (Clone Hero style) - only if current player is NOT ready
            ShowReadyPlayers();

            // Only show all these options if there are instruments available
            if (_possibleInstruments.Count > 0)
            {
                CreateItem(LocalizeHeader("Instrument"),
                    player.Profile.CurrentInstrument.ToLocalizedName(),
                    _lastMenuState == State.Instrument, () =>
                {
                    _menuState = State.Instrument;
                    UpdateForPlayer();
                });

                CreateItem(LocalizeHeader("Difficulty"),
                    player.Profile.CurrentDifficulty.ToLocalizedName(),
                    _lastMenuState == State.Difficulty, () =>
                {
                    _menuState = State.Difficulty;
                    UpdateForPlayer();
                });

                // Harmony players must pick their harmony index
                if (player.Profile.CurrentInstrument == Instrument.Harmony)
                {
                    CreateItem(LocalizeHeader("Harmony"),
                        (player.Profile.HarmonyIndex + 1).ToString(),
                        _lastMenuState == State.Harmony, () =>
                    {
                        _menuState = State.Harmony;
                        UpdateForPlayer();
                    });
                }

                // Only allow vocal modifiers to be selected once (so they don't conflict)
                if (player.Profile.GameMode != GameMode.Vocals ||
                    _vocalModifierSelectIndex == -1 ||
                    _vocalModifierSelectIndex == _playerIndex)
                {
                    // Create modifiers body text
                    string modifierText = "";
                    if ((player.Profile.CurrentModifiers & ~_excusableModifiers) == Modifier.None)
                    {
                        // If there are no modifiers (ignoring the excusable ones), then just say "none"
                        modifierText = Modifier.None.ToLocalizedName();
                    }
                    else
                    {
                        // Combine all modifiers
                        foreach (var modifier in _possibleModifiers)
                        {
                            if (!player.Profile.IsModifierActive(modifier)) continue;

                            modifierText += modifier.ToLocalizedName() + "\n";
                        }

                        modifierText = modifierText.Trim();
                    }

                    CreateItem(LocalizeHeader("Modifiers"),
                        modifierText, _lastMenuState == State.Modifiers, () =>
                    {
                        _menuState = State.Modifiers;
                        UpdateForPlayer();
                    });
                }

                // Ready button
                CreateItem(LocalizeHeader("Ready"), _lastMenuState == State.Main, _difficultyGreenPrefab, () =>
                {
                    int currentIndex = _playerIndex;

                    // If the player just selected vocal modifiers, don't show them again
                    if (player.Profile.GameMode == GameMode.Vocals &&
                        _vocalModifierSelectIndex == -1)
                    {
                        _vocalModifierSelectIndex = currentIndex;
                    }

                    // Sync player selection to multiplayer
                    if (_multiplayerSync != null)
                    {
                        _multiplayerSync.OnPlayerSelectionComplete(player);
                    }

                    // Mark this player as ready
                    _readyPlayerIndices.Add(currentIndex);

                    // Notify network that player is ready
                    SetLocalPlayerReadyState(currentIndex, true);

                    // Refresh UI so this player's ready state is shown immediately
                    UpdateForPlayer();

                    // Advance to the next local player that still needs input, or finish if none remain
                    if (!TryAdvanceToNextPendingPlayer())
                    {
                        CompleteLocalPlayerSelection();
                    }
                });
            }

            // Only show if there is more than one play, only if there is instruments available
            if (_possibleInstruments.Count <= 0 || PlayerContainer.Players.Count != 1)
            {
                // Sit out button
                CreateItem(LocalizeHeader("SitOut"), _possibleInstruments.Count <= 0, _difficultyRedPrefab, () =>
                {
                    // If the user went back to sit out, and the vocal modifiers were selected,
                    // deselect them.
                    if (_vocalModifierSelectIndex == _playerIndex)
                    {
                        _vocalModifierSelectIndex = -1;
                    }

                    player.SittingOut = true;
                    ChangePlayer(1);
                });
            }
        }

        private void ShowWarning(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                _warningMessageContainer.SetActive(false);
                _warningMessage.text = "";
            }
            else
            {
                _warningMessageContainer.SetActive(true);
                _warningMessage.text = message;
            }
        }

        private void CreateInstrumentMenu()
        {
            foreach (var instrument in _possibleInstruments)
            {
                bool selected = CurrentPlayer.Profile.CurrentInstrument == instrument;
                CreateItem(instrument.ToLocalizedName(), selected, () =>
                {
                    CurrentPlayer.Profile.CurrentInstrument = instrument;
                    UpdatePossibleDifficulties();
                    UpdatePossibleModifiers();

                    _menuState = State.Main;
                    UpdateForPlayer();
                });
            }
        }

        private void CreateDifficultyMenu()
        {
            foreach (var difficulty in _possibleDifficulties)
            {
                bool selected = CurrentPlayer.Profile.CurrentDifficulty == difficulty;
                CreateItem(difficulty.ToLocalizedName(), selected, () =>
                {
                    CurrentPlayer.Profile.CurrentDifficulty
                        = CurrentPlayer.Profile.DifficultyFallback
                        = difficulty;

                    _menuState = State.Main;
                    UpdateForPlayer();
                });
            }
        }

        private void CreateModifierMenu()
        {
            var profile = CurrentPlayer.Profile;

            _modifierItems.Clear();
            foreach (var modifier in _possibleModifiers)
            {
                var btn = Instantiate(_modifierItemPrefab, _container);
                btn.Initialize(modifier.ToLocalizedName(), profile.IsModifierActive(modifier), active =>
                {
                    // Enable/disable the modifier
                    if (active)
                    {
                        profile.AddSingleModifier(modifier);
                    }
                    else
                    {
                        profile.RemoveModifiers(modifier);
                    }

                    UpdateModifierMenu();
                });

                _navGroup.AddNavigatable(btn);
                _modifierItems.Add(btn);
            }

            // Create done button
            CreateItem(LocalizeHeader("Done"), _difficultyGreenPrefab, () =>
            {
                _menuState = State.Main;
                UpdateForPlayer();
            });
        }

        private void CreateHarmonyMenu()
        {
            for (int i = 0; i < _maxHarmonyIndex; i++)
            {
                int capture = i;
                bool selected = CurrentPlayer.Profile.HarmonyIndex == i;
                CreateItem((i + 1).ToString(), selected, () =>
                {
                    CurrentPlayer.Profile.HarmonyIndex = (byte) capture;

                    _menuState = State.Main;
                    UpdateForPlayer();
                });
            }
        }

        private void UpdateModifierMenu()
        {
            var profile = CurrentPlayer.Profile;

            for (int i = 0; i < _modifierItems.Count; i++)
            {
                var item = _modifierItems[i];
                var modifier = _possibleModifiers[i];

                item.Active = profile.IsModifierActive(modifier);
            }
        }

        private void UpdatePossibleModifiers()
        {
            var profile = CurrentPlayer.Profile;

            // Get the possible modifiers (split the enum into multiple) and
            // make sure current modifiers are valid, and remove the invalid ones
            _possibleModifiers.Clear();
            var (possible, excusable) = profile.GameMode.PossibleModifiers(profile.CurrentInstrument);
            _excusableModifiers = excusable;

            foreach (var modifier in EnumExtensions<Modifier>.Values)
            {
                // Skip if the modifier is not a possible one
                if ((possible & modifier) == 0)
                {
                    // Also try to clear it if it isn't considered excusable yet the player somehow has it
                    if (((excusable & modifier) == 0) && profile.IsModifierActive(modifier))
                    {
                        profile.RemoveModifiers(modifier);
                    }

                    continue;
                }

                _possibleModifiers.Add(modifier);

                if (profile.IsModifierActive(modifier) && !_possibleModifiers.Contains(modifier))
                {
                    profile.RemoveModifiers(modifier);
                }
            }

        }

        private bool TryAdvanceToNextPendingPlayer()
        {
            int playerCount = PlayerContainer.Players.Count;
            if (playerCount == 0)
            {
                return false;
            }

            for (int nextIndex = _playerIndex + 1; nextIndex < playerCount; nextIndex++)
            {
                if (_readyPlayerIndices.Contains(nextIndex))
                {
                    continue;
                }

                var nextPlayer = PlayerContainer.Players[nextIndex];
                if (nextPlayer.SittingOut)
                {
                    continue;
                }

                ChangePlayer(nextIndex - _playerIndex);
                return true;
            }

            return false;
        }

        private void CompleteLocalPlayerSelection()
        {
            int playerCount = PlayerContainer.Players.Count;

            if (playerCount == 0 || PlayerContainer.Players.All(i => i.SittingOut))
            {
                MenuManager.Instance.PopMenu();

                DialogManager.Instance.ShowMessage("Nobody's Playing!",
                    "You tried to play a song with every player sitting out.");

                return;
            }

            // Clamp to a valid index so un-ready actions remain functional while waiting
            _playerIndex = Mathf.Clamp(_playerIndex, 0, playerCount - 1);

            // Ensure all vocal players have the same modifiers active
            if (_vocalModifierSelectIndex != -1)
            {
                var primaryPlayer = PlayerContainer.Players[_vocalModifierSelectIndex];

                foreach (var player in PlayerContainer.Players)
                {
                    if (player.SittingOut || player == primaryPlayer)
                    {
                        continue;
                    }

                    if (player.Profile.GameMode == GameMode.Vocals)
                    {
                        player.Profile.CopyModifiers(primaryPlayer.Profile);
                    }
                }
            }

            float speed = float.Parse(_speedInput.text.TrimEnd('%')) / 100f;
            speed = Mathf.Clamp(speed, 0.1f, 50.0f);
            _songSpeed = speed;
            GlobalVariables.State.SongSpeed = speed;

            if (_multiplayerSync != null)
            {
                _multiplayerSync.OnAllLocalPlayersReady();
            }
            else
            {
                GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
            }
        }

        private void ChangePlayer(int add)
        {
            SetActivePlayer(_playerIndex + add);
        }

        private void SetActivePlayer(int targetIndex)
        {
            int playerCount = PlayerContainer.Players.Count;

            if (playerCount == 0)
            {
                _playerIndex = 0;
                CompleteLocalPlayerSelection();
                return;
            }

            if (targetIndex >= playerCount)
            {
                _playerIndex = playerCount;
                CompleteLocalPlayerSelection();
                return;
            }

            if (targetIndex < 0)
            {
                targetIndex = 0;
            }

            _playerIndex = targetIndex;
            _menuState = State.Main;

            var profile = CurrentPlayer.Profile;
            var song = GlobalVariables.State.CurrentSong;

            _possibleInstruments.Clear();
            var allowedInstruments = profile.GameMode.PossibleInstruments();

            foreach (var instrument in allowedInstruments)
            {
                bool invalidInstrument = false;
                foreach (var showSong in _songList)
                {
                    if (!HasPlayableInstrument(showSong, instrument))
                    {
                        invalidInstrument = true;
                        break;
                    }
                }

                if (!invalidInstrument)
                {
                    _possibleInstruments.Add(instrument);
                }
            }

            if (!_possibleInstruments.Contains(profile.CurrentInstrument) && _possibleInstruments.Count > 0)
            {
                profile.CurrentInstrument = _possibleInstruments[0];
            }

            _maxHarmonyIndex = song.VocalsCount;
            foreach (var showsong in GlobalVariables.State.ShowSongs)
            {
                _maxHarmonyIndex = Mathf.Min(_maxHarmonyIndex, showsong.VocalsCount);
            }

            if (profile.HarmonyIndex >= _maxHarmonyIndex)
            {
                profile.HarmonyIndex = 0;
            }

            UpdatePossibleModifiers();

            CurrentPlayer.SittingOut = false;

            UpdatePossibleDifficulties();

            UpdateForPlayer();
        }

        private void UpdatePossibleDifficulties()
        {
            _possibleDifficulties.Clear();

            var profile = CurrentPlayer.Profile;

            // Get the possible difficulties for the player's instrument in the song
            foreach (var difficulty in EnumExtensions<Difficulty>.Values)
            {
                bool invalidDifficulty = false;
                foreach (var showsong in _songList)
                {
                    if (!HasPlayableDifficulty(showsong, profile.CurrentInstrument, difficulty))
                    {
                        invalidDifficulty = true;
                        break;
                    }
                }

                if (!invalidDifficulty)
                {
                    _possibleDifficulties.Add(difficulty);
                }
            }

            // TODO: Handle difficulty fallback better in play a show mode

            var diff = (int) profile.DifficultyFallback;
            while (diff >= (int) Difficulty.Beginner && !_possibleDifficulties.Contains((Difficulty) diff))
            {
                --diff;
            }

            if (diff < (int) Difficulty.Beginner)
            {
                diff = (int) profile.DifficultyFallback;
                while (diff < (int) Difficulty.ExpertPlus)
                {
                    ++diff;
                    if (_possibleDifficulties.Contains((Difficulty) diff))
                    {
                        break;
                    }
                }
            }
            profile.CurrentDifficulty = (Difficulty) diff;
        }

        private void OnDisable()
        {
            // Unsubscribe from waiting event
            if (_multiplayerSync != null)
            {
                _multiplayerSync.OnWaitingForPlayers -= ShowWaitingForPlayersMessage;
            }
            
            // Unsubscribe from network player events
            UnsubscribeFromNetworkPlayerEvents();
            
            // Clean up player entries
            foreach (var kvp in _playerEntries)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            _playerEntries.Clear();
            
            // Check if Navigator still exists (might be destroyed during scene transition)
            if (Navigator.Instance != null)
            {
                Navigator.Instance.PopScheme();
            }
        }

        private void ShowWaitingForPlayersMessage(string message)
        {
            if (_waitingForPlayersText != null)
            {
                _waitingForPlayersText.text = message;
                _waitingForPlayersText.gameObject.SetActive(true);
            }
            
            // Also update ready status display
            UpdateReadyStatus();
        }
        
        private void ShowReadyPlayers()
        {
            // Display all ready players side-by-side (Clone Hero style)
            if (_readyPlayerIndices.Count == 0) return;

            var readyPlayerNames = new System.Text.StringBuilder();
            foreach (var index in _readyPlayerIndices)
            {
                if (index < PlayerContainer.Players.Count)
                {
                    var readyPlayer = PlayerContainer.Players[index];
                    if (readyPlayerNames.Length > 0)
                        readyPlayerNames.Append("    ");
                    readyPlayerNames.Append($"✓ {readyPlayer.Profile.Name}");
                }
            }

            // Show all ready players in one line
            if (readyPlayerNames.Length > 0)
            {
                CreateItem(null, readyPlayerNames.ToString(), false, _difficultyGreenPrefab, () => { });
            }
        }

        private void SetLocalPlayerReadyState(int playerIndex, bool ready)
        {
            // Send ready state to network
            if (Networking.YargNetworkManager.Instance != null && Networking.YargNetworkManager.Instance.isNetworkActive)
            {
                var localNetworkPlayer = GetLocalNetworkPlayer(playerIndex);
                if (localNetworkPlayer != null)
                {
                    localNetworkPlayer.CmdSetReady(ready);
                    Debug.Log($"[DifficultySelect] Set local player {playerIndex} ready state to: {ready}");
                }
                else
                {
                    Debug.LogWarning($"[DifficultySelect] Could not find local network player for index {playerIndex} to set ready state.");
                }
            }

            // Update status text locally so UI reflects the change immediately
            UpdateReadyStatus();
        }
        
        private void ShowLeaveLobbyDialog()
        {
            if (DialogManager.Instance == null) return;
            
            var dialog = DialogManager.Instance.ShowMessage(
                "Leave Lobby?",
                "Are you sure you want to leave the lobby? You will be disconnected from the host."
            );
            
            dialog.ClearButtons();
            dialog.AddDialogButton("Cancel", MenuData.Colors.BrightButton, () => DialogManager.Instance.ClearDialog());
            dialog.AddDialogButton("Leave Lobby", MenuData.Colors.CancelButton, () =>
            {
                DialogManager.Instance.ClearDialog();
                
                // Client disconnects from network
                if (Networking.YargNetworkManager.Instance != null)
                {
                    Networking.YargNetworkManager.Instance.LeaveLobby();
                }
                
                // Go back to main menu or lobby browser
                MenuManager.Instance.PopMenu();
            });
        }
        
        private void CreateReadyState(YargPlayer player)
        {
            // Show player's selected options as disabled (non-interactive) items
            
            // Show instrument (disabled)
            CreateItem(LocalizeHeader("Instrument"),
                player.Profile.CurrentInstrument.ToLocalizedName(),
                false, () => { });

            // Show difficulty (disabled)
            CreateItem(LocalizeHeader("Difficulty"),
                player.Profile.CurrentDifficulty.ToLocalizedName(),
                false, () => { });

            // Show harmony index if applicable (disabled)
            if (player.Profile.CurrentInstrument == Instrument.Harmony)
            {
                CreateItem(LocalizeHeader("Harmony"),
                    (player.Profile.HarmonyIndex + 1).ToString(),
                    false, () => { });
            }

            // Show modifiers (disabled)
            string modifierText = "";
            if ((player.Profile.CurrentModifiers & ~_excusableModifiers) == Modifier.None)
            {
                modifierText = Modifier.None.ToLocalizedName();
            }
            else
            {
                foreach (var modifier in _possibleModifiers)
                {
                    if (!player.Profile.IsModifierActive(modifier)) continue;
                    modifierText += modifier.ToLocalizedName() + "\n";
                }
                modifierText = modifierText.Trim();
            }
            
            CreateItem(LocalizeHeader("Modifiers"),
                modifierText, false, () => { });
            
            // Show "Waiting for other players..." text if in multiplayer and not all players ready
            if (Networking.YargNetworkManager.Instance != null && Networking.YargNetworkManager.Instance.isNetworkActive)
            {
                var allPlayers = Networking.YargNetworkManager.Instance.GetAllPlayers();
                int readyCount = 0;
                foreach (var p in allPlayers)
                {
                    if (p != null && p.IsReady) readyCount++;
                }
                
                if (readyCount < allPlayers.Count)
                {
                    // Not all players ready - show waiting message
                    CreateItem(null, $"Waiting for other players... ({readyCount}/{allPlayers.Count})", false, () => { });
                }
            }
            
            // Add "Unready" button (green, interactive) - this is the ONLY clickable button
            CreateItem("Unready", true, _difficultyGreenPrefab, () =>
            {
                int currentIndex = _playerIndex;

                // Unmark player as ready
                _readyPlayerIndices.Remove(currentIndex);

                // Notify network that player is no longer ready
                SetLocalPlayerReadyState(currentIndex, false);

                // Refresh the menu to show options again
                UpdateForPlayer();
            });
        }
        
        private void UpdateReadyStatus()
        {
            if (_readyStatusText == null) return;
            // Check if in multiplayer
            if (Networking.YargNetworkManager.Instance == null || !Networking.YargNetworkManager.Instance.isNetworkActive)
            {
                _readyStatusText.gameObject.SetActive(false);
                return;
            }
            
            var allPlayers = Networking.YargNetworkManager.Instance.GetAllPlayers();
            int readyCount = 0;
            int totalCount = 0;
            int localPlayerCount = 0;
            int localReadyCount = 0;

            foreach (var player in allPlayers)
            {
                if (player == null)
                {
                    continue;
                }

                totalCount++;
                if (player.IsReady)
                {
                    readyCount++;
                }

                if (player.IsLocalUser)
                {
                    localPlayerCount++;
                    if (player.IsReady)
                    {
                        localReadyCount++;
                    }
                }
            }

            bool allLocalPlayersReady = localPlayerCount > 0 && localReadyCount == localPlayerCount;

            if (allLocalPlayersReady)
            {
                if (readyCount >= totalCount && totalCount > 0)
                {
                    _readyStatusText.text = "✓ All players ready! Starting game...";
                    _readyStatusText.color = Color.green;
                }
                else
                {
                    _readyStatusText.text = $"✓ You are ready! Waiting for other players... ({readyCount}/{totalCount})";
                    _readyStatusText.color = new Color(0f, 1f, 0.5f); // Cyan-ish green
                }
            }
            else
            {
                _readyStatusText.text = $"Players Ready: {readyCount}/{totalCount}";
                _readyStatusText.color = new Color(1f, 0.8f, 0f); // Orange/yellow
            }
            
            _readyStatusText.gameObject.SetActive(true);
        }
        
        private System.Collections.IEnumerator UpdateReadyStatusPeriodically()
        {
            while (enabled)
            {
                UpdateReadyStatus();
                yield return new WaitForSeconds(0.5f);
            }
        }

        private void CreateItem(string header, string body, bool selected, DifficultyItem difficultyItem, UnityAction a)
        {
            var btn = Instantiate(difficultyItem, _container);

            if (header is null)
            {
                btn.Initialize(body, a);
            }
            else
            {
                btn.Initialize(header, body, a);
            }

            _navGroup.AddNavigatable(btn.Button);

            if (selected)
            {
                _navGroup.SelectLast();
            }
        }

        private void CreateItem(string body, bool selected, DifficultyItem difficultyItem, UnityAction a)
        {
            CreateItem(null, body, selected, difficultyItem, a);
        }

        private void CreateItem(string header, string body, bool selected, UnityAction a)
        {
            CreateItem(header, body, selected, _difficultyItemPrefab, a);
        }

        private void CreateItem(string body, bool selected, UnityAction a)
        {
            CreateItem(null, body, selected, a);
        }

        private string LocalizeHeader(string key)
        {
            return Localize.Key("Menu.DifficultySelect", key);
        }

        private bool HasPlayableInstrument(SongEntry entry, in Instrument instrument)
        {
            // For vocals, all players *must* select the same gamemode (solo/harmony)
            if (instrument is Instrument.Vocals or Instrument.Harmony)
            {
                if (!entry.HasInstrument(instrument))
                {
                    return false;
                }

                // Loop through all of the players up to the current one
                // to see what has already been selected.
                for (int i = 0; i < _playerIndex; i++)
                {
                    var player = PlayerContainer.Players[i];
                    var playerInstrument = player.Profile.CurrentInstrument;
                    if (playerInstrument is Instrument.Vocals or Instrument.Harmony)
                    {
                        return playerInstrument == instrument;
                    }
                }
            }

            return entry.HasInstrument(instrument) || instrument switch
            {
                // Allow 5 -> 4-lane conversions to be played on 4-lane
                Instrument.FourLaneDrums or
                Instrument.ProDrums      => entry.HasInstrument(Instrument.FiveLaneDrums),
                // Allow 4 -> 5-lane conversions to be played on 5-lane
                Instrument.FiveLaneDrums => entry.HasInstrument(Instrument.ProDrums),
                _ => false
            };
        }

        private bool HasPlayableDifficulty(SongEntry entry, in Instrument instrument, in Difficulty difficulty)
        {
            // For vocals, insert special difficulties
            if (instrument is Instrument.Vocals or Instrument.Harmony)
            {
                return difficulty is not (Difficulty.Beginner or Difficulty.ExpertPlus);
            }

            // Otherwise, we can do this
            return entry[instrument][difficulty] || instrument switch
            {
                // Allow 5 -> 4-lane conversions to be played on 4-lane
                Instrument.FourLaneDrums or
                Instrument.ProDrums      => entry[Instrument.FiveLaneDrums][difficulty],
                // Allow 4 -> 5-lane conversions to be played on 5-lane
                Instrument.FiveLaneDrums => entry[Instrument.ProDrums][difficulty],
                _ => false
            };
        }

        public void SongSpeedEndEdit(string text)
        {
            if (!float.TryParse(text.TrimEnd('%'), NumberStyles.Number, null, out var speed))
            {
                speed = 100;
            }

            int intSpeed = (int) Math.Clamp(speed, 10, 5000);

            _speedInput.SetTextWithoutNotify($"{intSpeed}%");
        }
        
        private void SubscribeToNetworkPlayerEvents()
        {
            if (Networking.YargNetworkManager.Instance == null || !Networking.YargNetworkManager.Instance.isNetworkActive)
            {
                return;
            }
            
            var allPlayers = Networking.YargNetworkManager.Instance.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                if (player != null)
                {
                    player.OnReadyStateChangedEvent += OnNetworkPlayerReadyChanged;
                    player.OnInstrumentChangedEvent += OnNetworkPlayerInstrumentChanged;
                    player.OnDifficultyChangedEvent += OnNetworkPlayerDifficultyChanged;
                }
            }
        }
        
        private void UnsubscribeFromNetworkPlayerEvents()
        {
            if (Networking.YargNetworkManager.Instance == null || !Networking.YargNetworkManager.Instance.isNetworkActive)
            {
                return;
            }
            
            // Unsubscribe from player left event
            if (Networking.YargNetworkManager.Instance != null)
            {
                Networking.YargNetworkManager.Instance.OnPlayerLeft -= OnPlayerLeftLobby;
            }
            
            var allPlayers = Networking.YargNetworkManager.Instance.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                if (player != null)
                {
                    player.OnReadyStateChangedEvent -= OnNetworkPlayerReadyChanged;
                    player.OnInstrumentChangedEvent -= OnNetworkPlayerInstrumentChanged;
                    player.OnDifficultyChangedEvent -= OnNetworkPlayerDifficultyChanged;
                }
            }
        }
        
        private void OnPlayerLeftLobby(Networking.NetworkPlayerData player)
        {
            Debug.Log($"[DifficultySelectMenu] Player left: {player?.PlayerName}");
            UpdateMultiplayerPlayerList();
            UpdateReadyStatus();
        }
        
        private void OnNetworkPlayerReadyChanged(bool isReady)
        {
            UpdateReadyStatus();
            UpdateMultiplayerPlayerList();
            
            // If current player is ready, refresh their UI to update the waiting message
            if (_readyPlayerIndices.Contains(_playerIndex))
            {
                UpdateForPlayer();
            }
            
            // Check if all players are ready and auto-start
            CheckAndAutoStart();
        }
        
        private void OnNetworkPlayerInstrumentChanged(int instrument, int difficulty)
        {
            UpdateMultiplayerPlayerList();
        }
        
        private void OnNetworkPlayerDifficultyChanged(int instrument, int difficulty)
        {
            UpdateMultiplayerPlayerList();
        }
        
        private void CheckAndAutoStart()
        {
            if (Networking.YargNetworkManager.Instance == null || !Networking.YargNetworkManager.Instance.isNetworkActive)
            {
                return;
            }
            
            // Only check on host
            if (!Networking.YargNetworkManager.Instance.IsHosting)
            {
                return;
            }
            
            // Check if all players are ready
            if (Networking.YargNetworkManager.Instance.AreAllPlayersReady())
            {
                Debug.Log("[DifficultySelect] All players ready - auto-starting gameplay");
                
                // Small delay to show "All players ready!" message
                StartCoroutine(AutoStartGameplayAfterDelay());
            }
        }
        
        private System.Collections.IEnumerator AutoStartGameplayAfterDelay()
        {
            yield return new WaitForSeconds(1.0f);
            
            // Start gameplay for all players
            Networking.YargNetworkManager.Instance.StartMultiplayerGameplay();
        }
        
        private System.Collections.IEnumerator DelayedUpdateMultiplayerPlayerList()
        {
            // Wait a frame for NetworkPlayerData objects to be fully spawned
            yield return null;
            
            Debug.Log("[DifficultySelectMenu] Delayed update of multiplayer player list");
            UpdateMultiplayerPlayerList();
        }
        
        private void UpdateMultiplayerPlayerList()
        {
            // Early exit if containers not assigned (optional feature)
            if (_multiplayerPlayerListContainer == null || _multiplayerPlayerListContent == null)
            {
                return;
            }
            
            // Only show player list container when in multiplayer
            if (Networking.YargNetworkManager.Instance == null || !Networking.YargNetworkManager.Instance.isNetworkActive)
            {
                _multiplayerPlayerListContainer.SetActive(false);
                return;
            }
            
            _multiplayerPlayerListContainer.SetActive(true);
            
            var allPlayers = Networking.YargNetworkManager.Instance.GetAllPlayers();
            Debug.Log($"[DifficultySelectMenu] Found {allPlayers.Count} players in network");
            var currentPlayers = new HashSet<Networking.NetworkPlayerData>(allPlayers.Where(p => p != null));
            
            // Remove entries for players that left
            var playersToRemove = new List<Networking.NetworkPlayerData>();
            foreach (var kvp in _playerEntries)
            {
                if (!currentPlayers.Contains(kvp.Key))
                {
                    playersToRemove.Add(kvp.Key);
                    if (kvp.Value != null && kvp.Value.gameObject != null)
                    {
                        Destroy(kvp.Value.gameObject);
                    }
                }
            }
            
            foreach (var player in playersToRemove)
            {
                _playerEntries.Remove(player);
            }
            
            // Add entries for new players
            foreach (var player in allPlayers)
            {
                if (player == null) continue;
                
                if (!_playerEntries.ContainsKey(player))
                {
                    Debug.Log($"[DifficultySelectMenu] Creating entry for player: {player.PlayerName}");
                    CreateMultiplayerPlayerEntry(player);
                }
            }
            
            Debug.Log($"[DifficultySelectMenu] Total player entries: {_playerEntries.Count}");
        }
        
        private void UpdateSongQueue()
        {
            // Early exit if containers not assigned
            if (_songQueueContainer == null || _songQueueContent == null)
            {
                return;
            }
            
            // Only show container if playing a show with songs in the queue
            if (!GlobalVariables.State.PlayingAShow || GlobalVariables.State.ShowSongs == null || GlobalVariables.State.ShowSongs.Count == 0)
            {
                _songQueueContainer.SetActive(false);
                return;
            }
            
            Debug.Log($"[DifficultySelectMenu] Updating song queue - {GlobalVariables.State.ShowSongs.Count} songs, current index: {GlobalVariables.State.ShowIndex}");
            _songQueueContainer.SetActive(true);
            Debug.Log($"[DifficultySelectMenu] Song queue container is now active: {_songQueueContainer.activeSelf}");
            
            // Log container hierarchy and properties
            Debug.Log($"[DifficultySelectMenu] Container name: {_songQueueContainer.name}, Content name: {_songQueueContent.name}");
            var containerRect = _songQueueContainer.GetComponent<RectTransform>();
            if (containerRect != null)
            {
                Debug.Log($"[DifficultySelectMenu] Container RectTransform - AnchoredPos: {containerRect.anchoredPosition}, SizeDelta: {containerRect.sizeDelta}, Scale: {containerRect.localScale}");
            }
            
            var contentRect = _songQueueContent.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                Debug.Log($"[DifficultySelectMenu] Content RectTransform - AnchoredPos: {contentRect.anchoredPosition}, SizeDelta: {contentRect.sizeDelta}, Scale: {contentRect.localScale}");
            }
            
            // Check for layout components
            var layoutGroup = _songQueueContent.GetComponent<VerticalLayoutGroup>();
            if (layoutGroup != null)
            {
                Debug.Log($"[DifficultySelectMenu] Content has VerticalLayoutGroup - enabled: {layoutGroup.enabled}, padding: {layoutGroup.padding.top}/{layoutGroup.padding.bottom}, spacing: {layoutGroup.spacing}");
            }
            else
            {
                Debug.LogWarning("[DifficultySelectMenu] Content missing VerticalLayoutGroup component!");
            }
            
            var contentSizeFitter = _songQueueContent.GetComponent<ContentSizeFitter>();
            if (contentSizeFitter != null)
            {
                Debug.Log($"[DifficultySelectMenu] Content has ContentSizeFitter - Vertical: {contentSizeFitter.verticalFit}");
            }
            
            // Clear existing entries
            foreach (var entry in _songQueueEntries)
            {
                if (entry != null && entry.gameObject != null)
                {
                    Destroy(entry.gameObject);
                }
            }
            _songQueueEntries.Clear();
            
            // Create entry for each song in the queue
            for (int i = 0; i < GlobalVariables.State.ShowSongs.Count; i++)
            {
                var song = GlobalVariables.State.ShowSongs[i];
                bool isCurrent = (i == GlobalVariables.State.ShowIndex);
                
                CreateSongQueueEntry(song, isCurrent);
            }
            
            Debug.Log($"[DifficultySelectMenu] Created {_songQueueEntries.Count} song queue entries");
        }
        
        private void CreateSongQueueEntry(SongEntry song, bool isCurrent)
        {
            if (_songQueueContent == null || song == null)
            {
                Debug.LogWarning("[DifficultySelectMenu] CreateSongQueueEntry: Content or song is NULL!");
                return;
            }
            
            GameObject entryObject;
            SongQueueEntry entryComponent;
            
            if (_songQueueEntryPrefab != null)
            {
                // Use prefab if available
                Debug.Log($"[DifficultySelectMenu] Using prefab to create entry for {song.Name}");
                entryObject = Instantiate(_songQueueEntryPrefab, _songQueueContent);
                
                // Force the entry to be active and visible
                entryObject.SetActive(true);
                
                // Log detailed hierarchy info
                Debug.Log($"[DifficultySelectMenu] Prefab instantiated - Name: {entryObject.name}, Active: {entryObject.activeSelf}, Parent: {_songQueueContent.name}");
                
                var rectTransform = entryObject.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    Debug.Log($"[DifficultySelectMenu] Entry RectTransform - AnchoredPos: {rectTransform.anchoredPosition}, SizeDelta: {rectTransform.sizeDelta}, Scale: {rectTransform.localScale}");
                }
                
                // Check for Canvas components that might affect visibility
                var canvas = entryObject.GetComponent<Canvas>();
                if (canvas != null)
                {
                    Debug.Log($"[DifficultySelectMenu] Entry has Canvas component - enabled: {canvas.enabled}");
                }
                
                var canvasGroup = entryObject.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    Debug.Log($"[DifficultySelectMenu] Entry has CanvasGroup - alpha: {canvasGroup.alpha}, interactable: {canvasGroup.interactable}, blocksRaycasts: {canvasGroup.blocksRaycasts}");
                    // Ensure it's visible
                    canvasGroup.alpha = 1f;
                }
                
                entryComponent = entryObject.GetComponent<SongQueueEntry>();
                
                if (entryComponent == null)
                {
                    Debug.LogWarning("[DifficultySelectMenu] Prefab missing SongQueueEntry component, adding it");
                    entryComponent = entryObject.AddComponent<SongQueueEntry>();
                }
            }
            else
            {
                // Fallback: create simple text entry
                Debug.Log($"[DifficultySelectMenu] No prefab, creating fallback entry for {song.Name}");
                entryObject = new GameObject($"SongQueueEntry_{song.Name}");
                entryObject.transform.SetParent(_songQueueContent, false);
                
                // Add RectTransform for UI
                var rectTransform = entryObject.AddComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(0, 40); // Height of 40
                
                // Add LayoutElement to ensure proper sizing
                var layoutElement = entryObject.AddComponent<LayoutElement>();
                layoutElement.minHeight = 40;
                layoutElement.preferredHeight = 40;
                
                // Add background - green for current song, gray for others
                var bg = entryObject.AddComponent<Image>();
                if (isCurrent)
                {
                    bg.color = new Color(0.2f, 0.9f, 0.2f, 0.5f); // Green for current song
                }
                else
                {
                    bg.color = new Color(0.2f, 0.2f, 0.2f, 0.3f); // Gray for other songs
                }
                
                // Create text for song name
                var textGO = new GameObject("Text");
                textGO.transform.SetParent(entryObject.transform, false);
                var textRect = textGO.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(10, 0);
                textRect.offsetMax = new Vector2(-10, 0);
                
                var text = textGO.AddComponent<TextMeshProUGUI>();
                text.text = $"{song.Name}\n<size=12>{song.Artist}</size>";
                text.fontSize = 16;
                text.color = Color.white;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                
                // Make current song bold
                if (isCurrent)
                {
                    text.fontStyle = FontStyles.Bold;
                }
                
                entryComponent = entryObject.AddComponent<SongQueueEntry>();
            }
            
            entryComponent.Initialize(song, isCurrent);
            _songQueueEntries.Add(entryComponent);
            
            Debug.Log($"[DifficultySelectMenu] Entry created and added - Total entries: {_songQueueEntries.Count}, Entry active: {entryObject.activeSelf}, In hierarchy: {entryObject.transform.parent != null}");
        }
        
        private void CreateMultiplayerPlayerEntry(Networking.NetworkPlayerData player)
        {
            if (_multiplayerPlayerListContent == null || player == null)
            {
                Debug.LogWarning("[DifficultySelectMenu] CreateEntry: Content or player is NULL!");
                return;
            }
            
            // Don't create duplicate entries
            if (_playerEntries.ContainsKey(player))
            {
                Debug.Log($"[DifficultySelectMenu] Entry already exists for {player.PlayerName}");
                return;
            }
            
            GameObject entryObject;
            MultiplayerPlayerEntry entryComponent;
            
            if (_multiplayerPlayerEntryPrefab != null)
            {
                Debug.Log($"[DifficultySelectMenu] Using prefab to create entry for {player.PlayerName}");
                // Use prefab if available
                entryObject = Instantiate(_multiplayerPlayerEntryPrefab, _multiplayerPlayerListContent);
                entryComponent = entryObject.GetComponent<MultiplayerPlayerEntry>();
                
                if (entryComponent == null)
                {
                    Debug.LogWarning("[DifficultySelectMenu] Prefab missing MultiplayerPlayerEntry component, adding it");
                    entryComponent = entryObject.AddComponent<MultiplayerPlayerEntry>();
                    
                    // Wire up references to existing children in prefab
                    var playerNameText = entryObject.transform.Find("PlayerName")?.GetComponent<TextMeshProUGUI>();
                    var readyStatusIcon = entryObject.transform.Find("ReadyStatus")?.GetComponent<Image>();
                    
                    Debug.Log($"[DifficultySelectMenu] Found prefab children - Name: {playerNameText != null}, Status: {readyStatusIcon != null}");
                    
                    if (playerNameText != null && readyStatusIcon != null)
                    {
                        var type = typeof(MultiplayerPlayerEntry);
                        type.GetField("_playerNameText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(entryComponent, playerNameText);
                        type.GetField("_readyStatusIcon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(entryComponent, readyStatusIcon);
                        Debug.Log($"[DifficultySelectMenu] Wired up prefab references via reflection for {player.PlayerName}");
                    }
                    else
                    {
                        Debug.LogError("[DifficultySelectMenu] Prefab is missing required children! Expected: InstrumentIcon, PlayerName, DifficultyIcon, ReadyStatus");
                    }
                }
            }
            else
            {
                // Create entry from scratch
                entryObject = new GameObject($"PlayerEntry_{player.PlayerName}");
                entryObject.transform.SetParent(_multiplayerPlayerListContent, false);
                
                // Add layout group for horizontal layout
                var layoutGroup = entryObject.AddComponent<HorizontalLayoutGroup>();
                layoutGroup.spacing = 10f;
                layoutGroup.childAlignment = TextAnchor.MiddleLeft;
                layoutGroup.childControlWidth = false;
                layoutGroup.childControlHeight = false;
                layoutGroup.childForceExpandWidth = false;
                layoutGroup.childForceExpandHeight = false;
                
                // Create player name text (will contain inline sprite icons)
                // Create container to hold all elements
                var containerObj = new GameObject("Container");
                containerObj.transform.SetParent(entryObject.transform, false);
                var containerRect = containerObj.AddComponent<RectTransform>();
                containerRect.anchorMin = new Vector2(0, 0.5f);
                containerRect.anchorMax = new Vector2(0, 0.5f);
                containerRect.pivot = new Vector2(0, 0.5f);
                // Create background panel for the entry (Halo Infinite style)
                var backgroundObj = new GameObject("Background");
                backgroundObj.transform.SetParent(containerObj.transform, false);
                var backgroundImage = backgroundObj.AddComponent<Image>();
                backgroundImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f); // Dark semi-transparent
                var backgroundRect = backgroundImage.GetComponent<RectTransform>();
                backgroundRect.anchorMin = Vector2.zero;
                backgroundRect.anchorMax = Vector2.one;
                backgroundRect.sizeDelta = Vector2.zero;
                backgroundRect.anchoredPosition = Vector2.zero;
                
                // Create separator line at bottom (subtle)
                var separatorObj = new GameObject("Separator");
                separatorObj.transform.SetParent(containerObj.transform, false);
                var separatorImage = separatorObj.AddComponent<Image>();
                separatorImage.color = new Color(0.3f, 0.3f, 0.3f, 0.5f); // Subtle gray line
                var separatorRect = separatorImage.GetComponent<RectTransform>();
                separatorRect.anchorMin = new Vector2(0, 0);
                separatorRect.anchorMax = new Vector2(1, 0);
                separatorRect.pivot = new Vector2(0.5f, 0);
                separatorRect.sizeDelta = new Vector2(0, 1); // 1px height
                separatorRect.anchoredPosition = Vector2.zero;
                
                // Container setup - shorter height for compact Halo style
                containerRect.sizeDelta = new Vector2(400, 35);
                containerRect.anchoredPosition = Vector2.zero;
                
                // Create combined icons (instrument + difficulty) - smaller Halo style
                var iconsObj = new GameObject("Icons");
                iconsObj.transform.SetParent(containerObj.transform, false);
                var iconsText = iconsObj.AddComponent<TextMeshProUGUI>();
                iconsText.fontSize = 20; // Smaller icons
                iconsText.alignment = TextAlignmentOptions.MidlineLeft;
                iconsText.richText = true;
                var iconsRect = iconsText.GetComponent<RectTransform>();
                iconsRect.sizeDelta = new Vector2(60, 35); // Smaller to match entry height
                iconsRect.anchoredPosition = new Vector2(8, 0); // Small left padding
                iconsRect.anchorMin = new Vector2(0, 0.5f);
                iconsRect.anchorMax = new Vector2(0, 0.5f);
                iconsRect.pivot = new Vector2(0, 0.5f);
                
                // Create player name text - Halo Infinite style
                var nameObj = new GameObject("PlayerName");
                nameObj.transform.SetParent(containerObj.transform, false);
                var nameText = nameObj.AddComponent<TextMeshProUGUI>();
                nameText.fontSize = 18; // Slightly smaller, cleaner
                nameText.alignment = TextAlignmentOptions.MidlineLeft;
                nameText.enableWordWrapping = false;
                nameText.overflowMode = TextOverflowModes.Ellipsis;
                nameText.horizontalAlignment = HorizontalAlignmentOptions.Left;
                nameText.color = Color.white; // Pure white like Halo
                
                var nameRect = nameText.GetComponent<RectTransform>();
                nameRect.anchorMin = new Vector2(0, 0.5f);
                nameRect.anchorMax = new Vector2(0, 0.5f);
                nameRect.pivot = new Vector2(0, 0.5f);
                nameRect.anchoredPosition = new Vector2(75, 0); // Closer to icons
                nameRect.sizeDelta = new Vector2(240, 35);
                
                // Create ready status icon - right side like Halo
                var statusObj = new GameObject("ReadyStatus");
                statusObj.transform.SetParent(containerObj.transform, false);
                var statusIcon = statusObj.AddComponent<Image>();
                var statusRect = statusIcon.GetComponent<RectTransform>();
                statusRect.sizeDelta = new Vector2(24, 24); // Smaller, more subtle
                statusRect.anchoredPosition = new Vector2(-10, 0); // Right-aligned with padding
                statusRect.anchorMin = new Vector2(1, 0.5f); // Anchor to right
                statusRect.anchorMax = new Vector2(1, 0.5f);
                statusRect.pivot = new Vector2(1, 0.5f);
                
                // Add MultiplayerPlayerEntry component and wire up references via reflection
                entryComponent = entryObject.AddComponent<MultiplayerPlayerEntry>();
                var type = typeof(MultiplayerPlayerEntry);
                type.GetField("_iconsText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(entryComponent, iconsText);
                type.GetField("_playerNameText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(entryComponent, nameText);
                type.GetField("_readyStatusIcon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(entryComponent, statusIcon);
            }
            
            entryObject.SetActive(true);
            
            Debug.Log($"[DifficultySelectMenu] Entry created for {player.PlayerName}, initializing...");
            
            // Initialize the entry with player data
            if (entryComponent != null)
            {
                entryComponent.Initialize(player);
                _playerEntries[player] = entryComponent;
                Debug.Log($"[DifficultySelectMenu] Entry initialized successfully for {player.PlayerName}");
            }
            else
            {
                Debug.LogError($"[DifficultySelectMenu] Failed to create MultiplayerPlayerEntry component for {player.PlayerName}!");
            }
        }
        
        private Networking.NetworkPlayerData GetLocalNetworkPlayer(int playerIndex)
        {
            if (Networking.YargNetworkManager.Instance == null || !Networking.YargNetworkManager.Instance.isNetworkActive)
            {
                return null;
            }
            
            var allPlayers = Networking.YargNetworkManager.Instance.GetAllPlayers();
            foreach (var player in allPlayers)
            {
                if (player != null && player.IsLocalUser && player.PlayerIndex == playerIndex)
                {
                    return player;
                }
            }
            
            return null;
        }
    }
}