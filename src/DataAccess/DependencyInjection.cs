using BusinessLogic.Documents;
using BusinessLogic.Employees;
using BusinessLogic.Projects;
using DataAccess;
using DataAccess.Infrastructure;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

// Lives in the Microsoft.Extensions.DependencyInjection namespace so callers
// can pick it up off IHostApplicationBuilder without an extra using.
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the EF Core DbContext and the repository implementations.
    /// The database engine is chosen at startup from configuration
    /// (<c>Database:Provider</c>), not at compile time, so the same binary
    /// can be deployed against either MSSQL or SQLite without a rebuild —
    /// the task requires the application to be easy to install.
    /// </summary>
    public static void AddDataAccessServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured.");

        var provider = builder.Configuration["Database:Provider"];

        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            switch (provider?.Trim().ToLowerInvariant())
            {
                case "sqlite":
                    options.UseSqlite(connectionString);
                    break;
                case null:
                case "":
                case "sqlserver":
                case "mssql":
                    options.UseSqlServer(connectionString);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported database provider '{provider}'. " +
                        "Use 'SqlServer' (default) or 'Sqlite'.");
            }
        });

        // Repositories are scoped to match the DbContext lifetime so a single
        // unit of work spans the whole request.
        builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
        builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

        builder.Services.Configure<FileStorageOptions>(
            builder.Configuration.GetSection("FileStorage"));
        builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
    }
}
