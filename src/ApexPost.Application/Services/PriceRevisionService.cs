using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Extensions;
using ApexPost.Application.Validation;
using ApexPost.Domain.Personnel;

namespace ApexPost.Application.Services;

/// <summary>
/// Raises or cuts the prices of a category by a percentage, for administrators. Baskets keep the prices they were
/// built with, because a retail line copies the price when it is added.
/// </summary>
public sealed class PriceRevisionService(
    ICurrentUser user,
    IProductRepository products,
    IProductCatalog catalog,
    IUnitOfWork unitOfWork,
    AttributeModelValidator validator,
    IClock clock)
{
    private const int PercentDecimals = 2;

    /// <summary>Returns how many products changed.</summary>
    public async Task<int> ReviseAsync(PriceRevisionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        user.RequireRole(StaffRole.Administrator);
        validator.EnsureValid(command);
        var percent = Math.Round(command.Percent, PercentDecimals, MidpointRounding.AwayFromZero);
        if (percent == 0 || Math.Abs(percent) > PriceRevisionCommand.MaximumPercent)
        {
            throw new ValidationFailedException(
                nameof(command.Percent),
                $"Percent must be between -{PriceRevisionCommand.MaximumPercent} and "
                + $"{PriceRevisionCommand.MaximumPercent}, and not zero.");
        }

        var changed = 0;
        await unitOfWork.ExecuteInTransactionAsync(
            async token => changed = await products
                .RevisePricesAsync(command.Category, percent, clock.UtcNow, token)
                .ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        catalog.Refresh();
        return changed;
    }
}
