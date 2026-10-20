using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Onygo.Api.Profiles;

namespace Onygo.Api.Infrastructure.Persistence;

public class OnygoDbContext(DbContextOptions<OnygoDbContext> options)
    : IdentityDbContext<IdentityUser>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<Profile> Profiles => Set<Profile>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Profile>(profile =>
        {
            profile.HasKey(p => p.UserId);
            profile.Property(p => p.FirstName).HasMaxLength(ProfileRules.MaxFirstNameLength);
            profile.HasOne<IdentityUser>().WithOne().HasForeignKey<Profile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
