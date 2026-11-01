using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Validation;

namespace ApexPost.UnitTests.Services;

public sealed class AttributeModelValidatorTests
{
    [Fact]
    public void Valid_model_has_no_errors()
    {
        var errors = new AttributeModelValidator().Validate(new AddRetailItemCommand { Sku = "pkg-0001" });

        Assert.Empty(errors);
    }

    [Fact]
    public void Every_rule_reports_its_own_message()
    {
        var model = new AddShippingCommand { ZoneCode = "Zone 1", WeightKg = 0m, Street = new string('x', 201) };

        var errors = new AttributeModelValidator().Validate(model);

        Assert.Equal("ZoneCode must look like Z01.", Assert.Single(errors["ZoneCode"]));
        Assert.Equal("WeightKg must be greater than zero.", Assert.Single(errors["WeightKg"]));
        Assert.Equal("Street must be at most 200 characters.", Assert.Single(errors["Street"]));
        Assert.Equal("City is required.", Assert.Single(errors["City"]));
    }

    [Fact]
    public void Ensure_valid_throws_with_the_field_errors()
    {
        var error = Assert.Throws<ValidationFailedException>(() =>
            new AttributeModelValidator().EnsureValid(new ReversalCommand { Reason = " " }));

        Assert.Equal(["Reason"], error.Errors.Keys);
    }

    [Fact]
    public void Rules_of_a_type_are_read_once_and_cached()
    {
        var validator = new AttributeModelValidator();

        validator.Validate(new AddRetailItemCommand());
        validator.Validate(new AddRetailItemCommand { Sku = "PKG-0001" });
        Assert.Equal(1, validator.CachedTypeCount);

        validator.Validate(new ReversalCommand());
        Assert.Equal(2, validator.CachedTypeCount);
    }

    [Fact]
    public void Cache_is_safe_for_parallel_requests()
    {
        var validator = new AttributeModelValidator();

        Parallel.For(0, 200, i => validator.Validate(new AddMoneyOrderCommand { Amount = i }));

        Assert.Equal(1, validator.CachedTypeCount);
    }

    [Fact]
    public void Secrets_are_left_out_of_command_text()
    {
        var verify = new VerifyIdentityCommand { DocumentNumber = "P12345678" }.ToString();
        var login = new LoginCommand { Email = "a@apexpost.test", Password = "secret-password" }.ToString();
        var password = new PasswordCommand { Password = "hunter2" }.ToString();

        Assert.DoesNotContain("P12345678", verify, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-password", login, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", password, StringComparison.Ordinal);
    }
}
