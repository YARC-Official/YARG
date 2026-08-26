using UnityEngine;
using YARG.Menu.ListMenu;

namespace YARG.Menu.Career
{
    public class SongView : ViewObject<SongViewType>
    {
        [SerializeField]
        private GameObject _songViewContainer;

        [Space]
        [SerializeField]
        private GameObject _secondaryTextContainer;
        [SerializeField]
        private GameObject _asMadeFamousByTextContainer;

    }
}