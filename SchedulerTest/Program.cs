using NetSchedulerLib;
using NetSchedulerLib.Common;
using NetSchedulerLib.Events;
using NetSchedulerLib.Utility;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using LoggerExtensions = NetSchedulerLib.Utility.LoggerExtensions;
using System.Reflection;
using SchedulerTest;
using Serilog.Formatting.Display;
using Serilog.Sinks.PeriodicBatching;

#region Configure Serilog

// Configure batching behavior
var batchingOptions = new PeriodicBatchingSinkOptions
{
    BatchSizeLimit = 500, // Maximum logs in a batch
    Period = TimeSpan.FromMinutes(10), // Write to the file every 10 minutes
    QueueLimit = 10000 // Maximum number of logs in the queue
};

// Define the text-based log format
var textFormatter = new MessageTemplateTextFormatter(
    "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{ClassName}] {MethodName:lj} {Message:lj}{NewLine}{Exception}",
    null
);


// Logging level switch for runtime log level adjustment
var generalLevelSwitch = new LoggingLevelSwitch(initialMinimumLevel: LogEventLevel.Debug);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .MinimumLevel.ControlledBy(generalLevelSwitch)
    .WriteTo.Console(
        outputTemplate: 
        "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{ClassName}] {MethodName:lj} {Message:lj}{NewLine}{Exception}"
    )
    .WriteTo.Sink(new PeriodicBatchingSink(
        new FileBatchingSink("Logs/log-.log", textFormatter), // Use text format and dynamic file names
        batchingOptions
    ))
    .CreateLogger();

#endregion

Log.Information("Application started...");

var logg = LoggerExtensions.GetLoggerFor<Program>();

try
{
    logg.Information("Application Starting Up!");

    logg.Information("Checking for existing profiles...");

    // Check if there are existing profiles in the ES folder.
    var workingFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "ES");

    if (!Directory.Exists(workingFolderPath))
    {
        Directory.CreateDirectory(workingFolderPath); // Create the directory if it doesn't exist
    }

    // Check if there are existing profiles in the ES folder.
    var existingProfiles = Directory.GetFiles(workingFolderPath, "*Profile.json");

    // Check if there are existing profiles
    if (existingProfiles.Length == 0)
    {
        logg.Information("No existing profiles found. Creating Test-Profile...");

        // Ensure ES directory exists in the working folder.
        workingFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "ES");
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

    // Initialize EventScheduler with Belgrade RS location
    var scheduler = new EventScheduler("ES/", 44.8125, 20.4612);
    // Monitor Memory usage
    MemoryMonitor.StartMonitoring();
    
    // Attach event handler to monitor events
    scheduler.OnEventFired += SchedulerOnOnEventFired;

    void SchedulerOnOnEventFired(IEsEvent esEvent)
    {
        logg.Information(
            $" *** EventScheduler: *** {DateTime.Now} ***> Profile: '{esEvent.Profile.Name}' {esEvent.EventType.ToString()}: '{esEvent.Name}' fired.\n" +
            $"     Actions: {(esEvent.HasActions()
                ? $"{string.Join(", ", esEvent.GetActions())}"
                : $"No actions defined.")}");
        esEvent.ExecuteActions(CreateAction);
    }

    // Start Scheduler
    await scheduler.InitializeAsync();

    // Attach to application exit for cleanup
    AppDomain.CurrentDomain.ProcessExit += (_, _) =>
    {
        scheduler.OnEventFired -= SchedulerOnOnEventFired;
        scheduler.Dispose(); // Dispose scheduler gracefully
        Log.Information("Application stopped...");
        Log.CloseAndFlush(); // Ensure logs are flushed and released
    };

    // Attach to CTRL+C for cleanup
    Console.CancelKeyPress += (sender, e) =>
    {
        e.Cancel = true; // Prevent immediate termination
        Log.Information("CTRL+C detected. Stopping the application...");
        scheduler.Dispose(); // Cleanup resources
        Log.Information("Application stopped by CTRL+C. Exiting...");
        Log.CloseAndFlush(); // Ensure logs are flushed and released
        Environment.Exit(0); // Exit after cleanup
    };

    // Show Initialized Profiles with events
    logg.Information("Scheduler initialized. Profiles:");
    var profiles = scheduler.GetProfiles();
    foreach (var profile in profiles)
    {
        logg.Information($"{profile.Name} => Events ({profile.GetEvents().Count})");
        var events = profile.GetEvents();
        foreach (var ev in events)
        {
            logg.Information($"{ev.EventType} '{ev.Name}' ({ev.RecDescription}) => {ev.TargetTime}" +
                             $"{(ev.EventState == EEventState.Disabled ? " [DISABLED]" : "")}");
        }

        // Add Some test actions to events
        if (profile.Name != "Test") continue;
        logg.Information("Adding actions to every 10 minutes events...");
        var evList = profile?.GetEvents();
        var everyTenMinEvents = evList?.FindAll(e => e.Name.Contains("Every 10 Minutes"));
        if (everyTenMinEvents is { Count: > 0 })
        {
            logg.Information($"Try Adding actions to {everyTenMinEvents.Count} events...");
            everyTenMinEvents.ForEach(e => { e.AddActions(["Test1Action", "Test2Action", "Test3Action"]); });
        }
    }

    logg.Information("Press CTRL+C to stop. Or type 'exit' to quit.");
    
    #region Example: Adding Profile and Events

    // if (scheduler.AddProfile("Test2", "Test Profile 2"))
    // {
    //     var test2 = scheduler.GetProfile("Test2");
    //     test2?.AddEvent(new Models.EsEventCfg
    //     {
    //         Name = "Event1",
    //         EventRecurrence = "EveryNthDay",
    //         EventType = nameof(EEventType.AbsoluteEvent),
    //         EventState = nameof(EEventState.Enabled),
    //         EventRecurrenceRate = 1,
    //         EventRecAdditionalRate = 0,
    //         Time = "12:00",
    //         Date = "07/15/2025"
    //     }, overwrite: true);
    //     test2?.AddEvent(new Models.EsEventCfg
    //     {
    //         Name = "Event2",
    //         EventRecurrence = "EveryNthDay",
    //         EventType = nameof(EEventType.AstronomicalEvent),
    //         EventState = nameof(EEventState.Enabled),
    //         EventRecurrenceRate = 1,
    //         EventRecAdditionalRate = 0,
    //         AstroOffset = $"{nameof(EAstroEvent.Sunrise)}:-10",
    //         TargetTime = "2025-07-15T00:00:00+02:00",
    //     }, overwrite: true);
    // }
    
    
    #endregion

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
                    Console.WriteLine(
                        "Invalid log level. Valid levels are: Verbose, Debug, Information, Warning, Error, Fatal.");
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