namespace ApexPost.Courier.Domain.Enums;

/// <summary>Whether a scan recorded on the device has reached the server.</summary>
public enum SyncStatus
{
    /// <summary>Stored locally and waiting to be uploaded. Every new scan starts here.</summary>
    Pending = 1,

    /// <summary>Uploaded and accepted by the server.</summary>
    Synced = 2,

    /// <summary>The last upload attempt failed; the scan is retried later.</summary>
    Failed = 3,
}
