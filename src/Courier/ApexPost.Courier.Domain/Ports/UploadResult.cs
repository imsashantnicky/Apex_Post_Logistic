namespace ApexPost.Courier.Domain.Ports;

/// <summary>The outcome of uploading one scan to the server.</summary>
/// <param name="Success">True when the server accepted the scan.</param>
/// <param name="Error">Why the upload failed; <see langword="null"/> on success.</param>
public sealed record UploadResult(bool Success, string? Error);
