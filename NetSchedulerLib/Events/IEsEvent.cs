using NetSchedulerLib.Common;
using NetSchedulerLib.Profiles;

namespace NetSchedulerLib.Events;

public interface IEsEvent
{
    /// <summary>
    /// Event Callback
    /// </summary>
    event Action<IEsEvent> OnEventFired;
    
    /// <summary>
    /// Will be automatically changed after sorting events 
    /// </summary>
    uint Id { get; set; }
    
    /// <summary>
    /// Unique Event Name per Profile
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Optional Event Description
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Automatically generated describing Event Recurrence
    /// </summary>
    string RecDescription { get; }

    /// <summary>
    /// Parent Profile
    /// </summary>
    IEsProfile Profile { get; }
    
    /// <summary>
    /// fNext Firing TimeDate
    /// </summary>
    DateTime TargetTime { get; }
    
    /// <summary>
    /// This property is auto-updated on Event-Fire
    /// </summary>
    DateTime? LastFired { get; }
    
    /// <summary>
    /// next event target time in format HH:mm
    /// Compatibility with Old event config
    /// </summary>
    string? Time { get; }
    
    /// <summary>
    /// next event target date in format MM/dd/yyyy
    /// Compatibility with Old event config
    /// </summary>
    string? Date { get; }
    
    EEventState EventState { get; }
    
    EEventType EventType { get; }

    ERecurrence Recurrence { get; }
    
    

    /// <summary>
    /// For EveryNth... Recurrencies.
    /// Default is 1, so EveryNthDay, with rate 2 is equivalent to Every second day
    /// </summary>
    uint Rate { get; }

    /// <summary>
    /// For EveryNthWeek Recurrence represents days of the week to be fired on,
    /// For EveryNthMonth Recurrence represents Month dates to be fired on
    /// </summary>
    int AdditionalRate { get; }
    
    /// <summary>
    /// Represents Astro Event Offset, Sunrise:+75 => 1 Hour and 15 mins after Sunrise
    /// </summary>
    string AstroOffset { get; }

    /// <summary>
    /// Enables the event, changing its state to active and allowing it to fire as per its configuration.
    /// </summary>
    /// <returns>
    /// A boolean value indicating whether the event was successfully enabled.
    /// </returns>
    bool Enable();

    /// <summary>
    /// Disables the event, changing its state to inactive and preventing it from firing.
    /// </summary>
    /// <returns>
    /// A boolean value indicating whether the event was successfully disabled.
    /// </returns>
    bool Disable();

    void Dispose();

    void AddActions(List<string> actions);
    void RemoveActions(List<string>? actions);
    List<string> GetActions();
    void AddAction(string action);
    void RemoveAction(string? action);
    void ClearActions();
    void SetActions(List<string> actions);
    bool HasActions();

    bool HasAction(string action, StringComparison comparison);
    void ExecuteActions(Action<string, object?>? actionCallback);
}

