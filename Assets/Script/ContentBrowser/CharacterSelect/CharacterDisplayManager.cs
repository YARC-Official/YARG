using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UniVRM10;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Settings;
using YARG.Settings.Customization;
using YARG.Venue.Characters;

namespace YARG.ContentBrowser.CharacterSelect
{
    public class CharacterDisplayManager : MonoBehaviour
    {
        [SerializeField]
        private GameObject _displayPrefab;
        [SerializeField]
        private TextMeshPro _characterName;
        [SerializeField]
        private TextMeshPro _characterCredits;

        [Header("Lights")]
        [SerializeField]
        private Light _spotlight;
        [SerializeField]
        private Light _leftLight;
        [SerializeField]
        private Light _rightLight;

        [Header("Locations")]
        [SerializeField]
        private Transform _selectedLocation;
        [SerializeField]
        private Transform _leftLocation;
        [SerializeField]
        private Transform _rightLocation;
        [SerializeField]
        private Transform _hiddenLocation;

        // We will have up to 3 characters displayed at once, plus one hidden
        private readonly Podium[] _podiums = new Podium[4];

        private GameObject _instance;

        private const float SPOTLIGHT_INTENSITY = 15.0f;

        public class CharacterInfo
        {
            public GameObject Prefab;
            public string     Name;
            public string     Author;
            public string     Identifier;
            public bool       IsAddressable;
            public AsyncOperationHandle<GameObject> Handle;
        }

        private enum SlotState
        {
            Pending,
            Loaded,
            Failed,
        }

        // A position in the carousel. Slots are created up front in a deterministic order so
        // that the order of finished loads can never reorder the carousel.
        private sealed class CharacterSlot
        {
            public SlotState State = SlotState.Pending;
            public CharacterInfo Info;
        }

        private readonly List<CharacterSlot> _slots = new();
        private int _currentCharacterIndex = 0;

        private bool _loadingFinished = false;
        private bool _loadingOverlayDismissed = false;
        private bool _isWaitingForSlot = false;

        // The character currently at the front of the carousel
        private CharacterInfo _primaryCharacter;

        // The currently selected character
        private CharacterInfo _selectedCharacter;

        // Rotation stuffs
        private const float MOVE_DURATION = 0.8f;
        private       float _currentAngle = 0f;
        private       Tween _rotationTween;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private async UniTaskVoid Start()
        {
            // Clear text until we have a character
            _characterName.text = string.Empty;
            _characterCredits.text = string.Empty;

            var loading = new LoadingContext();
            loading.SetLoadingText("Loading characters...");

            // Kick off background loading and wait until either everything is loaded, or the
            // slots we are about to display are. The remaining slots keep loading in the background.
            var cts = this.GetCancellationTokenOnDestroy();
            _ = LoadAllCharacters(loading, cts);
            try
            {
                await UniTask.WaitUntil(AreInitialSlotsReady, cancellationToken: cts);
            }
            finally
            {
                _loadingOverlayDismissed = true;
                loading.Dispose();
            }

            // Set a navigation scheme
            if (_slots.Count > 0)
            {
                _ = Navigator.Instance.PushScheme(new NavigationScheme(new()
                {
                    new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm", Select),
                    new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", Exit),
                    new NavigationScheme.Entry(MenuAction.Left, "Menu.Common.Scroll", Left),
                    new NavigationScheme.Entry(MenuAction.Right, "Menu.Common.Scroll", Right),
                }, true));
            }
            else
            {
                _ = Navigator.Instance.PushScheme(new NavigationScheme(new()
                {
                    new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", Exit),
                }, true));
                _characterName.SetText(Localize.Key("Menu.Content.CharacterSelect.NoCharacters"));
                _characterCredits.SetText(Localize.Key("Menu.Content.CharacterSelect.NoCharactersSubtext"));
            }

            // Instantiate all CharacterDisplays first, then assign them front-first so that the
            // layout matches the rotation direction (Left = +1, Right = -1):
            // index 0 (Left)  = _currentCharacterIndex + 1 (wrapping if necessary)
            // index 1 (Front) = _currentCharacterIndex
            // index 2 (Right) = _currentCharacterIndex - 1 (wrapping if necessary)
            // index 3 (Hidden) is left empty and is only populated while rotating
            for (int i = 0; i < _podiums.Length; i++)
            {
                var instance = Instantiate(_displayPrefab, GetDisplayLocation(i));
                _podiums[i] = instance.GetComponent<Podium>();
            }

            var assignedCharacters = new HashSet<CharacterInfo>();
            _podiums[1].SetCharacter(GetUniqueCharacterInfo(0, assignedCharacters));
            _podiums[0].SetCharacter(GetUniqueCharacterInfo(1, assignedCharacters));
            _podiums[2].SetCharacter(GetUniqueCharacterInfo(-1, assignedCharacters));

            _podiums[3].gameObject.SetActive(false);

            UpdatePrimaryCharacter();
        }

