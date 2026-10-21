# Fleet GPS Tracking Platform — Codex Implementation Specification
Version: 1.4 | Date: 2026-10-19 | Status: Version 1.3 preserved + confirmed ingestion, protocol extensibility, unknown-device, late-data and conditional cadence decisions (section 13)

## 1. Mission and constraints
Build an on-premises, web-based fleet GPS tracking platform for one distribution company operating ~300 trucks initially. Design and load-test for 1,000 simultaneously active devices; keep scaling to 5,000 as a future capacity scenario, not a day-one contractual SLA. Trucks may carry hazardous goods. This is a tracking platform, not a transportation management system (TMS). Existing dispatch/order system remains separate; integration is a later phase.

- Host all core services, application assets, map tiles, and operational data in the company's own datacenter/VMware environment.
- Initial operator access is internal corporate network only, but design for securely controlled internet exposure later (especially customer links).
- Develop custom software primarily in C#/.NET. Database: Microsoft SQL Server. No AD dependency; local application identities.
- Build a development/test environment first, then promote a validated deployment to production. Do not promote test data or secrets; use repeatable deployment and migration scripts.
- GPS hardware is not yet selected. Existing devices may be factory locked. Source samples from Iranian market and validate open documented protocols, server endpoint configuration, SMS commands and offline buffering before bulk procurement.

## 2. Scope: Phase 1
### 2.1 Fleet and live tracking
- Register trucks, GPS devices, SIM cards, drivers, regions, customers, and authorized users.
- Show dashboard of moving, stopped, offline and other statuses; fleet lists with search/filter and region-scoped visibility.
- Map: live location of one or multiple trucks; per-truck speed, heading if available, last GPS fix timestamp and last server receipt timestamp; clearly label stale positions.
- Truck details: 24h, 7d and arbitrary time-range history up to one year; track visualization and time-controlled playback.
- Reports: GPS-derived distance, moving time, stopped time/count, maximum speed, offline time, coverage/data completeness, highest/lowest/zero-mileage rankings, selectable columns and Excel export.
- Stop reports use operator-selectable minimum stop duration (e.g., 5 or 10 minutes); stop alerts have centrally configurable thresholds. Do not treat traffic lights as meaningful stops by default.
- GPS distance only, no odometer dependency. Filter implausible jumps, duplicates and invalid positions. Mark missing coverage; do not silently infer actual driven distance during gaps.
- Speed alerts: organization default, optional vehicle-group and vehicle overrides, and road speed limits only where source reliability is established. Label unknown road limits rather than claiming a legal violation. Log alert location, time, observed speed, applicable configured limit, status and operator acknowledgment.
- Alerts for extended stops, offline devices and speeding; acknowledgment/workflow and audit trail. Define cooldown/deduplication to prevent alert storms.

### 2.2 Assignments and access
- Driver-to-truck assignments are time bounded and may change several times per week. Fleet managers/operators create them; drivers do not log in in phase 1. Do not attribute historical violations to a driver when assignment is missing or ambiguous.
- Truck-to-region assignments are time bounded and transferable. Regional operators see current assigned trucks; after transfer, previous region retains read-only access to history generated during its assignment, but not new live or historical data. Senior administrators may grant explicitly logged, temporary exceptions.
- Independent application accounts with RBAC and resource-level authorization (not only menu hiding). Suggested roles: super admin, fleet manager, regional operator, customer-viewer where applicable, technical admin. Define exact permission matrix before coding.
- For customer tracking, support a manually registered temporary tracking session linking a truck, customer, start/end time, and optionally a shipment reference. Provide an expiring revocable unguessable link to view only that truck during the allowed period, without exposing other fleet data. Customer sees live data with no intentional delay; show last-fix freshness. Future customer portal and dispatch-system integration are out of scope.
- No mandatory origin/destination, route, or trip definition in phase 1. A pause does not automatically end an operational journey. Analyze movement and stops from GPS history instead.

### 2.2.1 Access decisions confirmed in the project conversation
- Fleet managers have company-wide fleet visibility and may transfer trucks between regions. This does not automatically grant unrelated user-administration or device-command permissions.
- Regional operators may change driver-to-truck assignments within their authorized region. This does not grant authority to transfer trucks between regions or correct historical assignments.
- A receiving region must not see history from before its assignment. A previous region retains read-only access to history from its own assignment periods. Explicitly logged, temporary senior-administrator exceptions remain supported.
- Bulk device-command execution is restricted to technical administrators. No other role receives this capability automatically, including fleet managers or super administrators.
- The user's affirmative answer about technical-administrator location access is interpreted as permission to view live vehicle positions. The authorized vehicle/region scope still needs clarification; historical tracks, reports, exports and customer information are not granted by this answer.
- These decisions approve the listed capabilities only. Remaining permission-matrix entries, single-device command permissions, sensitive-command workflows and historical-correction permissions still require explicit definition. No production implementation is authorized by these confirmations.

