using BusinessLogic.Employees;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = default!;
}