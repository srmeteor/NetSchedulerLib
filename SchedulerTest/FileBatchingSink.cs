using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.PeriodicBatching;

namespace SchedulerTest;

/// <summary>
/// Represents a custom logging sink that writes batched log events to dynamically
/// generated file paths based on a provided format and timestamp.
/// </summary>
public class FileBatchingSink : IBatchedLogEventSink
{
    private readonly string _baseFilePath; // Base file path (e.g., "Logs/log-.log")
    private readonly ITextFormatter _formatter;

    /// <summary>
    /// Handles the batching and writing of log events to files. The target file paths are dynamically
    /// generated using the specified base file path format, allowing for organization and rotation of logs.
    /// </summary>
    public FileBatchingSink(string baseFilePath, ITextFormatter formatter)
    {
        _baseFilePath = baseFilePath ?? throw new ArgumentNullException(nameof(baseFilePath));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
    }

    /// <summary>
    /// Processes a batch of log events and writes them to a dynamically generated file path.
    /// The file path is constructed based on a base path and current timestamp.
    /// </summary>
    /// <param name="events">The collection of log events to be written to the log file.</param>
    /// <returns>A task representing the asynchronous operation of writing the batch of log events to a file.</returns>
    public async Task EmitBatchAsync(IEnumerable<LogEvent> events)
    {
        // Generate a dynamic file path by including a timestamp
        var dynamicFilePath = GetDynamicFilePath();

        // Write batch of logs to the dynamically generated file
        await using var streamWriter = new StreamWriter(dynamicFilePath, append: true);
        foreach (var logEvent in events)
        {
            _formatter.Format(logEvent, streamWriter);
        }
        await streamWriter.FlushAsync();
        // Ensure logs are flushed to the file
        

    }

    /// <summary>
    /// Handles the case where the batch of log events is empty.
    /// </summary>
    /// <returns>A task representing the asynchronous operation of handling the empty batch.</returns>
    public Task OnEmptyBatchAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Generates a dynamic file path by appending the current timestamp to a base file path.
    /// The method replaces placeholders in the base path with a formatted date string, enabling
    /// file path organization and distinction based on the log event's timestamp.
    /// </summary>
    /// <returns>The dynamically generated file path, incorporating the current date as part of its name.</returns>
    private string GetDynamicFilePath()
    {
        // Generate a timestamp suffix for the file name (e.g., "-2025-07-11")
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd");

        // Replace the placeholder `-` in the base file path with the timestamp
        var directory = Path.GetDirectoryName(_baseFilePath) ?? ".";
        var fileName = Path.GetFileNameWithoutExtension(_baseFilePath)
                       + "-" + timestamp
                       + Path.GetExtension(_baseFilePath);

        // Return the final dynamic file path
        return Path.Combine(directory, fileName);
    }
}