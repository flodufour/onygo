using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Onygo.Api.Activities;
using Onygo.Api.Profiles;

namespace Onygo.Api.Infrastructure.Persistence;

public class OnygoDbContext(DbContextOptions<OnygoDbContext> options)
    : IdentityDbContext<IdentityUser>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<Profile> Profiles => Set<Profile>();

    public DbSet<Activity> Activities => Set<Activity>();

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
            // Pas de cascade : que deviennent les activités (et leurs candidatures, jamais supprimées — R10)
            // quand un compte est supprimé se décidera avec la suppression de compte.
            activity.HasOne<IdentityUser>().WithMany().HasForeignKey(a => a.OrganizerId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
