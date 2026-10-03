using System;
using System.Collections.Generic;
using YARG.Core.Game;
using YARG.Input.Bindings;
using YARG.Player;
using System.Linq;

namespace YARG.Helpers
{
    public static class BindingSetHelper
    {
        public static (List<YargProfile> allUsers, List<YargProfile> activeUsers) GetUsersOfBindingSet(ReusableBindingSet bindingSet)
        {
            var allUsers = new List<YargProfile>();
            var activeUsers = new List<YargProfile>();


            foreach (var deviceInfo in BindingsContainer.AllPlayerDeviceInfo)
            {
                // Check preferred binding sets (one per (ControllerFamily,GameMode) tuple)
                var profileBindings = deviceInfo.AllPreferredBindingSets;
                if (profileBindings.Contains(bindingSet))
                {
                    allUsers.Add(deviceInfo.Profile);

                    if (PlayerContainer.IsProfileTaken(deviceInfo.Profile))
                    {
                        activeUsers.Add(deviceInfo.Profile);
                    }

                    continue;
                }

                // If a connected player has multiple controllers of the same family at the same
                // time, then some of them might be using other binding sets besides that player's
                // general (ControllerFamily,GameMode)-wide preference
                if (deviceInfo.BindingSetsInUse.Contains(bindingSet))
                {
                    allUsers.Add(deviceInfo.Profile);

                    // Disconnected profiles will always have an empty BindingSetsInUse, so no need
                    // to check IsProfileTaken
                    activeUsers.Add(deviceInfo.Profile); ;
                }
            }

            return (allUsers, activeUsers);
        }
    }
}
