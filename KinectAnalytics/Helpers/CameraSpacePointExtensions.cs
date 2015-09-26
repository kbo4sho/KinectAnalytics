using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectAnalytics.Helpers
{
    public static class CameraSpacePointExtensions
    {
        public static string GetXYZ(this CameraSpacePoint source)
        {
            return source.X + "," + source.Y + "," + source.Z;
        }
    }
}
