using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Onygo.Api.Infrastructure.Persistence;

public class OnygoDbContext(DbContextOptions<OnygoDbContext> options) : IdentityDbContext<IdentityUser>(options);
