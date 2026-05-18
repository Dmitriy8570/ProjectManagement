using BusinessLogic.Common;
using BusinessLogic.Employees;
using BusinessLogic.Projects;

namespace Tests.BusinessLogic;

public class ProjectTests
{
    private static readonly DateTime DefaultStart = new(2024, 1, 1);
    private static readonly DateTime DefaultEnd = new(2024, 12, 31);

    // Employee.Id is settable only by EF Core (private setter), so unit-test
    // employees would otherwise all share Id == 0 and trip the PM/participant
    // disjointness check. Reflection is the standard way around that.
    private static Employee CreateEmployee(int id = 0, string email = "person@example.com")
    {
        var employee = new Employee("First", "Last", "Patronymic", email);
        if (id != 0)
            typeof(Employee).GetProperty(nameof(Employee.Id))!.SetValue(employee, id);
        return employee;
    }

    // Factory with sensible defaults: each test only spells out the field it
    // actually exercises, which keeps the intent of the test visible at a glance.
    private static Project CreateProject(
        string name = "Test Project",
        string customerCompany = "Customer Co",
        string executingCompany = "Executing Co",
        DateTime? startDate = null,
        DateTime? endDate = null,
        Employee? projectManager = null,
        int priority = 3) =>
        new(
            name,
            customerCompany,
            executingCompany,
            startDate ?? DefaultStart,
            endDate ?? DefaultEnd,
            projectManager ?? CreateEmployee(id: 1),
            priority);

    // ---------- Constructor ----------

    [Fact]
    public void Constructor_WithValidData_PopulatesAllFields()
    {
        var pm = CreateEmployee(id: 42);

        var project = CreateProject(projectManager: pm, priority: 3);

        Assert.Equal("Test Project", project.Name);
        Assert.Equal("Customer Co", project.CustomerCompany);
        Assert.Equal("Executing Co", project.ExecutingCompany);
        Assert.Equal(DefaultStart, project.StartDate);
        Assert.Equal(DefaultEnd, project.EndDate);
        Assert.Same(pm, project.ProjectManager);
        Assert.Equal(pm.Id, project.ProjectManagerId);
        Assert.Equal(3, project.Priority);
        Assert.Empty(project.Employees);
    }

