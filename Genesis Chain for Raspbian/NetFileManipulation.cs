using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Diagnostics;
using System.Numerics;
using firstchain.arith256;
using System.Threading;
using System.Timers;

namespace firstchain
{

    public partial class Program
    {
        // global variable for proccessing net files chunk
        public class BroadcastInfo
        {
            public byte flag { get; }
            public string filePath { get; }
            public byte header { get; }
            public BroadcastInfo(byte fl, byte head = 1, string fp = "")
            {
                this.flag = fl;
                this.filePath = fp;
                this.header = head;
            }
        }
        public static network NT;
        public static List<string> PendingDLBlocks = new List<string>();
        public static List<string> PendingDLTXs = new List<string>();
        public static List<string> peerRemovalQueue = new List<string>();
        public static List<string> dlRemovalQueue = new List<string>();

        // global broadcast parameters

        public static uint BROADCAST_BLOCKS_LIMIT = 30; // MAX NUMBER OF BLOCK I WANT TO BROADCAST WHEN UPLOADING MY BC. IF 0. BROADCAST THE FULL. 
        public static uint BROADCAST_FULL_BLOCKCHAIN_CLOCK = 30; // I SHOULD BROADCAST THE FULL EVERY HOUR... like SET A CLOCK HERE ( minute ) // should be 60 
        public static uint LATEST_FBROADCAST_TICK = 0;

        // broadcasting net files method

        public static List<BroadcastInfo> BroadcastQueue = new List<BroadcastInfo>();
        public static void BroadCast(BroadcastInfo b)
        {

            uint unixTimestamp = (uint)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMinutes; // min ! 

            switch (b.flag)
            {
                case 1:
                    NT.BroadcastFile(b.filePath, b.header);
                    BroadcastQueue.RemoveAt(0);
                    break;
                case 2:
                    if (unixTimestamp > LATEST_FBROADCAST_TICK + BROADCAST_FULL_BLOCKCHAIN_CLOCK)
                    {
                        //NT.BroadcastFile(GetLatestBlockChainFilePath(), 1);
                        uint latestIndex = RequestLatestBlockIndex(true);
                        Print("broadcast = " + latestIndex);
                        NT.BroadcastBlockchain(1, latestIndex);
                        BroadcastQueue = new List<BroadcastInfo>();
                        LATEST_FBROADCAST_TICK = unixTimestamp;
                    }
                    else
                    {
                        uint latestIndex = RequestLatestBlockIndex(true);
                        int startI = (int)(latestIndex - BROADCAST_BLOCKS_LIMIT);
                        Print("broadcast = " + latestIndex);
                        if (startI < 1) { startI = 1; ; }
                        NT.BroadcastBlockchain((uint)startI, latestIndex);
                        BroadcastQueue.RemoveAt(0);

                    }
                    break;
            }




        }

        // proccessing net files chunk methods

        public static string ConcatenateDL(string index)
        {
            string[] files = Directory.GetFiles(_folderPath + "net");
            string _path = _folderPath + "net/" + index.ToString();
            File.WriteAllBytes(_path, new byte[0]); // missing 4 bytes // USE THIS FOR CREATING A FILE! NO FILECREATE SVP! 
            List<uint> flist = new List<uint>();
            foreach (string s in files)
            {

                if (s.Contains(_path))
                {
                    uint result;
                    if (uint.TryParse(s.Replace(_path + "_", ""), out result))
                    {
                        flist.Add(Convert.ToUInt32(s.Replace(_path + "_", "")));
                    }

                }
            }
            flist.Sort();
            foreach (uint i in flist)
            {
                string fPath = _path + "_" + i.ToString();
                AppendBytesToFile(_path, File.ReadAllBytes(fPath));
                File.Delete(fPath);
            }
            Print(new FileInfo(_path).Length.ToString());
            return _path;
        }
        public static void RemoveAllConcatenateDL()
        {
            string[] files = Directory.GetFiles(_folderPath + "net");
            foreach (string s in files)
            {
                if (!s.Replace(_folderPath + "net", "").Contains("_"))
                {
                    File.Delete(s);
                }
            }
        }

    }
}
