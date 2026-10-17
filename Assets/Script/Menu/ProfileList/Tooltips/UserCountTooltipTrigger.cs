using System.Collections.Generic;
using UnityEngine;
using YARG.Localization;
using YARG.Menu.Tooltips;

namespace YARG.Menu.ProfileList
{
    public class UserCountTooltipTrigger : TooltipTrigger
    {
        [SerializeField]
        private BindingsCenterPaneSettingsPanel _settingsPanel;

        protected override (IReadOnlyList<string> titleParams, IReadOnlyList<string> textParams) GetParameters()
        {
            const string localizationKeyPrefix = "Menu.ProfileList.Tooltip.Bindings.Header.UserCount.";

            var (allUsers, activeUsers) = (_settingsPanel.AllUsers, _settingsPanel.ActiveUsers);

            var text = allUsers.Count switch
            {
                0 => Localize.Key($"{localizationKeyPrefix}Zero"),
                1 => Localize.KeyFormat($"{localizationKeyPrefix}One{(activeUsers.Count is 1 ? "Active" : "Inactive")}", allUsers[0].Name),
                _ => Localize.Key($"{localizationKeyPrefix}Multiple{(_settingsPanel.Locked ? "Locked" : "Unlocked")}")
            };

            return (
                new List<string>(),
                new List<string>() { text }
            );
        }
    }
}