        private bool AreInitialSlotsReady()
        {
            if (_loadingFinished)
            {
                return true;
            }

            int count = _slots.Count;
            if (count < 5)
            {
                return false;
            }

            // The front podium and its direct neighbours, plus the first hidden candidates
            return _slots[0].State == SlotState.Loaded
                && _slots[1].State == SlotState.Loaded
                && _slots[2].State == SlotState.Loaded
                && _slots[count - 2].State == SlotState.Loaded
                && _slots[count - 1].State == SlotState.Loaded;
        }

        private void UpdateNavigationScheme()
        {
            if (_slots.Count == 0)
            {
                return;
            }

            Navigator.Instance.PopScheme();

            var confirmEntry = new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm", Select);
            var unhideEntry = new NavigationScheme.Entry(MenuAction.Orange, "Menu.Content.CharacterSelect.Unhide", Unhide);
            var hideEntry = new NavigationScheme.Entry(MenuAction.Orange, "Menu.Content.CharacterSelect.Hide", Hide);
            var unsetEntry = new NavigationScheme.Entry(MenuAction.Orange, "Menu.Content.CharacterSelect.Unset", Unselect);

            var navigationEntries = new List<NavigationScheme.Entry>
            {
                new (MenuAction.Red, "Menu.Common.Back", Exit),
                new (MenuAction.Left, "Menu.Common.Scroll", Left),
                new (MenuAction.Right, "Menu.Common.Scroll", Right),
            };

            if (_primaryCharacter != null)
            {
                navigationEntries.Insert(0, confirmEntry);

                if (IsSelected(_primaryCharacter))
                {
                    navigationEntries.Insert(2, unsetEntry);
                }
                else if (IsHidden(_primaryCharacter))
                {
                    navigationEntries.Insert(2, unhideEntry);
                }
                else if (_primaryCharacter.IsAddressable)
                {
                    navigationEntries.Insert(2, hideEntry);
                }
            }

            _ = Navigator.Instance.PushScheme(new NavigationScheme(navigationEntries, true));
        }

        private Transform GetDisplayLocation(int index)
        {
            return index switch
            {
                0 => _leftLocation,
                1 => _selectedLocation,
                2 => _rightLocation,
                3 => _hiddenLocation,
                _ => throw new ArgumentOutOfRangeException(nameof(index), index, null)
            };
        }

        private CharacterInfo GetUniqueCharacterInfo(int offset, HashSet<CharacterInfo> assigned)
        {
            var characterInfo = GetCharacterInfo(offset);
            if (characterInfo == null || !assigned.Add(characterInfo))
            {
                return null;
            }

            return characterInfo;
        }

        private CharacterInfo GetCharacterInfo(int offset)
        {
            if (_slots.Count == 0)
            {
                return null;
            }

            var index = WrapCharacterIndex(_currentCharacterIndex + offset);
            if (index >= _slots.Count || _slots[index].State != SlotState.Loaded)
            {
                return null;
            }

            return _slots[index].Info;
        }

        private int WrapCharacterIndex(int index)
        {
            int count = _slots.Count;
            if (count == 0)
            {
                return 0;
            }

            return ((index % count) + count) % count;
        }

        private void UpdatePrimaryCharacter()
        {
            if (_slots.Count == 0)
            {
                return;
            }

            var selectedPodium = _podiums[1];

            if (selectedPodium == null || selectedPodium.CharacterInfo == null)
            {
                // An empty podium is in front, so nothing can be confirmed/hidden/unselected
                _primaryCharacter = null;
                _characterName.SetText(string.Empty);
                _characterCredits.SetText(string.Empty);
                _spotlight.color = Color.white;

                UpdateNavigationScheme();
                return;
            }

            _primaryCharacter = selectedPodium.CharacterInfo;
            _characterName.SetText(selectedPodium.Name);
            _characterCredits.SetText(selectedPodium.Credits);

            if (IsSelected(_primaryCharacter))
            {
                _spotlight.color = Color.gold;
            }
            else if (IsHidden(_primaryCharacter))
            {
                _spotlight.color = Color.darkRed;
            }
            else
            {
                _spotlight.color = Color.white;
            }

            UpdateNavigationScheme();
        }

