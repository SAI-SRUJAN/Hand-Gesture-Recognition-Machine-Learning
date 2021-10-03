using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.Util;
using Emgu.CV.Structure;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Accord.Imaging;
using AForge.Imaging.Textures;
using Accord.Statistics.Visualizations;
using System.IO;
using AForge.Controls;
using SVM;
using AForge.Video;
using AForge.Video.DirectShow;
using AForge.Video;
using AForge.Video.DirectShow;
using AForge;
using AForge.Imaging.Filters;

namespace Gesture_Recognition
{
    public partial class Form1 : Form
    {
        public Bitmap srcImg, dstImg;
        public string path = @"E:\Images";
        public IntPoint blob;
        private FilterInfoCollection CaptureDevice;
        private VideoCaptureDevice FinalFrame;
        public static Bitmap latestFrame;
        public Form1()
        {
            InitializeComponent();
        }
        public static Bitmap resize(Bitmap image, Size size)
        {
            return (Bitmap)(new Bitmap(image, size));
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            CaptureDevice = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach(FilterInfo Device in CaptureDevice)
            {
                comboBox2.Items.Add(Device.Name);
            }
            comboBox2.SelectedIndex = 0;
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }
        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            FinalFrame = new VideoCaptureDevice(CaptureDevice[comboBox2.SelectedIndex].MonikerString);
            FinalFrame.NewFrame += new NewFrameEventHandler(FinalFrame_NewFrame);
            FinalFrame.Start();

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(FinalFrame.IsRunning==true)
            {
                FinalFrame.Stop();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
                pictureBox2.Image = (Bitmap)pictureBox1.Image.Clone();
                Bitmap src = new Bitmap(pictureBox2.Image);
                Bitmap res = new Bitmap(pictureBox2.Image);
                SaveFileDialog saveDialog = new SaveFileDialog();
                src = resize(src, new Size(200, 200));
                res = resize(res, new Size(200, 200));
                pictureBox2.Image = src;
                srcImg = src;
                pictureBox2.Image = res;
                Bitmap sampleImage = new Bitmap(pictureBox2.Image);
                var rect = new Rectangle(0, 0, sampleImage.Width, sampleImage.Height);
                var data = sampleImage.LockBits(rect, ImageLockMode.ReadWrite, sampleImage.PixelFormat);
                var depth = Bitmap.GetPixelFormatSize(data.PixelFormat) / 8; //bytes per pixel

                var buffer = new byte[data.Width * data.Height * depth];

                //copy pixels to buffer
                Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

                System.Threading.Tasks.Parallel.Invoke(
                    () =>
                    {
                        //upper-left
                        Process(buffer, 0, 0, data.Width / 2, data.Height / 2, data.Width, depth);
                    },
                    () =>
                    {
                        //upper-right
                        Process(buffer, data.Width / 2, 0, data.Width, data.Height / 2, data.Width, depth);
                    },
                    () =>
                    {
                        //lower-left
                        Process(buffer, 0, data.Height / 2, data.Width / 2, data.Height, data.Width, depth);
                    },
                    () =>
                    {
                        //lower-right
                        Process(buffer, data.Width / 2, data.Height / 2, data.Width, data.Height, data.Width, depth);
                    }
                );

                //Copy the buffer back to image
                Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);

                sampleImage.UnlockBits(data);
                pictureBox2.Image = sampleImage;
                dstImg = sampleImage;
                void Process(byte[] buffer1, int x, int y, int endx, int endy, int width, int depth1)
                {
                    for (int i = x; i < endx; i++)
                    {
                        for (int j = y; j < endy; j++)
                        {
                            var offset = ((j * width) + i) * depth;
                            var B = buffer[offset + 0];
                            var G = buffer[offset + 1];
                            var R = buffer[offset + 2];
                            var a = Math.Max(R, Math.Max(B, G));
                            var b = Math.Min(R, Math.Min(B, G));
                            if (!(((R > 95) && (G > 40) && (B > 20) && ((a - b) > 15) && (Math.Abs(R - G) > 15) && (R > G) && (R > B)) || ((R > 220) && (G > 210) && (B > 170) && ((a - b) > 15) && (Math.Abs(R - G) > 15) && (R > G) && (G > B))))
                            {
                                buffer[offset + 0] = buffer[offset + 1] = buffer[offset + 2] = 0;
                            }
                            else
                            {
                                buffer[offset + 0] = buffer[offset + 1] = buffer[offset + 2] = 255;
                            }
                        }
                    }
                }


