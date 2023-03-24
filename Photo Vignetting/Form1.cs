using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Threading;
using System.Diagnostics;
using System.Collections;


namespace Photo_Vignetting
{
    public partial class Form1 : Form
    {
        private Bitmap mainBitmap;

        //timer
        Stopwatch stopwatch = new Stopwatch();

        //component initialization and prepairing threads info
        public Form1()
        {
            InitializeComponent();

            int threadsNumber = Environment.ProcessorCount;
            trackBar1.Value = threadsNumber;
            label3.Text = Convert.ToString(threadsNumber);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        //file opening with regex
        private void button1_Click(object sender, EventArgs e)
        {

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "jpgs|*.jpg|png|*.png|Bitmaps|*.bmp";

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string rexgexPattern = @"\b.(jpg)?(png)?(gif)?\b";
                string extention = dialog.SafeFileName;
                Match matching = Regex.Match(extention, rexgexPattern, RegexOptions.IgnoreCase);

                if (!matching.Success)
                    MessageBox.Show("Incorrect extension", "Error!!!");

                mainBitmap = new Bitmap(Image.FromFile(dialog.FileName));
                pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
                pictureBox1.Image = mainBitmap;
            }
            else
                MessageBox.Show("Something is wrong with the file", "Error!!!");
        }


        //shows current chosen thread amount
        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            label3.Text = Convert.ToString(trackBar1.Value);
        }

        //shows filter strength
        private void trackBar2_Scroll(object sender, EventArgs e)
        {
            label9.Text = Convert.ToString(trackBar2.Value);
        }

        //Disable visibility of the textbox with results for the "Run Test" option.
        private void button2_Click(object sender, EventArgs e)
        {
            textBox1.Visible = false;
            start();
        }





        //Starting pricedure, we prepair everything to work fine (threads, bitmap, timer)
        private void start()
        {
            if (pictureBox1.Image == null)
            {
                MessageBox.Show("Select an Image!", "Error!!!");
            }
            else
            {
                ThreadPool.SetMaxThreads(trackBar1.Value, trackBar1.Value); // thread amount

                Bitmap temp = new Bitmap(100, 100); //temporary working bitmap

                using (Graphics graf = Graphics.FromImage(temp)) //making 100x100 square
                {
                    Rectangle dimensions = new Rectangle(0, 0, 100, 100);
                    graf.FillRectangle(Brushes.White, dimensions);
                }

                Bitmap tempMap = temp;

                label4.Text = "made with C#";

                stopwatch.Start();
                tempMap = Begin(mainBitmap); //here we will begin the procedure
                stopwatch.Stop();

                //showing time
                label7.Text = stopwatch.ElapsedMilliseconds.ToString() + "ms";
                if (checkBox3.Checked == true) label7.Text = stopwatch.ElapsedMilliseconds.ToString();
                stopwatch.Reset();

                //showing output image
                pictureBox2.Image = tempMap;
                pictureBox2.SizeMode = PictureBoxSizeMode.StretchImage;
            }

        }