    [Fact]
    public void Constructor_TrimsStringFields()
    {
        // Trimming is delegated to DomainGuard.NotBlank — re-asserted here so a
        // future refactor that bypasses the guard cannot silently regress it.
        var project = CreateProject(
            name: "  Padded Name  ",
            customerCompany: "  Padded Customer  ",
            executingCompany: "  Padded Executing  ");

        Assert.Equal("Padded Name", project.Name);
        Assert.Equal("Padded Customer", project.CustomerCompany);
        Assert.Equal("Padded Executing", project.ExecutingCompany);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r")]
    public void Constructor_WithBlankName_Throws(string invalidName)
    {
        Assert.Throws<DomainValidationException>(() => CreateProject(name: invalidName));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r")]
    public void Constructor_WithBlankCustomerCompany_Throws(string invalidCustomerCompany)
    {
        Assert.Throws<DomainValidationException>(
            () => CreateProject(customerCompany: invalidCustomerCompany));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r")]
    public void Constructor_WithBlankExecutingCompany_Throws(string invalidExecutingCompany)
    {
        Assert.Throws<DomainValidationException>(
            () => CreateProject(executingCompany: invalidExecutingCompany));
    }

    [Fact]
    public void Constructor_WithNameLongerThanMax_Throws()
    {
        // Project caps the name at 200 chars (Project.cs); just over the limit
        // is the meaningful boundary to assert.
        Assert.Throws<DomainValidationException>(
            () => CreateProject(name: new string('a', 201)));
    }

    [Fact]
    public void Constructor_WithNullProjectManager_Throws()
    {
        // PM uses ArgumentNullException.ThrowIfNull rather than the domain
        // exception: a missing PM is a programmer error from the caller, not
        // a user-input violation, and the API layer maps the two differently.
        Assert.Throws<ArgumentNullException>(() =>
            new Project("Test", "test", "test", DefaultStart, DefaultEnd, null!, 3));
    }

    [Fact]
    public void Constructor_WithEndDateBeforeStartDate_Throws()
    {
        Assert.Throws<DomainValidationException>(
            () => CreateProject(startDate: DefaultEnd, endDate: DefaultStart));
    }

    [Fact]
    public void Constructor_WithEndEqualsStart_IsAllowed()
    {
        // Boundary case: zero-length projects are explicitly legal per DomainGuard.DateRange.
        var project = CreateProject(startDate: DefaultStart, endDate: DefaultStart);

        Assert.Equal(DefaultStart, project.StartDate);
        Assert.Equal(DefaultStart, project.EndDate);
    }

    [Fact]
    public void Constructor_WithNegativePriority_Throws()
    {
        Assert.Throws<DomainValidationException>(() => CreateProject(priority: -1));
    }

    [Fact]
    public void Constructor_WithZeroPriority_IsAllowed()
    {
        // Boundary case: NonNegative rejects strictly-negative, so 0 must pass.
        var project = CreateProject(priority: 0);

        Assert.Equal(0, project.Priority);
    }

    // ---------- AddEmployee ----------

    [Fact]
    public void AddEmployee_WithNull_Throws()
    {
        var project = CreateProject();

        Assert.Throws<ArgumentNullException>(() => project.AddEmployee(null!));
    }

    [Fact]
    public void AddEmployee_WithNewEmployee_AddsToParticipants()
    {
        var project = CreateProject(projectManager: CreateEmployee(id: 1));
        var employee = CreateEmployee(id: 2);

        project.AddEmployee(employee);

        Assert.Single(project.Employees);
        Assert.Contains(employee, project.Employees);
    }

    [Fact]
    public void AddEmployee_WhenEmployeeIsProjectManager_Throws()
    {
        // Enforces the "PM and participants are disjoint" invariant. If the PM
        // could double as a participant, any per-employee aggregation would
        // count them twice.
        var pm = CreateEmployee(id: 1);
        var project = CreateProject(projectManager: pm);

        Assert.Throws<DomainValidationException>(() => project.AddEmployee(pm));
    }

    [Fact]
    public void AddEmployee_WhenAlreadyAdded_IsNoOp()
    {
        // Idempotency by Id, not by reference: callers should be able to add
        // the "same" employee twice without first checking membership.
        var project = CreateProject(projectManager: CreateEmployee(id: 1));
        var employee = CreateEmployee(id: 2);

        project.AddEmployee(employee);
        project.AddEmployee(employee);

        Assert.Single(project.Employees);
    }

    // ---------- RemoveEmployee ----------

    [Fact]
    public void RemoveEmployee_WhenPresent_RemovesAndReturnsTrue()
    {
        var project = CreateProject(projectManager: CreateEmployee(id: 1));
        var employee = CreateEmployee(id: 2);
        project.AddEmployee(employee);

        var removed = project.RemoveEmployee(employee.Id);

        Assert.True(removed);
        Assert.Empty(project.Employees);
    }

    [Fact]
    public void RemoveEmployee_WhenAbsent_ReturnsFalseAndLeavesListIntact()
    {
        var project = CreateProject(projectManager: CreateEmployee(id: 1));
        project.AddEmployee(CreateEmployee(id: 2));

        var removed = project.RemoveEmployee(employeeId: 999);

        Assert.False(removed);
        Assert.Single(project.Employees);
    }

    // ---------- Update ----------

    [Fact]
    public void Update_WithAllArgumentsNull_LeavesProjectUnchanged()
    {
        // Partial-update contract: every parameter is opt-in, so a no-arg call
        // must be a no-op — not a wipe of every field to default values.
        var pm = CreateEmployee(id: 1);
        var project = CreateProject(projectManager: pm);

        project.Update();

        Assert.Equal("Test Project", project.Name);
        Assert.Equal("Customer Co", project.CustomerCompany);
        Assert.Equal("Executing Co", project.ExecutingCompany);
        Assert.Equal(DefaultStart, project.StartDate);
        Assert.Equal(DefaultEnd, project.EndDate);
        Assert.Same(pm, project.ProjectManager);
        Assert.Equal(3, project.Priority);
    }

    [Fact]
    public void Update_AppliesEachProvidedField()
    {
        var project = CreateProject(projectManager: CreateEmployee(id: 1));
        var newPm = CreateEmployee(id: 2, email: "new.pm@example.com");

        project.Update(
            name: "New Name",
            customerCompany: "New Customer",
            executingCompany: "New Executing",
            startDate: new DateTime(2025, 2, 1),
            endDate: new DateTime(2025, 11, 30),
            projectManager: newPm,
            priority: 7);

        Assert.Equal("New Name", project.Name);
        Assert.Equal("New Customer", project.CustomerCompany);
        Assert.Equal("New Executing", project.ExecutingCompany);
        Assert.Equal(new DateTime(2025, 2, 1), project.StartDate);
        Assert.Equal(new DateTime(2025, 11, 30), project.EndDate);
        Assert.Same(newPm, project.ProjectManager);
        Assert.Equal(newPm.Id, project.ProjectManagerId);
        Assert.Equal(7, project.Priority);
    }

    [Fact]
    public void Update_WithBlankName_Throws()
    {
        var project = CreateProject();

        Assert.Throws<DomainValidationException>(() => project.Update(name: ""));
    }

    [Fact]
    public void Update_WithOnlyStartDate_RevalidatesAgainstCurrentEndDate()
    {
        // Only one half of the range is supplied — the guard must compare it
        // against the entity's *existing* end date, not against default(DateTime),
        // otherwise a one-sided update could silently produce an inverted range.
        var project = CreateProject(startDate: DefaultStart, endDate: DefaultEnd);

        Assert.Throws<DomainValidationException>(
            () => project.Update(startDate: DefaultEnd.AddDays(1)));
    }

    [Fact]
    public void Update_WithOnlyEndDate_RevalidatesAgainstCurrentStartDate()
    {
        var project = CreateProject(startDate: DefaultStart, endDate: DefaultEnd);

        Assert.Throws<DomainValidationException>(
            () => project.Update(endDate: DefaultStart.AddDays(-1)));
    }

    [Fact]
    public void Update_WithNegativePriority_Throws()
    {
        var project = CreateProject();

        Assert.Throws<DomainValidationException>(() => project.Update(priority: -1));
    }

    [Fact]
    public void Update_WhenNewProjectManagerIsCurrentParticipant_SilentlyRemovesFromParticipants()
    {
        // Documents the "promote a participant to PM" path: when the two roles
        // would otherwise overlap, the participants entry is dropped rather
        // than the update being rejected. The invariant wins over the data.
        var project = CreateProject(projectManager: CreateEmployee(id: 1));
        var soonToBePm = CreateEmployee(id: 2, email: "future.pm@example.com");
        project.AddEmployee(soonToBePm);

        project.Update(projectManager: soonToBePm);

        Assert.Same(soonToBePm, project.ProjectManager);
        Assert.DoesNotContain(soonToBePm, project.Employees);
    }
}
