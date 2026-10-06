using System.Collections.Generic;
using System.Linq;
using YARG.Career;
using YARG.Localization;
using YARG.Player;

namespace YARG.Menu.Career
{
    public class CareerHeaderViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Category;

        public override bool IsClickable => false;

        private readonly CareerPreset _career;
        private readonly CareerEvaluation _evaluation;
        private readonly CareerEvaluation.TierResult _currentTier;
        private readonly string _playerNames;

        public CareerHeaderViewType(CareerPreset career, CareerEvaluation evaluation)
        {
            _career = career;
            _evaluation = evaluation;
            _currentTier = FindCurrentTier(evaluation);
            _playerNames = ResolvePlayerNames();
        }

        public override CareerInfo? GetCareerInfo()
        {
            var (earned, available) = TierStars(_currentTier);
            var (totalEarned, totalAvailable) = RunStars(_evaluation);

            return new CareerInfo
            {
                Kind               = CareerRowKind.CareerHeader,
                Unlocked           = true,
                Completed          = _evaluation?.Tiers.All(t => t.Completed) ?? false,
                EarnedStars        = earned,
                AvailableStars     = available,
                TotalEarnedStars   = totalEarned,
                TotalAvailableStars = totalAvailable,
                PlayerNames        = _playerNames,
                TierName           = TierDisplayName(_currentTier),
            };
        }

        public override string GetPrimaryText(bool selected)
        {
            var names = string.IsNullOrEmpty(_playerNames)
                ? Localize.Key("Menu.Career.NoPlayers")
                : _playerNames;

            return FormatAs(Localize.KeyFormat("Menu.Career.HeaderPlayers", names),
                TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            var tier = TierDisplayName(_currentTier);

            return FormatAs(string.IsNullOrEmpty(tier)
                    ? _career.Name
                    : Localize.KeyFormat("Menu.Career.HeaderTier", tier),
                TextType.Secondary, selected);
        }

        private static CareerEvaluation.TierResult FindCurrentTier(CareerEvaluation evaluation)
        {
            if (evaluation?.Tiers is null || evaluation.Tiers.Count == 0)
            {
                return null;
            }

            var unlocked = evaluation.Tiers.Where(tier => tier.Unlocked).ToList();
            if (unlocked.Count == 0)
            {
                return null;
            }

            return unlocked.FirstOrDefault(tier => !tier.Completed) ?? unlocked[^1];
        }

        private static (int earned, int available) TierStars(CareerEvaluation.TierResult tier)
        {
            if (tier is null)
            {
                return (0, 0);
            }

            return (tier.StarSum, tier.Songs.Count * STARS_PER_SONG);
        }

        private static (int earned, int available) RunStars(CareerEvaluation evaluation)
        {
            if (evaluation?.Tiers is null)
            {
                return (0, 0);
            }

            return (evaluation.Tiers.Sum(tier => tier.StarSum),
                evaluation.Tiers.Sum(tier => tier.Songs.Count) * STARS_PER_SONG);
        }

        private string TierDisplayName(CareerEvaluation.TierResult tier)
        {
            if (tier is null)
            {
                return string.Empty;
            }

            return _career.Tiers[tier.TierIndex].Name;
        }

        private static string ResolvePlayerNames()
        {
            var names = PlayerContainer.Players
                .Where(player => !player.SittingOut && !player.Profile.IsBot)
                .Select(player => player.Profile.Name)
                .ToList();

            return names.Count == 0 ? string.Empty : Localize.List(names);
        }
    }
}
