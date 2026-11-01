using ApexPost.Domain.Branches;

namespace ApexPost.Application.DTOs;

/// <summary>A simulated notification shown on the Notifications page and as a dashboard alert.</summary>
public sealed record Notification(NotificationKind Kind, BranchCode Branch, string Message, DateTimeOffset At);
