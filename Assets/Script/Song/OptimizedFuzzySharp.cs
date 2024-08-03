/*
MIT License for FuzzySharp by JakeBayer
Copyright (c) 2018 

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 */
using System;

namespace YARG.Song
{
    internal static class OptimizedFuzzySharp
    {
        private struct MatchingBlock
        {
            public int SourcePos;
            public int DestPos;
            public int Length;
        }

        private enum EditType
        {
            DELETE,
            EQUAL,
            INSERT,
            REPLACE,
            KEEP
        }

        private struct EditOp
        {
            public EditType EditType;
            public int SourcePos;
            public int DestPos;
        }

        public static double PartialRatio(ReadOnlySpan<char> argument, ReadOnlySpan<char> songStr)
        {
            ReadOnlySpan<char> shorter;
            ReadOnlySpan<char> longer;
            if (argument.Length < songStr.Length)
            {
                shorter = argument;
                longer = songStr;
            }
            else
            {
                shorter = songStr;
                longer = argument;
            }

            var matchingBlocks = GetMatchingBlocks(shorter.Length, longer.Length, GetEditOps(shorter, longer));
            double bestScore = 0;
            for (int i = 0; i < matchingBlocks.Length; ++i)
            {
                ref readonly var matchingBlock = ref matchingBlocks[i];
                int longStart = matchingBlock.DestPos - matchingBlock.SourcePos;
                if (longStart < 0)
                {
                    longStart = 0;
                }

                int longEnd = longStart + shorter.Length;
                if (longEnd > longer.Length)
                {
                    longEnd = longer.Length;
                }

                double score = GetRatio(shorter, longer[longStart..longEnd]);
                if (score > 0.995)
                {
                    return 100;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                }
            }
            return (int) Math.Round(100.0 * bestScore);
        }

        private static MatchingBlock[] GetMatchingBlocks(int len1, int len2, EditOp[] ops)
        {
            int numMatchingBlocks = 0;
            int o = 0;
            int destPos;
            int sourcePos = destPos = 0;
            for (int i = ops.Length; i != 0;)
            {
                while (ops[o].EditType == EditType.KEEP && --i != 0)
                {
                    o++;
                }

                if (i == 0)
                {
                    break;
                }

                if (sourcePos < ops[o].SourcePos || destPos < ops[o].DestPos)
                {
                    numMatchingBlocks++;
                    sourcePos = ops[o].SourcePos;
                    destPos = ops[o].DestPos;
                }

                var editType = ops[o].EditType;
                switch (editType)
                {
                    case EditType.REPLACE:
                        do
                        {
                            sourcePos++;
                            destPos++;
                            i--;
                            o++;
                        }
                        while (i != 0 && ops[o].EditType == editType && sourcePos == ops[o].SourcePos && destPos == ops[o].DestPos);
                        continue;
                    case EditType.DELETE:
                        do
                        {
                            sourcePos++;
                            i--;
                            o++;
                        }
                        while (i != 0 && ops[o].EditType == editType && sourcePos == ops[o].SourcePos && destPos == ops[o].DestPos);
                        continue;
                    case EditType.INSERT:
                        break;
                    default:
                        continue;
                }

                do
                {
                    destPos++;
                    i--;
                    o++;
                }
                while (i != 0 && ops[o].EditType == editType && sourcePos == ops[o].SourcePos && destPos == ops[o].DestPos);
            }

            if (sourcePos < len1 || destPos < len2)
            {
                numMatchingBlocks++;
            }

            var matchingBlocks = new MatchingBlock[numMatchingBlocks + 1];
            o = 0;
            sourcePos = destPos = 0;
            int mbIndex = 0;
            for (int i = ops.Length; i != 0;)
            {
                while (ops[o].EditType == EditType.KEEP && --i != 0)
                {
                    o++;
                }

                if (i == 0)
                {
                    break;
                }

                if (sourcePos < ops[o].SourcePos || destPos < ops[o].DestPos)
                {
                    ref var mb = ref matchingBlocks[mbIndex++];
                    mb.SourcePos = sourcePos;
                    mb.DestPos = destPos;
                    mb.Length = ops[o].SourcePos - sourcePos;
                    sourcePos = ops[o].SourcePos;
                    destPos = ops[o].DestPos;
                }

                var editType = ops[o].EditType;
                switch (editType)
                {
                    case EditType.REPLACE:
                        do
                        {
                            sourcePos++;
                            destPos++;
                            i--;
                            o++;
                        }
                        while (i != 0 && ops[o].EditType == editType && sourcePos == ops[o].SourcePos && destPos == ops[o].DestPos);
                        continue;
                    case EditType.DELETE:
                        do
                        {
                            sourcePos++;
                            i--;
                            o++;
                        }
                        while (i != 0 && ops[o].EditType == editType && sourcePos == ops[o].SourcePos && destPos == ops[o].DestPos);
                        continue;
                    case EditType.INSERT:
                        break;
                    default:
                        continue;
                }

                do
                {
                    destPos++;
                    i--;
                    o++;
                }
                while (i != 0 && ops[o].EditType == editType && sourcePos == ops[o].SourcePos && destPos == ops[o].DestPos);
            }

            if (sourcePos < len1 || destPos < len2)
            {
                ref var mb = ref matchingBlocks[mbIndex++];
                mb.SourcePos = sourcePos;
                mb.DestPos = destPos;
                mb.Length = len1 - sourcePos;
            }

            ref var finalBlock = ref matchingBlocks[mbIndex];
            finalBlock.SourcePos = len1;
            finalBlock.DestPos = len2;
            finalBlock.Length = 0;
            return matchingBlocks;
        }

