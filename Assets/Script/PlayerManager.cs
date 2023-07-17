using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YARG.Core;
using YARG.Data;
using YARG.PlayMode;
using YARG.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Player.Input;

namespace YARG
{
    public static class PlayerManager
    {
        public struct LastScore
        {
            public DiffPercent percentage;
            public DiffScore score;
            public int notesHit;
            public int notesMissed;
            public int maxCombo;
        }

        public class Player
        {
            private static int _nextPlayerName = 1;

            public string name;
            public string DisplayName => name;

            public InputStrategy inputStrategy;

            public float trackSpeed = 5f;
            public bool leftyFlip = false;

            public bool brutalMode = false;

            public Instrument? chosenInstrument = Instrument.FiveFretGuitar;
            public Difficulty chosenDifficulty = Difficulty.Expert;

            public LastScore? lastScore = null;
            // public AbstractTrack track = null;

            public Player()
            {
                name = $"New Player {_nextPlayerName++}";
            }

            public void TryPickRandomName()
            {
                // Skip if it is not a bot
                // if (!inputStrategy?.BotMode ?? true)
                // {
                //     return;
                // }

                var shuffledNames = new List<string>(RandomPlayerNames);
                shuffledNames.Shuffle();

                // Do not use the same name twice, if no available names, use "New Player"
                foreach (string t in shuffledNames)
                {
                    name = t;

                    // If no player has this name, finish!
                    if (players.All(i => i.name != name))
                    {
                        break;
                    }
                }

                // Make the name colored
                name = $"<color=yellow>{name}</color>";
            }
        }

        private static readonly List<string> RandomPlayerNames;
        public static List<Player> players = new();

        public static float AudioCalibration => SettingsManager.Settings.AudioCalibration.Data / 1000f;

        static PlayerManager()
        {
            // Load credits file
            var creditsPath = Addressables.LoadAssetAsync<TextAsset>("Credits").WaitForCompletion();

            // Read json
            var json = JsonConvert.DeserializeObject<
                Dictionary<string, Dictionary<string, JObject>>
            >(creditsPath.text);

            // Get names
            RandomPlayerNames = new List<string>();
            foreach (var (_, dict) in json)
            {
                foreach (var (name, _) in dict)
                {
                    RandomPlayerNames.Add(name);
                }
            }
        }

        public static int PlayersWithInstrument(Instrument instrument)
        {
            return players.Count(i => i.chosenInstrument == instrument);
        }
    }
}