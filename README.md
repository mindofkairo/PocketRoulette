# Pocket Roulette {.tabset}

## Overview

Pocket Roulette turns the start of every raid into a tiny gambling problem. When you load in, the mod spins the wheel and tries to sneak random loot into your pockets. Sometimes it's a snack, sometimes it is ammo, sometimes it's something stupidly rare that makes your neurons go brr.

Everything is configurable: reward chance, number of rolls, item weights, rarity modes, stack ranges, scav support, ground drops and notification messages. There's also personal luck, jackpots, challenges, extra spins, and the option to put a live grenade where you were standing. Run it as a balanced bonus system, a cursed slot machine, or a full chaos button for your server.

Doesn't add any custom items, so uninstalling won't leave made-up item templates in your profile. I'm not trying to brick your profile or anything lol.

**Solo and Fika supported. Live grenades are off by default.**

Go spin the pocket wheel big man.

## Features

### The original pocket casino

Roll existing EFT items at raid start, with configurable rarity weights and stack sizes. If a reward doesn't fit, it drops at your feet by default. You can turn ground drops off if you'd rather miss the reward. Scav rolls are supported too.

### Personal luck

Your luck belongs to you. Your squadmates have their own saved points, and being the host doesn't entitle them to yours.

Luck affects raid-start rolls, paid spins and bonus rewards. Higher luck favours rare loot and jackpots and lowers the weight of dangerous grenade outcomes. Notifications show the luck used; the ledger keeps that value with the roll.

- **Bad-luck protection:** unlucky starting batches build stored luck for future raids. Caps, rare-win reductions and inactivity decay are configurable.
- **Empty-pocket bonus:** start with empty pockets for a temporary boost. Finally, a reward for forgetting to bring anything.

Luck changes the wheel's weights. It doesn't change the separate chance for the starting wheel to run at all, and 10 luck does not mean every item has a 10% chance.

### Cursed jackpots

A valuable item, a countdown, and a very good reason to stop standing still.

You get an explanation first, then the prize appears and its timer starts. Grab it and move. Unclaimed loot disappears when the deadline ends; picking it up doesn't cancel the grenade. The default deadline is randomly chosen between **six and ten seconds**, with luck able to favour the longer end.

Requires live grenades to be enabled. Server owners control the prize rarity, explanation time and explosion window.

### Shared jackpots

Win an ultrarare main prize and decide what happens to the extra loot:

- **Share:** keep your main prize and give each eligible teammate their own gift, rolled using their luck.
- **Keep:** take the main prize and the reserved bonus for yourself. Everyone gets to judge you.

The default main prize is ultrarare; the kept bonus and squad gifts are rare or better. Prizes can land on the ground if they don't fit. The announcement and choice instructions stay visible throughout the decision window.

You have **20 seconds** by default. A timeout keeps both prizes, and solo play keeps both automatically. Five share lines and five keep lines are included, all editable. Yes, the stingy bitch line is in there.

### Lucky challenges

Sometimes the wheel gives you a little extra thing to work towards during the raid. You get assignment, progress and completion notices; no new quest menu to babysit.

The default challenge pool includes:

- Survive for five minutes.
- Travel 750 metres.
- Kill three enemies of any type.
- Kill three Scavs.
- Kill one PMC.
- Get a kill from under 25 metres.
- Get a kill from over 75 metres.

The base assignment chance is **30%**, before luck adjustments. Completion awards one bonus spin by default. Those bonus spins give items; they don't chain into another grenade event or challenge.

Each kill preset can be changed or disabled. Teammate kills don't count. This feature requires luck to be enabled.

### Extra spins

Spend **25,000 stash roubles** for another go through the raid ledger. Default limit: **two paid spins per PMC raid**. The menu closes after purchase, because you're still in a raid and your accountant is not bulletproof.

You can also choose automatic buy-ins in the F12 settings. That selection starts at **zero**. If you set it to a nonzero number, it buys that many every PMC raid until you change it back. Ledger purchases and automatic buy-ins share the same limit. Scavs can't buy spins.

### Grenade wheel

