#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Editor.Build
{
    public static class IOSBuildPostProcessor
    {
        [PostProcessBuild]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            // RtMidi (MIDI input via the embedded Minis package) is a static
            // library; CoreMIDI must be linked into the framework that hosts it.
            string frameworkTarget = project.GetUnityFrameworkTargetGuid();
            project.AddFrameworkToProject(frameworkTarget, "CoreMIDI.framework", false);

            // UTTypeFolder for the native folder picker (NativeFolderPicker.mm)
            project.AddFrameworkToProject(frameworkTarget, "UniformTypeIdentifiers.framework", false);

            // CABTMIDICentralViewController for BLE MIDI pairing (BluetoothMidiPairing.mm)
            project.AddFrameworkToProject(frameworkTarget, "CoreAudioKit.framework", false);

            project.WriteToFile(projectPath);

            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            // Let users drop songs into the app's Documents folder from the
            // Files app / Finder, and open them in place.
            plist.root.SetBoolean("UIFileSharingEnabled", true);
            plist.root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);

            // Vocals capture the microphone through BASS
            plist.root.SetString("NSMicrophoneUsageDescription",
                "YARG uses the microphone for vocals gameplay.");

            // BLE MIDI pairing (CABTMIDICentralViewController) uses CoreBluetooth;
            // iOS terminates the app on Bluetooth access without this key.
            plist.root.SetString("NSBluetoothAlwaysUsageDescription",
                "YARG uses Bluetooth to connect wireless MIDI instruments.");
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
