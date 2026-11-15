using System.Buffers.Text;
using System.Text;
using ApexPost.Courier.ApiClient.Auth;

namespace ApexPost.Courier.ApiClient.Tests.Auth;

public sealed class AccessTokenClaimsTests
{
    [Fact]
    public void The_courier_id_is_read_from_the_payload()
    {
        Assert.Equal("CR-101", AccessTokenClaims.TryGetCourierId(Token("""{"sub":"CR-101","courierId":"CR-101"}""")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    [InlineData("a.b")]
    [InlineData("header.%%%.signature")]
    public void Anything_that_is_not_a_jwt_gives_no_courier_id(string? token)
    {
        Assert.Null(AccessTokenClaims.TryGetCourierId(token));
    }

    [Theory]
    [InlineData("""{"sub":"admin"}""")]
    [InlineData("""{"courierId":"CR-1"}""")]
    [InlineData("""{"courierId":101}""")]
    [InlineData("""["CR-101"]""")]
    [InlineData("not json")]
    public void A_payload_without_a_well_formed_courier_id_gives_none(string payload)
    {
        Assert.Null(AccessTokenClaims.TryGetCourierId(Token(payload)));
    }

    private static string Token(string payload) =>
        $"eyJhbGciOiJIUzI1NiJ9.{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload))}.signature";
}
