# Pocket Roulette

SPT 4.0.X mod that gives your PMC one random EFT item in his pockets at the start of a raid.

Sometimes you get something sick, sometimes you get a screwdriver. Pure gambling, baby.

Doesn't add any custom items so your profile stays safe when you uninstall. I'm not trying to brick your profile or anything lol.

Go spin the pocket wheel big man.

## Configuration

Edit 'src/PocketRoulette.Server/config/config.json'.

The important fields are:

- mode: 'mixed', 'garbage', 'useful', or 'jackpot'
- enableNotification: show or hide the raid-start notification
- itemPool: weighted list of existing EFT item template IDs

## Known Issues

- If your pockets are already full at raid start, a few items just refuse to spawn in the world for some reason. They vanish into the void instead. No clue why, don’t ask me I’m not smart.

