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
    /// Gets the target time for the event.
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

    /// <summary>
    /// Represents the state of the event, indicating whether the event is enabled or disabled.
    /// </summary>
    EEventState EventState { get; }

    /// <summary>
    /// Represents the type of the event, categorizing it into predefined types such as AbsoluteEvent or AstronomicalEvent.
    /// </summary>
    EEventType EventType { get; }

    /// <summary>
    /// Represents the recurrence pattern of an event, determining how frequently the event is triggered.
    /// </summary>
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

    /// <summary>
    /// Releases all resources used by the event instance, including any associated timers,
    /// and prevents further operations on the object. Marks the event as disposed.
    /// </summary>
    void Dispose();


    /// <summary>
    /// Adds a collection of actions to the event, associating them with its execution.
    /// </summary>
    /// <param name="actions">
    /// A list of strings representing the names or identifiers of the actions to be added.
    /// If the list is null, no actions are added.
    /// </param>
    void AddActions(List<string> actions);

    
    /// <summary>
    /// Removes a collection of actions from the event, disassociating them from its execution.
    /// </summary>
    /// <param name="actions">
    /// A list of strings representing the names or identifiers of the actions to be removed.
    /// If the list is null, no actions are removed.
    /// </param>
    void RemoveActions(List<string>? actions);

    
    /// <summary>
    /// Retrieves the list of currently configured actions for the event.
    /// </summary>
    /// <returns>
    /// A list of strings representing the names or identifiers of the actions associated with the event.
    /// </returns>
    List<string> GetActions();

    /// <summary>
    /// Adds a single action to the event, associating it with its execution.
    /// </summary>
    /// <param name="action">
    /// A string representing the name or identifier of the action to be added. If the action is null,
    /// empty, or consists only of whitespace, the addition is ignored. Additionally, duplicate
    /// entries are prevented for actions already added to the event.
    /// </param>
    void AddAction(string action);

    /// <summary>
    /// Removes a specific action from the event's list of associated actions.
    /// </summary>
    /// <param name="action">
    /// The name or identifier of the action to be removed. If the value is null,
    /// empty, or consists only of whitespace, no action is removed.
    /// </param>
    void RemoveAction(string? action);

    /// <summary>
    /// Clears all actions associated with the event, resetting the action list and marking the
    /// event's profile as modified.
    /// </summary>
    void ClearActions();

    /// <summary>
    /// Sets the actions associated with the event, overwriting any previously set actions.
    /// Updates the event state to reflect the changes in its associated profile.
    /// </summary>
    /// <param name="actions">
    /// A list of strings representing the new set of actions to associate with the event.
    /// If null, the current actions remain unchanged.
    /// </param>
    void SetActions(List<string>? actions);

    /// <summary>
    /// Determines whether the event has any associated actions.
    /// </summary>
    /// <returns>
    /// True if the event contains one or more actions; otherwise, false.
    /// </returns>
    bool HasActions();

    /// <summary>
    /// Checks whether a specified action exists within the event using the specified string comparison option.
    /// </summary>
    /// <param name="action">The action to check for within the event.</param>
    /// <param name="comparison">The type of string comparison to use when evaluating the action.</param>
    /// <returns>Returns true if the action exists in the event, otherwise false.</returns>
    bool HasAction(string action, StringComparison comparison);

    /// <summary>
    /// Executes all registered actions for the event using the specified callback function.
    /// </summary>
    /// <param name="actionCallback">A delegate that processes each action and its corresponding context during execution.</param>
    void ExecuteActions(Action<string, object?>? actionCallback);
}

