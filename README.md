# Lab Breakout: Roguelite 

**Lab Breakout: Roguelite** is a fast-paced 2D top-down action roguelite developed in **Unity 6 (Universal Render Pipeline 2D)**. Fight your way through experimental laboratory sectors, battle waves of mutated bio-monsters and mechanical abominations, synthesize weapon evolutions, and escape the containment facility!

---

##  Key Features

- **Dynamic Combat & Weapon Evolution System**:
  - Arsenal of unique primary weapons: *Bio Needler, Nano Blade, Plasma Blaster, Tesla Orb, Gravity Grenadier, Flame Ring, Toxic Spore*, and more.
  - Upgrade and combine weapons with passive technology cards to unlock game-changing **Evolved Weapon Behaviors**.
- **Challenging Enemy & Boss Encounters**:
  - Varied enemy archetypes: *Walkers, Runners, Dashers, Burrowers, and Ranged Artillery*.
  - Multi-phase laboratory bosses with distinctive mechanics (*The Architect, Overlord, Stormcaller, Brute, Digger, Doctor*).
- **In-Run & Meta Progression**:
  - **In-Run Upgrades**: Earn EXP crystals, level up, and draft synergy cards / boons during your escape attempts.
  - **Meta Research Tree**: Invest research points earned across runs to permanently boost baseline survivor stats and unlock new perks.
- **Room & Sector Traversal**:
  - Clear hostile containment chambers and choose your path forward based on room reward types (Weapons, Upgrades, Health, or Research).
- **Character Selection**:
  - Choose from distinct operatives with unique baseline stats and starting loadouts.

---

##  Tech Stack & Requirements

| Specification | Detail |
| --- | --- |
| **Engine** | Unity 6 (`6000.0.80f1`) |
| **Render Pipeline** | Universal Render Pipeline (URP 2D) |
| **Input System** | Unity New Input System |
| **UI** | Unity UGUI & TextMeshPro |
| **Language** | C# (.NET Standard 2.1) |

---

##  Getting Started

### Prerequisites
- [Unity Hub](https://unity.com/download)
- Unity Editor version **`6000.0.80f1`** or compatible Unity 6 release.

### Installation & Running
1. **Clone the repository**:
   ```bash
   git clone https://github.com/Chanyut-Benja/lab-breakout-roguelite.git
   ```
2. **Open with Unity Hub**:
   - Open Unity Hub, click **Add** > **Add project from disk**.
   - Select the cloned project folder: `Lab_breakout_enemy_integrated`.
   - Ensure the Unity version is set to `6000.0.80f1`.
3. **Launch the Game**:
   - Navigate in Project window to `Assets/Scenes/MainMenuScene.unity` (or `GameScene.unity`).
   - Press the **Play** button in Unity Editor.

---

##  Controls

| Action | Key / Input |
| --- | --- |
| **Movement** | `W`, `A`, `S`, `D` or Arrow Keys |
| **Aim** | Mouse Cursor |
| **Attack / Shoot** | Left Mouse Button / Auto-aim weapons |
| **Interact / Select** | Left Mouse Button / `E` |
| **Pause Menu** | `Esc` |

---

##  Project Architecture

```
Assets/
├── Audio/                  # Sound effects, ambient tracks, and audio clips
├── Data/                   # ScriptableObjects (Enemies, Bosses, Upgrade Cards)
├── InputSystem_Actions     # New Input System mapping
├── Materials/              # URP 2D sprite materials and custom shaders
├── Prefabs/                # Projectiles, Enemy units, Bosses, UI elements
├── Scenes/
│   ├── MainMenuScene.unity # Title screen, character select, research tree
│   └── GameScene.unity     # Core gameplay loop and room traversal
├── Scripts/
│   ├── Characters/         # Player data and character select controllers
│   ├── Combat/             # Weapons, projectiles, hit detection, evolutions
│   ├── Core/               # Game loop, event managers, audio managers
│   ├── Enemies/            # AI behavior trees, spawn directors, boss mechanics
│   ├── Player/             # Movement, health, stats, and inventory management
│   ├── Progression/        # In-run drafting (Boons), EXP, and Meta Research
│   ├── Rooms/              # Room doors, spawn triggers, and chamber flow
│   └── UI/                 # HUD slots, health bars, popups, and menus
└── Sprites/                # Icons, character sprites, environment art
```

---

##  License
This project is for educational and portfolio demonstration purposes. All rights reserved by the author.
