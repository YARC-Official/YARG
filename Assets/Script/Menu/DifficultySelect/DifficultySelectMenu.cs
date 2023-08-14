using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Core;
using YARG.Core.Input;
using YARG.Data;
using YARG.Gameplay;
using YARG.Menu.Navigation;
using YARG.Player;
using YARG.PlayMode;

namespace YARG.Menu.DifficultySelect
{
    // TODO: This will be redone with the new difficulty menu

    public class DifficultySelectMenu : MonoBehaviour
    {
        private enum State
        {
            Instrument,
            Difficulty,
        }

        private static readonly List<Instrument> _expertPlusAllowedList = new()
        {
            Instrument.FourLaneDrums,
            Instrument.ProDrums,
            Instrument.FiveLaneDrums,
            Instrument.Vocals,
            Instrument.Harmony,
        };

        [SerializeField]
        private GenericOption[] _options;

        [SerializeField]
        private TextMeshProUGUI _header;

        [SerializeField]
        private TMP_InputField _speedInput;

        private int _playerIndex;
        private Instrument[] _instruments;
        private Difficulty[] _difficulties;
        private State _state;

        private int _optionCount;
        private int _selected;

        private YargPlayer CurrentPlayer => PlayerContainer.Players[_playerIndex];

        private void Start()
        {
            foreach (var option in _options)
            {
                option.MouseHoverEvent += HoverOption;
                option.MouseClickEvent += ClickOption;
            }
        }

        private void OnEnable()
        {
            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Up, "Up", () => MoveOption(-1)),
                new NavigationScheme.Entry(MenuAction.Down, "Down", () => MoveOption(1)),
                new NavigationScheme.Entry(MenuAction.Green, "Confirm", Next),
                new NavigationScheme.Entry(MenuAction.Red, "Back", () => MenuManager.Instance.PopMenu())
            }, false));

            _playerIndex = 0;

            // Get player info
            var allowedInstruments = CurrentPlayer.Profile.GameMode.PossibleInstruments();
            string headerText = CurrentPlayer.Profile.Name;

            UpdateInstrument(headerText, allowedInstruments);
        }

        private void OnDestroy()
        {
            Navigator.Instance.PopScheme();

            foreach (var option in _options)
            {
                option.MouseHoverEvent -= HoverOption;
                option.MouseClickEvent -= ClickOption;
            }
        }

        private void MoveOption(int i)
        {
            // Deselect old one
            _options[_selected].SetSelected(false);

            _selected += i;

            if (_selected < 0)
            {
                _selected = _optionCount - 1;
            }
            else if (_selected >= _optionCount)
            {
                _selected = 0;
            }

            // Select new one
            _options[_selected].SetSelected(true);
        }

        private void HoverOption(GenericOption option)
        {
            // Deselect old one
            _options[_selected].SetSelected(false);

            _selected = Array.IndexOf(_options, option);

            // Slightly different than with the keyboard.
            // Don't need to bound the top. The bottom should stop and not roll over or go to an empty option.
            if (_selected >= _optionCount)
            {
                _selected = _optionCount - 1;
            }

            // Select new one
            _options[_selected].SetSelected(true);
        }

        private void ClickOption(GenericOption option)
        {
            Next();
        }

        public void Next()
        {
            if (_state == State.Instrument)
            {
                var instrument = _instruments[_selected];
                CurrentPlayer.Profile.Instrument = instrument;

                bool showExpertPlus = _expertPlusAllowedList.Contains(instrument);
                UpdateDifficulty(instrument, showExpertPlus);
            }
            else if (_state == State.Difficulty)
            {
                CurrentPlayer.Profile.Difficulty = _difficulties[_selected];

                IncreasePlayerIndex();
            }
        }

        private void IncreasePlayerIndex()
        {
            _playerIndex++;

            if (_playerIndex >= PlayerContainer.Players.Count)
            {
                GlobalVariables.Instance.SongSpeed = float.Parse(_speedInput.text, CultureInfo.InvariantCulture);
                if (GlobalVariables.Instance.SongSpeed <= 0f)
                {
                    GlobalVariables.Instance.SongSpeed = 1f;
                }

                // Play song
                GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
            }
            else
            {
                UpdateInstrument(CurrentPlayer.Profile.Name, CurrentPlayer.Profile.GameMode.PossibleInstruments());
            }
        }

        private void UpdateInstrument(string headerText, Instrument[] allowedInstruments)
        {
            _state = State.Instrument;

            // Header
            _header.text = headerText;

            // Get allowed instruments
            var availableInstruments = GlobalVariables.Instance.CurrentSong.Parts.GetInstruments();

            // Force add pro drums and five lane
            if (availableInstruments.Contains(Instrument.FourLaneDrums))
            {
                availableInstruments.Add(Instrument.FiveLaneDrums);
            }
            else if (availableInstruments.Contains(Instrument.FiveLaneDrums))
            {
                availableInstruments.Add(Instrument.FourLaneDrums);
                availableInstruments.Add(Instrument.ProDrums);
            }

            // Filter out to only allowed instruments
            availableInstruments.RemoveAll(i => !allowedInstruments.Contains(i));

            _optionCount = availableInstruments.Count;

            // Add to options
            var ops = new string[availableInstruments.Count];
            _instruments = new Instrument[availableInstruments.Count];
            for (int i = 0; i < _instruments.Length; i++)
            {
                _instruments[i] = availableInstruments[i];
                ops[i] = availableInstruments[i].ToLocalizedName();
            }

            // Set text and sprites
            for (int i = 0; i < _options.Length; i++)
            {
                _options[i].SetSelected(false);

                if (i < ops.Length)
                {
                    _options[i].SetText(ops[i]);

                    if (i < _instruments.Length)
                    {
                        var sprite = Addressables.LoadAssetAsync<Sprite>($"FontSprites[{_instruments[i].ToResourceName()}]")
                            .WaitForCompletion();
                        _options[i].SetImage(sprite);
                    }
                }
                else
                    _options[i].SetText("");
            }

            // Select
            _selected = 0;
            _options[0].SetSelected(true);
        }

        private void UpdateDifficulty(Instrument instrument, bool showExpertPlus)
        {
            _state = State.Difficulty;

            // Get the correct instrument
            if (instrument == Instrument.ProDrums || instrument == Instrument.FiveLaneDrums)
            {
                instrument = Instrument.FourLaneDrums;
            }

            // Get the available difficulties
            var availableDifficulties = new List<Difficulty>();
            for (int i = 0; i < (int) Difficulty.ExpertPlus; i++)
            {
                if (!GlobalVariables.Instance.CurrentSong.Parts.HasPart(instrument, i))
                {
                    continue;
                }

                availableDifficulties.Add((Difficulty) i);
            }

            if (showExpertPlus)
            {
                availableDifficulties.Add(Difficulty.ExpertPlus);
            }

            _optionCount = availableDifficulties.Count;

            _difficulties = new Difficulty[_optionCount];
            var ops = new string[_optionCount];

            for (int i = 0; i < _optionCount; i++)
            {
                ops[i] = availableDifficulties[i].ToDisplayName();
                _difficulties[i] = availableDifficulties[i];
            }

            for (int i = 0; i < 6; i++)
            {
                _options[i].SetText("");
                _options[i].SetSelected(false);

                if (i < ops.Length)
                {
                    _options[i].SetText(ops[i]);
                }
            }

            _selected = _optionCount - 1;
            _options[_optionCount - 1].SetSelected(true);
        }
    }
}