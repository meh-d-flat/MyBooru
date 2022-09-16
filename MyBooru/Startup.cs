using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyBooru.Middleware;
using MyBooru.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MyBooru
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpContextAccessor();

            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(Directory.GetCurrentDirectory()).Parent)
                .SetApplicationName("MyBooru");

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options => 
                {
                    options.LoginPath = "/api/user/signin";
                    options.AccessDeniedPath = "/api/user/details";
                    options.Cookie.Name = "SESSION";
                    //options.SlidingExpiration = true;
                    options.Events.OnRedirectToLogin = context =>
                    {
                        context.Response.StatusCode = 401;
                        return Task.CompletedTask;
                    };
                    options.Events.OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = 403;
                        context.Response.Redirect("/api/user/details");
                        return Task.CompletedTask;
                    };
                });


            services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
                .Configure<ITicketStore>((options, store) => options.SessionStore = store);

            services.AddAuthorization();

            services.AddCors(options =>
            {
                options.AddDefaultPolicy(
                    policy =>
                    {
                        policy.WithOrigins(Configuration["ApiConsumerAddressPort"])
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials();
                    });
            });
            services.AddControllers();
            services.AddSingleton<ITicketStore, SessionTicketStore>();
            services.AddSingleton<LimitService>();
            services.AddTransient<Contracts.ICheckService, CheckService>();
            services.AddTransient<UploadService>();
            services.AddTransient<DownloadService>();
            services.AddTransient<RemoveService>();
            services.AddTransient<Contracts.ITagsService, TagsService>();
            services.AddTransient<TagsService>();
            services.AddTransient<UserService>();
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseFileServer(new FileServerOptions
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "Files")),
                RequestPath = "/Files"
            });

            app.UseRouting();

            app.UseCors();

            app.UseAuthentication();
            app.UseAuthorization();
            
            app.UseMiddleware<RequestLimitMiddleware>();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
