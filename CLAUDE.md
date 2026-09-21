# Death Recap — Valheim mod

A BepInEx/Harmony mod for Valheim: on death, shows a recap of what actually
killed you (attacker, damage type, damage amount) plus a rule-based
situational tip generated from tracked context — recent stamina level,
recent hit frequency, and active status effects (wet, freezing, poison,
tarred, etc.) at the moment of death. Not a joke mod like BruhGreydwarfs/
DagothTrolls — a real QoL mod, born from an earlier "what Valheim mods are
genuinely missing" research pass that flagged this as an unclaimed niche
(re-verified fresh via Thunderstore search before building — closest
existing mod, "Valkyrie Death Messages", is confirmed pure flavor-text with
no damage/stats/tips at all).

## Environment

Same machine/setup as `ValheimBruhGreydwarfs`/`ValheimDagothTrolls`:
- Valheim at `C:\Program Files (x86)\Steam\steamapps\common\Valheim`
- Mods via Vortex, deployed to `...\Valheim\BepInEx\plugins`
- .NET 8 SDK on PATH, target framework `netstandard2.1`
- `Character`/`Player`/`HitData`/`SEMan`/`ZNetScene` all live in
  `assembly_valheim.dll`.
- **New gotcha found for this project**: `Localization` (used to translate
  a creature's `m_name` key into a display string) is NOT in
  `assembly_valheim.dll` — it's in **`assembly_guiutils.dll`**. Had to add
  that reference to the csproj (on top of the usual `Assembly-CSharp` +
  `assembly_valheim` + `UnityEngine` set) to fix a CS0103 "Localization does
  not exist in the current context" error. Found empirically by scanning
  all the `valheim_Data\Managed\assembly_*.dll` files for the type
  definition via `System.Reflection.Metadata`, same technique used earlier
  to find `Character` in `assembly_valheim.dll` instead of
  `Assembly-CSharp.dll`.

## Implementation

Mod GUID `mod.deathrecap`, name "Death Recap". Everything in one
`Plugin.cs`:

- **Hit tracking**: `[HarmonyPatch(typeof(Character), "Damage")]` postfix
  filters to `__instance == Player.m_localPlayer`, records each hit (time,
  total damage via `HitData.DamageTypes.GetTotalDamage()`, majority damage
  type via `GetMajorityDamageType()`, `HitData.m_hitType`) into a
  12-second rolling `List<HitRecord>` (`HitTracker`).
- **Vitals tracking**: `Plugin.Awake` starts a coroutine sampling
  `Player.GetStamina()`/`GetHealth()` every 0.25s into a 12-second rolling
  `List<VitalSample>` (`VitalsTracker`) — used to answer "was stamina
  empty in the last few seconds before death," which isn't knowable from
  the death-instant state alone (by the time `OnDeath` fires, stamina may
  have partially regenerated or been irrelevant to the killing blow).
- **The recap trigger**: `[HarmonyPatch(typeof(Player), "OnDeath")]`
  postfix, filtered to the local player. Needs `Player.m_lastHit` (the
  killing blow's `HitData`, already used by vanilla `OnDeath` for its own
  death-stat bookkeeping) — that field's accessibility wasn't checked
  before use; instead of the usual "try direct access, fall back to
  `AccessTools.Field` on CS0122" flow, went straight to Harmony's
  `___m_lastHit` postfix-parameter injection (bypasses C# accessibility via
  direct IL field access, works regardless of private/protected). Compiled
  fine first try — turned out to be the more convenient choice here since
  it skips a build-fail-diagnose round trip.
- **Attacker name resolution**: `ZNetScene.instance.FindInstance(hit.m_attacker)`
  → `GameObject` → `Character.m_name` (a localization key) →
  `Localization.instance.Localize(...)` for a display string; falls back to
  the GameObject's own (Clone)-stripped name if no `Character` component,
  or `null` (environmental death, shown as "Died to {HitType}" instead) if
  the attacker ZDOID doesn't resolve at all (e.g. fall damage, drowning).
- **Status effects at death**: `player.GetSEMan().HaveStatusEffect(SEMan.s_statusEffectX)`
  for Wet, Freezing, Cold, Burning, Poison, Frost, Lightning, Spirit,
  Smoked, Tared, Encumbered — all real static hash fields on `SEMan`,
  confirmed via decompilation.
- **Tip engine** (`TipEngine`): explicitly rule-based per user's own
  decision, NOT a real LLM call — user was asked and picked this
  deliberately over an actual AI API, because a shared mod needing every
  friend to configure their own API key (or shipping one shared key, a
  real cost/security liability) was judged not worth it for a joke/QoL mod
  distributed as a zip. ~20 `(Func<DeathContext,bool>, string)` rules
  covering combinations the user specifically asked for ("lots of
  combinations of status effects, like poison, wet, etc") — stamina +
  hit-type combos, wet+freezing, tar+burning, poison+low-stamina,
  encumbered+drowning, burst-hit detection (3+ hits in 3s), per-damage-type
  flavor (lightning/frost/spirit), per-hit-type flavor (fall/drowning/
  freezing/edge-of-world/tree). All matching rules are collected and one is
  picked at random (not just first-match) so repeat deaths under similar
  circumstances don't always show the identical line. Small fallback flavor
  pool when nothing matches.
- **Display**: reuses vanilla's own `Character.Message(MessageHud.MessageType.Center, text)`
  — the same call vanilla itself uses for "$msg_youdied" — rather than a
  custom UI canvas. This is the deliberate MVP choice: no custom UI/canvas
  work needed, ships as a 2-line message (killer line + italicized tip
  line) reusing existing game UI. Could be upgraded to a proper custom
  overlay panel later if the plain-message presentation feels too plain in
  practice.
- Also logs the recap via BepInEx logger (`Plugin.Log.LogInfo`) on every
  death, mainly useful for iterating on/debugging the tip rules without
  needing to actually die repeatedly to read the in-game text closely.

## Status / next steps

- [x] Project folder created at
      `C:\Users\TiesB\OneDrive\Development\ValheimDeathRecap`
- [x] Wrote `DeathRecap.csproj` + `Plugin.cs`
- [x] `dotnet build` — succeeded after adding the `assembly_guiutils`
      reference for `Localization` (only real surprise this round)
- [x] Copied built `DeathRecap.dll` into
      `...\Valheim\BepInEx\plugins\DeathRecap\`
- [ ] **Not yet verified in-game at all.** This is a much bigger surface
      than the sound-swap mods (multiple Harmony patches, a coroutine, a
      20-rule engine, attacker-name resolution) — needs real playtesting:
      die in a few different ways (enemy hit, fall, drowning, burning,
      poisoned, while wet/cold, while encumbered, multiple hits in quick
      succession) and confirm the recap text is accurate and each rule
      fires when expected. Check `BepInEx/LogOutput.log` for
      `[Info :Death Recap]` lines to cross-reference what actually matched
      per death.
- [ ] Not yet packaged/shared with friends — hold off until verified
      in-game, same lesson as re-exporting BruhGreydwarfs/DagothTrolls
      after changes.
- [ ] Possible future polish (not started): a proper custom UI overlay
      instead of reusing `MessageHud`, once the plain-text version is
      confirmed working and the user has opinions on presentation.