### 2.3 Device and SIM management
- Device catalog with brand/model/protocol family, IMEI/serial, SIM number/ICCID where available, assignment history, provisioning status, supported features, and technical notes.
- Support multiple brands/protocols via pluggable adapters. Same public IP with separate TCP/UDP listener ports per protocol family is the default. Port allocation, network bindings and new driver deployment are technical-team configuration, not regular operator UI.
- Configurable device commands and model templates through technical-admin UI when existing protocol adapter supports them; new binary/proprietary protocol requires code, test and controlled deployment.
- Device configuration must be initiated and tracked within the application, including single-device and bulk operations. Integrate a third-party bidirectional SMS provider through its documented API; support command formatting per device family, asynchronous replies, correlation, retries, timeouts, audit trail and explicit distinction between SMS submitted, delivered, device replied and setting verified. Some devices may not support verification or reply correlation.
- Bulk command execution is restricted to technical administrators, as confirmed in section 2.2.1; this does not settle single-device permissions or command-specific safety controls.
- Set one centrally configurable default offline threshold for phase 1; per-device override deferred.
- Hardware procurement acceptance criteria: user-controlled endpoint/port, documented protocol, configurable update intervals, available SMS/remote provisioning, recovery after network outages, appropriate power/physical reliability and testable security. Do not assume all devices implement every feature.

### 2.4 Offline maps
- Host Iran map tiles/styles/fonts/assets locally, using licensed OpenStreetMap-derived data and MapLibre GL JS or equivalent self-hosted renderer. Do not download/rehost Google Maps tiles without a license allowing it.
- Provide documented map data refresh process and attribution/OSM licensing compliance. Internet independence applies to map display, not automatically to cellular data delivery.

## 2.5 Confirmed user-facing calendar requirement
- All dates presented to users must use the Jalali (Persian/Solar Hijri) calendar. This is a confirmed, locked requirement and must not be changed without explicit user approval.
- Apply this consistently to all user-facing surfaces that display dates, including dashboards, live tracking, history/playback, alerts, assignment and command screens, customer tracking, report filters/calendar pickers, reports, and Excel/print exports. This does not introduce new delivery channels or features.
- Preserve UTC timestamps for database storage, internal processing, device contracts and machine-to-machine APIs. Convert at the presentation boundary; do not replace timestamps with Jalali strings in persistent or transport data.
- Calendar selection is no longer an open decision. Display timezone, time-of-day formatting and numeral style remain separate decisions; Jalali display does not silently select them.
- Acceptance must cover every date-bearing user-facing surface and exports, including Jalali year/month transitions, leap-year boundaries and date-range conversion to the correct UTC query interval after the display timezone is approved.

## 2.6 Confirmed phase-one data-source constraint
- The user confirmed that phase one has GPS location data only and no additional vehicle sensors will be installed. This constrains the earlier conditional telemetry proposals; it does not authorize application implementation.
- Phase-one calculations may use valid location samples, their timestamps, server receipt metadata and manually maintained authorized master data/assignments. A reported GPS speed, if actually supplied by the selected protocol, may be used with its provenance recorded; do not assume that it exists.
- Vehicle movement/stationary state is distinct from engine running/off and ignition state. Never infer engine/ignition state, fuel consumption, external-power loss or engine-idling duration from location alone.
- Where speed is derived from consecutive GPS fixes, label it as estimated segment speed, not an instantaneous sensor measurement. Apply quality checks and do not derive it across unacceptable time gaps or invalid samples. Average-speed definitions remain subject to approval in section 12.
- Lack of incoming data establishes last-contact age and a no-data/offline condition only. It does not identify a power cut, cellular outage, device failure, jamming or any other specific cause. Buffered fixes received later do not prove that a live communication link existed during the gap.
- Distinguish stale location, insufficient samples and unknown movement from a positively observed stationary state. A missing or unreliable signal must not be displayed as zero speed, parked or engine off.
- Optional `ignition`, `externalPower`, `batteryLevel` and similar contract fields remain future-compatible and nullable; they are not phase-one feature promises and must not be synthesized or exposed as measured values.
- Section 12 records the review of the team's proposed features under this constraint. Existing approved scope remains authoritative; compatible additions are recommendations until explicitly approved, and previously excluded capabilities remain deferred.

