using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Core.Engine.Vocals;
using YARG.Helpers.Extensions;

namespace YARG.Menu.ScoreScreen
{
    public class VocalsScoreCard : ScoreCard<VocalsStats>
    {
        // The hit-offset histogram is meaningless for vocals (graded per phrase, not per note);
        // we render a phrase summary in its place instead.
        protected override bool ShouldShowOffsetHistogram => false;

        private IReadOnlyList<float> _phrasePercents;
        private int _percussionHits;
        private int _percussionTotal;

        public void SetPhrasePercents(IReadOnlyList<float> percents)
        {
            _phrasePercents = percents;
        }

        public void SetPercussion(int hits, int total)
        {
            _percussionHits = hits;
            _percussionTotal = total;
        }

        public override void SetCardContents()
        {
            base.SetCardContents();

            // Set background icon
            _instrumentIcon.sprite = Addressables
                .LoadAssetAsync<Sprite>($"InstrumentIcons[{Player.Profile.CurrentInstrument.ToResourceName()}]")
                .WaitForCompletion();

            // Build the phrase histogram + tally into the Advanced view (advanced-only automatically).
            // Renders nothing if the list is null/empty.
            VocalsPhraseHistogram.Build(AdvancedStatsRect, _phrasePercents, CreateStatLabel, AdvancedAccentColor,
                _percussionHits, _percussionTotal);
        }
    }
}
