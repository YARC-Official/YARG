using System;
using System.Collections.Generic;

// pattern: Functional Core

namespace YARG.Settings.Preview
{
    public readonly struct ProKeysOverlayBand
    {
        public ProKeysOverlayBand(float center, float width, int group, bool useLeftEdge, bool useRightEdge)
        {
            Center = center;
            Width = width;
            Group = group;
            UseLeftEdge = useLeftEdge;
            UseRightEdge = useRightEdge;
        }

        public float Center { get; }
        public float Width { get; }
        public int Group { get; }
        public bool UseLeftEdge { get; }
        public bool UseRightEdge { get; }
    }

    public readonly struct ProKeysOverlayLayer
    {
        public ProKeysOverlayLayer(ProKeysOverlayBand band, bool isEdge, bool flipX)
        {
            Band = band;
            IsEdge = isEdge;
            FlipX = flipX;
        }

        public ProKeysOverlayBand Band { get; }
        public bool IsEdge { get; }
        public bool FlipX { get; }
    }

    public static class ProKeysPreviewLayout
    {
        public const int WHITE_KEY_COUNT = 10;
        public const int OVERLAY_GROUP_COUNT = 5;

        public static ProKeysOverlayBand[] CreateOverlayBands(float trackWidth, float laneGap)
        {
            if (trackWidth <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(trackWidth));
            }

            float spacing = trackWidth / WHITE_KEY_COUNT;
            if (laneGap < 0f || laneGap >= spacing)
            {
                throw new ArgumentOutOfRangeException(nameof(laneGap));
            }

            float firstCenter = -trackWidth / 2f + spacing / 2f;
            float bandWidth = spacing - laneGap;
            var bands = new ProKeysOverlayBand[WHITE_KEY_COUNT];

            for (int lane = 0; lane < WHITE_KEY_COUNT; lane++)
            {
                bool isFirstLaneInGroup = lane % 2 == 0;
                bands[lane] = new ProKeysOverlayBand(
                    firstCenter + lane * spacing,
                    bandWidth,
                    lane / 2,
                    isFirstLaneInGroup,
                    !isFirstLaneInGroup);
            }

            return bands;
        }

        public static ProKeysOverlayLayer[] CreateOverlayLayers(float trackWidth, float laneGap)
        {
            var bands = CreateOverlayBands(trackWidth, laneGap);
            var layers = new List<ProKeysOverlayLayer>(bands.Length * 2);

            foreach (var band in bands)
            {
                layers.Add(new ProKeysOverlayLayer(band, false, false));
                if (band.UseLeftEdge)
                {
                    layers.Add(new ProKeysOverlayLayer(band, true, false));
                }

                if (band.UseRightEdge)
                {
                    layers.Add(new ProKeysOverlayLayer(band, true, true));
                }
            }

            return layers.ToArray();
        }
    }
}
