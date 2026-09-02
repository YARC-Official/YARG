using System;

namespace YARG.Menu.Navigation
{
    public readonly struct NavigationInputBlockState
    {
        public int BlockerCount { get; }

        public bool IsBlocked => BlockerCount > 0;

        public NavigationInputBlockState(int blockerCount)
        {
            BlockerCount = Math.Max(0, blockerCount);
        }

        public NavigationInputBlockState AddBlocker()
        {
            return new NavigationInputBlockState(BlockerCount + 1);
        }

        public NavigationInputBlockState RemoveBlocker()
        {
            return new NavigationInputBlockState(BlockerCount - 1);
        }
    }
}
