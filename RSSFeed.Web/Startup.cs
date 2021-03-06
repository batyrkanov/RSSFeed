﻿using System;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RSSFeed.Common;
using RSSFeed.Web.Areas.Admin.Data;
using RSSFeed.Web.Areas.Admin.Models;
using RSSFeed.Web.Util;
using RSSFeed.Web.Util.ApiClient;

namespace RSSFeed.Web
{
    public class Startup
    {
        private readonly PlatformInitializer _platformInitializer;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            // create platform initializer
            _platformInitializer = new PlatformInitializer(configuration);

        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            _platformInitializer.ConfigureServices(services);

            var connectionString = Configuration["ConnectionStrings:DefaultConnection"];

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddHangfire(config =>
            {
                config.UseSqlServerStorage(connectionString);
            });

            services.AddDbContext<ApplicationContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("IdentityConnection")));

            services.AddIdentity<User, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationContext>();

            // add signalr service
            services.AddSignalR().AddHubOptions<NewsHub>(option =>
            {
                option.KeepAliveInterval = System.TimeSpan.FromMinutes(10);
                option.HandshakeTimeout = System.TimeSpan.FromMinutes(10);
            });

            services.AddMvc()
            .SetCompatibilityVersion(CompatibilityVersion.Version_2_1)
            .AddJsonOptions(options =>
                {
                    options.SerializerSettings.ReferenceLoopHandling =
                                               Newtonsoft.Json.ReferenceLoopHandling.Ignore;
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHangfireServer();
            app.UseHangfireDashboard("/jobs");

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();
            app.UseAuthentication();
            app.UseSignalR(routes =>
            {
                routes.MapHub<NewsHub>("/GetNews");
            });
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                  name: "areas",
                  template: "{area:exists}/{controller=Channel}/{action=Index}/{id?}"
                );

                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
