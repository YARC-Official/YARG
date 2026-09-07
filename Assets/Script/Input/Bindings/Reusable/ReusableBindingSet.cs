using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core;
using YARG.Helpers;
using YARG.Input.Serialization;
using YARG.Menu.ProfileList;

namespace YARG.Input
{
    public partial class ReusableBindingSet
    {
        public Guid Guid { get; private set; }
        public GameMode? Mode { get; } // null means menu bindings
        public ControllerFamily ControllerFamily { get; private set; }
        public string Name { get; private set; }
        public bool IsDefault { get; private set; }

        // Key is binding name, like "FiveFret.Green" or "FourDrums.RedPad"
        // These names come from BindingCollection.Templates.cs; they are YARG's, not PlasticBand's
        public Dictionary<string, SerializedReusableControlBinding> Bindings = new();

        public ReusableBindingSet(string name, GameMode? mode, ControllerFamily controllerFamily, bool isDefault = false) {
            Name = name;
            Mode = mode;
            ControllerFamily = controllerFamily;
            Guid = isDefault ? Guid.Empty : Guid.NewGuid();
            IsDefault = isDefault;
        }

        public ReusableBindingSet(SerializedReusableBindingSet serialized)
        {
            Name = serialized.Name;
            Guid = serialized.Guid;
            Mode = serialized.GameMode;
            ControllerFamily = LayoutHelper.LayoutStringToControllerFamily(serialized.BaseLayout);
            Bindings = serialized.Bindings;
        }



#nullable enable
        public SerializedReusableBindingSet? Serialize()
        {
            if (Bindings.Count < 1)
            {
                return null;
            }

            return new SerializedReusableBindingSet(Name, LayoutHelper.ControllerFamilyToLayoutString(ControllerFamily)) {
                Guid = Guid,
                Bindings = Bindings,
                GameMode = Mode,
                BaseLayout = LayoutHelper.ControllerFamilyToLayoutString(ControllerFamily)
            };
        }
    }
}
