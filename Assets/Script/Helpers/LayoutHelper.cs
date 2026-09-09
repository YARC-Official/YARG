using PlasticBand.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using YARG.Core.Logging;
using YARG.Input.Bindings;
using YARG.Menu.ProfileList;
using static UnityEngine.InputSystem.Layouts.InputControlLayout;

namespace YARG.Helpers
{
    public static class LayoutHelper
    {
        public static ControlItemInfo GetControlInfo(ControllerFamily family, string controlName)
        {
            var layouts = GetLayoutsForFamily(family);
            var (controlsByPath, aliasesToCanonicalPaths) = BuildControlInfoIndex(layouts);

            if (controlsByPath.TryGetValue(controlName, out var info))
            {
                return info;
            }

            if (aliasesToCanonicalPaths.TryGetValue(controlName, out var canonicalPath) &&
                controlsByPath.TryGetValue(canonicalPath, out info)
            )
            {
                return info;
            }


            throw new ArgumentOutOfRangeException($"Controller family {family} does not contain control {controlName}!");
        }

        public static List<ControlItemInfo> GetAllControlsForControllerFamily(ControllerFamily family) {

            var layouts = GetLayoutsForFamily(family);
            return BuildControlInfoIndex(layouts).ControlsByPath.Values.ToList();
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

        private static List<string> GetLayoutsForFamily(ControllerFamily family)
        {
            return family switch
            {
                ControllerFamily.FiveFretGuitar => new() { nameof(FiveFretGuitar), nameof(RockBandGuitar), nameof(GuitarHeroGuitar), nameof(RiffmasterGuitar) },
                ControllerFamily.SixFretGuitar => new() { nameof(SixFretGuitar) },
                ControllerFamily.FourLaneDrumkit => new() { nameof(FourLaneDrumkit) },
                ControllerFamily.FiveLaneDrumkit => new() { nameof(FiveLaneDrumkit) },
                ControllerFamily.ProKeyboard => new() { nameof(ProKeyboard) },
                ControllerFamily.ProGuitar => new() { nameof(ProGuitar) },
                _ => throw new NotImplementedException() // TODO-FRICK
            };
        }

        private static (Dictionary<string, ControlItemInfo> ControlsByPath, Dictionary<string, string> AliasesToCanonicalPaths)
            BuildControlInfoIndex(List<string> layoutNames)
        {
            var controlsByPath = new Dictionary<string, ControlItemInfo>(StringComparer.OrdinalIgnoreCase);
            var aliasesToCanonicalPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void RegisterAliases(ControlItem controlItem, string controlPath)
            {
                foreach (var alias in controlItem.aliases)
                {
                    if (!aliasesToCanonicalPaths.TryAdd(alias, controlPath))
                    {
                        var existingPath = aliasesToCanonicalPaths[alias];

                        if (!string.Equals(existingPath, controlPath, StringComparison.OrdinalIgnoreCase))
                        {
                            YargLogger.LogWarning($"Alias {alias} refers to both {existingPath} and {controlPath}; the latter will be ignored");
                        }
                    }
                }
            }

            void AddControl(ControlItem controlItem, string sourceLayout, string parentPath = null, string parentLayout = null)
            {
                var controlPath = parentPath is null ? controlItem.name : $"{parentPath}/{controlItem.name}";

                if (controlItem.isModifyingExistingControl)
                {
                    if (!controlsByPath.TryGetValue(controlPath, out var existingControl))
                    {
                        throw new InvalidOperationException($"Control override {controlPath} was encountered before the original control.");
                    }
                   
                    existingControl.ApplyOverride(controlItem);
                    controlsByPath[controlPath] = existingControl;
                    RegisterAliases(controlItem, controlPath);
                    
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
                    RegisterAliases(controlItem, controlPath);

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
                    RegisterAliases(controlItem, controlPath);
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
                RegisterAliases(controlItem, controlPath);

                foreach (var childControlItem in childLayout.controls)
                {
                    AddControl(childControlItem, sourceLayout, controlPath, controlItem.layout);
                }
            }

            foreach (var layoutTreeString in layoutNames)
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

            return (controlsByPath, aliasesToCanonicalPaths);
        }


    }

    public struct ControlItemInfo
    {
        public string ControlPath;
        public string? ParentPath;
        public string SourceLayout;
        public string? ParentLayout;
        public ControlItem ControlItem;
        public string Layout;
        public bool HasChildren;
        public string DisplayName;

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

            if (!string.IsNullOrEmpty(controlItem.layout))
            {
                Layout = controlItem.layout;
            }
        }
    }
}
