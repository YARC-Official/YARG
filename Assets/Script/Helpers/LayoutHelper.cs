using PlasticBand.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using YARG.Input.Bindings;
using YARG.Menu.ProfileList;
using static UnityEngine.InputSystem.Layouts.InputControlLayout;

namespace YARG.Helpers
{
    public static class LayoutHelper
    {
        private enum ControlInclusion
        {
            Introduces,
            Inherits,
            DoesNotHave
        }

        const string INPUT_DEVICE = nameof(InputDevice);

        public static InputControlLayout GetMostGeneralLayoutForControl(string layoutName, string controlName)
        {
            return GetMostGeneralLayoutForControl(InputSystem.LoadLayout(layoutName), controlName);
        }

        public static InputControlLayout GetMostGeneralLayoutForControl(InputDevice controller, string controlName)
        {
            return GetMostGeneralLayoutForControl(controller.layout, controlName);
        }

        public static InputControlLayout GetMostGeneralLayoutForControl(InputControlLayout controlSource, string controlName)
        {
            if (CheckControlInclusion(controlSource, controlName) is ControlInclusion.DoesNotHave)
            {
                throw new InvalidOperationException($"Layout {controlSource.name} does not have control {controlName}!");
            }

            var mostGeneralLayout = controlSource;

            while (CheckControlInclusion(mostGeneralLayout, controlName) is not ControlInclusion.Introduces)
            {
                mostGeneralLayout = GetParentLayout(mostGeneralLayout);
            }

            return mostGeneralLayout;
        }

        private static ControlInclusion CheckControlInclusion(InputControlLayout layout, string controlName)
        {
            foreach (var control in layout.controls)
            {
                if (control.name == controlName)
                {
                    return control.isFirstDefinedInThisLayout ? ControlInclusion.Introduces : ControlInclusion.Inherits;
                }
            }

            return ControlInclusion.DoesNotHave;
        }

        private static InputControlLayout GetParentLayout(InputControlLayout layout)
        {
            var parentName = InputSystem.GetNameOfBaseLayout(layout.name);
            return InputSystem.LoadLayout(parentName);
        }

        public static ControllerFamily LayoutStringToControllerFamily(string layout)
        {
            return layout switch
            {
                LayoutStrings.FIVE_FRET_GUITAR => ControllerFamily.FiveFretGuitar,
                LayoutStrings.SIX_FRET_GUITAR => ControllerFamily.SixFretGuitar,
                LayoutStrings.FOUR_LANE_DRUMKIT => ControllerFamily.FourLaneDrumkit,
                LayoutStrings.FIVE_LANE_DRUMKIT => ControllerFamily.FiveLaneDrumkit,
                LayoutStrings.PRO_KEYBOARD => ControllerFamily.ProKeyboard,
                LayoutStrings.PRO_GUITAR => ControllerFamily.ProGuitar,
                _ => ControllerFamily.Generic
            };
        }

        public static string ControllerFamilyToLayoutString(ControllerFamily controllerFamily)
        {
            return controllerFamily switch
            {
                ControllerFamily.FiveFretGuitar => LayoutStrings.FIVE_FRET_GUITAR,
                ControllerFamily.SixFretGuitar => LayoutStrings.SIX_FRET_GUITAR,
                ControllerFamily.FourLaneDrumkit => LayoutStrings.FOUR_LANE_DRUMKIT,
                ControllerFamily.FiveLaneDrumkit => LayoutStrings.FIVE_LANE_DRUMKIT,
                ControllerFamily.ProKeyboard => LayoutStrings.PRO_KEYBOARD,
                ControllerFamily.ProGuitar => LayoutStrings.PRO_GUITAR,
                _ => LayoutStrings.INPUT_DEVICE
            };
        }

        public static string? GetDisplayNameOfControlInLayout(InputControlLayout layout, string name)
        {
            foreach (var control in layout.controls)
            {
                if (control.name == name)
                {
                    return control.displayName;
                }
            }

            return null;
        }

