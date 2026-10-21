# Teltonika FMB920 — SMS Commands & Device Configuration Runbook

**Version:** 1.0  
**Date:** 2026-10-20  
**Purpose:** Reference/runbook for Codex to implement a safe, auditable SMS device-management module inside an on-prem GPS Tracking platform.  
**Scope:** FMB920, subject to firmware-specific validation. This document records actual user-tested commands separately from documented-but-not-yet-tested commands.

> **SECURITY:** Never commit actual SMS Login/Password, SIM phone numbers, IMEIs, or device authentication material to source control. This file intentionally uses placeholders. The existing SMS credentials were shared in a conversation and should be rotated to a new private credential before production use. Logs and outbound SMS audit events must redact credentials. Do not expose secret parameter values (e.g. 3004) in UI or telemetry.

## 1. SMS Command Syntax

With configured SMS credentials:

```text
<SMS_LOGIN> <SMS_PASSWORD> <command> [arguments]
```

Examples using placeholders:

```text
<SMS_LOGIN> <SMS_PASSWORD> getinfo
<SMS_LOGIN> <SMS_PASSWORD> getparam 2004;2005;2006
<SMS_LOGIN> <SMS_PASSWORD> setparam 13003:0
```

When no SMS login/password are configured, standard Teltonika syntax begins with two space characters before the command. **Do not assume this for this device**: it has configured credentials. SMS commands have a documented 160-character length limit; account for the login/password prefix and provider encoding.

`getparam ID;ID` reads multiple parameters; `setparam ID:value;ID:value` sets them (if permitted). A successful `setparam` reply alone should be followed by a fresh `getparam` readback.

## 2. Actual Device Observations (verified by SMS replies)

These are **observed snapshots**, not defaults, not secrets, and not guarantees of the current settings after future edits.

| ID | Meaning | Observed response | Verification |
| --- | --- | --- | --- |
| `2001` | Cellular APN | `mtnirancell` | Read via SMS |
| `2004` | GPS server IP/domain | `138.201.197.17` | Read via SMS |
| `2005` | GPS server port | `21328` | Read via SMS |
| `2006` | GPS protocol | `0` = TCP | Read via SMS |
| `10050` | Home / Moving / Min Period (seconds) | `30` | Read via SMS |
| `10051` | Home / Moving / Min Distance (meters) | `500` | Read via SMS |
| `10052` | Home / Moving / Min Angle (degrees) | `10` | Read via SMS |
| `10053` | Home / Moving / Min Speed Delta (km/h) | `10` | Read via SMS |
| `10054` | Home / Moving / Min Saved Records | `1` | Read via SMS |
| `10055` | Home / Moving / Send Period (seconds) | `30` | Read via SMS |
| `13000` | FOTA WEB domain | `fm.teltonika.lt` | Read via SMS |
| `13001` | FOTA WEB port | `5000` | Read via SMS |
| `13002` | FOTA WEB period (minutes) | `30` | Read via SMS |
| `13003` | FOTA WEB status | Initially `1` (enabled); switched to `0`, then restored to `1` successfully | Read and modified via SMS; **do not assume current state without rereading** |
| `3004` | SMS Password | Value read successfully; **intentionally redacted** | Read via SMS; new credential subsequently worked with `getinfo` |
| `4000`–`4009` | First ten authorized numbers | All reported empty | Read via SMS; **does not prove later slots are empty** |
| `10` | Attempted Security Keyword parameter read | `Param ID:10 doesn't exist` | Device rejected this `getparam` ID; **does not prove no Security Keyword exists** |

Other observations: `getinfo` replied successfully; SMS password change by `setparam 3004:<NEW_PASSWORD>` was followed by an apparently successful `getinfo` with the new password. USB Configurator still asked for **Security Keyword** with 4 attempts remaining. SMS credentials and Configurator Security Keyword are independent controls.

**Important operational nuance:** `Min Period` is a *recording* criterion and `Send Period` controls *upload timing*; do not infer one coordinate per exact interval. GPS movement may generate additional records due to distance, angle, or speed delta. Similarly, a GPS update received late must be kept in history without overwriting a newer current position.

## 3. Read-only SMS Commands — ready-to-fill templates

### Device status

```text
<SMS_LOGIN> <SMS_PASSWORD> getinfo
```

### GPS destination, protocol, backup server

```text
<SMS_LOGIN> <SMS_PASSWORD> getparam 2004;2005;2006
<SMS_LOGIN> <SMS_PASSWORD> getparam 2007;2008;2009;2010
```

`2004` main server host, `2005` port, `2006` protocol (0 TCP / 1 UDP). `2007`–`2010` cover backup server settings; confirm firmware mapping when building adapters.

### Cellular APN

```text
<SMS_LOGIN> <SMS_PASSWORD> getparam 2001;2002;2003
```

