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
using KinectAnalytics.Helpers;


namespace KinectAnalytics
{
    public class PeopleTracker
    {
        KinectSensor kinect;
        FaceFrameSource faceFrameSource = null;

        private Dictionary<ulong, TrackedPerson> trackedPeople;
        private Dictionary<ulong, IDisposable> bodySubscriptions;
        private Dictionary<ulong, IDisposable> faceSubscriptions;

        Body[] bodies;

        Config config;

        public PeopleTracker(Config config)
        {
            this.config = config;
            try
            {
                this.kinect = KinectSensor.GetDefault();
                this.kinect.IsAvailableChanged += sensor_IsAvailableChanged;
                this.kinect.Open();
                this.Start();
            }
            catch (Exception e)
            {
                SendError(e.Message);
            }
        }

        void sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            if (e.IsAvailable)
            {
                SendInfo(string.Format("Sensor available {0}", DateTime.Now));
            }
            else
            {
                SendInfo(string.Format("Sensor unavailable {0}", DateTime.Now));
            }
        }

        public void Start()
        {
            SendInfo("People Tracker Started");

            this.bodySubscriptions = new Dictionary<ulong, IDisposable>();
            this.faceSubscriptions = new Dictionary<ulong, IDisposable>();
            this.trackedPeople = new Dictionary<ulong, TrackedPerson>();

            bodies = new Body[kinect.BodyFrameSource.BodyCount];
            
            var bodyReader = this.kinect.BodyFrameSource.OpenReader();
            var bodyFrameObservable = this.kinect
                                          .BodyFrameArrivedObservable(bodyReader)
                                          .SelectBodies(bodies).Sample(TimeSpan.FromSeconds(1));

            faceFrameSource = new FaceFrameSource(kinect,
                       0,
                       FaceFrameFeatures.FaceEngagement |
                       FaceFrameFeatures.Happy |
                       FaceFrameFeatures.Glasses);

            var faceReader = faceFrameSource.OpenReader();

            var faceFramesObservable = Observable.FromEvent<FaceFrameArrivedEventArgs>(
                  ev => { faceReader.FrameArrived += (s, ei) => ev(ei); },
                  ev => { faceReader.FrameArrived -= (s, ei) => ev(ei); }).SelectFaceFrame();

            kinect.SceneChanges()
                  .Subscribe(_ =>
                  {
                      var trackingId = _.SceneChangedType.TrackingId;

                      if (_.SceneChangedType is PersonEnteredScene)
                      {
                          SendInfo(string.Format("Person {0} entered scene", trackingId));
                          TrackedPerson person = new TrackedPerson() { TrackingId = trackingId, EnteredScene = DateTime.UtcNow };
                          trackedPeople.Add(trackingId, person);
                          bodySubscriptions.Add(trackingId, SubscribeToBody(person, bodyFrameObservable, faceFramesObservable));
                          faceFrameSource.TrackingId = trackingId;
                      }
                      else if (_.SceneChangedType is PersonLeftScene)
                      {
                          var person = trackedPeople[trackingId];
                          person.LeftScene = DateTime.UtcNow;
                          person.TotalInScene = person.LeftScene - person.EnteredScene;

                          SendInfo(string.Format("Person {0} left the scene {1} Engaged:{2} Happy:{3} Height:{4} FirstLocation:{5} LastLocation:{6}",
                                                 trackingId,
                                                 person.TotalInScene,
                                                 person.Engaged,
                                                 person.Happy,
                                                 person.Height,
                                                 person.FirstLocation,
                                                 person.LastLocation));

                          trackedPeople.Remove(trackingId);

                          var subscription = bodySubscriptions[trackingId];
                          bodySubscriptions.Remove(trackingId);
                          subscription.Dispose();

                          SendPerson(person);
                      }
                  });
        }

