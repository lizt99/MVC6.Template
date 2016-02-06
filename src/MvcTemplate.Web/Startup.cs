using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Mvc;
using Microsoft.AspNet.Mvc.Filters;
using Microsoft.AspNet.Mvc.ModelBinding;
using Microsoft.AspNet.Routing;
using Microsoft.Data.Entity;
using Microsoft.Framework.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;
using MvcTemplate.Components.Logging;
using MvcTemplate.Components.Mail;
using MvcTemplate.Components.Mvc;
using MvcTemplate.Components.Security;
using MvcTemplate.Controllers;
using MvcTemplate.Data.Core;
using MvcTemplate.Data.Logging;
using MvcTemplate.Data.Migrations;
using MvcTemplate.Services;
using MvcTemplate.Validators;
using NonFactors.Mvc.Grid;
using System;
using System.IO;
using Microsoft.Data.Entity.Infrastructure;

namespace MvcTemplate.Web
{
    public class Startup
    {
        private String ApplicationBasePath { get; }
        private readonly IConfiguration configuration;
        private readonly IApplicationEnvironment applicationEnvironment;
        private readonly IHostingEnvironment hostingEnvironment;

        public Startup(IApplicationEnvironment applicationenv,
            IHostingEnvironment hostenv)
        {
            this.applicationEnvironment = applicationenv;
            this.hostingEnvironment = hostenv;
            ApplicationBasePath = applicationenv.ApplicationBasePath;
            this.configuration = ConfigureConfiguration(applicationEnvironment, hostingEnvironment);
        }
        public void Configure(IApplicationBuilder app)
        {
            RegisterAppServices(app);
            RegisterRoute(app);
 
            SeedData(app);
        }
        public void ConfigureServices(IServiceCollection services)
        {
            RegisterCurrentDependencyResolver(services);
            RegisterLowercaseUrls(services);
            RegisterFilters(services);
            RegisterMvcGrid(services);
            RegisterSession(services);
            RegisterMvc(services);
        }

        public virtual void RegisterCurrentDependencyResolver(IServiceCollection services)
        {
            //services.AddEntityFramework()
            //        .AddSqlServer()
            //        .AddDbContext<Context>(options =>
            //            options.UseSqlServer(this.configuration["Data:DefaultConnection:ConnectionString"]));
            //services.AddTransient<DbContextOptions>(provider=>new DbContextOptions())
            services.AddTransient<DbContext, Context>();
            services.AddTransient<IUnitOfWork, UnitOfWork>();

            services.AddTransient<ILogger, Logger>();
            services.AddTransient<IAuditLogger>(provider =>
                new AuditLogger(provider.GetService<DbContext>(),
                    provider.GetService<IHttpContextAccessor>().HttpContext.User.Identity.Name));

            services.AddTransient<IHasher, BCrypter>();
            services.AddTransient<IMailClient>(provider => new SmtpMailClient("smtp.gmail.com", 587, "lizt99@163.com", "ChangeIt"));

            services.AddTransient<IExceptionFilter, ExceptionFilter>();
            services.AddTransient<IModelMetadataProvider, DisplayNameMetadataProvider>();

            services.AddSingleton<IGlobalizationProvider>(provider =>
                new GlobalizationProvider(Path.Combine(ApplicationBasePath, "Globalization.xml")));
            services.AddSingleton<IAuthorizationProvider>(provider => new AuthorizationProvider(typeof(BaseController).Assembly, provider));

            services.AddTransient<IMvcSiteMapParser, MvcSiteMapParser>();
            services.AddSingleton<IMvcSiteMapProvider>(provider => new MvcSiteMapProvider(
                Path.Combine(ApplicationBasePath, "Mvc.sitemap"), provider.GetService<IMvcSiteMapParser>(), provider.GetService<IAuthorizationProvider>()));

            services.AddTransient<IRoleService, RoleService>();
            services.AddTransient<IAccountService, AccountService>();

            services.AddTransient<IRoleValidator, RoleValidator>();
            services.AddTransient<IAccountValidator, AccountValidator>();

            
        }
        public virtual void RegisterLowercaseUrls(IServiceCollection services)
        {
            services.Configure<RouteOptions>(options => options.LowercaseUrls = true);
        }
        public virtual void RegisterFilters(IServiceCollection services)
        {
            services.Configure<MvcOptions>(options => options.Filters.Add(typeof(ExceptionFilter)));
        }
        public virtual void RegisterMvcGrid(IServiceCollection services)
        {
            services.AddMvcGrid();
        }
        public virtual void RegisterSession(IServiceCollection services)
        {
            services.AddCaching();
            services.AddSession();
        }
        public virtual void RegisterMvc(IServiceCollection services)
        {
            services
                .AddMvc()
                .AddMvcOptions(options => options.ModelBinders.Insert(0, new TrimmingModelBinder()))
                .AddRazorOptions(options => options.ViewLocationExpanders.Add(new ViewLocationExpander()));
        }

