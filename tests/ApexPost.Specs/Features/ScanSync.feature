@Spec
Feature: Scan sync
    A courier scans parcels on the device even without a connection. Every scan is kept on the device first and is
    sent to the server when the connection returns. The server stores each scan once, explains every scan it refuses
    and does not accept a scan that is more than 72 hours old.

Background:
    Given courier "CR-101" has shipment "APX-30000001"
    And the courier is offline

Scenario: Offline scans are uploaded when the connection returns
    When the courier records a "Pickup" scan for shipment "APX-30000001" at "Central Hub"
    And the courier records an "InTransit" scan for shipment "APX-30000001" at "Harbour Point Depot"
    And the device tries to sync
    Then 2 scans are waiting to be sent
    When the connection returns
    And the device syncs
    Then every scan on the device is "Synced"
    And the server holds 2 scans for shipment "APX-30000001"
    And shipment "APX-30000001" has status "In Transit"

Scenario: A scan that reaches the server twice is stored once
    Given the courier records a "Pickup" scan for shipment "APX-30000001" at "Central Hub"
    And the connection returns
    And the answer to the next sync is lost on the way back
    When the device syncs
    Then 1 scan is waiting to be sent
    When the device syncs
    Then the server reports 1 duplicate scan skipped
    And every scan on the device is "Synced"
    And the server holds 1 scan for shipment "APX-30000001"

Scenario: An invalid scan is rejected with a reason
    Given the courier records a "Pickup" scan for shipment "APX-39999999" at "Central Hub"
    And the connection returns
    When the device syncs
    Then the scan for shipment "APX-39999999" is "Failed"
    And the server answers "Unknown tracking number."
    And the server holds 0 scans for shipment "APX-30000001"

Scenario Outline: A scan is accepted only while it is at most 72 hours old
    Given the courier records a "Pickup" scan for shipment "APX-30000001" at "Central Hub"
    And <hours> hours pass before the connection returns
    When the device syncs
    Then the scan for shipment "APX-30000001" is "<sync status>"
    And the server answers "<answer>"
    And the server holds <stored> scans for shipment "APX-30000001"

Examples:
    | hours | sync status | answer                       | stored |
    | 71    | Synced      | accepted                     | 1      |
    | 72    | Synced      | accepted                     | 1      |
    | 73    | Failed      | Scan is older than 72 hours. | 0      |
