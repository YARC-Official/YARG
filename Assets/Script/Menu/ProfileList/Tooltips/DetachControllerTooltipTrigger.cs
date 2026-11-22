using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using YARG.Menu.Tooltips;

namespace YARG.Menu.ProfileList
{
    public class DetachControllerTooltipTrigger : TooltipTrigger
    {
        [SerializeField]
        private ControllerEntryView _entry;

        protected override (IReadOnlyList<string> titleParams, IReadOnlyList<string> textParams) GetParameters()
        {
            return (
                new List<string>() { },
                new List<string>() { _entry.Controller.displayName }
            );
        }
    }
}
