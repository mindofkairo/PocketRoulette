using System;
using PocketRoulette.Models;

namespace PocketRoulette
{
    public sealed partial class PocketRouletteScript
    {
        private string _menuPurchaseId;
        internal EFT.Player MenuPlayer => _player;
        internal bool MenuAvailable => _player != null && !_token.IsCancellationRequested && _player.HealthController.IsAlive;
        internal bool CanRequestMenu => MenuAvailable && _ready;
        internal bool CanPurchaseNow => CanRequestMenu && !_halted && !_processing && _queue.Count == 0 && _unconfirmed.Count == 0;
        internal string MenuError { get; private set; } = "";

        internal PlayerMenuInfo ReadPlayerMenu()
        {
            try
            {
                if (!CanRequestMenu) throw new InvalidOperationException("Waiting for roulette to initialize.");
                var update = RaidApi.Action("menu", new RaidActionRequest { RaidId = _raidId, PurchaseId = _menuPurchaseId ?? "" });
                Apply(update);
                if (_menuPurchaseId != null && update.Menu.ConfirmedPurchaseId == _menuPurchaseId) _menuPurchaseId = null;
                StartQueue();
                MenuError = "";
                return update.Menu;
            }
            catch (Exception ex) { MenuError = ex.Message; return null; }
        }
        internal bool BuyMenuSpin(int expectedPrice)
        {
            try
            {
                if (!CanPurchaseNow) throw new InvalidOperationException("Finish the current roll first.");
                if (_menuPurchaseId == null) _menuPurchaseId = Guid.NewGuid().ToString("N");
                Apply(RaidApi.Action("buy", new RaidActionRequest { RaidId = _raidId, PurchaseId = _menuPurchaseId, ExpectedPrice = expectedPrice }));
                _menuPurchaseId = null;
                MenuError = "";
                StartQueue();
                return true;
            }
            catch (Exception ex)
            {
                MenuError = ex.Message;
                Plugin.LogSource.LogWarning("[PocketRoulette] Purchase could not be confirmed; retry retains its payment ID: " + ex.Message);
                return false;
            }
        }
    }
}
