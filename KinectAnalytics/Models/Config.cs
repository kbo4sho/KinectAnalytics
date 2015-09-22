using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectAnalytics.Models
{
    public class Config
    {
        public string Name { get; set; }
        public Track Track { get; set; }
    }

    public class Track
    {
        public bool Height { get; set; }
        public bool Engadged { get; set; }
        public bool Smiled { get; set; }
    }
}
