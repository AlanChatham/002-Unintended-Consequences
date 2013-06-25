
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


        class LocatedSensor
        {
            public KinectSensor sensor {get; set;}
            public float x {get; set;}
            public float y {get; set;}
            public float z {get; set;}
            public Vector4 frontDirection { get; set; }
            /// <summary>
            /// A List of skeletons, with joint positions in relative orientation to the sensor
            /// </summary>
            public List<Skeleton> relativeSkeletons;
            /// <summary>
            /// A List of skeletons, with joint positions in a global orientation
            /// </summary>
            public List<Skeleton> globalSkeletons;

            private Matrix4 rotationMatrix;

            public LocatedSensor() { }

            /// <summary>
            /// Create a new sensor with location and orientation parameters
            /// </summary>
            /// <param name="sensor">A given Kinect sensor</param>
            /// <param name="x">X, in global coordinates</param>
            /// <param name="y">Y, in global coordinates</param>
            /// <param name="z">Z, in global coordinates</param>
            /// <param name="frontOrientation"></param>
            public LocatedSensor(KinectSensor sensor, float x, float y, float z, Vector4 frontOrientation){
                this.sensor = sensor;
                this.x = x;
                this.y = y;
                this.z = z;
                this.frontDirection = frontOrientation;

                this.relativeSkeletons = new List<Skeleton>();
                this.globalSkeletons = new List<Skeleton>();

                //Register an event to update the internal skeleton lists when we get fresh skeleton data
                sensor.SkeletonFrameReady += this.refreshSkeletonPositions;   
            }


            /// <summary>
            /// SkeletonFrameReady gets fired every skeleton frame update, and refreshes the LocatedSensor's
            ///  global and relative skeleton maps
            /// </summary>
            /// <param name="sender"></param>
            /// <param name="e"></param>
            private void refreshSkeletonPositions(object sender, SkeletonFrameReadyEventArgs e) {
                using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame()) {
                    if (skeletonFrame != null) {
                        // First, get the relative skeletons - easy peasy
                        Skeleton[] skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                        skeletonFrame.CopySkeletonDataTo(skeletons);
                        this.relativeSkeletons = skeletons.ToList<Skeleton>();

                        // TODO Next, apply a rotation translation to every point to get some global skeleton coordinates. Woo!

                        // And apply the global offset!
                    }
                }
            }
        }

        /// <summary>
        /// VisualKinectUnits hold a LocatedSensor kinect sensor class,
        ///  as well as an optional color bitmap and image to draw skeletons on
        /// </summary>
        class VisualKinectUnit
        {
            public LocatedSensor locatedSensor {get; set;}
            private System.Windows.Controls.Image skeletonDrawingImage;
            private System.Windows.Controls.Image colorImage;
            private WriteableBitmap colorBitmap;
            /// <summary>
            /// Drawing group for skeleton rendering output
            /// </summary>
            private DrawingGroup drawingGroup;
             /// <summary>
            /// Drawing image that we will display
            /// </summary>
            private DrawingImage imageSource;

            public VisualKinectUnit(){
            }

            #region drawingSettings
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
            #endregion
            
            /// <summary>
            /// Constructor for VisualKinectUnit
            /// </summary>
            /// <param name="sensor">LocatedSensor class kinect sensor</param>
            /// <param name="skeletonDrawingImage">Image that we'll draw the skeleton on</param>
            /// <param name="colorImage">Image we'll use to push the color camera video to</param>
            public VisualKinectUnit(LocatedSensor locatedSensor, System.Windows.Controls.Image skeletonDrawingImage = null, System.Windows.Controls.Image colorImage = null)
            {
                // Get in some parameters
                this.locatedSensor = locatedSensor;
                this.skeletonDrawingImage = skeletonDrawingImage;
                this.colorImage = colorImage;

                // Set up the basics for drawing a skeleton
                // Create the drawing group we'll use for drawing
                this.drawingGroup = new DrawingGroup();

                // Create an image source that we can use in our image control
                this.imageSource = new DrawingImage(this.drawingGroup);

                // Turn on the skeleton stream to receive skeleton frames
                locatedSensor.sensor.SkeletonStream.Enable();
               
                // Turn on the color stream to receive color frames
                locatedSensor.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

                // This is the bitmap we'll display on-screen
                colorBitmap = (new WriteableBitmap(locatedSensor.sensor.ColorStream.FrameWidth,
                                                   locatedSensor.sensor.ColorStream.FrameHeight,
                                                   96.0, 96.0, PixelFormats.Bgr32, null));

                // Add an event handler to be called whenever there is new color frame data
                if (colorImage != null) {
                    locatedSensor.sensor.ColorFrameReady += this.refreshColorImage;
                }
                // Add an event handler to be called whenever there is new color frame data
                if (skeletonDrawingImage != null) {
                    locatedSensor.sensor.SkeletonFrameReady += this.refreshSkeletonDrawing;
                    this.skeletonDrawingImage.Source = imageSource;
                }
            }

            public void Stop(){
                if (this.locatedSensor != null){
                    locatedSensor.sensor.Stop();
                }
            }

            /// <summary>
            /// Draw the Color Frame to screen
            /// </summary>
            /// <param name="sender">object sending the event</param>
            /// <param name="e">event arguments</param>
            /// //Refactor this by having you pass in a bitmap to draw to
            private void refreshColorImage(object sender, ColorImageFrameReadyEventArgs e) {  
                using (ColorImageFrame colorFrame = e.OpenColorImageFrame()) {
                    if (colorFrame != null) {
                        // Copy the pixel data from the image to a temporary array
                        byte[] colorPixels = new byte[colorFrame.PixelDataLength];
                        colorFrame.CopyPixelDataTo(colorPixels);

                        // Write the pixel data into our bitmap
                        Int32Rect sourceRect = new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight);
                        this.colorBitmap.WritePixels(sourceRect, colorPixels, this.colorBitmap.PixelWidth * sizeof(int), 0);

                        this.colorImage.Source = colorBitmap;
                    }
                }    
            }

            private void refreshSkeletonDrawing(object sender, SkeletonFrameReadyEventArgs e) {
                using (DrawingContext dc = this.drawingGroup.Open()) {
                    bool noTrackedSkeletons = true;
                    // Draw a transparent background to set the render size
                    dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));
                    if (this.locatedSensor.relativeSkeletons.Count > 0) {
                        foreach (Skeleton skel in this.locatedSensor.relativeSkeletons) {
                            RenderClippedEdges(skel, dc);

                            if (skel.TrackingState == SkeletonTrackingState.Tracked) {
                                noTrackedSkeletons = false;
                                this.DrawBonesAndJoints(skel, dc);
                            }
                            else if (skel.TrackingState == SkeletonTrackingState.PositionOnly){
                                 dc.DrawEllipse(
                                this.centerPointBrush,
                                null,
                                this.SkeletonPointToScreen(skel.Position),
                                BodyCenterThickness,
                                BodyCenterThickness);
                            }
                            else if (skel.TrackingState == SkeletonTrackingState.NotTracked) {
                            }
                        }
                        if (noTrackedSkeletons) {
                        }
                    }
                    // prevent drawing outside of our render area
                    this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
                }
            }
            /// <summary>
            /// Draws indicators to show which edges are clipping skeleton data
            /// </summary>
            /// <param name="skeleton">skeleton to draw clipping information for</param>
            /// <param name="drawingContext">drawing context to draw to</param>
            private void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
            {
                if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom)) {
                    drawingContext.DrawRectangle(
                        Brushes.Red,
                        null,
                        new Rect(0, this.skeletonDrawingImage.Height - ClipBoundsThickness, this.skeletonDrawingImage.Width, ClipBoundsThickness));
                }     
                if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top)){
                    drawingContext.DrawRectangle(
                        Brushes.Red,
                        null,
                        new Rect(0, 0, this.skeletonDrawingImage.Width, ClipBoundsThickness));
                }
                if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left)) {
                    drawingContext.DrawRectangle(
                        Brushes.Red,
                        null,
                        new Rect(0, 0, ClipBoundsThickness, this.skeletonDrawingImage.Height));
                }
                if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right)) {
                    drawingContext.DrawRectangle(
                        Brushes.Red,
                        null,
                        new Rect(this.skeletonDrawingImage.Width - ClipBoundsThickness, 0, ClipBoundsThickness, this.skeletonDrawingImage.Height));
                }
            }

                   /// <summary>
            /// Draws a skeleton's bones and joints
            /// </summary>
            /// <param name="skeleton">skeleton to draw</param>
            /// <param name="drawingContext">drawing context to draw to</param>
            private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext) {

                Console.WriteLine("DrawBonesandJoints called");
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
                // Render Joints
                foreach (Joint joint in skeleton.Joints) {
                    Brush drawBrush = null;

                    if (joint.TrackingState == JointTrackingState.Tracked) {
                        drawBrush = this.trackedJointBrush;
                    }
                    else if (joint.TrackingState == JointTrackingState.Inferred) {
                        drawBrush = this.inferredJointBrush;
                    }

                    if (drawBrush != null) {
                        drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                    }
                }
            }

            /// <summary>
            /// Maps a SkeletonPoint to lie within our render space and converts to Point
            /// </summary>
            /// <param name="skelpoint">point to map</param>
            /// <returns>mapped point</returns>
            private Point SkeletonPointToScreen(SkeletonPoint skelpoint) {
                // Convert point to depth space.  
                // We are not using depth directly, but we do want the points in our 640x480 output resolution.
                DepthImagePoint depthPoint = this.locatedSensor.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(
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
            private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1) {
                Joint joint0 = skeleton.Joints[jointType0];
                Joint joint1 = skeleton.Joints[jointType1];

                // If we can't find either of these joints, exit
                if (joint0.TrackingState == JointTrackingState.NotTracked ||
                    joint1.TrackingState == JointTrackingState.NotTracked) {
                    return;
                }

                // Don't draw if both points are inferred
                if (joint0.TrackingState == JointTrackingState.Inferred &&
                    joint1.TrackingState == JointTrackingState.Inferred) {
                    return;
                }

                // We assume all drawn bones are inferred unless BOTH joints are tracked
                Pen drawPen = this.inferredBonePen;
                if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked) {
                    drawPen = this.trackedBonePen;
                }

                drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
            }
        }

        #region Global Settings
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
        private List<VisualKinectUnit> visualKinectUnitList;
        private List<LocatedSensor> locatedSensorList;
        private LocatedSensor sensor0;
        private KinectSensor sensor1;

        private List<System.Windows.Controls.Image> skeletonImageList;
        private List<System.Windows.Controls.Image> colorImageList;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private List<DrawingGroup> drawingGroupList;
        private DrawingGroup drawingGroup0;
        private DrawingGroup drawingGroup1;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private List<DrawingImage> imageSourceList;
        private DrawingImage imageSource0;
        private DrawingImage imageSource1;

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private List<WriteableBitmap> colorBitmapList;
        private WriteableBitmap colorBitmap0;
        private WriteableBitmap colorBitmap1;

        // OSC 
        private static UdpWriter oscWriter;
        private static string[] oscArgs = new string[2];

        #endregion

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
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom)) {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }     
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top)){
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left)) {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right)) {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }


        private VisualKinectUnit Kinect0;

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e) {
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


            // Set up our lists
            visualKinectUnitList = new List<VisualKinectUnit>();
            skeletonImageList = new List<System.Windows.Controls.Image>();
            skeletonImageList.Add(Image0);
            skeletonImageList.Add(Image1);

            colorImageList = new List<System.Windows.Controls.Image>();
            colorImageList.Add(ColorImage0);
            colorImageList.Add(ColorImage1);

            locatedSensorList = new List<LocatedSensor>();
            drawingGroupList = new List<DrawingGroup>();
            imageSourceList = new List<DrawingImage>();
            colorBitmapList = new List<WriteableBitmap>();

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit
            int numberOfKinects = 0;
            foreach (var potentialSensor in KinectSensor.KinectSensors) {
                if (potentialSensor.Status == KinectStatus.Connected) {
                    // Start the sensor!
                    try {
                        potentialSensor.Start();
                        // Good to go, so count this one as connected!
                        // So let's set up some environment for this...
                        Vector4 orientation = new Vector4();
                        locatedSensorList.Add(new LocatedSensor(potentialSensor, 0, 0, 0, orientation));
                        drawingGroupList.Add(new DrawingGroup());
                        imageSourceList.Add(new DrawingImage(drawingGroupList.Last<DrawingGroup>()));

                        LocatedSensor sensor = new LocatedSensor(potentialSensor, 0, 0, 0, orientation);
                        if ((numberOfKinects < colorImageList.Count) && (numberOfKinects < skeletonImageList.Count)) {
                            System.Windows.Controls.Image colorImage = colorImageList[numberOfKinects];
                            System.Windows.Controls.Image skeletonImage = skeletonImageList[numberOfKinects];
                            visualKinectUnitList.Add(new VisualKinectUnit(sensor, skeletonImage, colorImage));
                        }
                        else {
                            visualKinectUnitList.Add(new VisualKinectUnit(sensor));
                        }
                        numberOfKinects++;
                    }
                    catch (IOException) {
                        Console.WriteLine("Couldn't start one of the Kinect sensors...");
                    }
                }
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            foreach (VisualKinectUnit unit in visualKinectUnitList){
                unit.Stop();
            }
        }

        /// <summary>
        /// Set kinect to track the closest skeleton
        /// </summary>
        private void TrackClosestSkeleton(IEnumerable<Skeleton> skeletonData)
        {
            if (this.sensor0.sensor != null && this.sensor0.sensor.SkeletonStream != null)
            {
                if (!this.sensor0.sensor.SkeletonStream.AppChoosesSkeletons)
                {
                    this.sensor0.sensor.SkeletonStream.AppChoosesSkeletons = true; // Ensure app chooses skeletons is set
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
                    this.sensor0.sensor.SkeletonStream.ChooseSkeletons(closestID); // Track this skeleton
                }
            }
        }


        private int sendOSC(int counter, Skeleton skel) {
            var elements = new List<OscElement>();
            var oscText = "";
            if (sendAnimataData){
                oscText = sendOSCHeadOnly(counter, skel, oscText);
            }
            
            if (showOscData) {
                oscViewer.Text = oscText;
            }
            counter++;
            return counter;
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