

using Microsoft.EntityFrameworkCore;
using Toplanti.Core.Entities.Concrete;
using Microsoft.Extensions.Configuration;
using Toplanti.Core.Utilities.IoC;
using Microsoft.Extensions.DependencyInjection;

namespace Toplanti.DataAccess.Concrete.EntityFramework.Contexts
{
    public class ToplantiContext:DbContext
    {
        private readonly IConfiguration? _configuration;

        public ToplantiContext()
        {
        }

        public ToplantiContext(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public ToplantiContext(DbContextOptions<ToplantiContext> options, IConfiguration configuration) : base(options)
        {
            _configuration = configuration;
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<OperationClaim> OperationClaims { get; set; } = null!;
        public DbSet<UserOperationClaim> UserOperationClaims { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var configuration = _configuration ?? ServiceTool.ServiceProvider?.GetService<IConfiguration>();
                if (configuration != null)
                {
                    optionsBuilder.UseSqlServer(configuration.GetConnectionString("ToplantiContext"));
                }
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique()
                .HasDatabaseName("UX_Users_Email")
                .HasFilter("[Email] IS NOT NULL");

            base.OnModelCreating(modelBuilder);
        }

    }
}
