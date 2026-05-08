# Pocket Roulette

Pocket Roulette turns the start of every raid into a tiny gambling problem. When you load in, the mod spins the wheel and tries to sneak random loot into your pockets. Sometimes it's a snack, sometimes it is ammo, sometimes it's something stupidly rare that makes your neurons go brr.

Everything is configurable: reward chance, number of rolls, item weights, rarity modes, stack ranges, scav support, ground drops, and notification messages. Run it as a balanced bonus system, a cursed slot machine, or a full chaos button for your server.

Doesn't add any custom items so your profile stays safe when you uninstall. I'm not trying to brick your profile or anything lol.

Go spin the pocket wheel big man.

### Fika Compatibility

I never had any issues with Fika, but I might’ve just gotten lucky lol.

It should work with or without Fika. Each player in the raid rolls their own pocket item independently.

### Configuration

Edit 'SPT/user/mods/PocketRoulette/config/config.json'.

Stuff you'll probably touch:

- mode: 'mixed', 'garbage', 'useful', 'jackpot', or 'chaos'
- itemCount: how many times Pocket Roulette rolls at raid start
- chancePercent: 0-100 chance that Pocket Roulette even does anything
- enableNotification: show or hide the raid-start notification
- debugLogging: show detailed server logs for item sync and ground registration
- allowGroundDrop: if it doesn’t fit in your pockets, drop it at your feet (or discard/skip it)
- scavEnabled: let your scav also hit the pocket casino
- itemPool: weighted list of existing EFT item template IDs
- itemPool.minCount / itemPool.maxCount: stack amount range for that reward, clamped to the item's real max stack size

You can also mess with all the funny messages it spits out if you’re bored.

### Modes

Modes pick a rarity tier first, then use each item's weight inside that rarity.

- mixed: mostly trash and okay stuff, tiny chance for something actually good
- garbage: you’re eating crayons and loving it
- useful: actually gives you decent stuff most of the time
- jackpot: loaded dice for rich kids
- chaos: every rarity has equal chance. Good luck dummy

### Known Issues

- If your pockets are already full at raid start, a few items just refuse to spawn in the world for some reason. They vanish into the void instead. No clue why, don’t ask me I’m not smart.
