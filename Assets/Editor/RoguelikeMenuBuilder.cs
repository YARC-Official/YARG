using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using YARG.Menu;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Navigation;
using YARG.Menu.Roguelike;

namespace Editor
{
    /// <summary>
    /// Builds the roguelike menu from the Quick Play (music library) menu, so it looks like Quick Play
    /// without hand-editing prefabs or scenes. Safe to re-run (e.g. after pulling music library
    /// changes from upstream): it replaces what it built before.
    /// </summary>
    /// <remarks>
    /// <list type="number">
    /// <item>Creates RoguelikeMenu.prefab from MusicLibraryMenu.prefab, swapping the MusicLibraryMenu
    /// component for RoguelikeMenu, re-pointing the prefab's button/scrollbar events at it, hiding the
    /// search bar and popup menu, and setting its menu ID.</item>
    /// <item>Adds it to MenuScene next to the Quick Play menu, with the same scene overrides.</item>
    /// <item>Adds a Roguelike button to the Game Modes menu, copied from the Careers button.</item>
    /// </list>
    /// </remarks>
    public static class RoguelikeMenuBuilder
    {
        private const string SOURCE_MENU_PREFAB    = "Assets/Prefabs/Menu/MusicLibrary/MusicLibraryMenu.prefab";
        private const string ROGUELIKE_MENU_FOLDER = "Assets/Prefabs/Menu/Roguelike";
        private const string ROGUELIKE_MENU_PREFAB = ROGUELIKE_MENU_FOLDER + "/RoguelikeMenu.prefab";
        private const string GAME_MODES_PREFAB     = "Assets/Prefabs/Menu/GameModes/GameModeMenu.prefab";
        private const string MENU_SCENE            = "Assets/Scenes/MenuScene.unity";

        private const string CAREER_BUTTON_METHOD    = "CareerList";
        private const string ROGUELIKE_BUTTON_METHOD = "Roguelike";
        private const string CAREER_BUTTON_KEY       = "Menu.GameModes.Options.CareerList";
        private const string ROGUELIKE_BUTTON_KEY    = "Menu.GameModes.Options.Roguelike";

        // The music library's header title, which RoguelikeMenu sets at runtime
        private const string LIBRARY_TITLE_TEXT = "Music Library";

        // MusicLibraryMenu fields that RoguelikeMenu has under a different name. Fields inherited from
        // ListMenu (row prefab, row parent, scrollbar) and _sidebar are copied by matching name.
        private static readonly Dictionary<string, string> RenamedFields = new()
        {
            { "_subHeader", "_subtitleText" },
            { "_sortInfoHeaderPrimaryText", "_headerPrimaryText" },
            { "_sortInfoHeaderSongCountText", "_headerSecondaryText" },
            { "_sortInfoHeaderStarCountText", "_headerSideText" },
        };

        // Parts of the music library the roguelike menu doesn't use
        private static readonly string[] HiddenFields = { "_searchField", "_popupMenu", "_noPlayerWarning" };

