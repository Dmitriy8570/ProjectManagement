using System.Text.Json.Serialization;
using DataAccess;
using System.Security.Claims;
using System.Text;
using BusinessLogic.Identity;
using DataAccess.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProjectManagement.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    // Send enums as their string names (e.g. "ToDo", "InProgress", "Done")
    // so payloads stay readable and the Vue client can treat them as
    // typed string unions. Both JSON I/O and the [FromQuery] binder honor it.
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

// CORS for the Vue dev server. Headers are listed explicitly because the
// CORS spec says `Access-Control-Allow-Headers: *` doesn't cover the
// `Authorization` header — Chrome will silently drop the bearer token from
// the request and the API returns 401. Listing it explicitly fixes that.
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:5173", "http://localhost:5174")
     .WithHeaders("Authorization", "Content-Type", "Accept", "Origin", "X-Requested-With")
     .AllowAnyMethod()));

builder.AddBusinessServices();
builder.AddDataAccessServices();

// ── JWT bearer authentication ─────────────────────────────────────────────────
// Vue talks to this API with `Authorization: Bearer <token>`. AddIdentityCore
// (in DataAccess) gives us UserManager/SignInManager; here we plug in the
// scheme that actually validates incoming tokens. JwtTokenService issues them
// on /api/auth/login.
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Key must be configured and at least 32 characters long.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            // Match the claim types JwtTokenService writes so [Authorize(Roles=...)]
            // and User.IsInRole(...) work without extra mapping.
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.NameIdentifier
        };
    });

builder.Services.AddAuthorization(o =>
{
    // Every endpoint requires auth unless explicitly opened with [AllowAnonymous].
    // /api/auth/login carries the opt-out.
    o.FallbackPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
await IdentitySeeder.SeedAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseExceptionHandler();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