                //Graysacle
                GrayscaleBT709 filter = new GrayscaleBT709();
                pictureBox2.Image = filter.Apply((Bitmap)pictureBox2.Image);
                dstImg = filter.Apply(dstImg);
                //Dilatation
                try
                {
                    Dilatation filter1 = new Dilatation();
                    pictureBox2.Image = filter1.Apply((Bitmap)pictureBox2.Image);
                    dstImg = filter1.Apply(dstImg);
                }
                catch (Exception)
                {
                    System.Windows.Forms.MessageBox.Show("Apply Grayscale");
                }
                //Biggest Blob Extraction
                ExtractBiggestBlob filter2 = new ExtractBiggestBlob();
                pictureBox2.Image = filter2.Apply((Bitmap)pictureBox2.Image);
                dstImg = filter2.Apply(dstImg);
                blob = filter2.BlobPosition;
                Bitmap newBmp = new Bitmap(dstImg.Width, dstImg.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (Graphics gfx = Graphics.FromImage(newBmp))
                {
                    gfx.DrawImage(dstImg, 0, 0);
                }
                //newBmp = dstImg;
                for (int i = 0; i < dstImg.Width; i++)
                {
                    for (int j = 0; j < dstImg.Height; j++)
                    {
                        System.Drawing.Color srcColor = srcImg.GetPixel(i + blob.X, j + blob.Y);
                        System.Drawing.Color dstColor = dstImg.GetPixel(i, j);
                        if (!(dstColor.R >= 0 && dstColor.R <= 10 && dstColor.G >= 0 && dstColor.G <= 10 && dstColor.B >= 0 && dstColor.B <= 10))
                        {
                            newBmp.SetPixel(i, j, srcColor);
                        }

                    }
                }
                dstImg = newBmp;
                pictureBox2.Image = newBmp;

                List<double> edgeCount = new List<double>();
                List<double> ratio = new List<double>();
                int pixelCount = 0;

                Bitmap hoefImage = new Bitmap(pictureBox2.Image);
                GrayscaleBT709 grayFilter = new GrayscaleBT709();
                hoefImage = grayFilter.Apply((Bitmap)pictureBox2.Image);
                CannyEdgeDetector cannyFilter = new CannyEdgeDetector(0, 0, 1.4);
                hoefImage = cannyFilter.Apply(hoefImage);
                pictureBox2.Image = hoefImage;
                var imgarray = new System.Drawing.Image[36];
                for (int i = 0; i < 6; i++)
                {
                    for (int j = 0; j < 6; j++)
                    {
                        pixelCount++;
                        var index = i * 6 + j;
                        imgarray[index] = new Bitmap(40, 40);
                        var graphics = Graphics.FromImage(imgarray[index]);
                        graphics.DrawImage(hoefImage, new Rectangle(0, 0, 40, 40), new Rectangle(i * 40, j * 40, 40, 40), GraphicsUnit.Pixel);
                        graphics.Dispose();
                    }
                }
                for (int n = 0; n < 36; n++)
                {
                    int counter = 0;
                    Bitmap bufferImage = new Bitmap(imgarray[n]);
                    for (int i = 0; i < 40; i++)
                    {
                        for (int j = 0; j < 40; j++)
                        {
                            System.Drawing.Color hoefColor = bufferImage.GetPixel(i, j);
                            if (!(hoefColor.R == 0 && hoefColor.G == 0 && hoefColor.B == 0))
                            {
                                counter++;
                            }
                        }
                    }
                    edgeCount.Add(counter);

                }
                double Total = edgeCount.Sum();
                foreach (double x in edgeCount)
                {

                    var a = x / Total;
                    ratio.Add(a);

                }

                FileStream fs = new FileStream(@"E:\test.txt", FileMode.Create, FileAccess.Write);
                StreamWriter sw = new StreamWriter(fs);
                int no = 0;
                sw.Write((++no) + " ");
                for (int i = 0; i < ratio.Count; ++i)
                {
                    sw.Write(i + ":" + ratio[i].ToString() + " ");
                }
                sw.WriteLine();

                sw.Close();
                fs.Close();
                //Support Vector Machine
                Problem train = Problem.Read(@"E:\AI.txt");
                Problem test = Problem.Read(@"E:\test.txt");

                Parameter parameters = new Parameter();

                double C;
                double Gamma;

                parameters.C = 32; parameters.Gamma = 8;
                Model model = Training.Train(train, parameters);
                Prediction.Predict(test, @"E:\result.txt", model, false);

            FileStream fs1 = new FileStream(@"E:\result.txt", FileMode.Open, FileAccess.Read);
            StreamReader sw1 = new StreamReader(fs1);
            string w = sw1.ReadLine();
            if (w =="1") { MessageBox.Show("A"); }
            else if (w == "2") { MessageBox.Show("B"); }
            else if (w == "3") { MessageBox.Show("C"); }
            else if (w == "4") { MessageBox.Show("D"); }
            else if (w == "5") { MessageBox.Show("E"); }
            else if (w == "6") { MessageBox.Show("F"); }
            else if (w == "7") { MessageBox.Show("G"); }
            else if (w == "8") { MessageBox.Show("H"); }
            else if (w == "9") { MessageBox.Show("I"); }
            else if (w == "10") { MessageBox.Show("J"); }
            else if (w == "11") { MessageBox.Show("K"); }
            //else { MessageBox.Show("L"); }
        }
            

        private void pictureBox3_Click(object sender, EventArgs e)
        {
            FileStream fs = new FileStream(@"E:\result.txt", FileMode.Open, FileAccess.Read);
            StreamReader sw = new StreamReader(fs);
            int i=sw.Read();
            if (i == 1) { MessageBox.Show("A"); }
            else if (i == 2) { MessageBox.Show("B"); }
            else if (i == 3) { MessageBox.Show("C"); }
            else if (i == 4) { MessageBox.Show("D"); }
            else if (i == 5) { MessageBox.Show("E"); }
            else if (i == 6) { MessageBox.Show("F"); }
            else if (i == 7) { MessageBox.Show("G"); }
            else if (i == 8) { MessageBox.Show("H"); }
            else if (i == 9) { MessageBox.Show("I"); }
            else if (i == 10) { MessageBox.Show("J"); }
            else if (i == 11) { MessageBox.Show("K"); }
            else  { MessageBox.Show("L"); }
        }



        private void FinalFrame_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            Bitmap cameraImage = (Bitmap)eventArgs.Frame.Clone();
            pictureBox1.Image = cameraImage;
        }
    }
}

