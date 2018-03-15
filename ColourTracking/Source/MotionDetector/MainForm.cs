using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Threading;




using AForge;
using AForge.Imaging;
using AForge.Video;
using AForge.Video.VFW;
using AForge.Video.DirectShow;
using AForge.Vision.Motion;
using WatchDog;

namespace MotionDetectorSample
{
    public partial class MainForm : Form
    {

        public double Min(List<double> list)
        {
            double minimum = list[0];
            foreach (double l in list)
            {
                if (l <= minimum)
                    minimum = l;
            }
            return minimum;
        }
        private delegate void ChangeColorDelegate(Color newColor);

        ChangeColorDelegate _changeColorDelegate;

        private void ChangeColor(Color newColor)
        {
            tsColor.BackColor = newColor;
        }

        // opened video source
        private IVideoSource videoSource = null;

        // motion detector
        MotionDetector detector = new MotionDetector(
            null,
            new MotionAreaHighlighting());


        string message = " ";
        string back_gesture = " ";
        double k = 0;
        int k1 = 0;
        int counter = 0;
        double[] x = new double[3];
        double[] y = new double[3];

        List<double> diff = new List<double>();
        Dictionary<int, string> alphabet = new Dictionary<int, string>();
        
                   
        // motion detection and processing algorithm
        private int motionDetectionType = 1;
        private int motionProcessingType = 1;

        // statistics length
        private const int statLength = 3;
        // current statistics index
        private int statIndex = 0;
        // ready statistics values
        private int statReady = 0;
        // statistics array
        private int[] statCount = new int[statLength];

        ColorDetection _colorDetection = new ColorDetection();

        // counter used for flashing
        private int flash = 0;
        private float motionAlarmLevel = 0.015f;

        private List<float> motionHistory = new List<float>();

        // Constructor
        public MainForm()
        {
            InitializeComponent();
            alphabet.Add(0, "hello");
            alphabet.Add(1, "world");
            alphabet.Add(2, "I am");
            alphabet.Add(3, "student");
            alphabet.Add(4, "good");
            alphabet.Add(5, "bye");
            alphabet.Add(10, " ");

            comboCalculateNewColor.SelectedIndex = 0;
            _changeColorDelegate = this.ChangeColor;
        }
       
        // Application's main form is closing
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            CloseVideoSource();
        }

        // "Exit" menu item clicked
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

     

        // "Open" menu item clieck - open AVI file
        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                // create video source
                AVIFileVideoSource fileSource = new AVIFileVideoSource(openFileDialog.FileName);

