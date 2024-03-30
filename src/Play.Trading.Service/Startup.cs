using System;
using System.Reflection;
using System.Text.Json.Serialization;
using GreenPipes;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Play.Common.Identity;
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
                // Avoid transforming Async suffix methods (IMPORTANT)
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
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Play.Trading.Service v1"));

                // Allow communication with the frontend
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

            app.UseAuthentication(); // for Identity

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<MessageHub>("/messagehub"); // SignalR Hub communication endpoint
            });
        }

        private void AddMassTransit(IServiceCollection services)
        {
            // NOTE: This version is different from the Common lib b/c we're using Saga.
            services.AddMassTransit(configure =>
            {
                configure.UsingPlayEconomyRabbitMq(retryConfigurator =>
                {
                    // retry 3 times within a 5 sec time frame.
                    // If exception is thrown then don't retry.
                    retryConfigurator.Interval(3, TimeSpan.FromSeconds(5));
                    retryConfigurator.Ignore(typeof(UnknownItemException));
                });
                configure.AddConsumers(Assembly.GetEntryAssembly());

                // Set up Mongo DB for Saga
                configure.AddSagaStateMachine<PurchaseStateMachine, PurchaseState>(sagaConfigurator =>
                {
                    sagaConfigurator.UseInMemoryOutbox();
                })
                .MongoDbRepository(r =>
                {
                    var serviceSettings = Configuration.GetSection(nameof(ServiceSettings)).Get<ServiceSettings>();
                    var mongoSettings = Configuration.GetSection(nameof(MongoDbSettings)).Get<MongoDbSettings>();

                    r.Connection = mongoSettings.ConnectionString;
                    r.DatabaseName = serviceSettings.ServiceName;
                });
            });

            // Endpoint configuration to send commands
            var queueSettings = Configuration.GetSection(nameof(QueueSettings)).Get<QueueSettings>();
            EndpointConvention.Map<GrantItems>(new Uri(queueSettings.GrantItemsQueueAddress));
            EndpointConvention.Map<DebitGil>(new Uri(queueSettings.DebitGilQueueAddress));
            EndpointConvention.Map<SubtractItems>(new Uri(queueSettings.SubtractItemsQueueAddress));

            services.AddMassTransitHostedService();
            services.AddGenericRequestClient(); // Register Request Clients
        }
    }
}
