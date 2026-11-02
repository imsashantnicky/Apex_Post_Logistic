using ApexPost.Application.Reports;
using ApexPost.Application.Services;
using ApexPost.Application.Settings;
using ApexPost.Application.Validation;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using Microsoft.Extensions.Options;

namespace ApexPost.UnitTests.Support;

/// <summary>
/// Real application services wired to in-memory fakes, with one open branch (BR001), a second branch (BR002),
/// a closed branch (BR006), staff in every role, three products with 10 in stock at BR001 and zone Z01.
/// </summary>
internal sealed class ServiceHarness : IDisposable
{
    public const string StandardSku = "PKG-0001";
    public const string ReducedSku = "STA-0001";
    public const string ExemptSku = "OFF-0001";
    public const string ZoneCode = "Z01";
    public const string ClosedBranchCode = "BR006";
    public const int InitialStock = 10;

    private readonly NotificationSubscriber _subscriber;

    public ServiceHarness()
    {
        Tax = new TaxCalculator(Options.Create(new TaxSettings()));
        Rules = new CheckoutRules(Tax, Options.Create(MoneyOrderSettings), Options.Create(StockSettings));
        _subscriber = new NotificationSubscriber(Events, Sink, Options.Create(MoneyOrderSettings));

        Branch = Branches.Seed(TestData.CreateBranch(TestData.HomeBranch));
        OtherBranch = Branches.Seed(TestData.CreateBranch(TestData.OtherBranch));
        ClosedBranch = Branches.Seed(TestData.CreateBranch(ClosedBranchCode, isActive: false));

        Clerk = AddStaff(StaffRole.CounterClerk, TestData.HomeBranch, "clerk1.br001@apexpost.test");
        Manager = AddStaff(StaffRole.BranchManager, TestData.HomeBranch, "manager.br001@apexpost.test");
        Auditor = AddStaff(StaffRole.Auditor, TestData.HomeBranch, "auditor1@apexpost.test");
        Admin = AddStaff(StaffRole.Administrator, TestData.HomeBranch, "admin1@apexpost.test");
        OtherClerk = AddStaff(StaffRole.CounterClerk, TestData.OtherBranch, "clerk1.br002@apexpost.test");
        OtherManager = AddStaff(StaffRole.BranchManager, TestData.OtherBranch, "manager.br002@apexpost.test");
        ClosedBranchClerk = AddStaff(StaffRole.CounterClerk, ClosedBranchCode, "clerk1.br006@apexpost.test");

        AddProduct(StandardSku, 10.00m, TaxCategory.Standard);
        AddProduct(ReducedSku, 4.00m, TaxCategory.Reduced);
        AddProduct(ExemptSku, 2.50m, TaxCategory.Exempt);
        Catalog.Add(new ShippingZone(ZoneCode, "Local", new Money(5.00m), new Money(1.50m)));
        As(Clerk);
    }

    public MoneyOrderSettings MoneyOrderSettings { get; } = new();

    public StockSettings StockSettings { get; } = new();

    public FakeCurrentUser User { get; } = new();

    public FixedClock Clock { get; } = new();

    public InMemoryRepository<Branch, BranchCode> Branches { get; } = new(EntityCopies.Copy);

    public InMemoryRepository<Staff, Guid> StaffMembers { get; } = new(EntityCopies.Copy);

    public InMemoryRepository<StockItem, StockKey> Stock { get; } = new(EntityCopies.Copy);

    public InMemoryRepository<CounterTransaction, Guid> Transactions { get; } = new(EntityCopies.Copy);

    public InMemoryCatalog Catalog { get; } = new();

    public FakeIdentityVerificationService Verifier { get; } = new();

    public RecordingNotificationSink Sink { get; } = new();

    public InMemoryCredentialStore Credentials { get; } = new();

    public FakePasswordHasher Hasher { get; } = new();

    public CounterEvents Events { get; } = new();

    public StockGate Gate { get; } = new();

    public AttributeModelValidator Validator { get; } = new();

    public TaxCalculator Tax { get; }

    public CheckoutRules Rules { get; }

    public Branch Branch { get; }

    public Branch OtherBranch { get; }

    public Branch ClosedBranch { get; }

    public Staff Clerk { get; }

    public Staff Manager { get; }

    public Staff Auditor { get; }

    public Staff Admin { get; }

    public Staff OtherClerk { get; }

    public Staff OtherManager { get; }

