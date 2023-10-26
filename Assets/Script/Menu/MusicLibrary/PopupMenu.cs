using TMPro;
using UnityEngine;
using UnityEngine.Events;
using YARG.Core.Extensions;
using YARG.Core.Input;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.Navigation;

namespace YARG.Menu.MusicLibrary
{
    public class PopupMenu : MonoBehaviour
    {
        private enum State
        {
            Main,
            SortSelect
        }

        [SerializeField]
        private PopupMenuItem _menuItemPrefab;

        [Space]
        [SerializeField]
        private GameObject _header;
        [SerializeField]
        private TextMeshProUGUI _headerText;
        [SerializeField]
        private MusicLibraryMenu _musicLibrary;
        [SerializeField]
        private Transform _container;
        [SerializeField]
        private NavigationGroup _navGroup;

        private State _menuState;

        private void OnEnable()
        {
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
                NavigationScheme.Entry.NavigateSelect,
                new NavigationScheme.Entry(MenuAction.Red, "Back", () =>
                {
                    if (_menuState == State.Main)
                    {
                        gameObject.SetActive(false);
                    }
                    else
                    {
                        _menuState = State.Main;
                        UpdateForState();
                    }
                })
            }, false));

            _menuState = State.Main;
            UpdateForState();
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();
        }

        private void UpdateForState()
        {
            // Reset content
            _navGroup.ClearNavigatables();
            _container.DestroyChildren();

            // Create the menu
            switch (_menuState)
            {
                case State.Main:
                    CreateMainMenu();
                    break;
                case State.SortSelect:
                    CreateSortSelect();
                    break;
            }

            _navGroup.SelectFirst();
        }

        private void CreateMainMenu()
        {
            SetHeader(null);

            CreateItem("Sort By: " + MusicLibraryMenu.Sort.ToLocalizedName(), () =>
            {
                _menuState = State.SortSelect;
                UpdateForState();
            });
        }

        private void CreateSortSelect()
        {
            SetHeader("Order By...");

            foreach (var sort in EnumExtensions<SongAttribute>.Values)
            {
                // Skip theses because they don't make sense
                if (sort == SongAttribute.Unspecified) continue;
                if (sort == SongAttribute.Instrument) continue;

                // Create an item for it
                CreateItem(sort.ToLocalizedName(), () =>
                {
                    _musicLibrary.ChangeSort(sort);
                    gameObject.SetActive(false);
                });
            }
        }

        private void SetHeader(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                _header.SetActive(false);
            }
            else
            {
                _header.SetActive(true);
                _headerText.text = text;
            }
        }

        private void CreateItem(string body, UnityAction a)
        {
            var btn = Instantiate(_menuItemPrefab, _container);
            btn.Initialize(body, a);
            _navGroup.AddNavigatable(btn.Button);
        }
    }
}