        private void Select()
        {
            if (_primaryCharacter == null)
            {
                return;
            }

            var characterInfo = new CustomCharacterInfo
            {
                Source = _primaryCharacter.IsAddressable
                    ? CustomCharacterSource.Addressable
                    : CustomCharacterSource.File,
                Identifier = _primaryCharacter.Identifier
            };

            if (SettingsManager.Settings.HiddenCharacters.Contains(characterInfo))
            {
                SettingsManager.Settings.HiddenCharacters.Remove(characterInfo);
            }

            SettingsManager.Settings.CustomCharacters[VenueCharacter.CharacterType.Vocals] = characterInfo;

            // Turn the spotlight gold or something
            _spotlight.DOColor(Color.gold, MOVE_DURATION * 0.333f);
            _podiums[1].SetLightColor(Color.gold, MOVE_DURATION * 0.333f);

            for (var i = 0; i < _podiums.Length; i++)
            {
                var podium = _podiums[i];
                if (i != 1 && podium.Light.color == Color.gold)
                {
                    podium.SetLightColor(Color.white, MOVE_DURATION * 0.333f);
                }
            }

            UpdateNavigationScheme();
        }

        private void Unselect()
        {
            if (_primaryCharacter == null)
            {
                return;
            }

            SettingsManager.Settings.CustomCharacters.Remove(VenueCharacter.CharacterType.Vocals);

            _spotlight.DOColor(Color.white, MOVE_DURATION * 0.333f);
            _podiums[1].SetLightColor(Color.white, MOVE_DURATION * 0.333f);

            UpdateNavigationScheme();
        }

        private void Hide()
        {
            if (_primaryCharacter == null)
            {
                return;
            }

            var characterInfo = new CustomCharacterInfo
            {
                Source = _primaryCharacter.IsAddressable
                    ? CustomCharacterSource.Addressable
                    : CustomCharacterSource.File,
                Identifier = _primaryCharacter.Identifier,
            };

            if (!SettingsManager.Settings.HiddenCharacters.Contains(characterInfo))
            {
                SettingsManager.Settings.HiddenCharacters.Add(characterInfo);
            }

            _spotlight.DOColor(Color.darkRed, MOVE_DURATION * 0.333f);
            _podiums[1].SetLightColor(Color.darkRed, MOVE_DURATION * 0.333f);

            UpdateNavigationScheme();
        }

        private void Unhide()
        {
            if (_primaryCharacter == null)
            {
                return;
            }

            var characterInfo = new CustomCharacterInfo
            {
                Source = _primaryCharacter.IsAddressable
                    ? CustomCharacterSource.Addressable
                    : CustomCharacterSource.File,
                Identifier = _primaryCharacter.Identifier,
            };

            if (SettingsManager.Settings.HiddenCharacters.Contains(characterInfo))
            {
                SettingsManager.Settings.HiddenCharacters.Remove(characterInfo);
            }

            _spotlight.DOColor(Color.white, MOVE_DURATION * 0.333f);
            _podiums[1].SetLightColor(Color.white, MOVE_DURATION * 0.333f);

            UpdateNavigationScheme();
        }

        private void Right()
        {
            RotateStepAsync(false).Forget();
        }

        private void Left()
        {
            RotateStepAsync(true).Forget();
        }

