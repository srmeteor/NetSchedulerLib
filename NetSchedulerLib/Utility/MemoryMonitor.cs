using Serilog;

namespace NetSchedulerLib.Utility;

public class MemoryMonitor
{
    private static readonly ILogger Logger = LoggerExtensions.GetLoggerFor<MemoryMonitor>();

    /// <summary>
    /// Starts monitoring memory usage and logs memory statistics periodically.
    /// This includes total memory usage and garbage collection counts for each generation.
    /// The method operates on a separate thread and logs statistics every 10 minutes.
    /// </summary>
    /// <remarks>
    /// This method continuously runs in the background after being started.
    /// It helps in identifying memory consumption patterns and garbage collection frequency during application execution.
    /// </remarks>
    /// <exception cref="Exception">Logs any exceptions that occur during memory monitoring.</exception>
    public static void StartMonitoring()
    {
        new Thread(() =>
        {
            while (true)
            {
                try
                {
                    long memoryUsed = GC.GetTotalMemory(false);

                    // Log memory usage
                    Logger.Information("Memory Used: {MemoryUsed} MB", memoryUsed / (1024 * 1024));

                    // Log garbage collection counts for each generation
                    Logger.Debug("Gen 0 Collections: {Gen0Count}", GC.CollectionCount(0));
                    Logger.Debug("Gen 1 Collections: {Gen1Count}", GC.CollectionCount(1));
                    Logger.Debug("Gen 2 Collections: {Gen2Count}", GC.CollectionCount(2));

                    Thread.Sleep(10 * 60 * 1000); // Log every 10 mins
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "An error occurred during memory monitoring.");
                }
            }
        }).Start();
    }
}


