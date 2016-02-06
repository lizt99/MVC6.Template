Open a command prompt (Windows Key + R, type cmd, click OK)
Use the cd command to navigate to the project directory
Run dnvm use 1.0.0-rc1-final
Run dnx ef migrations add MyFirstMigration to scaffold a migration to create the initial set of tables for your model.
Run dnx ef database update to apply the new migration to the database. Because your database doesn’t exist yet, it will be created for you before the migration is applied.