
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace KinectOSC
{
    //using Bespoke.Common.Osc;
    using Ventuz.OSC;
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        // Ugly code, fuck it.
        private int[] SkeletonArray0 = new int[6];
        private int[] SkeletonArray1 = new int[6];


        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap colorBitmap0;
        private WriteableBitmap colorBitmap1;

        /// <summary>
        /// Intermediate storage for the color data received from the camera
        /// </summary>
        private byte[] colorPixels0;
        private byte[] colorPixels1;

        /// <summary>
        /// Store the Draw Color checkbox value for quick access
        /// </summary>
        private bool drawColor = true;

        /// <summary>
        /// Store the OSC On checkbox value for quick access
        /// </summary>
        private bool oscOn = true;

        /// <summary>
        /// Store the Show OSC Data checkbox value for quick access
        /// </summary>
        private bool showOscData = false;

        /// <summary>
        /// If this is true the OSC data will be sent as a single string; else it will be sent as bundles of floats (x, y, z) for each joint
        /// </summary>
        private bool sendOscAsString = false;

        /// <summary>
        /// If this is true the skeleton will be drawn on screen
        /// </summary>
        private bool showSkeleton = false;

        /// <summary>
        /// If this is true then only the skeleton nearest the kinect will be tracked
        /// </summary>
        private bool trackNearestOnly = false;

        /// <summary>
        /// If this is true then the positions of the joints will be sent as percentages of the width and height
        /// </summary>
        private bool sendPositionsAsPercentage = true;

        /// <summary>
        /// If this is true then each variable of each of the joints will be sent separately (each osc element will have one float)
        /// </summary>
        private bool sendAllSeparately = false;

        /// <summary>
        /// Flag to choose to send data specifically in a format that Animata will appreciate
        /// </summary>
        private bool sendAnimataData = true;

        /// <summary>
        /// Scaling factor for Animata data, since Animata takes in OSC data in a stupid, pixel position way
        /// </summary>
        private double animataScaleFactor = 1.0;

        /// <summary>
        /// If this is true then each variable of each of the joints will be sent separately (each osc element will have one float)
        /// </summary>
        private string oscAddress = "";

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor0;
        private KinectSensor sensor1;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup0;
        private DrawingGroup drawingGroup1;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource0;
        private DrawingImage imageSource1;

        // OSC 
        private static UdpWriter oscWriter;
        private static string[] oscArgs = new string[2];

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }


        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Setup osc sender
            oscArgs[0] = "127.0.0.1";
            oscArgs[1] = OscPort.Text;
            oscWriter = new UdpWriter(oscArgs[0], Convert.ToInt32(oscArgs[1]));
            // Initialize Data viewer
            oscViewer.Text = "\nData will be shown here\nwhen there is a skeleton\nbeing tracked.";

            // Create the drawing group we'll use for drawing
            this.drawingGroup0 = new DrawingGroup();
            this.drawingGroup1 = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource0 = new DrawingImage(this.drawingGroup0);
            this.imageSource1 = new DrawingImage(this.drawingGroup1);

            // Display the drawing using our image control
            Image0.Source = this.imageSource0;
            Image1.Source = this.imageSource1;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    //this.sensor0 = potentialSensor;
                    //break;
                    
                    if (null == this.sensor0)
                        this.sensor0 = potentialSensor;
                    else if (null == this.sensor1)
                    {
                        this.sensor1 = potentialSensor;
                        break;
                    }
                }
            }

            if (null != this.sensor0)
            {
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor0.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor0.SkeletonFrameReady += this.Sensor0SkeletonFrameReady;

                // Turn on the color stream to receive color frames
                this.sensor0.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

                // Allocate space to put the pixels we'll receive
                this.colorPixels0 = new byte[this.sensor0.ColorStream.FramePixelDataLength];

                // This is the bitmap we'll display on-screen
                this.colorBitmap0 = new WriteableBitmap(this.sensor0.ColorStream.FrameWidth, this.sensor0.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Set the image we display to point to the bitmap where we'll put the image data
                this.ColorImage0.Source = this.colorBitmap0;

                // Add an event handler to be called whenever there is new color frame data
                this.sensor0.ColorFrameReady += this.Sensor0ColorFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor0.Start();
                }
                catch (IOException)
                {
                    this.sensor0 = null;
                }
            }
            
            if (null != this.sensor1)
            {
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor1.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor1.SkeletonFrameReady += this.Sensor1SkeletonFrameReady;

                // Turn on the color stream to receive color frames
                this.sensor1.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

                // Allocate space to put the pixels we'll receive
                this.colorPixels1 = new byte[this.sensor1.ColorStream.FramePixelDataLength];

                // This is the bitmap we'll display on-screen
                this.colorBitmap1 = new WriteableBitmap(this.sensor1.ColorStream.FrameWidth, this.sensor1.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Set the image we display to point to the bitmap where we'll put the image data
                this.ColorImage1.Source = this.colorBitmap1;

                // Add an event handler to be called whenever there is new color frame data
                this.sensor1.ColorFrameReady += this.Sensor1ColorFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor1.Start();
                }
                catch (IOException)
                {
                    this.sensor1 = null;
                }
            }

            if (null == this.sensor0)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
            if (null == this.sensor1)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor0)
            {
                this.sensor0.Stop();
            }
            if (null != this.sensor1)
            {
                this.sensor1.Stop();
            }
        }

        /// <summary>
        /// Draw the Color Frame to screen
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor0ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            if (drawColor)
            {
                using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
                {
                    if (colorFrame != null)
                    {
                        // Copy the pixel data from the image to a temporary array
                        colorFrame.CopyPixelDataTo(this.colorPixels0);

                        // Write the pixel data into our bitmap
                        this.colorBitmap0.WritePixels(
                            new Int32Rect(0, 0, this.colorBitmap0.PixelWidth, this.colorBitmap0.PixelHeight),
                            this.colorPixels0,
                            this.colorBitmap0.PixelWidth * sizeof(int),
                            0);
                    }
                }
            }
        }
        private void Sensor1ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            if (drawColor)
            {
                using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
                {
                    if (colorFrame != null)
                    {
                        // Copy the pixel data from the image to a temporary array
                        colorFrame.CopyPixelDataTo(this.colorPixels1);

                        // Write the pixel data into our bitmap
                        this.colorBitmap1.WritePixels(
                            new Int32Rect(0, 0, this.colorBitmap1.PixelWidth, this.colorBitmap1.PixelHeight),
                            this.colorPixels1,
                            this.colorBitmap1.PixelWidth * sizeof(int),
                            0);
                    }
                }
            }
        }

        /// <summary>
        /// Set kinect to track the closest skeleton
        /// </summary>
        private void TrackClosestSkeleton(IEnumerable<Skeleton> skeletonData)
        {
            if (this.sensor0 != null && this.sensor0.SkeletonStream != null)
            {
                if (!this.sensor0.SkeletonStream.AppChoosesSkeletons)
                {
                    this.sensor0.SkeletonStream.AppChoosesSkeletons = true; // Ensure app chooses skeletons is set
                }

                float closestDistance = 10000f; // Start with a far enough distance
                int closestID = 0;

                foreach (Skeleton skeleton in skeletonData.Where(s => s.TrackingState != SkeletonTrackingState.NotTracked))
                {
                    if (skeleton.Position.Z < closestDistance)
                    {
                        closestID = skeleton.TrackingId;
                        closestDistance = skeleton.Position.Z;
                    }
                }

                if (closestID > 0)
                {
                    this.sensor0.SkeletonStream.ChooseSkeletons(closestID); // Track this skeleton
                }
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor0SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Boolean noTrackedSkeletons = true;
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame()) {
                if (skeletonFrame != null)  {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                    // Update our skeleton list
                    // First, clear our list of any skeletons that don't exist anymore
                    for (int i = 0; i < SkeletonArray0.Length; i++) {
                        bool skeletonIsActive = false;
                        // is each element in our skeletons array?
                        for (int j = 0; j < skeletons.Length; j++){
                            if (SkeletonArray0[i] == skeletons[j].TrackingId)
                                skeletonIsActive = true;
                        }
                        // If the skeleton isn't active, then clear it out.
                        if (skeletonIsActive == false)
                            SkeletonArray0[i] = 0;
                    }
                    // For each skeleton, check if the ID is already in our skeleton list
                    for (int i = 0; i < skeletons.Length; i++) {
                        bool isInArray = false;
                        for (int j = 0; j < SkeletonArray0.Length; j++){
                            if (skeletons[i].TrackingId == SkeletonArray0[j])
                                isInArray = true;
                        }
                        if (isInArray == false){
                            // If it's not in the array, add it in at the first instace of a zero
                            for (int j = 0; j < SkeletonArray0.Length; j++)
                            {
                                if (SkeletonArray0[j] == 0){
                                    SkeletonArray0[j] = skeletons[i].TrackingId;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if (trackNearestOnly)  {
                TrackClosestSkeleton(skeletons);
            }

            using (DrawingContext dc = this.drawingGroup0.Open())  {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0){
                    var counter = 0;
                    foreach (Skeleton skel in skeletons) {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked) {
                            noTrackedSkeletons = false;
                            if (showSkeleton) {
                                this.DrawBonesAndJoints(skel, dc);
                            }
                            if (oscOn)
                                counter = sendOSC(counter, skel);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly){
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.NotTracked)
                        {
                        }
                    }
                    if (noTrackedSkeletons)  {
                        if (oscOn){
                            // send an osc message that says there are no skeletons to track
                            if (sendOscAsString)  {
                                var elements = new List<OscElement>();
                                var args = "true";
                                elements.Add(new OscElement("/noskeleton", args));
                                oscWriter.Send(new OscBundle(DateTime.Now, elements.ToArray()));
                            }
                        }
                    }
                }
                // prevent drawing outside of our render area
                this.drawingGroup0.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor1SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Boolean noTrackedSkeletons = true;
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                    // Update our skeleton list
                    // First, clear our list of any skeletons that don't exist anymore
                    for (int i = 0; i < SkeletonArray1.Length; i++)
                    {
                        bool skeletonIsActive = false;
                        // is each element in our skeletons array?
                        for (int j = 0; j < skeletons.Length; j++)
                        {
                            if (SkeletonArray1[i] == skeletons[j].TrackingId)
                                skeletonIsActive = true;
                        }
                        // If the skeleton isn't active, then clear it out.
                        if (skeletonIsActive == false)
                            SkeletonArray1[i] = 0;
                    }
                    // For each skeleton, check if the ID is already in our skeleton list
                    for (int i = 0; i < skeletons.Length; i++)
                    {
                        bool isInArray = false;
                        for (int j = 0; j < SkeletonArray1.Length; j++)
                        {
                            if (skeletons[i].TrackingId == SkeletonArray1[j])
                                isInArray = true;
                        }
                        if (isInArray == false)
                        {
                            // If it's not in the array, add it in at the first instace of a zero
                            for (int j = 0; j < SkeletonArray1.Length; j++)
                            {
                                if (SkeletonArray1[j] == 0)
                                {
                                    SkeletonArray1[j] = skeletons[i].TrackingId;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if (trackNearestOnly)
            {
                TrackClosestSkeleton(skeletons);
            }

            using (DrawingContext dc = this.drawingGroup1.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    // Start the second set of skeletons at 10, so these skeletons will be names s10 and on
                    var counter = 10;
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            noTrackedSkeletons = false;
                            if (showSkeleton)
                            {
                                this.DrawBonesAndJoints(skel, dc);
                            }
                            if (oscOn)
                                counter = sendOSC(counter, skel);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.NotTracked)
                        {
                        }
                    }
                    if (noTrackedSkeletons)
                    {
                        if (oscOn)
                        {
                            // send an osc message that says there are no skeletons to track
                            if (sendOscAsString)
                            {
                                var elements = new List<OscElement>();
                                var args = "true";
                                elements.Add(new OscElement("/noskeleton", args));
                                oscWriter.Send(new OscBundle(DateTime.Now, elements.ToArray()));
                            }
                        }
                    }
                }
                // prevent drawing outside of our render area
                this.drawingGroup1.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        private int sendOSC(int counter, Skeleton skel)
        {
            /*foreach (var item in SkeletonArray)
                Console.Write(item.ToString() + ",");
            Console.WriteLine();*/
            var elements = new List<OscElement>();
            var args = "";
            var jointNumber = 0;
            var oscText = "";
            if (sendOscAsString)
                sendOSCAsString(counter, skel, elements, ref args, ref jointNumber, ref oscText);
            else if (sendAllSeparately){
                sendOSCAllSeparately(ref counter, skel, ref oscText);
            }
            else if (sendAnimataData){
                oscText = sendOSCHeadOnly(counter, skel, oscText);
            }
            else
            {
                // This is the default sending method
                oscText = sendOSCInBundles(counter, skel, oscText);
            }
            if (showOscData)
            {
                oscViewer.Text = oscText;
                //showOscData = false;
                //checkBoxShowOscData.IsChecked = false;
            }
            counter++;
            return counter;
        }

        private string sendOSCInBundles(int counter, Skeleton skel, string oscText)
        {
            // joints bundled individually as 3 floats (x, y, z)
            foreach (Joint joint in skel.Joints)
            {
                var jointName = joint.JointType.ToString();
                var jointElement = new List<OscElement>();
                Point jointPosition = SkeletonPointToScreen(joint.Position);
                if (sendPositionsAsPercentage)
                {
                    jointPosition.X = Math.Round(jointPosition.X / 640, 3);
                    jointPosition.Y = Math.Round(jointPosition.Y / 480, 3);
                    // Get the id of the skeleton, as a number of player
                    int normalizedID = -1;
                    for (int i = 0; i < SkeletonArray0.Length; i++)
                    {
                        if (skel.TrackingId == SkeletonArray0[i])
                            normalizedID = i;
                    }
                    jointElement.Add(new OscElement("/skeleton" + normalizedID + "/" + jointName,
                                                    (float)jointPosition.X,
                                                    (float)jointPosition.Y,
                                                    (float)Math.Round(joint.Position.Z, 3)));
                    oscWriter.Send(new OscBundle(DateTime.Now, jointElement.ToArray()));
                    if (showOscData)
                    {
                        oscText += GenerateOscDataDump(counter, jointName, joint.Position,
                                                       jointPosition);
                    }
                }
                else
                {
                    jointElement.Add(new OscElement(
                                         "/skeleton" + counter + "/" + jointName,
                                         (float)Math.Round(joint.Position.X, 4),
                                         (float)Math.Round(joint.Position.Y, 4),
                                         (float)Math.Round(joint.Position.Z, 4)));
                    oscWriter.Send(new OscBundle(DateTime.Now, jointElement.ToArray()));
                    if (showOscData)
                    {
                        oscText += GenerateOscDataDump(counter, jointName, joint.Position);
                    }
                }

            }
            return oscText;
        }

        private string sendOSCHeadOnly(int counter, Skeleton skel, string oscText)
        {
            double playerHeight = skeletonHeight(skel);
            // joints bundled individually as 2 floats (x, y)
            Joint headJoint = skel.Joints[JointType.Head];

            string jointName =  "s" + counter + headJoint.JointType.ToString();
            var jointElement = new List<OscElement>();
                
            // Joint positions are returned in meters, so we'll assume a 2 meter tall person
            // and scale that range to pixels for the animation
            Point origin = new Point(0, 0); // Offset in meters to map our origin to the characters
            int characterHeight = 600;
            int characterWidth = 900;
            double playerHeightRatio = 2 / (playerHeight + .2);
            HeightBox.Text = playerHeight.ToString();
            var jointX = ((headJoint.Position.X - origin.X) / playerHeight) * characterWidth * animataScaleFactor;
            // Divide the Y coordinate by -2 to invert the Y axis, since it's a top left origin in Animata
            var jointY = ((headJoint.Position.Y + origin.Y) / (-1 * playerHeight)) * characterHeight * animataScaleFactor;
            var jointZ = ((headJoint.Position.Z));
            jointX = (float)Math.Round(jointX, 4);
            jointY = (float)Math.Round(jointY, 4);
            jointElement.Add(new OscElement(
                                    "/head",
                                    (float)Math.Round(jointX, 4), (float)Math.Round(jointY, 4), 
                                    (float)Math.Round(jointZ,4)));
            oscWriter.Send(new OscBundle(DateTime.Now, jointElement.ToArray()));
                
            if (showOscData)
            {
                oscText += "\n\n" + jointName + " " +
                           (float)Math.Round(jointX, 2) + " " +
                           (float)Math.Round(jointY, 2) + " " +
                                    (float)Math.Round(jointZ, 2);
            }
            
            return oscText;
        }

        private void sendOSCAllSeparately(ref int counter, Skeleton skel, ref string oscText)
        {
            // joints all sent individually as floats
            // Ex: "/skeleton#/JointName/x/#.##", "/skeleton#/JointName/y/#.##", "/skeleton#/JointName/z/#.##"
            var address = "";
            foreach (Joint joint in skel.Joints)
            {
                var jointName = joint.JointType.ToString();
                var jointElement = new List<OscElement>();
                Point jointPosition = SkeletonPointToScreen(joint.Position);
                address = "/skeleton" + counter + "/" + jointName;
                if (sendPositionsAsPercentage)
                {
                    jointPosition.X = Math.Round(jointPosition.X / 640, 3);
                    jointPosition.Y = Math.Round(jointPosition.Y / 480, 3);
                    //jointElement.Add(new OscElement("/skeleton" + counter + "/" + jointName, (float)jointPosition.X, (float)jointPosition.Y, Math.Round(joint.Position.Z, 3)));
                    //oscWriter.Send(new OscBundle(DateTime.Now, jointElement.ToArray()));

                    var jointElements = new List<OscElement>();
                    if (oscAddress != "")
                    {
                        jointElements.Add(new OscElement(oscAddress + counter,
                                                         (float)jointPosition.X));
                        counter++;
                        jointElements.Add(new OscElement(oscAddress + counter,
                                                         (float)jointPosition.Y));
                        counter++;
                        jointElements.Add(new OscElement(oscAddress + counter,
                                                         (float)
                                                         Math.Round(joint.Position.Z, 3)));
                        counter++;
                    }
                    else
                    {
                        jointElements.Add(new OscElement(address + "/x",
                                                         (float)jointPosition.X));
                        jointElements.Add(new OscElement(address + "/y",
                                                         (float)jointPosition.Y));
                        jointElements.Add(new OscElement(address + "/z",
                                                         (float)
                                                         Math.Round(joint.Position.Z, 3)));
                    }
                    oscWriter.Send(new OscBundle(DateTime.Now, jointElements.ToArray()));

                    if (showOscData)
                    {
                        oscText += GenerateOscDataDump(counter, jointName, joint.Position,
                                                       jointPosition);
                    }
                }
                else
                {
                    var jointElements = new List<OscElement>();
                    if (oscAddress != "")
                    {
                        jointElements.Add(new OscElement(oscAddress + counter,
                                                         (float)
                                                         Math.Round(joint.Position.X, 4)));
                        counter++;
                        jointElements.Add(new OscElement(oscAddress + counter,
                                                         (float)
                                                         Math.Round(joint.Position.Y, 4)));
                        counter++;
                        jointElements.Add(new OscElement(oscAddress + counter,
                                                         (float)
                                                         Math.Round(joint.Position.Z, 4)));
                        counter++;
                    }
                    else
                    {
                        jointElements.Add(new OscElement(address + "/x", (float) Math.Round(joint.Position.X, 4)));
                        jointElements.Add(new OscElement(address + "/y", (float) Math.Round(joint.Position.Y, 4)));
                        jointElements.Add(new OscElement(address + "/z", (float) Math.Round(joint.Position.Z, 4)));
                    }
                    oscWriter.Send(new OscBundle(DateTime.Now, jointElements.ToArray()));
                    if (showOscData)
                    {
                        oscText += GenerateOscDataDump(counter, jointName, joint.Position);
                    }
                }
            }
        }

        private void sendOSCAsString(int counter, Skeleton skel, List<OscElement> elements, ref string args, ref int jointNumber, ref string oscText)
        {
            // bundle all joints as a string for parsing at client end
            // "Joint name|Joint number:.56|.45|1.4, ... more joints"
            foreach (Joint joint in skel.Joints)
            {
                jointNumber++;
                var jointName = joint.JointType.ToString();
                Point jointPosition = SkeletonPointToScreen(joint.Position);
                if (sendPositionsAsPercentage)
                {
                    jointPosition.X = Math.Round(jointPosition.X / 640, 3);
                    jointPosition.Y = Math.Round(jointPosition.Y / 480, 3);
                    // concatenate all joints as one big string to send as OSC
                    args += jointName + "|" + jointNumber + ":" + (float)jointPosition.X + "|" +
                            (float)jointPosition.Y + "|" + (float)Math.Round(joint.Position.Z, 3) + ",";
                    if (showOscData)
                    {
                        oscText += "\n" + jointName + "|" + jointNumber + ":" +
                                   (float)jointPosition.X + "|" +
                                   (float)jointPosition.Y + "|" +
                                   (float)Math.Round(joint.Position.Z, 3) + ",";
                        //GenerateOscDataDump(counter, jointName, joint.Position, jointPosition);
                    }
                }
                else
                {
                    // concatenate all joints as one big string to send as OSC
                    args += jointName + "|" + jointNumber + ":" + (float)Math.Round(joint.Position.X, 4) + "|" +
                            (float)Math.Round(joint.Position.Y, 4) + "|" + (float)Math.Round(joint.Position.Z, 4) + ",";

                    if (showOscData)
                    {
                        oscText += "\n" + jointName + "|" + jointNumber + ":" +
                                   (float)Math.Round(joint.Position.X, 4) + "|" +
                                   (float)Math.Round(joint.Position.Y, 4) + "|" +
                                   (float)Math.Round(joint.Position.Z, 4) + ",";
                        //GenerateOscDataDump(counter, jointName, joint.Position);
                    }
                }

            }
            elements.Add(new OscElement("/skeleton" + counter, args));
            oscWriter.Send(new OscBundle(DateTime.Now, elements.ToArray()));
        }

        private String GenerateOscDataDump(int counter, string jointName, SkeletonPoint jointPoint)
        {
            var dataDump = "";
            if (oscAddress != "")
            {
                dataDump += oscAddress + (counter - 3) + "/" + Math.Round(jointPoint.X, 4) + "\n";
                dataDump += oscAddress + (counter - 2) + "/" + Math.Round(jointPoint.Y, 4) + "\n";
                dataDump += oscAddress + (counter - 1) + "/" + Math.Round(jointPoint.Z, 4) + "\n";
            }
            else
            {
                dataDump += "/skeleton" + counter + "/" + jointName + "/x" + Math.Round(jointPoint.X, 4) + "\n";
                dataDump += "/skeleton" + counter + "/" + jointName + "/y" + Math.Round(jointPoint.Y, 4) + "\n";
                dataDump += "/skeleton" + counter + "/" + jointName + "/z" + Math.Round(jointPoint.Z, 4) + "\n";
            }
            return dataDump;
        }

        private String GenerateOscDataDump(int counter, string jointName, SkeletonPoint jointPoint, Point jointPosition)
        {
            var dataDump = "";
            if (oscAddress != "")
            {
                dataDump += oscAddress + (counter - 3) + "/" + Math.Round(jointPosition.X, 3) + "\n";
                dataDump += oscAddress + (counter - 2) + "/" + Math.Round(jointPosition.Y, 3) + "\n";
                dataDump += oscAddress + (counter - 1) + "/" + Math.Round(jointPoint.Z, 3) + "\n";
            }
            else
            {
                dataDump += "/skeleton" + counter + "/" + jointName + "/x" + Math.Round(jointPosition.X, 3) + "\n";
                dataDump += "/skeleton" + counter + "/" + jointName + "/y" + Math.Round(jointPosition.Y, 3) + "\n";
                dataDump += "/skeleton" + counter + "/" + jointName + "/z" + Math.Round(jointPoint.Z, 3) + "\n";
            }
            return dataDump;
        }


        public static long GetTimestamp()
        {
            long ticks = DateTime.UtcNow.Ticks - DateTime.Parse("01/01/1970 00:00:00").Ticks;
            ticks /= 10000;
            return ticks;
        }


        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor0.CoordinateMapper.MapSkeletonPointToDepthPoint(
                                                                             skelpoint,
                                                                             DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor0)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor0.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor0.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }
        /// <summary>
        /// Handles the checking or unchecking of the draw color checkbox
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxDrawColorChanged(object sender, RoutedEventArgs e)
        {
            drawColor = this.checkBoxDrawColor.IsChecked.GetValueOrDefault();
            ResizeWindow();
        }

        private void CheckBoxOscOnChanged(object sender, RoutedEventArgs e)
        {
            oscOn = this.checkBoxOscOn.IsChecked.GetValueOrDefault();
        }
        private void CheckBoxShowOscDataChanged(object sender, RoutedEventArgs e)
        {
            showOscData = this.checkBoxShowOscData.IsChecked.GetValueOrDefault();
            if (showOscData)
            {
                oscViewer.Visibility = Visibility.Visible;
                CloseOscViewer.Visibility = Visibility.Visible;
            }
            else
            {
                oscViewer.Visibility = Visibility.Collapsed;
                CloseOscViewer.Visibility = Visibility.Collapsed;
            }
        }
        private void CheckBoxShowSkeletonChanged(object sender, RoutedEventArgs e)
        {
            showSkeleton = this.checkBoxShowSkeleton.IsChecked.GetValueOrDefault();
            ResizeWindow();
        }

        private void ResizeWindow()
        {
            if (!drawColor && !showSkeleton)
            {
                this.Height = 230;
            }
            else
            {
                this.Height = 755;
            }
        }
        private void CheckBoxTrackNearestOnlyChanged(object sender, RoutedEventArgs e)
        {
            trackNearestOnly = this.checkBoxTrackNearestOnly.IsChecked.GetValueOrDefault();
        }

        private void CheckBoxSendPositionAsPercentageChanged(object sender, RoutedEventArgs e)
        {
            sendPositionsAsPercentage = this.checkBoxSendPositionAsPercentage.IsChecked.GetValueOrDefault();
            Console.WriteLine(sendPositionsAsPercentage);
        }


        private void CheckBoxSendAsStringChanged(object sender, RoutedEventArgs e)
        {
            sendOscAsString = this.checkBoxSendAsString.IsChecked.GetValueOrDefault();
            if (sendOscAsString)
            {
                this.checkBoxSendSeparately.IsChecked = false;
                sendAllSeparately = false;
                this.checkBoxSendSeparately.IsEnabled = false;
            }
            else
            {
                this.checkBoxSendSeparately.IsEnabled = true;
            }
        }
        private void CheckBoxSendSeparatelyChanged(object sender, RoutedEventArgs e)
        {
            sendAllSeparately = this.checkBoxSendSeparately.IsChecked.GetValueOrDefault();
            if (sendAllSeparately)
            {
                OscAddress.IsEnabled = true;
            }
        }
        private void ChangePortClicked(object sender, RoutedEventArgs e)
        {
            oscArgs[1] = OscPort.Text;
            oscWriter = new UdpWriter(oscArgs[0], Convert.ToInt32(oscArgs[1]));
        }

        private void CloseOscViewerClicked(object sender, RoutedEventArgs e)
        {
            oscViewer.Visibility = Visibility.Collapsed;
            CloseOscViewer.Visibility = Visibility.Collapsed;
            checkBoxShowOscData.IsChecked = false;
        }

        private void ChangeAddressClicked(object sender, RoutedEventArgs e)
        {
            UpdateOscAddress();
        }

        private void OscAddressKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                UpdateOscAddress();
            }
        }

        private void UpdateOscAddress()
        {
            oscAddress = OscAddress.Text;
            if (oscAddress.Substring(0, 1) != "/")
            {
                oscAddress = "/" + oscAddress;
                OscAddress.Text = oscAddress;
                ChangeAddress.Focus();
            }
        }

        private void OscPortKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                oscArgs[1] = OscPort.Text;
                oscWriter = new UdpWriter(oscArgs[0], Convert.ToInt32(oscArgs[1]));
            }
        }

        private void CheckBoxSendAnimataData(object sender, RoutedEventArgs e)
        {
            sendAnimataData = this.checkBoxSendAnimataData.IsChecked.GetValueOrDefault();
        }

        private void ChangeScaleClicked(object sender, RoutedEventArgs e)
        {
            UpdateAnimataScaleFactor();
        }

        private void UpdateAnimataScaleFactor(){
            animataScaleFactor = Convert.ToDouble(AnimataScaleTextBox.Text);
        }

        private double Length(Joint p1, Joint p2)
        {
            return Math.Sqrt(
                Math.Pow(p1.Position.X - p2.Position.X, 2) +
                Math.Pow(p1.Position.Y - p2.Position.Y, 2) +
                Math.Pow(p1.Position.Z - p2.Position.Z, 2));
        }

        private double Length(params Joint[] joints)
        {
            double length = 0;
            for (int index = 0; index < joints.Length - 1; index++) {
                length += Length(joints[index], joints[index + 1]);
            }
            return length;
        }

        private int NumberOfTrackedJoints(params Joint[] joints)
        {
            int trackedJoints = 0;
            foreach (var joint in joints)
            {
                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    trackedJoints++;
                }
            }
            return trackedJoints;
        }

        private double skeletonHeight(Skeleton skeleton)
        {
            const double HEAD_DIVERGENCE = 0.1;

            var head = skeleton.Joints[JointType.Head];
            var neck = skeleton.Joints[JointType.ShoulderCenter];
            var spine = skeleton.Joints[JointType.Spine];
            var waist = skeleton.Joints[JointType.HipCenter];
            var hipLeft = skeleton.Joints[JointType.HipLeft];
            var hipRight = skeleton.Joints[JointType.HipRight];
            var kneeLeft = skeleton.Joints[JointType.KneeLeft];
            var kneeRight = skeleton.Joints[JointType.KneeRight];
            var ankleLeft = skeleton.Joints[JointType.AnkleLeft];
            var ankleRight = skeleton.Joints[JointType.AnkleRight];
            var footLeft = skeleton.Joints[JointType.FootLeft];
            var footRight = skeleton.Joints[JointType.FootRight];

            // Find which leg is tracked more accurately.
            int legLeftTrackedJoints = NumberOfTrackedJoints(hipLeft, kneeLeft, ankleLeft, footLeft);
            int legRightTrackedJoints = NumberOfTrackedJoints(hipRight, kneeRight, ankleRight, footRight);
            double legLength = legLeftTrackedJoints > legRightTrackedJoints ? Length(hipLeft, kneeLeft, ankleLeft, footLeft) : Length(hipRight, kneeRight, ankleRight, footRight);

            return Length(head, neck, spine, waist) + legLength + HEAD_DIVERGENCE;
        }
    }
}