        private static unsafe EditOp[] GetEditOps(ReadOnlySpan<char> c1, ReadOnlySpan<char> c2)
        {
            int len1 = c1.Length;
            int len2 = c2.Length;
            int p1 = 0;
            int p2 = 0;
            int len1o = 0;
            while (len1 > 0 && len2 > 0 && c1[p1] == c2[p2])
            {
                len1--;
                len2--;
                p1++;
                p2++;
                len1o++;
            }

            int len2o = len1o;
            while (len1 > 0 && len2 > 0 && c1[p1 + len1 - 1] == c2[p2 + len2 - 1])
            {
                len1--;
                len2--;
            }

            len1++;
            len2++;
            var matrix = stackalloc int[len2 * len1];
            for (int i = 0; i < len2; i++)
            {
                matrix[i] = i;
            }

            for (int i = 1; i < len1; i++)
            {
                matrix[len2 * i] = i;
            }

            for (int i = 1; i < len1; i++)
            {
                int ptrPrev = (i - 1) * len2;
                int ptrC = i * len2;
                int ptrEnd = ptrC + len2 - 1;
                char val = c1[p1 + i - 1];
                int ptrChar2 = p2;
                int x = i;
                ptrC++;
                while (ptrC <= ptrEnd)
                {
                    int num9 = matrix[ptrPrev++];
                    if (val != c2[ptrChar2++])
                    {
                        num9++;
                    }

                    x++;
                    if (x > num9)
                    {
                        x = num9;
                    }

                    num9 = matrix[ptrPrev] + 1;
                    if (x > num9)
                    {
                        x = num9;
                    }

                    matrix[ptrC++] = x;
                }
            }

            return EditOpsFromCostMatrix(len1, c1, p1, len1o, len2, c2, p2, len2o, matrix);
        }

