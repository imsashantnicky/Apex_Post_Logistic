# Week 4 Part D – demo script (3–5 minutes)

This plan records the courier app from sign-in to offline sync and recovery, plus the 401 and 403 answers of the API.
All data is fictional: couriers `CR-101` and `CR-102`, tracking numbers `APX-20000001` to `APX-20000010` (the API
seeds them in Development).

Record on the **Android emulator**. Airplane mode is the honest way to show "offline": on Windows, airplane mode does
not cut `localhost`, so the app would still reach the API.

## Before recording

1. Set up the API secrets once, as described in [README – Set up the user-secrets](../README.md#set-up-the-user-secrets)
   (this includes the partner key `PartnerKeys:ParcelTrackDemo`), and trust the development certificate:
   `dotnet dev-certs https --trust`.
2. Create the two extra tokens and paste them into the variables at the top of
   [`src/ApexPost.Api/ApexPost.Api.http`](../src/ApexPost.Api/ApexPost.Api.http) (`@adminToken`, `@expiredToken`)
   and `@partnerKey`. Do not commit these values: undo the edits with `git restore` after recording.

   ```bash
   dotnet user-jwts create --project src/ApexPost.Api --role Admin --output token
   dotnet user-jwts create --project src/ApexPost.Api --role Courier --claim courierId=CR-101 --not-before 2026-01-01 --expires-on 2026-01-02 --output token
   ```

   Copy the expired token to the emulator's clipboard as well (paste it into the emulator once), for step 3:15.
3. Start the API in Development (terminal 1). Keep this terminal visible so the viewer sees it stop and start:

   ```bash
   dotnet run --project src/ApexPost.Api --launch-profile https
   ```

4. Start an emulator (Android Studio Device Manager, or `emulator -avd <name>`), check that `adb devices` lists it,
   then start a Debug build (terminal 2). Only Debug accepts the development certificate, and only for `localhost`
   and `10.0.2.2`; the app calls `https://10.0.2.2:7310/`, the emulator's address for the host's `localhost`.

   ```bash
   dotnet build src/Mobile/ApexPost.Courier -f net10.0-android -t:Run
   ```

5. If the app still holds a token from an earlier run, open **Sync Status** and tap **Sign out**, so the recording
   starts on the sign-in page. Check that the **Pending** tab shows 0.
6. Open `ApexPost.Api.http` in VS Code (REST Client extension) or Visual Studio and run `courierLogin` once, so
   `{{courierToken}}` is filled.
7. Start the screen recorder (Xbox Game Bar `Win+Alt+R` captures the whole window with emulator, terminal and editor
   side by side). Record at 1080p or more with a readable font size.

## Recording

| Time | Do this | Say / show |
| --- | --- | --- |
| **Sign-in** | | |
| 0:00 | Show the sign-in page | "API: https://10.0.2.2:7310/" under the title; sign in by courier id or by pasting a token |
| 0:10 | Type `cr-1`, tap **Sign in** | Red text "Enter a courier id like CR-101." No request is sent |
| 0:20 | Type `CR-101`, tap **Sign in** | The app calls `POST /dev/token`, keeps the token in SecureStorage (never in a file or log) and opens the tabs |
| **Offline scans (airplane mode)** | | |
| 0:35 | Swipe down the emulator's quick settings and switch on **airplane mode** | "The courier has no signal now" |
| 0:45 | **New Scan**: `apx-20000003`, InTransit, location `Central Hub`, **Save scan** | "Saved on this device as Pending (APX-20000003, InTransit)"; the number is upper-cased |
| 1:00 | Save two more: `APX-20000004` Pickup and `APX-20000005` Pickup, location `Harbour Point` | Each one says "Saved on this device as Pending". Background upload attempts fail quietly; the red "Server unavailable – working offline…" banner may appear, because the circuit breaker counts those failures |
| 1:20 | **Pending** tab | Live count "Pending scans: 3" from the Couchbase change listener, list oldest first |
| **Reconnect → auto-sync** | | |
| 1:30 | Wait until about 20 s have passed since the last scan, then switch **airplane mode off**. Do not tap anything | The connectivity trigger starts the outbox; the count drops to 0 on its own, the banner disappears |
| 1:50 | **Sync Status** | Last sync time just now, "3 synced, 0 failed, 0 waiting.", circuit "Closed – the server answers normally" (nobody tapped Sync now) |
| 2:00 | **New Scan → Find a shipment**: `APX-20000003` → **Look up** | Status InTransit with the latest scan from this device: the server has the offline scan |
| **401 and 403** | | |
| 2:10 | Switch to the `.http` file. Run "401 - no token" and "401 - an expired token" | 401 ProblemDetails: "A bearer token is required…" and "The bearer token has expired. Sign in again for a new token." |
| 2:25 | Run "403 - an Admin token cannot upload scans" and "403 - courier CR-101 cannot upload a scan recorded by CR-102" | 403 ProblemDetails: role check ("The role of your token does not allow this action.") and the courierId check ("Courier CR-101 cannot upload scan …, which was recorded by CR-102.") |
| 2:40 | Run "401 - an unknown key" and "200 - a partner key reads status…" | Partner route: wrong `X-Api-Key` 401, valid key 200 without recipient details |
| 2:55 | In the app: **Sync Status → Sign out** | Token removed from SecureStorage, sign-in page |
| 3:05 | Paste the expired token under **Or paste an access token**, tap **Use token** | The app accepts it (it has a courierId claim); only the server knows it has expired |
| 3:15 | **New Scan**: `APX-20000006` Pickup, **Save scan** | The upload gets 401: the app clears SecureStorage and returns to sign-in with "Your session has expired. Sign in again; your scans are kept." The scan stays Pending |
| 3:30 | Sign in again as `CR-101` | The sync after sign-in uploads the waiting scan; **Pending** shows 0 |
| **Server down → circuit breaker → recovery** | | |
| 3:45 | **Stop the API** (Ctrl+C in terminal 1) | "Now the phone has signal but the server is down" |
| 3:50 | **New Scan**: `APX-20000007` Pickup, **Save scan**; then **Sync Status → Sync now** two or three times | Retries fail, the circuit opens: red banner "Server unavailable – working offline. Your scans stay on this device." on every tab, circuit "Open – calls are paused after repeated failures", Last problems lists the reason, the scan stays Pending |
| 4:15 | **Start the API again** (terminal 1, arrow up + Enter) and wait about 15 s (the break time) | Explain: after the break the next call is a trial call ("Half-open") |
| 4:35 | **Sync Status → Sync now** | "1 synced, 0 failed, 0 waiting.", banner gone, circuit "Closed – the server answers normally"; **Pending** count 0 |
| 4:50 | End on the **Sync Status** page | One-line summary: offline first, idempotent upload, JWT + roles, resilient client |

If the reconnect at 1:30 happens while the circuit is still open, the scans stay Pending and the banner stays. Wait
15 s and tap **Sync now**; mention that this is the breaker protecting the server.

## After recording

- Trim the video to 3–5 minutes and save it outside the repository (videos are not committed). Upload it with the
  submission.
- Restore the `.http` file so no token or key is committed: `git restore src/ApexPost.Api/ApexPost.Api.http`.
- Take the screenshots listed in [screenshots/week4-partD/README.md](screenshots/week4-partD/README.md) while the
  app is in the matching state.