        //below we start filter part connected with math, it is required to get a proper filter parameters
        public unsafe Bitmap Begin(Bitmap inputIMG)
        {
            //New bitmap, it will be returned as the processed image.
            Bitmap outputIMG = new Bitmap(inputIMG);

            double halfX = inputIMG.Width / 2; // half of the width
            double halfY = inputIMG.Height / 2; // half of the height

            double circle; // this is the small circle (or rather its radius) in vignetting, outside of which the filter will be applied.
            if (halfX > halfY)
                circle = halfY;
            else
                circle = halfX;

            double str = trackBar2.Value;
            circle *= str / 10; //to make it vivible, before this str value is not fitting

            //the farthest point from the circle
            double maxRadius = ((halfX * halfX) + (halfY * halfY) - (circle * circle));

            //thread synchronization.
            var eventt = new ManualResetEvent[1];
            eventt[0] = new ManualResetEvent(false);
            int threadsAmount = trackBar1.Value; //thread amount taken from trackBar

            //we set the attributes of the bitmap image and close it in the memory.
            BitmapData newImage = outputIMG.LockBits(new Rectangle(0, 0, outputIMG.Width, outputIMG.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

            //setting pointer on first pixel
            byte* pointer = (byte*)newImage.Scan0;

            //we calculate a variable that will help set the pointer at the end of the row of pixels in the image
            //this will allow us to move to a new line without skipping any bits.
            int correctRow = 4 - ((outputIMG.Width * 3) % 4);
            if (correctRow == 4) correctRow = 0; //if it landed perfectly at the end of the row.

            //the size in bits of a single row, adjusted to a multiple of 4 bits to prevent going out of bounds of the image.
            int rowBitsAmount = correctRow + outputIMG.Width * 3;

            //determining the number of rows a single thread should process.
            int rowsPerThread = inputIMG.Height / threadsAmount;

            // This variable is added to the last thread because there is a chance that the number of rows is not divisible by the number of threads.
            int correctHight = inputIMG.Height % threadsAmount;

            //primitive synchronization type, important to make the thread staff work fine
            var finish = new CountdownEvent(1);

            //heignt for every working thread
            int height = 0;


            for (int x = 0; x < threadsAmount; x++) // Here, there will be a separate task for each thread.
            {
                finish.AddCount(); //increment synchronisation type

                //We take the last bit in the last row where the thread should stop.
                byte* lastBit = pointer + rowBitsAmount * rowsPerThread;
                if (x == (threadsAmount - 1)) //correcting last bit
                    lastBit += rowBitsAmount * correctHight;
                lastBit--;

                // It's better to store the data in a class instead of calling a method multiple times with many parameters.
                // Instead, the method (doFilter) will be called for an object.
                FilterClass imageClass = new FilterClass(pointer, lastBit, maxRadius, correctRow, x, height, halfX, halfY, circle);
                ThreadPool.QueueUserWorkItem((state) =>
                {
                    try
                    {
                        imageClass.doFilter(state);
                    }
                    finally
                    {
                        finish.Signal(); //additional decrementation
                    }
                },
                    x);

                // to calculate the height correctly for each thread
                height += rowsPerThread;
                pointer = lastBit + 1;
            }

            finish.Signal();
            // return when the countdown reaches 0
            finish.Wait();
            //unlock bitmap
            outputIMG.UnlockBits(newImage);
            //returns bitmap with a ready image
            return outputIMG;
        }






        //Here will be stored information about the duration of procedures, which will then be printed in a textbox
        private ArrayList statistics = new ArrayList();

        //below there is a time showing method
        //It will call the procedure for several threads, save the results in an array list, and put it in the textbox.
        //It will be called and perform image processing for 1, 2, 4, 8, 16, 32, 64 threads in sequence.
        private void button3_Click(object sender, EventArgs e)
        {
            textBox1.Visible = true;
            textBox1.Clear();
            statistics.Clear();
            int threads = 1;
            String temptxt = "";

            if (checkBox3.Checked == false)
            {
                while (threads <= 64)
                {
                    trackBar1.Value = threads; //threads from trackbar
                    start();
                    temptxt = " threads: " + threads.ToString();
                    temptxt += " time: ";
                    temptxt += label7.Text;

                    statistics.Add(temptxt);
                    temptxt = "";
                    threads = threads * 2;
                }
            }
            //if no description option is checked,it will print only values (usefull to copy while making statistics)
            if (checkBox3.Checked == true)
            {
                while (threads <= 64)
                {
                    trackBar1.Value = threads;

                    start();
                    temptxt += label7.Text;
                    statistics.Add(temptxt);
                    temptxt = "";
                    threads = threads * 2;
                }
            }

            for (int y = 0; y < statistics.Count; y++)
            {
                temptxt = statistics[y].ToString();
                textBox1.Text += (temptxt + Environment.NewLine);
            }
        }


        //saving new picture
        private void button4_Click(object sender, EventArgs e)
        {
            Image picture2 = pictureBox2.Image;

            if (picture2 != null)
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "Image files (*.bmp;*.jpg;*.jpeg;*.png)|*.bmp;*.jpg;*.jpeg;*.png|All files (*.*)|*.*";
                saveFileDialog.RestoreDirectory = true;

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = saveFileDialog.FileName;
                    picture2.Save(filePath, ImageFormat.Jpeg);
                }
            }
            else
            {
                MessageBox.Show("No output image!!", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
