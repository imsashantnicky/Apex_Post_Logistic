# Copilot-assisted: drafted by GitHub Copilot, reviewed and edited by a human (see COPILOT-LOG.md).
@Spec
Feature: Shipment status update
    A dispatcher moves a shipment through its journey. A shipment only moves forward and never changes after it is
    delivered or failed.

Background:
    Given the dispatcher registers shipment "APX-40000001" for "Ava Example" in "Harbour Point"

Scenario: A shipment moves forward step by step
    When the dispatcher changes shipment "APX-40000001" to "Picked Up"
    And the dispatcher changes shipment "APX-40000001" to "In Transit"
    And the dispatcher changes shipment "APX-40000001" to "Out For Delivery"
    And the dispatcher changes shipment "APX-40000001" to "Delivered"
    Then shipment "APX-40000001" has status "Delivered"

Scenario: A backward move is refused
    Given shipment "APX-40000001" is currently at status "In Transit"
    When the dispatcher changes shipment "APX-40000001" to "Picked Up"
    Then the dispatcher is told that the shipment only moves forward
    And shipment "APX-40000001" has status "In Transit"

Scenario: A delivered shipment cannot change any more
    Given shipment "APX-40000001" is currently at status "Delivered"
    When the dispatcher changes shipment "APX-40000001" to "Failed"
    Then the dispatcher is told that the shipment cannot change any more
    And shipment "APX-40000001" has status "Delivered"

Scenario: An unknown shipment cannot be changed
    When the dispatcher changes shipment "APX-40000009" to "Picked Up"
    Then the dispatcher is told that the shipment was not found

Scenario Outline: A status change follows the shipment journey
    Given shipment "APX-40000001" is currently at status "<from>"
    When the dispatcher changes shipment "APX-40000001" to "<to>"
    Then the change is "<outcome>"
    And shipment "APX-40000001" has status "<result>"

Examples:
    | from       | to         | outcome | result     |
    | Created    | In Transit | allowed | In Transit |
    | In Transit | Picked Up  | refused | In Transit |
    | Picked Up  | Picked Up  | refused | Picked Up  |
    | Failed     | Delivered  | refused | Failed     |
