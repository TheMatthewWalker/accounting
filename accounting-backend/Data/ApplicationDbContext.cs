using Microsoft.EntityFrameworkCore;
using AccountingApp.Models;

namespace AccountingApp.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Organisation> Organisations { get; set; }
    public DbSet<OrganisationMember> OrganisationMembers { get; set; }
    public DbSet<GLAccount> GLAccounts { get; set; }
    public DbSet<DaybookEntry> DaybookEntries { get; set; }
    public DbSet<JournalEntry> JournalEntries { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<AccountBalance> AccountBalances { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>().HasKey(u => u.Id);
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.GoogleId).IsUnique().HasFilter("GoogleId IS NOT NULL");
        modelBuilder.Entity<User>().HasIndex(u => u.MicrosoftId).IsUnique().HasFilter("MicrosoftId IS NOT NULL");

        // Organisation configuration
        modelBuilder.Entity<Organisation>().HasKey(o => o.Id);
        modelBuilder.Entity<Organisation>()
            .HasMany(o => o.Members)
            .WithOne(om => om.Organisation)
            .HasForeignKey(om => om.OrganisationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Organisation>()
            .HasMany(o => o.GLAccounts)
            .WithOne(ga => ga.Organisation)
            .HasForeignKey(ga => ga.OrganisationId)
            .OnDelete(DeleteBehavior.Cascade);

        // OrganisationMember configuration
        modelBuilder.Entity<OrganisationMember>().HasKey(om => om.Id);
        modelBuilder.Entity<OrganisationMember>()
            .HasOne(om => om.User)
            .WithMany(u => u.OrganisationMemberships)
            .HasForeignKey(om => om.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // GLAccount configuration
        modelBuilder.Entity<GLAccount>().HasKey(ga => ga.Id);
        modelBuilder.Entity<GLAccount>()
            .HasMany(ga => ga.JournalEntries)
            .WithOne(je => je.GLAccount)
            .HasForeignKey(je => je.GLAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // DaybookEntry configuration
        modelBuilder.Entity<DaybookEntry>().HasKey(de => de.Id);
        modelBuilder.Entity<DaybookEntry>()
            .HasMany(de => de.JournalEntries)
            .WithOne(je => je.DaybookEntry)
            .HasForeignKey(je => je.DaybookEntryId)
            .OnDelete(DeleteBehavior.Cascade);
        // Configure Organisation relationship on DaybookEntry to use NO ACTION to avoid cascade cycles
        modelBuilder.Entity<DaybookEntry>()
            .HasOne(de => de.Organisation)
            .WithMany(o => o.DaybookEntries)
            .HasForeignKey(de => de.OrganisationId)
            .OnDelete(DeleteBehavior.NoAction);

        // JournalEntry configuration
        modelBuilder.Entity<JournalEntry>().HasKey(je => je.Id);

        // Customer configuration
        modelBuilder.Entity<Customer>().HasKey(c => c.Id);
        modelBuilder.Entity<Customer>()
            .HasOne(c => c.ControlAccount)
            .WithMany(ga => ga.Customers)
            .HasForeignKey(c => c.ControlAccountId)
            .OnDelete(DeleteBehavior.SetNull);
        // Configure Organisation relationship on Customer to use NO ACTION to avoid cascade cycles
        modelBuilder.Entity<Customer>()
            .HasOne(c => c.Organisation)
            .WithMany(o => o.Customers)
            .HasForeignKey(c => c.OrganisationId)
            .OnDelete(DeleteBehavior.NoAction);

        // Supplier configuration
        modelBuilder.Entity<Supplier>().HasKey(s => s.Id);
        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.ControlAccount)
            .WithMany(ga => ga.Suppliers)
            .HasForeignKey(s => s.ControlAccountId)
            .OnDelete(DeleteBehavior.SetNull);
        // Configure Organisation relationship on Supplier to use NO ACTION to avoid cascade cycles
        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.Organisation)
            .WithMany(o => o.Suppliers)
            .HasForeignKey(s => s.OrganisationId)
            .OnDelete(DeleteBehavior.NoAction);

        // AccountBalance configuration
        modelBuilder.Entity<AccountBalance>().HasKey(ab => ab.Id);
        modelBuilder.Entity<AccountBalance>()
            .HasOne(ab => ab.GLAccount)
            .WithMany()
            .HasForeignKey(ab => ab.GLAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
