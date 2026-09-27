# Build 04d test overlay — Pregnant Prisoner Interaction Foundation

This overlay starts the prisoner/captor branch after the validated/pushed 04c pregnancy-loss notification checkpoint.
It does **not** change the locked combat-loss probabilities, 2D-E withdrawal travel, or 2D-F-C escort mechanics.

## What this test build adds

- A pregnant female noble/hero held specifically in the player's main party can disclose her pregnancy when the player talks to her.
- The disclosure is once-per-active-pregnancy and persisted across save/load.
- The player receives two explicit choices:
  - **Chivalric Maternal Release** — releases her through Bannerlord's native `EndCaptivityAction.ApplyByReleasedByChoice` path.
  - **Retain in Captivity** — leaves her prisoner and records that the player knowingly retained a pregnant captive.
- If the player initially retains her, a special Chivalric Maternal Release line remains available in normal prisoner conversation options later.
- The informed-detention captor marker is persisted for the next captivity consequence/penalty build.
- Pregnancy end, birth, combat pregnancy loss, or maternal death clears the temporary disclosure/detention state so a later pregnancy can create a new interaction.

## Deliberately deferred from 04d

04d does **not** yet apply relationship/honor penalties or captivity miscarriage/stillbirth risk. It establishes and validates the safe interaction + native release path first. The next build will consume the persisted informed-detention marker for captor responsibility and penalties.

## First live test

1. Use a save with a pregnant enemy noblewoman/hero.
2. Capture her and ensure she is in the player's **main party** prisoner roster.
3. Talk to her from the party/prisoner interaction.
4. Confirm she says she is with child before normal prisoner options.
5. First test **Retain in Captivity**:
   - she remains a prisoner;
   - log contains `decision=RetainInCaptivity, informed=True`.
6. Talk to her again and confirm the disclosure does not repeat, but the special later-release line exists.
7. Reload the pre-choice save and test **Chivalric Maternal Release**:
   - conversation closes normally;
   - she is removed from the player's prisoner roster through Bannerlord's native release action;
   - log contains `decision=ChivalricMaternalRelease, nativeRelease=True`;
   - no PLE runtime exception occurs.

Do not commit/push 04d until both retain and release paths are live-validated.
