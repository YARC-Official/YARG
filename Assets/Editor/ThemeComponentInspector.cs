using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using YARG.Themes;

namespace Editor
{
    [CustomEditor(typeof(ThemeComponent))]
    public class ThemeComponentInspector : UnityEditor.Editor
    {
        private SerializedProperty _fiveFretNotes;
        private SerializedProperty _fourLaneNotes;
        private SerializedProperty _fiveLaneNotes;
        private SerializedProperty _proKeysNotes;
        private SerializedProperty _fiveFretFret;
        private SerializedProperty _fourLaneFret;
        private SerializedProperty _fiveLaneFret;
        private SerializedProperty _whiteKey;
        private SerializedProperty _blackKey;
        private SerializedProperty _kickFret;

        private void OnEnable()
        {
            _fiveFretNotes = serializedObject.FindProperty("_fiveFretNotes");
            _fourLaneNotes = serializedObject.FindProperty("_fourLaneNotes");
            _fiveLaneNotes = serializedObject.FindProperty("_fiveLaneNotes");
            _proKeysNotes = serializedObject.FindProperty("_proKeysNotes");
            _fiveFretFret = serializedObject.FindProperty("_fiveFretFret");
            _fourLaneFret = serializedObject.FindProperty("_fourLaneFret");
            _fiveLaneFret = serializedObject.FindProperty("_fiveLaneFret");
            _whiteKey = serializedObject.FindProperty("_whiteKey");
            _blackKey = serializedObject.FindProperty("_blackKey");
            _kickFret = serializedObject.FindProperty("_kickFret");
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            root.Add(new Label("\n<b><size=1.25em>Theme Component</size></b>\n"));
            root.Add(new Label("Configure note models, frets, and keys for this theme. " +
                "Export to create a .yargtheme file for sharing.\n")
            {
                style = { whiteSpace = WhiteSpace.Normal }
            });

            // Validation warnings
            var warnings = new VisualElement();
            var noteParents = new[] { _fiveFretNotes, _fourLaneNotes, _fiveLaneNotes, _proKeysNotes };
            if (!noteParents.Any(p => p.objectReferenceValue != null))
            {
                warnings.Add(new HelpBox("No note parents assigned. Theme will have no note models.", HelpBoxMessageType.Warning));
            }

            var fretFields = new[] { _fiveFretFret, _fourLaneFret, _fiveLaneFret };
            if (!fretFields.Any(p => p.objectReferenceValue != null))
            {
                warnings.Add(new HelpBox("No frets assigned. Theme will use default frets.", HelpBoxMessageType.Warning));
            }
            root.Add(warnings);

            root.Add(new Label("\n<b><size=1.15em>Note Parents</size></b>"));
            root.Add(new PropertyField(_fiveFretNotes));
            root.Add(new PropertyField(_fourLaneNotes));
            root.Add(new PropertyField(_fiveLaneNotes));
            root.Add(new PropertyField(_proKeysNotes));

            root.Add(new Label("\n<b><size=1.15em>Frets</size></b>"));
            root.Add(new PropertyField(_fiveFretFret));
            root.Add(new PropertyField(_fourLaneFret));
            root.Add(new PropertyField(_fiveLaneFret));

            root.Add(new Label("\n<b><size=1.15em>Keys</size></b>"));
            root.Add(new PropertyField(_whiteKey));
            root.Add(new PropertyField(_blackKey));

            root.Add(new Label("\n<b><size=1.15em>Kick</size></b>"));
            root.Add(new PropertyField(_kickFret));

            // Export button
            root.Add(new Label("\n<b><size=1.25em>Actions</size></b>\n"));

            var exportButton = new Button(() =>
            {
                if (target is ThemeComponent comp)
                {
                    comp.ExportTheme();
                }
            });
            exportButton.Add(new Label("Export Theme"));
            root.Add(exportButton);

            return root;
        }
    }
}