## 3. Proposed technical architecture (subject to proof of concept)
1. Independent .NET GPS Receiver Worker running on Linux VM, with TCP/UDP listeners and per-protocol framing/decoders; implement ACK/heartbeat according to each protocol.
2. Normalize incoming device packets into a versioned internal message schema. Persist raw packets separately with 30-day retention, access control and privacy safeguards. Design ACK semantics around actual protocol requirements; never acknowledge durable acceptance unless guarantees are met.
3. RabbitMQ durable queues for decoupling ingestion from persistence, with publisher confirms, bounded retry/dead-letter queues, idempotent consumers and backpressure. The phase-one decision is to use RabbitMQ as the sole pre-SQL durable buffering layer, without a separate receiver-side persistent spool. See section 13 for acknowledged failure limits and the distinction from the separately specified 30-day raw-packet archive.
4. .NET processing workers validate, deduplicate, handle out-of-order packets, update current position and write location history to SQL Server. Use bulk/batched writes where measured beneficial.
5. ASP.NET Core API and Blazor web UI, SignalR for live updates. Authorize every API/SignalR subscription and enforce region/customer scope server-side.
6. Independent Linux-hosted map tile service. SMS integration worker/service separated from GPS receiver.
7. SQL Server on a managed Windows Server VM; separate logical schemas/tables for operational data, current location, time-series positions, alerts, commands and audit events.
8. On-prem monitoring, centralized structured logs, metrics, tracing, service health, queue depth and device ingestion lag.

Start with a modular administrative application plus separately deployable receiver/processor/simulator workers; avoid premature microservice proliferation. Support Linux/Windows portability for custom .NET services. Avoid committing to exact .NET LTS release until kickoff; choose currently supported release.

## 4. Normalized GPS event — proposed v1 contract
Required when valid: `schemaVersion`, `deviceId`, `protocolId`, `eventTimeUtc`, `receivedAtUtc`, `latitude`, `longitude`, `positionValid`, `messageId`/idempotency key.
Optional: `speedKph`, `headingDegrees`, `altitudeMeters`, `accuracyMeters`, `satellites`, `ignition`, `externalPower`, `batteryLevel`, `deviceSequence`, `rawPacketReference`, `vendorAttributes`.
Rules: retain event time separately from receive time; UTC storage; enforce geographic ranges and reasonable time skew; nullable unknowns (do not invent zero speed or ignition state); derive motion from multiple samples; deduplicate with protocol-specific identifiers/fingerprints; preserve late-arriving fixes without corrupting current-position state; support versioned schema evolution.

## 5. Suggested SQL data model (design, not final DDL)
`Users`, `Roles`, `UserRoleAssignments`, `Regions`, `UserRegionGrants`, `Trucks`, `TruckTypes`, `TruckRegionAssignments`, `Drivers`, `TruckDriverAssignments`, `GpsDeviceModels`, `GpsDevices`, `SimCards`, `DeviceTruckAssignments`, `DeviceSimAssignments`, `CurrentPositions`, `PositionHistory`, `DeviceHeartbeats`, `DeviceRawPacketIndex`, `DeviceCommands`, `CommandAttempts`, `SmsProviderMessages`, `TrackingSessions`, `TrackingTokens`, `Alerts`, `AlertAcknowledgments`, `AuditEvents`, `ConfigurationVersions`.

Use temporal/history-aware assignments and effective-date checks; prevent unintended overlapping active assignments where business rules disallow them. Partition/index high-volume position history by time and truck/device after benchmarking; use SQL Server spatial types/indexes only for measured spatial query requirements. Retention: normalized position history 365 days; raw packets 30 days, both automated and auditable. Backup/DR schedules are managed by infrastructure and are out of application UI scope; application design must support consistent backup/restore and migration.

## 6. Performance and capacity acceptance plan
Initial production: 300 trucks. Design target: 1,000 devices. Stretch test: 5,000 devices. Provisional worst-case load at one fix every 5 seconds while moving: 200 events/sec at 1,000; 1,000 events/sec at 5,000; 17.28 million events/day at 1,000 if continuous 24h. Real traffic will vary with stopped intervals and device capabilities. GPS fix cadence and transmission cadence may differ; these figures are assumptions to validate against purchased hardware.

- Build separate .NET GPS simulator service (development/test only) running initially on same server as receiver, but isolate CPU/network resources when benchmarking so co-location does not hide receiver capacity. No simulator controls in production UI.
- Simulate distinct device IDs and protocol-accurate TCP/UDP packet framing, moving/stopped vehicles, corners, offline buffering, reconnection storms, delayed/out-of-order and duplicate messages, bad coordinates, database outage and RabbitMQ outage.
- Measure end-to-end device event to UI update latency, ingestion success/loss, duplicate rate, SQL write latency, CPU/RAM/network/disk, RabbitMQ backlog/drain time, query response time and map rendering performance.
- Provisional target: live map freshness <=5 seconds under normal connected conditions, subject to device transmission cadence, network conditions and agreed percentile (e.g. p95); never claim hard 5-second guarantees without validation.
- Require functional, endurance, stress, soak and recovery tests before launch. Define numeric thresholds, realistic payload mix, data retention sizing and acceptable recovery point/time during technical design.

