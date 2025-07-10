using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.PeriodicBatching;

namespace SchedulerTest;

public class FileBatchingSink : IBatchedLogEventSink
{
    private readonly string _baseFilePath; // Base file path (e.g., "Logs/log-.log")
    private readonly ITextFormatter _formatter;

    public FileBatchingSink(string baseFilePath, ITextFormatter formatter)
    {
        _baseFilePath = baseFilePath ?? throw new ArgumentNullException(nameof(baseFilePath));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
    }

    public Task EmitBatchAsync(IEnumerable<LogEvent> events)
    {
        // Generate a dynamic file path by including a timestamp
        var dynamicFilePath = GetDynamicFilePath();

        // Write batch of logs to the dynamically generated file
        using (var streamWriter = new StreamWriter(dynamicFilePath, append: true))
        {
            foreach (var logEvent in events)
            {
                _formatter.Format(logEvent, streamWriter);
            }
        }
        return Task.CompletedTask;
    }

    public Task OnEmptyBatchAsync()
    {
        return Task.CompletedTask;
    }

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