        public virtual void RegisterAppServices(IApplicationBuilder app)
        {
            app.UseCookieAuthentication(options =>
            {
                options.LoginPath = "/auth/login";
                options.AutomaticChallenge = true;
                options.AutomaticAuthenticate = true;
                options.AuthenticationScheme = "Cookies";
            });
            app.UseDeveloperExceptionPage();
            app.UseStaticFiles();
            app.UseSession();
        }
        public virtual void RegisterRoute(IApplicationBuilder app)
        {
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    "DefaultMultilingualArea",
                    "{language}/{area:exists}/{controller}/{action}/{id?}",
                    new { controller = "Home", action = "Index" },
                    new { language = "lt" });

                routes.MapRoute(
                    "DefaultArea",
                    "{area:exists}/{controller}/{action}/{id?}",
                    new { language = "en", controller = "Home", action = "Index" },
                    new { language = "en" });

                routes.MapRoute(
                    "DefaultMultilingual",
                    "{language}/{controller}/{action}/{id?}",
                    new { controller = "Home", action = "Index" },
                    new { language = "lt" });

                routes.MapRoute(
                    "Default",
                    "{controller}/{action}/{id?}",
                    new { language = "en", controller = "Home", action = "Index" },
                    new { language = "en" });
            });
        }

        public virtual void SeedData(IApplicationBuilder app)
        {
            using (Configuration configuration = new Configuration(app.ApplicationServices.GetService<DbContext>()))
                configuration.Seed();
        }


        /// <summary>
        /// Creates and configures the application configuration, where key value pair settings are stored. See
        /// http://docs.asp.net/en/latest/fundamentals/configuration.html
        /// http://weblog.west-wind.com/posts/2015/Jun/03/Strongly-typed-AppSettings-Configuration-in-ASPNET-5
        /// </summary>
        /// <param name="applicationEnvironment">The location the application is running in</param>
        /// <param name="hostingEnvironment">The environment the application is running under. This can be Development, 
        /// Staging or Production by default.</param>
        /// <returns>A collection of key value pair settings.</returns>
        private IConfiguration ConfigureConfiguration(
            IApplicationEnvironment applicationEnvironment,
            IHostingEnvironment hostingEnvironment)
        {
            ConfigurationBuilder configurationBuilder = new ConfigurationBuilder(
                applicationEnvironment.ApplicationBasePath);

            // Add configuration from the config.json file.
            configurationBuilder.AddJsonFile("config.json");

            // Add configuration from an optional config.development.json, config.staging.json or 
            // config.production.json file, depending on the environment. These settings override the ones in the 
            // config.json file.
            configurationBuilder.AddJsonFile($"config.{hostingEnvironment.EnvironmentName}.json", optional: true);

            // This reads the configuration keys from the secret store. This allows you to store connection strings
            // and other sensitive settings, so you don't have to check them into your source control provider. See 
            // http://go.microsoft.com/fwlink/?LinkID=532709 and
            // http://docs.asp.net/en/latest/security/app-secrets.html
            //configurationBuilder.AddUserSecrets();

            // Add configuration specific to the Development, Staging or Production environments. This config can 
            // be stored on the machine being deployed to or if you are using Azure, in the cloud. These settings 
            // override the ones in all of the above config files.
            // Note: To set environment variables for debugging navigate to:
            // Project Properties -> Debug Tab -> Environment Variables
            // Note: To get environment variables for the machine use the following command in PowerShell:
            // $env:[VARIABLE_NAME]
            // Note: To set environment variables for the machine use the following command in PowerShell:
            // $env:[VARIABLE_NAME]="[VARIABLE_VALUE]"
            // Note: Environment variables use a colon separator e.g. You can override the site title by creating a 
            // variable named AppSettings:SiteTitle. See 
            // http://docs.asp.net/en/latest/security/app-secrets.html
            configurationBuilder.AddEnvironmentVariables();

            return configurationBuilder.Build();
        }

    }
}
