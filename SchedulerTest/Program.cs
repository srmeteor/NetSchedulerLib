// See https://aka.ms/new-console-template for more information

using NetSchedulerLib.Common;
using NetSchedulerLib.Events;
using NetSchedulerLib.Profiles;

var esProfile = new EsProfile("Test Profile", "Test Profile Description");

esProfile.AddEvent(new Models.EsEventCfg()
{
    Id = 1,
    Name = "Every 2 Minutes",
    TargetTime = DateTime.Now.AddMinutes(1).ToString("yyyy-MM-ddTHH:mm:sszzz"),
    EventRecurrence = "EveryNthMinute",
    EventRecurrenceRate = 2,
    EventRecAdditionalRate = 0
});
esProfile.AddEvent(new Models.EsEventCfg()
{
    Id = 2,
    Name = "Every Hour",
    TargetTime = DateTime.Now.AddMinutes(1).ToString("yyyy-MM-ddTHH:mm:sszzz"),
    EventRecurrence = "EveryNthHour",
    EventRecurrenceRate = 1,
    EventRecAdditionalRate = 0
});

esProfile.AddEvent(new Models.EsEventCfg()
{
    Id = 3,
    Name = "Every Day",
    TargetTime = DateTime.Now.AddMinutes(1).ToString("yyyy-MM-ddTHH:mm:sszzz"),
    EventRecurrence = "EveryNthDay",
    EventRecurrenceRate = 1,
    EventRecAdditionalRate = 0
});

esProfile.OnProfileEventFired += (sender) =>
{
    Console.WriteLine($"{DateTime.Now} > Event {sender.Name} fired");
    
};

Console.WriteLine("Press any key to exit...");
Console.ReadKey();