using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using YARG.Core.Game;
using YARG.Core.Utility;
using YARG.Helpers;

namespace YARG.Settings.Customization
{
    // TODO: COLOR PROFILE
    // public class ColorProfileContainer : CustomContent<ColorProfile>
    // {
    //     public override IEnumerable<ColorProfile> DefaultPresets => null;
    //     public override IEnumerable<string> DefaultPresetNames => DefaultPresets.Select(i => i.Name);
    //
    //     public ColorProfileContainer(string contentDirectory) : base(contentDirectory)
    //     {
    //         Content.Add(ColorProfile.Default.Name, ColorProfile.Default);
    //     }
    //
    //     public ColorProfile GetColorProfileOrDefault(string name)
    //     {
    //         if (Content.TryGetValue(name, out var profile))
    //         {
    //             return profile;
    //         }
    //
    //         return ColorProfile.Default;
    //     }
    //
    //     public override void LoadFiles()
    //     {
    //         Content.Clear();
    //         Content.Add(ColorProfile.Default.Name, ColorProfile.Default);
    //
    //         PathHelper.SafeEnumerateFiles(ContentDirectory, "*.json", true, (path) =>
    //         {
    //             var colors = JsonConvert.DeserializeObject<ColorProfile>(File.ReadAllText(path),
    //                 new JsonColorConverter());
    //
    //             Content.TryAdd(colors.Name, colors);
    //
    //             return true;
    //         });
    //     }
    //
    //     public override void SaveItem(ColorProfile item)
    //     {
    //         var json = JsonConvert.SerializeObject(item, Formatting.Indented, new JsonColorConverter());
    //
    //         File.WriteAllText(Path.Combine(ContentDirectory, $"{item.Name.ToLower().Replace(" ", "")}.json"), json);
    //     }
    //
    //     public override void SetSettingsFromPreset(ColorProfile preset)
    //     {
    //         throw new System.NotImplementedException();
    //     }
    //
    //     public override void SetPresetFromSettings(ColorProfile preset)
    //     {
    //         throw new System.NotImplementedException();
    //     }
    // }
}