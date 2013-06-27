
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
    using MathNet.Numerics.LinearAlgebra.Double;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        class LocatedSensor
        {
            public KinectSensor sensor {get; set;}
            public float xOffset {get; set;}
            public float yOffset {get; set;}
            public float zOffset { get; set; }
            public DenseMatrix rotationMatrix { get; set; }
            /// <summary>
            /// A List of skeletons, with joint positions in relative orientation to the sensor
            /// </summary>
            public List<Skeleton> relativeSkeletons;
            /// <summary>
            /// A List of skeletons, with joint positions in a global orientation
            /// </summary>
            public List<Skeleton> globalSkeletons;

            public LocatedSensor() { }

            /// <summary>
            /// Create a new sensor with location and orientation parameters
            /// </summary>
            /// <param name="sensor">A given Kinect sensor</param>
            /// <param name="x">X, in global coordinates</param>
            /// <param name="y">Y, in global coordinates</param>
            /// <param name="z">Z, in global coordinates</param>
            /// <param name="theta">Rotation around the vertical axis, </param>
            public LocatedSensor(KinectSensor sensor, float x, float y, float z, 
                                  // Was going 
                                  //double rotationVectorX, double rotationVectorY, double rotationVectorZ,
                                  double theta){
                this.sensor = sensor;
                this.xOffset = x;
                this.yOffset = y;
                this.zOffset = z;

                // Set up the rotation matrix
        /*        this.rotationMatrix = new DenseMatrix(4, 4);
                double u = rotationVectorX;
                double u2 = Math.Pow(u, 2);
                double v = rotationVectorY;
                double v2 = Math.Pow(v, 2);
                double w = rotationVectorZ;
                double w2 = Math.Pow(w, 2);
                double thta = theta * Math.PI / 180; // Converted to radians
                rotationMatrix[0, 0] = u2 + (1 - u2) * Math.Cos(thta);
                rotationMatrix[0, 1] = u * v * (1 - Math.Cos(thta)) - w * Math.Sin(thta);
                rotationMatrix[0, 2] = u * w * (1 - Math.Cos(thta)) - v * Math.Sin(thta);
                rotationMatrix[0, 3] = 0;
                rotationMatrix[1, 0] = u * v * (1 - Math.Cos(thta)) - w * Math.Sin(thta);
                rotationMatrix[1, 1] = v2 + (1 - v2) * Math.Cos(thta);
                rotationMatrix[1, 2] = v * w * (1 - Math.Cos(thta)) - u * Math.Sin(thta);
                rotationMatrix[1, 3] = 0;
                rotationMatrix[2, 0] = u * w * (1 - Math.Cos(thta)) - v * Math.Sin(thta);
                rotationMatrix[2, 2] = v * w * (1 - Math.Cos(thta)) - u * Math.Sin(thta);
                rotationMatrix[2, 3] = w2 + (1 - w2) * Math.Cos(thta);
                rotationMatrix[2, 3] = 0;
                rotationMatrix[3, 0] = 0;
                rotationMatrix[3, 1] = 0;
                rotationMatrix[3, 2] = 0;
                rotationMatrix[3, 3] = 1;
          */

                double thta = theta * Math.PI / 180; // Converted to radians
                rotationMatrix = new DenseMatrix(3,3);
                rotationMatrix[0,0] = Math.Cos(thta);
                rotationMatrix[0,1] = 0;
                rotationMatrix[0,2] = Math.Sin(thta);
                rotationMatrix[1,0] = 0;
                rotationMatrix[1,1] = 1;
                rotationMatrix[1,2] = 0;
                rotationMatrix[2,0] = -Math.Sin(thta);
                rotationMatrix[2,1] = 0;
                rotationMatrix[2,2] = Math.Cos(thta);

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
                        Skeleton[] skeletonsR = new Skeleton[skeletonFrame.SkeletonArrayLength];
                        skeletonFrame.CopySkeletonDataTo(skeletonsR);
                        this.relativeSkeletons = skeletonsR.ToList<Skeleton>();

                        // Now global skeletons...
                        // First, clear our global skeletons list.
                        //  We'll be building this back up from scratch here
                        this.globalSkeletons.Clear();
                        // Next, iterate through all the skeletons, applying a rotation and translation
                        //  to get us into global coordinates
                        foreach (Skeleton skel in this.relativeSkeletons) {
                            // Add a temporary skeleton object to store transformed
                            //  data into
                            Skeleton tempSkel = skel;
                            
                            foreach (Joint j in skel.Joints){
                                // Make a new joint, then put it into our temporary joint
                                //  collection
                                JointType type = j.JointType;
                                Joint tempJoint = tempSkel.Joints[type];
                                // Copy the current joint state
                                JointTrackingState tracking = j.TrackingState;
                                tempJoint.TrackingState = tracking;

                                // However, we transform the position of the joint at least
                                SkeletonPoint shiftedPoint = new SkeletonPoint();
                                // Rotate the points
                                DenseMatrix point = new DenseMatrix(1, 3);
                                point[0, 0] = j.Position.X;
                                point[0, 1] = j.Position.Y;
                                point[0, 2] = j.Position.Z;
                                var rotatedPoint = point.Multiply(this.rotationMatrix);

                                // Then shift them by the global coordinates.
                                shiftedPoint.X = (float)rotatedPoint[0, 0] + this.xOffset;
                                shiftedPoint.Y = (float)rotatedPoint[0, 1] + this.yOffset;
                                shiftedPoint.Z = (float)rotatedPoint[0, 2] + this.zOffset;
                                tempJoint.Position = shiftedPoint;

                                tempSkel.Joints[type] = tempJoint;
                            }
                            // Next, alter the higher-level parameters of our skeleton
                            SkeletonPoint shiftedPosition = new SkeletonPoint();
                            // Rotate
                            DenseMatrix p = new DenseMatrix(1, 3);
                            p[0, 0] = tempSkel.Position.X;
                            p[0, 1] = tempSkel.Position.Y;
                            p[0, 2] = tempSkel.Position.Z;
                            var rPoint = p.Multiply(this.rotationMatrix);

                            // Then shift them by the global coordinates.
                            shiftedPosition.X = (float)rPoint[0, 0] + this.xOffset;
                            shiftedPosition.Y = (float)rPoint[0, 1] + this.yOffset;
                            shiftedPosition.Z = (float)rPoint[0, 2] + this.zOffset;

                            tempSkel.Position = shiftedPosition;

                            // Now add that skeleton to our global skeleton list
                            this.globalSkeletons.Add(tempSkel);
                        }

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
                    dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, this.skeletonDrawingImage.Width, this.skeletonDrawingImage.Height));
                    if (this.locatedSensor.relativeSkeletons.Count > 0) {
                      //  foreach (Skeleton skel in this.locatedSensor.relativeSkeletons) {
                        foreach (Skeleton skel in this.locatedSensor.globalSkeletons) {
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
                    this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, this.skeletonDrawingImage.Width, this.skeletonDrawingImage.Height));
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
                // Now adjust that point by the actual size of our drawing image
                double imageWidthRatio = this.skeletonDrawingImage.Width / 640;
                double imageHeightRatio = this.skeletonDrawingImage.Height / 480;
                return new Point(depthPoint.X * imageWidthRatio, depthPoint.Y * imageHeightRatio);
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

        float[] kinectXPositions = { 0f, -2.28f, -2.5f, -2.5f }; // X is positive left, if looking at the screen
        float[] kinectYPositions = { 0, 0, 0, 0 }; // Y is positive up
        float[] kinectZPositions = { 2.5f, 2.5f, 2.2f, 0.68f }; // Z is positive towards the screen, so offsets will usually be positive
        float[] kinectAngles = { 00, 0, 90, 90 };
 
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
        /// If this is true the skeleton will be drawn on screen
        /// </summary>
        private bool showSkeleton = false;

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

        private List<System.Windows.Controls.Image> skeletonImageList;
        private List<System.Windows.Controls.Image> colorImageList;

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

            // Set up our lists
            visualKinectUnitList = new List<VisualKinectUnit>();

            skeletonImageList = new List<System.Windows.Controls.Image>();
            skeletonImageList.Add(Image0);
            skeletonImageList.Add(Image1);
            skeletonImageList.Add(Image2);
            skeletonImageList.Add(Image3);

            colorImageList = new List<System.Windows.Controls.Image>();
            colorImageList.Add(ColorImage0);
            colorImageList.Add(ColorImage1);
            colorImageList.Add(ColorImage2);
            colorImageList.Add(ColorImage3);

            masterSkeletonList = new List<Skeleton>();
            leadSkeletonIDs = new List<int>();

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
                       
                        LocatedSensor sensor = new LocatedSensor(potentialSensor, kinectXPositions[numberOfKinects],
                                                                                  kinectYPositions[numberOfKinects],
                                                                                  kinectZPositions[numberOfKinects],
                                                                                  kinectAngles[numberOfKinects]);
                        if ((numberOfKinects < colorImageList.Count) && (numberOfKinects < skeletonImageList.Count)) {
                            System.Windows.Controls.Image colorImage = colorImageList[numberOfKinects];
                            System.Windows.Controls.Image skeletonImage = skeletonImageList[numberOfKinects];
                            VisualKinectUnit newSensor = new VisualKinectUnit(sensor, skeletonImage, colorImage);
                            // Add a callback to our updateSkeletons function, so every frameReady event,
                            //  we update our global list of skeletons
                            newSensor.locatedSensor.sensor.SkeletonFrameReady += updateSkeletons;

                            newSensor.locatedSensor.sensor.SkeletonFrameReady += sendOSCHeadOnly;
                            visualKinectUnitList.Add(newSensor);
                        }
                        else {
                            visualKinectUnitList.Add(new VisualKinectUnit(sensor));
                        }
                        numberOfKinects++;
                        Console.WriteLine("Number of Kinects : " + numberOfKinects);
                    }
                    catch (IOException) {
                        Console.WriteLine("Couldn't start one of the Kinect sensors...");
                    }
                }
            }
        }

     
        /// <summary>
        /// This list holds all the skeletons seen by all sensors,
        ///  with position data in the global coordinate frame
        /// We trust that there won't be a conflict between random
        ///  IDs assigned by kinect sensors
        /// </summary>
        private List<Skeleton> masterSkeletonList;
        /// <summary>
        /// Skeletons within this radius of each other will be assumed
        ///  to be the same skeleton
        /// </summary>
        private float sameSkeletonRadius = 1.0f;

        private List<int> leadSkeletonIDs;

        private void updateSkeletons(object sender, SkeletonFrameReadyEventArgs e) {
            masterSkeletonList = new List<Skeleton>();
            List<int> currentSkeletonIDs = new List<int>();
            // From each of our kinect sensors...
            foreach (VisualKinectUnit kinect in this.visualKinectUnitList){
                // Read all our skeleton data
                foreach (Skeleton skel in kinect.locatedSensor.globalSkeletons){
                    // And if the skeleton is being tracked...
                    if (skel.TrackingState == SkeletonTrackingState.Tracked) {
                        currentSkeletonIDs.Add(skel.TrackingId);
                        bool isInMasterList = false;
                        // if it's in our master list already, 
                        for( int i = 0; i < masterSkeletonList.Count; i++) {
                            // update the skeleton to the fresh data
                            if (skel.TrackingId == masterSkeletonList[i].TrackingId) {
                                masterSkeletonList[i] = skel;
                                isInMasterList = true;
                                break;
                            }
                        }
                        if (!isInMasterList) {
                            masterSkeletonList.Add(skel);
                        }
                    }
                }
            }
//            Console.WriteLine("Size of master list: " + masterSkeletonList.Count);
            // Now, make sure we remove extra IDs of skeletons that aren't in our view anymore
            for (int i = leadSkeletonIDs.Count - 1; i >= 0; i--) {
                if (currentSkeletonIDs.Find(item => item == leadSkeletonIDs[i]) == 0) {
                    leadSkeletonIDs.RemoveAt(i);
                }
            }

            // Now let's pick a skeleton to persistently follow if we're not following one
            if (leadSkeletonIDs.Count == 0 && currentSkeletonIDs.Count > 0) {
                leadSkeletonIDs.Add(currentSkeletonIDs[0]);
            }

            // And let's find duplicate skeletons that are our lead skeletons
            if (leadSkeletonIDs.Count > 0) {
                Skeleton trackedSkeleton = new Skeleton();
                // Find our tracked skeleton
                foreach (Skeleton skel in masterSkeletonList) {
                    if ((skel.TrackingState == SkeletonTrackingState.Tracked ) && (currentSkeletonIDs.Find(id => id == skel.TrackingId) != 0) ){
                        trackedSkeleton = skel;
                        break;
                    }
                }
                // Iterate through it agian, since we might have missed it the first time
                foreach (Skeleton skel in masterSkeletonList) {
                    if ((skel.TrackingState == SkeletonTrackingState.Tracked) && 
                        skel.Position.X < trackedSkeleton.Position.X + sameSkeletonRadius &&
                        skel.Position.X > trackedSkeleton.Position.X - sameSkeletonRadius &&
                        skel.Position.Z < trackedSkeleton.Position.Z + sameSkeletonRadius &&
                        skel.Position.Z > trackedSkeleton.Position.Z - sameSkeletonRadius &&
                        skel != trackedSkeleton)
                    {
                        if (leadSkeletonIDs.Find(item => item == skel.TrackingId) == 0)
                            leadSkeletonIDs.Add(skel.TrackingId);
                    }
                }
            }
  //          Console.Write("currentSkeletonIDs: ");
  //          Console.WriteLine(currentSkeletonIDs.Count);
  //          Console.Write("currentLeadSkeletonIDs: ");
 //           Console.WriteLine(leadSkeletonIDs.Count);
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

        private int sendOSC(int counter, Skeleton skel) {
            var elements = new List<OscElement>();
            var oscText = "";
            if (sendAnimataData){
                //oscText = sendOSCHeadOnly(counter, skel, oscText);
            }
            
            if (showOscData) {
                oscViewer.Text = oscText;
            }
            counter++;
            return counter;
        }

        private void sendOSCHeadOnly(object sender, SkeletonFrameReadyEventArgs e)
        {
            // If there isn't a skeleton we want to send, we don't send anything
            if (leadSkeletonIDs.Count == 0)
                return;
            Skeleton skel = new Skeleton();
            skel = null;
            // Get the skeleton we want to send
            foreach (Skeleton s in masterSkeletonList) {
                if (s.TrackingId == leadSkeletonIDs[0]) {
                    skel = s;
                }
            }
            // Just in case, if we didn't find a skeleton, get out of here
            if (skel == null) {
                Console.WriteLine("Had skeletons in our leadSkeletonIDs list, but couldn't find the appropriate skeleton... How odd.");
                return;
            }
            double playerHeight = skeletonHeight(skel);
            // joints bundled individually as 2 floats (x, y)
            Joint headJoint = skel.Joints[JointType.Head];

           var jointElement = new List<OscElement>();
                
            // Joint positions are returned in meters, so we'll assume a 2 meter tall person
            // and scale that range to pixels for the animation
          /*  Point origin = new Point(0, 0); // Offset in meters to map our origin to the characters
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
                                    (float)Math.Round(jointZ, 4)));
           */
           var jointX = headJoint.Position.X;
           var jointY = headJoint.Position.Y;
           var jointZ = headJoint.Position.Z;
            jointElement.Add(new OscElement(
                                    "/head",
                                    (float)Math.Round(jointX, 4), (float)Math.Round(jointY, 4),
                                    (float)Math.Round(jointZ, 4)));
            oscWriter.Send(new OscBundle(DateTime.Now, jointElement.ToArray()));
                
            if (showOscData)
            {
                string oscText = "\n\n/head " +
                           (float)Math.Round(jointX, 2) + " " +
                           (float)Math.Round(jointY, 2) + " " +
                                    (float)Math.Round(jointZ, 2);
                oscViewer.Text =  oscText;
            }
            
            
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