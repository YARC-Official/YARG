namespace Editor
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;
    using YARG.Venue.Characters;

    [CustomPropertyDrawer(typeof(AnimationStateMap))]
    public class AnimationStateMapDrawer : PropertyDrawer
    {
        private const float ButtonWidth = 25f;
        private const float Spacing     = 5f;

        // Cached foldout states for each property path
        private static Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();

        // Store the last selected enum value to use as default for next item
        private static Dictionary<string, int> lastSelectedEnumValue = new Dictionary<string, int>();

        // Track which control should receive focus in the next frame
        private static string nextFocusControl = null;

        // Flag to add a new item on the next frame
        private static bool   addNewItemNextFrame    = false;
        private static string pendingAddPropertyPath = null;

        // Flag to ensure we handle delayed focus properly
        private static bool shouldFocusNextFrame = false;
        private static int  framesSinceAdd       = 0;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Get the lists
            SerializedProperty typesProperty = property.FindPropertyRelative("_animationStateTypes");
            SerializedProperty namesProperty = property.FindPropertyRelative("_animationStateNames");

            // Check if the property is folded out
            bool folded = !foldoutStates.ContainsKey(property.propertyPath) || !foldoutStates[property.propertyPath];

            if (folded) return EditorGUIUtility.singleLineHeight;

            // Count how many entries we have
            int count = Mathf.Max(typesProperty.arraySize, 0);

            // Add height for the header, every entry, and the add button at the bottom
            return EditorGUIUtility.singleLineHeight * (count + 3) +
                EditorGUIUtility.standardVerticalSpacing * (count + 2);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Begin the property
            EditorGUI.BeginProperty(position, label, property);

            // Get the lists
            SerializedProperty typesProperty = property.FindPropertyRelative("_animationStateTypes");
            SerializedProperty namesProperty = property.FindPropertyRelative("_animationStateNames");

            // Make sure our arrays are the same size
            if (typesProperty.arraySize != namesProperty.arraySize)
            {
                int smallerSize = Mathf.Min(typesProperty.arraySize, namesProperty.arraySize);
                typesProperty.arraySize = smallerSize;
                namesProperty.arraySize = smallerSize;
            }

            // Check if we have a stored foldout state, otherwise initialize it to false
            if (!foldoutStates.ContainsKey(property.propertyPath))
            {
                foldoutStates[property.propertyPath] = false;
            }

            // Initialize last selected enum value if needed
            if (!lastSelectedEnumValue.ContainsKey(property.propertyPath))
            {
                lastSelectedEnumValue[property.propertyPath] = 0;
            }

            // Get a list of used enum values to avoid duplicates
            HashSet<int> usedEnumValues = new HashSet<int>();
            for (int i = 0; i < typesProperty.arraySize; i++)
            {
                usedEnumValues.Add(typesProperty.GetArrayElementAtIndex(i).enumValueIndex);
            }

            // Check if we need to add a new item from the last frame
            if (addNewItemNextFrame && pendingAddPropertyPath == property.propertyPath)
            {
                AddNewItem(property, typesProperty, namesProperty, usedEnumValues);
                addNewItemNextFrame = false;
                pendingAddPropertyPath = null;
                shouldFocusNextFrame = true;
                framesSinceAdd = 0;
                GUI.changed = true;
            }

            // Increment frame counter since add
            if (shouldFocusNextFrame)
            {
                framesSinceAdd++;

                // After a couple of frames, try focusing on the new dropdown
                if (framesSinceAdd >= 2)
                {
                    EditorGUI.FocusTextInControl("EnumField" + (typesProperty.arraySize - 1));
                    shouldFocusNextFrame = false;
                    framesSinceAdd = 0;
                }
            }

            // Draw foldout
            Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            foldoutStates[property.propertyPath] =
                EditorGUI.Foldout(foldoutRect, foldoutStates[property.propertyPath], label);

            // If folded out, draw the list
            if (foldoutStates[property.propertyPath])
            {
                // Indent the content
                EditorGUI.indentLevel++;

                // Draw header
                Rect headerRect = new Rect(position.x,
                    position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
                    position.width, EditorGUIUtility.singleLineHeight);

                Rect stateRect = new Rect(headerRect.x, headerRect.y, headerRect.width * 0.5f,
                    headerRect.height);
                Rect nameRect = new Rect(headerRect.x + headerRect.width * 0.5f, headerRect.y,
                    headerRect.width * 0.5f, headerRect.height);

                EditorGUI.LabelField(stateRect, "Animation State Type");
                EditorGUI.LabelField(nameRect, "Animation State Name");

                // Event handling for key presses - this needs to be done here to catch events for all fields
                Event currentEvent = Event.current;
                bool isEnterKeyDown = currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Return;
                string focusedControl = GUI.GetNameOfFocusedControl();

                // Check if enter key was pressed in a text field
                if (isEnterKeyDown && focusedControl.StartsWith("TextField"))
                {
                    pendingAddPropertyPath = property.propertyPath;
                    addNewItemNextFrame = true;
                    currentEvent.Use(); // Consume the event
                }

                // Check if enter key was pressed in an enum field
                if (isEnterKeyDown && focusedControl.StartsWith("EnumField"))
                {
                    // Extract the index from the control name
                    string indexStr = focusedControl.Substring("EnumField".Length);
                    if (int.TryParse(indexStr, out int index) && index < typesProperty.arraySize)
                    {
                        // Focus the corresponding text field
                        nextFocusControl = "TextField" + index;
                        currentEvent.Use(); // Consume the event
                    }
                }

                // Apply the focus change from the last frame if needed
                if (nextFocusControl != null)
                {
                    EditorGUI.FocusTextInControl(nextFocusControl);
                    nextFocusControl = null;
                }

                // Draw entries
                for (int i = 0; i < typesProperty.arraySize; i++)
                {
                    float yPos = headerRect.y + (i + 1) *
                        (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);

                    Rect entryStateRect = new Rect(position.x, yPos, position.width * 0.5f - Spacing,
                        EditorGUIUtility.singleLineHeight);
                    Rect entryNameRect = new Rect(position.x + position.width * 0.5f, yPos,
                        position.width * 0.5f - ButtonWidth - Spacing, EditorGUIUtility.singleLineHeight);
                    Rect entryButtonRect = new Rect(entryNameRect.x + entryNameRect.width + Spacing, yPos, ButtonWidth,
                        EditorGUIUtility.singleLineHeight);

                    // Draw enum dropdown
                    SerializedProperty typeProperty = typesProperty.GetArrayElementAtIndex(i);
                    EditorGUI.BeginChangeCheck();

                    // Name the enum field for focus control
                    GUI.SetNextControlName("EnumField" + i);

                    int previousValue = typeProperty.enumValueIndex;
                    EditorGUI.PropertyField(entryStateRect, typeProperty, GUIContent.none);

                    if (EditorGUI.EndChangeCheck())
                    {
                        // Check if the new value would create a duplicate
                        if (usedEnumValues.Contains(typeProperty.enumValueIndex) &&
                            typeProperty.enumValueIndex != previousValue)
                        {
                            // Revert to previous value
                            typeProperty.enumValueIndex = previousValue;
                            Debug.LogWarning("Cannot use the same Animation State Type twice");
                        }
                        else
                        {
                            // Update the used values set
                            usedEnumValues.Remove(previousValue);
                            usedEnumValues.Add(typeProperty.enumValueIndex);

                            // Update last selected value
                            lastSelectedEnumValue[property.propertyPath] = typeProperty.enumValueIndex;
                        }
                    }

                    // Draw string field
                    SerializedProperty nameProperty = namesProperty.GetArrayElementAtIndex(i);
                    GUI.SetNextControlName("TextField" + i);
                    nameProperty.stringValue = EditorGUI.TextField(entryNameRect, nameProperty.stringValue);

                    // Draw remove button
                    if (GUI.Button(entryButtonRect, "-"))
                    {
                        usedEnumValues.Remove(typeProperty.enumValueIndex);
                        typesProperty.DeleteArrayElementAtIndex(i);
                        namesProperty.DeleteArrayElementAtIndex(i);
                        i--; // Adjust index to account for removal
                    }
                }

                // Add button at the bottom of the list
                float addButtonY = headerRect.y + (typesProperty.arraySize + 1) *
                    (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);

                Rect addButtonRect = new Rect(
                    position.x + position.width - ButtonWidth,
                    addButtonY,
                    ButtonWidth,
                    EditorGUIUtility.singleLineHeight);

                if (GUI.Button(addButtonRect, "+"))
                {
                    AddNewItem(property, typesProperty, namesProperty, usedEnumValues);
                    shouldFocusNextFrame = true;
                    framesSinceAdd = 0;
                }

                EditorGUI.indentLevel--;
            }

            // End the property
            EditorGUI.EndProperty();
        }

        // Helper method to add a new item
        private void AddNewItem(SerializedProperty property, SerializedProperty typesProperty,
            SerializedProperty namesProperty, HashSet<int> usedEnumValues)
        {
            int nextEnumValue = GetNextAvailableEnumValue(property, usedEnumValues);

            // Add new entry immediately
            typesProperty.arraySize++;
            namesProperty.arraySize++;

            // Set values to next available enum value and empty string
            typesProperty.GetArrayElementAtIndex(typesProperty.arraySize - 1).enumValueIndex = nextEnumValue;
            namesProperty.GetArrayElementAtIndex(namesProperty.arraySize - 1).stringValue = "";

            // Update last selected value
            lastSelectedEnumValue[property.propertyPath] = nextEnumValue;

            // Schedule focus to be set in next frame (direct focus doesn't work when adding items)
            EditorApplication.delayCall += () =>
            {
                EditorGUI.FocusTextInControl("EnumField" + (typesProperty.arraySize - 1));
            };
        }

        // Helper method to find the next available enum value
        private int GetNextAvailableEnumValue(SerializedProperty property, HashSet<int> usedEnumValues)
        {
            int nextValue = lastSelectedEnumValue[property.propertyPath] + 1;

            // If we're at the end of the enum, wrap back to 0
            string[] names = System.Enum.GetNames(typeof(VenueCharacter.AnimationStateType));
            if (nextValue >= names.Length) nextValue = 0;

            // Find the first unused value starting from nextValue
            int startValue = nextValue;
            while (usedEnumValues.Contains(nextValue))
            {
                nextValue++;

                // If we wrap around completely, we need to add indices beyond the enum range
                if (nextValue >= names.Length) nextValue = 0;

                // If we've checked all enum values and came back to where we started,
                // then just use the starting value (this should rarely happen)
                if (nextValue == startValue) break;
            }

            return nextValue;
        }
    }
}