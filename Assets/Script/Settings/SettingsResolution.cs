using System.Collections.Generic;
using UnityEngine;
using YARG.PlayMode;
using YARG.Serialization;
using YARG.Settings.Types;
using YARG.UI;
using YARG.Util;
using TMPro;

namespace YARG.Settings {
    public class SettingsResolution : MonoBehaviour {
        public TMP_Dropdown resolutionDropdown;
        Resolution[] resolutions;

        void Start() {
            resolutions = Screen.resolutions;
            resolutionDropdown.ClearOptions();
            List<string> options = new List<string>();
            int currentResolutionIndex = 0;

            // Filter out duplicate resolutions
            HashSet<Resolution> uniqueResolutions = new HashSet<Resolution>(new ResolutionComparer());
            foreach (Resolution resolution in resolutions) {
                uniqueResolutions.Add(resolution);
            }

            List<Resolution> sortedResolutions = new List<Resolution>(uniqueResolutions);

            for (int i = 0; i < sortedResolutions.Count; i++) {
                string option = sortedResolutions[i].width + "x" + sortedResolutions[i].height;
                options.Add(option);

                if (sortedResolutions[i].width == Screen.width && sortedResolutions[i].height == Screen.height) {
                    currentResolutionIndex = i;
                }
            }

            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = currentResolutionIndex;
            resolutionDropdown.RefreshShownValue();

            resolutionDropdown.onValueChanged.AddListener(ChangeResolution);
        }

        void ChangeResolution(int resolutionIndex) {
            Resolution selectedResolution = new List<Resolution>(new HashSet<Resolution>(resolutions, new ResolutionComparer()))[resolutionIndex];
            Screen.SetResolution(selectedResolution.width, selectedResolution.height, Screen.fullScreen);
        }

        public void goBack() {
            GameManager.Instance.LoadScene(SceneIndex.MENU);
        }
    }

        // Custom comparer to compare resolutions based on width and height
        public class ResolutionComparer : IEqualityComparer<Resolution> {
            public bool Equals(Resolution x, Resolution y) {
                return x.width == y.width && x.height == y.height;
            }

            public int GetHashCode(Resolution resolution) {
                return resolution.width.GetHashCode() ^ resolution.height.GetHashCode();
            }
    }
}