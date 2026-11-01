using ApexPost.Domain.Verification;
using ApexPost.Infrastructure.Verification;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Infrastructure;

public sealed class SimulatedIdentityVerificationServiceTests
{
    [Theory]
    [InlineData("P12345678", VerificationResult.Passed)]
    [InlineData("FAIL1234", VerificationResult.Failed)]
    [InlineData("fail1234", VerificationResult.Failed)]
    [InlineData("PEND1234", VerificationResult.Pending)]
    public async Task Result_depends_on_the_document_number(string number, VerificationResult expected)
    {
        var result = await new SimulatedIdentityVerificationService()
            .VerifyAsync(DocumentType.Passport, number, Ct);

        Assert.Equal(expected, result);
    }
}
