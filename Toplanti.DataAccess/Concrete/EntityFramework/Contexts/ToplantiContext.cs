

using Microsoft.EntityFrameworkCore;

namespace DataAccess.EntityFramework.Contexts
{
    public class ToplantiContext:DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=Toplanti;Trusted_Connection=true");
        }

    }
}
