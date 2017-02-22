# Hearthstone MetaDetector

[![Join the chat at https://gitter.im/adnanc/HDT.Plugins.MetaDetector](https://badges.gitter.im/adnanc/HDT.Plugins.MetaDetector.svg)](https://gitter.im/adnanc/HDT.Plugins.MetaDetector?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

This Hearthstone Deck Tracker plugin tries to detect what deck your opponent is playing based on the cards played. Cards are compared with the most popular decks available on metastats.net and the closest matching decks are displayed. Decks are regulary updated and plugin automatically downloads the latest deck lists and stats from metastats.net. Deck ranks are based on how often a certain deck is played in the current meta.


# Installation

1. Download the latest version from https://github.com/adnanc/HDT.Plugins.MetaDetector/releases
2. Then unzip the release into the HDT Plugins directory (this directory can be opened with Options > Tracker > Plugins > Plugins Folder)
3. The directory should look like Plugins/MetaDetector/[some files]
4. Enable the plugin from Options > Tracker > Plugins. ***If you are already using MetaStats plugin, please disable that, it will cause data conflict***.
5. If the plugin does not show up in that list, right click 'MetaDetector.dll', go into Properties and click "Unblock" at the bottom.
