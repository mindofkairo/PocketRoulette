using EFT;

namespace PocketRoulette
{
    internal static class ChallengeVictim
    {
        public static string Classify(EPlayerSide side, WildSpawnType? role)
        {
            // EFT's own kill statistics correct these AI roles before using profile side.
            // pmcBot is a Raider, and exUsec is a Rogue; neither is an AI PMC role.
            if (role == WildSpawnType.pmcUSEC || role == WildSpawnType.pmcBEAR) return "pmc";
            if (side == EPlayerSide.Usec || side == EPlayerSide.Bear) return "pmc";
            return side == EPlayerSide.Savage ? "scav" : "";
        }
    }
}
