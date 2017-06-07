using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OpenCvSharp;
using OpenCvSharp.Extensions;

using System.IO;
using System.Diagnostics;

using System.Threading;

using System.Xml;
using System.Xml.Schema;


namespace GPUMonitor
{
    public partial class Form1 : Form
    {

        System.Threading.Thread t = null;

        public Bitmap capturedScreen;
        private Bitmap bmp;
        private Graphics g;
        private Bitmap icon;
        private Bitmap obj0;
        private Bitmap obj1;
        string score = "";
        string key = null;
        private Object thisLock = new Object();
        private State threadState = new State();
        private Bitmap lastBitmap = null;
        private string targetAppName = null;

        public class ImageObject
        {
            private string objectName = null;
            private Rectangle rect;
            private Bitmap bitmap;
            private double scoreThreshold;
            private static Graphics parentGraphics;
            private static Bitmap parentBitmap;
            private static string debugScore;
            private static Bitmap debugBitmap;

            static public void setParent(Graphics graphics, Bitmap bitmap, string score, Bitmap lastBitmap)
            {
                parentBitmap = bitmap;
                parentGraphics = graphics;
                debugScore = score; // TODO
                debugBitmap = lastBitmap;
            }

            static public ImageObject create(string objectName, Rectangle rectOnParent, double scoreThreshold, string path)
            {
                ImageObject obj = new ImageObject();
                obj.objectName = objectName;
                obj.rect = rectOnParent;
                obj.scoreThreshold = scoreThreshold;
                
                obj.bitmap = new Bitmap(path);

                return obj;
            }

            public bool findImageInScreen(Object lockObject, bool update = true)
            {
                captureScreen();
                return findImageIn(parentBitmap, lockObject, update);
            }

            public bool findImageIn(Bitmap targetBitmap, Object lockObject, bool update)
            {
                Bitmap croppedScreenBitmap = parentBitmap.Clone(rect, parentBitmap.PixelFormat);

                IplImage screenImage = BitmapConverter.ToIplImage(croppedScreenBitmap);
                IplImage targetImage = BitmapConverter.ToIplImage(targetBitmap);

                CvSize resSize = new CvSize(screenImage.Width - targetImage.Width + 1, screenImage.Height - targetImage.Height + 1);
                IplImage resImg = Cv.CreateImage(resSize, BitDepth.F32, 1);

                Cv.MatchTemplate(screenImage, targetImage, resImg, MatchTemplateMethod.SqDiffNormed);

                double minVal;
                double maxVal;
                CvPoint minLoc;
                CvPoint maxLoc;

                Cv.MinMaxLoc(resImg, out minVal, out maxVal, out minLoc, out maxLoc);
                string val = minVal.ToString("0.00000");
               debugScore = val;
                lock (lockObject)
                {
                    if (update)
                        debugBitmap = croppedScreenBitmap;

                    if ((minVal < scoreThreshold))
                    {
                        if (update)
                        {
                            Graphics g = Graphics.FromImage(croppedScreenBitmap);
                            g.DrawRectangle(new Pen(Color.Red, 2), new Rectangle(minLoc.X, minLoc.Y, targetImage.Width, targetImage.Height));
                        }
                        return true;
                    }
                    return false;
                }
            }

            private void captureScreen()
            {
                parentGraphics.CopyFromScreen(new Point(rect.Left, rect.Top), new Point(rect.Left, rect.Top), rect.Size);
            }
        }

