using Cysharp.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Helpers;
using YARG.Menu.Data;

namespace YARG.Menu.MusicLibrary
{
    /// <summary>
    /// A visual grouping label within a primary sort category. Secondary headers
    /// are deliberately skipped by list navigation.
    /// </summary>
    public sealed class SecondaryHeaderViewType : ViewType
    {
        private readonly string _text;
        private readonly int _songCount;
        private readonly string _iconPath;
        private readonly Sprite _icon;
        private static readonly Dictionary<string, Sprite> IconCache = new();

        public override BackgroundType Background => BackgroundType.SecondaryHeader;
        public override bool IsSelectable => false;
        public override bool UseWiderPrimaryText => true;
        public override string StableId => $"SecondaryHeader:{_text}";
        public int TotalStarsCount { get; set; }
        public bool HasGoldStars { get; set; }

        public SecondaryHeaderViewType(string text, int songCount, string iconPath = null)
        {
            _text = text;
            _songCount = songCount;
            _iconPath = iconPath;
        }

        public SecondaryHeaderViewType(string text, int songCount, Sprite icon)
        {
            _text = text;
            _songCount = songCount;
            _icon = icon;
        }

#nullable enable
        public override Sprite? GetIcon()
#nullable disable
        {
            if (_icon != null)
                return _icon;

            if (string.IsNullOrEmpty(_iconPath))
                return null;

            if (!IconCache.TryGetValue(_iconPath, out var icon))
            {
                IconCache[_iconPath] = icon = Addressables.LoadAssetAsync<Sprite>(_iconPath).WaitForCompletion();
            }

            return icon;
        }

        public override string GetPrimaryText(bool selected)
        {
            return TextColorer.StyleString(_text, MenuData.Colors.HeaderPrimary, 550);
        }

        public override string GetSecondaryText(bool selected)
        {
            var count = TextColorer.StyleString(
                ZString.Format("{0:N0}", _songCount),
                MenuData.Colors.HeaderSecondary,
                500);
            var label = TextColorer.StyleString(
                _songCount == 1 ? "SONG" : "SONGS",
                MenuData.Colors.HeaderTertiary,
                550);
            return ZString.Concat(count, " ", label);
        }

        public override string GetSideText(bool selected)
        {
            var obtainedStars = TextColorer.StyleString(
                ZString.Format("{0}", TotalStarsCount),
                MenuData.Colors.HeaderSecondary,
                600);

            var totalStars = TextColorer.StyleString(
                ZString.Format(" / {0}", _songCount * 5),
                MenuData.Colors.HeaderTertiary,
                550);

            return ZString.Concat(obtainedStars, totalStars);
        }
    }
}