        private static unsafe EditOp[] EditOpsFromCostMatrix(int len1, ReadOnlySpan<char> c1, int p1, int o1, int len2, ReadOnlySpan<char> c2, int p2, int o2, int* matrix)
        {
            int dir = 0;
            int pos = matrix[len1 * len2 - 1];
            var ops = new EditOp[pos];
            int i = len1 - 1;
            int j = len2 - 1;
            int ptr = len1 * len2 - 1;
            while (i > 0 || j > 0)
            {
                if (i != 0 && j != 0 && matrix[ptr] == matrix[ptr - len2 - 1] && c1[p1 + i - 1] == c2[p2 + j - 1])
                {
                    i--;
                    j--;
                    ptr -= len2 + 1;
                    dir = 0;
                    continue;
                }

                if (dir < 0 && j != 0 && matrix[ptr] == matrix[ptr - 1] + 1)
                {
                    ref var eop = ref ops[--pos];
                    eop.EditType = EditType.INSERT;
                    eop.SourcePos = i + o1;
                    eop.DestPos = --j + o2;
                    ptr--;
                    continue;
                }

                if (dir > 0 && i != 0 && matrix[ptr] == matrix[ptr - len2] + 1)
                {
                    ref var eop = ref ops[--pos];
                    eop.EditType = EditType.DELETE;
                    eop.SourcePos = --i + o1;
                    eop.DestPos = j + o2;
                    ptr -= len2;
                    continue;
                }

                if (i != 0 && j != 0 && matrix[ptr] == matrix[ptr - len2 - 1] + 1)
                {
                    ref var eop = ref ops[--pos];
                    eop.EditType = EditType.REPLACE;
                    eop.SourcePos = --i + o1;
                    eop.DestPos = --j + o2;
                    ptr -= len2 + 1;
                    dir = 0;
                    continue;
                }

                if (dir == 0 && j != 0 && matrix[ptr] == matrix[ptr - 1] + 1)
                {
                    ref var eop = ref ops[--pos];
                    eop.EditType = EditType.INSERT;
                    eop.SourcePos = i + o1;
                    eop.DestPos = --j + o2;
                    ptr--;
                    dir = -1;
                    continue;
                }

                if (dir == 0 && i != 0 && matrix[ptr] == matrix[ptr - len2] + 1)
                {
                    ref var eop = ref ops[--pos];
                    eop.EditType = EditType.DELETE;
                    eop.SourcePos = --i + o1;
                    eop.DestPos = j + o2;
                    ptr -= len2;
                    dir = 1;
                    continue;
                }

                throw new InvalidOperationException("Cant calculate edit op");
            }

            return ops;
        }

        private static double GetRatio(ReadOnlySpan<char> input1, ReadOnlySpan<char> input2)
        {
            int num = input1.Length;
            int num2 = input2.Length;
            int num3 = num + num2;
            int num4 = EditDistance(input1, input2);
            if (num4 != 0)
            {
                return (double) (num3 - num4) / num3;
            }

            return 1.0;
        }

        private static unsafe int EditDistance(ReadOnlySpan<char> c1, ReadOnlySpan<char> c2)
        {
            int str1 = 0;
            int str2 = 0;
            int len1 = c1.Length;
            int len2 = c2.Length;

            /* strip common prefix */
            while (len1 > 0 && len2 > 0 && c1[str1] == c2[str2])
            {
                len1--;
                len2--;
                str1++;
                str2++;
            }

            /* strip common suffix */
            while (len1 > 0 && len2 > 0 && c1[str1 + len1 - 1] == c2[str2 + len2 - 1])
            {
                len1--;
                len2--;
            }

            if (len1 == 0)
            {
                return len2;
            }

            if (len2 == 0)
            {
                return len1;
            }

            /* make the inner cycle (i.e. str2) the longer one */
            if (len1 > len2)
            {
                int nx = len1;
                int temp = str1;

                len1 = len2;
                len2 = nx;
                str1 = str2;
                str2 = temp;

                var t = c2;
                c2 = c1;
                c1 = t;
            }

            /* check len1 == 1 separately */
            if (len1 == 1)
            {
                return len2 + 1 - 2 * Memchr(c2, str2, c1[str1], len2);
            }

            len1++;
            len2++;
            int half = len1 >> 1;

            var row = stackalloc int[len2];
            int end = len2 - 1;
            for (int i = 0; i < len2; i++)
            {
                row[i] = i;
            }

            for (int i = 1; i < len1; i++)
            {
                int p = 1;
                char val = c1[str1 + i - 1];
                int c2p = str2;
                int d = i;
                int x = i;
                while (p <= end)
                {
                    x = val != c2[c2p++] ? x + 1 : --d;
                    d = row[p];
                    d++;
                    if (x > d)
                    {
                        x = d;
                    }

                    row[p++] = x;
                }
            }

            return row[end];
        }

        private static int Memchr(ReadOnlySpan<char> haystack, int offset, char needle, int num)
        {
            for (int index = 0; index != num; index++)
            {
                if (haystack[offset + index] == needle)
                {
                    return 1;
                }
            }
            return 0;
        }
    }
}
