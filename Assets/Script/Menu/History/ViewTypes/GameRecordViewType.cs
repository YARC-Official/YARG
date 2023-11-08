﻿using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core.Song;
using YARG.Scores;
using YARG.Song;

namespace YARG.Menu.History
{
    public class GameRecordViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Normal;

        public override bool UseFullContainer => true;

        private readonly GameRecord _gameRecord;
        private readonly SongMetadata _songMetadata;

        public GameRecordViewType(GameRecord gameRecord)
        {
            _gameRecord = gameRecord;

            var songsByHash = GlobalVariables.Instance.SongContainer.SongsByHash;
            _songMetadata = songsByHash.GetValueOrDefault(new HashWrapper(gameRecord.SongChecksum))[0];
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(_gameRecord.SongName, TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            return FormatAs(_gameRecord.SongArtist, TextType.Secondary, selected);
        }

        public override async UniTask<Sprite> GetIcon()
        {
            // TODO: Show "song missing" icon instead
            if (_songMetadata is null) return null;

            return await SongSources.SourceToIcon(_songMetadata.Source);
        }

        public override GameInfo? GetGameInfo()
        {
            return new GameInfo
            {
                BandScore = _gameRecord.BandScore,
                BandStars = _gameRecord.BandStars
            };
        }
    }
}