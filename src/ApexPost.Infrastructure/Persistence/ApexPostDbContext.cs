using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence;

/// <summary>The EF Core model of the SQL Server database. Entity sets and configurations are added per table.</summary>
public sealed class ApexPostDbContext(DbContextOptions<ApexPostDbContext> options) : DbContext(options);
