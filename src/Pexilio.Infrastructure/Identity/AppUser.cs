using Microsoft.AspNetCore.Identity;

namespace Pexilio.Infrastructure.Identity;

public sealed class AppUser : IdentityUser<Guid>;

public sealed class AppRole : IdentityRole<Guid>;
