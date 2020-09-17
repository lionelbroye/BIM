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

        public static void Initialize()
        {
            sp = new SerialPort("COM4", 9600);
            sp.Open();
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
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
            if ( sp.IsOpen)
            {
                sp.WriteLine(arg);
            }
        }
      

    }
}
