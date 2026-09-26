# Milestone 2D-F-B — Player Withdrawal Event UI and Escort Choice Staging

Baseline: Milestone 2D-F-A (locked and pushed after calculation + Bannerlord smoke/regression testing).

This overlay adds the player-authority withdrawal event choices using Bannerlord's native
`MultiSelectionInquiryData` / `InquiryElement` presentation, the same mechanism used by the
Random Events mod reference.

Player-authority choices:

- Approve Withdrawal — Strong Escort (50)
  - relationship consequence preview: +5
  - welfare: High
- Approve Withdrawal — Lean Escort (35)
  - relationship consequence preview: 0
  - welfare: Standard
- Approve Withdrawal — Minimal Escort (5)
  - relationship consequence preview: -25
  - welfare: SevereRisk
  - responsibility warning displayed
- Order Her to Remain in Service
  - existing denial / CommanderOverride logic remains intact
  - existing monthly denial relationship consequence remains intact

Safety boundary for 2D-F-B:

- Approval choices are persisted as a selected escort plan by request.
- The approved decision is marked final for that request so Escape/reload cannot create duplicate prompts.
- 2D-F-B DOES NOT move troops.
- 2D-F-B DOES NOT apply +5 / -25 yet.
- 2D-F-B DOES NOT start the 2D-E approved-withdrawal travel lifecycle yet.
- 2D-F-C will consume the staged plan, allocate/transfer the roster, apply the one-time relationship consequence, then authorize the proven 2D-E lifecycle.
- Denial remains fully live and continues through the existing relationship/responsibility path.
- The pregnant-player (MC herself) flow is unchanged.

Persistence:

- New save ledger: `PLE_M2DF_PlanByRequest`
- Pregnancy cleanup removes staged escort plans associated with that pregnancy.
- Existing enum numeric values are preserved. New player choices are appended at values 5-7.

Expected calculation-test terminal line after extraction/build:

`All Milestone 2D-F-B player escort-choice mapping tests passed.`
