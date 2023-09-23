using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Serilog;
using System.Diagnostics;

namespace DbIpToSql
{
    internal static class Program
    {
        public static long AppStartTime { get; private set; }
        private static ServiceProvider? _serviceProvider { get; set; }
        public static ServiceProvider ServiceProvider { get { return _serviceProvider!; } }

        static async Task<int> Main(string[] args)
        {
            AppStartTime = Stopwatch.GetTimestamp();

            SetupLogging();
            SetupDependencyInjection();

            Application app = (Application)ActivatorUtilities.CreateInstance(_serviceProvider!, typeof(Application));

            return await app.StartAsync(args);
        }

        private static void SetupLogging()
        {
            Serilog.Log.Logger = new Serilog.LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(path: Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) + $@"\Log\{typeof(Program).Namespace} Log.txt",
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {Message:lj}{NewLine}{Exception}{NewLine}",
                    rollingInterval: Serilog.RollingInterval.Day,
                    rollOnFileSizeLimit: true)
                .CreateLogger();
        }

        private static void SetupDependencyInjection()
        {
            IServiceCollection serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            IConfiguration config = LoadConfiguration();

            // Bind the configuration using IOptions
            services.AddOptions<AppSettings>().Bind(config.GetSection("AppSettings"));

            // Explicitly register the settings object so IOptions not required in constructors
            services.AddSingleton(resolver => resolver.GetRequiredService<IOptions<AppSettings>>().Value);

            // Add HttpClient for db-ip
            services.AddHttpClient("db-ip")
                .AddTransientHttpErrorPolicy(
                    x => x.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(3, retryAttempt))));
        }

        public static IConfiguration LoadConfiguration()
        {
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory)!)
                .AddJsonFile("appsettings.json", optional: false,
                             reloadOnChange: false);

            return builder.Build();
        }
    }
}