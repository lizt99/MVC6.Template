using Microsoft.Data.Entity;
using Microsoft.Extensions.Configuration;
using MvcTemplate.Data.Mapping;
using MvcTemplate.Objects;

namespace MvcTemplate.Data.Core
{
    public class Context : DbContext
    {

        static IConfiguration Configuration { get; set; }

        #region Administration

        protected DbSet<Role> Roles { get; set; }
        protected DbSet<Account> Accounts { get; set; }
        protected DbSet<Permission> Permissions { get; set; }
        protected DbSet<RolePermission> RolePermissions { get; set; }

        #endregion

        #region System

        protected DbSet<Log> Logs { get; set; }
        protected DbSet<AuditLog> AuditLogs { get; set; }

        #endregion

        static Context()
        {
            Configuration = new ConfigurationBuilder().AddJsonFile("config.json").Build();
            ObjectMapper.MapObjects();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            //System.Diagnostics.Debugger.Launch();
            //optionsBuilder.UseSqlServer(@"Data Source=.;Initial Catalog=GXFW_INT;Integrated Security=True;Connect Timeout=15;Encrypt=False;TrustServerCertificate=False");
            optionsBuilder.UseSqlServer(Configuration["Data:DefaultConnection:ConnectionString"]);
        }
    }
}
