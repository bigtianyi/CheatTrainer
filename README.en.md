# Cheat Trainer (14 in 1) — tModLoader Edition

**English** | [简体中文](README.md)

A tModLoader mod that reimplements the 12 features of **FLiNG's
《Terraria v1.4-v1.4.4.9 Plus 12 Trainer》** from scratch, plus **two original
bonus features** (No. 13 & 14):

- **No. 13 · Sword-to-Zenith Crafting** — turn any Zenith-recipe weapon into the Zenith
  (works in vanilla *and* Calamity)
- **No. 14 · Cell Phone / Shellphone Crafting** — craft any Cell Phone or Shellphone
  from any of its ingredients, across both crafting tiers

The original trainer is a **64-bit native C++ shell that injects x86 assembly into a
32-bit .NET process**. tModLoader, however, runs on **.NET 8 / x64**, so that injection
architecture cannot be ported directly. This mod instead uses **HookGen/MonoMod to hook
at the IL level**, mapping feature-for-feature onto the original.

---

## 1. Feature Table

| # | Feature | Default Key | Original Implementation | This Mod |
|---|---------|-------------|------------------------|----------|
| 1 | Infinite Health | NumPad 1 | `statLife = 9999` | Refill in `PostUpdateMiscEffects`; `PreKill` catch to block one-shots |
| 2 | Infinite Mana | NumPad 2 | `statMana = 9999` | `statMana = statManaMax2` |
| 3 | Infinite Breath | NumPad 3 | `breath = breathMax` | Same, plus reset `breathCD` |
| 4 | Infinite Flight | NumPad 4 | `wingTime=10000, rocketTime=9999` | Same, plus grant `wingsLogic` when unarmed |
| 5 | Noclip (Ghost) | NumPad 5 | `ghost = true` | Same |
| 6 | Infinite Items | NumPad 6 | `stack = maxStack` | Refill every stack to max each frame |
| 7 | Quick Heal | NumPad 7 | Patch the 0.91/0.56 thresholds in `QuickHeal_GetItemToUse` to 1.0 | Equivalent: auto-drink the strongest healing potion whenever not at full HP |
| 8 | Full Bright | NumPad 8 | Hook `LightingEngine::UpdateLightDecay` | Raise `Lighting.GlobalBrightness` (intensity adjustable) |
| 9 | Free Craft | NumPad 9 | Hook `Recipe::CollectedEnoughItemsToCraftRecipeNew` | Hook `Terraria.On_Recipe.CollectedEnoughItemsToCraftRecipeNew` |
| 10 | Ignore Crafting Stations | NumPad 0 | Hook `Recipe::PlayerMeetsTileRequirements` | Hook that method + `PlayerMeetsEnvironmentConditions` |
| 11 | Super Damage | NumPad * | `GetWeaponDamage` returns 999999 | `damage.Flat += 999999` in `ModifyWeaponDamage` |
| 12 | Game Speed Hack | NumPad / | Standalone `TrSpeedHack_x86.dll` (speed gear) | Hook `Main.DoUpdate`, replay/skip logic frames by multiplier |
| **13** | **Sword-to-Zenith** ★original | **`` ` `` (backtick)** | — (not in the original trainer) | Look up the Zenith recipe at runtime; only allow the craft check, consumption stays vanilla |
| **14** | **Cell Phone / Shellphone Craft** ★original | **F5** | — (not in the original trainer) | Look up both Cell Phone + Shellphone recipes at runtime (prefix match); same allow-only approach |

Press **NumPad .** to open/close the floating control panel — you can click the toggles
and drag the sliders there.

> On features 9 & 10: on the target tModLoader build those three methods have the real
> signatures `static bool CollectedEnoughItemsToCraftRecipeNew(Recipe)`,
> `static bool PlayerMeetsTileRequirements(Player, Recipe)` and
> `static bool PlayerMeetsEnvironmentConditions(Player, Recipe)`. Simply returning `true`
> is equivalent to the original trainer "skipping the check".

---

## 2. Feature 13 in Depth: Sword-to-Zenith

### What it does

If your inventory contains **any one weapon required by the Zenith recipe**, the Zenith
recipe lights up and becomes craftable — all missing materials are ignored. The weapon
you use **is still consumed normally**, so a Copper Shortsword effectively "becomes" a
Zenith. If you have N qualifying weapons, you can craft N Zeniths
(Copper Shortsword + Star Wrath = 2 Zeniths).

### Why the recipe must be resolved dynamically, not hard-coded

The Zenith recipe differs by environment:

| Environment | Recipe |
|---|---|
| Vanilla | Terra Blade + Meowmere + Star Wrath + Influx Waver + Horseman's Blade + Seedler + Star Wrath + Bee Keeper + Enchanted Sword + Copper Shortsword (×1 each) |
| Calamity | All 10 above are **kept**, **plus** Auric Bar ×5 |
| Other mods | "Better Zenith" style mods may rewrite it again |

So `TrainerZenithCraft` always **resolves the recipe at runtime** ("which materials does
the recipe whose output is the Zenith actually need?"). No code change is required when
the environment changes, and nothing breaks if an ID is absent.

### The key trick: allow the check, never touch consumption

```csharp
// TrainerHooks.cs — reuse the same hook as feature 9 (the master "enough materials?" gate)
private bool HookFreeCraft(On_Recipe.orig_CollectedEnoughItemsToCraftRecipeNew orig, Recipe recipe)
{
    TrainerConfig cfg = TrainerConfig.Instance;
    if (cfg != null && cfg.FreeCraft) return true;             // feature 9: ignore everything
    if (TrainerZenithCraft.ShouldUnlock(recipe)) return true;  // feature 13: allow only this recipe
    return orig(recipe);
}
```

| Phase | Owner | Effect |
|---|---|---|
| **Craft check** (is the recipe lit?) | This mod | Missing materials are "ignored" here |
| **Material consumption** | The game's original flow | Only consumes what you **actually own** |

That is why it never over-produces: we only make the button clickable; we never touch what
gets deducted. The game won't deduct materials you don't have, so your Copper Shortsword
is consumed as usual.

### Only "weapon"-type materials count

Calamity adds Auric Bar (not a weapon) to the recipe. If we allowed the craft based on
"any material in hand", holding only a bar would also work — which is wrong. So the cache
only collects materials with `damage > 0`, matching the "any sword" requirement.

### Compatibility

| Environment | Result |
|---|---|
| Vanilla | ✅ Craftable from a Copper Shortsword at the start |
| Calamity | ✅ Works; but **Auric Bar requires defeating Yharon to mine**, a Calamity end-game gate this feature cannot skip |
| Other recipe-modifying mods | ✅ Dynamic lookup, adapts automatically |

Default key `` ` `` (OemTilde): the numpad is fully used by features 1–12, and `` ` `` is
free in Terraria's default bindings.

---

## 3. Feature 14 in Depth: Cell Phone / Shellphone Crafting

### What it does

Unlocks both crafting tiers at once — **Cell Phone → Shellphone**. If your inventory
contains any one material required by **that particular recipe**, the recipe lights up and
becomes craftable, with missing materials ignored.

### Why both tiers must be covered together

```
Cell Phone = PDA + Magic Mirror
Shellphone = Cell Phone + Magic Conch + Demon Conch
```

The Shellphone recipe includes the Cell Phone, and the Cell Phone is itself a complex
recipe. If only the Shellphone recipe were unlocked, a player holding a Magic Conch still
couldn't craft one — because the Cell Phone is missing, and that can't be crafted either.
So both recipes are taken over together.

| If you only have… | You can craft |
|---|---|
| Magic Mirror / PDA | Cell Phone |
| Cell Phone / Magic Conch / Demon Conch | Shellphone (that item is consumed) |

### ⚠️ Ingredient sets must be kept per-recipe (a pitfall we hit)

The two recipes' ingredients **must not be merged into one union set**, or you get
"cross-tier false unlocks":

| Approach | With only a Magic Mirror | Result |
|---|---|---|
| ❌ Merged union | Cell Phone lit + **Shellphone also lit** | Craft once, get a free Shellphone |
| ✅ Grouped per recipe | Only Cell Phone lit | As intended |

So the cache is a `Dictionary<productType, HashSet<ingredientType>>`, and the check only
consults **that recipe's own** group.

### Differences from the Zenith implementation

| | Feature 13 (Zenith) | Feature 14 (Cell Phone) |
|---|---|---|
| Target recipes | Only `Zenith` | `CellPhone` + `Shellphone` |
| Ingredient filter | Only `damage > 0` (exclude Auric Bar) | **No filter** (all tools, `dmg = -1`) |
| Ingredient scope | Single recipe, no grouping | **Grouped by product** to prevent cross-tier unlocks |
| Name matching | Exact internal-name match on the product | `StartsWith` prefix match, catching all 4 Shellphone variants at once |

The Shellphone has **5 item IDs** (5358 Home / 5359 Spawn / 5360 Ocean / 5361 Underworld,
plus 5437 dummy). Hard-coding IDs is fragile; matching internal names starting with
`Shellphone` catches them all.

### The key trick

```csharp
// TrainerHooks.cs — again reusing the feature-9 "enough materials?" gate
private bool HookFreeCraft(On_Recipe.orig_CollectedEnoughItemsToCraftRecipeNew orig, Recipe recipe)
{
    TrainerConfig cfg = TrainerConfig.Instance;
    if (cfg != null && cfg.FreeCraft) return true;                 // feature 9
    if (TrainerZenithCraft.ShouldUnlock(recipe)) return true;      // feature 13
    if (TrainerShellphoneCraft.ShouldUnlock(recipe)) return true;  // feature 14
    return orig(recipe);
}
```

Same "allow the check, leave consumption to the game" principle — so one Magic Conch
simply "becomes" one Shellphone. It never over-produces.

Default key **F5**: F1–F4 are taken by vanilla (`Loadout1/2/3`, `ToggleCameraMode`);
F5 is free in Terraria's default bindings.

---

## 4. Installation

The `.tmod` is already installed automatically — **you don't need to compile anything**:

```
C:\Users\<username>\Documents\My Games\Terraria\tModLoader-preview\Mods\CheatTrainer.tmod
```

Launch tModLoader → **Mods** → find `Cheat Trainer (14 in 1)` → enable → **Reload Mods**.
(You must enable it manually the first time; `build.txt` sets no `side`, so it loads on
both client and server.)

> Note: this setup uses the `tModLoader-preview` save directory. If your game reads
> `My Games\Terraria\tModLoader\Mods` instead, just copy the `.tmod` there.

---

## 5. Usage

1. In-game, press **NumPad .** to open the panel, or press NumPad `1`–`9`, `0`, `*`, `/`
   to toggle features 1–12 directly (numbered in order). Feature 13
   (Sword-to-Zenith) is on `` ` `` (backtick); feature 14 (Cell Phone Crafting) is on **F5**.
