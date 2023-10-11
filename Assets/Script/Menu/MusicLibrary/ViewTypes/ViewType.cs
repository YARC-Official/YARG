using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.Data;

namespace YARG.Menu.MusicLibrary
{
    public abstract class ViewType
    {
        public enum BackgroundType
        {
            Normal,
            Category
        }

        protected enum TextType
        {
            Bright,
            Primary,
            Secondary
        }

        public abstract BackgroundType Background { get; }
        public virtual bool UseAsMadeFamousBy => false;

        public abstract string GetPrimaryText(bool selected);

        public virtual string GetSecondaryText(bool selected)
        {
            return string.Empty;
        }

        public virtual string GetSideText(bool selected)
        {
            return string.Empty;
        }

        public virtual UniTask<Sprite> GetIcon()
        {
            return UniTask.FromResult<Sprite>(null);
        }

        public virtual void SecondaryTextClick()
        {
        }

        public virtual void PrimaryButtonClick()
        {
        }

        public virtual void IconClick()
        {
        }

        protected static string FormatAs(string str, TextType type, bool selected)
        {
            if (!selected)
            {
                return type switch
                {
                    TextType.Bright    => TextColorer.FormatString(str, MenuData.Colors.BrightText, 500),
                    TextType.Primary   => TextColorer.FormatString(str, MenuData.Colors.PrimaryText),
                    TextType.Secondary => TextColorer.FormatString(str, MenuData.Colors.PrimaryText.WithAlpha(0.5f)),
                    _                  => throw new Exception("Unreachable.")
                };
            }
            else
            {
                return type switch
                {
                    TextType.Bright    => TextColorer.FormatString(str, MenuData.Colors.BrightText,  500),
                    TextType.Primary   => TextColorer.FormatString(str, MenuData.Colors.BrightText,  700),
                    TextType.Secondary => TextColorer.FormatString(str, MenuData.Colors.PrimaryText, 500),
                    _                  => throw new Exception("Unreachable.")
                };
            }
        }
    }
}