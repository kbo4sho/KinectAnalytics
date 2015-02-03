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
using Newtonsoft.Json;
using log4net;
using Newtonsoft.Json.Linq;
using System.IO;

namespace KinectAnalytics
{
    public class PeopleTracker
    {
        static private ILog log = LogManager.GetLogger(typeof(PeopleTracker));

        KinectSensor sensor;
        private Dictionary<ulong, TrackedPerson> trackedPeople;
        private Dictionary<ulong, IDisposable> rightHandSubscriptions;

        public PeopleTracker()
        {
            log.Info("People Tracker Started");
            try
            {
                sensor = KinectSensor.GetDefault();
                sensor.Open();
                sensor.IsAvailableChanged += sensor_IsAvailableChanged;
            }
            catch(Exception e)
            {
                log.Info(e.Message);
            }
        }

        void sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
           if(e.IsAvailable)
           {               
               log.Info(string.Format("Sensor available {0}", DateTime.UtcNow));
               Console.WriteLine("Sensor available");
               this.Start();
               rightHandSubscriptions = new Dictionary<ulong, IDisposable>();
               trackedPeople = new Dictionary<ulong, TrackedPerson>();
           }
           else
           {
               log.Info(string.Format("Sensor unavailable {0}", DateTime.UtcNow));
               Console.WriteLine("Sensor unavailable");
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
                          TrackedPerson person = new TrackedPerson() { TrackingId = trackingId, EnteredScene = DateTime.UtcNow };
                          trackedPeople.Add(trackingId, person);
                          rightHandSubscriptions.Add(trackingId, SubscribeToHandsRaised(person, bodyFrameObservable));

                          
                      }
                      else if (_.SceneChangedType is PersonLeftScene)
                      {
                          var person = trackedPeople[trackingId];
                          person.LeftScene = DateTime.UtcNow;
                          person.TotalInScene = person.LeftScene - person.EnteredScene;
                          person.Engaged = person.RightHandRaised && person.LeftHandRaised;

                          Console.WriteLine("Person {0} left the scene {1} hands raised:{2}",
                                            trackingId,
                                            person.TotalInScene,
                                            person.Engaged);

                          trackedPeople.Remove(trackingId);

                          var subscription = rightHandSubscriptions[trackingId];
                          rightHandSubscriptions.Remove(trackingId);

                          subscription.Dispose();

                          LogTrackedPerson(person);
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
                                                        });

            return new CompositeDisposable
            {
                handsSubscription
            };
        }

        private void LogTrackedPerson(TrackedPerson trackedPerson)
        {
            // Get file name
            var analyticsFilePath = GetFileNameFromDateTime();

            List<TrackedPerson> persons;

            if (File.Exists(analyticsFilePath))
            {
                var jsonString = File.ReadAllText(analyticsFilePath, Encoding.UTF8);
                persons = JsonConvert.DeserializeObject<List<TrackedPerson>>(jsonString);
            }
            else
            {
                persons = new List<TrackedPerson>();
            }

            // Add our new person
            persons.Add(trackedPerson);

            string newJson = JsonConvert.SerializeObject(persons.ToArray());

            // Make sure that our directory exisits
            Directory.CreateDirectory("log/Analytics");

            File.WriteAllText(analyticsFilePath, newJson);
        }

        private string GetFileNameFromDateTime()
        {
            return "log//Analytics//Analytics_" + DateTime.UtcNow.ToString("yyyyMMddHH") + ".json";
        }
    }
}
