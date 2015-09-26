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
        static void Main(string[] args)
        {
            var jsonString = File.ReadAllText("config.json", Encoding.UTF8);
            var config = JsonConvert.DeserializeObject<KinectAnalytics.Models.Config>(jsonString);

            var peopleTracker = new PeopleTracker(config);
            Console.ReadKey();
        }
    }
}   
