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
    /*
     * We don't actually need Proof Of Work
     * We have to implement a new way to validate blocks using probably system clock. 
     * We have to find a way to time sync every peer even if there are not at the same place
     */
    public partial class Program
    {
        public static List<Tuple<bool, string>> PendingBlockFiles = new List<Tuple<bool, string>>();
        public static List<Tuple<bool, string>> PendingPTXFiles = new List<Tuple<bool, string>>();

        
        
        // Main Thread of our blockchain

        static void Main(string[] args)
        {
            CheckFilesAtRoot();
            VerifyFiles();
            PrintArgumentInfo();

            if ( ValidYesOrNo("BimWatch configuration will start."))
                 BIMWATCH.ConfigurePort();
            if (ValidYesOrNo("XY Poem configuration will start."))
                XYPoem.ConfigurePort();

            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                GetInput();
            }).Start();
            /*
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                MagicKey();
            }).Start();
            */
            while (true)
            {
                try
                {
                    // ------------- PART 1
                    for (int i = PendingDLBlocks.Count - 1; i >= 0; i--)
                    {
                        Print("will concatenate dl at " + i.ToString() + PendingDLBlocks[i]);
                        PendingBlockFiles.Add(new Tuple<bool, string>(false, ConcatenateDL(PendingDLBlocks[i])));
                        Print(" we added " + PendingBlockFiles[i].Item2);
                        PendingDLBlocks.RemoveAt(i);
                    }
                    for (int i = PendingDLTXs.Count - 1; i >= 0; i--)
                    {
                        PendingPTXFiles.Add(new Tuple<bool, string>(false, ConcatenateDL(PendingDLTXs[i])));
                        PendingDLTXs.RemoveAt(i);
                    }
                    // ------------- PART 2
                    for (int i = PendingBlockFiles.Count - 1; i >= 0; i--) // un bloc est relancé deux fois ... 
                    {

                        ProccessTempBlocks(PendingBlockFiles[i].Item2, PendingBlockFiles[i].Item1);
                        PendingBlockFiles.RemoveAt(i); // 
                    }
                    for (int i = PendingPTXFiles.Count - 1; i >= 0; i--)
                    {
                        ProccessTempTXforPending(PendingPTXFiles[i].Item2, PendingPTXFiles[i].Item1);
                        PendingPTXFiles.RemoveAt(i);
                    }
                    if (PendingBlockFiles.Count == 0 && PendingPTXFiles.Count == 0 && BroadcastQueue.Count > 0)
                    {
                        // clean files containing pID in dll folder...
                        foreach (string s in dlRemovalQueue)
                        {
                            string[] files = Directory.GetFiles(Program._folderPath + "net");
                            foreach (string f in files)
                            {
                                if (f.Contains(s.ToString()))
                                {
                                    File.Delete(f);
                                }
                            }
                            Print("will delete files with " + s.ToString());
                        }
                        dlRemovalQueue = new List<string>();

                        if (NT != null)
                        {

                            // first remove the extendedpeer if need to be cleaned ...  
                            for (int n = peerRemovalQueue.Count - 1; n >= 0; n--)
                            {
                                string ip = peerRemovalQueue[n];
                                for (int i = NT.mPeers.Count - 1; i >= 0; i--)
                                {
                                    if (NT.mPeers[i].IP == ip)
                                    {
                                        NT.mPeers[i].Peer.Close();
                                        string _ip = NT.mPeers[i].IP;
                                        Int32 Port = 13000; //....
                                        NT.mPeers.RemoveAt(i);
                                        new Thread(() =>
                                        {
                                            Thread.CurrentThread.IsBackground = true;
                                            NT.Connect(ip, Port);
                                        }).Start();
                                        Print("peers " + _ip + " removed due to inactivity!");
                                        peerRemovalQueue.RemoveAt(n);
                                        break;
                                    }
                                }
                            }
                            bool _cS = false;
                            foreach (network.ExtendedPeer ex in NT.mPeers) // just send to everyone normally ... 
                            {
                                if (ex.currentlySending)
                                {
                                    _cS = true;
                                    break;
                                }
                            }
                            if (!_cS)
                            {
                                BroadCast(BroadcastQueue[0]);
                            }
                            else
                            {
                                //Console.WriteLine("currently sending");
                            }

                        }
                        else
                        {
                            PendingBlockFiles = new List<Tuple<bool, string>>();
                            PendingPTXFiles = new List<Tuple<bool, string>>();
                            BroadcastQueue = new List<BroadcastInfo>();
                        }
                    }
                    if ( PendingBlockFiles.Count == 0 && BroadcastQueue.Count == 0 && MININGENABLED && NT != null) // only mine when there is no pendingblock. 
                    {
                        if (NTIMES == 0)
                        {
                                Console.WriteLine("mining in finito");
                                // wait that pendingblockfiles are empty and broadcast is empty
                              
                                if (STOP_MINING)
                                {
                                    STOP_MINING = false;
                                    MININGENABLED = false;
                                }
                                // thread this ... .
                                string winblockPath = StartMining(MYMINERPKEY, MYUTXOPOINTER, MAXLOCKTIMESETTING, 1);
                                // need to thread this stuff ... 
                                if (winblockPath.Length != 0)
                                {

                                    PendingBlockFiles.Add(new Tuple<bool, string>(true, winblockPath));
                                }
                                else
                                {
                                    Console.WriteLine("le chemin d'acces au bloc miné a été vide... ");
                                }
                        }
                        else
                        {
                            Console.WriteLine("start mining " + NTIMES + " times ");
                            string winblockPath = StartMining(MYMINERPKEY, MYUTXOPOINTER, MAXLOCKTIMESETTING,NTIMES);
                            if (winblockPath.Length != 0)
                            {
                                PendingBlockFiles.Add(new Tuple<bool, string>(true, winblockPath));
                            }
                            else
                            {
                                Console.WriteLine("le chemin d'acces au bloc miné a été vide... ");
                            }
                            MININGENABLED = false;

                        }
                    }
                }
                catch ( Exception e)
                {
                    if ( e is NullReferenceException || e is ArgumentOutOfRangeException || e is ArgumentNullException || e is FileNotFoundException)
                    {
                        if ( SECURE_MODE)
                        {
                            RemoveAllConcatenateDL();
                            PendingDLBlocks = new List<string>();
                            PendingDLTXs = new List<string>();
                            PendingBlockFiles = new List<Tuple<bool, string>>();
                            PendingPTXFiles = new List<Tuple<bool, string>>();
                            BroadcastQueue = new List<BroadcastInfo>();
                            peerRemovalQueue = new List<string>();
                            dlRemovalQueue = new List<string>();
                            Console.WriteLine(e.ToString());
                        }
                       
                        // remove net temp .... 
                    }
                    else
                    {
                        Print(e.ToString());
                        return;
                    }
                   
                    
                }
                
            }

        }
        
  
    }


}
