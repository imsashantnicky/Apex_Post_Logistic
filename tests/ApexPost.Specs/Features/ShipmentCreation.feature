@Spec
Feature: Shipment creation
    A dispatcher registers new shipments so couriers can scan them and customers can track them.
    Every tracking number is registered only once, and a parcel must weigh between 0.1 kg and 30 kg.

Background:
    Given I am signed in as a dispatcher with the Admin role
    And the shipment register is empty

Scenario: A valid shipment is registered and can be looked up
    When I register shipment "APX-30000101" for "Ava Example" in "Harbour Point" weighing 2.5 kg
    Then the shipment is registered
    And looking up shipment "APX-30000101" shows status "Created"
    And looking up shipment "APX-30000101" shows recipient "Ava Example" in "Harbour Point"
    And the shipment register holds 1 shipment

Scenario: A tracking number that is already registered is refused
    Given shipment "APX-30000102" is already registered
    When I register shipment "APX-30000102" for "Ben Example" in "Northgate" weighing 1.0 kg
    Then the shipment is refused as a duplicate
    And the shipment register holds 1 shipment

Scenario Outline: Shipments with an invalid weight are refused
    When I register shipment "APX-30000103" for "Ava Example" in "Harbour Point" weighing <weight> kg
    Then the shipment is refused with the message "<message>"
    And the shipment register holds 0 shipments

Examples:
    | weight | message                               |
    | 0      | Weight must be between 0.1 and 30 kg. |
    | 0.05   | Weight must be between 0.1 and 30 kg. |
    | 30.001 | Weight must be between 0.1 and 30 kg. |
    | -1     | Weight must be between 0.1 and 30 kg. |
