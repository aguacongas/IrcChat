using IrcChat.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Data;

public class ChatDbContext(DbContextOptions<ChatDbContext> options) : DbContext(options)
{
    public DbSet<Message> Messages { get; set; }
    public DbSet<Channel> Channels { get; set; }
    public DbSet<Admin> Admins { get; set; }
    public DbSet<ConnectedUser> ConnectedUsers { get; set; }
    public DbSet<ReservedUsername> ReservedUsernames { get; set; }
    public DbSet<PrivateMessage> PrivateMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Channel);
            entity.HasIndex(e => e.Timestamp);
        });

        modelBuilder.Entity<Channel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
        });

        modelBuilder.Entity<ConnectedUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ConnectionId).IsUnique();
            entity.HasIndex(e => e.Channel);
            entity.HasIndex(e => new { e.Username, e.Channel });
        });

        modelBuilder.Entity<ReservedUsername>()
            .HasIndex(r => r.Username)
            .IsUnique();

        modelBuilder.Entity<ReservedUsername>()
            .HasIndex(r => new { r.Provider, r.ExternalUserId })
            .IsUnique();

        modelBuilder.Entity<PrivateMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SenderUsername);
            entity.HasIndex(e => e.RecipientUsername);
            entity.HasIndex(e => e.Timestamp);
        });
    }
}
