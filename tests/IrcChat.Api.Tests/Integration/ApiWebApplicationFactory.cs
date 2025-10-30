// tests/IrcChat.Api.Tests/Integration/ApiWebApplicationFactory.cs
using IrcChat.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IrcChat.Api.Tests.Integration;

public class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remplacer le DbContext par une version InMemory pour les tests
            services.RemoveAll(typeof(DbContextOptions<ChatDbContext>));
            services.RemoveAll(typeof(ChatDbContext));

            services.AddDbContext<ChatDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDatabase");
            });

            // S'assurer que la base de données est créée
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
            db.Database.EnsureCreated();
        });

        builder.UseEnvironment("Testing");
    }
}