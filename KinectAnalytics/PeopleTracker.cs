using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive;
using Kinect.ReactiveV2;
using log4net;

namespace KinectAnalytics
{
    public class PeopleTracker
    {
        static private ILog log = LogManager.GetLogger(typeof(PeopleTracker));

        KinectSensor sensor;
        private Dictionary<ulong, TrackedPerson> personSubscriptions;
        private Dictionary<ulong, IDisposable> rightHandSubscriptions;

        public PeopleTracker()
        {
            log.Info("Starting");

            sensor = KinectSensor.GetDefault();
            sensor.Open();

            if (sensor.IsOpen)
            {
                Console.WriteLine("Sensor opened");
                this.Start();
                rightHandSubscriptions = new Dictionary<ulong, IDisposable>();
                personSubscriptions = new Dictionary<ulong, TrackedPerson>();
            }
            else
            {
                Console.WriteLine("Failed to open sensor");
            }
        }


        public void Start()
        {
            Console.WriteLine("People Tracker Started");

            Body[] bodies = null;
            var reader = this.sensor.BodyFrameSource.OpenReader();
            var bodyFrameObservable = this.sensor
                                          .BodyFrameArrivedObservable(reader)
                                          .SelectBodies(bodies);

            sensor.SceneChanges()
                  .Subscribe(_ =>
                  {
                      var trackingId = _.SceneChangedType.TrackingId;

                      if (_.SceneChangedType is PersonEnteredScene)
                      {
                          Console.WriteLine("Person {0} entered scene", trackingId);
                          TrackedPerson person = new TrackedPerson() { TrackingId = trackingId, TimeEnteredScene = DateTime.UtcNow };
                          personSubscriptions.Add(trackingId, person);
                          rightHandSubscriptions.Add(trackingId, SubscribeToHandsRaised(person, bodyFrameObservable));
                      }
                      else if (_.SceneChangedType is PersonLeftScene)
                      {
                          var person = personSubscriptions[trackingId];
                          Console.WriteLine("Person {0} left the scene {1} hands raised:{2}",
                                            trackingId,
                                            DateTime.UtcNow - person.TimeEnteredScene,
                                            (person.RightHandRaised && person.LeftHandRaised));
                          personSubscriptions.Remove(trackingId);

                          var subscription = rightHandSubscriptions[trackingId];
                          rightHandSubscriptions.Remove(trackingId);
                          subscription.Dispose();
                      }
                  });
        }

        private IDisposable SubscribeToHandsRaised(TrackedPerson person, IObservable<Body[]> bodyFrameObservable)
        {
            var handsSubscription = bodyFrameObservable.SelectTracked(person.TrackingId)
                                                       .Subscribe(pos =>
                                                       {
                                                            var WristRight = pos.Joints[JointType.WristRight];
                                                            var ElbowRight = pos.Joints[JointType.ElbowRight];

                                                            if (WristRight.Position.Y > ElbowRight.Position.Y)
                                                            {
                                                                person.RightHandRaised = true;
                                                            }

                                                            var WristLeft = pos.Joints[JointType.WristLeft];
                                                            var ElbowLeft = pos.Joints[JointType.ElbowLeft];

                                                            if (WristLeft.Position.Y > ElbowLeft.Position.Y)
                                                            {
                                                                person.LeftHandRaised = true;
                                                            }

                                                            // TODO: Are hands above waist?
                                                            // TODO: Stuff like that

                                                        });

            return new CompositeDisposable
            {
                handsSubscription
            };
        }
    }
}
