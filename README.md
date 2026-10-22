# Pocket Roulette

Pocket Roulette turns the start of every raid into a tiny gambling problem. When you load in, the mod spins the wheel and tries to sneak random loot into your pockets. Sometimes it's a snack, sometimes it is ammo, sometimes it's something stupidly rare that makes your neurons go brr.

Everything is configurable: reward chance, number of rolls, item weights, rarity modes, stack ranges, scav support, ground drops, and notification messages. Now there's personal luck, jackpots, challenges, extra spins, and the option to put a live grenade where you were standing. Run it as a balanced bonus system, a cursed slot machine, or a full chaos button for your server.

Doesn't add any custom items, so you can uninstall without leaving custom item templates in your profile. I'm not trying to brick your profile or anything lol.

Go spin the pocket wheel big man.

### Features

- **Personal luck and bad-luck protection:** unlucky raid starts build up your own luck for future rolls. Better luck favours rare loot and jackpots and makes dangerous grenade outcomes less likely. Your squadmates have their own luck; nobody gets to steal yours. Caps and decay are configurable.
- **Empty-pocket bonus:** load in with empty pockets for a temporary luck boost. Finally, a reward for forgetting to bring anything.
- **Cursed jackpots:** a valuable item, a countdown, and a grenade. Grab the loot and move. Leave it too long and the unclaimed item disappears when the timer ends. Explosion delays are configurable, and turning live grenades off removes these too.
- **Shared jackpots:** keep your main prize and give the squad gifts, or keep both prizes and let everyone judge you. Gifts use each teammate's own luck. Share/keep messages are configurable, with five of each by default.
- **Lucky challenges:** sometimes you'll get a survival, travel or kill challenge for bonus spins. Default kill presets are three enemies of any type, three Scavs, one PMC, one kill under 25m, or one kill over 75m. Counts, enemy types and distance limits are configurable. Teammate kills don't count. You get assignment, progress and completion notifications, so you don't need to keep checking a quest menu. Requires the luck system to be enabled.
- **Extra spins:** spend stash roubles for another go through the ledger, or choose automatic buy-ins in F12. Default price is 25,000 roubles, with a shared limit of two extra spins per PMC raid. Automatic buy-ins start at zero; if you choose a nonzero amount, it repeats each raid until you change it back.
- **Grenade wheel:** optional RGD-5, M67, Zarya and M18 smoke rolls. The squad gets a warning before dangerous events. These are real grenades, so move your ass. Live grenades are **off by default**.
- **Raid ledger:** see your luck, decay rules, stash balance and forty recent rolls, including their odds, luck, source and whether they reached your pockets, landed on the ground or were missed.

Every feature has its own server config toggle. Turn on what sounds fun and leave the rest alone. Disabling luck also turns off bad-luck protection, empty-pocket luck and lucky challenges for everyone; saved luck is kept. Shared jackpots, purchases and enabled grenade events can still run at base odds. Disabling grenades removes both grenade-wheel rolls and cursed jackpots.

### Controls (Configurable)

- **HOME:** open or close the raid ledger. Escape or the back button closes it too.
- **F8:** share a jackpot. **F9:** keep it. Both keys are configurable in F12.

Jackpot prize details and the share/keep countdown stay on screen until you decide. Let the timer run out and you keep both prizes. Playing solo keeps both automatically.

The ledger closes after an extra-spin purchase. Opening it doesn't pause the raid, so maybe don't do your accounting in the middle of a gunfight.

### Fika Compatibility

Supports solo play and Fika. Each player rolls independently and keeps their own saved luck. Shared jackpots let the winner decide whether the squad gets gifts, and grenade warnings tell everyone who's about to become a problem.

### Installation / Updating

This version is for **SPT 4.1.X**.

Close the game and server, then copy the release's `BepInEx` and `SPT_Runtime` folders into your game install. Add the matching optional Fika bridge if you use Fika.

Keep the `assets` folder beside `PocketRoulette.dll`; that's where the ledger's artwork lives. Keep your existing server config and `data` folder when updating so you don't lose your settings or saved luck. Older JSON configs import automatically with backups.

### Configuration

Edit `SPT_Runtime/user/mods/PocketRoulette/config/config.jsonc` inside the game install. It's JSONC now, so the file has comments explaining what everything does. Feature toggles are right at the top.

