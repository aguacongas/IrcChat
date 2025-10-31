// tests/IrcChat.Api.Tests/Integration/ApiWebApplicationFactory.cs
using IrcChat.Api.Data;
using IrcChat.Api.Hubs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IrcChat.Api.Tests.Integration;

public class ApiWebApplicationFactory : WebApplicationFactory<ChatHub>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Remplacer le DbContext par une version InMemory pour les tests
            services.RemoveAll<IDbContextOptionsConfiguration<ChatDbContext>>()
                .RemoveAll<IDbContextFactory<ChatDbContext>>()
                .RemoveAll<DbContextOptions<ChatDbContext>>()
                .RemoveAll<ChatDbContext>();

            services.AddDbContext<ChatDbContext>(options => options.UseInMemoryDatabase("TestDatabase"))
                .AddSingleton<IDbContextFactory<ChatDbContext>>(sp =>
                {
                    var optionsBuilder = new DbContextOptionsBuilder<ChatDbContext>();
                    optionsBuilder.UseInMemoryDatabase("TestDatabase");
                    return new PooledDbContextFactory<ChatDbContext>(optionsBuilder.Options);
                }); ;

            // S'assurer que la base de données est créée
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
            db.Database.EnsureCreated();
        });

        builder.UseEnvironment("Testing");
    }
}