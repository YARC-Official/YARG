using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using YARG.Menu;
using YARG.Menu.Career;
using YARG.Menu.Navigation;
using YARG.Menu.Roguelike;

namespace Editor
{
    /// <summary>
    /// Builds the roguelike menu from the career menu, so its layout follows career's without
    /// hand-editing prefabs or scenes. Safe to re-run (e.g. after pulling career menu changes from
    /// upstream): it replaces what it built before.
    /// </summary>
    /// <remarks>
    /// <list type="number">
    /// <item>Creates RoguelikeMenu.prefab from CareerMenu.prefab, swapping the CareerMenu component
    /// for RoguelikeMenu and setting its menu ID.</item>
    /// <item>Adds it to MenuScene next to the career menu, with the same scene overrides.</item>
    /// <item>Adds a Roguelike button to the Game Modes menu, copied from the Careers button.</item>
    /// </list>
    /// </remarks>
    public static class RoguelikeMenuBuilder
    {
        private const string CAREER_MENU_PREFAB    = "Assets/Prefabs/Menu/Career/CareerMenu.prefab";
        private const string ROGUELIKE_MENU_FOLDER = "Assets/Prefabs/Menu/Roguelike";
        private const string ROGUELIKE_MENU_PREFAB = ROGUELIKE_MENU_FOLDER + "/RoguelikeMenu.prefab";
        private const string GAME_MODES_PREFAB     = "Assets/Prefabs/Menu/GameModes/GameModeMenu.prefab";
        private const string MENU_SCENE            = "Assets/Scenes/MenuScene.unity";

        private const string CAREER_BUTTON_METHOD    = "CareerList";
        private const string ROGUELIKE_BUTTON_METHOD = "Roguelike";
        private const string CAREER_BUTTON_KEY       = "Menu.GameModes.Options.CareerList";
        private const string ROGUELIKE_BUTTON_KEY    = "Menu.GameModes.Options.Roguelike";

        // CareerMenu fields that RoguelikeMenu has under a different name
        private static readonly Dictionary<string, string> RenamedFields = new()
        {
            { "_careerNameText", "_titleText" },
            { "_careerDescriptionText", "_subtitleText" },
        };

