using System;
using System.Reflection;
using System.Text.Json.Serialization;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Play.Common.HealthChecks;
using Play.Common.Identity;
using Play.Common.Logging;
using Play.Common.MassTransit;
using Play.Common.MongoDB;
using Play.Common.Settings;
using Play.Identity.Contracts;
using Play.Inventory.Contracts;
using Play.Trading.Service.Entities;
using Play.Trading.Service.Exceptions;
using Play.Trading.Service.Settings;
using Play.Trading.Service.SignalR;
using Play.Trading.Service.StateMachines;

namespace Play.Trading.Service
{
    public class Startup
    {
        private const string AllowedOriginSetting = "AllowedOrigin";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Configure Mongo Db Collection and RabbitMQ + Saga
            services.AddMongo()
                    .AddMongoRepository<CatalogItem>("catalogitems")
                    .AddMongoRepository<InventoryItem>("inventoryitems")
                    .AddMongoRepository<ApplicationUser>("users")
                    .AddJwtBearerAuthentication();
            AddMassTransit(services);

            services.AddControllers(options =>
            {
                // Avoid transforming "Async" suffix methods
                options.SuppressAsyncSuffixInActionNames = false;
            })
            .AddJsonOptions(options => options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Play.Trading.Service", Version = "v1" });
            });

            // Register SignalR
            services.AddSingleton<IUserIdProvider, UserIdProvider>()
                    .AddSingleton<MessageHub>()
                    .AddSignalR();

            // Health Checks
            services.AddHealthChecks()
                    .AddMongoDb();
            
            // Seq Logging
            services.AddSeqLogging(Configuration);
            
            // Telemetry Tracing
            services.AddOpenTelemetryTracing(builder =>
            {
                var serviceSettings = Configuration.GetSection(nameof(ServiceSettings)).Get<ServiceSettings>();

                // collect traces from the service and mass transit
                builder.AddSource(serviceSettings.ServiceName)
                    .AddSource("MassTransit")
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName: serviceSettings.ServiceName))
                    .AddHttpClientInstrumentation() // outside traffic
                    .AddAspNetCoreInstrumentation() // core traffic
                    .AddConsoleExporter();
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Play.Trading.Service v1"));

                // CORS for Frontend
                app.UseCors(builder =>
                {
                    builder.WithOrigins(Configuration[AllowedOriginSetting])
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials(); // For SignalR purpose
                });
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            // For Identity
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<MessageHub>("/messagehub"); // SignalR Hub communication endpoint
                endpoints.MapPlayEconomyHealthChecks();  // Map endpoints to HealthChecks
            });
        }

        /// <summary>
        /// This method is different from the Common lib because we're using Sagas.
        /// </summary>
        private void AddMassTransit(IServiceCollection services)
        {
            services.AddMassTransit(configure =>
            {
                configure.UsingPlayEconomyMessageBroker(Configuration, retryConfigurator =>
                {
                    // retry 3 times within a 5 sec time frame.
                    // If exception is thrown then don't retry.
                    retryConfigurator.Interval(3, TimeSpan.FromSeconds(5));
                    retryConfigurator.Ignore(typeof(UnknownItemException));
                });

                // Set up Mongo DB for Saga
                configure.AddConsumers(Assembly.GetEntryAssembly());
                configure.AddSagaStateMachine<PurchaseStateMachine, PurchaseState>(sagaConfigurator =>
                {
                    sagaConfigurator.UseInMemoryOutbox();
                })
                    .MongoDbRepository(r =>
                    {
                        var serviceSettings = Configuration.GetSection(nameof(ServiceSettings))
                                                           .Get<ServiceSettings>();
                        var mongoSettings = Configuration.GetSection(nameof(MongoDbSettings))
                                                           .Get<MongoDbSettings>();

                        r.Connection = mongoSettings.ConnectionString;
                        r.DatabaseName = serviceSettings.ServiceName;
                    });
            });

            var queueSettings = Configuration.GetSection(nameof(QueueSettings)).Get<QueueSettings>();
            
            // Endpoint configuration to send commands
            EndpointConvention.Map<GrantItems>(new Uri(queueSettings.GrantItemsQueueAddress));
            EndpointConvention.Map<DebitGil>(new Uri(queueSettings.DebitGilQueueAddress));
            EndpointConvention.Map<SubtractItems>(new Uri(queueSettings.SubtractItemsQueueAddress));
            
        }
    }
}
