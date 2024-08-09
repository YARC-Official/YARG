using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Player;

namespace YARG.Menu.Persistent
{
    public class ActivePlayerList : MonoBehaviour
    {
        [SerializeField]
        private GameObject _noPlayersContainer;
        [SerializeField]
        private GameObject _playerNamesContainer;
        [SerializeField]
        private GameObject _playerNamesPrefab;

        [SerializeField]
        private int _maxShownPlayerNames = 3;

        public void UpdatePlayerList(IReadOnlyCollection<YargPlayer> players)
        {
            // Only show this message if there are no players, including bots.
            _noPlayersContainer.SetActive(players.Count == 0);

            players = players.Where(e => !e.Profile.IsBot).ToList();
            var showPlayerNames = players.Count <= _maxShownPlayerNames;

            foreach (Transform child in _playerNamesContainer.transform)
            {
                Destroy(child.gameObject);
            }

            foreach (var player in players)
            {
                var newObj = Instantiate(_playerNamesPrefab, _playerNamesContainer.transform);
                var itemComponent = newObj.GetComponent<ActivePlayerListItem>();
                itemComponent.Initialize(player.Profile);
                itemComponent.ShowName = showPlayerNames;
            }
        }
    }
}
