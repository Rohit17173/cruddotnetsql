using DotNetEnv;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyCrudApi.Data;
using MyCrudApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Load .env (keeps your existing behavior)
Env.Load();

// read connection string from environment
var connectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTION");

if (string.IsNullOrWhiteSpace(connectionString))
{
    // Fail early with a clear message in logs if the connection string is missing
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("ERROR: AZURE_SQL_CONNECTION environment variable is not set. Aborting startup.");
    Console.ResetColor();
    throw new InvalidOperationException("AZURE_SQL_CONNECTION environment variable is required.");
}

// register DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

// --- AUTOMATIC MIGRATION ON STARTUP (with retry) ---
async Task ApplyMigrationsWithRetryAsync(IServiceProvider services, int retries = 10, int delayMs = 5000)
{
    using var scope = services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    int attempt = 0;
    while (true)
    {
        try
        {
            attempt++;
            logger.LogInformation("Attempting to apply EF Core migrations (attempt {Attempt})...", attempt);

            // This will apply any pending migrations (create/update tables)
            db.Database.Migrate();

            logger.LogInformation("Database migrations applied successfully.");
            break;
        }
        catch (Exception ex) when (attempt <= retries)
        {
            // For SQL connectivity/transient errors it's okay to retry
            logger.LogWarning(ex, "Failed to apply migrations on attempt {Attempt}. Retrying in {Delay}ms...", attempt, delayMs);

            await Task.Delay(delayMs);
            // exponential backoff
            delayMs = Math.Min(delayMs * 2, 30000);
        }
        catch (Exception ex)
        {
            // No more retries — log and rethrow to stop the app from running in a bad state
            var msg = "Could not apply database migrations. See inner exception for details.";
            logger.LogError(ex, msg);
            throw;
        }
    }
}

// Kick off migration before wiring up endpoints so DB is ready for requests
await ApplyMigrationsWithRetryAsync(app.Services);


// ---- Your endpoints (unchanged) ----
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapPost("/persons", async (AppDbContext db, Person person) =>
{
    db.Persons.Add(person);
    await db.SaveChangesAsync();
    return Results.Created($"/persons/{person.Id}", person);
});

app.MapGet("/persons", async (AppDbContext db) =>
{
    return await db.Persons.ToListAsync();
});

app.MapPut("/persons/{id}", async (int id, AppDbContext db, Person updatedPerson) =>
{
    var person = await db.Persons.FindAsync(id);

    if (person is null)
        return Results.NotFound($"Person with ID {id} not found.");

    person.Name = updatedPerson.Name;
    person.Age = updatedPerson.Age;

    await db.SaveChangesAsync();

    return Results.Ok(person);
});

app.MapDelete("/persons/{id}", async (int id, AppDbContext db) =>
{
    var person = await db.Persons.FindAsync(id);

    if (person is null)
        return Results.NotFound($"Person with ID {id} not found.");

    db.Persons.Remove(person);
    await db.SaveChangesAsync();

    return Results.Ok($"Person with ID {id} deleted.");
});

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
