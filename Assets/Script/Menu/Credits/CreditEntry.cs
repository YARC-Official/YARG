using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using YARG.Localization;
using YARG.Menu.Credits;

namespace YARG.Menu
{
    public class CreditEntry : MonoBehaviour
    {
        private static readonly Dictionary<string, string> _urlTable = new()
        {
            {
                "Website", "{0}"
            },
            {
                "Twitter", "https://twitter.com/{0}"
            },
            {
                "Twitch", "https://www.twitch.tv/{0}"
            },
            {
                "Github", "https://github.com/{0}"
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
        private TextMeshProUGUI _nameText;
        [SerializeField]
        private List<SocialInfo> _socialInfos;
        [SerializeField]
        private TextMeshProUGUI _descriptionText;

        private CreditsMenu.Contributor _contributor;

        public void Initialize(CreditsMenu.Contributor contributor)
        {
            _contributor = contributor;

            _nameText.text = _contributor.Name;

            // Add the social info
            if (_contributor.Socials is not null)
            {
                foreach (var socialInfo in _socialInfos)
                {
                    if (!_contributor.Socials.ContainsKey(socialInfo.Social))
                    {
                        socialInfo.Button.SetActive(false);
                    }
                }
            }
            else
            {
                foreach (var socialInfo in _socialInfos)
                {
                    socialInfo.Button.SetActive(false);
                }
            }

            // Description text
            if (_contributor.Contributions is not null)
            {
                var repos = contributor.Contributions.Keys
                    .Select(i => Localize.Key("Menu.Credits.Repos", i));
                var roles = contributor.Contributions.Values
                    .SelectMany(i => i)
                    .Distinct()
                    .Select(i => Localize.Key("Menu.Credits.Roles", i));

                _descriptionText.text =
                    Localize.KeyFormat("Menu.Credits.Description",
                        Localize.List(repos), Localize.List(roles));
            }
        }

        public void OpenSocial(string social)
        {
            var arg = _contributor.Socials[social];
            if (arg == null)
            {
                return;
            }

            string url;
            switch (social)
            {
                case "Discord":
                    // These don't have urls, so just copy to the clipboard
                    GUIUtility.systemCopyBuffer = arg;
                    return;
                case "Email":
                    url = $"mailto:{arg}";
                    break;
                case "VideoService":
                    // The "VideoService" entry allows three options: URL, YouTube @, and YouTube ID
                    if (arg.StartsWith("http"))
                    {
                        url = arg;
                    }
                    else if (arg.StartsWith("@"))
                    {
                        url = $"https://www.youtube.com/{arg}";
                    }
                    else
                    {
                        url = $"https://www.youtube.com/channel/{arg}";
                    }

                    break;
                default:
                    if (_urlTable.TryGetValue(social, out var templateUrl))
                    {
                        url = string.Format(templateUrl, arg);
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