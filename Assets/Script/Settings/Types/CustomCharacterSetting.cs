using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UniVRM10;
using YARG.Helpers;
using YARG.Settings.Customization;
using YARG.Venue.Characters;

namespace YARG.Settings.Types
{
    public class CustomCharacterSetting : DropdownSetting<string>
    {
        private VenueCharacter.CharacterType _characterType;
        private Dictionary<string, string>   _fileToName = new();

        private const string CHARACTER_FOLDER = "characters";

        public string CustomCharacterPath
        {
            get
            {
                var folder = Path.Combine(CustomContentManager.CustomizationDirectory, CHARACTER_FOLDER);

                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                return folder;
            }
        }

        public CustomCharacterSetting(string value, VenueCharacter.CharacterType characterType, Action<string> onChange = null) :
            base(value, onChange, localizable: false)
        {
            _characterType = characterType;
        }

        public override void UpdateValues()
        {
            _fileToName.Clear();
            _possibleValues.Clear();
            _possibleValues.Add(string.Empty);

            var folder = CustomCharacterPath;
            if (!Directory.Exists(folder))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(folder, "*.yargchar"))
            {
                _possibleValues.Add(file);
                _fileToName[file] = Path.GetFileNameWithoutExtension(file);
            }
        }

        public override string ValueToString(string value)
        {
            return _fileToName.GetValueOrDefault(value, "None");
        }
    }
}
