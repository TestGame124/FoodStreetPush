# Mechanics audit — *The Last Restaurant* v0.2 (18 Sep 2026)

Audit of this branch against `The_Last_Restaurant_Mechanics_and_Starting_Layout_GDD.pdf`.
Every claim below was verified by reading the source.

## Summary: the code implements a different design

The `last-orders-gdd` branch builds **"Last Orders"** — a Heat-driven siege builder whose own
GDD is cited by section number throughout the source (§4 phases, §11 Heat, §15 consequences,
§24 architecture). Its spine is Heat: serving customers by day generates Heat, which
deterministically sizes the wave that attacks at night.

**The Last Restaurant v0.2 is a different, tighter P0 spec.** Day→Preparation→Night→Recovery;
Coins/Wood/Scrap/Tomatoes/Reputation; a Chef and a Helper running an explicit job queue; tomato
soup; a 240 HP gate and a 180 HP guard post against three authored walkers; a 500 HP diner that
can lose; and a joystick expedition.

These are not two drafts of one design. They disagree on the phase model, the resource set, the
failure state, and the meaning of leaving the base. This document records what exists, not which
document should win.

---

## P0 systems (§2)

### Base input and camera — PARTIAL

Present: `GridBuildingSystem/Scripts/_/CameraTarget.cs` (pan/zoom drag);
`Game/CameraController/CameraController.cs` mapping `CameraType{Main,Focus,Tutorial}` onto
Cinemachine 2 virtual cameras; tap selection via the legacy `IInteractableObject.OnTouch`.

Missing: pinch zoom; the 12 px tap-vs-pan threshold (§4); safe-area UI; the §4 contextual object
panel with per-object available actions; and §18's orthographic 60° landscape framing — the
project renders perspective, and the generated canvas is portrait 1080×1920.

### Grid construction — STRONG PARTIAL

The best-covered system. `LastOrders/Build/` is a complete command stack —
`PlaceBuildableCommand` (footprint, rotation, occupancy), `RotateBuildableCommand`,
`MoveBuildableCommand`, `DeleteBuildableCommand`, `RebuildAllCommand` — with dual-currency cost
(materials plus clock-minutes), a time-limited undo window, and `ExecuteBatch` with rollback.

Missing: interaction-cell reservation and clearance (§5 requires "the full footprint plus explicit
interaction cells"); all four §5 reachability validations (customer arrival→gate→seat, staff
shelter→station, defence spawn→diner, decorative-versus-collision separation); and the green/red
ghost with a stated reason (§4, §20).

Contradiction: §5 states P0 has no demolition feature, but `DeleteBuildableCommand` exists.

### Workers and jobs — ABSENT

There is no `JobService`, no job contract type, and no
`Queued/Reserved/Travelling/Working/Delivering/Completed/Blocked/Cancelled` state machine
anywhere in the project. Nothing reserves inputs or work positions.

`Survivors/SurvivorService.cs` is a **stat roster**, not a dispatcher: survivors carry `Role`,
`Trait`, `InjuryDaysRemaining`, `AwayDaysRemaining` and `DailyWage`, and they modify aggregate
numbers. They have no agent, no pathing, and no task assignment. Legacy
`Scripts/Staff/StaffBehaviour.cs` is a three-state Free/Serve/Return FSM belonging to the old game.

The §6 Chef / Helper / Mechanic split, the job priority rules, and the interruption-and-blocked-path
handling do not exist.

### Restaurant — PARTIAL, split across two layers

The new layer models the **economics**:

- `Customers/CustomerDirector.cs` — deterministic day planner; party archetype, size and arrival
  minute; guarantees the "Last Party" beat.
- `Customers/CustomerIdentity.cs` — per-visitor outcome, payment and reputation.
- `Perimeter/DoorController.cs` — doors as the service bottleneck.

The legacy layer runs the **simulation**: `VisitorSpawner/VisitorBehaviour.cs`
(Arriving→GoToTable→Waiting→Eating→Leaving), `TableBehaviour`, `CookingSystem/CookeryStation.cs`,
`Item/OrdersSystem/OrderSystem.cs`.

Missing: meal reservation and the four-meal counter store; the 3 s serve / 12 s eat / 3 s clean
timings; the dirty-table→clean cycle; one active customer per table; the single outside waiting
slot; patience expiry; and the §8 safeguard that stops arrivals rather than filling the diner with
impossible orders.

### Farming and inventory — PARTIAL

Present: legacy `Scripts/Planting/PlantPot.cs` (plant, staged growth, harvest, save);
`Economy/ResourceLedger.cs` (`Add`, `CanAfford`, `TrySpend`, `ApplyTheft`).

