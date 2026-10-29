# Pocket Roulette Changelog

## v1.9.3

### Fixed

- Corrected Fika detection when another mod has a Fika addon installed without Fika itself. Pocket Roulette now checks the actual Fika plugin ID instead of any plugin ID containing "fika". This prevents loading its Fika bridge with a missing dependency, which could disrupt other mods' startup/type scans. The bridge can still be bundled for solo and Fika players.

## v1.9.2 - SPT 4.1.X update

Changes since the SPT 4.0.X release (v1.2.1).

### New

- **Raid ledger:** HOME opens your current/stored luck, decay rules, stash balance and forty recent rolls with their items, odds, luck, source and delivery result. Drag the header to move it; adjust its scale in F12 from 0.25 to 1.25, default 0.55.
- **Personal luck:** every player has their own saved luck, used for raid-start rolls, extra spins and bonus rewards. Higher luck favours rare loot and jackpots while reducing dangerous grenade outcomes. Notifications and the ledger show the luck used for each roll.
- **Bad-luck protection:** unlucky starting rolls build luck for future raids, with configurable caps, rare-win reductions and inactivity decay.
- **Empty-pocket bonus:** start a raid with empty pockets for a temporary luck boost.
- **Cursed jackpots:** valuable loot appears at your feet with an explanation and a countdown before a real grenade explodes. Grab it and move. Unclaimed loot disappears when the timer ends. Explosion delays are configurable; disabling live grenades also disables cursed jackpots.
- **Shared jackpots:** keep the main prize and share gifts with your squad, or keep both prizes for yourself. Teammates' gifts use their own luck. F8 shares and F9 keeps by default; both are configurable. Prize details and the countdown stay visible until the decision ends. Includes five configurable share messages and five keep messages, picked at random for the group. Solo players and timed-out choices keep both prizes.
- **Lucky challenges:** optional survival, travel and kill challenges award bonus spins during ordinary play. Includes configurable presets for any enemy kills, Scav kills, PMC kills, a kill under 25m and a kill over 75m. Presets have individual toggles, kill counts, enemy types and strict distance limits. Self/teammate kills and repeated death reports don't count. Assignment chance, targets and rewards are configurable, with assignment, progress and completion notifications. Requires the luck system.
- **Extra spins for roubles:** buy spins from your stash through the ledger, or select automatic raid-start buy-ins in F12. Defaults are 25,000 roubles per spin and two per PMC raid; automatic purchases default to zero. Purchases close the ledger and share one per-raid limit.
- **Live grenade rolls and grenade wheel:** optional weighted RGD-5, M67, Zarya and M18 smoke outcomes, with configurable warnings, delays and fuse settings. Squadmates are warned before dangerous events. Live grenades are disabled by default.

### Changed

- Default RGD-5 rolls use the full native fuse (zero prewarm), giving 1.5 seconds more reaction time after spawning. Applies to both the grenade wheel and single-grenade fallback.
- Reworked the mod for **SPT 4.1.X**!
- Outcomes are selected and tracked by the server for each player. Reward, purchase and jackpot decisions have protection against duplicate requests.
- Server owners control luck, events, challenges and purchases.
- Luck and grenade switches are server-wide master controls: disabling luck pauses its dependent bonuses/protection/challenges; disabling grenades excludes both the wheel and cursed jackpots. Independent jackpots and paid spins remain available at base odds without luck. Saved points and child settings are retained.
- Normal full-pocket ground drops are enabled by default. Existing explicit preferences are preserved.
- Config files use readable apostrophes and quoted text, and release text uses ordinary hyphens.
- Roll odds account for personal luck, rarity weights, item weights and eligible events. Historical ledger rows retain the odds and luck used at the time of the spin.

### Fixed

- Inventory transaction timeouts now stop the remaining rolls instead of continuing while an earlier item may still be added, avoiding conflicting pocket placements.
- Improved full-pocket ground-drop reliability by loading reward assets before spawning and checking that EFT actually created the ground item.
- Failed Fika ground-item synchronization is no longer reported as a successfully delivered reward.

## v1.2.1 - SPT 4.0.X

### Fixed
- Prevented duplicate item IDs during post-raid saves by registering pocket rewards without inserting them directly into the persistent PMC profile.

## v1.2.0

### Added
- Server authorized BepInEx/F12 client config overrides.
- New server option `allowClientOverrides`, defaulting to false.
- New client `Allow Client Config` toggle plus local settings for odds, item count, mode, scav rewards, and ground drops.

## v1.1.0

### Added
- You can now choose how many times it rolls at raid start (multi-roll raids show a summary notification.)
- Per-item stack ranges (with smart limits so it doesn't give you 9000 bolts)
- Reward chance setting. (0-100%)
- Debug logging toggle.
- Scav raid enable/disable option.
- Optional ground drops when pockets are full.
- Config now reloads automatically like a big boy
- Config validation that actually tells you if you messed up
- More customizable notification messages.

### Changed


- Now picks rarity first, then item inside it (feels way better imo)
- Server/Fika sync should be more stable
- Console is no longer yelling at you every raid

### Fixed
- Stacked items look clean in notifications now
- Missed rewards no longer appear as "best finds."
- Bad config edits no longer explode everything
- No more duplicate items from sync retries