        [MenuItem("YARG/Roguelike/Build Menu")]
        public static void Build()
        {
            if (!EditorUtility.DisplayDialog("Build Roguelike Menu",
                "This will:\n\n" +
                "• Create (or replace) " + ROGUELIKE_MENU_PREFAB + " from the career menu\n" +
                "• Add it to MenuScene next to the career menu\n" +
                "• Add a Roguelike button to the Game Modes menu\n\n" +
                "MenuScene will be opened and saved.",
                "Build", "Cancel"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var log = new StringBuilder();
            BuildMenuPrefab(log);
            AddGameModesButton(log);
            AddToMenuScene(log);
            AssetDatabase.SaveAssets();

            Debug.Log("[Roguelike] Build Menu finished:\n" + log);
            EditorUtility.DisplayDialog("Build Roguelike Menu", log.ToString(), "OK");
        }

        private static void BuildMenuPrefab(StringBuilder log)
        {
            if (!AssetDatabase.IsValidFolder(ROGUELIKE_MENU_FOLDER))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(ROGUELIKE_MENU_FOLDER)!.Replace('\\', '/'),
                    Path.GetFileName(ROGUELIKE_MENU_FOLDER));
            }

            var contents = PrefabUtility.LoadPrefabContents(CAREER_MENU_PREFAB);
            try
            {
                contents.name = "RoguelikeMenu";

                var careerMenu = contents.GetComponent<CareerMenu>();
                if (careerMenu == null)
                {
                    throw new System.InvalidOperationException("CareerMenu component not found on " + CAREER_MENU_PREFAB);
                }

                var roguelikeMenu = contents.AddComponent<RoguelikeMenu>();
                CopyMatchingFields(careerMenu, roguelikeMenu);

                // Career shows its art in these; the roguelike menu has none, and an empty RawImage
                // draws as a white box
                var careerFields = new SerializedObject(careerMenu);
                DisableRawImage(careerFields.FindProperty("_bgImage"));
                DisableRawImage(careerFields.FindProperty("_videoTexture"));

                Object.DestroyImmediate(careerMenu);

                var menuObject = new SerializedObject(contents.GetComponent<MenuObject>());
                menuObject.FindProperty("<Menu>k__BackingField").intValue = (int) MenuManager.Menu.Roguelike;
                menuObject.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, ROGUELIKE_MENU_PREFAB);
                log.AppendLine("Saved " + ROGUELIKE_MENU_PREFAB);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void CopyMatchingFields(Component source, Component destination)
        {
            var src = new SerializedObject(source);
            var dst = new SerializedObject(destination);

            var property = src.GetIterator();
            property.NextVisible(true);
            do
            {
                if (property.name == "m_Script")
                {
                    continue;
                }

                if (RenamedFields.TryGetValue(property.name, out var renamed))
                {
                    var renamedTarget = dst.FindProperty(renamed);
                    if (renamedTarget != null && property.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        renamedTarget.objectReferenceValue = property.objectReferenceValue;
                    }
                    continue;
                }

                // Fields inherited from ListMenu (row prefab, row parent, scrollbar) have the same path
                var target = dst.FindProperty(property.propertyPath);
                if (target != null && target.propertyType == property.propertyType)
                {
                    dst.CopyFromSerializedProperty(property);
                }
            } while (property.NextVisible(false));

            dst.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DisableRawImage(SerializedProperty property)
        {
            if (property?.objectReferenceValue is RawImage image)
            {
                image.enabled = false;
            }
        }

        private static void AddGameModesButton(StringBuilder log)
        {
            var contents = PrefabUtility.LoadPrefabContents(GAME_MODES_PREFAB);
            try
            {
                var buttons = contents.GetComponentsInChildren<NavigatableButton>(true);
                if (buttons.Any(button => button.CallsMethod(ROGUELIKE_BUTTON_METHOD)))
                {
                    log.AppendLine("Game Modes already has a Roguelike button");
                    return;
                }

                var careerButton = buttons.FirstOrDefault(button => button.CallsMethod(CAREER_BUTTON_METHOD));
                if (careerButton == null)
                {
                    log.AppendLine("WARNING: Careers button not found in Game Modes, no Roguelike button added");
                    return;
                }

                var source = careerButton.gameObject;
                var copy = Object.Instantiate(source, source.transform.parent);
                copy.name = "Roguelike";
                copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);

                int replaced = 0;
                foreach (var component in copy.GetComponentsInChildren<Component>(true))
                {
                    replaced += ReplaceStrings(component, new Dictionary<string, string>
                    {
                        { CAREER_BUTTON_METHOD, ROGUELIKE_BUTTON_METHOD },
                        { CAREER_BUTTON_KEY, ROGUELIKE_BUTTON_KEY },
                    });
                }

                PrefabUtility.SaveAsPrefabAsset(contents, GAME_MODES_PREFAB);
                log.AppendLine($"Added Roguelike button to Game Modes ({replaced} references updated)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>Replace exact string values in a component's serialized fields (method names, keys).</summary>
        private static int ReplaceStrings(Component component, Dictionary<string, string> replacements)
        {
            if (component == null)
            {
                return 0;
            }

            int count = 0;
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType == SerializedPropertyType.String &&
                    replacements.TryGetValue(property.stringValue, out var replacement))
                {
                    property.stringValue = replacement;
                    count++;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return count;
        }

        private static void AddToMenuScene(StringBuilder log)
        {
            var scene = EditorSceneManager.OpenScene(MENU_SCENE, OpenSceneMode.Single);
            var menus = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MenuObject>(true))
                .ToList();

            var careerMenu = menus.FirstOrDefault(menu => menu.Menu == MenuManager.Menu.Career);
            if (careerMenu == null)
            {
                log.AppendLine("WARNING: career menu not found in MenuScene, roguelike menu not added");
                return;
            }

            foreach (var existing in menus.Where(menu => menu.Menu == MenuManager.Menu.Roguelike))
            {
                Object.DestroyImmediate(existing.gameObject);
                log.AppendLine("Removed previous roguelike menu from MenuScene");
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ROGUELIKE_MENU_PREFAB);
            var careerObject = careerMenu.gameObject;
            var instance = (GameObject) PrefabUtility.InstantiatePrefab(prefab, careerObject.transform.parent);
            instance.transform.SetSiblingIndex(careerObject.transform.GetSiblingIndex() + 1);

            int copied = CopySceneOverrides(careerObject, instance);
            instance.name = "RoguelikeMenu";
            instance.SetActive(careerObject.activeSelf);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"Added RoguelikeMenu to MenuScene ({copied} layout overrides copied from the career menu)");
        }

        /// <summary>
        /// Copy the career menu instance's scene overrides (layout tweaks made in MenuScene) onto the
        /// roguelike instance, matching objects by hierarchy path and component type.
        /// </summary>
        private static int CopySceneOverrides(GameObject careerInstance, GameObject roguelikeInstance)
        {
            var careerAsset = PrefabUtility.GetCorrespondingObjectFromSource(careerInstance);
            var roguelikeAsset = PrefabUtility.GetCorrespondingObjectFromSource(roguelikeInstance);
            var modifications = PrefabUtility.GetPropertyModifications(careerInstance);
            if (careerAsset == null || roguelikeAsset == null || modifications == null)
            {
                return 0;
            }

            var mapped = new List<PropertyModification>();
            foreach (var modification in modifications)
            {
                if (modification.propertyPath == "m_Name")
                {
                    continue;
                }

                var target = MapToOtherPrefab(modification.target, careerAsset, roguelikeAsset);
                if (target == null)
                {
                    continue;
                }

                mapped.Add(new PropertyModification
                {
                    target = target,
                    propertyPath = modification.propertyPath,
                    value = modification.value,
                    objectReference = modification.objectReference,
                });
            }

            // Keep the instance's own modifications unless a copied one sets the same property
            var copiedKeys = new HashSet<(Object, string)>(mapped.Select(m => (m.target, m.propertyPath)));
            var existing = PrefabUtility.GetPropertyModifications(roguelikeInstance) ?? new PropertyModification[0];
            var merged = existing.Where(m => !copiedKeys.Contains((m.target, m.propertyPath))).Concat(mapped);

            PrefabUtility.SetPropertyModifications(roguelikeInstance, merged.ToArray());
            return mapped.Count;
        }

        private static Object MapToOtherPrefab(Object target, GameObject fromRoot, GameObject toRoot)
        {
            var gameObject = target switch
            {
                GameObject go => go,
                Component c   => c.gameObject,
                _             => null,
            };
            if (gameObject == null || !gameObject.transform.IsChildOf(fromRoot.transform))
            {
                return null;
            }

            // Walk by sibling index rather than name: several children share a name (or have none),
            // and the roguelike prefab is a copy of the career prefab, so the structure is identical
            var indices = new Stack<int>();
            for (var t = gameObject.transform; t != fromRoot.transform; t = t.parent)
            {
                indices.Push(t.GetSiblingIndex());
            }

            var mappedTransform = toRoot.transform;
            foreach (int index in indices)
            {
                if (index >= mappedTransform.childCount)
                {
                    return null;
                }
                mappedTransform = mappedTransform.GetChild(index);
            }

            if (target is GameObject)
            {
                return mappedTransform.gameObject;
            }

            // Match the component by type and its index among components of that type
            var component = (Component) target;
            var type = component.GetType();
            int componentIndex = System.Array.IndexOf(gameObject.GetComponents(type), component);
            var candidates = mappedTransform.GetComponents(type);
            return componentIndex >= 0 && componentIndex < candidates.Length ? candidates[componentIndex] : null;
        }
    }
}
