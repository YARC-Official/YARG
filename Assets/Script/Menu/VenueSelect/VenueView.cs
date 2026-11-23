using UnityEngine;
using YARG.Menu.ListMenu;
using YARG.Venue;

namespace YARG.Menu.VenueSelect
{
    public class VenueView : ViewObject<VenueViewType>
    {
        private const float DISABLED_ALPHA = 0.6f;

        private CanvasGroup _rowCanvasGroup;

        public void PrimaryButtonClick() => ViewType?.ViewClick();

        public override void Show(bool selected, VenueViewType viewType)
        {
            base.Show(selected, viewType);

            // ViewObject resets the alpha in Show, so the disabled look has to be applied afterwards
            if (viewType != null &&
                VenueRegistry.GetAvailabilityState(viewType.Descriptor) == VenueAvailabilityState.Disabled)
            {
                RowCanvasGroup.alpha = DISABLED_ALPHA;
            }
        }

        private CanvasGroup RowCanvasGroup => _rowCanvasGroup != null ? _rowCanvasGroup : _rowCanvasGroup = GetComponent<CanvasGroup>();
    }
}
