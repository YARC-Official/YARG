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

        private List<CharacterInfo> _characters;
        private int _currentCharacterIndex = 0;

        private bool _loadingFinished = false;

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

            _characters = new List<CharacterInfo>();
            var loading = new LoadingContext();
            loading.SetLoadingText("Loading characters...");

            // Kick off background loading and wait for a minimum of 5 (or all if total < 5)
            var cts = this.GetCancellationTokenOnDestroy();
            _ = LoadAllCharacters(loading, cts);
            // We preload 5 in an effort to avoid weirdness when loading is slow
            await UniTask.WaitUntil(() => _characters.Count >= 5 || _loadingFinished, cancellationToken: cts);
            loading.Dispose();

            // Set a navigation scheme
            if (_characters.Count > 0)
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
                    new NavigationScheme.Entry(MenuAction.Left, "Menu.Common.Scroll", Left),
                    new NavigationScheme.Entry(MenuAction.Right, "Menu.Common.Scroll", Right),
                }, true));
                _characterName.SetText(Localize.Key("Menu.Content.CharacterSelect.NoCharacters"));
                _characterCredits.SetText(Localize.Key("Menu.Content.CharacterSelect.NoCharactersSubtext"));
            }

            // Instantiate some CharacterDisplays,
            // index 0 being _currentCharacterIndex - 1 (wrapping if necessary)
            // index 1 being _currentCharacterIndex
            // index 2 being _currentCharacterIndex + 1 (wrapping if necessary)
            // index 3 being _currentCharacterIndex + 2 (wrapping if necessary) (starts disabled)
            var offset = -1;
            var assignedCharacters = new HashSet<CharacterInfo>();
            for (int i = 0; i < _podiums.Length; i++)
            {
                var characterInfo = GetUniqueCharacterInfo(offset, assignedCharacters);
                offset++;

                var displayLocation = GetDisplayLocation(i);
                var instance = Instantiate(_displayPrefab, displayLocation);
                var characterDisplay = instance.GetComponent<Podium>();

                if (displayLocation == _hiddenLocation)
                {
                    instance.gameObject.SetActive(false);
                }
                else
                {
                    characterDisplay.SetCharacter(characterInfo);
                }

                _podiums[i] = characterDisplay;
            }

            UpdatePrimaryCharacter();
        }

        private void UpdateNavigationScheme()
        {
            if (_characters.Count == 0)
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
                3 => _hiddenLocation
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
            var index = WrapCharacterIndex(_currentCharacterIndex + offset);
            if (_characters.Count == 0 || index >= _characters.Count)
            {
                return null;
            }

            return _characters[index];
        }

        private int WrapCharacterIndex(int index)
        {
            if (_characters.Count == 0)
            {
                return 0;
            }

            return (index + _characters.Count) % _characters.Count;
        }

        private void UpdatePrimaryCharacter()
        {
            var selectedPodium = _podiums[1];

            if (selectedPodium != null)
            {
                // This is here instead of combined with the above to prevent the text from getting overwritten
                if (selectedPodium.CharacterInfo == null)
                {
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
            }
            else
            {
                _primaryCharacter = null;
                _characterName.SetText(string.Empty);
                _characterCredits.SetText(string.Empty);
            }

            UpdateNavigationScheme();
        }

        private void Select()
        {
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
            SettingsManager.Settings.CustomCharacters.Remove(VenueCharacter.CharacterType.Vocals);

            _spotlight.DOColor(Color.white, MOVE_DURATION * 0.333f);
            _podiums[1].SetLightColor(Color.white, MOVE_DURATION * 0.333f);

            UpdateNavigationScheme();
        }

        private void Hide()
        {
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
            if (_rotationTween != null && _rotationTween.IsPlaying())
            {
                return;
            }

            _currentCharacterIndex = WrapCharacterIndex(_currentCharacterIndex - 1);

            var oldRight = _podiums[2];
            var hidden = _podiums[3];

            if (_characters.Count > _podiums.Length)
            {
                hidden.SetCharacter(GetCharacterInfo(-1));
            }

            hidden.gameObject.SetActive(true);

            Rotate(false, oldRight.CharacterInfo, () =>
            {
                OnRotateComplete(false);
            });
        }

        private void Left()
        {
            if (_rotationTween != null && _rotationTween.IsPlaying())
            {
                return;
            }

            _currentCharacterIndex = WrapCharacterIndex(_currentCharacterIndex + 1);

            var oldLeft = _podiums[0];
            var hidden = _podiums[3];

            if (_characters.Count > _podiums.Length)
            {
                hidden.SetCharacter(GetCharacterInfo(1));
            }

            hidden.gameObject.SetActive(true);

            Rotate(true, oldLeft.CharacterInfo, () =>
            {
                OnRotateComplete(true);
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
            try
            {
                // Enumerate local files
                var folder = Path.Combine(CustomContentManager.CustomizationDirectory, "characters");
                string[] files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.yargchar") : Array.Empty<string>();

                // Ask addressables if there are any remote characters
                var locationsHandle = Addressables.LoadResourceLocationsAsync("character");
                await locationsHandle.Task;
                var locations = locationsHandle.Status == AsyncOperationStatus.Succeeded ? locationsHandle.Result : null;

                var totalFiles = files.Length;
                var totalRemote = locations?.Count ?? 0;
                var totalCount = totalFiles + totalRemote;

                var loadedCount = 0;
                var tasks = new List<UniTask>();

                async UniTask LoadingTask(UniTask<CharacterInfo> loadTask)
                {
                    var info = await loadTask;
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    if (info != null)
                    {
                        _characters.Add(info);
                    }

                    int current = Interlocked.Increment(ref loadedCount);
                    if (!_loadingFinished)
                    {
                        context.SetSubText($"Loading characters ({current}/{totalCount})");
                    }
                }

                // Kick off all loads concurrently
                foreach (var file in files)
                {
                    tasks.Add(LoadingTask(LoadLocalCharacter(file)));
                }

                if (locations != null)
                {
                    foreach (var location in locations)
                    {
                        tasks.Add(LoadingTask(LoadRemoteCharacter(location)));
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
            }
        }

        private async UniTask<CharacterInfo> LoadLocalCharacter(string file)
        {
            var bundle = await AssetBundle.LoadFromFileAsync(file);
            if (bundle == null) return null;

            var request = bundle.LoadAssetAsync<GameObject>(BackgroundHelper.CHARACTER_PREFAB_PATH.ToLowerInvariant());
            var prefab = await request as GameObject;

            if (prefab == null)
            {
                bundle.Unload(true);
                return null;
            }

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

        private async UniTask<CharacterInfo> LoadRemoteCharacter(IResourceLocation result)
        {
            var key = result.PrimaryKey;
            var handle = Addressables.LoadAssetAsync<GameObject>(result);
            var prefab = await handle.Task;
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                return null;
            }

            var instance = await Addressables.InstantiateAsync(key);
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

            Addressables.ReleaseInstance(instance);

            return new CharacterInfo {
                Prefab = prefab,
                Identifier = key,
                Name = name,
                Author = author,
                IsAddressable = true,
                Handle = handle
            };
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
                    if (_characters.Count > 0)
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
            foreach (var character in _characters)
            {
                if (character.IsAddressable && character.Handle.IsValid())
                {
                    Addressables.Release(character.Handle);
                }

                character.Prefab = null;
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
