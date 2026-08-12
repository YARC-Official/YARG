using TMPro;
using UnityEngine;
using YARG.Core.Input;
using YARG.Menu.Navigation;

namespace YARG.Menu.Content
{
    public class ContentMenu : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _versionText;

        private void Start()
        {
            _versionText.text = GlobalVariables.Instance.CurrentVersion;
        }

        private void OnEnable()
        {
            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                NavigationScheme.Entry.NavigateSelect,
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", () => MenuManager.Instance.PopMenu())
            }, true));
        }

        public void CharacterSelect()
        {
            // DialogManager.Instance.ShowMessage("Not Implemented Yet", "Sorry, there isn't anything here yet");
            GlobalVariables.Instance.LoadScene(SceneIndex.Content);
        }

        public void Back()
        {
            MenuManager.Instance.PopMenu();
        }

        private void OnDisable()
        {
            Navigator.Instance?.PopScheme();
        }
    }
}