Stuff you'll probably touch:

- `mode`: `mixed`, `garbage`, `useful`, `jackpot`, or `chaos`
- `itemCount`: how many times Pocket Roulette rolls at raid start
- `chancePercent`: 0-100 chance that Pocket Roulette even does anything; zero disables roulette and purchases
- `enableNotification`: show or hide ordinary reward notifications; actionable jackpot choices still appear
- `debugLogging`: show detailed logs for troubleshooting
- `allowClientOverrides`: lets players opt into their own BepInEx/F12 settings instead of the normal server roll settings
- `allowGroundDrop`: true by default; if it doesn't fit in your pockets, drop it at your feet. Set false to skip overflow rewards instead
- `scavEnabled`: let your scav also hit the pocket casino; paid spins are PMC only
- `itemPool`: weighted list of existing EFT item template IDs
- `itemPool.minCount` / `itemPool.maxCount`: stack amount range for that reward, clamped to the item's real max stack size
- `enableLuck`: master switch for the luck system for ALL players. Each player still keeps their own saved points; turning it off pauses luck effects, gains/decay, empty-pocket bonuses and challenges
- `enableBadLuckProtection` / `badLuckProtection`: requires luck; adjust points gained after unlucky raid starts, the stored cap, rare-win reduction and daily inactivity decay
- `enableEmptyPocketBonus` / `emptyPocketBonus.luckPoints`: requires luck; temporary bonus for starting with empty pockets, default two points
- `enableLuckyChallenges` / `luckyChallenges`: requires luck; default 30% assignment chance before luck adjustments, one bonus spin, and optional progress notices. Survival and walking targets are configurable too
- `luckyChallenges.killPresets`: enable/disable each kill challenge, change `kills` and choose `targetType` (`any`, `scav`, `pmc`). AI USEC/BEAR count as PMCs even if their profile uses the Scav side; Raiders and Rogues stay Scav targets. Zero distance means no limit; positive `minDistanceMeters` / `maxDistanceMeters` mean strictly over/under that distance at the moment of the kill. An empty list disables kill presets
- `enableSharedJackpots` / `sharedJackpots`: enable squad sharing, change the 20-second decision window, prize rarities and random share/keep message lists
- `enableBuyInSpins` / `buyInSpins`: allow paid spins, set `priceRoubles` (25,000 by default) and `maxSpinsPerRaid` (two by default)
- `enableGrenadeEvent`: master grenade switch, false by default. If false, neither the grenade wheel nor cursed jackpots can roll, even if their own switches are true
- `enableGrenadeWheel` / `grenadeWheel`: requires grenades; configure types, weights and fuse prewarm. Turning just the wheel off uses the single `grenadeTpl` instead
- `enableCursedJackpot` / `cursedJackpot`: requires grenades; configure the valuable prize and minimum/maximum explosion delays (six to ten seconds by default)

Feature switches are server-wide and apply when each player's raid starts. Their saved luck values stay separate. Changing a parent switch doesn't erase your child settings, so you can turn it back on later without rebuilding your config. Existing explicit settings are kept when updating, including an older `allowGroundDrop: false`.

You can also mess with all the funny messages it spits out if you're bored.

By default the server config is the authority, which keeps Fika/multiplayer fair. Basically, the server and client need each other's consent for a player to use their own F12 config. If the server allows overrides, each client can still choose the server config or their own custom normal-roll settings. Luck, event rules, challenges and purchases stay controlled by the server.

Client-side item pools and messages live in `BepInEx/plugins/PocketRoulette/PocketRoulette.ClientConfig.jsonc`.

### Modes

Modes pick a rarity tier first, then use each item's weight inside that rarity.

- `mixed`: mostly trash and okay stuff, tiny chance for something actually good
- `garbage`: you're eating crayons and loving it
- `useful`: actually gives you decent stuff most of the time
- `jackpot`: loaded dice for rich kids
- `chaos`: every rarity starts with equal weight. Good luck dummy

Personal luck adjusts those starting weights. Luck is measured in points, not percentages: 10 luck doesn't mean a flat 10% chance at every item. The ledger shows each roll's odds after applying its luck and active event weights, per spin that actually takes place. Older rolls keep their original odds even if your luck changes afterward.
