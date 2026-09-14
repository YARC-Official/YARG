using UnityEditor;
using UnityEngine;
using System.Linq;

namespace Minis
{
    //
    // Custom inspector for MidiDeviceAssigner
    //
    [CustomEditor(typeof(MidiDeviceAssigner)), CanEditMultipleObjects]
    sealed class MidiDeviceAssignerEditor : Editor
    {
        SerializedProperty _channel;
        SerializedProperty _productName;

        static class Channel
        {
            public static readonly GUIContent [] Labels = (new [] {
                "Any", "1", "2", "3", "4", "5", "6", "7", "8",
                "9", "10", "11", "12", "13", "14", "15", "16i"
            }).Select(s => new GUIContent(s)).ToArray();

            public static readonly int [] Numbers =
                { -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
        }

        void OnEnable()
        {
            _channel = serializedObject.FindProperty("_channel");
            _productName = serializedObject.FindProperty("_productName");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.IntPopup(_channel, Channel.Labels, Channel.Numbers);
            EditorGUILayout.PropertyField(_productName);

            EditorGUILayout.HelpBox(
                "Assigns a device whose name contains the string above "+
                "(case sensitive.) Keep empty to accept any product.",
                MessageType.None
            );

            serializedObject.ApplyModifiedProperties();
        }
    }
}
