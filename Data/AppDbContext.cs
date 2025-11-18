using Microsoft.EntityFrameworkCore;
using MyCrudApi.Models;

namespace MyCrudApi.Data;

public class AppDbContext : DbContext
{
	public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}
    	public DbSet<Person> Persons => Set<Person>();
}