APN name, APN username, APN password. **Redact any secret values from logs.**

### Data acquisition — home network / moving

```text
<SMS_LOGIN> <SMS_PASSWORD> getparam 10050;10051;10052;10053;10054;10055
```

### Data acquisition — home network / stopped

```text
<SMS_LOGIN> <SMS_PASSWORD> getparam 10000;10004;10005
```

**Status: the command was proposed but no actual values were supplied yet.**

### Data acquisition — roaming and unknown network

```text
<SMS_LOGIN> <SMS_PASSWORD> getparam 10100;10104;10105
<SMS_LOGIN> <SMS_PASSWORD> getparam 10150;10151;10152;10153;10154;10155
<SMS_LOGIN> <SMS_PASSWORD> getparam 10200;10204;10205
<SMS_LOGIN> <SMS_PASSWORD> getparam 10250;10251;10252;10253;10254;10255
```

These IDs are documented in the Teltonika FM/FMB/OBD family parameter reference; **not yet tested on this unit**.

### FOTA WEB connectivity

```text
<SMS_LOGIN> <SMS_PASSWORD> getparam 13000;13001;13002;13003
```

FOTA Domain, Port, Period in minutes, Status (0 disabled / 1 enabled). The first three and `13003` were read successfully on this device.

### SMS account and authorized numbers

```text
<SMS_LOGIN> <SMS_PASSWORD> getparam 3003
<SMS_LOGIN> <SMS_PASSWORD> getparam 4000;4001;4002;4003;4004
<SMS_LOGIN> <SMS_PASSWORD> getparam 4005;4006;4007;4008;4009
```

`3003` SMS login; `3004` SMS password. **Do not poll/read/display `3004` in production UI.** The documented authorized-number parameter range for the FM/FMB/OBD family is `4000`–`4199`; only `4000`–`4009` were checked and were empty. Verify the range specifically for the connected firmware before claiming the whole list is clear.

## 4. Write Commands — require explicit operator approval

**Project permission decision (2026-10-21, specification v1.5 section 15):** one authorized technical administrator may approve and execute a write command, including sensitive changes; a second approver is not required. Single-device and bulk configuration execution remain technical-admin-only and company-scoped. All preview, audit, readback and recovery controls below still apply.

**Changes must not be sent automatically by Codex or software just because this runbook exists.** For any write: request authorized human approval; record device identity, old and proposed values, timestamp, operator, change ticket; verify device/SIM ownership; have a rollback plan; send only once; read back; check device health and live ingest.

### A. Disable / enable FOTA WEB

```text
# Disable (tested successfully previously):
<SMS_LOGIN> <SMS_PASSWORD> setparam 13003:0

# Verify:
<SMS_LOGIN> <SMS_PASSWORD> getparam 13003

# Re-enable (tested successfully previously):
<SMS_LOGIN> <SMS_PASSWORD> setparam 13003:1

# Verify again:
<SMS_LOGIN> <SMS_PASSWORD> getparam 13003
```

A `Value:0` means FOTA setting disabled, **not** proof the seller lost all administrative access or account ownership. Disabling FOTA can prevent legitimate remote update/configuration functions. Re-enabling returns the device's FOTA capability, potentially including access by the current FOTA account holder. As of the last confirmed readback after testing, FOTA was re-enabled; another disable request was discussed but no subsequent successful reply was provided. **Always read current value first.**

### B. Change SMS password

```text
# Do NOT insert real secrets in source code, tickets or runbook:
<SMS_LOGIN> <OLD_SMS_PASSWORD> setparam 3004:<NEW_SMS_PASSWORD>

# Test using NEW credential after success:
<SMS_LOGIN> <NEW_SMS_PASSWORD> getinfo
```

Firmware family reference lists SMS login (`3003`) and password (`3004`) with up to 5 characters each. A password change was tested on the device with new-password `getinfo` success. Do not persist actual passwords in the database as plaintext; use a secrets-management mechanism and strong access controls. Changing the SMS password does **not** unlock Configurator, transfer FOTA account ownership, or prove all alternate control paths are closed.

### C. Change destination server (NOT YET PERFORMED)

```text
# TEMPLATE ONLY — do not execute until new receiver is listening, protocol compatible,
# device ACK/records verified, and rollback window agreed.
<SMS_LOGIN> <SMS_PASSWORD> setparam 2004:<NEW_SERVER_IP_OR_DOMAIN>;2005:<NEW_SERVER_PORT>;2006:0

# Readback:
<SMS_LOGIN> <SMS_PASSWORD> getparam 2004;2005;2006
```

Changing destination may stop data reaching the existing platform immediately. Only switch a single test unit at first. Document original address/port securely, verify that the new server handles Teltonika TCP framing and ACK correctly, then consider fleet rollout. The old vendor may still have access through other channels.

### D. APN update (NOT YET PERFORMED)

