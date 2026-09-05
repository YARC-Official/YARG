using System;
using YARG.Helpers;

namespace Editor
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;
    using YARG.Venue.Characters;

    [CustomPropertyDrawer(typeof(SerializedDictionary<,>), true)]
    public class SerializedDictionaryDrawer : PropertyDrawer
    {
        private const float ButtonWidth = 25f;
        private const float Spacing     = 5f;

        // Cached foldout states for each property path
        private static Dictionary<string, bool> _foldoutStates = new Dictionary<string, bool>();

        // Store the last selected enum value to use as default for next item
        private static Dictionary<string, int> _lastSelectedEnumValue = new Dictionary<string, int>();

        // Track which control should receive focus in the next frame
        private static string _nextFocusControl = null;

        // Flag to add a new item on the next frame
        private static bool   _addNewItemNextFrame    = false;
        private static string _pendingAddPropertyPath = null;

        // Flag to ensure we handle delayed focus properly
        private static bool _shouldFocusNextFrame = false;
        private static int  _framesSinceAdd       = 0;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Get the lists
            var keysProperty = property.FindPropertyRelative("_keys");
            var valuesProperty = property.FindPropertyRelative("_values");

            if (!CanDrawDictionary(keysProperty, valuesProperty))
            {
                return EditorGUIUtility.singleLineHeight;
            }

            // Check if the property is folded out
            bool folded = !_foldoutStates.ContainsKey(property.propertyPath) || !_foldoutStates[property.propertyPath];

            if (folded) return EditorGUIUtility.singleLineHeight;

            // Count how many entries we have
            int count = Mathf.Max(keysProperty.arraySize, 0);

            // Add height for the header, every entry, and the add button at the bottom
            return EditorGUIUtility.singleLineHeight * (count + 3) +
                EditorGUIUtility.standardVerticalSpacing * (count + 2);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Begin the property
            EditorGUI.BeginProperty(position, label, property);

            // Get the lists
            var keysProperty = property.FindPropertyRelative("_keys");
            var valuesProperty = property.FindPropertyRelative("_values");

            if (!CanDrawDictionary(keysProperty, valuesProperty))
            {
                EditorGUI.LabelField(position, label, "Only SerializedDictionary<Enum,> is supported");
                EditorGUI.EndProperty();
                return;
            }

            // Make sure our arrays are the same size
            if (keysProperty.arraySize != valuesProperty.arraySize)
            {
                int smallerSize = Mathf.Min(keysProperty.arraySize, valuesProperty.arraySize);
                keysProperty.arraySize = smallerSize;
                valuesProperty.arraySize = smallerSize;
            }

            // Check if we have a stored foldout state, otherwise initialize it to false
            _foldoutStates.TryAdd(property.propertyPath, false);

            // Initialize last selected enum value if needed
            _lastSelectedEnumValue.TryAdd(property.propertyPath, 0);

            // Get a list of used enum values to avoid duplicates
            HashSet<int> usedEnumValues = new HashSet<int>();
            for (int i = 0; i < keysProperty.arraySize; i++)
            {
                usedEnumValues.Add(keysProperty.GetArrayElementAtIndex(i).enumValueIndex);
            }

            // Check if we need to add a new item from the last frame
            if (_addNewItemNextFrame && _pendingAddPropertyPath == property.propertyPath)
            {
                AddNewItem(property, keysProperty, valuesProperty, usedEnumValues);
                _addNewItemNextFrame = false;
                _pendingAddPropertyPath = null;
                _shouldFocusNextFrame = true;
                _framesSinceAdd = 0;
                GUI.changed = true;
            }

            // Increment frame counter since add
            if (_shouldFocusNextFrame)
            {
                _framesSinceAdd++;

                // After a couple of frames, try focusing on the new dropdown
                if (_framesSinceAdd >= 2)
                {
                    EditorGUI.FocusTextInControl("EnumField" + (keysProperty.arraySize - 1));
                    _shouldFocusNextFrame = false;
                    _framesSinceAdd = 0;
                }
            }

            // Draw foldout
            Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            _foldoutStates[property.propertyPath] =
                EditorGUI.Foldout(foldoutRect, _foldoutStates[property.propertyPath], label);

            // If folded out, draw the list
            if (_foldoutStates[property.propertyPath])
            {
                // Indent the content
                EditorGUI.indentLevel++;

                // Draw header
                Rect headerRect = new Rect(position.x,
                    position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
                    position.width, EditorGUIUtility.singleLineHeight);

                Rect keyRect = new Rect(headerRect.x, headerRect.y, headerRect.width * 0.5f,
                    headerRect.height);
                Rect valueRect = new Rect(headerRect.x + headerRect.width * 0.5f, headerRect.y,
                    headerRect.width * 0.5f, headerRect.height);

                EditorGUI.LabelField(keyRect, "Key");
                EditorGUI.LabelField(valueRect, "Value");

                // Event handling for key presses - this needs to be done here to catch events for all fields
                Event currentEvent = Event.current;
                bool isEnterKeyDown = currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Return;
                string focusedControl = GUI.GetNameOfFocusedControl();

                // Check if enter key was pressed in a text field
                if (isEnterKeyDown && focusedControl.StartsWith("ValueField"))
                {
                    _pendingAddPropertyPath = property.propertyPath;
                    _addNewItemNextFrame = true;
                    currentEvent.Use(); // Consume the event
                }

                // Check if enter key was pressed in an enum field
                if (isEnterKeyDown && focusedControl.StartsWith("EnumField"))
                {
                    // Extract the index from the control name
                    string indexStr = focusedControl.Substring("EnumField".Length);
                    if (int.TryParse(indexStr, out int index) && index < keysProperty.arraySize)
                    {
                        // Focus the corresponding text field
                        _nextFocusControl = "ValueField" + index;
                        currentEvent.Use(); // Consume the event
                    }
                }

                // Apply the focus change from the last frame if needed
                if (_nextFocusControl != null)
                {
                    EditorGUI.FocusTextInControl(_nextFocusControl);
                    _nextFocusControl = null;
                }

                float yPos = headerRect.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                // Draw entries
                for (int i = 0; i < keysProperty.arraySize; i++)
                {
                    SerializedProperty keyProperty = keysProperty.GetArrayElementAtIndex(i);
                    SerializedProperty valueProperty = valuesProperty.GetArrayElementAtIndex(i);

                    float rowHeight = GetRowHeight(keyProperty, valueProperty);

                    Rect entryKeyRect = new Rect(position.x, yPos, position.width * 0.5f - Spacing,
                        EditorGUIUtility.singleLineHeight);
                    Rect entryValueRect = new Rect(position.x + position.width * 0.5f, yPos,
                        position.width * 0.5f - ButtonWidth - Spacing, EditorGUIUtility.singleLineHeight);
                    Rect entryButtonRect = new Rect(entryValueRect.x + entryValueRect.width + Spacing, yPos, ButtonWidth,
                        EditorGUIUtility.singleLineHeight);

                    // Draw enum dropdown
                    EditorGUI.BeginChangeCheck();

                    // Name the enum field for focus control
                    GUI.SetNextControlName("EnumField" + i);

                    int previousValue = keyProperty.enumValueIndex;
                    EditorGUI.PropertyField(entryKeyRect, keyProperty, GUIContent.none);

                    if (EditorGUI.EndChangeCheck())
                    {
                        // Check if the new value would create a duplicate
                        if (usedEnumValues.Contains(keyProperty.enumValueIndex) &&
                            keyProperty.enumValueIndex != previousValue)
                        {
                            // Revert to previous value
                            keyProperty.enumValueIndex = previousValue;
                            Debug.LogWarning("Cannot use the same Animation State Type twice");
                        }
                        else
                        {
                            // Update the used values set
                            usedEnumValues.Remove(previousValue);
                            usedEnumValues.Add(keyProperty.enumValueIndex);

                            // Update last selected value
                            _lastSelectedEnumValue[property.propertyPath] = keyProperty.enumValueIndex;
                        }
                    }

                    // Draw string field
                    GUI.SetNextControlName("ValueField" + i);
                    EditorGUI.PropertyField(entryValueRect, valueProperty, GUIContent.none, true);

                    // Draw remove button
                    if (GUI.Button(entryButtonRect, "-"))
                    {
                        usedEnumValues.Remove(keyProperty.enumValueIndex);
                        keysProperty.DeleteArrayElementAtIndex(i);
                        valuesProperty.DeleteArrayElementAtIndex(i);
                        i--; // Adjust index to account for removal
                    }

                    yPos += rowHeight + EditorGUIUtility.standardVerticalSpacing;
                }

                // Add button at the bottom of the list
                Rect addButtonRect = new Rect(
                    position.x + position.width - ButtonWidth,
                    yPos,
                    ButtonWidth,
                    EditorGUIUtility.singleLineHeight);

                if (GUI.Button(addButtonRect, "+"))
                {
                    AddNewItem(property, keysProperty, valuesProperty, usedEnumValues);
                    _shouldFocusNextFrame = true;
                    _framesSinceAdd = 0;
                }

                EditorGUI.indentLevel--;
            }

            // End the property
            EditorGUI.EndProperty();
        }

        private static bool CanDrawDictionary(SerializedProperty keysProperty, SerializedProperty valuesProperty)
        {
            return keysProperty != null && valuesProperty != null &&
                keysProperty.isArray && valuesProperty.isArray &&
                (keysProperty.arraySize == 0 ||
                    keysProperty.GetArrayElementAtIndex(0).propertyType == SerializedPropertyType.Enum);
        }

        private static float GetRowHeight(SerializedProperty keyProperty, SerializedProperty valueProperty)
        {
            return Mathf.Max(
                EditorGUI.GetPropertyHeight(keyProperty),
                EditorGUI.GetPropertyHeight(valueProperty),
                EditorGUIUtility.singleLineHeight
                );
        }

        // Helper method to add a new item
        private static void AddNewItem(SerializedProperty property, SerializedProperty keysProperty,
            SerializedProperty valuesProperty, HashSet<int> usedEnumValues)
        {
            int nextEnumValue = GetNextAvailableEnumValue(property, keysProperty, usedEnumValues);

            // Add new entry immediately
            keysProperty.arraySize++;
            valuesProperty.arraySize++;

            // Set values to next available enum value and empty string
            keysProperty.GetArrayElementAtIndex(keysProperty.arraySize - 1).enumValueIndex = nextEnumValue;
            ResetPropertyValue(valuesProperty.GetArrayElementAtIndex(valuesProperty.arraySize - 1));

            // Update last selected value
            _lastSelectedEnumValue[property.propertyPath] = nextEnumValue;

            // Schedule focus to be set in next frame (direct focus doesn't work when adding items)
            EditorApplication.delayCall += () =>
            {
                EditorGUI.FocusTextInControl("EnumField" + (keysProperty.arraySize - 1));
            };
        }

        private static void ResetPropertyValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Enum:
                    property.intValue = 0;
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = false;
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = 0f;
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = string.Empty;
                    break;
                case SerializedPropertyType.Color:
                    property.colorValue = default;
                    break;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = null;
                    break;
                case SerializedPropertyType.Vector2:
                    property.vector2Value = default;
                    break;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = default;
                    break;
                case SerializedPropertyType.Vector4:
                    property.vector4Value = default;
                    break;
                case SerializedPropertyType.Quaternion:
                    property.quaternionValue = default;
                    break;
                case SerializedPropertyType.Rect:
                    property.rectValue = default;
                    break;
                case SerializedPropertyType.AnimationCurve:
                    property.animationCurveValue = null;
                    break;
                case SerializedPropertyType.Bounds:
                    property.boundsValue = default;
                    break;
                case SerializedPropertyType.BoundsInt:
                    property.boundsIntValue = default;
                    break;
                case SerializedPropertyType.Vector2Int:
                    property.vector2IntValue = default;
                    break;
                case SerializedPropertyType.Vector3Int:
                    property.vector3IntValue = default;
                    break;
                case SerializedPropertyType.RectInt:
                    property.rectIntValue = default;
                    break;
            }
        }

        // Helper method to find the next available enum value
        private static int GetNextAvailableEnumValue(SerializedProperty property, SerializedProperty keysProperty,
            HashSet<int> usedEnumValues)
        {
            int nextValue = _lastSelectedEnumValue[property.propertyPath] + 1;

            string[] names = keysProperty.arraySize > 0
                ? keysProperty.GetArrayElementAtIndex(0).enumDisplayNames
                : Array.Empty<string>();

            if (names.Length == 0)
            {
                return 0;
            }

            if (nextValue >= names.Length)
            {
                nextValue = 0;
            }

            // Find the first unused value starting from nextValue
            int startValue = nextValue;
            while (usedEnumValues.Contains(nextValue))
            {
                nextValue++;

                // If we wrap around completely, wrap back to 0
                if (nextValue >= names.Length)
                {
                    nextValue = 0;
                }

                // If we've checked all enum values and came back to where we started,
                // then just use the starting value (this should rarely happen)
                if (nextValue == startValue)
                {
                    break;
                }
            }

            return nextValue;
        }
    }
}