Missing: any LastOrders-side crop loop. **`Wood` and `Tomato` do not exist** —
`Data/ResourceTypes.cs` declares Coin, Scrap, Parts, Ammo, Medicine, Meat, Grain, Veg, Spice,
Luxury. There is no inventory capacity (§9 specifies 100 item units), no protected quest slots, no
overflow display, and no once-per-day supply request to prevent the §9 economy dead end.

### Phase controller — PARTIAL, semantically different

`DayCycle/DayCycleMachine.cs` is deep and well-built: deferred non-re-entrant transitions with an
8-hop guard, `ForceTo` for save restore, and phase predicates (`AllowsBuilding`, `AllowsService`,
`IsSiege`, `IsSaveBoundary`).

But the cycle is **Dawn→Day→Dusk→Night→Resolution**, and `TimeService` imposes a 60-clock-minute
working day. §3 says the opposite: *"There is no real-time deadline forcing a night attack. A day
is a progression cycle, not a twenty-four-hour clock."*

Missing: the §3 readiness checks (usable diner, closed valid boundary, functional gate, assigned
guard); Start Night as a deliberate gated action; modal-panel pause of night simulation; the offline
rules; and **defeat** — `DayCycle/States/NightState.cs` exits on
`timeUp && allSpawned && live.Count == 0`. There is no lose condition at all, where §3 and §10
require diner HP reaching zero to end the attack and trigger the recovery flow.

### Combat and navigation — PARTIAL, large gaps

Present and deep:

- `Night/WaveDirector.cs` — the wave plan is a pure function of (day, heat, campaignSeed); no
  `UnityEngine.Random`.
- `Night/EnemySpawner.cs`, `Night/EnemyAgent.cs` — pooled flow-field AI; archetype variety comes
  from which flow field a definition samples, with no per-archetype branching.
- `Consequence/DamageableStructure.cs` — HP, Intact/Damaged/Wrecked/Destroyed, patch and repair.
- `Consequence/StructureFire.cs`, `Perimeter/PerimeterService.cs`.

Missing:

- **The guard post entirely.** No object, no assigned-guard firing at 12 damage per second over
  9 m, no line of sight, no lowest-remaining-path-distance target selection (§10).
- **The gate** as a distinct 240 HP, 4 m object. Doors exist, but as a service mechanic.
- **The diner** as a 500 HP protected objective.
- Victory and defeat conditions; Emergency Repair (one per night, 80 HP, §10).
- Enemy path choice weighing estimated break time against detour travel time (§11); attack slots
  along walls.
- The Night 1 authored sequence — three walkers at 0/4/8 s from (42, 2), routed via (20, 2) and
  (20, 6) to the gate at (20, 8).

**Defences deal no damage.** `defenseDps`, `defenseRangeTiles` and `consumesAmmo` on
`PlacedObjectTypeSO`, and `guardDps` on `SurvivorRoleDefinition`, are written only by
`Editor/BuildableConfigurator.cs` and `Editor/ContentGenerator.cs`. No runtime code reads any of
them. The same is true of `EnemyDefinition.stealsResources` — `ResourceLedger.ApplyTheft` has no
caller.

### Tutorial and save — SAVE PRESENT, TUTORIAL AND PROGRESSION ABSENT

`Save/LastOrdersSaveParticipant.cs` and `Save/LastOrdersSaveData.cs` are solid: an ordered restore
(Time → Heat → Reputation → Ledger → Survivors → Ghosts → phase), registered in `GameLoop.Start()`,
branching on `SaveSystem.StartedFresh` so a returning player is never handed a second starting kit.

Missing: the 11-step §13 opening sequence, and any `ProgressionService` — no unlock-flag store, no
§9 milestone table, no idempotent one-time reward grants. Gating fields are authored across
`PlotTierDefinition`, `RecipeDefinition`, `PlacedObjectTypeSO` and `CrisisAbilityDefinition`, and
**nothing enforces them at runtime**. Named-night `completionReward` and `reputationReward` are
never paid out.

Known fragility: ghost restore uses `Resources.LoadAll<PlacedObjectTypeSO>("")`, which finds only
assets inside a `Resources/` folder. Every Last Orders buildable lives outside one.

### Expedition — ABSENT

Zero project-wide matches for joystick, expedition, extraction or rescue. The design's answer to
leaving the base is `Dispatch/DispatchService.cs`: an abstract, menu-driven board of destinations
with weighted outcome tiers and two-option vignettes. There is no playable off-site mode, no hero
controller, no auto-attack, and no follower.

---

## P1 / P2

