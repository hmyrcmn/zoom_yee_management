

using Microsoft.EntityFrameworkCore;
using Core.Entities.Concrete;

namespace Toplanti.DataAccess.Concrete.EntityFramework.Contexts
{
    public class ToplantiContext:DbContext
    {
        public ToplantiContext()
        {
        }

        public ToplantiContext(DbContextOptions<ToplantiContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            //optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=Toplanti;Trusted_Connection=true");
        }

    }
}
