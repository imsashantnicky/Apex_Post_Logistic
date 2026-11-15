# Week 4 Part A – screenshots

Screenshots of the courier console harness. Start it with
`dotnet run --project src/Courier/ApexPost.Courier.Harness`, choose **10 Seed Fictional Demo Data** once, then take each
screenshot below. Save them in this folder with the file names shown.

| File | What it shows | How to produce it |
| --- | --- | --- |
| `find-by-tracking.png` | Every scan of one shipment, oldest first | Menu **2 Find by Tracking Number**, enter `APX-10000001` |
| `list-pending.png` | The scans waiting for upload (`syncStatus = Pending`) | Menu **3 List Pending** |
| `scans-per-type.png` | Count of scans per scan type for one courier | Menu **7 Scans per Type for Courier**, enter `CR-101` |
| `simulate-sync.png` | Outbox run: attempted, synced and failed counts; `APX-99999999` ends `Failed` | Menu **4 Simulate Sync**, then **3 List Pending** (now empty) |
| `listener-log.png` | Live change lines (`added`/`updated`/`deleted`, document id) and `[LIVE] Pending scans: N` | Menu **1 Add Scan** or **4 Simulate Sync**; show the console and `logs/listener.log` (in the folder the harness was started from) |
| `query-plans.png` | `SEARCH _ USING INDEX idx_scan_trackingNumber` and `idx_scan_syncStatus` | Menu **11 Show Query Plans** |
