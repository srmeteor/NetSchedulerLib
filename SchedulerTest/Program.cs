// See https://aka.ms/new-console-template for more information

using NetSchedulerLib;
using NetSchedulerLib.Common;
using NetSchedulerLib.Events;
using NetSchedulerLib.Profiles;

var scheduler = new EventScheduler("ES/", 44.8125, 20.4612);
await scheduler.InitializeAsync();

Console.WriteLine("Press any key to exit...");
Console.ReadKey();