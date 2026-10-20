using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Onygo.Api.Activities;
using Onygo.Api.Applications;
using Onygo.Api.Profiles;

namespace Onygo.Api.Infrastructure.Persistence;

public class OnygoDbContext(DbContextOptions<OnygoDbContext> options)
    : IdentityDbContext<IdentityUser>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<Profile> Profiles => Set<Profile>();

    public DbSet<Activity> Activities => Set<Activity>();

    public DbSet<ActivityApplication> Applications => Set<ActivityApplication>();

    private const int EnumColumnLength = 20;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Profile>(profile =>
        {
            profile.HasKey(p => p.UserId);
            profile.Property(p => p.FirstName).HasMaxLength(ProfileRules.MaxFirstNameLength);
            profile.HasOne<IdentityUser>().WithOne().HasForeignKey<Profile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Activity>(activity =>
        {
            activity.HasKey(a => a.Id);
            activity.Property(a => a.Type).HasConversion<string>().HasMaxLength(EnumColumnLength);
            activity.Property(a => a.Status).HasConversion<string>().HasMaxLength(EnumColumnLength);
            activity.Property(a => a.CustomType).HasMaxLength(ActivityRules.MaxCustomTypeLength);
            activity.Property(a => a.Title).HasMaxLength(ActivityRules.MaxTitleLength);
            activity.Property(a => a.Hook).HasMaxLength(ActivityRules.MaxHookLength);
            activity.ComplexProperty(a => a.Location, location =>
                location.Property(l => l.Address).HasMaxLength(ActivityRules.MaxAddressLength));
            activity.ComplexProperty(a => a.PublicAreaCenter);
            // Feed : activités ouvertes triées par date.
            activity.HasIndex(a => new { a.Status, a.StartsAt });
            // Pas de cascade : que deviennent les activités (et leurs candidatures, jamais supprimées — R10)
            // quand un compte est supprimé se décidera avec la suppression de compte.
            activity.HasOne<IdentityUser>().WithMany().HasForeignKey(a => a.OrganizerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ActivityApplication>(application =>
        {
            application.ToTable("ActivityApplications");
            application.HasKey(a => a.Id);
            application.Property(a => a.Status).HasConversion<string>().HasMaxLength(EnumColumnLength);
            application.Property(a => a.Message).HasMaxLength(ApplicationRules.MaxMessageLength);
            // R3 : une seule candidature par personne et par activité, quel que soit son statut.
            application.HasIndex(a => new { a.ActivityId, a.ApplicantId }).IsUnique();
            application.HasIndex(a => new { a.ApplicantId, a.AppliedAt });
            // R10 : jamais de suppression physique, y compris par cascade.
            application.HasOne<Activity>().WithMany().HasForeignKey(a => a.ActivityId).OnDelete(DeleteBehavior.Restrict);
            application.HasOne<IdentityUser>().WithMany().HasForeignKey(a => a.ApplicantId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
