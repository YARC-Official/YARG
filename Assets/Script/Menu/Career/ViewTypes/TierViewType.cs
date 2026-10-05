using System;
using YARG.Career;
using YARG.Localization;

namespace YARG.Menu.Career
{
    /// <summary>
    /// A tier header row. Unlocked tiers report how much of the tier's star total has been earned;
    /// the next locked tier reports what is still required to reach it and cannot be clicked.
    /// </summary>
    public class TierViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Category;

        private readonly CareerTier                _tier;
        private readonly CareerEvaluation.TierResult _result;
        private readonly string                    _lockHint;

        public TierViewType(CareerTier tier, CareerEvaluation.TierResult result)
        {
            _tier = tier;
            _result = result;
            IsLocked = _result is null || !_result.Unlocked;
            _lockHint = IsLocked ? BuildLockHint() : null;
        }

        private bool IsLocked { get; }

        // A locked tier is still selectable so the list can be browsed, but it starts nothing.
        public override bool IsClickable => !IsLocked;

        public override CareerInfo? GetCareerInfo()
        {
            return new CareerInfo
            {
                Kind           = IsLocked ? CareerRowKind.LockedTierHeader : CareerRowKind.TierHeader,
                Unlocked       = !IsLocked,
                Completed      = _result?.Completed ?? false,
                EarnedStars    = _result?.StarSum ?? 0,
                AvailableStars = _tier.Songs?.Length * STARS_PER_SONG ?? 0,
                TierName       = _tier.Name,
                LockHint       = _lockHint,
            };
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(_tier.Name, TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            string text;

            if (IsLocked)
            {
                text = _lockHint;
            }
            else
            {
                text = _tier.UnlockType switch
                {
                    UnlockType.StarCount => Localize.KeyFormat("Menu.Career.StarProgress", _result?.StarSum ?? 0, _tier.Songs?.Length * STARS_PER_SONG ?? 0),
                    UnlockType.CompletionCount => Localize.KeyFormat("Menu.Career.StarProgress", _result?.RemainingToUnlock ?? 0, _tier.Songs?.Length ?? 0),
                    _ => "Unknown Unlock Type (how?)"
                };
            }

            return FormatAs(text, TextType.Secondary, selected);
        }

        /// <summary>
        /// What is still missing for this tier.
        /// </summary>
        private string BuildLockHint()
        {
            var remaining = Math.Max(0, _result?.RemainingToUnlock ?? 0);

            return _tier.UnlockType switch
            {
                UnlockType.StarCount =>
                    Localize.KeyFormat("Menu.Career.UnlockStars", remaining),
                UnlockType.CompletionCount =>
                    Localize.KeyFormat("Menu.Career.UnlockSongs", remaining),
                _ => Localize.Key("Menu.Career.Locked"),
            };
        }

        public override string ToString()
        {
            return $"TierViewType: {_tier.Name}{(IsLocked ? " (locked)" : "")}";
        }
    }
}
