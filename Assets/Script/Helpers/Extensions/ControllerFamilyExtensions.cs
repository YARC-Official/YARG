using System;
using System.Collections.Generic;
using System.Text;
using YARG.Menu.ProfileList;

namespace YARG.Helpers.Extensions
{
    public static class ControllerFamilyExtensions
    {
        public static bool HasLeftyNames(this ControllerFamily controllerFamily)
        {
            return controllerFamily is ControllerFamily.FiveFretGuitar or ControllerFamily.SixFretGuitar or ControllerFamily.FourLaneDrumkit;
        }
    }
}
