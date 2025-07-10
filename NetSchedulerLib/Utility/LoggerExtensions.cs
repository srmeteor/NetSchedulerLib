using System.Runtime.CompilerServices;
using Serilog;

namespace NetSchedulerLib.Utility;

public static class LoggerExtensions
{
    /// <summary>
    /// Gets an ILogger scoped to the given class type with an optional custom name.
    /// </summary>
    /// <typeparam name="T">The class type for which the logger is scoped.</typeparam>
    /// <param name="customName">Custom name to override the default class name.</param>
    /// <returns>A Serilog ILogger instance scoped to the class type.</returns>
    public static ILogger GetLoggerFor<T>(string? customName = null)
    {
        return Log.ForContext("ClassName", customName ?? typeof(T).Name);
    }




}