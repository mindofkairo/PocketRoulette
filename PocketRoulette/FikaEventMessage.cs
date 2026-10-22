using PocketRoulette.Models;

namespace PocketRoulette
{
    public sealed class FikaEventMessage
    {
        public int OwnerNetId;
        public string OwnerProfileId;
        public string OwnerNickname;
        public string RaidId;
        public string PlanId;
        public string Stage;
        public bool Success;
        public RollPlan Plan;
        public float X;
        public float Y;
        public float Z;
        public double Deadline;
        public float Prewarm;
    }
}
