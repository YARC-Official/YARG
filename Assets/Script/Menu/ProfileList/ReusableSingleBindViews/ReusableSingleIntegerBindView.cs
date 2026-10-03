using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Input.Bindings;
using YARG.Menu.ProfileList;

namespace YARG.Menu.ProfileList
{
    public class ReusableSingleIntegerBindView : ReusableSingleBindView<ReusableIntegerBinding, ReusableSingleIntegerBinding, int>
    {
        [SerializeField]
        private TMP_InputField _valueText;

        protected override void UpdateDummyInputVisuals(InputControl<int> dummyControl)
        {
            _valueText.text = dummyControl.value.ToString();
        }

        protected override void PopulateControlDropdown()
        {
            return;

            // No integer bindings currently exist, and the binding type only exists as a relic of
            // the early Pro Guitar support (where it was used for string fret values). Since Pro
            // Guitar didn't support custom bindings, the integer binding prefab never had a dropdown
            // or any configurability; just a value display. That means that, when this calls base,
            // it errors out because it expects to find a dropdown to populate.

            // Rather than go to the effort of creating, populating, and implementing the dropdown for a
            // binding type that doesn't actually exist, I'm leaving the following placeholder code as a
            // starting place for whoever comes along (quite possibly me) to implement some actual integer
            // binding, whether that's Pro Guitar frets, the RB guitar FX switch, or whatever else. If that's
            // you, reach out to me with any questions about the reusable bindings system.
            // -Frickitickitavi

            base.PopulateControlDropdown();

            foreach (var control in _allControls)
            {
                if (control.Layout is LayoutStrings.INTEGER)
                {
                    _dropdownControls.Add(new(control));
                    _controlDropdown.options.Add(new(control.DisplayName));
                }
            }
        }
    }
}
