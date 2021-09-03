using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LibraryAPI.Model
{
    public class MemberContext : IdentityDbContext
    {
        public virtual DbSet<Member> Members { get; set; }
        public virtual DbSet<RefreshToken> RefreshTokens { get; set; }



        public MemberContext(DbContextOptions<MemberContext> options)
            :base(options)
        {
            Database.EnsureCreated();
        }
        
    }
}
