using PocketRoulette.Models;

namespace PocketRoulette
{
    public enum RewardPlacement { Pocket, Ground, Missed, Failed, Grenade }

    public sealed class RewardResult
    {
        public PoolItem Item { get; }
        public int StackCount { get; }
        public RewardPlacement Placement { get; }
        public bool Received => Placement == RewardPlacement.Pocket || Placement == RewardPlacement.Ground;

        public RewardResult(PoolItem item, int count, RewardPlacement placement)
        {
            Item = item;
            StackCount = count;
            Placement = placement;
        }
    }
}