## 7. Security requirements
- Default deny between DMZ/ingestion, application, map and database network zones; restrict ports to required flows; never expose SQL Server or RabbitMQ management publicly.
- TLS for all browser/API and supported device links; where GPS only supports plaintext, isolate endpoints, rate-limit, authenticate device identity when possible and treat data as untrusted.
- Local account system with strong password hashing, MFA for privileged users, account lockout/rate limits, secure sessions, CSRF/XSS/SQL-injection defenses, secret vault/environment injection and key rotation.
- Region-scoped authorization in every query, export, report and real-time channel; audit sensitive access and administrative actions.
- Customer links: high-entropy tokens stored hashed, short lifetime, revocation, scope to one tracking session, avoid leakage via referrers/logs; optionally additional verification for sensitive cargo.
- Validate/limit packet lengths, message framing, parser timeouts, connection counts and malformed-packet handling; fuzz protocol adapters and test denial-of-service conditions.
- Signed releases, least-privilege service identities, dependency/SBOM scanning, security testing before any internet publication.

## 8. Later phases — explicitly out of scope now
Geofencing and route-deviation alerts; mandated origin/destination and dispatch/TMS integration; refrigerated truck temperature and other telematics sensors; driver mobile access; comprehensive customer portal; richer driver-behavior analytics; possible multi-company tenancy. Keep extensibility points but do not build these features now.

Under the confirmed GPS-position-only constraint, measured fuel consumption (including moving/idling breakdown), ignition/engine status, external-power-disconnection detection and engine-idling reports are unavailable in phase one. Reconsider them only after explicit approval of an additional data source and scope change. Connectivity to an external transport-monitoring platform is a recorded future integration request pending identification, documentation and approval; it is not an approved phase-one integration.

## 9. Delivery plan for Codex
**Stage A — Foundation:** repository/solution structure, architecture decision records, SQL schema/migrations, local identity/authorization baseline, CI checks, container/VM deployment manifests and observability.
**Stage B — Data path:** one protocol adapter + receiver + durable message flow + processing + current/history SQL writes + simulator; verify end-to-end correctness and failures.
**Stage C — Fleet UX:** admin master data, assignment histories, regional permissions, dashboard, map, history and playback.
**Stage D — Operations:** alerts, reports, ranking, SMS configuration workflows, device management and customer temporary links.
**Stage E — Readiness:** offline map data and update procedure, security review, 1,000-device load test, restore/recovery drills, pilot with a few real devices, rollout to 300 trucks.

For each stage: supply migrations, tests, configuration samples without secrets, API contracts, operational runbook and measurable acceptance criteria. Codex must not fabricate real GPS packet formats, SMS provider APIs or road speed-limit data. Use simulator fixtures until vendor documentation is obtained. Ask for approval before irreversible infrastructure, security or scope changes.

## 10. Decisions still open (do not silently assume)
- Exact GPS vendors/models and their TCP/UDP framing, device identifiers, remote command formats and offline store-and-forward behavior. The modular adapter principle is approved in section 13, but no vendor implementation has been approved.
- SMS provider, inbound reply capabilities, webhook security, rate limits and delivery guarantees.
- Final map tile pipeline, storage size, map update cadence, road speed limit data coverage and attribution details.
- Remaining permission-matrix details after section 2.2.1, including technical-administrator live-position scope, single-device commands and historical corrections; customer tracking session activation/deactivation workflow, token lifetime and whether customer links are internet-accessible in phase 1.
- Stop/motion classification thresholds, speed alert tolerance and configurable default offline threshold. Device-specific moving/stationary transmission cadences remain hardware-dependent; 5 seconds moving / 60 seconds stopped is a provisional test profile, not a universal requirement.
- Storage capacity, partitions, archiving and retention deletion approach based on measured event sizes and 1,000-device tests. A dedicated approximately 50 GB RabbitMQ volume is a provisional starting allocation, not a validated queue capacity or SLA.
- Actual SLOs for UI latency, recovery and data loss; high availability, DR ownership and deployment topology.
- Team proposal decisions in section 12: average-speed denominator, GPS moving-duration thresholds, maximum-stop filters, comparison/summary reports, additional registry fields and access, parking semantics and the exact external transport-monitoring platform. No engine/fuel/power measurements may be assumed under section 2.6.

