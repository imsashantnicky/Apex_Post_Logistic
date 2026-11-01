using System.Globalization;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Personnel;

namespace ApexPost.Infrastructure.Seeding;

/// <summary>
/// 26 staff in all four roles: per branch one manager (manager.br001@…) and clerks (clerk1.br001@…), plus
/// two auditors and two administrators. clerk4.br002 is inactive; manager.br006 works at the closed branch.
/// </summary>
public static class DemoStaff
{
    public const string EmailDomain = "@apexpost.test";
    public const string InactiveClerkEmail = "clerk4.br002" + EmailDomain;
    public const string HeadOffice = "BR001";

    private const int HeadOfficeAccounts = 2;

    private static readonly (string Branch, int Clerks)[] ClerksPerBranch =
    [
        ("BR001", 4), ("BR002", 4), ("BR003", 3), ("BR004", 2), ("BR005", 2), ("BR006", 1),
    ];

    private static readonly string[] Names =
    [
        "Amelia Hart", "Noah Patel", "Grace Okafor", "Liam Chen", "Olivia Brennan", "Ethan Novak",
        "Sofia Rossi", "Lucas Meyer", "Isla Murray", "Mason Clarke", "Ava Lindqvist", "Leo Fernandes",
        "Mia Kowalski", "James O'Neill", "Chloe Dubois", "Henry Adeyemi", "Ella Svensson", "Oscar Haddad",
        "Ruby Nakamura", "Jack Moreno", "Freya Walsh", "Samuel Ito", "Lily Gallagher", "Daniel Costa",
        "Zara Ahmed", "Thomas Byrne",
    ];

    public static IReadOnlyList<Staff> Create()
    {
        var names = new Queue<string>(Names);
        List<Staff> staff = [];
        for (var number = 1; number <= HeadOfficeAccounts; number++)
        {
            staff.Add(Member(names, Email("admin", number), StaffRole.Administrator, HeadOffice));
            staff.Add(Member(names, Email("auditor", number), StaffRole.Auditor, HeadOffice));
        }

        foreach (var (branch, clerks) in ClerksPerBranch)
        {
            var suffix = "." + branch.ToLowerInvariant() + EmailDomain;
            staff.Add(Member(names, "manager" + suffix, StaffRole.BranchManager, branch));
            for (var number = 1; number <= clerks; number++)
            {
                var email = "clerk" + number.ToString(CultureInfo.InvariantCulture) + suffix;
                staff.Add(Member(names, email, StaffRole.CounterClerk, branch));
            }
        }

        return staff;
    }

    private static string Email(string prefix, int number) =>
        prefix + number.ToString(CultureInfo.InvariantCulture) + EmailDomain;

    private static Staff Member(Queue<string> names, string email, StaffRole role, string branch) =>
        new(
            Guid.CreateVersion7(),
            names.Dequeue(),
            email,
            role,
            new BranchCode(branch),
            isActive: email != InactiveClerkEmail);
}
