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

            services.AddAuthentication("bla.bla")//CookieAuthenticationDefaults.AuthenticationScheme
                .AddCookie("bla.bla", options => 
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


            services.AddOptions<CookieAuthenticationOptions>("bla.bla")
                .Configure<ITicketStore>((options, store) => options.SessionStore = store);

            services.AddAuthorization();

            services.AddCors(options =>
            {
                options.AddDefaultPolicy(
                    policy =>
                    {
                        policy.WithOrigins(Configuration.GetSection("ApiConsumerAddressPort").Get<string[]>())
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials()
                            .SetPreflightMaxAge(TimeSpan.FromMinutes(3));
                    });
            });
            services.AddControllers();
            services.AddSingleton<ITicketStore, SessionTicketStore>();
            services.AddSingleton<LimitService>();
            services.AddTransient<Contracts.ICheckService, CheckService>();
            services.AddTransient<Contracts.IUploadService, UploadService>();
            services.AddTransient<Contracts.IDownloadService, DownloadService>();
            services.AddTransient<Contracts.IRemoveService, RemoveService>();
            services.AddTransient<Contracts.ITagsService, TagsService>();
            services.AddTransient<Contracts.IUserService, UserService>();
            services.AddTransient<Contracts.IQueryService, QueryService>();
            services.AddTransient<Contracts.ICommentService, CommentService>();

            services.AddMemoryCache(o =>
            {
                o.ExpirationScanFrequency = TimeSpan.FromDays(30);
                //o.SizeLimit = 1_000_000_000;//a GB
            });

            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.ApplicationServices.GetRequiredService<Contracts.ICheckService>().DBSetupAsync();

            app.UseHttpsRedirection();

            if (!System.IO.Directory.Exists("Files"))
                System.IO.Directory.CreateDirectory("Files");

            app.UseFileServer(new FileServerOptions
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "Files")),
                RequestPath = "/Files"
            });

            app.UseRouting();

            app.UseCors();

            if (!env.IsDevelopment())
            {
                app.UseMiddleware<OriginRefererCheckMiddleware>();
            }
            app.UseMiddleware<RequestLimitMiddleware>();

            app.UseAuthentication();
            app.UseAuthorization();
            

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