Optional RGD-5, M67, Zarya and M18 green smoke outcomes. These use real game grenades, not an invisible damage button. The squad gets a warning before the hazard; nobody gets special immunity, so move your ass.

The default RGD-5 uses its full native fuse, with prewarm set to zero.

**Live grenades are off by default.** Turning the master grenade switch off removes cursed jackpots too. Turning only the wheel off uses the configured single grenade instead; that does not disable grenades.

### Raid ledger

Open the ledger to see current/stored luck, decay rules, stash balance and up to **40 recent rolls**. Each row records the item, quantity, odds, luck used, source and delivery result.

Drag the header to move it. Adjust its scale in F12 from **0.25 to 1.25**; the default is **0.55**. Opening the ledger does not pause the raid.

### Rarity modes

| Mode | What you're signing up for |
| --- | --- |
| `mixed` | Mostly trash and okay stuff, with a small chance of something actually good. |
| `garbage` | You're eating crayons and loving it. |
| `useful` | Decent stuff more often. Very responsible gambling. |
| `jackpot` | Loaded dice for rich kids. |
| `chaos` | Every rarity starts with equal weight. Good luck dummy. |

Modes choose a rarity tier, then use each item's weight within it. Personal luck adjusts those starting weights; enabled events also take their share of the wheel.

## Controls

| Default key | Action |
| --- | --- |
| `HOME` | Open or close the raid ledger. |
| `F8` | Share when a shared-jackpot choice is open. |
| `F9` | Keep both prizes when a choice is open. |

Ledger and jackpot bindings are configurable.

## Install

### Fresh install

1. Download **Pocket Roulette for SPT 4.1.X**.
2. Close the game and SPT server.
3. Open the main archive and copy its `BepInEx` and `SPT_Runtime` folders into the folder containing `EscapeFromTarkov.exe`. Merge/replace the folders if prompted. Don't put the entire archive inside another `PocketRoulette` folder.
4. Start the server, then the game. Load into a raid and let the pocket gods do their thing.

The resulting paths should include:

```text
[game folder]/BepInEx/plugins/PocketRoulette/PocketRoulette.dll
[game folder]/BepInEx/plugins/PocketRoulette/assets/roubles.png
[game folder]/SPT_Runtime/user/mods/PocketRoulette/PocketRoulette.Server.dll
[game folder]/SPT_Runtime/user/mods/PocketRoulette/config/config.jsonc
```

The server portion goes on the SPT server. Every participating player needs the client plugin, assets and matching bridge when using Fika.


### Uninstalling

Close the game/server and remove the Pocket Roulette client folder, server mod folder and any old loose Pocket Roulette DLL. You can keep a backup of the server `config` and `data` folders if you might return. Your rewards are ordinary EFT items by default; there should be no custom item templates to clean out.

## Config Options

Edit `[game folder]/SPT_Runtime/user/mods/PocketRoulette/config/config.jsonc`.

It's JSONC, so comments explain the options. (theres a lot..) Feature switches are right at the top. Keep commas and quoted strings intact.

### Stuff you'll probably touch

| Option | Default | What it does |
| --- | --- | --- |
| `mode` | `mixed` | Starting rarity distribution. See Features for the five modes. |
| `itemCount` | `1` | Starting spins per raid, from 1 to 20. |
| `chancePercent` | `100` | Chance the starting wheel runs. `0` disables roulette and purchases. |
| `allowGroundDrop` | `true` | Drop ordinary rewards at your feet if they don't fit. Set false to miss overflow rewards. |
| `scavEnabled` | `true` | Let your scav hit the pocket casino. Paid spins remain PMC only. |
| `enableNotification` | `true` | Ordinary roulette notices. Actionable jackpot choices and group hazard alerts still appear. |
| `allowClientOverrides` | `false` | Allow players to opt into local normal-roll settings. Both server permission and the player's F12 opt-in are required. |
| `debugLogging` | `false` | Extra troubleshooting logs. |
| `itemPool` | Shipped EFT pool | Existing item IDs, names, rarities and relative weights. No custom items are added. |
| `itemPool.minCount` / `maxCount` | Per item | Reward stack range, limited by the real item's maximum stack size. |

