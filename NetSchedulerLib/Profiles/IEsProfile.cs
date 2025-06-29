using System.Collections.Concurrent;
using NetSchedulerLib.Common;
using NetSchedulerLib.Events;

namespace NetSchedulerLib.Profiles;

public interface IEsProfile
{
    string Name { get; }
    string Description { get; }
    
    EventScheduler Owner { get; }
    
    bool Changed { get; set; }
    ConcurrentDictionary<string, IEsEvent> Events { get; }
    bool AddEvent(Models.EsEventCfg esEventCfg);
    bool RemoveEvent(string? eventName);
    event Action<IEsEvent> OnProfileEventFired;
    Task SaveAsync();
    
    void Dispose();
}