        [MenuItem("YARG/Roguelike/Build Menu")]
        public static void Build()
        {
            if (!EditorUtility.DisplayDialog("Build Roguelike Menu",
                "This will:\n\n" +
                "• Create (or replace) " + ROGUELIKE_MENU_PREFAB + " from the Quick Play menu\n" +
                "• Add it to MenuScene next to the Quick Play menu\n" +
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

            var contents = PrefabUtility.LoadPrefabContents(SOURCE_MENU_PREFAB);
            try
            {
                contents.name = "RoguelikeMenu";

                var libraryMenu = contents.GetComponent<MusicLibraryMenu>();
                if (libraryMenu == null)
                {
                    throw new System.InvalidOperationException("MusicLibraryMenu component not found on " + SOURCE_MENU_PREFAB);
                }

                var roguelikeMenu = contents.AddComponent<RoguelikeMenu>();
                CopyMatchingFields(libraryMenu, roguelikeMenu);
                AssignTitleText(contents, roguelikeMenu, log);

                var libraryFields = new SerializedObject(libraryMenu);
                // The search bar's focus overlay is a full-screen dark image that lives outside the
                // search bar, and the search code hides it every frame unless the search box has
                // focus. With the search bar hidden that code never runs, so the overlay would stay
                // on and dim the whole menu. Hide it explicitly, before the search bar itself.
                if (libraryFields.FindProperty("_searchField")?.objectReferenceValue is Component searchField)
                {
                    HideReferencedObject(new SerializedObject(searchField).FindProperty("_focusBackground"), contents);
                }

                foreach (var field in HiddenFields)
                {
                    HideReferencedObject(libraryFields.FindProperty(field), contents);
                }

                int retargeted = RetargetEvents(contents, libraryMenu, roguelikeMenu, out int removed);
                log.AppendLine($"Re-pointed {retargeted} button/scrollbar events at the roguelike menu, " +
                    $"removed {removed} that have no roguelike equivalent");

                Object.DestroyImmediate(libraryMenu);

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

        private static void HideReferencedObject(SerializedProperty property, GameObject contentsRoot)
        {
            var gameObject = property?.objectReferenceValue switch
            {
                GameObject go       => go,
                Component component => component.gameObject,
                _                   => null,
            };
            if (gameObject == null)
            {
                return;
            }

            // If it's part of an embedded prefab (e.g. the search bar, whose script sits on a child
            // while the visible outline is the prefab's root), hide the whole embedded prefab
            var embeddedRoot = PrefabUtility.GetNearestPrefabInstanceRoot(gameObject);
            if (embeddedRoot != null && embeddedRoot != contentsRoot)
            {
                gameObject = embeddedRoot;
            }

            gameObject.SetActive(false);
        }

        /// <summary>Give RoguelikeMenu the header title text, and relabel every copy of it in the prefab.</summary>
        private static void AssignTitleText(GameObject contents, RoguelikeMenu menu, StringBuilder log)
        {
            var titles = contents.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true)
                .Where(text => text.text == LIBRARY_TITLE_TEXT)
                .ToList();

            foreach (var title in titles)
            {
                title.text = "Game Modes";
            }

            var fields = new SerializedObject(menu);
            fields.FindProperty("_titleText").objectReferenceValue =
                titles.FirstOrDefault(text => text.gameObject.activeInHierarchy) ?? titles.FirstOrDefault();
            fields.ApplyModifiedPropertiesWithoutUndo();

            if (titles.Count == 0)
            {
                log.AppendLine("WARNING: header title text not found, the roguelike menu will keep the Quick Play title");
            }
        }

        /// <summary>
        /// Point UnityEvent calls (header back button, scrollbar, ...) that target the music library
        /// component at the roguelike menu instead. Calls to methods the roguelike menu doesn't have
        /// (e.g. NextSort) are removed.
        /// </summary>
        private static int RetargetEvents(GameObject contents, Component from, Component to, out int removed)
        {
            int retargeted = 0;
            removed = 0;
            var targetTypeName = to.GetType().FullName + ", " + to.GetType().Assembly.GetName().Name;

            foreach (var component in contents.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component == from)
                {
                    continue;
                }

                var serialized = new SerializedObject(component);

                // Collect the call lists first; editing arrays while iterating would break the iterator
                var callLists = new List<string>();
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.isArray && property.name == "m_Calls" &&
                        property.propertyPath.EndsWith("m_PersistentCalls.m_Calls"))
                    {
                        callLists.Add(property.propertyPath);
                    }
                }

                bool changed = false;
                foreach (var path in callLists)
                {
                    var calls = serialized.FindProperty(path);
                    for (int i = calls.arraySize - 1; i >= 0; i--)
                    {
                        var call = calls.GetArrayElementAtIndex(i);
                        var target = call.FindPropertyRelative("m_Target");
                        if (target.objectReferenceValue != from)
                        {
                            continue;
                        }

                        var methodName = call.FindPropertyRelative("m_MethodName").stringValue;
                        if (to.GetType().GetMethod(methodName, System.Type.EmptyTypes) is { IsPublic: true })
                        {
                            target.objectReferenceValue = to;
                            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = targetTypeName;
                            retargeted++;
                        }
                        else
                        {
                            calls.DeleteArrayElementAtIndex(i);
                            removed++;
                        }
                        changed = true;
                    }
                }

                if (changed)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            return retargeted;
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

            var sourceMenu = menus.FirstOrDefault(menu => menu.Menu == MenuManager.Menu.MusicLibrary);
            if (sourceMenu == null)
            {
                log.AppendLine("WARNING: Quick Play menu not found in MenuScene, roguelike menu not added");
                return;
            }

            foreach (var existing in menus.Where(menu => menu.Menu == MenuManager.Menu.Roguelike))
            {
                Object.DestroyImmediate(existing.gameObject);
                log.AppendLine("Removed previous roguelike menu from MenuScene");
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ROGUELIKE_MENU_PREFAB);
            var sourceObject = sourceMenu.gameObject;
            var instance = (GameObject) PrefabUtility.InstantiatePrefab(prefab, sourceObject.transform.parent);
            instance.transform.SetSiblingIndex(sourceObject.transform.GetSiblingIndex() + 1);

            int copied = CopySceneOverrides(sourceObject, instance);
            instance.name = "RoguelikeMenu";
            instance.SetActive(sourceObject.activeSelf);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"Added RoguelikeMenu to MenuScene ({copied} layout overrides copied from the Quick Play menu)");
        }

        /// <summary>
        /// Copy the source menu instance's scene overrides (layout tweaks made in MenuScene) onto the
        /// roguelike instance, matching objects by hierarchy path and component type.
        /// </summary>
        private static int CopySceneOverrides(GameObject sourceInstance, GameObject roguelikeInstance)
        {
            var sourceAsset = PrefabUtility.GetCorrespondingObjectFromSource(sourceInstance);
            var roguelikeAsset = PrefabUtility.GetCorrespondingObjectFromSource(roguelikeInstance);
            var modifications = PrefabUtility.GetPropertyModifications(sourceInstance);
            if (sourceAsset == null || roguelikeAsset == null || modifications == null)
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

                var target = MapToOtherPrefab(modification.target, sourceAsset, roguelikeAsset);
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
            // and the roguelike prefab is a copy of the source prefab, so the structure is identical
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
