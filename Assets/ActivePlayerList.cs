using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Player;

namespace YARG
{
    public class ActivePlayerList : MonoBehaviour
    {
        public GameObject NoPlayersContainer;
        public GameObject PlayerNamesContainer;
        public GameObject PlayerNamesPrefab;

        public int MaxShownPlayerNames = 3;

        public void UpdatePlayerList(IReadOnlyCollection<YargPlayer> players)
        {
            // Only show this message if there are no players, including bots.
            NoPlayersContainer.SetActive(players.Count == 0);

            players = players.Where(e => !e.Profile.IsBot).ToList();
            var showPlayerNames = players.Count <= MaxShownPlayerNames;

            foreach (Transform child in PlayerNamesContainer.transform)
            {
                Destroy(child.gameObject);
            }

            foreach (var player in players)
            {
                var newObj = Instantiate(PlayerNamesPrefab, PlayerNamesContainer.transform);
                newObj.name = "Player: " + player.Profile.Name;
                var itemComponent = newObj.GetComponent<ActivePlayerListItem>();
                itemComponent.Profile = player.Profile;
                itemComponent.ShowName = showPlayerNames;
            }
        }
    }
}
