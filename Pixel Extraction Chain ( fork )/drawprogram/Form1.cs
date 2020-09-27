using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.IO;

namespace drawprogram
{
    public partial class Form1 : Form
    {  
        Bitmap myDraw = new Bitmap(1000, 1000); // only allow a 1000 on 1000 bitmap ( but it doesnt matter ) .... 
        Graphics g;
        PictureBox drawBox = new PictureBox();
        public static string _folderPath = "";
        public List<PixelWrapper.Vector> UnvalidatedPixel = new List<PixelWrapper.Vector>();
        public static List<List<PixelWrapper.Vector>> ProccessedPixel = new List<List<PixelWrapper.Vector>>();
        public List<PixelWrapper.Pixel> ValidatedPixel = new List<PixelWrapper.Pixel>();
        public uint pixelCount = 0;
        public uint pixelProcessedCount = 0;
        public bool _checkvalidity = true;

        public bool ctrl_pressed = false;
        public bool _MouseDown = false;
        public List<PixelWrapper.Vector> MousePos = new List<PixelWrapper.Vector>();
        public PixelWrapper.Vector SelectPOINTA = null;
        public PixelWrapper.Vector SelectPOINTB = null;

        public string KeyPath = "";
        public static PixelWrapper.UPO MYUPO = null;
        // get private and public key from path ... 
        public Form1()
        {
            _folderPath = "C:\\Users\\Gaël\\source\\repos\\PIXCHAIN\\firstchain\\bin\\Debug\\";
            KeyPath = "C:\\Users\\Gaël\\source\\repos\\PIXCHAIN\\firstchain\\bin\\Debug\\Compte A\\";
            if ( !File.Exists(KeyPath + "publicKey")) { MessageBox.Show("keys are not in folder! Please Relaunch!");  return;  }
            ulong mupoindex = PixelWrapper.GetUPOIndex(PixelWrapper.ComputeSHA256(File.ReadAllBytes(KeyPath + "publicKey")));
            if ( mupoindex == 0) { MessageBox.Show("Upo not found! Please Mine something and relaunch!"); return; }
            MYUPO = PixelWrapper.GetOfficialUPO_B(mupoindex);
            if ( MYUPO == null)
            {
                MessageBox.Show("Upo not found! Please Mine something and relaunch!"); return;

            }
            
            InitializeComponent();
            myUPOInfo.Text = PixelWrapper.SHAToHex(MYUPO.HashKey, false) + " | sold : " + MYUPO.Sold;
            g = Graphics.FromImage(myDraw);
            this.Controls.Add(drawBox);
            drawBox.Size = new Size(1000, 1000);
            
            drawBox.MouseDown += new System.Windows.Forms.MouseEventHandler(this.MouseDown);
            drawBox.MouseUp += new System.Windows.Forms.MouseEventHandler(this.MouseUp);
            drawBox.MouseMove += new System.Windows.Forms.MouseEventHandler(this.Draw);
            this.KeyDown += KeyDownHandler;
            this.KeyUp += KeyUpHandler;

            Thread validationThread = new Thread(new ThreadStart(PixelWrapper.ValidatingThread));
            validationThread.IsBackground = true;
            validationThread.Start();
        }

