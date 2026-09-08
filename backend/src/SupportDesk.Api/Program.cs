using Microsoft.EntityFrameworkCore;
using SupportDesk.Api.Infrastructure;
using SupportDesk.Api.Infrastructure.ErrorHandling;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("SupportDesk")));

// RFC 7807 everywhere: business rules via the handler below, everything else via the
// default ProblemDetails pipeline (500 responses stay sanitized).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BusinessRuleExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Apply migrations and seed demo data on first run (seeder is a no-op when data exists).
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

app.MapControllers();

app.Run();
