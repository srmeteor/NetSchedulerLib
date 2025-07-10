using NetSchedulerLib.Common;
using NetSchedulerLib.Profiles;

namespace NetSchedulerLib.Events;

public interface IEsEvent
{
    event Action<IEsEvent> OnEventFired;
    uint Id { get; set; }
    string Name { get; }
    
    string Description { get; }
    
    string RecDescription { get; }

    IEsProfile Profile { get; }
    DateTime TargetTime { get; }
    
    DateTime? LastFired { get; }
    
    string? Time { get; }
    
    string? Date { get; }
    
    EEventState EventState { get; }
    
    EEventType EventType { get; }

    ERecurrence Recurrence { get; }
    
    

    /// <summary>
    /// For EveryNth... Recurrencies.
    /// default is 1, so EveryNthDay, with rate 1 is equivalent to Daily
    /// </summary>
    uint Rate { get; }

    /// <summary>
    /// For EveryNthWeek Recurrence represents days of the week to be fired on,
    /// For EveryNthMonth Recurrence represents Month dates to be fired on
    /// </summary>
    int AdditionalRate { get; }
    
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

