using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LibraryAPI.Model
{
    public class GUIMemberContext : DbContext
    {
        public GUIMemberContext(DbContextOptions<GUIMemberContext> options)
            : base(options)
        {
            Database.EnsureCreated();
        }
        public DbSet<GUIMember> Members { get; set; }
    }
}
