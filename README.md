# Kinect Analytics
A C# class library that provides a simple interface to expose analytics on people as they are being collected. A Microsoft Kinect V2 is required to collect data.

Here are the items that can currently be tracked. Theses can be changed via config.
* Height (average height of the user in meteres, returns 0 if result indeterminate) 
* Engadged (looking at camera or screen)
3. Happy (did they smile)
4. Position when entered (x,y,z)
5. Position when left (x,y,z)
6. Left hand raised (did left hand get raised while in view of the camera)
7. Right hand raised (did right hand get raised while in view of the camera)
8. Time spent in scene (total time user spent in front of the camera)
