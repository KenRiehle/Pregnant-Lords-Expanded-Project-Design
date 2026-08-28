# Optional Integration Architecture

## Locked behavior

Pregnant Lords Expanded is a complete standalone mod. It must work with vanilla Bannerlord
1.5.1 when Affairs of Calradia/Friends With Benefits (AOC/FWB), DramaLord, and every other
pregnancy-related mod are absent.

Optional integrations must never become required DLL references or take control of conception,
fertility, twins, pregnancy duration, or normal birth handling.

## Hook roles

The core exposes two deliberately narrow hook types:

| Hook | Intended use |
|---|---|
| `IPregnancyDataProvider` | A mod such as DramaLord may supply an existing conception date, father, due date, or configured duration when no usable native record exists. |
| `IPregnancyObservationSink` | A mod such as AOC may observe a normalized pregnancy result for later relationship-memory or blood-debt compatibility without changing the result used by the core. |

The native Bannerlord provider has the highest priority. If its active record has valid timing,
that result wins. An optional provider may fill a timing gap, but cannot overwrite a complete
native result.

Each external call is isolated. A missing assembly, renamed type, reflection failure, or thrown
exception logs one diagnostic warning and then fails open to the remaining providers.

## Planned MCM presentation

When MCM is added in a later milestone, the compatibility section should behave as follows:

| Setting | Installed | Not installed |
|---|---|---|
| Use AOC Integration | User-selectable | Disabled and marked “Not detected” |
| Use DramaLord Pregnancy Data | User-selectable | Disabled and marked “Not detected” |

Removing either external mod from an existing campaign must disable its adapter automatically.
Pregnant Lords Expanded then returns to the native provider without corrupting the save.

Pregnancy-duration mods such as PregnancyModifier do not need a dedicated switch when they patch
Bannerlord's active `PregnancyModel`. The native provider reads the resulting active duration.

## Implementation boundary

Milestone 1 includes the provider registry and observation hook surface, but it does not yet ship
the AOC or DramaLord adapters. Those adapters should be implemented and runtime-tested separately
after the native provider passes the Milestone 1 campaign acceptance tests.
