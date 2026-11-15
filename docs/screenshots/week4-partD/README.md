# Week 4 Part D – screenshots of the courier app

Start the API with `dotnet run --project src/ApexPost.Api --launch-profile https`. Start the app in Debug on Windows
or on the Android emulator, as described in [DEMO-SCRIPT.md](../../DEMO-SCRIPT.md). Save the files in this folder
with the names shown.

| File | What it shows | How to produce it |
| --- | --- | --- |
| `app-login.png` | Sign-in page: API address, demo courier card, paste-token card | Start the app signed out (**Sync Status → Sign out**) |
| `app-login-error.png` | Red "Enter a courier id like CR-101." | Type `cr-1`, tap **Sign in** |
| `app-new-scan-validation.png` | Field error "Enter a tracking number like APX-10000001." under the tracking number | **New Scan**: type `APX-1234`, tap **Save scan** |
| `app-new-scan-saved.png` | "Saved on this device as Pending (APX-20000003, InTransit)." | Enter a valid scan and tap **Save scan** |
| `app-lookup-404.png` | Friendly 404 text with "Reference: 00-…" | **Find a shipment** `APX-00000000` |
| `app-pending-list.png` | "Pending scans: 3" and three rows, oldest first | Stop the API, save three scans, open **Pending** |
| `app-offline-banner.png` | Red banner "Server unavailable – working offline" on a tab, circuit "Open", Last problems | API stopped, **Sync now** two or three times on **Sync Status** |
| `app-sync-recovered.png` | "3 synced, 0 failed, 0 waiting", circuit "Closed", no banner | Start the API, wait 15 s, **Sync now** |
| `app-session-expired.png` | Sign-in page with "Your session has expired. Sign in again; your scans are kept." | Paste a token made with `--valid-for 1m`, wait, save a scan |
| `app-android.png` | Any tab running in the Android emulator | Run with `-f net10.0-android` |
