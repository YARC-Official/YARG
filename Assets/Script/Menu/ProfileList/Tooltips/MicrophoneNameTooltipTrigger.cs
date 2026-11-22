using System.Collections.Generic;
using UnityEngine;
using YARG.Menu.Tooltips;

namespace YARG.Menu.ProfileList
{
    public class MicrophoneNameTooltipTrigger : TooltipTrigger
    {
        [SerializeField]
        private MicrophoneEntryView _entry;

        protected override (IReadOnlyList<string> titleParams, IReadOnlyList<string> textParams) GetParameters()
        {
            return (
                new List<string>() { _entry.Microphone.DisplayName },
                new List<string>() { }
            );
        }
    }
}
