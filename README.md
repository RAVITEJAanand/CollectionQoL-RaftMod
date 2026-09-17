# 🧲 Collection QoL — Loot, Nets & Detection Quality-of-Life for Raft

[![Version](https://img.shields.io/badge/Version-1.0.0-blue.svg?style=for-the-badge&logo=github)](https://github.com/RAVITEJAanand/CollectionQoL-RaftMod/releases)
[![Raft Version](https://img.shields.io/badge/Raft-The%20Final%20Chapter%20(v1.0+)-brightgreen.svg?style=for-the-badge&logo=steam)](https://store.steampowered.com/app/648800/Raft/)
[![Discord](https://img.shields.io/badge/Discord-Join%20Community-5865F2?style=for-the-badge&logo=discord&logoColor=white)](https://discord.gg/B4EMrR5Vrf)

**Author**: KONDURI (RAVITEJAanand)
**Game**: Raft (The Final Chapter Update 1.09 / v13.01)
**Version**: 1.0.0
**Framework**: BepInEx 5.4.23, HarmonyLib
**Discord**: [Join our Modding Discord](https://discord.gg/B4EMrR5Vrf)
**Compatibility**: Designed to run alongside **Sailor's Companion**, **Farmer's Companion**, and **Inventory Master** with zero hotkey clashes.

---

## 🌟 Overview

**Collection QoL** takes the tedium out of gathering loot, hooking debris, and fighting sharks for scraps. It automatically empties nets, widens hook range and accuracy, magnet-pulls loot on demand, highlights resources on the water and underwater, and prioritizes what gets picked up first — all while tracking floating debris on a radar overlay.

If **Sailor's Companion** is also installed, the handful of overlapping features (hook speed, magnet, island hand pickup, item detector scan, auto-empty nets) default to **off** so the two mods never fight over the same pickup — your own saved settings are never touched.

---

## 🎮 Keybindings

| Hotkey | Action |
| :--- | :--- |
| **`F3`** | Open/close the Collection QoL settings menu |
| **`Alt + F5`** | Magnetic Collector — timed loot magnet pull |
| **`Alt + F6`** | Item Detector Scan pulse |
| **`Alt + F7`** | Cycle resource priority preset |
| **`Alt + F8`** | Show/hide all overlays |
| **`Ctrl + Alt + F8`** | Write the R&D diagnostic dump to the log |

The `Alt` modifier keeps every action on its original F-key while staying clear of Sailor's Companion's own bare F5–F8 menu keys.

---

## 🚀 Features

1. **Auto Empty Nets** — periodically empties nearby collection nets into your inventory.
2. **Net Range Increase** — bigger catch trigger radius on nets.
3. **Hook Speed Boost** — faster hook reeling/gathering.
4. **Hook Accuracy Boost** — pulls near-miss items onto the hook.
5. **Magnetic Collector (`Alt+F5`)** — timed loot magnet with cooldown, pulls and auto-collects loot in range, including barrels.
6. **Hand Pickup Island** — auto-picks up small debris while on land.
7. **Smart Debris Tracking** — panel with net/hook interception predictions.
8. **Resource Highlight** — brackets and labels on nearby loot.
9. **Item Detector Scan (`Alt+F6`)** — scan pulse revealing loot in range for a few seconds.
10. **Loot Glow** — pulsing lights on valuable loot for visibility.
11. **Barrel Target Assist** — aim marker and bigger hook capture radius for barrels.
12. **Underwater Loot Assist** — dive HUD and underwater loot markers, with auto-grab very close by.
13. **Resource Priority (`Alt+F7`)** — three switchable priority presets (Balanced/Building/Food) controlling pickup order.
14. **Sound Alerts** — audio cues when valuable loot is nearby.
15. **Floating Loot Tracker** — radar overlay with edge arrows for tracked loot kinds.

---

## 🛠️ Installation

1. Install [BepInEx 5.4.23 (x64)](https://github.com/BepInEx/BepInEx/releases) into your Raft directory.
2. Place `CollectionQoL.dll` into `Raft/BepInEx/plugins/CollectionQoL/`.
3. Launch Raft and press **`F3`**!
