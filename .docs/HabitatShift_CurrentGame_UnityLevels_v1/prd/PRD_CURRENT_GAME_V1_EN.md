# Habitat Shift PRD — Current Product v1

## 1. Product vision

Habitat Shift is a tactile mobile spatial puzzle. Players directly drag multi-cell Habitats across a continuous board to collect every same-color Sproutling. Controls must feel immediate and forgiving around valid tight clearances while preserving strict authored geometry and collision legality.

The target audience enjoys short, calm mobile puzzle sessions, tactile feedback, Woodland Orthographic presentation, and a progression from direct manipulation to Elevators, assists, and movement constraints.

## 2. Core loop

1. Continue or select an unlocked level.
2. Touch a Habitat and drag freely in continuous 2D board space.
3. Collect compatible Sproutlings when a valid footprint cell receives them.
4. Avoid foreign-color Sproutlings, other Habitats, obstacles, masks, and bounds.
5. Release and settle to a strict-legal integer anchor.
6. Complete all movable Habitats, mandatory Elevator queues, and remaining Sproutlings.
7. Watch the victory sequence, then choose Next, Replay, or Level Select.

## 3. Screens and navigation

- **Splash:** concise brand entry with no fake loading UI.
- **Home:** real progress-derived Continue, Select Level, and Settings.
- **Level Select:** real current/completed/unlocked/locked hierarchy and catalog difficulty.
- **Gameplay:** Close–Level–Pause HUD, board, Undo/Restart, and three assists.
- **Pause:** Resume, Restart, Settings, and exit navigation.
- **Settings:** functional persisted Music, Sound Effects, and Haptics controls.
- **Tutorial/Onboarding:** contextual drag, Elevator, assist, and constraint guidance.
- **Result:** appears only after the victory presentation gate; Next, Replay, Level Select.

Back navigation preserves the active session where required. The product has no fabricated rewards, currency, scoring stars, or fake state.

## 4. Gameplay and progression

The production catalog contains 18 levels:

- L1–L6: continuous core, footprint, and routing progression.
- L7: Elevator introduction.
- L8: Nest Bloom unlock.
- L9–L11: queue and multi-source routing.
- L12: Garden Shift unlock and four-way supply.
- L13–L14: combined-mechanic consolidation.
- L15: horizontal/vertical movement constraints.
- L16: Root Trim unlock with constraint/Elevator combination.
- L17: crossed deliveries.
- L18: current-system mastery.

Production difficulty values are `TUTORIAL`, `EASY`, `NORMAL`, `HARD`, `SUPER_HARD`, and `MASTERY`. Catalog geometry and metadata are authored truth and must not be rebalanced during import.

## 5. Assists

- **Nest Bloom:** select an eligible Habitat and collect compatible Sproutlings under current runtime eligibility.
- **Garden Shift:** select a Sproutling and compatible Habitat; collect without moving Habitat geometry.
- **Root Trim:** select an eligible Habitat and trim its footprint; Restart restores the authored footprint.

Charges are spent only on successful application. Invalid targeting and Cancel produce no success VFX and consume no charge. Production uses persisted unlocks/charges. Debug may display `∞` without reading or writing charge state, but eligibility remains real.

## 6. Feedback, presentation, and victory

The current direction is **Woodland Orthographic**: dark forest, walnut/oak, moss/stone, cream text, and emerald primary actions. Dynamic Habitats and Sproutlings remain the visual focus. Controls keep at least a 48dp-equivalent touch target and the frame may never obscure edge entities.

Collection, blocked contact, assists, Elevator release, Habitat completion, and level completion emit exactly-once presentation events. Sound, haptics, and VFX are independent. Pause, Settings, navigation, and recomposition must not replay consumed effects.

Victory locks gameplay input while preserving the board, plays 2900 ms of celebration, holds for 700 ms, then opens Result after 3600 ms total. With system animation scale zero, skip the cinematic delay and keep only a brief confirmation.

## 7. Persistence

Persist highest unlocked level, last selected/Continue target, per-level best moves, tutorial/onboarding keys, Music, Sound Effects, Haptics, assist unlocks, and assist charges.

Never persist pointer state, active drag, contact manifold, soft pose, settle animation, transient VFX, modal animation, or victory timestamps.

## 8. Accessibility and performance

- Player-facing labels and state descriptions; never expose raw IDs or debug enums.
- Screen-reader order, contrast, and text scaling must preserve gameplay readability.
- Input and rendering must share the same board transform.
- Target stable 60 FPS on the target device with no progressive degradation during held contact or long drags.
- Dense L18 must react to direction changes on the next pointer/frame update.

## 9. Status and boundaries

This PRD records current behavior and does not declare M8 or M9 frozen. Only approved L1–L18 are exported. L19–L50 and unapproved mechanics are out of scope. A Unity port must achieve runtime parity, not merely deserialize the catalog.
