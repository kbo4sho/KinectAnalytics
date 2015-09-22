using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive;
using Kinect.ReactiveV2;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.IO;
using Newtonsoft.Json;

namespace KinectAnalytics
{
    class Program
    {
        static void Main(string[] args)
        {
            var jsonString = File.ReadAllText("config.json", Encoding.UTF8);
            var config = JsonConvert.DeserializeObject<KinectAnalytics.Models.Config>(jsonString);
            
            var peopleTracker = new PeopleTracker();
            Console.ReadKey();
        }
    }

}
