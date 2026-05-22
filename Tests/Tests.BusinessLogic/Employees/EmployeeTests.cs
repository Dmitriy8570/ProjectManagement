using BusinessLogic.Common;
using BusinessLogic.Employees;

namespace Tests.BusinessLogic.Employees;

public class EmployeeTests
{
    private static Employee CreateEmployee(
        string firstName = "Ivan",
        string lastName = "Ivanov",
        string? patronymic = "Ivanovich",
        string email = "ivan@example.com") =>
        new(firstName, lastName, patronymic, email);

    // ---------- Constructor ----------

    [Fact]
    public void Constructor_WithValidData_PopulatesAllFields()
    {
        var employee = CreateEmployee();

        Assert.Equal("Ivan", employee.FirstName);
        Assert.Equal("Ivanov", employee.LastName);
        Assert.Equal("Ivanovich", employee.Patronymic);
        Assert.Equal("ivan@example.com", employee.Email);
        Assert.Empty(employee.Projects);
    }

    [Fact]
    public void Constructor_TrimsStringFields()
    {
        var employee = CreateEmployee(
            firstName: "  Ivan  ",
            lastName: "  Ivanov  ",
            patronymic: "  Ivanovich  ",
            email: "  ivan@example.com  ");

        Assert.Equal("Ivan", employee.FirstName);
        Assert.Equal("Ivanov", employee.LastName);
        Assert.Equal("Ivanovich", employee.Patronymic);
        Assert.Equal("ivan@example.com", employee.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r")]
    public void Constructor_WithBlankFirstName_Throws(string invalid)
    {
        Assert.Throws<DomainValidationException>(() => CreateEmployee(firstName: invalid));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r")]
    public void Constructor_WithBlankLastName_Throws(string invalid)
    {
        Assert.Throws<DomainValidationException>(() => CreateEmployee(lastName: invalid));
    }

    // Patronymic is optional by design (Russian middle name, not always present).
    // Blank or null input must be accepted and stored as an empty string so the
    // rest of the system can treat the column as a normal non-null value.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r")]
    public void Constructor_WithBlankOrNullPatronymic_StoresEmptyString(string? blank)
    {
        var employee = CreateEmployee(patronymic: blank);

        Assert.Equal(string.Empty, employee.Patronymic);
    }

    [Fact]
    public void Constructor_WithFirstNameLongerThanMax_Throws()
    {
        Assert.Throws<DomainValidationException>(
            () => CreateEmployee(firstName: new string('a', 101)));
    }

    [Fact]
    public void Constructor_WithLastNameLongerThanMax_Throws()
    {
        Assert.Throws<DomainValidationException>(
            () => CreateEmployee(lastName: new string('a', 101)));
    }

    [Fact]
    public void Constructor_WithPatronymicLongerThanMax_Throws()
    {
        Assert.Throws<DomainValidationException>(
            () => CreateEmployee(patronymic: new string('a', 101)));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@nodomain.com")]
    [InlineData("nouser@")]
    [InlineData("missing-at-sign.com")]
    public void Constructor_WithInvalidEmail_Throws(string invalidEmail)
    {
        Assert.Throws<DomainValidationException>(() => CreateEmployee(email: invalidEmail));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithNullOrBlankEmail_Throws(string? invalidEmail)
    {
        Assert.Throws<DomainValidationException>(() => CreateEmployee(email: invalidEmail!));
    }

    [Fact]
    public void Constructor_WithEmailLongerThanMax_Throws()
    {
        var longLocal = new string('a', 95);
        var tooLong = $"{longLocal}@example.com";

        Assert.Throws<DomainValidationException>(() => CreateEmployee(email: tooLong));
    }

    // ---------- Update ----------

    [Fact]
    public void Update_WithAllArgumentsNull_LeavesEmployeeUnchanged()
    {
        var employee = CreateEmployee();

        employee.Update();

        Assert.Equal("Ivan", employee.FirstName);
        Assert.Equal("Ivanov", employee.LastName);
        Assert.Equal("Ivanovich", employee.Patronymic);
        Assert.Equal("ivan@example.com", employee.Email);
    }

    [Fact]
    public void Update_AppliesEachProvidedField()
    {
        var employee = CreateEmployee();

        employee.Update(
            firstName: "Petr",
            lastName: "Petrov",
            patronymic: "Petrovich",
            email: "petr@example.com");

        Assert.Equal("Petr", employee.FirstName);
        Assert.Equal("Petrov", employee.LastName);
        Assert.Equal("Petrovich", employee.Patronymic);
        Assert.Equal("petr@example.com", employee.Email);
    }

    [Fact]
    public void Update_WithOnlyFirstName_LeavesOtherFieldsUnchanged()
    {
        var employee = CreateEmployee();

        employee.Update(firstName: "Petr");

        Assert.Equal("Petr", employee.FirstName);
        Assert.Equal("Ivanov", employee.LastName);
        Assert.Equal("Ivanovich", employee.Patronymic);
        Assert.Equal("ivan@example.com", employee.Email);
    }

    [Fact]
    public void Update_WithBlankFirstName_Throws()
    {
        var employee = CreateEmployee();

        Assert.Throws<DomainValidationException>(() => employee.Update(firstName: ""));
    }

    [Fact]
    public void Update_WithBlankLastName_Throws()
    {
        var employee = CreateEmployee();

        Assert.Throws<DomainValidationException>(() => employee.Update(lastName: " "));
    }

    // Patronymic is optional, so passing a blank string through Update is the
    // intentional way to clear a previously set value — not an error.
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Update_WithBlankPatronymic_ClearsPatronymic(string blank)
    {
        var employee = CreateEmployee();

        employee.Update(patronymic: blank);

        Assert.Equal(string.Empty, employee.Patronymic);
    }

    [Fact]
    public void Update_WithInvalidEmail_Throws()
    {
        var employee = CreateEmployee();

        Assert.Throws<DomainValidationException>(() => employee.Update(email: "not-an-email"));
    }

    [Fact]
    public void Update_TrimsUpdatedFields()
    {
        var employee = CreateEmployee();

        employee.Update(firstName: "  Petr  ", email: "  petr@example.com  ");

        Assert.Equal("Petr", employee.FirstName);
        Assert.Equal("petr@example.com", employee.Email);
    }

    [Fact]
    public void Update_DoesNotChangeFieldWhenNullPassed()
    {
        var employee = CreateEmployee();

        employee.Update(firstName: null, lastName: null, patronymic: null, email: null);

        Assert.Equal("Ivan", employee.FirstName);
        Assert.Equal("Ivanov", employee.LastName);
        Assert.Equal("Ivanovich", employee.Patronymic);
        Assert.Equal("ivan@example.com", employee.Email);
    }
}
