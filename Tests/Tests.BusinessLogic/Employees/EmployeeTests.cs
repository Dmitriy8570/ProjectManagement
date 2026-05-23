using BusinessLogic.Common;
using BusinessLogic.Employees;

namespace Tests.BusinessLogic.Employees;

public class EmployeeTests
{
    // Email used to be a domain field and was validated here; it now lives on
    // the linked ApplicationUser. The Identity password/user validators cover
    // email format and uniqueness, so those cases are gone from this file.

    private static Employee CreateEmployee(
        string firstName = "Ivan",
        string lastName = "Ivanov",
        string? patronymic = "Ivanovich") =>
        new(firstName, lastName, patronymic);

    // ---------- Constructor ----------

    [Fact]
    public void Constructor_WithValidData_PopulatesAllFields()
    {
        var employee = CreateEmployee();

        Assert.Equal("Ivan", employee.FirstName);
        Assert.Equal("Ivanov", employee.LastName);
        Assert.Equal("Ivanovich", employee.Patronymic);
        Assert.Empty(employee.Projects);
    }

    [Fact]
    public void Constructor_TrimsStringFields()
    {
        var employee = CreateEmployee(
            firstName: "  Ivan  ",
            lastName: "  Ivanov  ",
            patronymic: "  Ivanovich  ");

        Assert.Equal("Ivan", employee.FirstName);
        Assert.Equal("Ivanov", employee.LastName);
        Assert.Equal("Ivanovich", employee.Patronymic);
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

    // ---------- Update ----------

    [Fact]
    public void Update_WithAllArgumentsNull_LeavesEmployeeUnchanged()
    {
        var employee = CreateEmployee();

        employee.Update();

        Assert.Equal("Ivan", employee.FirstName);
        Assert.Equal("Ivanov", employee.LastName);
        Assert.Equal("Ivanovich", employee.Patronymic);
    }

    [Fact]
    public void Update_AppliesEachProvidedField()
    {
        var employee = CreateEmployee();

        employee.Update(
            firstName: "Petr",
            lastName: "Petrov",
            patronymic: "Petrovich");

        Assert.Equal("Petr", employee.FirstName);
        Assert.Equal("Petrov", employee.LastName);
        Assert.Equal("Petrovich", employee.Patronymic);
    }

    [Fact]
    public void Update_WithOnlyFirstName_LeavesOtherFieldsUnchanged()
    {
        var employee = CreateEmployee();

        employee.Update(firstName: "Petr");

        Assert.Equal("Petr", employee.FirstName);
        Assert.Equal("Ivanov", employee.LastName);
        Assert.Equal("Ivanovich", employee.Patronymic);
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
    public void Update_TrimsUpdatedFields()
    {
        var employee = CreateEmployee();

        employee.Update(firstName: "  Petr  ");

        Assert.Equal("Petr", employee.FirstName);
    }

    [Fact]
    public void Update_DoesNotChangeFieldWhenNullPassed()
    {
        var employee = CreateEmployee();

        employee.Update(firstName: null, lastName: null, patronymic: null);

        Assert.Equal("Ivan", employee.FirstName);
        Assert.Equal("Ivanov", employee.LastName);
        Assert.Equal("Ivanovich", employee.Patronymic);
    }
}
