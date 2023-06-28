using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

namespace YARG.UI
{
    public class CreditCard : MonoBehaviour
    {
        // TODO: Add Steam, and SoundCloud

        private static readonly Dictionary<string, string> URLTable = new()
        {
            {
                "twitter", "https://twitter.com/"
            },
            {
                "twitch", "https://www.twitch.tv/"
            },
            {
                "github", "https://github.com/"
            }
        };

        [Serializable]
        private struct SocialInfo
        {
            // We would be using an enum, but Unity can't do those in the button's inspector
            public string Social;
            public GameObject Button;
        }

        [SerializeField]
        private TextMeshProUGUI _headerText;

        [SerializeField]
        private List<SocialInfo> _socialInfos;

        private JObject _info;

        public void SetFromJObject(string name, JObject info)
        {
            _info = info;
            _headerText.text = name;

            // Add the roles
            if (info.TryGetValue("roles", out var token) && token.Type == JTokenType.Array)
            {
                foreach (var role in token.ToObject<string[]>())
                {
                    _headerText.text += $"<sprite name={role}>";
                }
            }

            // Add the social info
            foreach (var socialInfo in _socialInfos)
            {
                if (!info.ContainsKey(socialInfo.Social))
                {
                    socialInfo.Button.SetActive(false);
                }
            }
        }

        public void OpenSocial(string social)
        {
            var arg = _info[social]?.ToString();
            if (arg == null)
            {
                return;
            }

            string url;
            switch (social)
            {
                case "discord":
                case "email":
                    GUIUtility.systemCopyBuffer = arg;
                    return;
                case "website":
                    url = arg;
                    break;
                case "videoService":
                    // Assume YouTube (for now)
                    if (arg.StartsWith("@"))
                    {
                        url = $"https://www.youtube.com/{arg}";
                    }
                    else
                    {
                        url = $"https://www.youtube.com/channel/{arg}";
                    }

                    break;
                default:
                    if (URLTable.TryGetValue(social, out var urlStart))
                    {
                        url = urlStart + arg;
                    }
                    else
                    {
                        throw new Exception("Unreachable");
                    }

                    break;
            }

            Application.OpenURL(url);
        }
    }
}