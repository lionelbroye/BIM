using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Threading; 

namespace firstchain
{
    class arduino
    {

        public static  Thread rcvThread;
        public static SerialPort sp;

        public static uint HEADPOSITION = 0;

        public static void HashToClock(byte[] hash)
        {
            string hashString = Program.SHAToHex(hash, false);
            uint minute_to_add = 0;
            foreach ( char c in hashString.ToCharArray())
            {
                switch ( c)
                {
                    case '0': break;
                    case '1': minute_to_add += 10;  break;
                    case '2': minute_to_add += 20;  break;
                    case '3': minute_to_add += 30; break;
                    case '4': minute_to_add += 40; break;
                    case '5': minute_to_add += 50; break;
                    case '6': minute_to_add += 60; break;
                    case '7': minute_to_add += 70; break;
                    case '8': minute_to_add += 80; break;
                    case '9': minute_to_add += 90; break;
                    case 'a': break;
                    case 'b': minute_to_add += 1; break;
                    case 'c': minute_to_add += 2; break;
                    case 'd': minute_to_add += 3; break;
                    case 'e': minute_to_add += 4; break;
                    case 'f': minute_to_add += 5; break;
                    case 'g': minute_to_add += 6; break;
                    case 'h': minute_to_add += 7; break;
                    case 'i': minute_to_add += 8; break;
                    case 'j': minute_to_add += 9; break;
                    case 'k': minute_to_add += 10; break;
                    case 'l': minute_to_add += 11; break;
                    case 'm': minute_to_add += 12; break;
                    case 'n': minute_to_add += 13; break;
                    case 'o': minute_to_add += 14; break;
                    case 'p': minute_to_add += 15; break;
                    case 'q': minute_to_add += 16; break;
                    case 'r': minute_to_add += 17; break;
                    case 's': minute_to_add += 18; break;
                    case 't': minute_to_add += 19; break;
                    case 'u': minute_to_add += 20; break;
                    case 'v': minute_to_add += 21; break;
                    case 'w': minute_to_add += 22; break;
                    case 'x': minute_to_add += 23; break;
                    case 'y':
                        minute_to_add += 24;
                        break;
                    case 'z':
                        minute_to_add += 25;
                        break;

                }
            }
            for (int i = 0; i < minute_to_add; i++)
            {
             //   SendTick("7");
            }

            

        }

        public static void Initialize(string portName)
        {
            try
            {
                sp = new SerialPort(portName, 9600);
                sp.Open();
                AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
                uint lastindex = Program.RequestLatestBlockIndex(true);
                SendTick("5");
                for (int i = 0; i < lastindex; i++)
                {
                    SendTick("3");
                    Thread.Sleep(100);
                }
                HEADPOSITION = lastindex;
            }
            catch (Exception e)
            {
                Console.WriteLine( portName.ToString() + " is not opened.");
            }

        }
        public static void GetPortReady()
        {
            string[] result = SerialPort.GetPortNames();
            Console.WriteLine("available port : ");
            foreach (string s in result)
            {
                Console.WriteLine(s);
            }
        }
        public static void OnProcessExit(object sender, EventArgs e)
        {
            if (sp != null)
            {
                if (sp.IsOpen == true)
                {
                    sp.Close();
                }
            }

        }
        public static void SendTick(string arg)
        {
            if (sp != null)
            {
                if (sp.IsOpen)
                {
                    sp.Write(arg);
                }
            }
        }


    }
}
