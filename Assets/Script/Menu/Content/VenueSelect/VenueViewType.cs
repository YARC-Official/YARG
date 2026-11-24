using System;
using YARG.Menu.ListMenu;
using YARG.Settings;
using YARG.Venue;

namespace YARG.Menu.Content.VenueSelect
{
    public class VenueViewType : BaseViewType
    {
        // The bundled font atlases have no pin/star glyph, so the marker uses a plain bullet
        private const string PIN_INDICATOR = "\u2022 ";

        public VenueDescriptor Descriptor { get; }

        private readonly Action<VenueDescriptor> _onClick;

        public VenueViewType(VenueDescriptor descriptor, Action<VenueDescriptor> onClick)
        {
            Descriptor = descriptor;
            _onClick = onClick;
        }

        public override BackgroundType Background => BackgroundType.Normal;

        public override string GetPrimaryText(bool selected)
        {
            var state = VenueRegistry.GetAvailabilityState(Descriptor);
            var type = state == VenueAvailabilityState.Disabled ? TextType.Secondary : TextType.Bright;

            var name = Descriptor.DisplayName;

            return FormatAs(name, type, selected);
        }

        public override string GetSecondaryText(bool selected) => string.Empty;

        public void ViewClick()
        {
            _onClick?.Invoke(Descriptor);
        }

        public static string GetSourceKey(VenueEntrySource source)
        {
            return source switch
            {
                VenueEntrySource.Addressable  => "Menu.Content.VenueSelect.Sources.Remote",
                VenueEntrySource.LauncherFile => "Menu.Content.VenueSelect.Sources.Launcher",
                _                             => "Menu.Content.VenueSelect.Sources.Local",
            };
        }

        public static string GetPinActionKey(VenueDescriptor venue)
        {
            if (venue != null && VenueRegistry.IsPinned(venue.Info))
            {
                return "Menu.Content.VenueSelect.Actions.Unpin";
            }

            return "Menu.Content.VenueSelect.Actions.Pin";
        }

        public static string GetAvailabilityActionKey(VenueDescriptor venue)
        {
            if (venue == null)
            {
                return "Menu.Content.VenueSelect.Actions.Disable";
            }

            if (!venue.ExcludeFromRandomByDefault)
            {
                return VenueRegistry.IsHidden(venue.Info)
                    ? "Menu.Content.VenueSelect.Actions.Enable"
                    : "Menu.Content.VenueSelect.Actions.Disable";
            }

            return VenueRegistry.GetAvailabilityState(venue) switch
            {
                VenueAvailabilityState.HintOnly => "Menu.Content.VenueSelect.Actions.IncludeInRandom",
                VenueAvailabilityState.Enabled  => "Menu.Content.VenueSelect.Actions.Disable",
                _                               => "Menu.Content.VenueSelect.Actions.RestoreHintOnly",
            };
        }
    }
}
