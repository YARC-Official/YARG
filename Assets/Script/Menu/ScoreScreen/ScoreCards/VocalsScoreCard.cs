using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Core.Engine.Vocals;

namespace YARG.Menu.ScoreScreen
{
    public class VocalsScoreCard : ScoreCard<VocalsStats>
    {
        public override void SetCardContents()
        {
            base.SetCardContents();

            // Set background icon
            _instrumentIcon.sprite = Addressables
                .LoadAssetAsync<Sprite>($"InstrumentIcons[vocals]")
                .WaitForCompletion();
        }
    }
}