## 11. Immediate instruction to Codex
Read this specification and **first** produce: (1) solution/module layout; (2) architecture diagram; (3) SQL Server conceptual ERD and migrations plan; (4) normalized GPS schema; (5) simulator + receiver proof-of-concept design; (6) threat model and permission matrix draft; (7) prioritized milestones and acceptance tests. Identify ambiguities and request approval before implementing broad functionality. Begin coding only the approved Stage A/B vertical slice, using mock protocol fixtures clearly labeled as synthetic.

## 12. Team feature proposal review — GPS-position-only phase one

### 12.1 Review status and interpretation
- Source: the team's Persian map/report feature list supplied by the user, followed by the user's confirmation that only GPS location data is available and no vehicle sensors will be installed in this phase.
- **Existing** means already covered by approved scope. **Proposed** means technically compatible with location data but requires approval of the addition or its calculation rules. **Deferred** means a scope change or additional workflow/integration is required. **Unavailable** means location alone cannot establish the requested measurement. **Clarify** means the supplied title is insufficient to define a feature.
- Record all proposals for traceability without treating the complete team list as approved. Numeric thresholds such as four hours, thirty minutes and two days are team suggestions, not adopted defaults or legal limits.
- Moving/stopped, communication freshness and location freshness are separate states. Use distinct icon/badge encodings with a legend; reserve engine/ignition labels for a future verified source. An animated marker must not imply fresh measured movement between missing samples.

### 12.2 Map, popup and search proposals
| Team proposal | Disposition | Interpretation / dependency |
|---|---|---|
| Live location of all fleet vehicles and movement on the map | Existing | Show only the authorized fleet; preserve last-fix/receipt times, stale labels and history/playback. Movement is GPS-derived, not engine state. |
| Separate icon colors for vehicle on/off | Unavailable as stated; proposed alternative | Show moving, stationary, stale/no-data and unknown states. A stationary vehicle may have its engine running; do not rename stationary to off. |
| Instantaneous speed | Existing with data qualification | Display protocol-supplied GPS speed only when available and valid; otherwise a quality-filtered estimated segment speed may be proposed. Unknown remains unknown. |
| Average speed | Proposed | Derivable after choosing moving-time versus observed-elapsed-time denominator; see section 12.4. |
| Vehicle popup: plate, smart number, body/trailer type | Existing master-data concept; proposed field refinement | Confirm the meaning/format of vehicle smart number and body/trailer type. These are manually maintained registry data, not obtained from GPS. |
| Driver popup: smart number, full name, mobile | Existing driver/assignment concept; proposed field/access refinement | Use the assignment applicable to the displayed event time. Missing/ambiguous assignments remain explicit; personal identifiers/mobile require field-level access decisions and are not automatically exposed to customers. |
| Popup: fuel consumption | Unavailable | No measured consumption from location alone; do not substitute an unlabeled theoretical estimate. |
| Popup: average speed and total distance | Distance existing; average speed proposed | Display an explicit selected period, data coverage and calculation basis. Total must not imply lifetime distance beyond retained data. |
| Variable-period mileage and traveled-route reports | Existing | Authorized GPS history, distance and playback with gaps visible; no inferred path or distance through missing coverage. |
| Vehicle lookup by plate / smart number | Plate search compatible with existing filtering; smart-number field proposed | Normalize input and apply the same resource authorization as live/history access. |
| Driver lookup by full name / smart number | Proposed search refinement | Resolve time-bounded vehicle assignments; a driver does not carry an independently tracked GPS identity. Do not infer an unrecorded assignment. |
| Connection to smart transport-monitoring platform | Deferred / clarify | Obtain exact platform identity, owner, API, authentication, data directions, legal/contractual obligations and connectivity requirements. An integration boundary is sufficient now; do not invent an API or add phase-one implementation. |

### 12.3 Numbered report/feature traceability
Original item numbers are preserved; omitted numbers were not supplied.

