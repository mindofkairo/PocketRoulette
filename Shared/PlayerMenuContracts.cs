#nullable disable
using System;
using System.Collections.Generic;

namespace PocketRoulette.Models
{
    public sealed class PlayerMenuInfo
    {
        public double StoredLuck { get; set; }
        public double EmptyPocketLuck { get; set; }
        public double EffectiveLuck { get; set; }
        public double InactivityDecayPerDay { get; set; }
        public double WinDecayPercent { get; set; }
        public bool LuckEnabled { get; set; }
        public int PriceRoubles { get; set; }
        public int RemainingSpins { get; set; }
        public long StashRoubles { get; set; }
        public bool CanBuy { get; set; }
        public bool Sandbox { get; set; }
        public string BuyUnavailableReason { get; set; } = "";
        public string ConfirmedPurchaseId { get; set; } = "";
        public List<RollHistoryEntry> History { get; set; } = new List<RollHistoryEntry>();
    }
    public sealed class RollHistoryEntry
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Rarity { get; set; } = "";
        public string Origin { get; set; } = "";
        public string Placement { get; set; } = "";
        public int Count { get; set; }
        public double OddsPercent { get; set; }
        public double Luck { get; set; }
        public DateTime RolledUtc { get; set; }
    }
}
