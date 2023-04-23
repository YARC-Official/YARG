// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

namespace MoonscraperChartEditor.Song.IO
{
    public class SortableBytes
    {
        public uint tick;
        public byte[] bytes;

        public SortableBytes()
        {
            tick = 0;
            bytes = new byte[0];
        }

        public SortableBytes(uint tick, byte[] bytes)
        {
            this.tick = tick;
            this.bytes = bytes;
        }

        public static void Sort(SortableBytes[] bytes)
        {
            MergeSort(bytes, 0, bytes.Length - 1);
        }

        public static SortableBytes[] MergeAlreadySorted(SortableBytes[] a, SortableBytes[] b)
        {
            SortableBytes[] merged = new SortableBytes[a.Length + b.Length];
            int i = 0, j = 0;

            for (int k = 0; k < merged.Length; ++k)
            {
                SortableBytes selected;

                if (i >= a.Length)
                    selected = b[j++];
                else if (j >= b.Length)
                    selected = a[i++];
                else
                    selected = a[i].tick < b[j].tick ? a[i++] : b[j++];

                merged[k] = selected;
            }

            return merged;
        }

        static void MergeSort(SortableBytes[] bytes, int left, int right)
        {
            int mid;

            if (right > left)
            {
                mid = (right + left) / 2;

                MergeSort(bytes, left, mid);
                MergeSort(bytes, mid + 1, right);

                Merge(bytes, left, (mid + 1), right);
            }

        }

        static void Merge(SortableBytes[] bytes, int left, int mid, int right)
        {
            SortableBytes[] temp = new SortableBytes[bytes.Length];
            int i, eol, num, pos;

            eol = (mid - 1);
            pos = left;
            num = (right - left + 1);

            while ((left <= eol) && (mid <= right))
            {
                if (bytes[left].tick <= bytes[mid].tick)
                    temp[pos++] = bytes[left++];
                else
                    temp[pos++] = bytes[mid++];
            }

            while (left <= eol)
                temp[pos++] = bytes[left++];

            while (mid <= right)
                temp[pos++] = bytes[mid++];

            for (i = 0; i < num; i++)
            {
                bytes[right] = temp[right];
                right--;
            }
        }
    }
}
