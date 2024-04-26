using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.Data;

namespace YARG.Menu.ListMenu
{
    public abstract class BaseViewType
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

        public abstract string GetPrimaryText(bool selected);
        public abstract string GetSecondaryText(bool selected);

        public virtual Sprite? GetIcon()
        {
            return null;
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
                    TextType.Bright    => TextColorer.StyleString(str, MenuData.Colors.BrightText, 500),
                    TextType.Primary   => TextColorer.StyleString(str, MenuData.Colors.PrimaryText),
                    TextType.Secondary => TextColorer.StyleString(str, MenuData.Colors.PrimaryText.WithAlpha(0.5f)),
                    _                  => throw new Exception("Unreachable.")
                };
            }
            else
            {
                return type switch
                {
                    TextType.Bright    => TextColorer.StyleString(str, MenuData.Colors.BrightText,  500),
                    TextType.Primary   => TextColorer.StyleString(str, MenuData.Colors.BrightText,  700),
                    TextType.Secondary => TextColorer.StyleString(str, MenuData.Colors.PrimaryText, 500),
                    _                  => throw new Exception("Unreachable.")
                };
            }
        }
    }
}