```text
<SMS_LOGIN> <SMS_PASSWORD> setparam 2001:<APN>
<SMS_LOGIN> <SMS_PASSWORD> getparam 2001
```

Incorrect APN can interrupt cellular data and FOTA until repaired via SMS/USB/approved recovery. Avoid unless necessary.

### E. Change acquisition interval (NOT YET PERFORMED)

```text
# Example only; do not run without verifying expected reporting behaviour,
# cost, firmware restrictions and network/SIM limits.
<SMS_LOGIN> <SMS_PASSWORD> setparam 10050:5
<SMS_LOGIN> <SMS_PASSWORD> getparam 10050
```

The fleet project's candidate 5s moving / 60s stopped is a **test hypothesis, not an approved fixed setting**. Separate record generation criteria, packet batching, Send Period, roaming/unknown profile and battery/network impact.

## 5. Security, ownership and risk register

1. **SMS access != complete administrative ownership.** Knowing SMS credentials allows `getparam` and some `setparam`; Configurator Security Keyword may still lock USB access. Four attempts remained at last report. Avoid guessing keywords or consuming retry attempts.
2. **FOTA WEB account ownership unresolved.** FOTA enabled (`13003:1`) with official Teltonika host does not prove the seller owns the FOTA account; it does mean there is an independent remote management route if provisioned to an account. Seek official account/device transfer with purchase records and IMEI.
3. **Authorized SMS numbers unresolved.** First ten were empty; remaining slots not audited. Depending on settings, anyone with current SMS credentials may issue commands from an otherwise unlisted number.
4. **Device-side GPRS/AVL command channel may exist.** Assess commands supported by exact protocol and firmware. Do not assume SMS password change blocks all commands issued from the old server.
5. **Read-after-write** mandatory. Record Teltonika's SMS acknowledgement and a separate verification read. Delayed/no SMS response does not necessarily mean failure; avoid duplicate blind writes.
6. **Secrets**: Do not expose passwords in logs, Git, UI, SMS audit payloads or generated Codex code examples; avoid `getparam 3004` except break-glass authorized diagnostics.
7. **Transport**: cellular SMS and many device TCP links lack end-to-end cryptographic guarantees. IMEI alone is not strong identity proof; isolate receiver in DMZ and implement validation, rate limits, and authentication where supported.
8. **Emergency rollback**: maintain a secure baseline of existing server connection settings, test on one unit and confirm telemetry before rollout. Never push unsupported reset, firmware or security commands across the fleet without a verified recovery path.

## 6. Integration requirements for Codex

Build a device-control feature designed for authorized company personnel, not hardcoded commands:

- Catalog **parameter ID, purpose, type, scope, supported device models/firmware, read/write permission, risk classification, expected reply**.
- Support **read-only snapshots**, explicit operator-approved changes, dry-run preview, command tracking, provider SMS submission, device reply matching, readback verification and rollback plan.
- Differentiate statuses: `Draft`, `Approved`, `SubmittedToSMSProvider`, `DeliveredToHandset` (if carrier supports), `DeviceReplied`, `Verified`, `TimedOut`, `Failed`, `Unknown`.
- Keep an immutable audit trail with masked secrets; enforce RBAC so only authorized technical administrators can issue GPS configuration commands.
- Store device capabilities at the *firmware + model* level; never assume one FMB920 test proves every GPS supports identical IDs or SMS management.
- Stage commands to one test device before bulk rollout. Rate-limit SMS sends and respect reply length / segmentation / character encoding.
- Preserve separation of the GPS tracking backend (TCP/UDP receiver, processing queue and SQL Server) from the SMS management path. Reading remote settings does not mean telematics transport is working.
- Never modify an actual physical device without explicit operator confirmation in the application.

## 7. Links to official documentation

- Teltonika FMB920 SMS/GPRS commands: https://wiki.teltonika-gps.com/view/FMB920_SMS/GPRS_Commands
- Teltonika FMB920 first start / SMS configuration: https://wiki.teltonika-gps.com/view/FMB920_First_Start
- Teltonika FM/FMB/OBD family parameter list (verify hardware and firmware applicability): https://wiki.teltonika-gps.com/view/FM_FMB_OBD_Device_Family_Parameter_list
- Teltonika FMB920 Configuration: https://wiki.teltonika-gps.com/view/FMB920_Configuration

## 8. Items still open

- Confirm current value of `13003` after latest requested change.
- Read actual stopped-profile values (`10000`, `10004`, `10005`).
- Validate the exact firmware and relevant parameter mappings, permitted minimum reporting intervals, and SMS limits.
- Audit all supported authorized-number slots and any other remote-management mechanisms.
- Resolve FOTA account ownership and Configurator Security Keyword through approved manufacturer/distributor process.
- Verify server/protocol compatibility in a controlled test before repointing device traffic.