2. Every toggle **saves automatically** to the mod config, so it persists across sessions.
3. To rebind: **Settings → Controls → Cheat Trainer**. All 15 entries
   (`Cheat0`–`Cheat13` + `TogglePanel`) are listed there. The floating panel shows the
   **actual bound keys in real time** (it reads `ModKeybind.GetAssignedKeys`, not hard-coded
   defaults), showing "Unbound" when nothing is bound.
4. While the panel is open, clicks on the panel won't trigger in-game mining/use. Close the
   panel to play normally.
5. Drag the panel by its title bar.

> ✅ **Zero key conflicts**: default hotkeys all live on the numpad. Checking the local
> `input profiles.json`, vanilla only uses numpad `Add`(+) = zoom in map and `Subtract`(-)
> = zoom out, while `NumPad0`–`NumPad9`, `Multiply`(*), `Divide`(/), `Decimal`(.) are
> **all free** — the mod uses only the latter. Feature 13 uses `` ` `` (OemTilde) since the
> numpad is full; it is also free by default. Feature 14 uses **F5** — F1–F4 are taken
> (loadouts 1/2/3, toggle camera), and F5–F12 are all free, so the earliest is used.
>
> ⚠️ **One prerequisite: NumLock must be ON**, otherwise the numpad degrades into arrow keys.

### The three sliders

| Slider | Range | Default | Notes |
|--------|-------|---------|-------|
| Game Speed | 0.1x ~ 10.0x | 2.5x | Mirrors the original speed gear |
| Damage Bonus | +1000 ~ +999999 | +999999 | Mirrors the original 999999 return |
| Brightness | 1.0 ~ 5.0 | 3.0 | 1.0 = vanilla brightness |

---

## 6. Building It Yourself

The source lives at the repo root (i.e. `CheatTrainer/`). tModLoader ships its own
Roslyn — **no .NET SDK required**:

```bash
cd /d/SteamLibrary/steamapps/common/tModLoader
./dotnet/dotnet.exe 'D:\SteamLibrary\steamapps\common\tModLoader\tModLoader.dll' \
    -server -build '<repo root>' -unsafe -define 'JETBRAINS_ANNOTATIONS'
