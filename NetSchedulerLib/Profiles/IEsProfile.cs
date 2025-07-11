using System.Collections.Concurrent;
using NetSchedulerLib.Common;
using NetSchedulerLib.Events;

namespace NetSchedulerLib.Profiles;

public interface IEsProfile
{
    /// <summary>
    /// Gets the name of the profile.
    /// </summary>
    /// <remarks>
    /// This property represents the unique identifier or display name for a profile.
    /// It is immutable after the profile is created and is often utilized in logging
    /// or referencing the profile within the system.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Gets or sets the description of the profile.
    /// </summary>
    /// <remarks>
    /// This property provides a textual explanation of the profile's purpose or details.
    /// It may include information such as intended use, configurations, or notes
    /// to describe the profile's functionality within the system.
    /// </remarks>
    string Description { get; }

    /// <summary>
    /// Gets the event scheduler associated with the profile.
    /// </summary>
    /// <remarks>
    /// This property provides access to the <see cref="EventScheduler"/> instance that owns
    /// and manages the profile. It acts as a linkage between the profile and its underlying
    /// scheduling system, enabling interaction with the scheduler's functionality, such as
    /// calculating event times or managing profile configurations.
    /// </remarks>
    EventScheduler Owner { get; }

    /// <summary>
    /// Gets or sets a value indicating whether there have been changes to the profile.
    /// </summary>
    /// <remarks>
    /// This property reflects any modifications made within the profile, such as enabling, disabling,
    /// adding, or removing events. It can be used to track whether the profile needs to be saved or updated.
    /// </remarks>
    bool Changed { get; set; }

    /// <summary>
    /// Adds a new event to the profile using the specified event configuration.
    /// </summary>
    /// <param name="esEventCfg">The configuration details of the event to be added.</param>
    /// <param name="overwrite">Indicates whether to overwrite an existing event if one with the same name already exists.</param>
    /// <returns>True if the event was successfully added, otherwise false.</returns>
    bool AddEvent(Models.EsEventCfg esEventCfg, bool overwrite);

    /// <summary>
    /// Removes an event by its name from the profile.
    /// </summary>
    /// <param name="eventName">The name of the event to be removed. It must not be null or whitespace.</param>
    /// <returns>True if the event was successfully removed; otherwise, false.</returns>
    bool RemoveEvent(string? eventName);

    /// <summary>
    /// Occurs when an event associated with the profile is fired.
    /// </summary>
    /// <remarks>
    /// This event is triggered whenever an action or condition tied to a specific event
    /// within the profile occurs. Subscribers to this event can execute custom logic
    /// in response to the event firing. The <see cref="IEsEvent"/> parameter provides
    /// details about the specific event that was fired.
    /// </remarks>
    event Action<IEsEvent> OnProfileEventFired;

    /// <summary>
    /// Saves the profile and its associated events to the configuration file asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task SaveAsync();

    /// <summary>
    /// Retrieves a list of all events associated with the profile, sorted by their scheduled target time.
    /// </summary>
    /// <returns>A list of events ordered by their target time.</returns>
    List<IEsEvent> GetEvents();

    /// <summary>
    /// Enables all events in the profile that are currently disabled.
    /// </summary>
    /// <returns>True if all events were successfully enabled; otherwise, false.</returns>
    bool EnableAllEvents();

    /// <summary>
    /// Disables all events associated with the profile.
    /// Attempts to update the status of each enabled event to a disabled state.
    /// </summary>
    /// <returns>True if all events were successfully disabled; otherwise, false.</returns>
    bool DisableAllEvents();

    /// <summary>
    /// Removes all events associated with the profile.
    /// </summary>
    /// <returns>True if all events were successfully removed; otherwise, false.</returns>
    bool RemoveAllEvents();

    /// <summary>
    /// Releases all resources used by the profile and its associated events.
    /// </summary>
    void Dispose();
}