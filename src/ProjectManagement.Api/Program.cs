using System.Text.Json.Serialization;
using DataAccess;
using Microsoft.EntityFrameworkCore;
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

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:5173", "http://localhost:5174")
     .AllowAnyHeader()
     .AllowAnyMethod()));

builder.AddBusinessServices();
builder.AddDataAccessServices();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program { }
