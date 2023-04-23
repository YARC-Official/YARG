// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using UnityEngine;

/// <summary>
/// Loads and processes any persistent data, like save data, as well as just convenient defines and properties that exists outside of the "Chart Editor" concept.
/// </summary>
public class Globals : MonoBehaviour {
    public static readonly string applicationBranchName = "";

    public static readonly string LINE_ENDING = "\r\n";
    public const string TABSPACE = "  ";
    public static string autosaveLocation;
    static string workingDirectory = string.Empty;
    public static string realWorkingDirectory { get { return workingDirectory; } }

    public static readonly string[] validAudioExtensions = { ".ogg", ".wav", ".mp3", ".opus" };
    public static readonly string[] validTextureExtensions = { ".jpg", ".png" };
    public static string[] localEvents = { };
    public static string[] globalEvents = { };
}
