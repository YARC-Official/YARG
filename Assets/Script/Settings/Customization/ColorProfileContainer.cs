using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Core.Game;
using YARG.Core.Utility;
using YARG.Helpers;

namespace YARG.Settings.Customization
{
    public class ColorProfileContainer : CustomContent<ColorProfile>
    {
        public ColorProfileContainer(string contentDirectory) : base(contentDirectory)
        {
        }

        public override void LoadFiles()
        {
            Content.Clear();

            PathHelper.SafeEnumerateFiles("*.json", true, (path) =>
            {
                var colors = JsonConvert.DeserializeObject<ColorProfile>(File.ReadAllText(path),
                    new JsonColorConverter());

                if (colors.Name != "Default")
                {
                    Content.Add(colors.Name, colors);
                }

                return true;
            });
        }

        public override void SaveItem(ColorProfile item)
        {
            var json = JsonConvert.SerializeObject(item, Formatting.Indented, new JsonColorConverter());

            File.WriteAllText(Path.Combine(ContentDirectory, $"{item.Name.ToLower().Replace(" ", "")}.json"), json);
        }
    }
}