        public static List<ControlItemInfo> GetAllControlsForControllerFamily(ControllerFamily family) {
            List<string> layoutTreeStrings = family switch {
                ControllerFamily.FiveFretGuitar => new() { nameof(FiveFretGuitar), nameof(RockBandGuitar), nameof(GuitarHeroGuitar), nameof(RiffmasterGuitar) },
                ControllerFamily.SixFretGuitar => new() { nameof(SixFretGuitar) },
                ControllerFamily.FourLaneDrumkit => new() { nameof(FourLaneDrumkit) },
                ControllerFamily.FiveLaneDrumkit => new() { nameof(FiveLaneDrumkit) },
                ControllerFamily.ProKeyboard => new() { nameof(ProKeyboard) },
                ControllerFamily.ProGuitar => new() { nameof(ProGuitar) },
                _ => throw new NotImplementedException() // TODO-FRICK
            };

            Dictionary<string, ControlItemInfo> controlsByPath = new();

            void AddControl(ControlItem controlItem, string sourceLayout, string parentPath = null, string parentLayout = null)
            {
                var controlPath = parentPath is null ? controlItem.name : $"{parentPath}/{controlItem.name}";

                if (controlItem.isModifyingExistingControl)
                {
                    if (controlsByPath.TryGetValue(controlPath, out var existingControl))
                    {
                        existingControl.ApplyOverride(controlItem);
                        controlsByPath[controlPath] = existingControl;
                    }
                    else
                    {
                        controlsByPath[controlPath] = new(
                            controlPath: controlPath,
                            parentPath: parentPath,
                            sourceLayout: sourceLayout,
                            parentLayout: parentLayout,
                            controlItem: controlItem,
                            hasChildren: false
                        );
                    }

                    return;
                }

                if (controlItem.layout.IsEmpty())
                {
                    controlsByPath[controlPath] = new(
                        controlPath: controlPath,
                        parentPath: parentPath,
                        sourceLayout: sourceLayout,
                        parentLayout: parentLayout,
                        controlItem: controlItem,
                        hasChildren: false
                    );
                    return;
                }

                var childLayout = InputSystem.LoadLayout(controlItem.layout);

                if (childLayout.controls.Count == 0)
                {
                    controlsByPath[controlPath] = new(
                        controlPath: controlPath,
                        parentPath: parentPath,
                        sourceLayout: sourceLayout,
                        parentLayout: parentLayout,
                        controlItem: controlItem,
                        hasChildren: false
                    );
                    return;
                }

                controlsByPath[controlPath] = new(
                    controlPath: controlPath,
                    parentPath: parentPath,
                    sourceLayout: sourceLayout,
                    parentLayout: parentLayout,
                    controlItem: controlItem,
                    hasChildren: true
                );

                foreach (var childControlItem in childLayout.controls)
                {
                    AddControl(childControlItem, sourceLayout, controlPath, controlItem.layout);
                }
            }

            foreach (var layoutTreeString in layoutTreeStrings)
            {
                var layout = InputSystem.LoadLayout(layoutTreeString);

                foreach (var controlItem in layout.controls)
                {
                    if (controlItem.isFirstDefinedInThisLayout)
                    {
                        AddControl(controlItem, layoutTreeString);
                    }
                }
            }

            return controlsByPath.Values.ToList();
        }
    }

    public struct ControlItemInfo
    {
        public string Layout;
        public string? ParentLayout;
        public string SourceLayout;
        public bool HasChildren;
        public string ControlPath;
        public string? ParentPath;
        public string DisplayName;
        public ControlItem ControlItem;

        public ControlItemInfo(
            string controlPath,
            string? parentPath,
            string sourceLayout,
            string? parentLayout,
            ControlItem controlItem,
            bool hasChildren
        ) {
            ControlPath = controlPath;
            DisplayName = controlItem.displayName;
            Layout = controlItem.layout;
            ParentLayout = parentLayout;
            SourceLayout = sourceLayout;
            ParentPath = parentPath;
            HasChildren = hasChildren;
            ControlItem = controlItem;
        }

        public void ApplyOverride(ControlItem controlItem)
        {
            if (!string.IsNullOrEmpty(controlItem.displayName))
            {
                DisplayName = controlItem.displayName;
            }

            ControlItem = controlItem;
        }
    }
}