| Original ID | Team title | Disposition | GPS-only interpretation / unresolved detail |
|---|---|---|---|
| 1 | پیمایش بیش از ۴ ساعت در یک روز | Proposed / clarify | If this means cumulative GPS moving duration per local reporting day, it is feasible. Confirm cumulative versus continuous duration and report versus alert. It does not establish legal driving hours or driver attendance. Four hours is not an approved limit. |
| 2 | تخلف سرعت | Existing | Compare valid speed with the applicable configured threshold. A legal road-speed violation requires reliable approved limit data; do not claim one from an arbitrary fleet threshold. |
| 3 | تست ایرانسل | Clarify | Likely a SIM/operator connectivity acceptance scenario, not a GPS-derived report. Confirm intent; no carrier API or cellular signal-quality measurement is assumed. |
| 4 | توقف مبادی | Deferred | Requires approved origin definitions and association with an operation/trip; not derivable as an origin stop from coordinates alone. |
| 5 | توقف مقاصد | Deferred | Requires approved destination definitions and association with an operation/trip. Ordinary GPS stops remain in scope. |
| 6 | توقف های کمتر از 30 دقیقه | Proposed filter refinement | Add an upper stop-duration filter while retaining the approved minimum-duration/noise rule. Thirty minutes is a proposed filter, not the central stop-alert threshold. |
| 7 | حصار جغرافیایی | Deferred | Technically possible with GPS but explicitly excluded from phase one; requires scope approval, boundary data and event rules. |
| 10 | ساعت کارکرد خودروها | Clarify / proposed alternative | GPS moving duration is feasible; engine hours and working/attendance hours cannot be established from location alone. Use an accurate report label. |
| 11 | سرعت متوسط | Proposed | Define denominator, observation coverage and treatment of stops/gaps before adoption. |
| 12 | سرعت | Existing | Valid protocol GPS speed when available; otherwise distinguish estimated segment speed from measured instantaneous speed. |
| 15 | قطع ارتباط بیش از 2 روز | Existing offline report; proposed duration filter | Report no-data intervals exceeding a selected duration. Do not replace the approved centrally configurable offline threshold with two days without approval. |
| 16 | قطع برق دستگاه | Unavailable | Requires verified power telemetry; no-data condition alone cannot establish a power cut. |
| 17 | لاگ | Existing / clarify | Audit trail and technical logs already exist in scope. Confirm which log/report is intended; do not expose raw packets, secrets or unrestricted diagnostics to ordinary users. |
| 19 | مسافت | Existing | Quality-filtered GPS distance for a selected interval with missing coverage shown. |
| 20 | مصرف سوخت | Unavailable | Requires an approved additional source; GPS distance is not measured fuel consumption. |
| 21 | مصرف سوخت (در حرکت و درجا) | Unavailable | Requires fuel and engine-state data; GPS stationary intervals do not establish engine idling. |
| 23 | میزان رانندگی هر متحرک | Existing moving-time report / clarify | Can report GPS-derived movement duration per vehicle. Driver attribution requires an unambiguous effective assignment and does not prove who physically drove. |
| 24 | وضعیت سوئیچ | Unavailable | Requires ignition input/verified telemetry; do not infer from movement, stopping or network silence. |
| 25 | وضعیت قطع ارتباط | Existing | Show last contact, no-data duration and stale-fix status separately; cause remains unknown. |
| 26 | پارکینگ ها | Clarify | GPS stationary intervals are feasible. Named parking areas, entry/exit and dwell reports require defined places/geofencing and a separately approved scope change. |
| 27 | پیمایش کشنده ها در 2 بازه زمانی | Proposed comparison report | Compare GPS distance and coverage for two selected periods; requires tractor classification and explicit treatment of unequal periods/coverage. |
| 28 | کارکرد درجای موتور | Unavailable | Requires engine-running state together with stationary state; stationary duration is not engine-idling duration. |
| 30 | گارانتی | Clarify / deferred | Identify vehicle versus device warranty. Manual warranty metadata/reminders would be a separate administrative proposal, not a GPS calculation. |
| 31 | گزارش خلاصه عملکرد | Proposed presentation | Compose approved distance, movement, stops, speed and completeness metrics; do not introduce inferred fuel, engine hours or a new performance score. |
| 32 | گزارش سفرهای روزانه | Proposed GPS-activity alternative; formal trips deferred | A daily movement/stop summary is feasible. Business trips require approved start/end rules; a pause must not automatically create/end a journey. |
| 33 | گزارش فعالیت متحرک ها | Existing metrics; proposed consolidated report | Summarize authorized movement, stops, distance and data freshness for an explicit interval; definition/columns need approval. |

### 12.4 Calculation and acceptance rules for proposed GPS-derived additions
- **Distance:** existing quality-filtered GPS calculation; no odometer, inferred route or gap-filling promise. State interval, units and coverage.
- **Moving duration:** derive from multiple valid samples using approved speed/displacement, minimum-duration and gap thresholds. Do not count unobserved intervals as confirmed movement or stopping. Record algorithm/configuration version for reproducibility.
- **Average speed:** proposed options are accepted distance divided by accepted moving duration, or accepted distance divided by sufficiently observed elapsed duration including observed stops. Label the chosen metric and coverage. Unknown or zero denominators yield unavailable, not a fabricated value. Do not silently average irregularly spaced speed samples or include unobserved gaps as confirmed elapsed driving/stopping.
- **Daily aggregation:** use the confirmed Jalali calendar for user-facing dates and convert reporting-day boundaries using the separately approved display/reporting timezone. Split intervals crossing a day boundary; do not silently choose the execution server's timezone.
- **Driver and region attribution:** use effective assignments for each event/interval; split at assignment changes and enforce section 2.2.1. Summaries, rankings, comparisons, exports and popups must not leak data outside the permitted historical intervals.
- **Two-period comparisons:** show absolute metrics and completeness for each interval. Any normalized daily rate or percentage change needs an explicit denominator; a zero baseline must not cause division errors or a misleading percentage.
- **Additional tests:** GPS jitter while stationary, invalid fixes, missing speed, unequal sample spacing, long gaps, buffered late fixes, duplicates, assignment changes and Jalali/reporting-day boundaries. Assert that none of these conditions produces invented ignition, fuel, external-power or engine-idling measurements.

