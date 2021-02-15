using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Threading;

namespace firstchain
{
    class BIMWATCH
    {

        /*
             Here List Of Data That Can be send to the watch actually... 
             Number of Total Blocks. Number of Tides since the genesis. Number Of Block 
        */

       

        public static SerialPort sp_WATCH;
        public static Thread RCV_WATCH;
        public static Thread SND_WATCH;

        public static bool ConfigurePort()
        {
            string[] ports = SerialPort.GetPortNames();
            Console.WriteLine("List of usable ports : ");
            foreach (string s in ports)
            {
                Console.Write(s + " / ");
            }
            Console.WriteLine("");
            Console.WriteLine("Please type name of the port connected to the watch : ");

            while (true)
            {

                string portName = Console.ReadLine();
                try
                {
                    sp_WATCH = new SerialPort(portName, 9600);
                    sp_WATCH.Open();
                    sp_WATCH.ReadTimeout = 100;
                    Console.WriteLine("Port  " + portName + " opened.");
                    break;
                }
                catch
                {
                    Console.WriteLine("Failed to open port.");
                    if (!Program.ValidYesOrNo("")) { Console.WriteLine("BimWatch configuration aborted."); return false; }
                }
            }

            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
            RCV_WATCH = new Thread(new ThreadStart(Receive_BW_Data));
            RCV_WATCH.IsBackground = true;
            RCV_WATCH.Start();
            /*
            SND_WATCH = new Thread(new ThreadStart(ProcessMessageQueue));
            SND_WATCH.IsBackground = true;
            SND_WATCH.Start();*/
            return true;
        }
        public static void Receive_BW_Data()
        {
            while (true)
            {
                if (sp_WATCH != null)
                {
                    if (sp_WATCH.IsOpen == true)
                    {
                        try
                        {
                            string r_data;

                            r_data = sp_WATCH.ReadLine(); //< j'obient la valeur ... 
                            if (r_data.Length > 0)
                            {
                               // then proccess
                                if (r_data.Contains("RLH"))
                                {
                                    string hash = Program.SHAToHex(Program.GetBlockAtIndex(Program.RequestLatestBlockIndex(true)).Hash, true);
                                    sp_WATCH.Write(hash);
                                }
                                if (r_data.Contains("RBC"))
                                {
                                    string blocklenght = Program.RequestLatestBlockIndex(true).ToString();
                                    // get the number of block mine today 
                                    sp_WATCH.Write(blocklenght);
                                }
                                if (r_data.Contains("RCC")) 
                                {
                                    SendBlocksAndTideInfo();
                                }
                                if (r_data.Contains("TTT"))
                                {
                                    List<byte> bytes = new List<byte>();
                                    uint test = 2;
                                    for (uint i = 0; i < 10; i++) { test += 11; bytes = Program.AddBytesToList(bytes, BitConverter.GetBytes(test)); }
                                    string answer = "";
                                    foreach (byte b in bytes)
                                    {
                                        char c = (char)b;
                                        answer += c.ToString();
                                    }

                                    sp_WATCH.Write(answer);
                                }
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

        public static void SendBlocksAndTideInfo()
        {

            // get tidals value for 3 tides.
            // get block creation at timestamp and transaction number
            List<Tuple<float, uint>> fval = Program.GetTidalValuesInRangeOfTides(3);
            List<byte> tidalsByteArray = new List<byte>();

         
            for (int i = 0; i < fval.Count; i++)
            {
                uint minuteDist = 0; 
                if ( i == 0 )
                {
                    tidalsByteArray = Program.AddBytesToList(tidalsByteArray, BitConverter.GetBytes((float)Math.Round(fval[i].Item1 * 100f) / 100f)); // the water level 
                     tidalsByteArray = Program.AddBytesToList(tidalsByteArray, BitConverter.GetBytes(minuteDist)); // the timestamp
                   
                   
                }
                else {
                    // do it in range of minute
                    minuteDist = (fval[i-1].Item2 - fval[i].Item2)/60;
                     tidalsByteArray = Program.AddBytesToList(tidalsByteArray, BitConverter.GetBytes((float)Math.Round(fval[i].Item1 * 100f) / 100f)); // the water level 
                     Console.WriteLine(minuteDist);
                    tidalsByteArray = Program.AddBytesToList(tidalsByteArray, BitConverter.GetBytes(minuteDist)); // the timestamp
                }
                Console.WriteLine(fval[i].Item1.ToString() + "m at " + minuteDist + "minute offset");

            }
            
            Console.WriteLine("number of tides : " + fval.Count);

            List<Tuple<float, Program.Block>> blocks = Program.GetBlocksMinedSinceNumberOfTides(3);
            uint nBlocks = 0;
            uint nTrans = 0;
            if (blocks == null)
            {
                tidalsByteArray = Program.AddBytesToList(tidalsByteArray, BitConverter.GetBytes(nBlocks));
                tidalsByteArray = Program.AddBytesToList(tidalsByteArray, BitConverter.GetBytes(nTrans));
                Console.WriteLine("0");
            }
            else
            {
                nBlocks = (uint)blocks.Count; 
                foreach ( Tuple<float, Program.Block> rs in blocks)
                {
                    nTrans += rs.Item2.DataSize; 
                }
                tidalsByteArray = Program.AddBytesToList(tidalsByteArray, BitConverter.GetBytes(nBlocks));
                tidalsByteArray = Program.AddBytesToList(tidalsByteArray, BitConverter.GetBytes(nTrans));
                Console.WriteLine(blocks.Count);
            }
            Console.WriteLine(nBlocks + " " + nTrans);

            // 7 * [4-4] ( float-uint)
            string answer = System.Text.Encoding.ASCII.GetString(Program.ListToByteArray(tidalsByteArray)); // weird ??

            // we will convert it raw... 

             answer = "";
            foreach (byte b in tidalsByteArray){
                char c = (char)b;
                answer += c.ToString();
            }

            sp_WATCH.Write(answer);
        }
        public static void OnProcessExit(object sender, EventArgs e)
        {

            // fermer les ports quand nous en avons plus besoin 
            if (sp_WATCH != null)
            {
                if (sp_WATCH.IsOpen == true)
                {

                    sp_WATCH.Close();
                }
            }

        }
    }
}