        private IDisposable SubscribeToBody(TrackedPerson person, IObservable<Body[]> bodyFrameObservable, IObservable<FaceFrameResult> faceFrames)
        {
            var bodySubscription = bodyFrameObservable.SelectTracked(person.TrackingId)
                                                       .Subscribe(body =>
                                                       {
                                                           if(config.Track.Position)
                                                           {
                                                               //Set first location
                                                               var location = body.Joints[JointType.SpineBase].Position.GetXYZ();
                                                               if (string.IsNullOrEmpty(person.FirstLocation))
                                                               {
                                                                   person.FirstLocation = location;
                                                               }

                                                               person.LastLocation = location;
                                                           }

                                                           if (config.Track.RightHandRasied)
                                                           {
                                                               var WristRight = body.Joints[JointType.WristRight];
                                                               var ElbowRight = body.Joints[JointType.ElbowRight];

                                                               if (WristRight.Position.Y > ElbowRight.Position.Y)
                                                               {
                                                                   person.RightHandRaised = true;
                                                               }
                                                           }

                                                           if (config.Track.LeftHandRasied)
                                                           {
                                                               var WristLeft = body.Joints[JointType.WristLeft];
                                                               var ElbowLeft = body.Joints[JointType.ElbowLeft];

                                                               if (WristLeft.Position.Y > ElbowLeft.Position.Y)
                                                               {
                                                                   person.LeftHandRaised = true;
                                                               }
                                                           }

                                                           if (config.Track.Height)
                                                           {
                                                               if (body.Joints[JointType.Head].TrackingState == TrackingState.Tracked &&
                                                                  body.Joints[JointType.FootLeft].TrackingState == TrackingState.Tracked)
                                                               {
                                                                   var height = HeightHelper.Height(body);

                                                                   if (person.MaxHeight < height)
                                                                   {
                                                                       person.MaxHeight = height;
                                                                   }

                                                                   if (person.MinHeight > height || person.MinHeight == 0)
                                                                   {
                                                                       person.MinHeight = height;
                                                                   }

                                                                   person.Height = (person.MaxHeight + person.MinHeight) / 2;
                                                               }
                                                               else
                                                               {
                                                                   person.MaxHeight = 0;
                                                                   person.MinHeight = 0;
                                                               }
                                                           }
                                                       });

            var faceSubscription = SubscribeToFace(person, faceFrames);

            return new CompositeDisposable
            {
                bodySubscription,
                faceSubscription
            };
        }


        private IDisposable SubscribeToFace(TrackedPerson person, IObservable<FaceFrameResult> faceFrameObservable)
        {
            var faceSubscription = faceFrameObservable
                                   .Where(t => t.TrackingId == person.TrackingId)
                                   .Subscribe(faceFrameResult =>
                                    {
                                        if(config.Track.Engadged)
                                        {
                                            var isEngadged = faceFrameResult.FaceProperties[FaceProperty.Engaged] == DetectionResult.Yes;
                                            if (isEngadged)
                                            {
                                                person.Engaged = true;
                                            }
                                        }

                                        if (config.Track.Happy)
                                        {
                                            var isHappy = faceFrameResult.FaceProperties[FaceProperty.Happy] == DetectionResult.Yes;
                                            if (isHappy)
                                            {
                                                person.Happy = true;
                                            }
                                        }

                                        var orderedTrackedBodies = bodies.Where(b => b != null && b.IsTracked).OrderBy(b => b.TrackingId);
                                        var currentIndex = Array.FindIndex(orderedTrackedBodies.ToArray(), b => b.TrackingId == person.TrackingId);

                                        if (currentIndex >= orderedTrackedBodies.Count() - 1)
                                        {
                                            currentIndex = 0;
                                        }
                                        else
                                        {
                                            //iterate index
                                            currentIndex += 1;
                                        }

                                        var newTrackingId = orderedTrackedBodies.Skip(currentIndex).Select(b => b.TrackingId).FirstOrDefault();

                                        if (newTrackingId == 0)
                                        {
                                            newTrackingId = person.TrackingId;
                                        }

                                        faceFrameSource.TrackingId = newTrackingId;
                                    });

            return new CompositeDisposable
            {
                faceSubscription
            };
        }


        private void SendPerson(TrackedPerson trackedPerson)
        {
            OnPersonJustLeft(new TrackedPersonEventArgs(trackedPerson));
        }

        public event EventHandler PersonJustLeft;
        private void OnPersonJustLeft(TrackedPersonEventArgs trackedPerson)
        {
            var handler = this.PersonJustLeft;
            if (handler != null)
            {
                handler(this, trackedPerson);
            }
        }

        private void SendError(string error)
        {
            OnError(new StringEventArgs(error));
        }

        public event EventHandler Error;
        private void OnError(StringEventArgs error)
        {
            var handler = this.Error;
            if (handler != null)
            {
                handler(this, error);
            }
        }

        private void SendInfo(string message)
        {
            OnInfo(new StringEventArgs(message));
        }

        public event EventHandler Info;
        private void OnInfo(StringEventArgs info)
        {
            var handler = this.Info;
            if (handler != null)
            {
                handler(this, info);
            }
        }

    }

    public class StringEventArgs : EventArgs
    {
        public string Message { get; internal set; }
        public StringEventArgs(string message)
        {
            this.Message = message;
        }
    }

    public class TrackedPersonEventArgs : EventArgs
    {
        public TrackedPerson TrackedPerson { get; internal set; }
        public TrackedPersonEventArgs(TrackedPerson trackedPerson)
        {
            this.TrackedPerson = trackedPerson;
        }
    }
}
