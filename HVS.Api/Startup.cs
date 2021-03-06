﻿using System.IO;
using AutoMapper;
using HVS.Api.Core.Business.Caching;
using HVS.Api.Core.Business.Filters;
using HVS.Api.Core.Business.IoC;
using HVS.Api.Core.Business.Models;
using HVS.Api.Core.Business.Services;
using HVS.Api.Core.Common.Constants;
using HVS.Api.Core.Common.Extensions;
using HVS.Api.Core.DataAccess;
using HVS.Api.Core.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Serialization;
using Serilog;
using Serilog.Extensions.Logging;
using Swashbuckle.AspNetCore.Swagger;
using HVS.Api.Core.DataAccess.Repositories.Base;
using HVS.Api.Core.Common.Helpers;
using HVS.Api.Core.Business.Models.Users;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System;

namespace HVS.Api
{
    public class Startup
    {
        /// <summary>
        /// 
        /// </summary>
        public static IConfigurationRoot Configuration;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="env"></param>
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
              .SetBasePath(env.ContentRootPath)
              .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();


            var logPath = Configuration["AppSettings:LoggingPath"] + "HVS-{Date}-" + System.Environment.MachineName + ".txt";
            Log.Logger = new LoggerConfiguration()
              .MinimumLevel.Warning()
              .WriteTo.RollingFile(logPath, retainedFileCountLimit: 15)
              .CreateLogger();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add service and create Policy with options
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                  builder => builder.SetIsOriginAllowed(host => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    );
            });

            services.AddControllers().AddNewtonsoftJson(options =>
            {
                // Use the default property (Pascal) casing
                options.SerializerSettings.ContractResolver = new DefaultContractResolver();
            });

            services.AddSingleton(Configuration);
            services.Configure<AppSettings>(Configuration.GetSection("AppSettings"));


            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddSerilog(dispose: true);
                loggingBuilder.SetMinimumLevel(LogLevel.Information);
                loggingBuilder.AddFilter<SerilogLoggerProvider>(null, LogLevel.Trace);
            });
            services.AddSingleton<ILoggerProvider, SerilogLoggerProvider>();

            //services.AddAutoMapper();

            //Config Automapper map
            //Mapper.Initialize(config =>
            //{
            //    config.CreateMap<User, UserRegisterModel>().ReverseMap();
            //});

            var conn = Configuration.GetConnectionString("DefaultConnectionString");
            services.AddDbContextPool<HVSNetCoreDbContext>(options => options.UseSqlServer(conn));

            //Register JwtHelper
            services.AddScoped<IJwtHelper, JwtHelper>();

            //Register Repository
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

            //Register Service
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<ISSOAuthService, SSOAuthService>();
            services.AddScoped<IUserService, UserService>();

            //Register MemoryCacheManager
            services.AddScoped<ICacheManager, MemoryCacheManager>();

            // Set Service Provider for IoC Helper
            IoCHelper.SetServiceProvider(services.BuildServiceProvider());

            services.AddMvc(option =>
            {
                option.Filters.Add<HandleExceptionFilterAttribute>();
            });

            // Register the Swagger generator, defining one or more Swagger documents
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "HVS API",
                    Description = "ASP.NET Core API.",
                    TermsOfService = null,
                    Contact = new OpenApiContact { Name = "DINH KHAC HOAI PHUNG", Email = "phungdkh@gmail.com", Url = null },
                });

                c.DescribeAllParametersInCamelCase();
                c.OperationFilter<AccessTokenHeaderParameterOperationFilter>();
            });

            services.AddAuthentication(Microsoft.AspNetCore.Server.IISIntegration.IISDefaults.AuthenticationScheme);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            // global policy - assign here or on each controller
            app.UseCors("CorsPolicy");

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            loggerFactory.AddSerilog();

            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "HVS API V1");
            });
            app.UseRouting();

            // Auto run migration
            RunMigration(app);

            // Initialize Data
            InitDataRole();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="app"></param>
        private void RunMigration(IApplicationBuilder app)
        {
            using (var scope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<HVSNetCoreDbContext>().Database.Migrate();
            }
        }

        private void InitDataRole()
        {
            var roleRepository = IoCHelper.GetInstance<IRepository<Role>>();

            var roles = new[]
            {
                new Role {
                    Id = RoleConstants.SaId,
                    Name = "Super Admin"
                }
            };

            roleRepository.GetDbContext().Roles.AddIfNotExist(x => x.Name, roles);
            roleRepository.GetDbContext().SaveChanges();
        }
    }
}