### 12.5 Approval queue and roadmap impact
- Keep existing stages and scope. Candidate refinements for Stage C are GPS-state icon semantics, authorized popup fields and smart-number search. Candidate Stage D additions are explicitly defined average speed, upper-bound stop filtering, moving-duration filters and comparison/summary reports. These candidates require approval before implementation.
- Confirm smart-number meanings, vehicle/driver registry fields and their access permissions; average-speed definition; the meaning of four-hour movement and work hours; whether thirty-minute/two-day values are report filters; tractor classification; and the intended content of activity/daily reports.
- Clarify “تست ایرانسل”, “پارکینگ ها”, “گارانتی” and the exact external platform. Do not convert ambiguous titles into implementation commitments.
- Geofencing, origin/destination operations and formal trip workflows remain deferred. Fuel, ignition, power-cut and engine-idling features remain unavailable under the confirmed input constraint. No production application code is authorized by this documentation update.


## 13. Confirmed design decisions from the 2026-10-19 architecture workshop (v1.4)

This section records decisions expressly confirmed by the user after v1.3. It supplements earlier sections without changing the approved feature scope. Where a numerical value or device capability remains provisional, it is expressly labeled as such. No application implementation is authorized merely by updating this document.

### 13.1 One durable pre-SQL buffer: RabbitMQ
- **Approved:** Use durable RabbitMQ queues as the **only additional persistent buffering tier between GPS receiver and SQL Server** for phase one. Do **not** add a second receiver-side disk spool or an additional backup queue merely as a precaution. The 30-day raw-packet archive from sections 3 and 5 is an independent retention/audit requirement; it is **not** being approved as a failover buffer and should not silently introduce another synchronous write requirement. Reconcile raw archival implementation separately without changing this decision.
- Primary goal: bridge **short SQL Server maintenance/restart interruptions of minutes**, not guarantee arbitrary multi-hour/day outages. Upon SQL recovery, drain the backlog without duplicate business records.
- Implement durable queues/messages and appropriate persistence settings; publisher confirms on successful RabbitMQ acceptance; manual consumer acknowledgments **only after the corresponding database transaction has committed**. Consumers must be idempotent because re-delivery is possible. Preserve retry, dead-letter, backlog metrics and backpressure.
- **Provisional infrastructure allocation:** start planning for approximately **50 GB dedicated RabbitMQ disk space**, with sufficient headroom for the OS, RabbitMQ internals and operational alarms. The usable capacity and actual outage coverage **must** be proven by measuring serialized message sizes, broker disk amplification, delivery rate, queue growth and drain performance at the target device count. This is not a validated SLA or a guarantee of 50 GB usable payload storage.
- **Explicit accepted limitation:** If RabbitMQ is unavailable, or the receiver loses contact with RabbitMQ before a publish confirmation, this architecture does **not** guarantee that incoming GPS packets will be durably retained. Implement clear error handling and monitoring rather than falsely treating unconfirmed publishes as committed. Device-side store-and-forward may help **only when supported and tested**; it is not assumed. GPS-protocol responses/ACKs must follow the actual device protocol and the chosen loss policy: do not assert durable acceptance before it occurred, and do not invent a universal ACK behavior.
- Operational checks: RabbitMQ availability, durable queue depth/age, publish-confirm failures, disk free space/watermarks, DB outage duration, consumer lag, backlog drain time, duplicates and dead-letter count. Do not silently discard messages on queue overflow.

### 13.2 Modular, multi-vendor GPS protocol receiver
- **Approved:** Keep the GPS receiver core protocol-agnostic. Add support for a genuinely new device protocol by implementing and deploying a **separate protocol adapter/decoder**, without rewriting the receiver's core orchestration or SQL-processing pipeline.
- Retain one public IP with separately configured TCP/UDP listener ports per protocol family as the default deployment arrangement; multiple device models may share an adapter when their wire protocol is compatible.
- Keep framing, device identification, authentication/checks (when supported), packet decoding, and device-specific ACK/heartbeat behavior within the appropriate adapter or explicit interfaces. Feed the common versioned normalized GPS contract from section 4 into RabbitMQ.
- The adapter extensibility principle does **not** mean an unknown binary protocol can be integrated by merely selecting a brand in the admin UI. New protocols require implementation, protocol fixtures, security review, tests and controlled deployment. Port bindings remain technical-team configuration.

