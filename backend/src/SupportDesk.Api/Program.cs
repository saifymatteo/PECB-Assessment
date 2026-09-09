using Microsoft.EntityFrameworkCore;
using SupportDesk.Api.Features;
using SupportDesk.Api.Infrastructure;
using SupportDesk.Api.Infrastructure.ErrorHandling;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("SupportDesk")));

// RFC 7807 everywhere: business rules via the handler below, everything else via the
// default ProblemDetails pipeline (500 responses stay sanitized).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BusinessRuleExceptionHandler>();
builder.Services.AddExceptionHandler<NotFoundExceptionHandler>();

builder.Services.AddScoped<TicketReferenceGenerator>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<IAgentService, AgentService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

app.MapControllers();

// Apply migrations and seed demo data on startup in every environment (the seeder is a
// no-op when data exists) - the Docker image runs in Production and still needs a usable DB.
await using var scope = app.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
await db.Database.MigrateAsync();
await DbSeeder.SeedAsync(db);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();
