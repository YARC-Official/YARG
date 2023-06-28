using System;
using UnityEngine;
using YARG.Input;
using YARG.PlayMode;
using YARG.Settings;

namespace YARG.UI
{
    public class PauseMenu : MonoBehaviour
    {
        private enum ButtonIndex
        {
            RESUME = 0,
            RESTART,
            SETTINGS,
            QUIT
        }

        [SerializeField]
        private GenericOption[] options;

        private int playerIndex;

        private int optionCount;
        private int selected;

        private void Start()
        {
            foreach (var option in options)
            {
                option.MouseHoverEvent += HoverOption;
                option.MouseClickEvent += ClickOption;
            }

            UpdateText();
        }

        private void OnEnable()
        {
            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Up, "Up", () => { MoveOption(-1); }),
                new NavigationScheme.Entry(MenuAction.Down, "Down", () => { MoveOption(1); }),
                new NavigationScheme.Entry(MenuAction.Confirm, "Confirm", () => { SelectCurrentOption(); }),
                new NavigationScheme.Entry(MenuAction.Back, "Back", () => { OnResumeSelected(); })
            }, false));
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();

            SettingsMenu.Instance.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            foreach (var option in options)
            {
                option.MouseHoverEvent -= HoverOption;
                option.MouseClickEvent -= ClickOption;
            }
        }

        private void MoveOption(int i)
        {
            // Deselect old one
            options[selected].SetSelected(false);

            selected += i;

            if (selected < 0)
            {
                selected = optionCount - 1;
            }
            else if (selected >= optionCount)
            {
                selected = 0;
            }

            // Select new one
            options[selected].SetSelected(true);
        }

        private void HoverOption(GenericOption option)
        {
            // Deselect old one
            options[selected].SetSelected(false);

            selected = Array.IndexOf(options, option);

            // Slighty different than with the keyboard.
            // Don't need to bound the top. The bottom should stop and not roll over.
            if (selected >= optionCount)
            {
                selected = optionCount - 1;
            }

            // Select new one
            options[selected].SetSelected(true);
        }

        private void ClickOption(GenericOption option)
        {
            SelectCurrentOption();
        }

        public void SelectCurrentOption()
        {
            switch ((ButtonIndex) selected)
            {
                case ButtonIndex.RESUME:
                    OnResumeSelected();
                    break;
                case ButtonIndex.RESTART:
                    OnRestartSelected();
                    break;
                case ButtonIndex.SETTINGS:
                    OnSettingsSelected();
                    break;
                case ButtonIndex.QUIT:
                    OnQuitSelected();
                    break;
                default:
                    Debug.LogError($"Unhandled option index {selected}!");
                    break;
            }
        }

        private void UpdateText()
        {
            // Add to options
            string[] ops =
            {
                "Resume", "Restart", "Settings", "Quit",
            };
            optionCount = ops.Length - 0;

            // Set text and sprites
            for (int i = 0; i < ops.Length; i++)
            {
                options[i].SetText(ops[i]);
                options[i].SetSelected(false);
            }

            // Select
            selected = 0;
            options[0].SetSelected(true);
        }

        private void OnResumeSelected()
        {
            Play.Instance.Paused = false;
        }

        private void OnRestartSelected()
        {
            GameManager.AudioManager.UnloadSong();
            GameManager.Instance.LoadScene(SceneIndex.PLAY);
            Play.Instance.Paused = false;
        }

        private void OnSettingsSelected()
        {
            SettingsMenu.Instance.gameObject.SetActive(true);
        }

        private void OnQuitSelected()
        {
            Play.Instance.Exit();
        }
    }
}