        private async UniTaskVoid RotateStepAsync(bool clockwise)
        {
            if (_slots.Count == 0 || _isWaitingForSlot || (_rotationTween != null && _rotationTween.IsPlaying()))
            {
                return;
            }

            var step = clockwise ? 1 : -1;
            var hiddenIndex = -1;

            if (_slots.Count >= _podiums.Length)
            {
                var ct = this.GetCancellationTokenOnDestroy();

                _isWaitingForSlot = true;
                try
                {
                    while (_slots.Count >= _podiums.Length)
                    {
                        var slotIndex = WrapCharacterIndex(_currentCharacterIndex + (step * 2));
                        var slot = _slots[slotIndex];

                        if (slot.State == SlotState.Loaded)
                        {
                            hiddenIndex = slotIndex;
                            break;
                        }

                        // The user scrolled faster than loading; wait for the hidden slot
                        await UniTask.WaitUntil(() => slot.State != SlotState.Pending, cancellationToken: ct);

                        if (ct.IsCancellationRequested)
                        {
                            return;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                finally
                {
                    _isWaitingForSlot = false;
                }
            }

            _currentCharacterIndex = WrapCharacterIndex(_currentCharacterIndex + step);
            var nextPrimary = clockwise ? _podiums[0].CharacterInfo : _podiums[2].CharacterInfo;

            var hidden = _podiums[3];

            // Activate before assigning the character, otherwise the Animator trigger is dropped
            hidden.gameObject.SetActive(true);

            if (hiddenIndex >= 0)
            {
                var targetInfo = _slots[hiddenIndex].Info;
                if (hidden.CharacterInfo != targetInfo)
                {
                    hidden.SetCharacter(targetInfo);
                }
            }

            Rotate(clockwise, nextPrimary, () =>
            {
                OnRotateComplete(clockwise);
            });
        }

        private void OnRotateComplete(bool clockwise)
        {
            var rotated = new Podium[_podiums.Length];

            for (var i = 0; i < _podiums.Length; i++)
            {
                var newIndex = clockwise
                    ? WrapPodiumIndex(i + 1)
                    : WrapPodiumIndex(i - 1);

                rotated[newIndex] = _podiums[i];
            }

            for (var i = 0; i < _podiums.Length; i++)
            {
                _podiums[i] = rotated[i];
            }

            _podiums[3].gameObject.SetActive(false);

            _currentAngle = 0f;
            UpdatePodiumPositions(_currentAngle);
            UpdatePrimaryCharacter();
        }

        private int WrapPodiumIndex(int index)
        {
            return (index + _podiums.Length) % _podiums.Length;
        }

        private void Exit()
        {
            // Pop navigation scheme and go back to the menu scene
            Navigator.Instance.PopScheme();
            GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
        }

        private async UniTask LoadAllCharacters(LoadingContext context, CancellationToken ct)
        {
            AsyncOperationHandle<IList<IResourceLocation>> locationsHandle = default;

            try
            {
                // Enumerate local files
                var folder = Path.Combine(CustomContentManager.CustomizationDirectory, "characters");
                string[] files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.yargchar") : Array.Empty<string>();
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);

                // Ask addressables if there are any remote characters
                locationsHandle = Addressables.LoadResourceLocationsAsync("character");
                await locationsHandle.Task;
                if (ct.IsCancellationRequested) return;

                var locations = new List<IResourceLocation>();
                if (locationsHandle.Status == AsyncOperationStatus.Succeeded && locationsHandle.Result != null)
                {
                    locations.AddRange(locationsHandle.Result);
                    locations.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.PrimaryKey, b.PrimaryKey));
                }

                var totalCount = files.Length + locations.Count;

                var slots = new CharacterSlot[totalCount];
                _slots.Clear();
                for (int i = 0; i < totalCount; i++)
                {
                    slots[i] = new CharacterSlot();
                    _slots.Add(slots[i]);
                }

                var loadedCount = 0;

                async UniTask LoadSlotAsync(CharacterSlot slot, int index)
                {
                    var loadTask = index < files.Length
                        ? LoadLocalCharacter(files[index], ct)
                        : LoadRemoteCharacter(locations[index - files.Length], ct);

                    CharacterInfo info;
                    try
                    {
                        info = await loadTask;
                    }
                    catch (Exception e)
                    {
                        YargLogger.LogError($"Error loading character: {e.Message}");
                        info = null;
                    }

                    if (ct.IsCancellationRequested)
                    {
                        ReleaseCharacterInfo(info);
                        return;
                    }

                    if (info == null)
                    {
                        slot.State = SlotState.Failed;

                        var removedIndex = _slots.IndexOf(slot);
                        if (removedIndex >= 0)
                        {
                            _slots.RemoveAt(removedIndex);

                            if (removedIndex < _currentCharacterIndex)
                            {
                                _currentCharacterIndex--;
                            }
                        }
                    }
                    else
                    {
                        slot.Info = info;
                        slot.State = SlotState.Loaded;
                    }

                    int current = Interlocked.Increment(ref loadedCount);
                    if (!_loadingOverlayDismissed && !_loadingFinished)
                    {
                        context.SetSubText($"Loading characters ({current}/{totalCount})");
                    }
                }

                var tasks = new List<UniTask>(totalCount);

                if (totalCount > 0)
                {
                    tasks.Add(LoadSlotAsync(slots[0], 0));
                }

                // This ensures that async calls are dispatched in an order that doesn't cause unnecessary waiting for
                // the initially visible set of characters
                for (int left = 1, right = totalCount - 1; left <= right; left++, right--)
                {
                    tasks.Add(LoadSlotAsync(slots[left], left));
                    if (right != left)
                    {
                        tasks.Add(LoadSlotAsync(slots[right], right));
                    }
                }

                await UniTask.WhenAll(tasks);
            }
            catch (Exception e)
            {
                YargLogger.LogError($"Error loading characters: {e.Message}");
            }
            finally
            {
                _loadingFinished = true;

                if (locationsHandle.IsValid())
                {
                    Addressables.Release(locationsHandle);
                }
            }
        }

