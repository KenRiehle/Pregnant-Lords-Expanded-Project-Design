Pregnant Lords Expanded — Milestone 2D-F-C
Elite Escort Execution and Surplus Troop Handoff — Test Build 03

Baseline required: locked/pushed Milestone 2D-F-B.

Extract this ZIP over the repository root:
C:\Pregnant-Lords-Expanded-Repository

First run the calculation tests. Expected new final line:
All Milestone 2D-F-C escort handoff capacity-planning tests passed.

Then build/copy the Release DLL and use the preserved pre-petition Thyrsif save.
For the first live test choose Strong Escort (50) on a land-route scenario and give the
other army party enough capacity to accept all surplus troops.

Naval routing is intentionally unchanged in this test build so roster handoff and 2D-E
withdrawal execution can be validated independently.

See docs/MILESTONE_2D-F_C_BUILD_NOTES.md for the exact invariants and test procedure.


Build 02 correction:
Strong Escort now applies an actual +5 relationship delta. Test Build 01 passed +5 through
Bannerlord's normal positive-relation action, which the diplomacy model could increase to +10.
Build 02 bypasses that positive-gain multiplier only for the 2D-F escort-choice consequence while
preserving effective-hero relation mapping, relation-change event dispatch, save/reload deduplication,
and native relationship caps. Lean remains 0 and Minimal remains -25.


Build 03 compile correction: fully qualifies TaleWorlds.CampaignSystem.Campaign.Current inside the PregnantLordsExpanded.Campaign namespace. No gameplay logic changed from Build 02.