                OpenVideoSource(fileSource);
            }
        }

      
      

          

        // Open local video capture device
        private void localVideoCaptureDeviceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            VideoCaptureDeviceForm form = new VideoCaptureDeviceForm();

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                // create video source
                VideoCaptureDevice videoSource = new VideoCaptureDevice(form.VideoDevice);

                // open it
                OpenVideoSource(videoSource);
            }
        }

        // Open video file using DirectShow
        private void openVideoFileusingDirectShowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                // create video source
                FileVideoSource fileSource = new FileVideoSource(openFileDialog.FileName);

                // open it
                OpenVideoSource(fileSource);
            }

        }

        // Open video source
        private void OpenVideoSource(IVideoSource source)
        {
            // set busy cursor
            this.Cursor = Cursors.WaitCursor;

            // close previous video source
            CloseVideoSource();

            // start new video source
            videoSourcePlayer.VideoSource = source;
            videoSourcePlayer.Start();

            // reset statistics
            statIndex = statReady = 0;

            // start timers
            timer.Start();
            alarmTimer.Start();

            videoSource = source;

            this.Cursor = Cursors.Default;
        }

        // Close current video source
        private void CloseVideoSource()
        {
            // set busy cursor
            this.Cursor = Cursors.WaitCursor;

            // stop current video source
            videoSourcePlayer.SignalToStop();

            // wait 2 seconds until camera stops
            for (int i = 0; (i < 50) && (videoSourcePlayer.IsRunning); i++)
            {
                Thread.Sleep(100);
            }
            if (videoSourcePlayer.IsRunning)
                videoSourcePlayer.Stop();

            // stop timers
            timer.Stop();
            alarmTimer.Stop();

            motionHistory.Clear();

            // reset motion detector
            if (detector != null)
                detector.Reset();

            videoSourcePlayer.BorderColor = Color.Black;
            this.Cursor = Cursors.Default;
        }

        // New frame received by the player
        private void videoSourcePlayer_NewFrame(object sender, ref Bitmap image)
        {
            
            diff.Clear();
          
           
                lock (this)
                {
                    if (detector != null)
                    {
                        float motionLevel = detector.ProcessFrame(image);

                        if (motionLevel > motionAlarmLevel)
                        {
                            // flash for 10 seconds
                            flash = (int)(10 * (1000 / alarmTimer.Interval));
                        }

                        // check objects' count
                        if (detector.MotionProcessingAlgorithm is BlobCountingObjectsProcessing)
                        {
                            BlobCountingObjectsProcessing countingDetector = (BlobCountingObjectsProcessing)detector.MotionProcessingAlgorithm;
                        }


                    // HERE!!!



                    if (counter == 100)
                    {

                        objectCenterLabel.Text = string.Format("Center: {0}", _colorDetection.Center);

                        Rectangle a = _colorDetection.BoundsBox;
                        x[0] = a.Left;
                        y[0] = a.Top;
                        x[1] = _colorDetection.Center.X;
                        y[1] = _colorDetection.Center.Y;
                        x[2] = a.Right;
                        y[2] = a.Bottom;



                        double S1 = Convert.ToDouble(x[0] * y[0] + x[1] * y[1] + x[2] * y[2]);
                        double S2 = Convert.ToDouble(x[0] + x[1] + x[2]);
                        double S3 = Convert.ToDouble(y[0] + y[1] + y[2]);
                        double Sq = Convert.ToDouble(x[0] * x[0] + x[1] * x[1] + x[2] * x[2]);

                        if ((3 * Sq - S2 * S2) == 0)
                        {
                            k1 = 10;
                            if (!back_gesture.Equals(alphabet[k1], StringComparison.Ordinal))
                            {
                                message += " ";
                                message += alphabet[k1];
                                back_gesture = alphabet[k1];
                            }
                        }
                        else
                        {
                            k = (3 * S1 - S2 * S3) / (3 * Sq - S2 * S2);
                            k = Math.Round(k, 3);

                            double Y1 = k * 100;
                            diff.Add(Math.Abs(Y1 - 100 * 0.73)); //hello(A)
                            diff.Add(Math.Abs(Y1 - 100 * 0.557)); //world(Æ)
                            diff.Add(Math.Abs(Y1 - 100 * 1.67));//I am(Ë)
                            diff.Add(Math.Abs(Y1 - 100 * 0.983));//student(Î)
                            diff.Add(Math.Abs(Y1 - 100 * 1.4));//good(Ð)
                            diff.Add(Math.Abs(Y1 - 100 * 1.77));//bye
                            k1 = diff.IndexOf(Min(diff));
                            if (!back_gesture.Equals(alphabet[k1], StringComparison.Ordinal))
                            {
                                message += " ";
                                message += alphabet[k1];
                                back_gesture = alphabet[k1];
                            }
                        }

                        objectsBoundaryLabel.Text = string.Format("Left:{0}, Top:{1}, Right:{2}, Bottom:{3}, Message:{4}", a.Left, a.Top, a.Right, a.Bottom, message);
                        counter = 0;
                    }
                    else
                    {
                        counter++;
                    }
                    Color newColor = _colorDetection.ObjectColor;

                        BeginInvoke(_changeColorDelegate, newColor);

                        // accumulate histor
                        motionHistory.Add(motionLevel);
                        if (motionHistory.Count > 50000)
                        {
                            motionHistory.RemoveAt(0);
                        }
                    }
                }
                
        }

        // Draw motion history
        private void DrawMotionHistory(Bitmap image)
        {
            Color greenColor = Color.FromArgb(128, 0, 255, 0);
            Color yellowColor = Color.FromArgb(128, 255, 255, 0);
            Color redColor = Color.FromArgb(128, 255, 0, 0);

            BitmapData bitmapData = image.LockBits(new Rectangle(0, 0, image.Width, image.Height),
                ImageLockMode.ReadWrite, image.PixelFormat);

            int t1 = (int)(motionAlarmLevel * 500);
            int t2 = (int)(0.075 * 500);

            for (int i = 1, n = motionHistory.Count; i <= n; i++)
            {
                int motionBarLength = (int)(motionHistory[n - i] * 500);

                if (motionBarLength == 0)
                    continue;

                if (motionBarLength > 50)
                    motionBarLength = 50;

                Drawing.Line(bitmapData,
                    new IntPoint(image.Width - i, image.Height - 1),
                    new IntPoint(image.Width - i, image.Height - 1 - motionBarLength),
                    greenColor);

                if (motionBarLength > t1)
                {
                    Drawing.Line(bitmapData,
                        new IntPoint(image.Width - i, image.Height - 1 - t1),
                        new IntPoint(image.Width - i, image.Height - 1 - motionBarLength),
                        yellowColor);
                }

                if (motionBarLength > t2)
                {
                    Drawing.Line(bitmapData,
                        new IntPoint(image.Width - i, image.Height - 1 - t2),
                        new IntPoint(image.Width - i, image.Height - 1 - motionBarLength),
                        redColor);
                }
            }

            image.UnlockBits(bitmapData);
        }
        
        // On timer event - gather statistics
        private void timer_Tick(object sender, EventArgs e)
        {
            IVideoSource videoSource = videoSourcePlayer.VideoSource;

            if (videoSource != null)
            {
                // get number of frames for the last second
                statCount[statIndex] = videoSource.FramesReceived;

                // increment indexes
                if (++statIndex >= statLength)
                    statIndex = 0;
                if (statReady < statLength)
                    statReady++;

                float fps = 0;

                // calculate average value
                for (int i = 0; i < statReady; i++)
                {
                    fps += statCount[i];
                }
                fps /= statReady;

                statCount[statIndex] = 0;

                fpsLabel.Text = fps.ToString("F2") + " fps";
            }
        }

        // Turn off motion detection
        private void noneToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            motionDetectionType = 0;
            SetMotionDetectionAlgorithm(null);
        }

        // Turn off motion processing
        private void noneToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            motionProcessingType = 0;
            SetMotionProcessingAlgorithm(null);
        }

        // Set motion area highlighting
        private void motionAreaHighlightingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            motionProcessingType = 1;
            SetMotionProcessingAlgorithm(new MotionAreaHighlighting());
        }

        // Set motion borders highlighting
        private void motionBorderHighlightingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            motionProcessingType = 2;
            SetMotionProcessingAlgorithm(new MotionBorderHighlighting());
        }

        // Set objects' counter
        private void blobCountingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            motionProcessingType = 3;
            SetMotionProcessingAlgorithm(new BlobCountingObjectsProcessing());
        }

        // Set grid motion processing
        private void gridMotionAreaProcessingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            motionProcessingType = 4;
            SetMotionProcessingAlgorithm(new GridMotionAreaProcessing(32, 32));
        }

        // Set new motion detection algorithm
        private void SetMotionDetectionAlgorithm(IMotionDetector detectionAlgorithm)
        {
            lock (this)
            {
                detector.MotionDetectionAlgorthm = detectionAlgorithm;
                motionHistory.Clear();

                if (detectionAlgorithm is TwoFramesDifferenceDetector)
                {
                    if (
                        (detector.MotionProcessingAlgorithm is MotionBorderHighlighting) ||
                        (detector.MotionProcessingAlgorithm is BlobCountingObjectsProcessing))
                    {
                        motionProcessingType = 1;
                        SetMotionProcessingAlgorithm(new MotionAreaHighlighting());
                    }
                }
            }
        }

        // Set new motion processing algorithm
        private void SetMotionProcessingAlgorithm(IMotionProcessing processingAlgorithm)
        {
            lock (this)
            {
                detector.MotionProcessingAlgorithm = processingAlgorithm;
            }
        }

        // On opening of Tools menu
        private void toolsToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            localVideoCaptureSettingsToolStripMenuItem.Enabled =
                ((videoSource != null) && (videoSource is VideoCaptureDevice));
        }

        // Display properties of local capture device
        private void localVideoCaptureSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if ((videoSource != null) && (videoSource is VideoCaptureDevice))
            {
                try
                {
                    ((VideoCaptureDevice)videoSource).DisplayPropertyPage(this.Handle);
                }
                catch (NotSupportedException)
                {
                    MessageBox.Show("The video source does not support configuration property page.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // Timer used for flashing in the case if motion is detected
        private void alarmTimer_Tick(object sender, EventArgs e)
        {
            if (flash != 0)
            {
                videoSourcePlayer.BorderColor = (flash % 2 == 1) ? Color.Black : Color.Red;
                flash--;
            }
        }

        private void colorTrackingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetMotionDetectionAlgorithm(_colorDetection);
        }

        private void defineColorTrackingObjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (videoSourcePlayer.VideoSource != null)
            {
                Bitmap currentVideoFrame = videoSourcePlayer.GetCurrentVideoFrame();

                if (currentVideoFrame != null)
                {
                    MotionRegionsForm form = new MotionRegionsForm();
                    form.VideoFrame = currentVideoFrame;
                    //form.MotionRectangles = detector.MotionZones;

                    Bitmap temp = currentVideoFrame;

                    // show the dialog
                    if (form.ShowDialog(this) == DialogResult.OK)
                    {
                        Rectangle[] rects = form.MotionRectangles;
                        if (rects.Length == 0)
                            return;

                        _colorDetection.Reset();
                        _colorDetection.Initialize(temp, form.MotionRectangles[0]);
                    }
                    return;
                }
            }

            MessageBox.Show("It is required to start video source and receive at least first video frame before setting motion zones.",
                "Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void colorDifference_TextChanged(object sender, EventArgs e)
        {
            int value;
            if (int.TryParse(colorDifference.Text, out value))
            {
                _colorDetection.DifferenceThreshold = value;
            }
        }

        private void comboCalculateNewColor_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboCalculateNewColor.SelectedItem.ToString() == "Yes")
            {
                _colorDetection.DynamicColorTracking = true;
            }
            else
            {
                _colorDetection.DynamicColorTracking = false;
            }
        }
    }
}