### Feature switches

| Switch | Default | Dependency / effect |
| --- | --- | --- |
| `enableLuck` | `true` | Server-wide master luck switch; points are still saved separately for each player. |
| `enableBadLuckProtection` | `true` | Requires luck. Builds stored points after unlucky starting batches. |
| `enableEmptyPocketBonus` | `true` | Requires luck. Temporary bonus for starting with empty pockets. |
| `enableLuckyChallenges` | `true` | Requires luck. Assigns ordinary-play challenges for bonus spins. |
| `enableSharedJackpots` | `true` | Enables jackpot offers; solo play keeps both prizes. |
| `enableBuyInSpins` | `true` | Permits paid spins. Automatic purchases still default to zero per player. |
| `enableGrenadeEvent` | `false` | Master permission for live grenade outcomes. Off excludes the wheel and cursed jackpots. |
| `enableGrenadeWheel` | `true` | Requires grenades. Off uses `grenadeTpl` instead of the wheel. |
| `enableCursedJackpot` | `true` | Requires grenades. Enables timed valuable loot followed by an explosion. |

Disabling luck pauses its effects, gains/decay, empty-pocket bonuses and challenge assignment/completion for everyone. Saved luck is kept. Shared jackpots, purchases and enabled grenade events remain available at base odds. Child settings stay in the config so you don't have to rebuild them when turning a feature back on.

### Luck, challenges and jackpot tuning

| Setting | Default | What it changes |
| --- | --- | --- |
| `luck.maxLuck` | `20` | Effective luck cap, including temporary bonuses. |
| `luck.showAtRoll` | `true` | Show luck used in roll notifications. |
| `badLuckProtection.gainPerUnluckyRaid` | `1` | Stored gain once per eligible unlucky starting batch; buying spins doesn't farm this. |
| `badLuckProtection.maxStoredLuck` | `10` | Stored-point cap. |
| `badLuckProtection.winDecayPercent` | `50` | Reduction after receiving a rare/ultrarare win, applied at most once per raid. |
| `badLuckProtection.inactivityDecayPerDay` | `0.25` | Stored points lost per inactive real-world day, applied at the next raid start. `0` disables this decay. |
| `emptyPocketBonus.luckPoints` | `2` | Temporary boost from the initial empty-pocket snapshot. |
| `luckyChallenges.assignmentChancePercent` | `30` | Base assignment chance before luck adjustments. |
| `luckyChallenges.bonusSpins` | `1` | Item-only spins for completing a challenge. |
| `luckyChallenges.killPresets` | Five presets | Individual toggles, kill counts, enemy types and distance limits. An empty list removes kill challenges. |
| `sharedJackpots.decisionSeconds` | `20` | Time to share or keep. |
| `sharedJackpots.shareMessages` / `keepMessages` | Five each | Random decision lines. `{user}` or `(user)` inserts the winner's name. |
| `buyInSpins.priceRoubles` | `25000` | Stash roubles per paid spin. |
| `buyInSpins.maxSpinsPerRaid` | `2` | Combined automatic and ledger purchase limit. |

Kill presets accept `targetType: "any"`, `"scav"` or `"pmc"`. AI PMCs count as PMCs; Raiders and Rogues count as Scav targets here. A zero distance bound means no limit. Positive `minDistanceMeters` / `maxDistanceMeters` mean strictly over/under that distance, measured when the enemy dies. Exactly 25m does not satisfy "under 25m."

### Grenade settings

| Setting | Default | What it changes |
| --- | --- | --- |
| `grenadeSpawnDelaySeconds` | `0.75` | Warning delay before an ordinary grenade appears at the roll location. |
| `grenadeWheel` | Four choices | Existing grenade IDs, weights, danger flag and fuse prewarm. |
| `grenadeTpl` | RGD-5 | Single-grenade fallback when the wheel is disabled. |
| `grenadeTimeSpentSeconds` | `0` | Fuse already spent on the fallback grenade. |
| `cursedJackpot.minExplosionDelaySeconds` / `maxExplosionDelaySeconds` | `6` / `10` | Cursed deadline after the explanation and prize placement. |
| `cursedJackpot.explanationSeconds` | `2` | Time to read the explanation before the cursed timer starts. |

