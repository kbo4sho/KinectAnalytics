using Microsoft.Kinect;
using Microsoft.Kinect.Face;
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
using KinectAnalytics.Models;


namespace KinectAnalytics
{
    public class PeopleTracker
    {
        static private ILog log = LogManager.GetLogger(typeof(PeopleTracker));

        KinectSensor sensor;
        // The face frame source
        FaceFrameSource faceFrameSource = null;

        //// The face frame reader
        MultiSourceFrameReader multiReader = null;
        
        private Dictionary<ulong, TrackedPerson> trackedPeople;
        private Dictionary<ulong, IDisposable> rightHandSubscriptions;
        private Dictionary<ulong, IDisposable> faceSubscriptions;

        IDisposable bodyFrameForFaceSubscription;

        public PeopleTracker()
        {
            log.Info("People Tracker Started");
            try
            {
                this.sensor = KinectSensor.GetDefault();
                this.sensor.IsAvailableChanged += sensor_IsAvailableChanged;
                this.sensor.Open();
                this.Start();
            }
            catch(Exception e)
            {
                log.Error(e.Message);
            }
        }

        void sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
           if(e.IsAvailable)
           {
               log.Info(string.Format("Sensor available {0}", DateTime.UtcNow));
           }
           else
           {
               log.Info(string.Format("Sensor unavailable {0}", DateTime.UtcNow));
           }
        }

        public void Start()
        {
            log.Info("People Tracker Started");

            this.rightHandSubscriptions = new Dictionary<ulong, IDisposable>();
            this.faceSubscriptions = new Dictionary<ulong, IDisposable>();
            this.trackedPeople = new Dictionary<ulong, TrackedPerson>();

            Body[] bodies = null;
            var bodyReader = this.sensor.BodyFrameSource.OpenReader();
            var bodyFrameObservable = this.sensor
                                          .BodyFrameArrivedObservable(bodyReader)
                                          .SelectBodies(bodies);

            bodyFrameForFaceSubscription = bodyFrameObservable.Subscribe(OnBodyFrameForFace);

            faceFrameSource = new FaceFrameSource(sensor,
                       0,
                       FaceFrameFeatures.FaceEngagement |
                       FaceFrameFeatures.Happy |
                       FaceFrameFeatures.Glasses);

            var faceReader = faceFrameSource.OpenReader();

            var faceFramesObservable = Observable.FromEvent<FaceFrameArrivedEventArgs>(
                  ev => { faceReader.FrameArrived += (s, ei) => ev(ei); },
                  ev => { faceReader.FrameArrived -= (s, ei) => ev(ei); }).SelectFaceFrame();

            sensor.SceneChanges()
                  .Subscribe(_ =>
                  {
                      var trackingId = _.SceneChangedType.TrackingId;

                      if (_.SceneChangedType is PersonEnteredScene)
                      {
                          log.Info(string.Format("Person {0} entered scene", trackingId));
                          TrackedPerson person = new TrackedPerson() { TrackingId = trackingId, EnteredScene = DateTime.UtcNow };
                          trackedPeople.Add(trackingId, person);
                          rightHandSubscriptions.Add(trackingId, SubscribeToHandsRaised(person, bodyFrameObservable));
                          faceSubscriptions.Add(trackingId, SubscribeToFace(person, faceFramesObservable));
                      }
                      else if (_.SceneChangedType is PersonLeftScene)
                      {
                          var person = trackedPeople[trackingId];
                          person.LeftScene = DateTime.UtcNow;
                          person.TotalInScene = person.LeftScene - person.EnteredScene;
                          person.Engaged = person.RightHandRaised && person.LeftHandRaised;

                          log.Info(string.Format("Person {0} left the scene {1} hands raised:{2}", 
                                                 trackingId,
                                                 person.TotalInScene,
                                                 person.Engaged));

                          trackedPeople.Remove(trackingId);

                          var handSubscription = rightHandSubscriptions[trackingId];
                          rightHandSubscriptions.Remove(trackingId);
                          handSubscription.Dispose();

                          var faceSubscription = faceSubscriptions[trackingId];
                          faceSubscriptions.Remove(trackingId);
                          faceSubscription.Dispose();

                          LogTrackedPerson(person);
                      }
                  });
        }

        private void OnBodyFrameForFace(Body[] bodies)
        {
            // Set the neast body to be reporting face ananlaytics

            var body = bodies
                       .Where(b => b.IsTracked)
                       .OrderBy(b => b.Joints[JointType.Head]
                       .Position.Z)
                       .FirstOrDefault();

            if (body != null)
            {
                faceFrameSource.TrackingId = body.TrackingId;
            }


            // Cycle through bodies

            //if (trackedPeople != null)
            //{
            //    var tackedCount = trackedPeople.Count;
            //    var orderTrackedPeople = trackedPeople.OrderBy(p => p.Value.EnteredScene).Select(e => e.Value.TrackingId);
            //    if (tackedCount > 0)
            //    {
            //        var enumerator = orderTrackedPeople.SkipWhile(k => k != faceFrameSource.TrackingId).Skip(1).FirstOrDefault();

            //        if (enumerator > 0)
            //        {
            //            faceFrameSource.TrackingId = enumerator;
            //        }
            //        else
            //        {
            //            faceFrameSource.TrackingId = orderTrackedPeople.First();
            //        }
                    
            //    }
            //}

        }

        private IDisposable SubscribeToHandsRaised(TrackedPerson person, IObservable<Body[]> bodyFrameObservable)
        {
            var handsSubscription = bodyFrameObservable.SelectTracked(person.TrackingId)
                                                       .Subscribe(body =>
                                                       {
                                                           var WristRight = body.Joints[JointType.WristRight];
                                                           var ElbowRight = body.Joints[JointType.ElbowRight];

                                                           if (WristRight.Position.Y > ElbowRight.Position.Y)
                                                           {
                                                               
                                                               person.RightHandRaised = true;
                                                           }

                                                           var WristLeft = body.Joints[JointType.WristLeft];
                                                           var ElbowLeft = body.Joints[JointType.ElbowLeft];

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

        private IDisposable SubscribeToFace(TrackedPerson person, IObservable<FaceFrameResult> faceFrameObservable)
        {
            var faceSubscription = faceFrameObservable
                                   .Where(t => t.TrackingId == person.TrackingId)
                                   .Subscribe(faceFrameResult =>
                                    {
                                            var eyeLeftClosed = faceFrameResult.FaceProperties[FaceProperty.LeftEyeClosed];
                                            var eyeRightClosed = faceFrameResult.FaceProperties[FaceProperty.RightEyeClosed];
                                            var mouthOpen = faceFrameResult.FaceProperties[FaceProperty.MouthOpen];
                                            if (mouthOpen == DetectionResult.Yes)
                                            {
                                                Console.WriteLine("Mouth Open {0}", faceFrameSource.TrackingId);
                                            }
                                    });

            return new CompositeDisposable
            {
                faceSubscription
            };
        }

        private void LogTrackedPerson(TrackedPerson trackedPerson)
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

        private string GetFileNameFromDateTime()
        {
            // Get a different file name for every hour
            return "log//Analytics//Analytics_" + DateTime.UtcNow.ToString("yyyyMMddHH") + ".json";
        }

    }
}
