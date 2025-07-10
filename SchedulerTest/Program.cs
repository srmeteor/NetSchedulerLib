using NetSchedulerLib;
using NetSchedulerLib.Common;
using NetSchedulerLib.Events;
using NetSchedulerLib.Utility;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using LoggerExtensions = NetSchedulerLib.Utility.LoggerExtensions;
using System.Reflection;

// Create individual level switches
var generalLevelSwitch = new LoggingLevelSwitch(LogEventLevel.Debug);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext() // Enables capturing dynamic properties like "ClassName"
    .MinimumLevel.ControlledBy(generalLevelSwitch)
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{ClassName}] {MethodName:lj} {Message:lj}{NewLine}{Exception}"
    )
    .WriteTo.File(
        "Logs/log-.txt",
        restrictedToMinimumLevel: LogEventLevel.Verbose,
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{ClassName}] {MethodName:lj} {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

var logg = LoggerExtensions.GetLoggerFor<Program>();

try
{
    logg.Information("Application Starting Up!");
    
    logg.Information("Checking for existing profiles...");
    var existingProfiles = Directory.GetFiles("ES/", "*Profile.json");
    
    // Check if there are existing profiles
    if (existingProfiles.Length == 0)
    {
        logg.Information("No existing profiles found. Creating Test-Profile...");

        // Ensure ES directory exists in the working folder.
        var workingFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "ES");
        if (!Directory.Exists(workingFolderPath))
        {
            Directory.CreateDirectory(workingFolderPath);
        }

        // Get the content of Test-Profile.json from the embedded resource.
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Test-Profile.json";

        await using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
            {
                throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");
            }
            using (StreamReader reader = new StreamReader(stream))
            {
                var testProfileContent = reader.ReadToEnd();

                // Write content to the target path.
                var targetPath = Path.Combine(workingFolderPath, "Test-Profile.json");
                File.WriteAllText(targetPath, testProfileContent);
            }
        }

        logg.Information("Test-Profile created.");
    }
    else
    {
        logg.Information($"{existingProfiles.Length} Existing profiles found. Skipping creation of Test-Profile.");
    }

    var scheduler = new EventScheduler("ES/", 44.8125, 20.4612);
    MemoryMonitor.StartMonitoring();
    scheduler.OnEventFired += SchedulerOnOnEventFired;

    void SchedulerOnOnEventFired(IEsEvent obj)
    {
        logg.Information(
            $" *** EventScheduler: *** {DateTime.Now} ***> Profile: '{obj.Profile.Name}' {obj.EventType.ToString()}: '{obj.Name}' fired.\n" +
            $"     Actions: {(obj.HasActions() 
                ? $"{string.Join(", ", obj.GetActions())}"
                : $"No actions defined.")}");
        obj.ExecuteActions(CreateAction);
    }

    await scheduler.InitializeAsync();

    // Attach to application exit for cleanup
    AppDomain.CurrentDomain.ProcessExit += (_, _) =>
    {
        scheduler.OnEventFired -= SchedulerOnOnEventFired;
        scheduler.Dispose(); // Dispose scheduler gracefully
        Log.Information("Application stopped...");
    };

    Console.CancelKeyPress += (sender, e) =>
    {
        e.Cancel = true; // Prevent immediate termination
        Log.Information("CTRL+C detected. Stopping the application...");
        scheduler.Dispose(); // Cleanup resources
        Environment.Exit(0); // Exit after cleanup
    };

    logg.Information("Scheduler initialized. Profiles:");
    var profiles = scheduler.GetProfiles();
    foreach (var profile in profiles)
    {
        logg.Information($"{profile.Name} => Events ({profile.Events.Count})");
        var events = profile.GetEvents();
        foreach (var ev in events)
        {
            logg.Information($"{ev.EventType} '{ev.Name}' ({ev.RecDescription}) => {ev.TargetTime}" +
                             $"{(ev.EventState == EEventState.Disabled ? " [DISABLED]" : "")}");
        }

        if (profile.Name != "Test") continue;
        logg.Information("Adding actions to every 10 minutes events...");
        var evList = profile?.GetEvents();
        var everyTenMinEvents = evList?.FindAll(e => e.Name.Contains("Every 10 Minutes"));
        if (everyTenMinEvents is {Count: > 0})
        {
            logg.Information($"Try Adding actions to {everyTenMinEvents.Count} events...");
            everyTenMinEvents.ForEach(e =>
            {
                e.AddActions(["Test1Action", "Test2Action", "Test3Action"]);
            });
        }
    }

    logg.Information("Press CTRL+C to stop. Or type 'exit' to quit.");

    // Main loop to read user input
    while (true)
    {
        Console.WriteLine("\nEnter command (loglevel [level], fatal, error, or exit):");
        var input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
            continue;

        var commandParts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);


        switch (commandParts[0].ToLower())
        {
            case "loglevel":
                if (commandParts.Length > 1 && Enum.TryParse<LogEventLevel>(commandParts[1], true, out var newLevel))
                {
                    generalLevelSwitch.MinimumLevel = newLevel;
                    logg.Information("Log level changed to: {NewLevel}", newLevel);
                }
                else
                {
                    Console.WriteLine("Invalid log level. Valid levels are: Verbose, Debug, Information, Warning, Error, Fatal.");
                }
                break;

            case "fatal":
                logg.Fatal("Simulated Fatal log triggered via user command.");
                break;
            case "error":
                logg.Error("Simulated Error log triggered via user command.");
                break;
            case "exit":
                logg.Information("Exiting...");
                Environment.Exit(0);
                break;
            default:
                Console.WriteLine("Unknown command.");
                break;
        }
    }
}
catch (Exception ex)
{
    logg.Fatal(ex, "The application encountered a fatal error and is shutting down.");
}
finally
{
    // Ensure logs are flushed and released
    Log.CloseAndFlush();
}

void CreateAction(string action, object? sender = null)
{
    string senderInfo = sender?.GetType().Name ?? "null";
    if (sender is IEsEvent esEvent)
    {
        senderInfo = $"{esEvent.Profile.Name} - {esEvent.Name}";
    }
    logg.Information($"Action '{action}' (sender:{senderInfo}) triggered via user callback.");
}