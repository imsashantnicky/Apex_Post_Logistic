using ApexPost.Domain.Branches;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ApexPost.Infrastructure.Persistence.Converters;

/// <summary>Stores a <see cref="BranchCode"/> as its text, for example BR001.</summary>
public sealed class BranchCodeConverter()
    : ValueConverter<BranchCode, string>(code => code.Value, value => new BranchCode(value));
