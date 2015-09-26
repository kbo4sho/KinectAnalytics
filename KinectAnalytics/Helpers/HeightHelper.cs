using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectAnalytics.Helpers
{
    public static class HeightHelper
    {
        public static double Height(Body skeleton)
        {
            const double HEAD_DIVERGENCE = 0.16;

            var head = skeleton.Joints[JointType.Head];
            var neck = skeleton.Joints[JointType.Neck];
            var spine = skeleton.Joints[JointType.SpineMid];
            var waist = skeleton.Joints[JointType.SpineBase];
            var hipLeft = skeleton.Joints[JointType.HipLeft];
            var hipRight = skeleton.Joints[JointType.HipRight];
            var kneeLeft = skeleton.Joints[JointType.KneeLeft];
            var kneeRight = skeleton.Joints[JointType.KneeRight];
            var ankleLeft = skeleton.Joints[JointType.AnkleLeft];
            var ankleRight = skeleton.Joints[JointType.AnkleRight];
            var footLeft = skeleton.Joints[JointType.FootLeft];
            var footRight = skeleton.Joints[JointType.FootRight];

            double legLength = Distance(hipLeft, kneeLeft, ankleLeft, footLeft);

            return Distance(head, neck, spine, waist) + legLength + HEAD_DIVERGENCE;
        }

        /// <summary>
        /// Returns the length of the segment defined by the specified joints.
        /// </summary>
        /// <param name="p1">The first joint (start of the segment).</param>
        /// <param name="p2">The second joint (end of the segment).</param>
        /// <returns>The length of the segment in meters.</returns>
        private static double Distance(Joint p1, Joint p2)
        {
            return Math.Sqrt(
                Math.Pow(p1.Position.X - p2.Position.X, 2) +
                Math.Pow(p1.Position.Y - p2.Position.Y, 2) +
                Math.Pow(p1.Position.Z - p2.Position.Z, 2));
        }

        /// <summary>
        /// Returns the length of the segments defined by the specified joints.
        /// </summary>
        /// <param name="joints">A collection of two or more joints.</param>
        /// <returns>The length of all the segments in meters.</returns>
        private static double Distance(params Joint[] joints)
        {
            double length = 0;

            for (int index = 0; index < joints.Length - 1; index++)
            {
                length += Distance(joints[index], joints[index + 1]);
            }

            return length;
        }
    }
}
