# GKit.* — test plan

## Current state

**187 tests, all passing** — 159 in `GKit.Tests` and 28 in `GKit.Tests.Blazor`.
Every test that was red when this plan was written is now green; see `GKIT-CHANGES.md`.

```
dotnet test GKit.Tests/GKit.Tests.csproj
dotnet test GKit.Tests.Blazor/GKit.Tests.Blazor.csproj
```

There is no tier D suite — see "Tier D — retired" for what replaced it and the manual
checklist that covers the remainder.

Conventions match `Benati.Rifiuti.Test`: xUnit 2.9.3, `Microsoft.NET.Test.Sdk` 18.7.0,
`coverlet.collector`, `net10.0`, `<Using Include="Xunit" />`.

---

## 1. Testing strategy per package

The suite splits into four tiers by what a test needs in order to run.

| Tier | Needs | Packages | CI |
|---|---|---|---|
| **A — pure** | nothing | Reporting, Pdf, Settings (logic), DbMigration (store), Quartz (config), PLC (mapping/options), RENTRI (cache, options, headers), SmtpHost (routing) | every push |
| **B — in-process infra** | SQLite, `TestServer`, `WebApplicationFactory` | EntityFramework, Settings (DB), Application, SmartCardHost (hub), Mqtt (server), RENTRI (`HttpMessageHandler` stub) | every push |
| **C — component** | bUnit | BlazorExt, MudBlazorExt, Authentication.Blazor, SmartCardHost.Blazor | every push |
| **D — integration** | real hardware/network | *retired — see below* | manual checklist |

Tiers A, B and C are hermetic and run in about two seconds. There is no tier D suite: the
services are not available to this project, so it would be permanently skipped.

### Seams that needed to exist first — all now in place

These refactorings were prerequisites for the tests below. All have been done:

| Package | Extract | Unlocks |
|---|---|---|
| `GKit.Quartz` | `JobScheduleResolver` — pure function `(IEnumerable<Type>, GKitQuartzOptions) → IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>`, lifted out of the `ApplicationStarted` lambda in `UseGKitQuartz` | enabled/disabled precedence, cron vs interval semantics, abstract-type filtering, trigger identity |
| `GKit.PLC` | make `PlcContextOptions` / `EntityPropertyDescriptor` visible via `InternalsVisibleTo`, or expose a public `PlcModel` read model | CLR→`VarType` mapping (the S1 in 9.1), connection-string parsing |
| `GKit.RENTRI` | header parsing lifted onto `BaseClient` (which exists so the stubs stay regenerable) | `Retry-After`, paging headers, demo/live base URL |
| `GKit.Application` | `Application.WrapAsync(args, Func<IHost>) → Task<int>` | exit codes, runner dispatch, `--help` |
| `GKit.SmtpHost` | `SmtpRouteAttribute.IsMatch` → `internal` is fine with `InternalsVisibleTo`; hoist controller discovery into an injectable `IRouteTable` | route matching, discovery caching |
| `GKit.Reporting` | `XlsReporterOptions` with `AutoSizeColumns` | rendering without the SkiaSharp dependency |

`InternalsVisibleTo Include="GKit.Tests"` is declared per-project in `GKit.PLC` and
`GKit.RENTRI` — the only two that needed it — rather than globally.

---

## 2. What is implemented

| Area | Tests | Notes |
|---|---|---|
| `GKit.Tests/EntityFramework` | 23 | soft delete, revisions, reflection copying; `Creating_a_revision_issues_no_extra_query` pins the removal of the per-entity `SELECT` |
| `GKit.Tests/Settings` | 14 | culture round-trip, `Nullable<T>`/enum conversion, key-prefix isolation, multicast notification |
| `GKit.Tests/Reporting` | 20 | RFC 4180 CSV, typed XLSX cells, reporter reuse, grouping incl. the BFer scenario |
| `GKit.Tests/DbMigration` | 11 | mapping-store round trip, reverse lookup, index resilience, pending-change visibility |
| `GKit.Tests/PLC` | 32 | the CLR→`VarType` table and address-derived widths — the highest-risk logic in the suite |
| `GKit.Tests/Quartz` | 15 | allow/deny precedence, schedule semantics, type discovery, key stability |
| `GKit.Tests/RENTRI` | 26 | `ApiResultsCache` under concurrency, environment selection, `Retry-After`, paging headers |
| `GKit.Tests/Authentication` | 16 | `LocalUrl` open-redirect guard |
| `GKit.Tests/SmartCardHost` | 2 | guards the recovered `AddSmartCardHostCheck` API at compile time |
| `GKit.Tests.Blazor` | 28 | tier C — see below |

