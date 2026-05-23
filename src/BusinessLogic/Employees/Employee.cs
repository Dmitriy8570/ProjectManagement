using BusinessLogic.Common;
using BusinessLogic.Projects;

namespace BusinessLogic.Employees;

public class Employee
{
    public int Id { get; private set; }
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string Patronymic { get; private set; } = default!;

    // Email is owned by the linked Identity user (ApplicationUser.Email) so it
    // doesn't live on the domain entity — read-side queries project it via a
    // join, write-side commands change it through IUserAccountService.

    // Navigation: projects this employee participates in as a regular member.
    // Project management (where this employee is the PM) is modelled separately
    // on the Project side, so it is intentionally not exposed here.
    //
    // Exposed as IReadOnlyCollection backed by a private list so callers cannot
    // mutate the relationship bypassing the aggregate root (Project).
    private readonly List<Project> _projects = new();
    public IReadOnlyCollection<Project> Projects => _projects;

    // Required by EF Core to rehydrate the entity from the database.
    // Do not use from domain code — always go through the public constructor
    // so invariants are enforced.
    private Employee() { }

    public Employee(string firstName, string lastName, string? patronymic)
    {
        FirstName  = DomainGuard.NotBlank(firstName, nameof(firstName), maxLength: 100);
        LastName   = DomainGuard.NotBlank(lastName, nameof(lastName), maxLength: 100);
        Patronymic = DomainGuard.OptionalText(patronymic, nameof(patronymic), maxLength: 100);
    }

    /// <summary>
    /// Partial update: a <c>null</c> argument leaves the corresponding field
    /// unchanged. Validation runs on every non-null value so the entity can
    /// never end up in an invalid state. An empty string for Patronymic clears it.
    /// </summary>
    public void Update(
        string? firstName = null,
        string? lastName = null,
        string? patronymic = null)
    {
        if (firstName is not null)
            FirstName = DomainGuard.NotBlank(firstName, nameof(firstName), maxLength: 100);

        if (lastName is not null)
            LastName = DomainGuard.NotBlank(lastName, nameof(lastName), maxLength: 100);

        if (patronymic is not null)
            Patronymic = DomainGuard.OptionalText(patronymic, nameof(patronymic), maxLength: 100);
    }
}
