using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectAnalytics.Models
{
    public class TrackedPerson
    {
        public ulong TrackingId;
        public DateTime EnteredScene;
        public DateTime LeftScene;
        public TimeSpan TotalInScene;
        public bool RightHandRaised;
        public bool LeftHandRaised;
        public bool Engaged;
        public bool Happy;
        public double Height;
        public double MaxHeight;
        public double MinHeight;
        public string FirstLocation;
        public string LastLocation;

    }
}