### 13.3 Handling known versus unknown devices
- **Approved:** Identify each connecting device and check it against the registered/provisioned device inventory before admitting its positions to the operational pipeline. Device identifiers such as IMEI are protocol-dependent and **not proof of cryptographic identity by themselves**; apply additional validation where available.
- **Known and authorized** devices: process valid packets through the normal, authorized tracking pipeline (including RabbitMQ and SQL Server).
- **Unknown/unregistered** devices: exclude them from live maps, current positions, operational position history, driver/region metrics and customer-facing views. Retain only **strictly bounded diagnostic connection metadata and minimal sample payloads** in a separately access-controlled technical quarantine, with explicit size, rate, retention and redaction limits to be defined at technical design. This quarantine is for diagnostics/provisioning, not a second durable GPS ingestion queue or indefinite tracking store.
- Apply rate limits, input validation, auditing and controlled technical-admin review of unknown devices. Device registration/provisioning requires an authorized action; do not automatically trust an IMEI merely because it has appeared on a network socket.

### 13.4 Late and offline-buffered GPS fixes
- **Approved:** Persist valid delayed/buffered fixes to the correct historical timeline using the original GPS event timestamp (`eventTimeUtc`) while separately preserving the server receipt time (`receivedAtUtc`).
- Update `CurrentPositions` and live map state **only when an incoming valid fix is newer than the currently accepted fix according to a deterministic timestamp/ordering rule**. Do not let delayed old fixes cause the vehicle's live marker to jump backward.
- Deduplicate repeated retransmissions. Missing recent observations remain marked **stale/unknown** until fresh valid fixes arrive; an old buffered fix received now does not imply that the truck was online or at that location now.
- Reject/quarantine invalid coordinates, unreasonable timestamps and other impossible points according to the validation policy. History repair and recalculation of affected aggregates/alerts from late fixes require explicit tested rules; do not silently corrupt earlier reports or audit history.

### 13.5 Moving versus stopped transmission frequency
- **Approved in principle:** Permit *different configurable data acquisition/transmission intervals for moving and stationary devices* **where the actual GPS hardware and firmware support them**. This is a device capability, not something the central receiver can force by itself.
- **Provisional test profile only:** approximately **5 seconds while moving** and **60 seconds while stopped**, subject to device-specific documentation and field tests. These numbers are **not final company-wide defaults**; a previously discussed 2-minute stopped interval remains an alternative, not a conflicting mandatory requirement.
- Verify for each sample model: whether moving/stationary modes exist, whether acquisition and transmission cadences differ, whether a minimum speed/motion threshold is needed, whether settings can be changed via SMS or another authorized supported management protocol, and how unsent fixes are buffered/retransmitted.
- If a model does not support distinct rates, document its supported fixed or event-driven behavior and accommodate that model without inventing functionality or claiming uniform 5-second freshness.

### 13.6 Items explicitly **not** decided in this workshop
- The organization-wide offline/no-data timeout was **asked but not answered**. Keep a centrally configurable phase-one threshold; **do not** choose a default value without a new decision.
- The precise RabbitMQ usable capacity, message disk footprint, retention during SQL outages and numeric recovery SLOs remain subject to load testing; no additional receiver-side disk buffer is authorized.
- GPS brand/model selection, the SMS provider and any security-keyword recovery outcome remain open. Do not assume that the existing locked Teltonika FMB920 devices are configured or usable with the new server.
- Exact quarantine limits/retention, protocol-specific ACK rules and device-side replay guarantees remain technical design decisions that require measured/documented behavior and approval when they change scope or reliability guarantees.

### 13.7 Codex implementation instruction for these decisions
- Treat the confirmed principles in sections 13.1–13.5 as approved architectural constraints for planning, diagrams, proof-of-concept design and targeted acceptance tests.
- **Do not** implement an extra persistent pre-SQL buffering system, pretend RabbitMQ is lossless during its own outage, silently promote unknown devices to active fleet tracking, overwrite fresh locations with late GPS fixes, or hardcode unverified GPS data cadences.
- In the Stage A/B plan, explicitly test a short SQL outage with a durable RabbitMQ backlog and replay, consumer crash before/after DB commit, producer publish-confirm failure, unknown-device packet arrival, older buffered positions arriving after newer positions, and at least two device configurations for cadence support or non-support.
- Ask for approval before expanding implementation beyond the previously authorized Stage A/B vertical slice; keep the complete v1.3 feature inventory and unresolved items intact.
