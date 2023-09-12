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
        public override ColorProfile Default => ColorProfile.Default;

        public ColorProfileContainer(string contentDirectory) : base(contentDirectory)
        {
            Content.Add(Default.Id, Default);
        }

        public override void LoadFiles()
        {
            Content.Clear();
            Content.Add(Default.Id, Default);

            PathHelper.SafeEnumerateFiles(ContentDirectory, "*.json", true, (path) =>
            {
                var colors = JsonConvert.DeserializeObject<ColorProfile>(File.ReadAllText(path),
                    new JsonColorConverter());

                Content.TryAdd(colors.Id, colors);

                return true;
            });
        }

        public override void SaveItem(ColorProfile item)
        {
            Debug.Log($"Saving color profile {item.Name}");
            var json = JsonConvert.SerializeObject(item, Formatting.Indented, new JsonColorConverter());

            File.WriteAllText(Path.Combine(ContentDirectory, $"{item.Name.ToLower().Replace(" ", "")}.json"), json);
        }
    }
}