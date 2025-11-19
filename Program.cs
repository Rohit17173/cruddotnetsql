using DotNetEnv;

using Microsoft.EntityFrameworkCore;

using MyCrudApi.Data;

using MyCrudApi.Models;

var builder = WebApplication.CreateBuilder(args);

Env.Load();
var connectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTION");

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

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


