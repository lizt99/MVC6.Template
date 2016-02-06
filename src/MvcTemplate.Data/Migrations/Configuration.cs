using Microsoft.Data.Entity;
using MvcTemplate.Data.Core;
using MvcTemplate.Data.Logging;
using MvcTemplate.Objects;
using System;
using System.Linq;

namespace MvcTemplate.Data.Migrations
{
    public sealed class Configuration : IDisposable
    {
        private IUnitOfWork UnitOfWork { get; }
        private Boolean Disposed { get; set; }

        public Configuration(DbContext context)
        {
            IAuditLogger logger = new AuditLogger(context, "sys_seeder");
            UnitOfWork = new UnitOfWork(context, logger);
        }

        public void Seed()
        {
            SeedPermissions();
            SeedRoles();

            SeedAccounts();
        }

        #region Administration

        private void SeedPermissions()
        {
            Permission[] permissions =
            {
                new Permission { Id = "00000000-0000-0000-0000-000000000001",
                    Area = "Administration", Controller = "Accounts", Action = "Index" },
                new Permission { Id = "00000000-0000-0000-0000-000000000002",
                    Area = "Administration", Controller = "Accounts", Action = "Create" },
                new Permission { Id = "00000000-0000-0000-0000-000000000003",
                    Area = "Administration", Controller = "Accounts", Action = "Details" },
                new Permission { Id = "00000000-0000-0000-0000-000000000004",
                    Area = "Administration", Controller = "Accounts", Action = "Edit" },

                new Permission { Id = "00000000-0000-0000-0000-000000000005",
                    Area = "Administration", Controller = "Roles", Action = "Index" },
                new Permission { Id = "00000000-0000-0000-0000-000000000006",
                    Area = "Administration", Controller = "Roles", Action = "Create" },
                new Permission { Id = "00000000-0000-0000-0000-000000000007",
                    Area = "Administration", Controller = "Roles", Action = "Details" },
                new Permission { Id = "00000000-0000-0000-0000-000000000008",
                    Area = "Administration", Controller = "Roles", Action = "Edit" },
                new Permission { Id = "00000000-0000-0000-0000-000000000009",
                    Area = "Administration", Controller = "Roles", Action = "Delete" }
            };

            Permission[] currentPermissions = UnitOfWork.Select<Permission>().ToArray();
            foreach (Permission permission in currentPermissions)
            {
                if (!permissions.Any(perm => perm.Id == permission.Id))
                {
                    UnitOfWork.DeleteRange(UnitOfWork.Select<RolePermission>().Where(role => role.PermissionId == permission.Id));

                    UnitOfWork.Delete(permission);
                }
            }

            foreach (Permission permission in permissions)
            {
                Permission currentPermission = currentPermissions.SingleOrDefault(perm => perm.Id == permission.Id);
                if (currentPermission == null)
                {
                    UnitOfWork.Insert(permission);
                }
                else
                {
                    currentPermission.Controller = permission.Controller;
                    currentPermission.Action = permission.Action;
                    currentPermission.Area = permission.Area;

                    UnitOfWork.Update(currentPermission);
                }
            }

            UnitOfWork.Commit();
        }

        private void SeedRoles()
        {
            if (!UnitOfWork.Select<Role>().Any(role => role.Title == "Sys_Admin"))
            {
                UnitOfWork.Insert(new Role { Title = "Sys_Admin" });
                UnitOfWork.Commit();
            }

            String adminRoleId = UnitOfWork.Select<Role>().Single(role => role.Title == "Sys_Admin").Id;
            RolePermission[] adminPermissions = UnitOfWork
                .Select<RolePermission>()
                .Where(rolePermission => rolePermission.RoleId == adminRoleId)
                .ToArray();

            foreach (Permission permission in UnitOfWork.Select<Permission>())
                if (!adminPermissions.Any(rolePermission => rolePermission.PermissionId == permission.Id))
                    UnitOfWork.Insert(new RolePermission
                    {
                        RoleId = adminRoleId,
                        PermissionId = permission.Id
                    });

            UnitOfWork.Commit();
        }

        private void SeedAccounts()
        {
            Account[] accounts =
            {
                new Account
                {
                    Username = "admin",
                    Passhash = "$2a$13$yTgLCqGqgH.oHmfboFCjyuVUy5SJ2nlyckPFEZRJQrMTZWN.f1Afq", // Admin123?
                    Email = "admin@admins.com",
                    IsLocked = false,

                    RoleId = UnitOfWork.Select<Role>().Single(role => role.Title == "Sys_Admin").Id
                }
            };

            foreach (Account account in accounts)
            {
                Account dbAccount = UnitOfWork.Select<Account>().FirstOrDefault(model => model.Username == account.Username);
                if (dbAccount != null)
                {
                    dbAccount.IsLocked = account.IsLocked;

                    UnitOfWork.Update(dbAccount);
                }
                else
                {
                    UnitOfWork.Insert(account);
                }
            }

            UnitOfWork.Commit();
        }

        #endregion

        public void Dispose()
        {
            if (Disposed) return;

            UnitOfWork.Dispose();

            Disposed = true;
        }
    }
}