        public Form1()
        {
            InitializeComponent();
//            this.TopMost = true;

            string xmlPath = "config.xml";

            XmlDocument configXml = new XmlDocument();

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Parse;
            settings.ValidationType = ValidationType.DTD;

            XmlReader reader = XmlReader.Create(xmlPath, settings);
            try
            {
                configXml.Load(reader);
            }
            catch (XmlSchemaValidationException e)
            {
                MessageBox.Show(e.Message, "Illigal setting in config.xml", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            targetAppName = configXml.SelectSingleNode("/configuration/application/processname").InnerText;
                


            bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            g = Graphics.FromImage(bmp);

            icon = new Bitmap(@"icon.bmp");

            XmlNodeList nodes = configXml.SelectNodes("/configuration/objects/object");

            obj0 = new Bitmap(nodes[0].SelectSingleNode("file").InnerText); // casting_icon.bmp
            obj1 = new Bitmap(nodes[1].SelectSingleNode("file").InnerText); // hp_bar.bmp
        }

        private delegate void threadObject();

        private void reset(threadObject func)
        {
            stop();
            threadState.setState(State.threadState.ACTIVE);
            t = new System.Threading.Thread(new System.Threading.ThreadStart(func));
            t.Start();
        }

        private void stop()
        {
            if (t != null)
            {
                threadState.setState(0);
                t.Join();
            }
        }



        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            g.Dispose();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
#if false

            MODI.Document doc = new MODI.Document();
            doc.Create(@"test.bmp");
            doc.OCR(MODI.MiLANGUAGES.miLANG_ENGLISH, false, false);

            StringBuilder str = new StringBuilder();

            for (int i = 0; i < doc.Images.Count; i++)
            {
                MODI.Image img = (MODI.Image)doc.Images[i];
                MODI.Layout layout = img.Layout;

                Console.WriteLine(layout.Text);
                Console.WriteLine();

                for (int j = 0; j < layout.Words.Count; j++)
                {
                    MODI.Word word = (MODI.Word)layout.Words[j];
                    str.Append("[" + word.Text + "]");
                }
            }
            StreamWriter outfile = new StreamWriter(@"ocr.txt");
            outfile.Write(str.ToString());
            outfile.Close();
#endif

        }

        // timer1 object is placed in Form1.cs (Design)
        private void timer1_Tick(object sender, EventArgs e)
        {
            string appName = GetActiveApplicationName();
            label1.Text = appName;
            label2.Text = score;

            //Rectangle rect = new Rectangle(1000, 850, 100, 100);
            //g.CopyFromScreen(new Point(rect.Left, rect.Top), new Point(rect.Left, rect.Top), rect.Size);
            //Bitmap croppedScreenBitmap = screenBitmap.Clone(searchRect, screenBitmap.PixelFormat);
            //lastBitmap = croppedScreenBitmap;

            //Rectangle rect = Screen.PrimaryScreen.Bounds;
            //bmp = new Bitmap(rect.Width, rect.Height);
            //Graphics g = Graphics.FromImage(bmp);
            //g.CopyFromScreen(rect.X, rect.Y, 0, 0, rect.Size);
            //lastBitmap = bmp;

            //            pictureBox1.Image = lastBitmap;

            if (lastBitmap != null)
            {
                lock(thisLock)
                {
                    pictureBox1.Image = lastBitmap;
                    lastBitmap = null;
                }
            }

            if (appName == targetAppName)
            {
                threadState.setState(State.threadState.ACTIVE);
                lock(thisLock)
                {
                    if (key != null)
                    {
                        SendKeys.Send(key);
                        key = null;
                    }
                }
            }
            else
            {
                threadState.setState(State.threadState.SUSPENDED);
            }
        }

        class State
        {
            public enum threadState {EXIT, SUSPENDED, ACTIVE };
            private threadState state = threadState.EXIT;
            private Object stateLock = new Object();

            public void setState(threadState state)
            {
                lock (stateLock)
                {
                    this.state = state;
                }
            }

            public bool waitState()
            {
                while (true)
                {
                    lock (stateLock)
                    {
                        if (state == threadState.EXIT)
                            return true;
                        if (state == threadState.ACTIVE)
                            return false;
                    }
                    Thread.Sleep(500);
                }
            }
        }


        private void macro1()
        {
            mainLoop(false);
        }

        private void macro2()
        {
            mainLoop(true);
        }

        private void mainLoop(bool isNegative)
        {
            Console.WriteLine("INFO: Thread has been started");
            while (true)
            {
                if (threadState.waitState())
                    return;

                while (true)
                {
                    if (threadState.waitState())
                        return;

                    if (findTemplate(obj0, new Rectangle(1000, 850, 100, 100), 0.05))
                    {
                        lock (thisLock)
                        {
                            key = "8";
                        }
                        break;
                    }
                    Thread.Sleep(500);
                }

                Thread.Sleep(1000);

                if (findTemplate(obj0, new Rectangle(1000, 850, 100, 100), 0.05))
                    continue;

                Thread.Sleep(5000);

                while (true)
                {
                    if (threadState.waitState())
                        return;

                    if (findTemplate(obj0, new Rectangle(1000, 850, 100, 100), 0.05, false))
                        break;

                    if (isNegative ^ findTemplate(obj1, new Rectangle(908, 410, 100, 200), 0.0005))
                    {
                        lock (thisLock)
                        {
                            key = "8";
                        }
                        break;
                    }
                    Thread.Sleep(100);
                }
                Thread.Sleep(500);
            }
        }

        private bool findTemplate(Bitmap image, Rectangle rect, double th, bool update = true)
        {
            g.CopyFromScreen(new Point(rect.Left, rect.Top), new Point(rect.Left, rect.Top), rect.Size);
            return findImage(bmp, image, rect, th, update);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        private string GetActiveApplicationName()
        {
            IntPtr hWnd = GetForegroundWindow();

            int processid;
            GetWindowThreadProcessId(hWnd, out processid);

            return Process.GetProcessById(processid).ProcessName;
        }

        private bool findImage(Bitmap screenBitmap, Bitmap targetBitmap, Rectangle searchRect, double th, bool update)
        {
            Bitmap croppedScreenBitmap = screenBitmap.Clone(searchRect, screenBitmap.PixelFormat);

            IplImage screenImage = BitmapConverter.ToIplImage(croppedScreenBitmap);
            IplImage targetImage = BitmapConverter.ToIplImage(targetBitmap);

            CvSize resSize = new CvSize(screenImage.Width - targetImage.Width + 1, screenImage.Height - targetImage.Height + 1);
            IplImage resImg = Cv.CreateImage(resSize, BitDepth.F32, 1);

            Cv.MatchTemplate(screenImage, targetImage, resImg, MatchTemplateMethod.SqDiffNormed);

            double minVal = 0.0;
            double maxVal = 0.0;
            CvPoint minLoc;
            CvPoint maxLoc;

            Cv.MinMaxLoc(resImg, out minVal, out maxVal, out minLoc, out maxLoc);
            string val = minVal.ToString("0.00000");
            score = val;
            lock(thisLock)
            {
                if (update)
                {
                    lastBitmap = croppedScreenBitmap;
                }

                if ((minVal < th))
                {
                    if (update)
                    {
                        Graphics g = Graphics.FromImage(croppedScreenBitmap);
                        g.DrawRectangle(new Pen(Color.Red, 2), new Rectangle(minLoc.X, minLoc.Y, targetImage.Width, targetImage.Height));
                    }
                    return true;
                }
                return false;

            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Console.WriteLine("INFO: STOP");
            stop();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Console.WriteLine("INFO: MACRO1");
            reset(macro1);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Console.WriteLine("INFO: MACRO2");
            reset(macro2);
        }
    }
}
