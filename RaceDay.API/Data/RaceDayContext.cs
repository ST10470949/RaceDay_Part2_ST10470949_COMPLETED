using Microsoft.EntityFrameworkCore;
using RaceDay.API.Models;

namespace RaceDay.API.Data
{
    public class RaceDayContext : DbContext
    {
        public RaceDayContext(DbContextOptions<RaceDayContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Event> Events => Set<Event>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Enrolment> Enrolments => Set<Enrolment>();
        public DbSet<Result> Results => Set<Result>();
        public DbSet<RouteWeatherInfo> RouteWeatherInfos => Set<RouteWeatherInfo>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Store the Role enum as text ("Organiser"/"Participant") so the column
            // matches the CHECK constraint used in the Part 1 SQL script.
            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .Property(u => u.CreatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            modelBuilder.Entity<Event>()
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            modelBuilder.Entity<Category>()
                .Property(c => c.MaxParticipants)
                .HasDefaultValue(100);

            modelBuilder.Entity<Enrolment>()
                .Property(e => e.EnrolmentDate)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            modelBuilder.Entity<Enrolment>()
                .Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(EnrolmentStatus.Confirmed);

            modelBuilder.Entity<RouteWeatherInfo>()
                .Property(w => w.UpdatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            // A Participant can only enrol once per Category - same rule as the
            // UQ_Enrolment constraint in Part 1.
            modelBuilder.Entity<Enrolment>()
                .HasIndex(e => new { e.ParticipantID, e.CategoryID })
                .IsUnique();

            // One Result per Enrolment.
            modelBuilder.Entity<Result>()
                .HasIndex(r => r.EnrolmentID)
                .IsUnique();

            // Avoid delete cycles through Users. Deleting an organiser/participant must
            // not silently remove their historical records.
            modelBuilder.Entity<Event>()
                .HasOne(e => e.Organiser)
                .WithMany(u => u.EventsOrganised)
                .HasForeignKey(e => e.OrganiserID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Enrolment>()
                .HasOne(e => e.Participant)
                .WithMany(u => u.Enrolments)
                .HasForeignKey(e => e.ParticipantID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Result>()
                .HasOne(r => r.CapturedBy)
                .WithMany()
                .HasForeignKey(r => r.CapturedByID)
                .OnDelete(DeleteBehavior.Restrict);

            // One current weather/route record per event.
            modelBuilder.Entity<Event>()
                .HasOne(e => e.WeatherInfo)
                .WithOne(w => w.Event)
                .HasForeignKey<RouteWeatherInfo>(w => w.EventID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RouteWeatherInfo>()
                .HasIndex(w => w.EventID)
                .IsUnique();
        }
    }
}
