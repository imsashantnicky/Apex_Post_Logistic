using System.Data;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Repositories;

/// <summary>
/// Runs dbo.usp_AdjustStock. The procedure decides under the row's lock and commits the quantity and its history
/// together; here its errors become the exceptions the rest of the application already handles. Values only ever
/// travel as parameters.
/// </summary>
public sealed class SqlStockAdjuster(ApexPostDbContext database) : IStockAdjuster
{
    private const int UnknownBranch = 51022;
    private const int UnknownProduct = 51023;
    private const int NotEnoughStock = 51024;
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task<int> AdjustAsync(
        BranchCode branch,
        Sku sku,
        int change,
        string reason,
        Guid staffId,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(sku);
        var newQuantity = new SqlParameter("@NewQuantity", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var code = new SqlParameter("@BranchCode", SqlDbType.VarChar, ProcedureParameters.BranchCodeLength)
        {
            Value = branch.Value,
        };
        var product = new SqlParameter("@Sku", SqlDbType.VarChar, ProcedureParameters.SkuLength) { Value = sku.Value };
        var text = new SqlParameter("@Reason", SqlDbType.NVarChar, TextLimits.Reason) { Value = reason };
        var adjustedAt = new SqlParameter("@AdjustedAt", SqlDbType.DateTime2) { Value = at.UtcDateTime };

        try
        {
            await database.Database.ExecuteSqlAsync(
                $"""
                EXEC dbo.usp_AdjustStock @BranchCode = {code}, @Sku = {product}, @Change = {change},
                    @Reason = {text}, @StaffId = {staffId}, @AdjustedAt = {adjustedAt},
                    @NewQuantity = {newQuantity} OUTPUT
                """,
                cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException exception) when (exception.Number is UnknownBranch or UnknownProduct)
        {
            throw new NotFoundException(exception.Message);
        }
        catch (SqlException exception) when (exception.Number == NotEnoughStock)
        {
            var available = await AvailableAsync(branch, sku, cancellationToken).ConfigureAwait(false);
            throw new InsufficientStockException(sku, -change, available);
        }
        catch (SqlException exception) when (exception.Number is UniqueIndexViolation or UniqueConstraintViolation)
        {
            throw new ConcurrencyConflictException("The record was changed by someone else.", exception);
        }

        return (int)newQuantity.Value;
    }

    private Task<int> AvailableAsync(BranchCode branch, Sku sku, CancellationToken cancellationToken) =>
        database.Set<StockItem>()
            .AsNoTracking()
            .Where(item => item.Branch == branch && item.Sku == sku)
            .Select(item => item.Quantity)
            .FirstOrDefaultAsync(cancellationToken);
}
