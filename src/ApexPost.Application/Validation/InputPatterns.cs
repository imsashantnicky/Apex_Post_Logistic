namespace ApexPost.Application.Validation;

/// <summary>Regular expressions and examples for the <see cref="PatternAttribute"/> on input models.</summary>
public static class InputPatterns
{
    public const string Sku = "^[A-Z]{3}-[0-9]{4}$";
    public const string SkuExample = "PKG-0001";
    public const string BranchCode = "^BR[0-9]{3}$";
    public const string BranchCodeExample = "BR001";
    public const string ZoneCode = "^Z[0-9]{2}$";
    public const string ZoneCodeExample = "Z01";
    public const string Email = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
    public const string EmailExample = "name@apexpost.test";
    public const string DocumentNumber = "^[A-Z0-9]{6,20}$";
    public const string DocumentNumberExample = "6 to 20 letters or digits";
}
