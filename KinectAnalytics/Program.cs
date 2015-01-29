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

namespace KinectAnalytics
{
    class Program
    {
        static void Main(string[] args)
        {
            var peopleTracker = new PeopleTracker();
            Console.ReadKey();
        }
    }

    

    public class TrackedPerson 
    {
        public ulong TrackingId;
        public DateTime TimeEnteredScene;
        public bool RightHandRaised;
        public bool LeftHandRaised;
    }

}
