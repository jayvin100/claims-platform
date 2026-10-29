using Claims.Api.Claims;
using Claims.Api.Infrastructure.Identity;
using Claims.Api.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Infrastructure.Data;

/// <summary>The one database context shared by all modules (ADR-0001).</summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<DemoUser> Users => Set<DemoUser>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>The next number from the one sequence all Markets share (D19). Gaps are fine; repeats can't happen.</summary>
    // EF's SqlQuery for a single value reads it from a column that must be called "Value".
    public Task<long> NextClaimSequenceAsync() =>
        Database.SqlQuery<long>($"SELECT nextval('claim_number_seq') AS \"Value\"").SingleAsync();

    // Every save turns the Claims' raised events into outbox rows first. SaveChanges is one transaction,
    // so the Claim change and its events are stored together or not at all (ADR-0004).
    // The Claims keep their events until the save succeeds, so a failed save loses nothing.
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var claims = AddEventsToOutbox();
        int saved = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        claims.ForEach(claim => claim.ClearEvents());
        return saved;
    }

    // The API only saves asynchronously; this stops a synchronous save from skipping the outbox.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var claims = AddEventsToOutbox();
        int saved = base.SaveChanges(acceptAllChangesOnSuccess);
        claims.ForEach(claim => claim.ClearEvents());
        return saved;
    }

    private List<Claim> AddEventsToOutbox()
    {
        var claims = ChangeTracker.Entries<Claim>().Select(entry => entry.Entity).ToList();
        foreach (var claim in claims)
            OutboxMessages.AddRange(claim.Events.Select(OutboxMessage.From));
        return claims;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DemoUser>(user =>
        {
            user.ToTable("demo_users");
            user.Property(u => u.Name).HasMaxLength(100);
            user.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);

            // Seed data lives in the migration, so applying migrations twice never duplicates users.
            user.HasData(DemoUser.Seed);
        });

        modelBuilder.HasSequence<long>("claim_number_seq");

        modelBuilder.Entity<Claim>(claim =>
        {
            claim.ToTable("claims");
            claim.HasIndex(c => c.ClaimNumber).IsUnique();
            claim.HasIndex(c => c.ClaimantId);
            claim.HasIndex(c => c.AssignedOfficerId);
            claim.Property(c => c.ClaimNumber).HasMaxLength(20);
            claim.Property(c => c.PolicyNumber).HasMaxLength(50);
            claim.Property(c => c.Market).HasConversion<string>().HasMaxLength(2);
            claim.Property(c => c.Currency).HasMaxLength(3);
            claim.Property(c => c.ClaimType).HasConversion<string>().HasMaxLength(20);
            claim.Property(c => c.Description).HasMaxLength(2000);
            claim.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

            // Optimistic concurrency (D13): Npgsql maps a uint row version to Postgres's own xmin column,
            // which changes on every update. EF adds "WHERE xmin = <value when loaded>" to each UPDATE, so if
            // another request saved first, no row matches and SaveChanges throws DbUpdateConcurrencyException.
            // A shadow property, because the version is a database concern the Claim's rules never read.
            claim.Property<uint>("Version").IsRowVersion();

            // History is saved through the Claim (its "_history" field); ClaimId is a database-only column.
            claim.HasMany(c => c.History).WithOne().HasForeignKey("ClaimId").IsRequired();
            // Events are not columns; SaveChanges moves them to the outbox.
            claim.Ignore(c => c.Events);
        });

        modelBuilder.Entity<ClaimHistoryEntry>(entry =>
        {
            entry.ToTable("claim_history");
            entry.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entry.Property(e => e.Description).HasMaxLength(2000);
        });

        modelBuilder.Entity<OutboxMessage>(message =>
        {
            message.ToTable("outbox");
            message.HasKey(m => m.Sequence);
            message.HasIndex(m => m.EventId).IsUnique();
            message.Property(m => m.Type).HasMaxLength(50);
            message.Property(m => m.Payload).HasColumnType("jsonb");
        });
    }
}