Production chains (wheat → mill → flour → bakery → bread), animals and the coop, the grease trap,
the second attack lane, and the runner and breaker enemies are **all absent**. `Build_Spikes` and
`Build_NailTurret` exist as data only and, per the note above, deal no damage. Live events and
social are correctly out of P0 scope and are not implemented.

---

## Economy (§9)

The opening ledger of 200 coins / 20 wood / 6 scrap / 4 tomatoes **cannot be expressed** — Wood and
Tomatoes are not resource types. `GameBalanceConfig.startingResources` currently grants 120 Coin,
60 Scrap, 10 Parts, 20 Ammo, 2 Medicine, 12 Veg, 12 Grain, 6 Meat, 4 Spice.

Reputation is close in spirit: `Reputation/ReputationService.cs` is never spent, and its
`HighestTierReached` floor makes tiers permanent, matching §9's "service progress, never spent".

---

## Architecture scorecard (§21)

| §21 system | Verdict |
| --- | --- |
| PhaseController | Present; different phase set, no defeat |
| EconomyService | Partial — no capacity cap, no reservation/escrow API, no transaction IDs or idempotency. Atomicity is per-call. §14's "Reserved for first-night defences" has nothing to build on. |
| PlacementService | Present, as a command stack over the legacy grid |
| JobService | **Absent** |
| RestaurantService | Partial, split between the legacy and new layers |
| CombatDirector | Present; owns no victory or defeat |
| ProgressionService | **Absent** |
| SaveService | Present |

---

## Acceptance tests (§22)

**None of the ten exist.** The project has no edit-mode or play-mode test assemblies; the only
`.asmdef` files belong to third-party packages, and the only test folders are Bayat's and a
`SavingLoading/Testing` helper.

---

## Integration gaps

- `Core/LastOrdersBridge.cs` is the single seam from legacy code into the new layer. It exposes
  four members, and **only two are ever called**: `NotifyDishCooked` (`CookRecipeData.cs:69`) and
  `NotifyCustomerServed` (`VisitorBehaviour.cs:199`). `NotifyCoinEarned` and `IsServing` have no
  callers — so legacy coin income never reaches the ledger, and the old visitor spawner keeps
  running at night.
- Five of the seven Crisis verbs (`Reload`, `Bait`, `Reroute`, `Evacuate`, `Rally`) return `true`
  and expect event subscribers that do not exist. Only `Patch` and `Douse` do anything.
- `UI/LastOrdersHUD.OnCrisisVerb` has no tile picker; it resolves abilities at
  `cam.position + cam.forward * 20f`. The source comment flags this as deliberate scaffolding.

---

## What a v0.2-conformant P0 would still need

Ordered by dependency, not by size:

1. **JobService** — the job contract, reservations, and the Chef/Helper loop. Nothing in §6, §8 or
   §13 works without it, and it is the single largest absent piece.
2. **Resource model** — add Wood and Tomato; add inventory capacity, protected quest slots, and the
   reservation API §14 assumes.
3. **ProgressionService** — unlock flags and idempotent one-time grants, then enforce the gating
   fields already authored across the definition assets.
4. **Defeat and the night objective** — diner HP, victory/defeat resolution, the recovery kit.
5. **Guard post and gate** as first-class combat objects, and a runtime that actually reads
   `defenseDps`.
6. **Farming loop** on the LastOrders side, feeding the soup recipe.
7. **Tutorial** — the 11 steps of §13, event-driven and save-resumable.
8. **Expedition** — an entire second mode; the largest new surface after jobs.
9. **Acceptance tests** — all ten from §22.

---

## The measured site plan (§15–19)

Implemented separately and independently of the mechanics question, by
`Editor/BlockoutSchedule.cs` (the coordinates, once),
`Editor/BlockoutSceneBuilder.cs` (generates `Assets/Scenes/LastOrders_Blockout.unity`), and
`Editor/BlockoutValidator.cs` (asserts the result against the schedule).

The coordinate table was machine-checked and is internally consistent: no footprint overlaps, the
fence resolves to exactly 54 sections, the guard post centre and range match §11, and the interior
fits its 0.25 m shell with the stated 2.75 m kitchen opening.

Two findings worth a design decision:

- **The staff route from the dining aisle to the kitchen is 1 m wide** if the 3×3 table envelopes
  are treated as solid collision, which violates §18's own 2 m minimum for main two-way routes. The
  builder treats the envelope as reservation and only the central 1×1 table as solid, which widens
  the band to 2 m. If the 3×3 is meant to be solid, the layout needs T2 moved south or the counter
  moved north.
- **B1 entrance debris blocks the entrance twice** — it fills the 2 m door throat, and R1's initial
  state is separately "door blocked". One clearing action should unblock one thing.
