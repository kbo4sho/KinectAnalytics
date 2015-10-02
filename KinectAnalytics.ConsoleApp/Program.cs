using KinectAnalytics.Models;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectAnalytics.ConsoleApp
{
    class Program
    {
        static private ILog log = LogManager.GetLogger(typeof(Program));

        static void Main(string[] args)
        {
            var jsonString = File.ReadAllText("config.json", Encoding.UTF8);
            var config = JsonConvert.DeserializeObject<KinectAnalytics.Models.Config>(jsonString);

            var peopleTracker = new PeopleTracker(config);

            peopleTracker.PersonJustLeft += peopleTracker_PersonJustLeft;
            peopleTracker.Info += peopleTracker_Info;
            peopleTracker.Error += peopleTracker_Error;

            Console.ReadKey();
        }

        static void peopleTracker_Error(object sender, EventArgs e)
        {
            Console.WriteLine(((StringEventArgs)e).Message);
        }

        static void peopleTracker_Info(object sender, EventArgs e)
        {
            Console.WriteLine(((StringEventArgs)e).Message);
        }

        static void peopleTracker_PersonJustLeft(object sender, EventArgs e)
        {
            LogTrackedPerson(((TrackedPersonEventArgs)e).TrackedPerson);
        }


        private static void LogTrackedPerson(TrackedPerson trackedPerson)
        {
            // Get file name
            var analyticsFilePath = GetFileNameFromDateTime();

            List<TrackedPerson> persons;

            if (File.Exists(analyticsFilePath))
            {
                // Deserialize our saved analytics
                var jsonString = File.ReadAllText(analyticsFilePath, Encoding.UTF8);
                persons = JsonConvert.DeserializeObject<List<TrackedPerson>>(jsonString);
            }
            else
            {
                // Create new analytics file
                persons = new List<TrackedPerson>();
            }

            // Add our new person
            persons.Add(trackedPerson);

            string newJson = JsonConvert.SerializeObject(persons.ToArray());

            // Make sure that our directory exisits
            Directory.CreateDirectory("log/Analytics");

            File.WriteAllText(analyticsFilePath, newJson);

            log.Info(string.Format("Person Logged {0}", DateTime.UtcNow));
        }

        private static string GetFileNameFromDateTime()
        {
            // Get a different file name for every hour
            return "log//Analytics//Analytics_" + DateTime.UtcNow.ToString("yyyyMMddHH") + ".json";
        }
    }
}   
