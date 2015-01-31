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
        private bool isActive = false;
        string score = "";
        string key = null;
        private Object thisLock = new Object();
        private State threadState = new State();
        private Bitmap lastBitmap = null;
        private string targetAppName = null;

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
            icon = new Bitmap(@"icon.bmp");

            XmlNodeList nodes = configXml.SelectNodes("/configuration/objects/object");

            obj0 = new Bitmap(nodes[0].SelectSingleNode("file").InnerText);
            obj1 = new Bitmap(nodes[1].SelectSingleNode("file").InnerText);

            
//            castingIcon = new Bitmap(@"casting_icon.bmp");
//            hpBar = new Bitmap(@"hp_bar.bmp");
            g = Graphics.FromImage(bmp);

//            reset();
        }

        private void reset()
        {
            stop();
            threadState.setState(1);
            t = new System.Threading.Thread(new System.Threading.ThreadStart(mainLoop));
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
#if true

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

        private void timer1_Tick(object sender, EventArgs e)
        {
            string appName = GetActiveApplicationName();
            label1.Text = appName;
            label2.Text = score;

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
                isActive = true;
                threadState.setState(2);
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
                threadState.setState(1);
                isActive = false;
            }
        }

        class State
        {
            private int state = 0;
            private Object stateLock = new Object();

            public void setState(int state)
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
                        if (state == 0)
                            return true;
                        if (state == 2)
                            return false;
                    }
                    Thread.Sleep(500);
                }
            }
        }

        private void mainLoop()
        {
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
                {
                    return;
                }

                Thread.Sleep(5000);

                while (true)
                {
                    if (threadState.waitState())
                        return;

                    if (findTemplate(obj0, new Rectangle(1000, 850, 100, 100), 0.05, false))
                        break;

                    if (!findTemplate(obj1, new Rectangle(908, 410, 100, 200), 0.0005))
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

            double minVal;
            double maxVal;
            CvPoint minLoc;
            CvPoint maxLoc;

            Cv.MinMaxLoc(resImg, out minVal, out maxVal, out minLoc, out maxLoc);
            string val = minVal.ToString("00000000.00000");
            score = val;
            lock(thisLock)
            {
                if (update)
                    lastBitmap = croppedScreenBitmap;

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
            stop();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            reset();
        }
    }
}