        private async UniTask<CharacterInfo> LoadLocalCharacter(string file, CancellationToken ct)
        {
            var bundle = await AssetBundle.LoadFromFileAsync(file);
            if (bundle == null) return null;

            if (ct.IsCancellationRequested)
            {
                bundle.Unload(true);
                return null;
            }

            var request = bundle.LoadAssetAsync<GameObject>(BackgroundHelper.CHARACTER_PREFAB_PATH.ToLowerInvariant());
            var prefab = await request as GameObject;

            // Bail out before doing any shader work if we are shutting down or the prefab is missing
            if (ct.IsCancellationRequested || prefab == null)
            {
                bundle.Unload(true);
                return null;
            }

            BackgroundHelper.LoadMetalShaders(bundle, prefab, BackgroundHelper.ExportType.Character);

            var instance = Instantiate(prefab);
            instance.gameObject.SetActive(false);

            string name;
            string author;
            var vrmInstance = instance.GetComponent<Vrm10Instance>();

            if (vrmInstance != null && vrmInstance.Vrm != null && vrmInstance.Vrm.Meta != null)
            {
                name = string.IsNullOrWhiteSpace(vrmInstance.Vrm.Meta.Name)
                    ? Path.GetFileNameWithoutExtension(file)
                    : vrmInstance.Vrm.Meta.Name;
                var authors = vrmInstance.Vrm.Meta.Authors;
                author = authors.Count switch
                {
                    0 => Localize.Key("Menu.Content.CharacterSelect.UnspecifiedAuthor"),
                    1 => authors[0],
                    _ => string.Join(", ", authors)
                };
            }
            else
            {
                name = Path.GetFileNameWithoutExtension(file);
                author = Localize.Key("Menu.Content.CharacterSelect.UnspecifiedAuthor");
            }

            Destroy(instance);
            bundle.Unload(false);

            return new CharacterInfo
            {
                Prefab = prefab,
                Identifier = file,
                Name = name,
                Author = author,
                IsAddressable = false
            };
        }

        private async UniTask<CharacterInfo> LoadRemoteCharacter(IResourceLocation result, CancellationToken ct)
        {
            var key = result.PrimaryKey;
            var handle = Addressables.LoadAssetAsync<GameObject>(result);
            var prefab = await handle.Task;

            if (ct.IsCancellationRequested || handle.Status != AsyncOperationStatus.Succeeded)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                return null;
            }

            GameObject instance = null;

            // The handle only transfers ownership to the returned CharacterInfo on success
            var handleTransferred = false;