```

(Replace `<repo root>` with the absolute path to your cloned `CheatTrainer` folder.)

On success it packages and installs into the Mods directory automatically.

Alternatively, copy the whole `CheatTrainer` folder into
`My Games\Terraria\tModLoader-preview\ModSources\` and hit "Build + Reload" in-game.
(Change `TModLoaderPath` in the csproj to your own install path.)

---

## 7. Bilingual (Chinese / English)

The mod ships both Chinese and English text, covering the **config UI, floating panel,
chat messages and hotkey names**.

### Follow the game language (default)

Text follows the game language by default.

### Switch language per-mod

**Settings → Mod Configuration → Cheat Trainer (14 in 1) → Language → UI Language**:

| Option | Effect |
|---|---|
| `Follow Game` (default) | Use the game's language |
| `English` | Force English |
| `中文` | Force Chinese |

Once selected, **even the config page itself** switches language (close and reopen the
config page to see it).

### How it works

Strings live in 4 hjson files under `Localization/`. Each file contains three copies:

```
UI: { PanelTitle: ... }                ← native key, follows game language
LangEn: { UI: { PanelTitle: ... } }    ← English mirror
LangZh: { UI: { PanelTitle: ... } }    ← Chinese mirror
```

This way **both languages are always readable regardless of culture**, which is what makes
the in-mod language switch possible.

The switch hooks `Language.GetText`: when a specific language is chosen, it inserts
`LangEn.` / `LangZh.` after `Mods.CheatTrainer.` in the key and looks it up again. Hooking
at the `Language` layer is necessary because **the mod config UI is rendered by tModLoader
itself** (via `ConfigManager` → `Language.GetText`); without this hook the config page
wouldn't follow along.

> The hook only processes keys starting with `Mods.CheatTrainer.`; everything else passes
> through untouched. The prefix check runs first, so the cost to the game's own text lookup
> is a single string comparison.

The 4 hjson files are generated by `_tools/gen_localization.py`. Edit the script and
regenerate rather than hand-editing, to keep both languages in sync:

```bash
python _tools/gen_localization.py
```

---

## 8. Known Limitations

- **Noclip (NumPad 5)**: uses vanilla's `Player.ghost` mechanic, exactly like the original
  trainer — you can't attack/use items while phasing; turning it off restores normal play.
- **Game Speed Hack (NumPad /)**: implemented by replaying `Main.DoUpdate`. There's a
  "max 10 steps per frame" cap to prevent lock-ups. At low speeds (<1x) motion looks
  choppy — that's inherent to slow motion.
- **Full Bright (NumPad 8)**: adjusts `Lighting.GlobalBrightness`. If it's not bright
  enough, drag the slider right.
- **Multiplayer**: these are local modifications and will be overridden by server
  anti-cheat/sync, or get you kicked. **Single-player only.**
- Feature 6 (Infinite Items) can desync your inventory in multiplayer — again, best solo.
- **Feature 13 (Sword-to-Zenith)**:
  - Only recognizes the 10 vanilla Zenith-recipe weapons (**Tin Shortsword does NOT work**;
    only Copper Shortsword does)
  - Still requires standing near a Mythril/Orichalcum Anvil by default; can be disabled in config
  - **Does not skip Calamity boss progression**: Auric Bar can be ignored, but "must defeat
    Yharon to mine Auric Ore" is a Calamity end-game gate this cannot bypass
  - The recipe cache invalidates and rebuilds on **world switch / mod reload**
- **Feature 14 (Cell Phone / Shellphone Crafting)**:
  - Both tiers covered: Cell Phone (PDA + Mirror) and Shellphone (Cell Phone + 2 Conches)
  - Still requires a Tinkerer's Workshop by default; can be disabled in config
  - All 4 Shellphone variants (Home / Spawn / Ocean / Underworld) are covered
  - With one Magic Conch, press F5 and it "becomes" a Shellphone (Cell Phone and Demon
    Conch are ignored)
  - Same allow-only approach — consumption stays with the game, so no over-production
  - Recipe cache invalidates and rebuilds on **world switch / mod reload**

---

## 9. License & Disclaimer

### License

This project is released under the **MIT License** — see [LICENSE](LICENSE). You are free
to use, modify and distribute it.

### Relationship to FLiNG's trainer

This mod **is an independent reimplementation and contains no third-party trainer code or
binaries**:

| | FLiNG's trainer | This mod |
|---|---|---|
| Form | Standalone `.exe` + 32-bit DLL injection into the game process | Pure tModLoader mod (.NET 8 / x64) |
| Technique | Named pipes + x86 assembly injection | Harmony/HookGen IL-level hooks |
| License | Third-party copyrighted work | Original, MIT |

The feature list references publicly documented trainer features, but **all code, assets
and binaries are original to this repository**. No third-party files are bundled or
redistributed.

> **A note on the name**: this project is deliberately *not* called "Terraria Trainer",
> which is FLiNG's product name. Calling it **Cheat Trainer** keeps it clearly distinct,
> matches the naming convention of tModLoader cheat mods (Cheat Sheet, Cheat Hotkeys…),
> and avoids any impression of being an official FLiNG release or repack.

### Disclaimer

- This is a **cheat mod**, intended for **single-player** fun only. Using it in multiplayer
  may desync you with the server or get you kicked.
- Using cheats **significantly reduces the challenge** and can make your save "too easy".
- Assess the risk yourself. The author is not liable for any save corruption or loss
  resulting from use of this mod.

### Credits

- [tModLoader](https://github.com/tModLoader/tModLoader) — the mod loader and runtime
- [Re-Logic](https://terraria.org/) — creators of Terraria
