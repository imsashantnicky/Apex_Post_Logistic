namespace ApexPost.Courier.ApiClient.Contracts;

/// <summary>Recipient block of a v2 create or replace request.</summary>
/// <param name="Name">Recipient name.</param>
/// <param name="Phone">Recipient phone number.</param>
/// <param name="Address">Street address.</param>
/// <param name="City">Destination city.</param>
public sealed record RecipientRequest(string Name, string Phone, string Address, string City);