Two test-design notes worth keeping:

- **Isolate per test, not per class.** Tests that assert on row counts each need their own
  in-memory database; a shared `IClassFixture` let one test's seed leak into the next and
  produced confusing failures twice during this work.
- **Split options types per concern.** An unsupported property type makes *every* round trip of
  its owning type throw, so a single shared `AppOptions` would have masked which conversion was
  actually broken.

## 3. Proposed tests not yet implemented

### `GKit.Quartz` (tier A, after extracting `JobScheduleResolver`)

- `DisabledJobs_is_honoured_when_EnabledJobs_is_also_set` — **1.3** precedence
- `EnabledJobs_acts_as_an_allow_list`
- `Neither_list_set_schedules_everything`
- `TimeSpanSchedule_produces_a_repeating_interval_trigger` — **1.2** (the S1 semantics bug)
- `CronSchedule_produces_a_cron_trigger_with_the_given_expression`
- `Abstract_job_types_are_skipped` / `Interfaces_are_skipped` / `Open_generics_are_skipped` — **1.4**
- `Assemblies_that_fail_to_load_types_are_skipped_not_fatal` — **1.4**
- `Job_keys_are_stable_across_restarts` — **1.5**
- `Trigger_keys_are_unique_when_the_same_type_name_appears_twice` — **1.5**
- `Null_JobTypeName_fails_validation_without_throwing` — **1.6**
- `Multiple_schedule_attributes_on_one_job_produce_multiple_triggers`
- DI test: `UseGKitQuartz_without_AddQuartzCheck_does_not_schedule_an_unresolvable_probe` — **1.1**

### `GKit.PLC` (tier A, after `InternalsVisibleTo`)

- `[Theory]` over the CLR→`VarType` table — **9.1**, the highest-value single test in this plan:
  `bool→Bit`, `byte→Byte`, `short→Int`, `ushort→Word`, `int→DInt`, `uint→DWord`,
  `float→Real`, `double→LReal`, `string→String`, `DateTime→DateTimeLong`
- `Unmapped_clr_types_throw_rather_than_defaulting_to_DWord` — **9.1**
- `[Theory]` over connection strings: `"10.0.0.1"`, `"10.0.0.1:102"`, `"10.0.0.1,0"`,
  `"10.0.0.1:102,0,1"`, and malformed inputs
- `Ports_above_32767_are_accepted` — **9.4**
- `ToPlcAddress_preserves_the_inferred_VarType`
- `HasConversion_round_trips_through_the_converter`
- `Property_expressions_that_are_not_member_access_throw_a_clear_error`

### `GKit.RENTRI` (tier A/B, with a stub `HttpMessageHandler`)

- `ApiResultsCache` under concurrency: 100 parallel `GetValue` calls on one key invoke the
  provider exactly once and do not corrupt the dictionary — **7.4** (S1)
- `Expired_entries_are_recomputed`; `InvalidateKey`/`InvalidateAll`
- `Retry_After_in_seconds_is_parsed_as_seconds` — **7.5** (`"120"` must be 2 minutes, not 120 days)
- `Retry_After_as_an_HTTP_date_is_parsed`
- `Paging_headers_populate_the_context`; `Missing_paging_headers_leave_nulls`
- `Anonymous_clients_target_the_configured_environment` — **7.2** (S1)
- `[Theory]` `ClientOptionsExtensions`: `AsDemo`/`ToDemo`/`AsLive`/`ToLive`/`IsDemo`/`IsLive`
- `ToDemo_does_not_mutate_the_original` (vs `AsDemo`, which does)
- `The_request_carries_an_Authorization_bearer_and_an_Agid_JWT_Signature`
- `The_Digest_header_matches_the_SHA_256_of_the_body`
- `The_JWT_has_explicit_iat_and_exp` — **7.7**
- `A_401_does_not_stop_the_host` — **7.3** (S1); host-level test with `TestServer`

### `GKit.Application` (tier B, after `WrapAsync`)

- `A_failing_host_exits_non_zero` — **6.1** (S1)
- `A_clean_run_exits_zero`
- `--help lists every registered runner and does not start the host`
- `--help works when combined with other arguments` — **6.5**
- `A_runner_that_disables_host_run_prevents_Run`
- `Non_matching_runners_are_not_executed`
- `The_host_is_disposed_on_every_path` — **6.3**
- `ApplyPendingMigrations_logs_when_it_skips_in_Development` — **6.2**
- `MapHealthChecksJson_emits_the_documented_shape` (`WebApplicationFactory`)