        public void KeyDownHandler(object sender, KeyEventArgs e)
        {

            if ( e.KeyCode == Keys.D && !ctrl_pressed)
            {
                ctrl_pressed = true;
                SelectPOINTA = null;
                SelectPOINTB = null;
            }
        }
        public void KeyUpHandler(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.D)
            {
                ctrl_pressed = false;
                ClearDraw();
                PrintAllPixel();
                if (SelectPOINTA != null)
                {
                    if (SelectPOINTB.X < SelectPOINTA .X || SelectPOINTB.Y < SelectPOINTA.Y)
                    {
                       
                        return;
                    }
                    else
                    {
                        List<PixelWrapper.Vector> selpix = new List<PixelWrapper.Vector>();
                        for (int y = SelectPOINTA.Y ; y <= SelectPOINTB.Y; y++)
                        {
                            for (int x = SelectPOINTA.X; x <= SelectPOINTB.X; x++)
                            {
                                for ( int i = 0; i < UnvalidatedPixel.Count; i++ )
                                {
                                    if (UnvalidatedPixel[i].X == x && UnvalidatedPixel[i].Y == y)
                                    {
                                        selpix.Add(UnvalidatedPixel[i]);
                                        UnvalidatedPixel.RemoveAt(i) ;
                                        break;
                                        // remove pix then ... 
                                    }
                                }
                            }

                        }
                        
                        ProccessedPixel.Add(selpix);
                        pixelProcessedCount += (uint)selpix.Count;
                        proccessingpixelInfo.Text = pixelProcessedCount.ToString();
                        PrintAllPixel();
                    }
                       
                }
               
            }
        }
        public void ClearDraw()
        {
            myDraw = new Bitmap(1000, 1000);
            g = Graphics.FromImage(myDraw);
        }
        public void PrintAllPixel()
        {
            foreach (PixelWrapper.Vector pix in UnvalidatedPixel)
            {
                g.FillRectangle(Brushes.Red, pix.X, pix.Y, 1, 1);
                
            }
            foreach ( List<PixelWrapper.Vector> proclist in ProccessedPixel)
            {
                foreach (PixelWrapper.Vector pix in proclist)
                {
                    g.FillRectangle(Brushes.Blue, pix.X, pix.Y, 1, 1);
                }
            }
            foreach (PixelWrapper.Pixel vpix in ValidatedPixel)
            {
                g.FillRectangle(Brushes.Black, vpix.position.X, vpix.position.Y, 1, 1);
                drawBox.Image = myDraw;
            }
            drawBox.Image = myDraw;
        }
        public void PrintValidatedPixel()
        {
            ClearDraw();
            _checkvalidity = true;
            foreach ( PixelWrapper.Pixel vpix in ValidatedPixel)
            {
                g.FillRectangle(Brushes.Black, vpix.position.X, vpix.position.Y, 1, 1);
                drawBox.Image = myDraw;
            }
        }

        private void MouseDown(object sender, MouseEventArgs e)
        {
            _MouseDown = true ;
        }
        private void MouseUp(object sender, MouseEventArgs e)
        {
            _MouseDown = false;
            if ( _checkvalidity )
            {
                PrintAllPixel();
                _checkvalidity = false;
            }
        }

        private void Draw(object sender, MouseEventArgs e)
        {
            if ( _MouseDown && !ctrl_pressed)
            {
                g.FillRectangle(Brushes.Red, e.X, e.Y, 1, 1);
                drawBox.Image = myDraw;
                mouseXYinfo.Text = e.X.ToString() + ":" + e.Y.ToString();
                PixelWrapper.Vector upix = new PixelWrapper.Vector(e.X, e.Y); 
                UnvalidatedPixel.Add(upix);
                pixelCount++;
                pixelCountInfo.Text = pixelCount.ToString();
            }
            else
            {
                if ( ctrl_pressed && _MouseDown)
                {
                    if ( SelectPOINTA == null)
                    {
                        SelectPOINTA = new PixelWrapper.Vector(e.X, e.Y);
                    }
                    
                    SelectPOINTB = new PixelWrapper.Vector(e.X, e.Y);
                    ClearDraw();
                    g.FillRectangle(Brushes.Blue, SelectPOINTA.X, SelectPOINTA.Y, SelectPOINTB.X - SelectPOINTA.X, SelectPOINTB.Y - SelectPOINTA.Y);
                    PrintAllPixel();
                    drawBox.Image = myDraw;
                   
                }
            }
        }

        private void checkpixelstateBTN_Click(object sender, EventArgs e)
        {
            PrintValidatedPixel();
        }
    }
}
