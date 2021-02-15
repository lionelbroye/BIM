using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace firstchain
{

    class XYPoem
    {


        class Point
        {

            public float X { get; set; }
            public float Y { get; set; }

            public Point(float x = 0f, float y = 0f)
            {
                this.X = x;
                this.Y = y;
            }
        }
        public static List<float[]> buildBezierSegment(float[] p0, float[] p1, float[] p2, float[] p3)
        {
            List<float[]> segList = new List<float[]>();
            float px = p0[0];
            float py = p0[1];
            for (int i = 1; i < 100; i++)
            {
                float ratio = (float)(i) / 100;
                float x00 = p0[0] + (p1[0] - p0[0]) * ratio; float y00 = p0[1] + (p1[1] - p0[1]) * ratio;
                float x01 = p1[0] + (p2[0] - p1[0]) * ratio; float y01 = p1[1] + (p2[1] - p1[1]) * ratio;
                float x02 = p2[0] + (p3[0] - p2[0]) * ratio; float y02 = p2[1] + (p3[1] - p2[1]) * ratio;
                float x10 = (x01 - x00) * ratio + x00;
                float y10 = (y01 - y00) * ratio + y00;
                float x11 = (x02 - x01) * ratio + x01;
                float y11 = (y02 - y01) * ratio + y01;
                float x20 = (x11 - x10) * ratio + x10;
                float y20 = (y11 - y10) * ratio + y10;
                float dx = x20 - px;
                float dy = y20 - py;
                float dis = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dis > 1)
                {
                    segList.Add(new float[2] { x20, y20 });
                    px = x20; py = y20;
                }
            }
            if (segList.Count == 0)
            {
                segList.Add(new float[2] { p3[0], p3[1] });
            }
            return segList;
        }
        public static List<float[]> buildQuadraticBezierSegment(float[] p0, float[] p1, float[] p2)
        {
            List<float[]> segList = new List<float[]>();
            float currentSegmentX = p0[0];
            float currentSegmentY = p0[1];
            for (int i = 1; i < 100; i++)
            {
                float t = (float)(i) / 100;
                float curvePointX = (1 - t) * (1 - t) * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0];
                float curvePointY = (1 - t) * (1 - t) * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1];
                float dis = (float)Math.Sqrt(Math.Pow((curvePointX - currentSegmentX), 2) + Math.Pow((curvePointY - currentSegmentY), 2));
                if (dis > 1)
                {
                    segList.Add(new float[2] { curvePointX, curvePointY });
                    currentSegmentX = curvePointX;
                    currentSegmentY = curvePointY;
                }

            }
            if (segList.Count == 0)
            {
                segList.Add(new float[2] { p2[0], p2[1] });
            }
            return segList;
        }

        public static List<float[]> buildArcSegment(float rx, float ry, float phi, int fA, int fS, float x1, float y1, float x2, float y2)
        {
            List<float[]> segList = new List<float[]>();
            phi = phi / 180 * (float)Math.PI;
            float x1p = (float)Math.Cos(phi) * (x1 - x2) / 2 + (float)Math.Sin(phi) * (y1 - y2) / 2;
            float y1p = (float)-Math.Sin(phi) * (x1 - x2) / 2 + (float)Math.Cos(phi) * (y1 - y2) / 2;
            float lam = x1p * x1p / (rx * rx) + y1p * y1p / (ry * ry);
            if (lam > 1)
            {
                rx = (float)Math.Sqrt(lam) * rx;
                ry = (float)Math.Sqrt(lam) * ry;
            }
            float tmp = (rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p) / (rx * rx * y1p * y1p + ry * ry * x1p * x1p);
            float st = (float)Math.Sqrt(Math.Round(tmp, 5));
            float cp_sign;
            if (fA == fS)
            {
                cp_sign = -1;
            }
            else
            {
                cp_sign = 1;
            }

            float cxp = cp_sign * (st * rx * y1p / ry);
            float cyp = cp_sign * (-st * ry * x1p / rx);
            float cx = (float)Math.Cos(phi) * cxp - (float)Math.Sin(phi) * cyp + (x1 + x2) / 2;
            float cy = (float)Math.Sin(phi) * cxp + (float)Math.Cos(phi) * cyp + (y1 + y2) / 2;

            float Vxc = (x1p - cxp) / rx;
            float Vyc = (y1p - cyp) / ry;
            Vxc = (x1p - cxp) / rx;
            Vyc = (y1p - cyp) / ry;
            float Vxcp = (-x1p - cxp) / rx;
            float Vycp = (-y1p - cyp) / ry;

            if (Vyc >= 0) cp_sign = 1;
            else cp_sign = -1;

            float th1 = cp_sign * (float)Math.Acos(Vxc / (float)Math.Sqrt(Vxc * Vxc + Vyc * Vyc)) / (float)Math.PI * 180;
            if ((Vxc * Vycp - Vyc * Vxcp) >= 0) cp_sign = 1;
            else cp_sign = -1;
            tmp = (Vxc * Vxcp + Vyc * Vycp) / ((float)Math.Sqrt(Vxc * Vxc + Vyc * Vyc) * (float)Math.Sqrt(Vxcp * Vxcp + Vycp * Vycp));
            float dth = cp_sign * (float)Math.Acos((float)Math.Round(tmp, 3)) / (float)Math.PI * 180;

            if (fS == 0 && dth > 0) dth -= 360;
            if (fS >= 1 && dth < 0) dth += 360;

            float theta = th1 / 180 * (float)Math.PI;
            float px = rx * (float)Math.Cos(theta) + cx;
            float py = ry * (float)Math.Sin(theta) + cy;
            for (int i = 1; i < 101; i++)
            {
                float ratio = (float)i / 100;
                theta = (th1 + dth * ratio) / 180 * (float)Math.PI;
                float x = (float)Math.Cos(phi) * rx * (float)Math.Cos(theta) - (float)Math.Sin(phi) * ry * (float)Math.Sin(theta) + cx;
                float y = (float)Math.Sin(phi) * rx * (float)Math.Cos(theta) + (float)Math.Cos(phi) * ry * (float)Math.Sin(theta) + cy;
                float dx = x - px; float dy = y - py;
                float dis = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dis > 1)
                {
                    segList.Add(new float[2] { x, y });
                    px = x;
                    py = y;
                }
            }
            return segList;
        }
        // SOME SVG PARSING FOUND in makedraw py file... 

        public class SvgParser
        {
            public float xbias { get; set; }
            public float ybias { get; set; }
            public List<float[]> tf { get; set; }
            public List<List<float[]>> originPathList { get; set; }
            public List<List<float[]>> rawPathTest { get; set; }
            public SvgParser()
            {
                this.xbias = 0;
                this.ybias = 0;
                this.tf = new List<float[]>();
                this.originPathList = new List<List<float[]>>();
                this.rawPathTest = new List<List<float[]>>();
            }
            public void lineTo(float x, float y)
            {
                this.rawPathTest[rawPathTest.Count - 1].Add(new float[2] { x, y });
                for (int i = 0; i < this.tf.Count; i++)
                {

                    // tf weird shit here 
                    float[] tf = this.tf[-1 - i]; // wtf ??
                    float x1 = tf[0] * x + tf[2] * y + tf[4];
                    float y1 = tf[1] * x + tf[3] * y + tf[5];
                    x = x1;
                    y = y1;
                }
                float[] point = new float[2] { x, y };
                this.originPathList[originPathList.Count - 1].Add(point); // weird??

            }
            public void moveTo(float x, float y)
            {
                this.rawPathTest.Add(new List<float[]>());
                this.rawPathTest[rawPathTest.Count - 1].Add(new float[2] { x, y });
                for (int i = 0; i < this.tf.Count; i++)
                {

                    // tf weird shit here 
                    float[] tf = this.tf[-1 - i]; // wtf ??
                    float x1 = tf[0] * x + tf[2] * y + tf[4];
                    float y1 = tf[1] * x + tf[3] * y + tf[5];
                    x = x1;
                    y = y1;
                }
                float[] initpoint = new float[2] { x, y };
                this.originPathList.Add(new List<float[]>());
                this.originPathList[originPathList.Count - 1].Add(initpoint);// weird??
            }
            public void parsePath(string d)
            {
                /*
                string ds = d.Replace("e-", "ee");
                ds = ds.Replace("-", " -").Replace("s", " s ").Replace("S", " S ").Replace("c", " c ").Replace("C", " C ").Replace("v", " v ").Replace("V", " V ");
                ds = ds.Replace("l", " l ").Replace("L", " L ").Replace("A", " A ").Replace("a", " a ").Replace(",", " ").Replace("M", " M ").Replace("h", " h ").Replace("H", " H ").Replace("m", " m ").Replace("z", " z ");
                ds = ds.Replace("q", " q ").Replace("Q", " Q ");
                string[] ss = ds.Split(' '); // <-- probably splitting space here ! be carefull 
                for (int i = 0; i < ss.Length; i++)
                    ss[i] = ss[i].Replace("ee", "e-");

                */
                // dont knwo what is the fucking -e ... 
                string ds = d.Replace(",", " ");
                ds = ds.Replace(".", ",");
                string[] ss = ds.Split(' '); // <-- probably splitting space here ! be carefull 
                List<float[]> pbuff = new List<float[]>();
                int ptr = 0;
                string state = "";
                string prevstate = "";
                float curvecnt = 0;
                float[] lastControl = new float[2];
                float x = this.xbias;
                float y = this.ybias;
                float x0 = x; // some initial variable to backup?
                float y0 = y; // some initial variable to backup?

                while (ptr < ss.Length)
                {
                    if (Regex.IsMatch(ss[ptr], @"^[a-zA-Z]+$"))//ss[ptr] == "") // is alpha ????? weird... alphanumerical ??? only letters
                    {

                        Console.WriteLine("into state " + ss[ptr]);
                        prevstate = state;
                        state = ss[ptr];
                        ptr += 1;
                        curvecnt = 0;
                        if (state == "C" || state == "c" || state == "Q" || state == "q")
                        {
                            pbuff = new List<float[]>();
                            pbuff.Add(new float[2] { x, y });
                        }
                        if (state == "z" || state == "Z")
                        {
                            x = x0;
                            y = y0;
                            this.lineTo(x0, y0);
                        }
                        if (state == "s" || state == "S")
                        {
                            pbuff = new List<float[]>();
                            pbuff.Add(new float[2] { x, y });
                        }
                    }
                    else
                    {
                        Console.WriteLine("parsing : " + ss[ptr]);
                        bool _uknw = true;
                        switch (state)
                        {
                            case "h":
                                _uknw = false;
                                float dis = float.Parse(ss[ptr]);
                                this.lineTo(x + dis, y);
                                x = x + dis; y = y;
                                ptr++;
                                break;

                            case "H":
                                _uknw = false;
                                dis = float.Parse(ss[ptr]);
                                this.lineTo(dis, y);
                                x = dis; y = y;
                                ptr++;
                                break;
                            case "v":
                                _uknw = false;
                                dis = float.Parse(ss[ptr]);
                                this.lineTo(x, y + dis);
                                x = x; y = y + dis;
                                ptr++;
                                break;
                            case "V":
                                _uknw = false;
                                dis = float.Parse(ss[ptr]);
                                this.lineTo(x, dis);
                                x = x; y = dis;
                                ptr++;
                                break;
                            case "m":

                                _uknw = false;
                                float dx = float.Parse(ss[ptr]);
                                float dy = float.Parse(ss[ptr + 1]);
                                ptr += 2;
                                x = x + dx; y = y + dy;
                                curvecnt++;
                                if (curvecnt > 1) { this.lineTo(x, y); }
                                else { this.moveTo(x, y); x0 = x; y0 = y; }
                                break;
                            case "M":
                                _uknw = false;
                                float ax = float.Parse(ss[ptr]) + this.xbias;
                                float ay = float.Parse(ss[ptr + 1]) + this.ybias;
                                ptr += 2;
                                curvecnt++;
                                x = ax; y = ay;
                                if (curvecnt > 1) { this.lineTo(x, y); }
                                else { this.moveTo(x, y); x0 = x; y0 = y; }
                                break;
                            case "a":
                                _uknw = false;
                                float rx = float.Parse(ss[ptr]);
                                float ry = float.Parse(ss[ptr + 1]);
                                float phi = float.Parse(ss[ptr + 2]);
                                int fA = int.Parse(ss[ptr + 3]);
                                int fS = int.Parse(ss[ptr + 4]);
                                float px = float.Parse(ss[ptr + 5]) + x;
                                float py = float.Parse(ss[ptr + 6]) + y;
                                ptr += 7;
                                List<float[]> arcSeg = buildArcSegment(rx, ry, phi, fA, fS, x, y, px, py);
                                foreach (float[] s in arcSeg)
                                {
                                    this.lineTo(s[0], s[1]);
                                }
                                x = px; y = py;
                                break;
                            case "A":
                                _uknw = false;
                                rx = float.Parse(ss[ptr]);
                                ry = float.Parse(ss[ptr + 1]);
                                phi = float.Parse(ss[ptr + 2]);
                                fA = int.Parse(ss[ptr + 3]);
                                fS = int.Parse(ss[ptr + 4]);
                                px = float.Parse(ss[ptr + 5]) + xbias;
                                py = float.Parse(ss[ptr + 6]) + ybias;
                                ptr += 7;
                                arcSeg = buildArcSegment(rx, ry, phi, fA, fS, x, y, px, py);
                                foreach (float[] s in arcSeg)
                                {
                                    this.lineTo(s[0], s[1]);
                                }
                                x = px; y = py;
                                break;

                            case "c":
                                _uknw = false;
                                dx = float.Parse(ss[ptr]);
                                dy = float.Parse(ss[ptr + 1]);
                                pbuff.Add(new float[2] { x + dx, y + dy });
                                ptr += 2;
                                curvecnt++;
                                if (curvecnt == 3)
                                {
                                    List<float[]> bzseg = buildBezierSegment(pbuff[0], pbuff[1], pbuff[2], pbuff[3]);
                                    lastControl = pbuff[2];
                                    foreach (float[] s in bzseg)
                                    {
                                        this.lineTo(s[0], s[1]);
                                    }
                                    x = x + dx; y = y + dy;
                                    pbuff = new List<float[]>();
                                    pbuff.Add(new float[2] { x, y });
                                    curvecnt = 0;

                                }
                                break;

                            case "C":
                                _uknw = false;
                                ax = float.Parse(ss[ptr]) + this.xbias;
                                ay = float.Parse(ss[ptr + 1]) + this.ybias;
                                pbuff.Add(new float[2] { ax, ay });
                                ptr += 2;
                                curvecnt++;
                                if (curvecnt == 3)
                                {
                                    List<float[]> bzseg = buildBezierSegment(pbuff[0], pbuff[1], pbuff[2], pbuff[3]);
                                    lastControl = pbuff[2];
                                    foreach (float[] s in bzseg)
                                    {
                                        this.lineTo(s[0], s[1]);
                                    }
                                    x = ax; y = ay;
                                    pbuff = new List<float[]>();
                                    pbuff.Add(new float[2] { ax, ay });
                                    curvecnt = 0;

                                }
                                break;
                            case "q":
                                _uknw = false;
                                dx = float.Parse(ss[ptr]);
                                dy = float.Parse(ss[ptr + 1]);
                                pbuff.Add(new float[2] { x + dx, y + dy });
                                ptr += 2;
                                curvecnt++;
                                if (curvecnt == 2)
                                {
                                    List<float[]> bzseg = buildQuadraticBezierSegment(pbuff[0], pbuff[1], pbuff[2]);
                                    lastControl = pbuff[1];
                                    foreach (float[] s in bzseg)
                                    {
                                        this.lineTo(s[0], s[1]);
                                    }
                                    x = x + dx; y = y + dy;
                                    pbuff = new List<float[]>();
                                    pbuff.Add(new float[2] { x, y });
                                    curvecnt = 0;

                                }
                                break;
                            case "Q":
                                _uknw = false;
                                ax = float.Parse(ss[ptr]) + this.xbias;
                                ay = float.Parse(ss[ptr + 1]) + this.ybias;
                                pbuff.Add(new float[2] { ax, ay });
                                ptr += 2;
                                curvecnt++;
                                if (curvecnt == 2)
                                {
                                    List<float[]> bzseg = buildQuadraticBezierSegment(pbuff[0], pbuff[1], pbuff[2]);
                                    lastControl = pbuff[1];
                                    foreach (float[] s in bzseg)
                                    {
                                        this.lineTo(s[0], s[1]);
                                    }
                                    x = ax; y = ay;
                                    pbuff = new List<float[]>();
                                    pbuff.Add(new float[2] { ax, ay });
                                    curvecnt = 0;

                                }
                                break;
                            case "s":
                                _uknw = false;
                                dx = float.Parse(ss[ptr]);
                                dy = float.Parse(ss[ptr + 1]);
                                ptr += 2;
                                curvecnt++;
                                if (curvecnt == 1)
                                {
                                    if (prevstate == "S" || prevstate == "s" || prevstate == "C" || prevstate == "c")
                                    {
                                        float[] controlPoint = new float[2] { 2 * pbuff[0][0] - lastControl[0], 2 * pbuff[0][1] - lastControl[1] };
                                        pbuff.Add(controlPoint);
                                        pbuff.Add(new float[2] { x + dx, y + dy });

                                    }
                                    else
                                    {
                                        pbuff.Add(pbuff[0]);
                                        pbuff.Add(new float[2] { x + dx, y + dy });

                                    }

                                }
                                if (curvecnt == 2)
                                {
                                    pbuff.Add(new float[2] { x + dx, y + dy });
                                    List<float[]> bzseg = buildBezierSegment(pbuff[0], pbuff[1], pbuff[2], pbuff[3]);
                                    lastControl = pbuff[2];
                                    foreach (float[] s in bzseg)
                                    {
                                        this.lineTo(s[0], s[1]);
                                    }
                                    x = x + dx;
                                    y = y + dy;
                                    pbuff = new List<float[]>();
                                    pbuff.Add(new float[2] { x, y });
                                    curvecnt = 0;
                                }

                                break;
                            case "S":
                                _uknw = false;
                                ax = float.Parse(ss[ptr]) + this.xbias;
                                ay = float.Parse(ss[ptr + 1]) + this.ybias;
                                ptr += 2;
                                curvecnt++;
                                if (curvecnt == 1)
                                {
                                    if (prevstate == "S" || prevstate == "s" || prevstate == "C" || prevstate == "c")
                                    {
                                        float[] controlPoint = new float[2] { 2 * pbuff[0][0] - lastControl[0], 2 * pbuff[0][1] - lastControl[1] };
                                        pbuff.Add(controlPoint);
                                        pbuff.Add(new float[2] { ax, ay });

                                    }
                                    else
                                    {
                                        pbuff.Add(pbuff[0]);
                                        pbuff.Add(new float[2] { ax, ay });

                                    }

                                }
                                if (curvecnt == 2)
                                {
                                    pbuff.Add(new float[2] { ax, ay });
                                    List<float[]> bzseg = buildBezierSegment(pbuff[0], pbuff[1], pbuff[2], pbuff[3]);
                                    lastControl = pbuff[2];
                                    foreach (float[] s in bzseg)
                                    {
                                        this.lineTo(s[0], s[1]);
                                    }
                                    x = ax;
                                    y = ay;
                                    pbuff = new List<float[]>();
                                    pbuff.Add(new float[2] { x, y });
                                    curvecnt = 0;
                                }

                                break;
                            case "l":
                                _uknw = false;
                                dx = float.Parse(ss[ptr]);
                                dy = float.Parse(ss[ptr + 1]);
                                ptr += 2;
                                curvecnt++;
                                x = x + dx;
                                y = y + dy;
                                this.lineTo(x, y);
                                break;
                            case "L":
                                _uknw = false;
                                ax = float.Parse(ss[ptr]) + this.xbias;
                                ay = float.Parse(ss[ptr + 1]) + this.ybias;
                                ptr += 2;
                                curvecnt++;
                                x = ax;
                                y = ay;
                                this.lineTo(x, y);
                                break;



                        }
                        if (_uknw)
                        {

                            ptr++;
                            Console.WriteLine("unknown state : " + state);

                        }


                    }

                }
            }
        }

        public static string[] UTF8_CHAR_SVG_PATHS = new string[256]; // A REMPLIR...

        /*-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_- SOME POEMS HIDDEN IN BLOCK TRANSACTIONS -_-_-_-_-_-_-_-_-_-_-_-_*/

        public static void PrintPoemFromBlockTransaction(Program.Block b, byte type = 0)
        {
            if (b.Data.Count == 0) return;
            if (sp_XY == null) return;
            if (!sp_XY.IsOpen) return;

            // just an example of what we can do. Here we raw print characters found 
            // in receiver public key hash of block transaction with the plotter

            string Poem = "";
            foreach ( Program.Tx tx in b.Data)
            {
                switch (type)
                {
                    // the poem is hidden in the receiver public key hash. 
                    case 0: Poem = System.Text.Encoding.ASCII.GetString(tx.rHashKey); break;
                }
            }

            // List Of Svg Path from the Poem
            List<string> poemPaths = new List<string>();
            // TODO
            foreach ( char c in Poem.ToCharArray())
            {
                poemPaths.Add(UTF8_CHAR_SVG_PATHS[(byte)c]);
            }
            foreach ( string s in poemPaths)
            {

                // do what you want here... 

                List<List<Point>> pts = GetPointListsFromSVGPath(s, false);
                pts = NormalizeListForPlotterDimension(pts, 200, true);
                pts = ApplyOffsetToPointsList(pts, new Point(50, 50), true);
                LoadPointsToMsgQueue(pts);
            }
        }

        public static void BuildandHidePoemsInPublicKeyHash(string _sprkey, string _spukey, uint sutxop, uint amount, string poem, uint rutxop, uint fee, uint locktime)
        {
            byte[] poemsb = Encoding.UTF8.GetBytes(poem);
            /*
            List<byte> poemsb = new List<byte>();
            foreach ( char c in poem)
            {
                poemsb.Add((byte)c);
            }*/
            // build multiple transaction of 0 coin if poem is more than 32 bytes.
            //uint txNumb = 0;
            List<List<byte>> poemChunks = new List<List<byte>>();
            uint chunkcounter = 0;
            List<byte> chunk = new List<byte>();
            for (int i = 0; i < poemsb.Length; i++)
            {
                if (chunkcounter == 32)
                {
                    chunkcounter = 0;
                    poemChunks.Add(chunk);
                    chunk = new List<byte>();
                }
                chunk.Add(poemsb[i]);
                chunkcounter++;
               
            }
            poemChunks.Add(chunk);
            // fill the last chunk ...
            uint _fillCount = 32 - (uint)poemChunks[poemChunks.Count - 1].Count;
          
            if ( _fillCount > 0)
            {
                for (int i = 0; i < _fillCount; i++ )
                {
                    poemChunks[poemChunks.Count - 1].Add(0); // fill with zero
                }

            }
            // do a warning if there is more than one chunk 
            if ( poemChunks.Count > 1)
            {
                if (!Program.ValidYesOrNo("Poem is higher than 32 bytes. It will be split into " + poemChunks.Count + " transactions."))
                    return;

            }

            uint tou_offset = 0;
            for (int i = 0; i < poemChunks.Count; i++)
            {
                //newtx sprkey: spukey: sutxop: amount: rpukey: rutxop: fee:
                byte[] _MyPublicKey = File.ReadAllBytes(_spukey);
                byte[] sUTXOPointer = BitConverter.GetBytes(sutxop);
                byte[] Coinamount = BitConverter.GetBytes(amount);
                byte[] _hashOthPublicKey = Program.ListToByteArray(poemChunks[i]);
                byte[] rUTXOPointer = BitConverter.GetBytes(rutxop);
                byte[] FEE = BitConverter.GetBytes(fee);
                byte[] LockTime = BitConverter.GetBytes((uint)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds + locktime);
                if (sutxop == 0) { Program.Print("bad pointer"); return; }
                Program.UTXO utxo = Program.GetOfficialUTXOAtPointer(sutxop);
                if (utxo == null) { Program.Print("bad pointer"); return; }
                if (!utxo.HashKey.SequenceEqual(Program.ComputeSHA256(_MyPublicKey))) { Program.Print("bad pointer"); return; }
                uint newtou = utxo.TokenOfUniqueness + 1 + tou_offset;
                byte[] TOU = BitConverter.GetBytes(newtou);
                bool needDust = false;
                if (rutxop != 0)
                {
                    Program.UTXO oUTXO = Program.GetOfficialUTXOAtPointer(rutxop);
                    if (oUTXO == null) { Program.Print("bad pointer"); return; }
                }
                else { needDust = true; }
                if (Program.GetFee(utxo, needDust) > fee)
                {
                    Program.Print("insuffisiant fee");
                    return;
                }
                if (fee + amount > utxo.Sold) { Program.Print("insuffisiant sold"); return; }
                //           if ( TX.TxFee < GetFee(sUTXO, dustNeeded)) { Print("Invalid Fee"); return false;  }
                // uint sum = TX.Amount + TX.TxFee;
                Program.CreateTxFile(_sprkey, _MyPublicKey, Coinamount, LockTime, sUTXOPointer, rUTXOPointer, TOU, FEE, _hashOthPublicKey);
                tou_offset++;
            }
           

        }

       // SVG PATH
        public static string test_SVG = "m 331,408 v -35 h 21.5 21.5 v 35 35 H 352.5 331 Z m -107,18.12511 c -7.27572,-2.33793 -12.46257,-5.74149 -18.31994,-12.02137 -19.31031,-20.70319 -12.62651,-55.23462 13.04468,-67.39446 6.35118,-3.0084 7.46509,-3.20928 17.79629,-3.20928 10.38719,0 11.39639,0.18479 17.54786,3.21315 10.73198,5.28334 18.59457,14.3151 22.38971,25.71907 2.32593,6.98914 2.14988,19.54808 -0.37647,26.85646 -7.46676,21.60031 -30.86196,33.65518 -52.08213,26.83643 z";
        // the plotter stuff...
        public static List<string> MsgQueue = new List<string>();
        public static int PEN_DOWN = 120;
        public static int PEN_UP = 30;

        public static SerialPort sp_XY;
        public static Thread RCV_PLOTTER;
        public static Thread SND_PLOTTER;
        public static bool _Busy = false; // le plotter est en train de faire des trucs

        public static void ConfigurePort()
        {
            string[] ports = SerialPort.GetPortNames();
            Console.WriteLine("Voici la liste des ports utilisables : ");
            foreach (string s in ports)
            {
                Console.Write(s + " / ");
            }
            Console.WriteLine("");
            Console.WriteLine("Veuillez taper le nom du port connecté au plotter : ");

            while (true)
            {

                string portName = Console.ReadLine();
                try
                {
                    sp_XY = new SerialPort(portName, 115200);
                    sp_XY.Open();
                    sp_XY.ReadTimeout = 100;
                    Console.WriteLine("Port  " + portName + " ouvert.");
                    break;
                }
                catch
                {
                    if (!Program.ValidYesOrNo("")) { return; }
                    Console.WriteLine("Erreur à l'ouverture du port.");
                }
            }

            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
            RCV_PLOTTER = new Thread(new ThreadStart(Receive_XY_Data));
            RCV_PLOTTER.IsBackground = true;
            RCV_PLOTTER.Start();
            SND_PLOTTER = new Thread(new ThreadStart(ProcessMessageQueue));
            SND_PLOTTER.IsBackground = true;
            SND_PLOTTER.Start();
        }
        public static void Receive_XY_Data()
        {
            while (true)
            {
                if (sp_XY != null)
                {
                    if (sp_XY.IsOpen == true)
                    {
                        try
                        {
                            string r_data;

                            r_data = sp_XY.ReadLine(); //< j'obient la valeur ... 
                            if (r_data == "OK")
                            {
                                _Busy = false;
                            }
                            Console.WriteLine(r_data);


                        }
                        catch (System.TimeoutException e)
                        {
                        }
                    }

                }
            }
        }

        public static void ProcessMessageQueue() // multi threaded message send
        {
            while (true)
            {
                if (_Busy)
                    Thread.Sleep(1000);
                else
                {
                    if (MsgQueue.Count > 0)
                    {
                        _Busy = true;
                        sp_XY.WriteLine(MsgQueue[0]);
                        MsgQueue.RemoveAt(0);
                    }

                }

            }

        }
        public static void OnProcessExit(object sender, EventArgs e)
        {

            // fermer les ports quand nous en avons plus besoin 
            if (sp_XY != null)
            {
                if (sp_XY.IsOpen == true)
                {

                    sp_XY.Close();
                }
            }

        }
        static void LoadPointsToMsgQueue(List<List<Point>> points)
        {

            foreach (List<Point> lp in points)
            {

                // 1 aller a la position 0 de l'ensemble de points
                MsgQueue.Add("GO X" + lp[0].X + "Y" + lp[0].Y);
                // 2 baisser le crayon
                MsgQueue.Add("M1 " + PEN_DOWN.ToString());
                for (int i = 1; i < lp.Count; i++)
                {
                    // 4 se deplacer jusquau dernier point 
                    MsgQueue.Add("GO X" + lp[0].X + "Y" + lp[0].Y);
                }
                // 4 lever le crayon  crayon
                MsgQueue.Add("M1 " + PEN_UP.ToString());
            }
        }

        // SVG PATH RAW DATA HERE

        // eg : un cercle et un triangle...
        
        /*
        static void Main(string[] args)
        {
            //  ConfigurePort(); // ouvre le port serie et lance les threads de comm


            // exemple lancer test impression
            List<List<Point>> pts = GetPointListsFromSVGPath(test_SVG, false);
            pts = NormalizeListForPlotterDimension(pts, 200, true);
            pts = RawScaleListOfPoints(pts, 4, 4, true); // aggrandit x2 la liste de points
            pts = RawScaleListOfPoints(pts, 0.5f, 1, true);
            pts = ApplyOffsetToPointsList(pts, new Point(50, 50), true);
            //  LoadPointsToMsgQueue(pts);

        
            while (true) { }

        }

        */


        static List<List<Point>> ApplyOffsetToPointsList(List<List<Point>> points, Point offset, bool _debug = false)
        {
            foreach (List<Point> lp in points)
            {
                for (int i = 0; i < lp.Count; i++)
                {

                    lp[i] = new Point(lp[i].X + offset.X, lp[i].Y + offset.Y);
                    if (i > 0)
                    {
                        Point a = lp[i - 1];
                        Point b = lp[i];
                        //if (_debug)
                            //drawLine(a, b);

                    }
                    if (_debug)
                        Console.WriteLine(lp[i].X + " " + lp[i].Y);
                }

            }
            return points;
        }
        static List<List<Point>> ForceListOfPointsToOrigin(List<List<Point>> points)
        {
            float lowestX = float.MaxValue;
            float lowestY = float.MaxValue;
            float highestX = 0;
            float highestY = 0;
            foreach (List<Point> lp in points)
            {
                foreach (Point p in lp)
                {
                    if (p.X < lowestX)
                        lowestX = p.X;
                    if (p.Y < lowestY)
                        lowestY = p.Y;
                    if (p.X > highestX)
                        highestX = p.X;
                    if (p.Y > highestY)
                        highestY = p.Y;
                }

            }

            foreach (List<Point> lp in points)
            {
                for (int i = 0; i < lp.Count; i++)
                {

                    lp[i] = new Point(lp[i].X - lowestX, lp[i].Y - lowestY);

                }

            }

            return points;
        }
        static List<List<Point>> RawScaleListOfPoints(List<List<Point>> points, float dividerX, float dividerY, bool _debug = false)
        {

            foreach (List<Point> lp in points)
            {
                for (int i = 0; i < lp.Count; i++)
                {

                    lp[i] = new Point(lp[i].X * dividerX, lp[i].Y * dividerY);
                    if (i > 0)
                    {
                        Point a = lp[i - 1];
                        Point b = lp[i];
                        //if (_debug)
                            //drawLine(a, b);

                    }
                    if (_debug)
                        Console.WriteLine(lp[i].X + " " + lp[i].Y);
                }

            }

            return points;
        }

        static List<List<Point>> NormalizeListForPlotterDimension(List<List<Point>> points, float squaresize, bool _debug)
        {
            // [0] Get the lowest x y 
            float lowestX = float.MaxValue;
            float lowestY = float.MaxValue;
            float highestX = 0;
            float highestY = 0;
            foreach (List<Point> lp in points)
            {
                foreach (Point p in lp)
                {
                    if (p.X < lowestX)
                        lowestX = p.X;
                    if (p.Y < lowestY)
                        lowestY = p.Y;
                    if (p.X > highestX)
                        highestX = p.X;
                    if (p.Y > highestY)
                        highestY = p.Y;
                }

            }
            float diffX = highestX - lowestX;
            float diffY = highestY - lowestY;
            float divider = 1f;
            if (diffX > diffY && diffX > squaresize)
            {
                divider = squaresize / diffX;
            }
            if (diffX < diffY && diffY > squaresize)
            {
                divider = squaresize / diffY;
            }
            foreach (List<Point> lp in points)
            {
                for (int i = 0; i < lp.Count; i++)
                {

                    lp[i] = new Point(lp[i].X - lowestX, lp[i].Y - lowestY);

                }

            }

            return RawScaleListOfPoints(points, divider, divider, _debug);
        }

        static List<List<Point>> GetPointListsFromSVGPath(string svgpath, bool _debug = false)
        {
            List<List<Point>> result = new List<List<Point>>();
            SvgParser svgp = new SvgParser();
            int pointcount = 0;
            svgp.parsePath(svgpath);
            foreach (List<float[]> f in svgp.originPathList)
            {
                List<Point> segments = new List<Point>();
                if (_debug)
                    Console.WriteLine("_____________");
                for (int i = 0; i < f.Count; i++)
                {

                    Point e = new Point(f[i][0], f[i][1]);
                    segments.Add(e);
                    if (i > 0)
                    {
                        Point a = new Point(f[i - 1][0], f[i - 1][1]);
                        Point b = e;
                        //if (_debug)
                            //drawLine(a, b);

                    }
                    pointcount++;
                    if (_debug)
                        Console.WriteLine(e.X + " " + e.Y);
                }
                result.Add(segments);

            }
            return result;
        }


    }
}