### `GKit.SmtpHost` (tier A/B)

- `The_default_identity_store_rejects_unknown_credentials` — **10.1** (S1); currently the default
  store returns `true` for everything, so this is the single most important test in the package
- `AddSmtpHost_without_WithIdentityStore_fails_fast`
- `[Theory]` `SmtpRouteAttribute`: matches on the *address*, not the display name — **10.2**
- `A_null_subject_does_not_throw` — **10.2**
- `Identity_scoped_routes_only_match_the_named_user`
- `Controller_discovery_is_performed_once_not_per_message` — **10.3** (count reflection calls)
- `Dead_letters_preserve_attachments_and_html` — **10.4**
- `Dead_letter_filenames_do_not_collide_under_load` — **10.4**
- `The_broker_round_trips_a_message` and honours cancellation — **10.6**

### `GKit.Mqtt` (tier A/B)

- `Wildcard_topic_filters_dispatch_to_their_handler` — **16.2**
- `MqttLogger_binds_source_and_message_to_the_template` — **16.1**
- `MqttLogger_respects_the_configured_log_level` — **16.1**
- `Handlers_with_a_wrong_signature_are_rejected_at_construction`
- `An_unreachable_broker_does_not_prevent_host_startup` — **16.3**

### `GKit.EntityFramework` — additional

- `Cascade_deleted_dependents_are_also_soft_deleted`
- `WithCurrentRevisionFilter_excludes_superseded_rows` (new API, 2.4)
- `OnlyCurrent_and_OnlyAlive_compose` (new API, 2.4)
- `Concurrent_saves_on_separate_contexts_do_not_interfere` — the threaded version of the S1
- `CopyToObject_skips_indexers` — **2.7**

### `GKit.Pdf` (tier A)

- `A_stamped_field_lands_on_the_configured_page` (read back with `ReadAllText`)
- `Integers_are_not_reformatted_with_two_decimals` — **14.1**
- `A_per_field_Format_is_honoured` (new API, 14.1)
- `An_out_of_range_PageNumber_throws_naming_the_field` — **14.2**
- `ReflectionPdfStamper_only_picks_up_attributed_properties`
- `ReadAllText_extracts_Tj_and_TJ_operands`

### Tier C — Blazor components (bUnit) — ✅ implemented in `GKit.Tests.Blazor`

Two lessons worth recording:

- `[SupplyParameterFromQuery]` parameters cannot be set with `ComponentParameterCollectionBuilder`;
  they must be driven through `NavigationManager.GetUriWithQueryParameter`. That is also how an
  attacker supplies them, so the open-redirect tests are more faithful for it.
- `MudDataGrid` needs a `MudPopoverProvider` in the render tree. Where a component's logic is
  reachable without a renderer — as `EntityGrid.BuildQuery` is — testing it directly is more
  robust than fighting the full rendering stack.

- `EntityGrid_does_not_show_soft_deleted_rows_by_default` — **4.1** (S1); the single most
  valuable component test, since 21 grids across BFer/StuffHR depend on it
- `EntityGrid_shows_them_when_IgnoreQueryFilters_is_set`
- `Export_and_grid_return_the_same_row_set` — **4.1**
- `Loaded_rows_are_not_tracked_when_no_shared_context_is_used` — **4.2**
- `OnLoadedServerData_fires_once_per_load` — **4.3**
- `EditEntityDialog_can_save_a_prefilled_model` — **4.5**
- `EditEntityDialog_sets_attributes_only_on_the_first_render` — **4.6**
- `ToXlsAsync_skips_columns_it_cannot_resolve` — **4.4**
- `DbContextProvider_cascades_itself_to_children` — **5.2**
- `DocumentEventSource_disconnect_passes_the_DotNetObjectReference` — **5.1** (fake `IJSRuntime`)
- `[Theory]` `RedirectToLogin` rejects absolute return URLs — **18.1** (S1)
- `TimerService_surfaces_callback_exceptions` — **5.3**

### Tier D — retired

**The integrated services (a PLC, an OPC UA server, a PCSC reader, a live RENTRI account, a
directory server) are not available to this project**, so a tier D suite would be permanently
skipped — which is worse than no suite, because a skipped test reads as covered.

Rather than leave the gap, most of what tier D was meant to catch has been pulled forward into
hermetic tests. What was actually hiding behind those boundaries was pure logic:

| Was "needs a real …" | Now covered by | Tests |
|---|---|---|
| MQTT broker | `MqttTopicRouter`, `MqttLogger.Format` | wildcard dispatch, handler validation, log templating, level mapping |
| Directory server | `LdapFilter.Escape`, `ADUser.GroupsNames` | RFC 4515 escaping incl. a filter-injection attempt, DN parsing |
| SMTP conversation | `SmtpRouteTable`, `SmtpRouteAttribute` | address-vs-display-name matching, absent Subject header, one-time discovery, dead-letter naming |
| PCSC reader | `ISCardReaderExtensions.StripStatusWord` | status-word validation, payload extraction |
| OPC UA server | `OpcUaConnectionPool` | session identity per options, concurrent-race single creation, removal |
| RENTRI endpoint | `BaseClient` header parsing, `ApiResultsCache` | `Retry-After` forms, paging headers, cache under concurrency |

Three of those conversions **required fixing the defect first** — the Mqtt log template and
wildcard routing, the LDAP filter injection, and the SMTP address matching were all still
broken. Writing the test is what forced the fix.

### What genuinely cannot be automated here

A short, honest list. Each needs the physical thing, and no test double substitutes:

1. **The PLC returns the expected bytes at the configured address.** The mapping table is
   tested; whether `DB200.DBW136` is really where `MinPacks` lives is a property of the machine.
   This matters most for the AllGlass address corrections below.
2. **An OPC UA session survives a network drop and reconnects.** The reconnect wiring is
   correct by inspection; only a real disconnect proves the handler fires.
3. **A card reader produces the UID the badge is printed with.**
4. **RENTRI accepts the signed JWT.** The token's shape is testable; its acceptance is the
   server's opinion.
5. **An LDAP bind succeeds against the real directory** with the configured base DN.

### Manual smoke checklist

Run once per release, by whoever has access to the hardware. Roughly 15 minutes.

**AllGlass — PLC (do this first; the wave 2 changes alter what is read)**

- [ ] Start `ErpPalletLink`. It must not throw at startup — `ModelBuilder.Validate()` now fails
      the build for any property whose S7 type could not be determined.
- [ ] `--help` lists the command line runners.
- [ ] Run `PalletMachieStatusCommandLineRunner` and compare every field against the HMI.
      `MinPacks` and `MachineState` previously read four bytes at two-byte addresses and are
      expected to **change value** — to the correct one.
- [ ] `CurrentFlasks` / `TotalFlasks` will read wrong until the addresses are corrected
      (`DBB224`→`DBD224`, `DBB228`→`DBD228`); confirm against the HMI before and after.
- [ ] Leave it running 5 minutes: `/api/health` stays healthy and the PLC connection count on
      the CPU does not climb (the pre-fix code opened one per job tick and per probe).

**AllGlass — OPC UA**

- [ ] With `PalletSynchronizeJob` running every 20 s, the server shows **one** session, not a
      new one per tick.
- [ ] Pull the network cable for 30 s and reconnect: the session recovers without a restart
      (direct connections never reconnected before).

**BFer — RENTRI**

- [ ] A lookup succeeds against the demo environment (signed JWT accepted).
- [ ] `/api/health` reports the **demo** endpoints, not production.
- [ ] Expire or remove the certificate: the app logs the 401 and **stays up**.
- [ ] Open socket count is stable across ~50 lookups.

**StuffHR — smart card**

- [ ] `/api/health` reports `SMART_CARD`.
- [ ] Badge a card: the UID matches the printed value with **no trailing characters**.

**Any app — SMTP / AD, if used**

- [ ] An unknown SMTP credential is now rejected (this changed).
- [ ] An LDAP login succeeds and group-based authorisation still works.

## 4. CI

```yaml
# .github/workflows/ci.yml
- run: dotnet test GKit.Tests/GKit.Tests.csproj
       --filter "Category!=Integration"
       --collect:"XPlat Code Coverage"
       --logger trx
```

Suggested gates, phased in as the failing tests go green:

1. **now** — the suite runs and the failure count is published; no gate
2. **after wave 1 of `GKIT-REVIEW.md`** — zero failures required
3. **then** — line coverage ≥ 60 % on tier A/B packages, and
   `PackageValidationBaselineVersion` set so an accidental API removal (as happened to
   `AddSmartCardHostCheck`, **0.5**) fails the build

Run the culture-sensitive tests under a non-invariant culture in CI —
`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` must stay `false`, otherwise
`CultureInfo.GetCultureInfo("it-IT")` silently returns the invariant culture and the culture tests
become vacuous.

Add a second job on `windows-latest`: the `DateTime.Now` and PEM-certificate defects behave
differently there, and `SkiaSharp` native assets resolve from a different package.