    public Staff ClosedBranchClerk { get; }

    public ServiceHarness As(Staff staff)
    {
        User.SignInAs(staff);
        return this;
    }

    /// <summary>Like a request, every service gets a unit of work of its own.</summary>
    public CheckoutService Checkout()
    {
        var unitOfWork = new FakeUnitOfWork();
        var branches = new InMemoryBranches(Branches, unitOfWork);
        return new(
            Access(unitOfWork),
            branches,
            branches,
            Catalog,
            new ShippingPricer(Catalog, Options.Create(new ShippingSettings())),
            Verifier,
            new InMemoryTransactions(Transactions, unitOfWork),
            new InMemoryStock(Stock, unitOfWork),
            unitOfWork,
            Gate,
            Events,
            Rules,
            Validator,
            new RepositoryTransactionQueries(Transactions, Tax),
            Clock);
    }

    public ReversalService Reversal()
    {
        var unitOfWork = new FakeUnitOfWork();
        var stock = new InMemoryStock(Stock, unitOfWork);
        return new(Access(unitOfWork), stock, unitOfWork, Gate, Events, Validator, Clock);
    }

    public StockService StockAdjustments() => StockAdjustments(new FakeUnitOfWork());

    public StockService StockAdjustments(InMemoryStock stock, FakeUnitOfWork unitOfWork) =>
        new(User, Catalog, stock, Gate, Events, Rules, Validator, Clock);

    public InMemoryStock StockRecords(FakeUnitOfWork unitOfWork) => new(Stock, unitOfWork);

    public PriceRevisionService Prices(FakeUnitOfWork unitOfWork) =>
        new(User, Catalog, Catalog, unitOfWork, Validator, Clock);

    public ReportService Reports() =>
        new(
            new RepositoryReportQueries(Transactions, Stock, Catalog),
            Sink,
            User,
            Clock,
            Options.Create(StockSettings));

    public StaffService StaffAdmin()
    {
        var unitOfWork = new FakeUnitOfWork();
        var staff = new InMemoryStaff(StaffMembers, unitOfWork);
        return new(
            User,
            staff,
            staff,
            new InMemoryBranches(Branches, unitOfWork),
            Credentials,
            unitOfWork,
            Hasher,
            Validator,
            Options.Create(new PasswordSettings()));
    }

    public LoginService Login() =>
        new(new InMemoryStaff(StaffMembers, new FakeUnitOfWork()), Credentials, Hasher, new DummyPasswordHash(Hasher));

    public CatalogueService Catalogue() => new(User, new InMemoryCatalogueQueries(Catalog, Stock), Rules);

    public BranchService BranchAdmin()
    {
        var unitOfWork = new FakeUnitOfWork();
        var branches = new InMemoryBranches(Branches, unitOfWork);
        return new(User, branches, branches, new InMemoryTransactions(Transactions, unitOfWork), unitOfWork);
    }

    public async Task<int> StockOfAsync(string sku, string branch = TestData.HomeBranch) =>
        (await Stock.GetAsync(new StockKey(new BranchCode(branch), new Sku(sku)), CancellationToken.None))
            ?.Quantity ?? 0;

    public async Task<CounterTransaction> TransactionAsync(Guid id) =>
        await Transactions.GetAsync(id, CancellationToken.None)
            ?? throw new InvalidOperationException($"Transaction {id} was not stored.");

    public void Dispose()
    {
        _subscriber.Dispose();
        Gate.Dispose();
    }

    private Staff AddStaff(StaffRole role, string branch, string email) =>
        StaffMembers.Seed(new Staff(Guid.NewGuid(), "Sam Taylor", email, role, new BranchCode(branch)));

    private void AddProduct(string sku, decimal price, TaxCategory taxCategory)
    {
        Catalog.Add(TestData.CreateProduct(sku, price, taxCategory));
        Stock.Seed(TestData.CreateStock(sku, InitialStock));
    }

    private StockService StockAdjustments(FakeUnitOfWork unitOfWork) =>
        StockAdjustments(StockRecords(unitOfWork), unitOfWork);

    private TransactionAccess Access(FakeUnitOfWork unitOfWork) =>
        new(
            User,
            new InMemoryStaff(StaffMembers, unitOfWork),
            new InMemoryBranches(Branches, unitOfWork),
            new InMemoryTransactions(Transactions, unitOfWork),
            new RepositoryTransactionQueries(Transactions, Tax));
}