**Prewarm means fuse already used up.** Increasing it makes the grenade explode sooner, not later. Leave RGD-5 at zero if you want its full native fuse. Cursed countdowns have their own timing settings.

### What players can change

Keys, ledger scale and automatic buy-in count are personal settings. Normal roll overrides additionally require `allowClientOverrides: true` on the server and `Allow Client Config` enabled in that player's F12 settings. Local pools/messages use `PocketRoulette.ClientConfig.jsonc` beside the client DLL.

Luck, events, challenges and purchase rules remain controlled by the server. Most raid settings are captured at raid start, so edit them between raids and use a fresh raid when checking a change.

## Fika

Each player rolls independently and has their own saved luck. Shared jackpots let the winner decide whether the squad gets gifts, and hazard warnings tell everyone who's about to become a problem.

### What to install

- Every participating game client needs the **client plugin and assets**.
- Every Fika game client, including the hosting client, also needs the **matching `PocketRoulette.Fika.dll`** from the optional bridge.

Keep the client and bridge versions matched. If Fika is detected but the bridge is missing or incompatible, Pocket Roulette skips rolls and reports the problem rather than creating unsynchronized loot.

### A few things to know

- Share/keep choices use the configured keys, default F8/F9. Gifts go to eligible squadmates in the raid, not everyone with a profile on the server.
- The winner keeps the main prize either way. Sharing trades their reserved bonus for personal gifts to the squad; it doesn't clone the winner's exact item for everyone.
- Group hazard alerts do not grant immunity. These are normal game grenades with normal damage.
- Luck and feature switches are server-owned. Each player can still choose their keys, ledger scale and whether to buy automatic spins.

This release's Fika integration is for **SPT 4.1.X**.

## FAQ

### Can I turn the grenades off?

Yes. They're already off in a fresh config. Set `enableGrenadeEvent` to `false` and neither the grenade wheel nor cursed jackpots can roll. You can keep all the normal loot, luck and jackpot features.

### Why did a grenade go off so fast?

Check prewarm. It means time already spent on the fuse. The current RGD-5 default is zero, but updates preserve old config values. See Install for the two settings to check if you're upgrading.

### My F12 settings aren't changing the rolls.

The server is in charge by default. It must allow client overrides, and you must opt into them. Event, luck, challenge and purchase rules stay server-owned even then. Try a new raid after changing settings.

### Why do current luck and the luck beside an old roll differ?

The row remembers what the wheel used at the time. A later rare win or decay can change your current value. The odds in that row also stay tied to the original roll.

### Why isn't an item's percentage just the raid chance?

`chancePercent` decides whether the starting wheel runs. The ledger's percentage is that item's selection chance on a spin that takes place, including its luck-adjusted rarity, item weights and competing events. It doesn't multiply in the starting activation chance or the chance of a particular stack amount.

### I got a shared jackpot while playing solo. Where's my choice?

You keep both prizes automatically when no eligible teammates are present. There's nobody to disappoint. With a squad, the choice defaults to keep if you let the timer expire.

### Does buying a spin guarantee something good?

No. You're buying a spin, not good judgement. Paid spins use the normal selection rules, and can miss or select an enabled event. Turning the starting chance down doesn't make paid spins cheaper; a starting chance of zero disables purchases entirely.

### I lost a spin because my pockets were full.

With `allowGroundDrop: true`, ordinary overflow rewards should appear at your feet. Existing configs keep their old value, so check that setting before blaming the pocket gods. Ground loot can still be missed if you don't pick it up.

### Does this add custom items or need a profile wipe?

No custom items and no wipe required by the mod. Rewards use existing EFT templates. Back up your profile and mod settings before updating, as you normally would.

### Something isn't working. What should I include in a report?

SPT version, Pocket Roulette version, whether you use Fika, whether you were host or guest, what happened, and the relevant server/BepInEx log lines. Include the settings involved and whether the reward appeared in pockets, on the ground or nowhere. "It broke" gives me roughly as much to work with as an empty Slickers wrapper.

{.endtabset}
