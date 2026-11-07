# Week 4 Part D – demo script (3–5 minutes)

This script records the courier app from sign-in to offline sync. It works on Windows or on the Android emulator.
All data is fictional: couriers `CR-101` and `CR-102`, tracking numbers `APX-20000001` to `APX-20000010` (the API
seeds them in Development).

## Before recording

1. Set up the API secrets once, as described in [README – Set up the user-secrets](../README.md#set-up-the-user-secrets),
   and trust the development certificate: `dotnet dev-certs https --trust`.
2. Start the API in Development (terminal 1). Leave this terminal visible so the viewer can see it stop and start:

   ```bash
   dotnet run --project src/ApexPost.Api --launch-profile https
   ```

3. Start the app (terminal 2). Use a Debug build: only Debug accepts the development certificate, and only for
   `localhost` and `10.0.2.2`.
   - **Windows:**

     ```bash
     dotnet build src/Mobile/ApexPost.Courier -f net10.0-windows10.0.19041.0 -t:Run
     ```

   - **Android emulator:** start an emulator first (Android Studio Device Manager, or
     `emulator -avd <name>`). Check that `adb devices` lists it, then run the build below. The app calls
     `https://10.0.2.2:7310/`, which is the emulator's address for the host's `localhost`.

     ```bash
     dotnet build src/Mobile/ApexPost.Courier -f net10.0-android -t:Run
     ```

4. If the app still holds a token from an earlier run, open **Sync Status** and tap **Sign out**. The recording then
   starts on the sign-in page.
5. Start the screen recorder: Xbox Game Bar (`Win+Alt+R`) on Windows, or the emulator's
   *Extended controls → Record and playback*. Record at 1080p or more and make the font readable.

## Recording

| Time | Do this | Say / show |
| --- | --- | --- |
| 0:00 | Show the sign-in page | "API: https://…:7310/" under the title; two ways to sign in |
| 0:15 | Type `cr-1` and tap **Sign in** | Red text "Enter a courier id like CR-101." No request is sent |
| 0:25 | Type `CR-101` and tap **Sign in** | The app calls `POST /dev/token`, stores the token in SecureStorage and opens the tabs |
| 0:40 | **New Scan**: type `APX-1234` and tap **Save scan** | The field error under the tracking number. Nothing is saved |
| 0:50 | Enter `apx-20000003`, type InTransit, location `Central Hub`, tap **Save scan** | "Saved on this device as Pending (APX-20000003, InTransit)". The number is upper-cased |
| 1:05 | **Find a shipment**: `APX-20000003` → **Look up** | Status InTransit, latest scan from the device: the upload already happened |
| 1:15 | **Find a shipment**: `APX-00000000` → **Look up** | Friendly 404 text from ProblemDetails plus "Reference: 00-…" (trace id) |
| 1:30 | **Stop the API** (Ctrl+C in terminal 1) | Explain: the courier keeps working offline |
| 1:40 | **New Scan**: save 3 scans for `APX-20000004` (Pickup) and `APX-20000005` (Pickup) | Each one says "Saved on this device as Pending" |
| 2:00 | **Pending** tab | The live count ("Pending scans: 3") and the list, oldest first |
| 2:10 | **Sync Status** → **Sync now** two or three times | Retries fail, the circuit opens: red banner "Server unavailable – working offline" on every tab; Circuit breaker "Open"; Last problems lists the stop reason |
| 2:40 | Switch between the tabs | The banner is global; the scans stay Pending |
| 2:50 | **Start the API again** (terminal 1), wait about 15 s | Circuit breaker goes to "Half-open" on the next call |
| 3:10 | **Sync Status** → **Sync now** | "3 synced, 0 failed, 0 waiting", banner gone, circuit "Closed" |
| 3:20 | **Pending** tab | Count drops to 0 live, empty-state text |
| 3:30 | Optional, Android only: switch on airplane mode, save a scan, switch it off | The connectivity trigger uploads it without tapping Sync now |
| 3:50 | Optional: sign in with a token that expires soon (see below) and wait | After expiry the next sync gets 401: back to sign-in with "Your session has expired…", scans kept |
| 4:20 | **Sync Status** → **Sign out** | Token removed from SecureStorage, sign-in page |

Token for the 401 step (paste it under **Or paste an access token**; the API allows 30 s clock skew):

```bash
dotnet user-jwts create --project src/ApexPost.Api --role Courier --claim courierId=CR-101 --valid-for 1m
```

## After recording

- Trim the video to 3–5 minutes and save it outside the repository (videos are not committed). Upload it with the
  submission.
- Take the screenshots listed in [screenshots/week4-partD/README.md](screenshots/week4-partD/README.md) while the
  app is in the matching state.
