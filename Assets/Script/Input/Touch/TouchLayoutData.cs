using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Core.Input;
using YARG.Helpers;

namespace YARG.Input.Touch
{
    /// <summary>
    /// One draggable touch button: normalized center position (0..1 of the
    /// screen) and normalized size (fraction of the screen).
    /// Stored as JSON so the layout survives restarts and updates.
    /// </summary>
    [Serializable]
    public class TouchButtonLayout
    {
        /// <summary>Stable id, e.g. "FretGreen", "FretRed", "Strum".</summary>
        public string Id;

        /// <summary>Normalized X of the button center (0 = left, 1 = right).</summary>
        public float CenterX;

        /// <summary>Normalized Y of the button center (0 = bottom, 1 = top).</summary>
        public float CenterY;

        /// <summary>Normalized width (fraction of screen width).</summary>
        public float Width;

        /// <summary>Normalized height (fraction of screen height).</summary>
        public float Height;
    }

    /// <summary>
    /// Persisted layout of the Android touch controls.
    /// File: <c>&lt;persistentDataPath&gt;/release/touchLayout.json</c>
    /// (same folder as YARG's own <c>settings.json</c>).
    /// Positions are normalized so the layout survives rotation / resolution
    /// changes; sizes are stored separately per button so each button can be
    /// resized independently in edit mode.
    /// </summary>
    [Serializable]
    public class TouchLayoutData
    {
        public const string FileName = "touchLayout.json";

        /// <summary>Schema version, bump when the layout format changes.</summary>
        public int Version = 1;

        public List<TouchButtonLayout> Buttons = new List<TouchButtonLayout>();

        /// <summary>Default layout (landscape phone, frets bottom-left to center, strum bottom-right).</summary>
        public static TouchLayoutData Default()
        {
            // Fret buttons: a row along the bottom, each ~11% of screen width.
            // Strum: a wide bar at the bottom-right, easy to hit with the thumb.
            // Y is measured from the bottom so the row hugs the bottom edge.
            return new TouchLayoutData
            {
                Buttons = new List<TouchButtonLayout>
                {
                    new TouchButtonLayout { Id = TouchButtonIds.FretGreen,  CenterX = 0.10f, CenterY = 0.10f, Width = 0.11f, Height = 0.20f },
                    new TouchButtonLayout { Id = TouchButtonIds.FretRed,    CenterX = 0.22f, CenterY = 0.10f, Width = 0.11f, Height = 0.20f },
                    new TouchButtonLayout { Id = TouchButtonIds.FretYellow, CenterX = 0.34f, CenterY = 0.10f, Width = 0.11f, Height = 0.20f },
                    new TouchButtonLayout { Id = TouchButtonIds.FretBlue,   CenterX = 0.46f, CenterY = 0.10f, Width = 0.11f, Height = 0.20f },
                    new TouchButtonLayout { Id = TouchButtonIds.FretOrange, CenterX = 0.58f, CenterY = 0.10f, Width = 0.11f, Height = 0.20f },
                    new TouchButtonLayout { Id = TouchButtonIds.Strum,      CenterX = 0.86f, CenterY = 0.10f, Width = 0.20f, Height = 0.20f },
                },
            };
        }

        public TouchButtonLayout GetButton(string id)
        {
            foreach (var button in Buttons)
            {
                if (button.Id == id)
                    return button;
            }

            return null;
        }

        public static string FilePath => Path.Combine(PathHelper.PersistentDataPath, FileName);

        public static TouchLayoutData Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return Default();

                string json = File.ReadAllText(FilePath);
                var data = JsonConvert.DeserializeObject<TouchLayoutData>(json);
                if (data == null || data.Buttons == null || data.Buttons.Count == 0)
                    return Default();

                // Validate: every expected button must exist exactly once, inside the screen.
                var defaults = Default();
                foreach (var expected in defaults.Buttons)
                {
                    var found = data.GetButton(expected.Id);
                    if (found == null)
                    {
                        data.Buttons.Add(expected);
                        continue;
                    }

                    found.CenterX = Mathf.Clamp(found.CenterX, 0f, 1f);
                    found.CenterY = Mathf.Clamp(found.CenterY, 0f, 1f);
                    found.Width   = Mathf.Clamp(found.Width,   0.03f, 0.5f);
                    found.Height  = Mathf.Clamp(found.Height,  0.03f, 0.5f);
                }

                return data;
            }
            catch (Exception)
            {
                // Corrupt layout file must never break the game.
                return Default();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(PathHelper.PersistentDataPath);
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception)
            {
                // Saving the layout must never break gameplay.
            }
        }
    }

    /// <summary>Stable ids for the six touch buttons.</summary>
    public static class TouchButtonIds
    {
        public const string FretGreen  = "FretGreen";
        public const string FretRed    = "FretRed";
        public const string FretYellow = "FretYellow";
        public const string FretBlue   = "FretBlue";
        public const string FretOrange = "FretOrange";
        public const string Strum      = "Strum";

        public static GuitarAction ActionFor(string id)
        {
            switch (id)
            {
                case FretGreen:  return GuitarAction.GreenFret;
                case FretRed:    return GuitarAction.RedFret;
                case FretYellow: return GuitarAction.YellowFret;
                case FretBlue:   return GuitarAction.BlueFret;
                case FretOrange: return GuitarAction.OrangeFret;
                case Strum:      return GuitarAction.StrumDown;
                default:         return GuitarAction.GreenFret;
            }
        }

        public static bool IsStrum(string id) => id == Strum;
    }
}