            try
            {
                instance = await Addressables.InstantiateAsync(key);
                if (instance == null)
                {
                    YargLogger.LogError($"Error loading character: failed to instantiate '{key}'");
                    return null;
                }

                instance.gameObject.SetActive(false);

                string name;
                string author;

                var vrmInstance = instance.GetComponent<Vrm10Instance>();
                if (vrmInstance != null && vrmInstance.Vrm != null && vrmInstance.Vrm.Meta != null)
                {
                    name = vrmInstance.Vrm.Meta.Name;
                    var authors = vrmInstance.Vrm.Meta.Authors;

                    author = authors.Count switch
                    {
                        0 => "",
                        1 => authors[0],
                        _ => string.Join(", ", authors)
                    };
                }
                else
                {
                    // Name fallback is the last component
                    var index = key.LastIndexOf('/');
                    name = key[(index + 1)..];
                    author = string.Empty;
                }

                if (ct.IsCancellationRequested)
                {
                    return null;
                }

                var info = new CharacterInfo {
                    Prefab = prefab,
                    Identifier = key,
                    Name = name,
                    Author = author,
                    IsAddressable = true,
                    Handle = handle
                };

                handleTransferred = true;
                return info;
            }
            catch (Exception e)
            {
                YargLogger.LogError($"Error loading character: {e.Message}");
                return null;
            }
            finally
            {
                if (instance != null)
                {
                    Addressables.ReleaseInstance(instance);
                }

                if (!handleTransferred && handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        private static void ReleaseCharacterInfo(CharacterInfo info)
        {
            if (info == null)
            {
                return;
            }

            if (info.IsAddressable && info.Handle.IsValid())
            {
                Addressables.Release(info.Handle);
            }

            info.Prefab = null;
        }

        private void Rotate(bool clockwise, CharacterInfo nextPrimary, Action onRotationComplete)
        {
            if (_rotationTween != null && _rotationTween.IsPlaying())
            {
                _rotationTween.Complete();
            }

            var textHidden = false;
            var textUpdated = false;

            float target = _currentAngle + (clockwise ? 90f : -90f);

            _rotationTween = DOTween.To(() => _currentAngle, x => _currentAngle = x, target, MOVE_DURATION)
                .SetEase(Ease.InOutCubic)
                .OnUpdate(() =>
                {
                    UpdatePodiumPositions(_currentAngle);

                    var progress = _rotationTween.ElapsedPercentage();
                    if (_slots.Count > 0)
                    {
                        if (!textHidden && progress > 0.2f)
                        {
                            _characterName.SetText(string.Empty);
                            _characterCredits.SetText(string.Empty);
                            _spotlight.DOIntensity(0f, MOVE_DURATION * 0.333f)
                                .OnComplete(() => _spotlight.color = Color.white);
                            textHidden = true;
                        }

                        if (!textUpdated && progress > 0.8f)
                        {
                            if (nextPrimary != null)
                            {
                                _characterName.SetText(nextPrimary.Name);
                                _characterCredits.SetText(nextPrimary.Author);
                                if (IsSelected(nextPrimary))
                                {
                                    _spotlight.color = Color.gold;
                                }
                                else if (IsHidden(nextPrimary))
                                {
                                    _spotlight.color = Color.darkRed;
                                }
                                _spotlight.DOIntensity(SPOTLIGHT_INTENSITY, MOVE_DURATION * 0.333f);
                            }

                            textUpdated = true;
                        }
                    }
                })
                .OnComplete(() =>
                {
                    onRotationComplete?.Invoke();
                    _rotationTween = null;
                });
        }

        private void UpdatePodiumPositions(float angle)
        {
            var radius = Mathf.Abs(_leftLocation.position.x);
            for (int i = 0; i < _podiums.Length; i++)
            {
                if (_podiums[i] == null)
                {
                    continue;
                }

                var baseAngle = i * (360f / _podiums.Length);
                var totalRadians = (baseAngle + angle) * Mathf.Deg2Rad;

                var x = Mathf.Cos(totalRadians) * radius;
                var z = (Mathf.Sin(totalRadians) * radius) - radius;

                _podiums[i].transform.position = new Vector3(x, _podiums[i].transform.position.y, z);
            }
        }

        public static bool IsSelected(CharacterInfo characterInfo)
        {
            return SettingsManager.Settings.CustomCharacters.TryGetValue(VenueCharacter.CharacterType.Vocals, out var customInfo)
                && customInfo.Identifier == characterInfo.Identifier;
        }

        public static bool IsHidden(CharacterInfo characterInfo)
        {
            var customInfo = new CustomCharacterInfo
            {
                Source = characterInfo.IsAddressable ? CustomCharacterSource.Addressable : CustomCharacterSource.File,
                Identifier = characterInfo.Identifier
            };

            return SettingsManager.Settings.HiddenCharacters.Contains(customInfo);
        }

        private void OnDestroy()
        {
            _rotationTween?.Kill();

            foreach (var slot in _slots)
            {
                ReleaseCharacterInfo(slot.Info);
            }

            foreach (var podium in _podiums)
            {
                if (podium != null)
                {
                    Destroy(podium.gameObject);
                }
            }

            Resources.UnloadUnusedAssets();
        }
    }
}
