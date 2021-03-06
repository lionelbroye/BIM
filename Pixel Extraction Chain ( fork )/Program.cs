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
namespace firstchain
{
    /*
     * Gerer les empreintes de fichiers lors de l'upload 
     * --> produire un ID unique! ex ? : mon empreinte unix + le nombre d'envoie dans la seconde ( faire un thread qui reset scompteur chaque seconde )
     * 
     * Gestion des fichier TX ( inscrire une TX si seulement elle n'existe pas dans notre PTX file . 
     * 
     * Test des bonnes receptions de transactions dans le reseau et validation dans les blocs.... 
     * 
     * 
     */
    class Program
    {
        public class Block
        {
            public uint Index { get; } // 4 o
            public byte[] Hash { get; } // 32 o
            public byte[] previousHash { get; } // 32 o
            public uint DataSize { get; } // 4 o
            public List<PixelWrapper.PixTX> Data { get; } // 1100 o * datasize
            public uint TimeStamp { get; } // 4 o
            public MinerToken minerToken { get; } // 44 o
            public byte[] HashTarget { get; } // 32 o
            public uint Nonce { get; } // 4 o 

            public Block(uint index, byte[] hash, byte[] ph, List<PixelWrapper.PixTX> data, uint ts, MinerToken mt, byte[] hashtarget, uint nonce)
            {
                this.Index = index;
                this.Hash = hash;
                this.previousHash = ph;
                this.Data = data;
                this.TimeStamp = ts;
                this.minerToken = mt;
                this.HashTarget = hashtarget;
                this.Nonce = nonce;
                this.DataSize = (uint)data.Count;
            }

        }
 
        public class MinerToken
        {
            public byte[] MinerPKEY { get; }
            public ulong mUPOIndex { get; }
            public uint MiningReward { get; }

            public MinerToken(byte[] hashpkey, ulong utxoP, uint reward)
            {
                this.MinerPKEY = hashpkey;
                this.mUPOIndex = utxoP;
                this.MiningReward = reward;
            }
        }

        public static string _folderPath = "";

        public static uint BLOCKCHAIN_FILE_CHUNK = 30000000;
        public static uint PTX_MAX_SIZE = 50000000;
        public static byte[] CURRENT_HASH_TARGET;

        //--------------------------------------------------- MAIN CONSENSUS PARAMS : ( if you change this, you have to init a new blockchain ) 

        public static uint MONOCHROME_SIZE = 1000;
        public static uint GENESIS_PIXEL_VALUE = 1; // this is the minimum value
        public static uint WINNING_RUN_DISTANCE = 6; // LONGEST CHAIN WIN RULES DISTANCE
        public static uint MAX_RETROGRADE = 10; // WHAT IS THE MAXIMUM OF DOWNGRADING BLOCKCHAIN FOR A LIGHT FORK.
        public static uint TARGET_CLOCK = 42; // 2016. NEW HASH TARGET REQUIRED EVERY TARGET_CLOCKth BLOCK
        public static uint TIMESTAMP_TARGET = 11; // TIMESTAMP BLOCK SHOULD BE HIGHER THAN MEDIAN OF LAST TIMESTAMP_TARGETth BLOCK
        public static uint TARGET_TIME = 40320; // 1209600 . number of seconds a block should be mined 10 *  ---> WE SHOULD GET ONE BLOCK EVERY 10S . this is working !!! 
        public static uint TARGET_DIVIDER_BOUNDARIES = 4; // 4. LIMIT OF NEW TARGET BOUNDARIES (QUARTER + AND QUARTER - )
        public static uint FIRST_UNIX_TIMESTAMP = 1598981949;
        public static uint NATIVE_REWARD = 50; // COIN GIVE TO FIRST REWARD_DIVIDER_CLOCKth BLOCK
        public static uint REWARD_DIVIDER_CLOCK = 210000; // NUMBER OF BLOCK BEFORE NATIVE REWARD SHOULD BE HALFED
        public static byte[] MAXIMUM_TARGET = StringToByteArray("00000000FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"); //< MAX HASH TARGET. MINIMUM DIFFICULTY.
                                                                                                                                     //--------------------------------------------------------------------------------------------------------------------------

        //--------------------------------------- BROADCAST INFO --------------------------------------------
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
        public static List<uint> PendingDLBlocks = new List<uint>();
        public static List<uint> PendingDLTXs = new List<uint>();
        public static List<Tuple<bool, string>> PendingBlockFiles = new List<Tuple<bool, string>>();
        public static List<Tuple<bool, string>> PendingPTXFiles = new List<Tuple<bool, string>>();
        public static uint BROADCAST_BLOCKS_LIMIT = 30; // MAX NUMBER OF BLOCK I WANT TO BROADCAST WHEN UPLOADING MY BC. IF 0. BROADCAST THE FULL. 
        public static uint BROADCAST_FULL_BLOCKCHAIN_CLOCK = 30; // I SHOULD BROADCAST THE FULL EVERY HOUR... like SET A CLOCK HERE ( minute ) // should be 60 
        public static uint LATEST_FBROADCAST_TICK = 0;
        public static List<BroadcastInfo> BroadcastQueue = new List<BroadcastInfo>();
        public static List<string> peerRemovalQueue = new List<string>();
        public static List<uint> dlRemovalQueue = new List<uint>();
        static void Main(string[] args)
        {

            //arduino.Initialize();
            CheckFilesAtRoot();
            VerifyFiles();
            PrintArgumentInfo();
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                GetInput();
            }).Start();

            while (true)
            {
                for (int i = PendingDLBlocks.Count - 1; i >= 0; i--)
                {
                    PendingBlockFiles.Add(new Tuple<bool, string>(false, ConcatenateDL(PendingDLBlocks[i])));
                    Console.WriteLine(" we added " + PendingBlockFiles[PendingBlockFiles.Count - 1].Item2);
                    PendingDLBlocks.RemoveAt(i);
                }
                for (int i = PendingDLTXs.Count - 1; i >= 0; i--)
                {
                    PendingPTXFiles.Add(new Tuple<bool, string>(false, ConcatenateDL(PendingDLTXs[i])));
                    PendingDLTXs.RemoveAt(i);
                }
                for (int i = PendingBlockFiles.Count - 1; i >= 0; i--) // un bloc est relancé deux fois ... 
                {
                    foreach (Tuple<bool, string> pp in PendingBlockFiles)
                    {
                        Console.WriteLine(pp.Item2);
                    }
                    ProccessTempBlocks(PendingBlockFiles[i].Item2, PendingBlockFiles[i].Item1);
                    PendingBlockFiles.RemoveAt(i); // 
                }
                for (int i = PendingPTXFiles.Count - 1; i >= 0; i--)
                {
                    PixelWrapper.ProccessTempTXforPending(PendingPTXFiles[i].Item2, PendingPTXFiles[i].Item1);
                    PendingPTXFiles.RemoveAt(i);
                }
                if (PendingBlockFiles.Count == 0 && PendingPTXFiles.Count == 0 && BroadcastQueue.Count > 0)
                {
                    // clean files containing pID in dll folder...
                    foreach (uint s in dlRemovalQueue)
                    {
                        string[] files = Directory.GetFiles(Program._folderPath + "net");
                        foreach (string f in files)
                        {
                            if (f.Contains(s.ToString()))
                            {
                                File.Delete(f);
                            }
                        }
                        Console.WriteLine("will delete files with " + s.ToString());
                    }
                    dlRemovalQueue = new List<uint>();

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
                                    Console.WriteLine("peers " + _ip + " removed due to inactivity!");
                                    peerRemovalQueue.RemoveAt(n);
                                    break;
                                }
                            }
                        }
                        bool _cS = false;
                        foreach (network.ExtendedPeer ex in NT.mPeers)
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
            }


        }
        public static string ConcatenateDL(uint index)
        {
            string[] files = Directory.GetFiles(_folderPath + "net");
            string _path = _folderPath + "net\\" + index.ToString();
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
            Console.WriteLine(new FileInfo(_path).Length);
            return _path;
        }
        //----------------------- RAW BROADCAST COMMAND     ---------------------
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
                        uint latestIndex = RequestLatestBlockIndex(true);
                        Console.WriteLine("broadcast = " + latestIndex);
                        NT.BroadcastBlockchain(1, latestIndex);
                        BroadcastQueue = new List<BroadcastInfo>();
                        LATEST_FBROADCAST_TICK = unixTimestamp;
                    }
                    else
                    {
                        uint latestIndex = RequestLatestBlockIndex(true);
                        int startI = (int)(latestIndex - BROADCAST_BLOCKS_LIMIT);
                        Console.WriteLine("broadcast = " + latestIndex);
                        if (startI < 1) { startI = 1; ; }
                        NT.BroadcastBlockchain((uint)startI, latestIndex);
                        BroadcastQueue.RemoveAt(0);

                    }
                    break;
            }




        }
        //----------------------- COMMAND     ---------------------
        public static void GetInput()
        {

            while (true)
            {
                bool argumentFound = false;
                string argument = Console.ReadLine();
                if (argument == "getbcinfo")
                {
                    PrintChainInfo();
                    argumentFound = true;
                }
                if (argument.Contains("rqshom"))
                {
                    string s = argument.Replace("rqshom", "");
                    s = s.Replace(" ", "");
                    int index = 0;
                    if (int.TryParse(s, out index))
                    {
                        DateTime today = DateTime.Now;
                        DateTime tomorrow = today.AddDays(1);
                        SHOM.GetSHOMData(index, today, tomorrow);
                        argumentFound = true;
                    }
                    else
                    {
                        Console.WriteLine("bad arguments ");
                    }
                    argumentFound = true;

                }
                if (argument.Contains("setbrcparam"))
                {
                    string maxArgs = GetStringAfterArgs(argument, "max:");
                    string fbcArgs = GetStringAfterArgs(argument, "fbc:");
                    uint max = 0;
                    uint fbc = 0;
                    if (uint.TryParse(maxArgs, out max))
                    {
                        BROADCAST_BLOCKS_LIMIT = max;
                        Console.WriteLine("new broadcast blocks max limit : " + BROADCAST_BLOCKS_LIMIT);
                    }
                    if (uint.TryParse(fbcArgs, out fbc))
                    {
                        BROADCAST_FULL_BLOCKCHAIN_CLOCK = fbc;
                        Console.WriteLine("full blockchain will be broadcast every " + BROADCAST_FULL_BLOCKCHAIN_CLOCK + " minutes. ");
                    }
                    argumentFound = true;
                }
                if (argument == "netconnect")
                {
                    uint unixTimestamp = (uint)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMinutes;
                    LATEST_FBROADCAST_TICK = unixTimestamp;
                    NT = new network();
                    NT.Initialize();
                    argumentFound = true;
                }
                if (argument == "nettest 1")
                {
                    if (NT != null)
                    {
                        NT.BroadcastFile(GetLatestBlockChainFilePath(), 1);
                    }
                    else
                    {
                        Console.WriteLine("You're offline. Please use netconnect to get online. ");
                    }

                    argumentFound = true;
                }
                if (argument.Contains("nettest 2"))
                {
                    if (NT != null)
                    {
                        string s = argument.Replace("nettest 2", "");
                        s = s.Replace(" ", "");
                        string[] args = s.Split(':');
                        if (args.Length == 2)
                        {
                            uint start = 0;
                            uint end = 0;
                            if (uint.TryParse(args[0], out start) && uint.TryParse(args[1], out end))
                            {
                                Console.WriteLine("sending from " + start + " to " + end);
                                NT.BroadcastBlockchain(start, end);
                            }
                        }
                        else
                        {
                            Console.WriteLine("wrong args");
                        }
                    }
                    else
                    {
                        Console.WriteLine("You're offline. Please use netconnect to get online. ");
                    }

                    argumentFound = true;
                }
                if (argument == "nettest 3")
                {
                    if (NT != null)
                    {
                        NT.BroadcastFile(_folderPath + "ptx", 2);
                    }
                    else
                    {
                        Console.WriteLine("You're offline. Please use netconnect to get online. ");
                    }

                    argumentFound = true;
                }

                if (argument.Contains("getblockinfo"))
                {
                    string s = argument.Replace("getblockinfo", "");
                    s = s.Replace(" ", "");
                    uint index = 0;
                    bool success = uint.TryParse(s, out index);
                    if (success)
                    {
                        Block b = GetBlockAtIndex(index);
                        if (b == null)
                        {
                            Console.WriteLine("Block can't be found.");
                        }
                        else
                        {
                            Console.WriteLine("[  block data  ]");
                            PrintBlockData(b);
                        }
                        argumentFound = true;
                    }
                    else
                    {
                        Console.WriteLine("invalid argument");
                    }
                    argumentFound = true;
                }
                if (argument == "createwallet")
                {
                    GenerateNewPairKey();
                    argumentFound = true;
                }
                if (argument.Contains("getupo"))
                {
                    string s = argument.Replace("getupo", "");
                    s = s.Replace(" ", "");
                    uint index = 0;
                    bool success = uint.TryParse(s, out index);
                    if (success)
                    {
                        PixelWrapper.UPO upo = PixelWrapper.GetOfficialUPO_B(index);  
                        if (upo == null)
                        {
                            Console.WriteLine("UPO of this public Key not existing in Pixel Database ");
                        }
                        else
                        {
                            //PrintUPO(upo);
                            Console.WriteLine("UPO Hash    :" + SHAToHex(upo.HashKey, false));
                            Console.WriteLine("UPO Sold    :" + upo.Sold);
                            Console.WriteLine("UPO Token   :" + upo.TokenOfUniqueness);
                            Console.WriteLine("Pixels mined:" + upo.pixelsSize);
                            // should had more info ... 
                        }
                        argumentFound = true;
                    }
                }
                if (argument.Contains("getupoindex"))
                {
                    string path = argument.Replace("location", AppDomain.CurrentDomain.BaseDirectory);
                    path = getfilePath(path.ToCharArray());
                    if (path.Length == 0) { Console.WriteLine("invalid argument. Please set path of your public key file in quote. "); }
                    else
                    {
                        Console.WriteLine(path);

                        if (File.Exists(path))
                        {
                            byte[] pkeyHASH = ComputeSHA256(File.ReadAllBytes(path));
                            Console.WriteLine("result for hash key : " + SHAToHex(pkeyHASH, false));
                            ulong myUPOP = PixelWrapper.GetUPOIndex(pkeyHASH);
                            PixelWrapper.UPO myUPO = PixelWrapper.GetOfficialUPO(myUPOP, pkeyHASH);

                            if (myUPO != null)
                            {
                                Console.WriteLine("____________________________________");
                                Console.WriteLine("Your UPO Index is " + myUPOP);
                                Console.WriteLine("____________________________________");
                                Console.WriteLine("UPO Hash    :" + SHAToHex(pkeyHASH, false));
                                Console.WriteLine("UPO Sold    :" + myUPO.Sold);
                                Console.WriteLine("UPO Token   :" + myUPO.TokenOfUniqueness);
                                Console.WriteLine("Pixels mined:" + myUPO.pixelsSize);
                            }
                            else
                            {
                                Console.WriteLine("UTXO of this public Key not existing in UTXO Set ");
                            }

                        }
                        else
                        {
                            Console.WriteLine("Path : " + path + " does not exist. Please set path of your public key file. ");
                        }
                        argumentFound = true;
                    }
                }
                if (argument == "verifychain")
                {
                    if (ValidYesOrNo("[WARNING] Verify blockchain can take a lot of time."))
                    {
                        Console.WriteLine("Verifying Blockchain...");
                        if (isBlockChainValid())
                        {
                            Console.WriteLine("Blockchain is Valid! ");
                        }
                        else
                        {
                            Console.WriteLine("Blockchain is not valid! ");
                        }

                    }

                    argumentFound = true;
                }
                if (argument == "buildutxos")
                {
                    if (ValidYesOrNo("[WARNING] It will delete current UTXO Set, then rebuilt it can take a lot of time."))
                    {
                        Console.WriteLine("Please wait during UTXO Set writting...");
                        BuildUTXOSet();
                        Console.WriteLine("UTXO Set writting finished!");
                    }
                    argumentFound = true;
                }
                if (argument == "initchain")
                {

                    if (ValidYesOrNo("[WARNING] It will delete UTXO Set, blockchain files, PTX file and forks. You will be disconnected! "))
                    {
                        NT = null; // we should close nt
                        ClearAllFiles(); CheckFilesAtRoot(); PrintChainInfo();
                    }
                    argumentFound = true;
                }

                if (argument.Contains("mine"))
                {
                    //mine pkey:[pkeypath] utxop:[utxop] minlock:[] ntime:[]
                    // find pkey
                    string pkeyArgs = GetStringAfterArgs(argument, "pkey:", '\"');
                    string utxopArgs = GetStringAfterArgs(argument, "utxop:");
                    string ntimeArgs = GetStringAfterArgs(argument, "ntime:");
                    if (pkeyArgs.Length != 0)
                    {
                        argumentFound = true;
                        ulong utxopointer = 0;
                        string path = pkeyArgs.Replace("location", AppDomain.CurrentDomain.BaseDirectory);
                        path = getfilePath(path.ToCharArray());
                        if (File.Exists(path))
                        {
                            byte[] pkeyHASH = ComputeSHA256(File.ReadAllBytes(path));
                            bool Continue = true;
                            if (utxopArgs.Length == 0)
                            {
                                if (!ValidYesOrNo("[WARNING] You didn't set an UTXO pointer.")) { Continue = false; }
                            }
                            else
                            {
                                ulong parsePointer = 0;
                                if (!ulong.TryParse(utxopArgs, out parsePointer) && Continue)
                                {
                                    if (!ValidYesOrNo("[WARNING] Incorrect UTXO pointer") && Continue) { Continue = false; }
                                }
                                else
                                {
                                    PixelWrapper.UPO utxo = PixelWrapper.GetOfficialUPO(parsePointer, pkeyHASH );
                                    if (utxo == null && Continue) { if (!ValidYesOrNo("[WARNING] Incorrect UTXO pointer")) { Continue = false; } }
                                    else
                                    {
                                        if (!utxo.HashKey.SequenceEqual(pkeyHASH) && Continue)
                                        {
                                            if (!ValidYesOrNo("[WARNING] Incorrect UTXO pointer"))
                                            { Continue = false; }
                                        }

                                    }
                                    if (parsePointer == 0 && Continue) { if (!ValidYesOrNo("[WARNING] Incorrect UTXO pointer")) { Continue = false; } }
                                    utxopointer = parsePointer;
                                }

                            }
                            if (Continue)
                            {
                                uint nTime = 0;
                                if (ntimeArgs.Length != 0)
                                {
                                    uint.TryParse(ntimeArgs, out nTime);
                                }
                                if (nTime == 0)
                                {
                                    while (true)
                                    {
                                        Console.WriteLine("mining in finito");
                                        // wait that pendingblockfiles are empty and broadcast is empty
                                        if (NT != null)
                                        {
                                            while (PendingBlockFiles.Count != 0 || BroadcastQueue.Count != 0)
                                            {
                                                Console.WriteLine(PendingBlockFiles.Count + " " + BroadcastQueue.Count);
                                                Thread.Sleep(500);
                                            }
                                        }
                                        StartMining(pkeyHASH, utxopointer, 1);
                                        PendingBlockFiles.Add(new Tuple<bool, string>(true, _folderPath + "winblock"));
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("start mining " + nTime + " times ");
                                    StartMining(pkeyHASH, utxopointer, nTime);
                                    PendingBlockFiles.Add(new Tuple<bool, string>(true, _folderPath + "winblock"));

                                }



                                argumentFound = true;
                            }

                        }
                        else
                        {
                            Console.WriteLine("Path : " + path + " does not exist. Please set path of your public key file. ");
                        }
                    }
                    else
                    {
                        Console.WriteLine("invalid argument. Please set path of your public key file in quote. ");
                    }

                }
                if (argument.Contains("reqtx"))
                {
                    argument = argument.Replace("reqtx", "");
                    argument = argument.Replace("location", AppDomain.CurrentDomain.BaseDirectory);
                    string txPath = getfilePath(argument.ToCharArray());
                    if (File.Exists(txPath))
                    {
                        PendingPTXFiles.Add(new Tuple<bool, string>(true, txPath));
                        argumentFound = true;
                    }
                    else
                    {
                        Console.WriteLine("Path : " + txPath + " does not exist. Please set path of your public key file. ");
                    }
                }
                if (!argumentFound)
                {
                    Console.WriteLine("invalid input. type getcmdinfo for command information. ");
                }
                if (argument.Contains("getcmdinfo"))
                {
                    PrintArgumentInfo();
                }
                Console.WriteLine("");

            }
        }
        static string GetStringAfterArgs(string searchstring, string arg, char specificdelimiter = ' ')
        {
            int startIndex = searchstring.IndexOf(arg);
            int delimcounter = 0;
            if (startIndex == -1) { return ""; }
            List<char> result = new List<char>();
            for (int i = startIndex + arg.Length; i < searchstring.Length; i++)
            {
                if (searchstring[i] == specificdelimiter && specificdelimiter == ' ')
                {
                    break;

                }
                else
                {
                    if (specificdelimiter == '\"' && searchstring[i] == '\"')
                    {
                        delimcounter++;
                        if (delimcounter == 2)
                        {
                            break;
                        }
                    }
                }
                result.Add(searchstring[i]);
            }
            char[] chars = new char[result.Count];
            for (int i = 0; i < result.Count; i++)
            {
                chars[i] = result[i];
            }
            return new string(chars);
        }
        static bool ValidYesOrNo(string warning)
        {
            //bool confirmed = false;
            //string Key;
            Console.WriteLine(warning);
            ConsoleKey response;
            do
            {
                Console.Write("Do you want to procceed ? [y/n] ");
                response = Console.ReadKey(false).Key;   // true is intercept key (dont show), false is show
                if (response != ConsoleKey.Enter)
                    Console.WriteLine();

            } while (response != ConsoleKey.Y && response != ConsoleKey.N);

            if (response == ConsoleKey.Y)
            {
                return true;
            }
            else
            {
                return false;
            }

        }
        static string getfilePath(char[] searchstring)
        {
            string filepath = "";// search in the quote -- we absolutely need quote
            int index = -1;
            for (int i = 0; i < searchstring.Length; i++)
            {
                if (searchstring[i] == '\"')
                {
                    index = i + 1;
                    break;
                }
            }
            if (index > -1)
            {
                for (int i = index; i < searchstring.Length; i++)
                {
                    if (searchstring[i] == '\"')
                    {
                        break;
                    }
                    filepath += searchstring[i].ToString();
                }
            }

            return filepath;
        }

        //----------------------- INFO        ---------------------
        public static void PrintArgumentInfo()
        {

            Console.WriteLine("-----------------");
            Console.WriteLine("Get Blockchain Info      -> getbcinfo");
            Console.WriteLine("Get Block Data at Index  -> getblockinfo #");
            Console.WriteLine("Create Wallet (RSA 4096) -> createwallet");
            Console.WriteLine("Get UTXO Data at Index   -> getutxo #");
            Console.WriteLine("Find UTXO Pointer        -> getutxop pkey:#");
            Console.WriteLine("Verify Blokchain         -> verifychain");
            Console.WriteLine("Rebuild UTXO Set         -> buildutxos");
            Console.WriteLine("Init App                 -> initchain");
            Console.WriteLine("Mine                     -> mine pkey:# utxop:# minlock:# ntime:#");
            ////newtx sprkey: spukey: sutxop: amount: rpukey: rutxop: fee: lock:
            Console.WriteLine("Add Pending Transaction  -> reqtx #");
            Console.WriteLine("Connect to nodes         -> netconnect");
            Console.WriteLine("Set Broadcast parameter  -> setbrcparam max:# fbc:#");
            Console.WriteLine("Print this               -> getcmdinfo");
            Console.WriteLine("");
        }
        public static void PrintBlockData(Block b)
        {

            Console.WriteLine("-----------------");
            Console.WriteLine("index         : " + b.Index);
            Console.WriteLine("merkle root   : " + SHAToHex(b.Hash, false));
            Console.WriteLine("previous hash : " + SHAToHex(b.previousHash, false));
            Console.WriteLine("#TX           : " + b.DataSize);
            foreach (PixelWrapper.PixTX TXS in b.Data)
            {
                PrintTXData(TXS);
            }
            Console.WriteLine("miner hashkey : " + SHAToHex(b.minerToken.MinerPKEY, false));
            Console.WriteLine("hash target   : " + SHAToHex(b.HashTarget, false));
            Console.WriteLine("nonce         : " + b.Nonce);
            Console.WriteLine("");
        }
        public static void PrintTXData(PixelWrapper.PixTX TX)
        {
            Console.WriteLine("-----------------");
            Console.WriteLine("sender           : " + SHAToHex(ComputeSHA256(TX.sPKey), false));
            Console.WriteLine("extraction count : " + TX.ExtractionSize);
            Console.WriteLine("pixel value      : " + TX.Value);
            Console.WriteLine("fee              : " + TX.TxFee);
            Console.WriteLine("-----------------");
        }
        public static void PrintUPO ( PixelWrapper.UPO upo)
        {
            Console.WriteLine("-----------------");
            Console.WriteLine("public key       : " + SHAToHex(ComputeSHA256(upo.HashKey), false));
            Console.WriteLine("token used       : " + upo.TokenOfUniqueness);
            Console.WriteLine("sold             : " + upo.Sold);
            Console.WriteLine("  pixel possessed  ");
            Console.WriteLine("___________________");
            foreach ( PixelWrapper.Pixel pix in upo.Pixels)
            {
                Console.Write("["+pix.position.X+","+ pix.position.Y+ "]");
            }
            Console.WriteLine("-----------------");
        }
        public static void PrintChainInfo()
        {

            uint i = RequestLatestBlockIndex(false);
            uint a = RequestLatestBlockIndex(true);
            Console.WriteLine("[blockchain info]");
            Console.WriteLine("-----------------");
            Console.WriteLine("blockchain length   : " + a);
            Console.WriteLine("your fork length    : " + i);
            uint cv = GetCurrencyVolume(a);
            uint reward = GetMiningReward(RequestLatestBlockIndex(true));
            Console.WriteLine("currency volume     : " + cv);
            Console.WriteLine("mining reward       : " + reward);
            Console.WriteLine("current hash target : " + SHAToHex(CURRENT_HASH_TARGET, false));
            //difficulty = difficulty_1_target / current_target
            BigInteger MAXTARGET = BytesToUint256(MAXIMUM_TARGET);
            BigInteger CURRTARGET = BytesToUint256(CURRENT_HASH_TARGET);
            BigInteger DIFF = BigInteger.Divide(MAXTARGET, CURRTARGET);
            Console.WriteLine("current difficulty : " + DIFF.ToString());
            Block genesis = GetBlockAtIndex(0);
            Block latest = GetBlockAtIndex(RequestLatestBlockIndex(true));
            if (genesis == null || latest == null) { Console.WriteLine("[WARNING] Your blockchain files are corrupted!"); return; }
            Console.WriteLine("[  genesis data   ]");
            PrintBlockData(genesis);
            Console.WriteLine("[latest block data]");
            PrintBlockData(latest);
            Console.WriteLine("");
        }

        //------------------------ INIT       ---------------------
        public static void CheckFilesAtRoot() // check if file are in folder to process
        {
            _folderPath = AppDomain.CurrentDomain.BaseDirectory + "\\";
            Console.WriteLine(_folderPath);
            

            if (!Directory.Exists(_folderPath + "pixelstate")) // create a pixelstate forlder
            {
                Directory.CreateDirectory(_folderPath + "pixelstate");
                File.WriteAllBytes(_folderPath + "pixelstate\\0", new byte[8]);
                Console.WriteLine("pixel state data will be created..... ");
                if ( File.Exists(_folderPath + "pixelstate\\0"))
                {
                    Console.WriteLine(_folderPath);
                    Console.WriteLine("pixel state data has been created.... ");
                }
                //Environment.Exit(0);
            }
            
            if (!File.Exists(_folderPath + "genesis")) // create genesis and upload upo
            {
                CreateGenesis();
                Console.WriteLine("creating genesis ");
            }
            if (!Directory.Exists(_folderPath + "blockchain"))
            {
                Directory.CreateDirectory(_folderPath + "blockchain");
                File.WriteAllBytes(_folderPath + "blockchain\\0", new byte[4]);
                AppendBytesToFile(_folderPath + "blockchain\\0", File.ReadAllBytes(_folderPath + "genesis"));
                File.WriteAllBytes(_folderPath + "blockchain\\1", new byte[4]);
                
            }
            if (!Directory.Exists(_folderPath + "net"))
            {
                Directory.CreateDirectory(_folderPath + "net");
            }
            else
            {
                Directory.Delete(_folderPath + "net", true);
                Directory.CreateDirectory(_folderPath + "net");
            }
            
            if (!File.Exists(_folderPath + "ptx"))
            {
                File.WriteAllBytes(_folderPath + "ptx", new byte[0]);
            }
            if (!Directory.Exists(_folderPath + "fork"))
            {
                Directory.CreateDirectory(_folderPath + "fork");
            }

            PixelWrapper.LATEST_UPO_INDEX = PixelWrapper.GetLastUPOIndex();
            Console.WriteLine("UPO Number : " + PixelWrapper.LATEST_UPO_INDEX);
            UpdateHashTarget();
        }

        public static void CreateGenesis() // WILL CREATE A BLACK MONOLYTH GENESIS
        {
            byte[] gen = Convert.FromBase64String("im a a genesis block");
            for (int i = 0; i < 10; i++)
            {
                gen = ComputeSHA256(gen);
            }
            BigInteger b1 = BytesToUint256(MAXIMUM_TARGET);
            byte[] firsttarget = Uint256ToByteArray(b1); //< firsttarget is correctly write! 

            // BUILD THE GENESIS MONOCHROME TRANSACTION UPO
            List<PixelWrapper.Pixel> pixels = new List<PixelWrapper.Pixel>();
            for (int y = 0; y < MONOCHROME_SIZE; y++) // pixels have to be ordered from top left to bottom right
            {
                for (int x = 0; x < MONOCHROME_SIZE; x++)
                {
                    pixels.Add(new PixelWrapper.Pixel(new PixelWrapper.Vector(x, y), 0, 0, GENESIS_PIXEL_VALUE));
                }
            }
            PixelWrapper.UPO genesisUPO = new PixelWrapper.UPO(new byte[32], 0, 0, pixels);
            PixelWrapper.AddDust(genesisUPO);
            Block Genesis = new Block(0, gen, gen, new List<PixelWrapper.PixTX>(), FIRST_UNIX_TIMESTAMP, new MinerToken(new byte[32], 0, 0), firsttarget, 0);

            byte[] bytes = BlockToBytes(Genesis);
            File.WriteAllBytes(_folderPath + "genesis", bytes);
        }
        public static void ClearAllFiles() // CLEAR ALL FILES ( GENESIS, UTXO SET, PENDING TRANSACTION, FORKS, AND BLOCKCHAIN FILES. 
        {
            _folderPath = AppDomain.CurrentDomain.BaseDirectory + "\\";
            if (File.Exists(_folderPath + "genesis"))
            {
                File.Delete(_folderPath + "genesis");
            }
            if (File.Exists(_folderPath + "ptx"))
            {
                File.Delete(_folderPath + "ptx");
            }
            if (Directory.Exists(_folderPath + "pixelstate"))
            {
                Directory.Delete(_folderPath + "pixelstate", true);
            }
            if (Directory.Exists(_folderPath + "fork"))
            {
                Directory.Delete(_folderPath + "fork", true);
            }
            if (Directory.Exists(_folderPath + "blockchain"))
            {
                Directory.Delete(_folderPath + "blockchain", true);
            }
            Console.WriteLine("All files have been cleared.");
            PixelWrapper.LATEST_UPO_INDEX = 0;
        }

        public static void VerifyFiles()
        {
            // we first verify every blockchain file
            string[] files = Directory.GetFiles(_folderPath + "blockchain");
            foreach (string s in files)
            {

                if (!isHeaderCorrectInBlockFile(s) &&RequestLatestBlockIndex(true)!=0)
                {
                    if (ValidYesOrNo("[WARNING] Your blockchain data is corrupted. Should reinitialize all files.")) { ClearAllFiles(); CheckFilesAtRoot(); return; }
                }

            }
            // then we verify every fork
            files = Directory.GetFiles(_folderPath + "fork");
            foreach (string s in files)
            {
                if (!isHeaderCorrectInBlockFile(s))
                {
                    if (ValidYesOrNo("[WARNING] Fork file " + s + " corrupted. Should delete this fork file ")) { File.Delete(s); }
                }

            }
            // then we verify utxo set  -> reminder : header is 4 bytes. (currency volume ). UTXO FORMAT is 40 bytes.
            /*
            uint fLenght = (uint)new FileInfo(_folderPath + "utxos").Length;
            if (fLenght < 4) { Console.WriteLine("utxo set file corrupted"); }
            fLenght -= 4;
            if (fLenght % 40 != 0 && fLenght != 4)
            {
                if (ValidYesOrNo("[WARNING] UTXO Set file corrupted. Should rebuild UTXO Set. ")) { BuildUTXOSet(); }
            } // we should absolutely then rebuild the utxo set. 
            
            // then we verify ptx  -> no header here. TX FORMAT is 1100 bytes.
            fLenght = (uint)new FileInfo(_folderPath + "ptx").Length;
            if (fLenght != 0 && fLenght % 1100 != 0)
            {
                if (ValidYesOrNo("[WARNING] PTX file corrupted. Should reinitialize ptx file ")) { File.Delete(_folderPath + "ptx"); CheckFilesAtRoot(); }
            }
            */
        }
        public static bool isHeaderCorrectInBlockFile(string _filePath) //< verify header correctness of a block file
        {
            uint latestIndex = RequestLatestBlockIndexInFile(_filePath);
            Block b = GetBlockAtIndexInFile(latestIndex, _filePath);
            if (b == null) { Console.WriteLine("no block at index specify by header :" + latestIndex); return false; }
            return true;
        }
        //------------------------ BYTE MANIP ---------------------

        public static List<byte> AddBytesToList(List<byte> list, byte[] bytes)
        {
            foreach (byte b in bytes) { list.Add(b); }
            return list;
        }
        public static byte[] ListToByteArray(List<byte> list)
        {
            byte[] result = new byte[list.Count];
            for (int i = 0; i < list.Count; i++) { result[i] = list[i]; }
            return result;
        }

        //-----------------    ARITH 256 BIT -------------------

        public static string HexToDecimal(string hex)
        {
            List<int> dec = new List<int> { 0 };   // decimal result

            foreach (char c in hex)
            {
                int carry = Convert.ToInt32(c.ToString(), 16);

                for (int i = 0; i < dec.Count; ++i)
                {
                    int val = dec[i] * 16 + carry;
                    dec[i] = val % 10;
                    carry = val / 10;
                }

                while (carry > 0)
                {
                    dec.Add(carry % 10);
                    carry /= 10;
                }
            }

            var chars = dec.Select(d => (char)('0' + d));
            var cArr = chars.Reverse().ToArray();
            return new string(cArr);
        }
        public static BigInteger BytesToUint256(byte[] bytes)
        {
            if (bytes.Length != 32) { Console.WriteLine("bytes to uint256 wrong format!"); return new BigInteger(0); }
            List<byte> DataBuilder = new List<byte>();
            DataBuilder = AddBytesToList(DataBuilder, bytes);
            DataBuilder = AddBytesToList(DataBuilder, new byte[1]); //< append a byte 0x00 for unsigned constructor.
            byte[] bconstructor = ListToByteArray(DataBuilder);
            return new BigInteger(bconstructor);
        }
        public static byte[] Uint256ToByteArray(BigInteger bi)
        {
            byte[] bytes = bi.ToByteArray();
            //BitConverter
            if (bytes.Length > 33)
            {
                Console.WriteLine("uint 256 wrong format.");
                return new byte[1];

            }
            // [1] first delete append byte ... 
            List<byte> DataBuilder = new List<byte>();
            for (int i = 0; i < bytes.Length - 1; i++)
            {
                DataBuilder.Add(bytes[i]);
            }
            uint padding = 32 - (uint)DataBuilder.Count;
            if (padding > 0)
            {
                UInt256 newUint = new UInt256(bi.ToByteArray()); //< we cannot construct an uint256 with less than 32 byte ... thats why 
                return newUint.ToByteArray();

            }
            else
            {
                return ListToByteArray(DataBuilder);
            }


        }

        public static void FatalErrorHandler(uint err, string msg = "")
        {

            if (ValidYesOrNo("[FATAL ERROR] Should reinit chain and close app : " + msg)) { ClearAllFiles(); Environment.Exit(0); }

        }


        //------------------------------------------------------
        public static MemoryMappedFile MemFile(string path) //< CONSTRUCTOR TO CREATE A  MAPPED FILE with fileshare.read ... 
        {
            return MemoryMappedFile.CreateFromFile(
                      //include a readonly shared stream
                      File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read),
                      //not mapping to a name
                      null,
                      //use the file's actual size
                      0L,
                      //read only access
                      MemoryMappedFileAccess.Read,
                      //not configuring security
                      null,
                      //adjust as needed
                      HandleInheritability.None,
                      //close the previously passed in stream when done
                      false);

        }
        public static byte[] GetBytesFromFile(uint startIndex, uint length, string _filePath)
        {
            // using (MemoryMappedFile memoryMappedFile = MemoryMappedFile.CreateFromFile(_filePath)) 
            //{
            // using (MemoryMappedViewStream memoryMappedViewStream = memoryMappedFile.CreateViewStream(startIndex, length, MemoryMappedFileAccess.Read))
            using (MemoryMappedFile memFile = MemFile(_filePath))
            {
                using (MemoryMappedViewStream memoryMappedViewStream = memFile.CreateViewStream(startIndex, length, MemoryMappedFileAccess.Read))
                {
                    byte[] result = new byte[length];
                    for (uint i = 0; i < length; i++)
                    {
                        result[i] = (byte)memoryMappedViewStream.ReadByte();
                    }
                    memoryMappedViewStream.Dispose();
                    return result;
                }
            }
        }
        public static void OverWriteBytesInFile(uint startIndex, string _filePath, byte[] bytes) // can result an error. cant use file get length. 
        {

            using (MemoryMappedFile memoryMappedFile = MemoryMappedFile.CreateFromFile(_filePath))
            {
                using (MemoryMappedViewStream memoryMappedViewStream = memoryMappedFile.CreateViewStream(startIndex, bytes.Length))
                {
                    memoryMappedViewStream.WriteAsync(bytes, 0, bytes.Length);
                    memoryMappedViewStream.Dispose();
                }
            }
        }
        public static void AppendBytesToFile(string _filePath, byte[] bytes)
        {

            using (FileStream f = new FileStream(_filePath, FileMode.Append))
            {
                f.Write(bytes, 0, bytes.Length);
                f.Dispose();
            }

        }
        public static void TruncateFile(string _filePath, uint length) // can result an error. cant use file get length. 
        {
            FileInfo fi = new FileInfo(_filePath);
            FileStream fs = new FileStream(_filePath, FileMode.Open);

            fs.SetLength(fi.Length - length);
            fs.Close();
        }

       
        public static MinerToken BytesToMinerToken(byte[] bytes) // CAN RESULT NULL
        {
            if (bytes.Length != 44) { return null; }
            byte[] hashKey = new byte[32];
            byte[] utxoP = new byte[8];
            byte[] reward = new byte[4];
            uint byteOffset = 0;
            for (uint i = byteOffset; i < byteOffset + 32; i++)
            {
                hashKey[i - byteOffset] = bytes[i];
            }
            byteOffset += 32;
            for (uint i = byteOffset; i < byteOffset + 8; i++)
            {
                utxoP[i - byteOffset] = bytes[i];
            }
            byteOffset += 8;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                reward[i - byteOffset] = bytes[i];
            }
            return new MinerToken(hashKey, BitConverter.ToUInt64(utxoP, 0), BitConverter.ToUInt32(reward, 0));
        }
        public static byte[] MinerTokenToBytes(MinerToken mt)
        {
            List<byte> Databuilder = new List<byte>();
            Databuilder = AddBytesToList(Databuilder, mt.MinerPKEY);
            Databuilder = AddBytesToList(Databuilder, BitConverter.GetBytes(mt.mUPOIndex));
            Databuilder = AddBytesToList(Databuilder, BitConverter.GetBytes(mt.MiningReward));
            return ListToByteArray(Databuilder);
        }
        public static Block BytesToBlock(byte[] bytes) // CAN RESULT NULL
        {
            if (bytes.Length < 156) { return null; } // 156 octets is minimum size for a block. 

            byte[] index = new byte[4];
            byte[] hash = new byte[32];
            byte[] phash = new byte[32];
            byte[] ds = new byte[4];
            List<PixelWrapper.PixTX> dt = new List<PixelWrapper.PixTX>();
            byte[] ts = new byte[4];
            MinerToken minertoken;
            byte[] ht = new byte[32];
            byte[] nonce = new byte[4];


            uint byteOffset = 0;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                index[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 32; i++)
            {
                hash[i - byteOffset] = bytes[i];
            }
            byteOffset += 32;
            for (uint i = byteOffset; i < byteOffset + 32; i++)
            {
                phash[i - byteOffset] = bytes[i];
            }
            byteOffset += 32;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                ds[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            
            for (uint i = 0; i < BitConverter.ToUInt32(ds, 0); i++)
            {
                byteOffset += 544; 
                if (bytes.Length < byteOffset + 4) { return null; }
                byte[] esbytes = new byte[4];
                for (uint n = byteOffset; n < byteOffset + 4; n++)
                {
                    esbytes[n - byteOffset] = bytes[n]; 
                }
                uint ExtractionSize = BitConverter.ToUInt32(esbytes, 0);
                byteOffset -= 544;
                byte[] extractiondata = new byte[548 + (ExtractionSize * 72) + 520];
                if (bytes.Length < byteOffset + extractiondata.Length ) { return null; }
                for (uint n = byteOffset; n < byteOffset + 548 + (ExtractionSize * 72) + 520; n++)
                {
                    extractiondata[n - byteOffset] = bytes[n]; 
                }
                dt.Add(PixelWrapper.BytesToPixTX(extractiondata));
                byteOffset += 548 + (ExtractionSize * 72) + 520;
             
            }

            if (bytes.Length < byteOffset + 84) { return null; }
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                ts[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            byte[] mbytes = new byte[44];
            for (uint i = byteOffset; i < byteOffset + 44; i++)
            {
                mbytes[i - byteOffset] = bytes[i];
            }
            byteOffset += 44;
            minertoken = BytesToMinerToken(mbytes);
            for (uint i = byteOffset; i < byteOffset + 32; i++)
            {
                ht[i - byteOffset] = bytes[i];
            }

            byteOffset += 32;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                nonce[i - byteOffset] = bytes[i];
            }

            return new Block(BitConverter.ToUInt32(index, 0), hash, phash, dt, BitConverter.ToUInt32(ts, 0), minertoken, ht, BitConverter.ToUInt32(nonce, 0));

        }
        public static byte[] BlockToBytes(Block b)
        {

            List<byte> DataBuilder = new List<byte>();
            DataBuilder = AddBytesToList(DataBuilder, BitConverter.GetBytes(b.Index));
            DataBuilder = AddBytesToList(DataBuilder, b.Hash);
            DataBuilder = AddBytesToList(DataBuilder, b.previousHash);
            DataBuilder = AddBytesToList(DataBuilder, BitConverter.GetBytes(b.DataSize));
            foreach (PixelWrapper.PixTX trans in b.Data) { DataBuilder = AddBytesToList(DataBuilder, PixelWrapper.PixTxToBytes(trans)); }
            DataBuilder = AddBytesToList(DataBuilder, BitConverter.GetBytes(b.TimeStamp));
            DataBuilder = AddBytesToList(DataBuilder, MinerTokenToBytes(b.minerToken));
            DataBuilder = AddBytesToList(DataBuilder, b.HashTarget);
            DataBuilder = AddBytesToList(DataBuilder, BitConverter.GetBytes(b.Nonce));

            return ListToByteArray(DataBuilder);
        }


        //------------------------ SHA256 ---------------------

        public static byte[] ComputeSHA256(byte[] msg)
        {
            SHA256 sha = SHA256.Create();
            byte[] result = sha.ComputeHash(msg);
            return result;
        }
        public static string SHAToHex(byte[] bytes, bool upperCase)
        {
            StringBuilder result = new StringBuilder(bytes.Length * 2);

            for (int i = 0; i < bytes.Length; i++)
                result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));

            return result.ToString();
        }
        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
        //------------------------ UTXO SET  ---------------------
        
  
        public static void ProccessTempBlocks(string _filePath, bool needPropagate) // MAIN FUNCTION TO VALID BLOCK
        {
            Console.WriteLine("STARTING PROCESSING BLOCKS FOR " + _filePath);
            FileInfo f = new FileInfo(_filePath);
            if (f.Length < 8) { File.Delete(_filePath); return; }
            if (!isHeaderCorrectInBlockFile(_filePath)) { Console.WriteLine("header incorrect!"); File.Delete(_filePath); return; }

            uint firstTempIndex = BitConverter.ToUInt32(GetBytesFromFile(4, 8, _filePath), 0);
            uint latestTempIndex = BitConverter.ToUInt32(GetBytesFromFile(0, 4, _filePath), 0);
            Console.WriteLine(firstTempIndex + " " + latestTempIndex);
            uint latestOfficialIndex = RequestLatestBlockIndex(true);

            bool HardFork = false;
            Console.WriteLine((int)(latestOfficialIndex - MAX_RETROGRADE));
            if ((int)firstTempIndex <= (int)(latestOfficialIndex - MAX_RETROGRADE)) // create a uint max value error ! 
            {
                if (firstTempIndex == 0) { Console.WriteLine("NO GENESIS ALLOWED !"); File.Delete(_filePath); return; }
                if (latestTempIndex < latestOfficialIndex + WINNING_RUN_DISTANCE) { Console.WriteLine("NOT WINNING DIST"); File.Delete(_filePath); return; }
                HardFork = true;
            }
            else
            {
                uint latestIndex = RequestLatestBlockIndex(false);
                if (firstTempIndex > latestIndex + 1) { File.Delete(_filePath); return; }
                // we check if we have a fork that contains specific index to mesure
                if (latestTempIndex < latestOfficialIndex + 1) { File.Delete(_filePath); return; }
            }
            if (HardFork)
            {
                Console.WriteLine("hard forking");
                Block currentBlockReading = GetBlockAtIndexInFile(firstTempIndex, _filePath);
                Block previousBlock = GetBlockAtIndex(firstTempIndex - 1);
                if (currentBlockReading == null || previousBlock == null) { File.Delete(_filePath); return; }
                List<uint> timestamps = new List<uint>();
                uint TC = 0;
                for (uint i = firstTempIndex - 1; i >= 0; i--)
                {
                    if (i == uint.MaxValue) { break; }
                    Block b = GetBlockAtIndex(i);
                    if (b == null) { Console.WriteLine("[missing block]"); File.Delete(_filePath); return; }
                    timestamps.Add(b.TimeStamp);
                    TC++;
                    if (TC >= TIMESTAMP_TARGET)
                    {
                        break;
                    }
                }
                uint MINTIMESTAMP = GetTimeStampRequirementB(timestamps);

                byte[] tempTarget = new byte[32];
                if (isNewTargetRequired(firstTempIndex))
                {
                    // will just use gethashtarget with ComputeHashTargetB
                    Block earlierBlock;
                    if (previousBlock.Index + 1 <= TARGET_CLOCK)
                    {
                        earlierBlock = GetBlockAtIndex(0); // get genesis
                        if (earlierBlock == null) { Console.WriteLine("[missing block]"); File.Delete(_filePath); return; }
                    }
                    else
                    {
                        earlierBlock = GetBlockAtIndex(previousBlock.Index + 1 - TARGET_CLOCK);//  need also an update
                        if (earlierBlock == null) { Console.WriteLine("[missing block]"); File.Delete(_filePath); return; }

                    }
                    tempTarget = ComputeHashTargetB(previousBlock, earlierBlock);
                }
                else
                {
                    tempTarget = previousBlock.HashTarget;
                }

                List<PixelWrapper.UPO> vUPO = PixelWrapper.GetVirtualUPOCacheFromFile(_filePath, firstTempIndex - 1, firstTempIndex - 1);
               

                while (true)
                {
                    Tuple<bool, List<PixelWrapper.UPO>> bV = IsBlockValid(currentBlockReading, previousBlock, MINTIMESTAMP, tempTarget, vUPO);

                    if (!bV.Item1)
                    {
                        File.Delete(_filePath);
                        return;
                    }
                    vUPO = bV.Item2;

                    if (currentBlockReading.Index == latestTempIndex)
                    {
                        PixelWrapper.DownGradeUPOset(firstTempIndex - 1);
                        DowngradeOfficialChain(firstTempIndex - 1);
                        AddBlocksToOfficialChain(_filePath, needPropagate);
                        return;
                    }
                    previousBlock = currentBlockReading; //*
                    currentBlockReading = GetBlockAtIndexInFile(currentBlockReading.Index + 1, _filePath); //* 
                    if (currentBlockReading == null) { File.Delete(_filePath); return; }

                    timestamps.RemoveAt(0);
                    timestamps.Add(previousBlock.TimeStamp);
                    MINTIMESTAMP = GetTimeStampRequirementB(timestamps);

                    if (isNewTargetRequired(currentBlockReading.Index))
                    {
                        Block earlierBlock;
                        if (previousBlock.Index + 1 <= TARGET_CLOCK)
                        {
                            earlierBlock = GetBlockAtIndex(0);
                            if (earlierBlock == null) { Console.WriteLine("[missing block]"); File.Delete(_filePath); return; }
                        }
                        else
                        {
                            if (previousBlock.Index + 1 - TARGET_CLOCK > latestOfficialIndex)
                            {
                                earlierBlock = GetBlockAtIndexInFile(previousBlock.Index + 1 - TARGET_CLOCK, _filePath);
                                if (earlierBlock == null) { Console.WriteLine("[missing block]"); File.Delete(_filePath); return; }
                            }
                            else
                            {
                                earlierBlock = GetBlockAtIndex(previousBlock.Index + 1 - TARGET_CLOCK);
                                if (earlierBlock == null) { Console.WriteLine("[missing block]"); File.Delete(_filePath); return; }
                            }
                        }
                        tempTarget = ComputeHashTargetB(previousBlock, earlierBlock);
                    }
                    else
                    {
                        tempTarget = previousBlock.HashTarget;
                    }

                }
            }
            else
            {
                // [1] we need to check if first temp index is lower than our highest forks index forks OR if first temp index is equal to latestOfficialIndex + 1
                // [2] we need to find if 
                Block currentBlockReading = GetBlockAtIndexInFile(firstTempIndex, _filePath); // we get like latesttempindex -> 2 
                if (currentBlockReading == null) { File.Delete(_filePath); Console.WriteLine("wrong index specified 1"); return; }

                string[] forkfiles = Directory.GetFiles(_folderPath + "fork");
                uint latestIndex = RequestLatestBlockIndex(false);
                // 
                string _pathToGetPreviousBlock = "";
                if (firstTempIndex != latestOfficialIndex + 1) // searching A fork : if firsttempindex(1) is not latestofficialindex  (0) + 1
                {

                    Console.WriteLine("called");
                    latestIndex = RequestLatestBlockIndex(false);
                    if (latestIndex > firstTempIndex - 1)
                    {
                        _pathToGetPreviousBlock = GetIndexBlockChainFilePath(firstTempIndex - 1);
                        if (_pathToGetPreviousBlock == "")
                        {
                            File.Delete(_filePath); Console.WriteLine("[1]Can't find a fork to process those blocks. temp index : " + firstTempIndex); return;
                        }
                    }
                    else
                    {
                        // check if we can find a fork with this specific block file ... goes here when lightfork ... 
                        string forkpath = FindMatchingFork(currentBlockReading);
                        if (forkpath.Length == 0) { Console.WriteLine("[2]Can't find a fork to process those blocks. temp index : " + firstTempIndex); File.Delete(_filePath); return; }
                        else { _pathToGetPreviousBlock = forkpath; }
                        Block bb = GetBlockAtIndexInFile(latestTempIndex, _filePath);
                        if (bb == null) { Console.WriteLine("[missing block]"); File.Delete(_filePath); return; }
                        if (isForkAlreadyExisting(bb)) { Console.WriteLine("File Already Existing"); File.Delete(_filePath); return; }
                    }
                }
                else
                {
                    //_pathToGetPreviousBlock = GetLatestBlockChainFilePath(); // this is shit ... 
                    _pathToGetPreviousBlock = GetIndexBlockChainFilePath(firstTempIndex - 1);
                }
                Console.WriteLine(_pathToGetPreviousBlock);
                Block previousBlock = GetBlockAtIndexInFile(firstTempIndex - 1, _pathToGetPreviousBlock);

                if (previousBlock == null) { File.Delete(_filePath); Console.WriteLine("wrong index specified 2"); return; }

                List<uint> timestamps = new List<uint>();
                uint TC = 0;
                for (uint i = firstTempIndex - 1; i >= 0; i--)
                {
                    if (i == uint.MaxValue) { break; }
                    if (i > latestOfficialIndex)
                    {
                        Block b = GetBlockAtIndexInFile(i, _pathToGetPreviousBlock);
                        if (b == null) { Console.WriteLine("[missing block]"); File.Delete(_filePath); return; }
                        timestamps.Add(b.TimeStamp);
                    }
                    else
                    {
                        Block b = GetBlockAtIndex(i);
                        if (b == null) { Console.WriteLine("[missing block]"); File.Delete(_filePath); return; }
                        timestamps.Add(GetBlockAtIndex(i).TimeStamp);
                    }
                    TC++;
                    if (TC >= TIMESTAMP_TARGET)
                    {
                        break;
                    }
                }
                uint MINTIMESTAMP = GetTimeStampRequirementB(timestamps);
                Console.WriteLine(MINTIMESTAMP);
                byte[] tempTarget = new byte[32];
                if (isNewTargetRequired(firstTempIndex))
                {
                    // will just use gethashtarget with ComputeHashTargetB
                    Block earlierBlock;
                    if (latestOfficialIndex < previousBlock.Index + 1 - TARGET_CLOCK)
                    {
                        uint lastindexfile = RequestLatestBlockIndexInFile(_pathToGetPreviousBlock);
                        if (lastindexfile < previousBlock.Index + 1 - TARGET_CLOCK)
                        {
                            earlierBlock = GetBlockAtIndexInFile(previousBlock.Index + 1 - TARGET_CLOCK, _filePath);
                            if (earlierBlock == null) { Console.WriteLine("[missing block]"); File.Delete(_filePath); return; }
                        }
                        else
                        {
                            earlierBlock = GetBlockAtIndexInFile(previousBlock.Index + 1 - TARGET_CLOCK, _pathToGetPreviousBlock); // it means that we have an index shit...
                            if (earlierBlock == null) { Console.WriteLine("[missing block]"); File.Delete(_filePath); return; }
                        }

                    }
                    else
                    {
                        earlierBlock = GetBlockAtIndex(previousBlock.Index + 1 - TARGET_CLOCK);
                        if (earlierBlock == null) { Console.WriteLine("[missing block]"); File.Delete(_filePath); return; }

                    }



                    tempTarget = ComputeHashTargetB(previousBlock, earlierBlock);
                    Console.WriteLine(previousBlock.Index + 1);


                }
                else
                {
                    tempTarget = previousBlock.HashTarget;
                }

                // we have to build here an upo cache. it can be empty if firsttempindex = lastofficialindex+1 
                

                List<PixelWrapper.UPO> vUPO = new List<PixelWrapper.UPO>();
                if ( currentBlockReading.Index != latestOfficialIndex +1)
                {
                    vUPO = PixelWrapper.GetVirtualUPOCacheFromFile(_pathToGetPreviousBlock, firstTempIndex - 1, previousBlock.Index);
                }
                while (true)
                {
                    Tuple<bool, List<PixelWrapper.UPO>> bV = IsBlockValid(currentBlockReading, previousBlock, MINTIMESTAMP, tempTarget, vUPO);

                    if (!bV.Item1)
                    {
                        File.Delete(_filePath);
                        return;
                    }
                    vUPO = bV.Item2; // vutxo are update! 
                    if (currentBlockReading.Index == latestTempIndex)
                    {

                        if (_pathToGetPreviousBlock == GetLatestBlockChainFilePath())
                        {
                            string newPath = GetNewForkFilePath();
                            File.Move(_filePath, newPath);
                            Console.WriteLine("new fork added");
                            PixelWrapper.UpdatePendingTXFileB(newPath);
                            VerifyRunState(needPropagate);
                            // we should verify if newpath exist. if it is existing we broadcast it
                            if (File.Exists(newPath) && needPropagate)
                            {
                                BroadcastQueue.Add(new BroadcastInfo(1, 1, newPath));
                            }
                            return;
                        }
                        else
                        {
                            // we will need to concatenate those two forks... to write a new one ... 
                            string newForkPath = ConcatenateForks(_pathToGetPreviousBlock, _filePath, firstTempIndex);
                            VerifyRunState(needPropagate);
                            // we should verify if newpath exist. if it is existing we broadcast it
                            if (File.Exists(newForkPath) && needPropagate)
                            {
                                BroadcastQueue.Add(new BroadcastInfo(1, 1, newForkPath));
                            }
                            Console.WriteLine("fork has been append.");
                            return;
                        }

                    }
                    previousBlock = currentBlockReading; //*
                    currentBlockReading = GetBlockAtIndexInFile(currentBlockReading.Index + 1, _filePath); //*
                    if (currentBlockReading == null) { File.Delete(_filePath); Console.WriteLine("wrong index specified 3"); return; }

                    timestamps.RemoveAt(0);
                    timestamps.Add(previousBlock.TimeStamp);
                    MINTIMESTAMP = GetTimeStampRequirementB(timestamps);

                    if (isNewTargetRequired(currentBlockReading.Index))
                    {
                        // will just use gethashtarget with ComputeHashTargetB
                        Block earlierBlock;
                        if (latestOfficialIndex < previousBlock.Index + 1 - TARGET_CLOCK)
                        {
                            uint lastindexfile = RequestLatestBlockIndexInFile(_pathToGetPreviousBlock);
                            if (lastindexfile < previousBlock.Index + 1 - TARGET_CLOCK)
                            {
                                earlierBlock = GetBlockAtIndexInFile(previousBlock.Index + 1 - TARGET_CLOCK, _filePath);
                                if (earlierBlock == null) { Console.WriteLine("[missing block]"); File.Delete(_filePath); return; }
                            }
                            else
                            {
                                earlierBlock = GetBlockAtIndexInFile(previousBlock.Index + 1 - TARGET_CLOCK, _pathToGetPreviousBlock); // it means that we have an index shit...
                                if (earlierBlock == null) { Console.WriteLine("[missing block]"); File.Delete(_filePath); return; }
                            }

                        }
                        else
                        {
                            earlierBlock = GetBlockAtIndex(previousBlock.Index + 1 - TARGET_CLOCK);
                            if (earlierBlock == null) { Console.WriteLine("[missing block]"); File.Delete(_filePath); return; }

                        }
                        tempTarget = ComputeHashTargetB(previousBlock, earlierBlock);
                    }
                    else
                    {
                        tempTarget = previousBlock.HashTarget;
                    }
                }

            }
            
        }

        public static string FindMatchingFork(Block b)
        {
            string[] forkfiles = Directory.GetFiles(_folderPath + "fork");
            foreach (string s in forkfiles)
            {

                uint latestIndex = RequestLatestBlockIndexInFile(s);
                if (latestIndex >= b.Index - 1)
                {
                    Block forkblock = GetBlockAtIndexInFile(b.Index - 1, s);
                    if (forkblock == null) { return ""; }
                    if (forkblock.Hash.SequenceEqual(b.previousHash))
                    {
                        return s;
                    }
                }
            }
            return "";
        }
        public static bool isForkAlreadyExisting(Block b)
        {
            string[] forkfiles = Directory.GetFiles(_folderPath + "fork");
            foreach (string s in forkfiles)
            {
                uint latestIndex = RequestLatestBlockIndexInFile(s);

                if (latestIndex >= b.Index)
                {
                    Block forkblock = GetBlockAtIndexInFile(b.Index, s);
                    if (forkblock == null) { return false; }
                    if (forkblock.Hash.SequenceEqual(b.Hash))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        public static void VerifyRunState(bool needPropagate) // < this will checking fork win then Upgrade the chain! 
        {
            Console.WriteLine("winning dist" + WINNING_RUN_DISTANCE);
            string[] forkfiles = Directory.GetFiles(_folderPath + "fork");
            bool _found = false;
            foreach (string s in forkfiles)
            {
                uint latestIndex = RequestLatestBlockIndexInFile(s);
                uint latestOfficialIndex = RequestLatestBlockIndex(true);
                if (latestIndex >= latestOfficialIndex + WINNING_RUN_DISTANCE)
                {
                    uint firstTempIndex = BitConverter.ToUInt32(GetBytesFromFile(4, 8, s), 0);
                    if (firstTempIndex < latestOfficialIndex)
                    {
                        DowngradeOfficialChain(firstTempIndex - 1);
                        PixelWrapper.DownGradeUPOset(firstTempIndex - 1);
                    }
                    AddBlocksToOfficialChain(s, needPropagate);
                    _found = true;
                    // clear all forks
                    // Console.WriteLine("called a");
                    break;
                }
            }
            if (_found)
            {
                //Console.WriteLine("called b");
                foreach (string s in forkfiles)
                {
                    File.Delete(s);
                }
            }


        }
        public static uint GetTimeStampRequirementA() //< will return current timestamp needed for next block 
        {
            // from the 11 previous block 
            uint lastIndex = RequestLatestBlockIndex(true);
            List<uint> timestamp = new List<uint>();
            uint tcounter = 0;
            for (uint i = lastIndex; i >= 0; i--)
            {
                if (i == uint.MaxValue) { break; }
                Block b = GetBlockAtIndex(i);
                if (b == null) { return uint.MaxValue; } //< return a max value if an error occured! 
                timestamp.Add(b.TimeStamp);
                tcounter++;
                if (tcounter == TIMESTAMP_TARGET) { break; }
            }
            uint sum = 0;
            foreach (uint i in timestamp) { sum += i; }
            sum /= (uint)timestamp.Count;
            return sum;
        }
        public static uint GetTimeStampRequirementB(List<uint> timestamp) //< will return current timestamp needed  
        {

            uint sum = 0;
            foreach (uint i in timestamp) { sum += i; }
            sum /= (uint)timestamp.Count;
            return sum;

        }
        public static byte[] UpdateHashTarget() //<--- Update the actual Hash Target Required. 
        {
            uint index = RequestLatestBlockIndex(true);
            if (isNewTargetRequired(index))
            {
                CURRENT_HASH_TARGET = ComputeHashTargetA();
            }
            else
            {
                Block b = GetBlockAtIndex(index);
                if (b == null) { return CURRENT_HASH_TARGET; }
                CURRENT_HASH_TARGET = b.HashTarget;
            }
            return CURRENT_HASH_TARGET;
        }
        public static byte[] ComputeHashTargetA() //< compute Next Hash Target from latest index
        {
            uint index = RequestLatestBlockIndex(true);
            Block pb = GetBlockAtIndex(index - TARGET_CLOCK);
            if (pb == null) { return CURRENT_HASH_TARGET; }
            uint TimeStampA = pb.TimeStamp;

            Block b = GetBlockAtIndex(index);
            if (b == null) { return CURRENT_HASH_TARGET; }
            uint TimeStampB = b.TimeStamp;
            uint TimeSpent = TimeStampB - TimeStampA;

            // for 10 mn mining it is : 
            // needed = 1209600
            // quarter + : 4838400
            // quarter - : 302400

            uint QPLUS = TARGET_TIME * TARGET_DIVIDER_BOUNDARIES;
            uint QMINUS = TARGET_TIME / TARGET_DIVIDER_BOUNDARIES;

            if (TimeSpent > QPLUS) { TimeSpent = QPLUS; }
            if (TimeSpent < QMINUS) { TimeSpent = QMINUS; }
            BigInteger b1 = BytesToUint256(b.HashTarget);

            BigInteger b2 = new BigInteger(TimeSpent);
            b1 = BigInteger.Multiply(b1, b2);
            BigInteger divider = new BigInteger(TARGET_TIME);
            b1 = BigInteger.Divide(b1, divider);

            BigInteger maxtarget = BytesToUint256(MAXIMUM_TARGET);
            if (b1.CompareTo(maxtarget) == 1)
            {
                b1 = BytesToUint256(MAXIMUM_TARGET);
            }
            Console.WriteLine("new target decimal is : " + b1.ToString());
            return Uint256ToByteArray(b1);

        }
        public static byte[] ComputeHashTargetB(Block latest, Block previous) //< compute Hash Target with specific block 
        {
            // Get Time Spent from previous to latest. previous is always index - 1 .... 
            uint index = RequestLatestBlockIndex(true);
            uint TimeStampA = previous.TimeStamp;
            uint TimeStampB = latest.TimeStamp;
            uint TimeSpent = TimeStampB - TimeStampA;
            // <<<< timespent B - timespent A
            // for 10 mn mining it is : 
            // needed = 1209600
            // quarter + : 4838400
            // quarter - : 302400

            // for 5s mining it is : 
            // needed : 10080
            // quarter + : 40320
            // quarter - : 2520
            //
            uint QPLUS = TARGET_TIME * TARGET_DIVIDER_BOUNDARIES;
            uint QMINUS = TARGET_TIME / TARGET_DIVIDER_BOUNDARIES;

            if (TimeSpent > QPLUS) { TimeSpent = QPLUS; }
            if (TimeSpent < QMINUS) { TimeSpent = QMINUS; }
            BigInteger b1 = BytesToUint256(latest.HashTarget);

            BigInteger b2 = new BigInteger(TimeSpent);
            b1 = BigInteger.Multiply(b1, b2);
            BigInteger divider = new BigInteger(TARGET_TIME);
            b1 = BigInteger.Divide(b1, divider);

            BigInteger maxtarget = BytesToUint256(MAXIMUM_TARGET);
            if (b1.CompareTo(maxtarget) == 1)
            {
                b1 = BytesToUint256(MAXIMUM_TARGET);
            }
            Console.WriteLine("new target decimal is : " + b1.ToString());
            return Uint256ToByteArray(b1);
        }
        public static bool isNewTargetRequired(uint index)
        {
            if (index < TARGET_CLOCK)
            {
                return false;
            }

            if (index % TARGET_CLOCK == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        public static string ConcatenateForks(string _path1, string _path2, uint endIndex)
        {
            // will get block of _path1 until endindex(not include) , then procceed to write full block of path2
            string newForkPath = GetNewForkFilePath();
            File.WriteAllBytes(newForkPath, new byte[4]);
            uint startIndex = RequestLatestBlockIndex(true);
            uint LastIndex = RequestLatestBlockIndexInFile(_path2);
            for (uint i = startIndex + 1; i < endIndex; i++)
            {
                Block b = GetBlockAtIndexInFile(i, _path1);
                if (b == null) { File.Delete(_path2); return ""; } // FATAL ERROR
                byte[] bytes = BlockToBytes(b);
                AppendBytesToFile(newForkPath, bytes);
            }
            for (uint i = endIndex; i < LastIndex + 1; i++)
            {
                Block b = GetBlockAtIndexInFile(i, _path2);
                if (b == null) { File.Delete(_path2); FatalErrorHandler(0, "no block found in data during concatening forks file"); return ""; } // FATAL ERROR
                byte[] bytes = BlockToBytes(b);
                AppendBytesToFile(newForkPath, bytes);
                PixelWrapper.UpdatePendingTXFile(b);
            }
            OverWriteBytesInFile(0, newForkPath, BitConverter.GetBytes(LastIndex));
            Console.WriteLine("will delete " + _path2);
            File.Delete(_path2);
            return newForkPath;
        }
        public static void DowngradeOfficialChain(uint pointer) //< downgrade blockchain to specific length ( block#pointer not included!)
        {
            uint latestIndex = RequestLatestBlockIndex(true);
            int bytelength = 0;
            for (uint i = pointer + 1; i < latestIndex + 1; i++)
            {
                Block b = GetBlockAtIndex(i);
                if (b == null) { FatalErrorHandler(0, "no block found in data during downgrading chain"); return; } // FATAL ERROR
                bytelength += BlockToBytes(b).Length;
            }
            // we know the exact length of bytes we have to truncate.... we can use this value to erase blockchain part
            while (true)
            {
                string filePath = GetLatestBlockChainFilePath();
                FileInfo f = new FileInfo(filePath);
                if (bytelength > f.Length)
                {
                    File.Delete(filePath);
                    bytelength -= (int)f.Length;
                    bytelength += 4; // padding header
                }
                else
                {
                    TruncateFile(filePath, (uint)bytelength);
                    break;
                }
            }

            OverWriteBytesInFile(0, GetLatestBlockChainFilePath(), BitConverter.GetBytes(pointer));
            Console.WriteLine("Blockchain Downgraded at " + pointer);
        }
        public static void AddBlocksToOfficialChain(string filePath, bool needPropagate)
        {
            // GET THE LATEST BLOCKCHAIN FILE PATH.->
            string blockchainPath = GetLatestBlockChainFilePath();

            uint firstTempIndex = BitConverter.ToUInt32(GetBytesFromFile(4, 8, filePath), 0);
            uint latestTempIndex = RequestLatestBlockIndexInFile(filePath);
            //Console.WriteLine(latestTempIndex);
            for (uint i = firstTempIndex; i < latestTempIndex + 1; i++)
            {
                Block b = GetBlockAtIndexInFile(i, filePath);
                if (b == null) { FatalErrorHandler(0, "no block found in data during updating official chain"); return; } // FATAL ERROR
                PrintBlockData(b);
                byte[] bytes = BlockToBytes(b);
                FileInfo f = new FileInfo(blockchainPath);
                if (f.Length + bytes.Length > BLOCKCHAIN_FILE_CHUNK)
                {
                    string name = GetNewBlockChainFilePath();
                    File.WriteAllBytes(name, BitConverter.GetBytes(b.Index));
                    AppendBytesToFile(name.ToString(), bytes);
                }
                else
                {
                    OverWriteBytesInFile(0, blockchainPath, BitConverter.GetBytes(b.Index));
                    AppendBytesToFile(blockchainPath, bytes);
                }
                PixelWrapper.UpgradeUPOSet(b);
                PixelWrapper.UpdatePendingTXFile(b);
            }
            if (needPropagate)
            {
                BroadcastQueue.Add(new BroadcastInfo(2, 1));
            }
            string[] forkfiles = Directory.GetFiles(_folderPath + "fork");
            foreach (string s in forkfiles)
            {
                File.Delete(s);
            }
            Console.WriteLine("will delete " + filePath);
            File.Delete(filePath);
            Console.WriteLine("Blockchain updated!");

        }
        public static void BuildUTXOSet()
        {
            string fPath = _folderPath + "utxos";
            File.WriteAllBytes(_folderPath + "utxos", new byte[4]); // a revoir ... 
            uint lastIndex = RequestLatestBlockIndex(true);
            for (uint i = 1; i < lastIndex + 1; i++)
            {
                Block b = GetBlockAtIndex(i);
                if (b == null) { FatalErrorHandler(0); return; } // FATAL ERROR
                PixelWrapper.UpgradeUPOSet(b);
            }
        }
        public static bool isBlockChainValid() //< THIS WILL COMPLETELY REBUILD AN UTXO SET. 
        {
            Console.WriteLine("method work in progress actually. please wait update of this program");
            return false;
            uint lastIndex = RequestLatestBlockIndex(true);
            Block gen = GetBlockAtIndex(0);
            if (gen == null) { return false; }
            byte[] HashTarget = gen.HashTarget;

            for (uint i = 1; i < lastIndex + 1; i++)
            {
                Block b = GetBlockAtIndex(i);
                Block prevb = GetBlockAtIndex(i - 1);
                if (b == null || prevb == null) { return false; }
                if (isNewTargetRequired(i))
                {
                    Block pb = GetBlockAtIndex(b.Index - TARGET_CLOCK);
                    if (pb == null) { return false; }
                    HashTarget = ComputeHashTargetB(prevb, pb);
                }
                else
                {
                    HashTarget = prevb.HashTarget;
                }
                // if (!IsBlockValid(b, prevb, 0, HashTarget)){ 
                //  return false;
                // }
            }
            return true;
        }
        public static Tuple<bool, List<PixelWrapper.UPO>> IsBlockValid(Block b, Block prevb, uint MIN_TIME_STAMP, byte[] HASHTARGET, List<PixelWrapper.UPO> vUPO)
        {
            uint latestIndex = RequestLatestBlockIndex(true);
            if (!b.previousHash.SequenceEqual(prevb.Hash)) { Console.WriteLine("wrong previous hash"); return new Tuple<bool, List<PixelWrapper.UPO>>(false, vUPO); }
            uint unixTimestamp = (uint)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            if (b.TimeStamp < MIN_TIME_STAMP || b.TimeStamp > unixTimestamp) { Console.WriteLine("wrong time stamp"); return new Tuple<bool, List<PixelWrapper.UPO>>(false, vUPO); }
            if (!b.HashTarget.SequenceEqual(HASHTARGET)) { Console.WriteLine("wrong HASH TARGET"); return new Tuple<bool, List<PixelWrapper.UPO>>(false, vUPO); }
            uint sumFEE = 0;
            uint ptxCount = 0;



            if (b.minerToken.mUPOIndex != 0)
            {
                PixelWrapper.UPO mUTXO = PixelWrapper.GetOfficialUPO(b.minerToken.mUPOIndex, b.minerToken.MinerPKEY);
                if (mUTXO == null) { Console.WriteLine("wrong UTXO pointer"); return new Tuple<bool, List<PixelWrapper.UPO>>(false, vUPO); }
                else { if (!mUTXO.HashKey.SequenceEqual(b.minerToken.MinerPKEY)) { Console.WriteLine("wrong UTXO pointer"); return new Tuple<bool, List<PixelWrapper.UPO>>(false, vUPO); } }
            }
            if (b.minerToken.MiningReward > GetMiningReward(b.Index) + sumFEE) { Console.WriteLine("wrong mining reward"); return new Tuple<bool, List<PixelWrapper.UPO>>(false, vUPO); }

            vUPO = PixelWrapper.UpdateUPOCacheListWithFullBlock(vUPO, b);
            Console.WriteLine("upo cache length : " + vUPO.Count);
            vUPO = PixelWrapper.UpdateUPOCacheListWithMinerToken(vUPO, b.minerToken);

            foreach (PixelWrapper.PixTX ptx in b.Data)
            {
                if (!PixelWrapper.isPixTxValid(ptx, vUPO))
                {
                    Console.WriteLine("Pixel Transaction not valid"); return new Tuple<bool, List<PixelWrapper.UPO>>(false, vUPO);
                }
                vUPO = PixelWrapper.UpdateUPOCacheWithPixelTX(vUPO, ptx, b.Index, ptxCount); // apply tx to the cache
                
                sumFEE += ptx.TxFee;
                // this  will update all UPO or add UPO in cache specifically from this transaction
                ptxCount++;
            }
            // update minertoken
            
            // now verify Merkle Root Correctness. 
            List<byte> dataBuilder = new List<byte>();
            dataBuilder = AddBytesToList(dataBuilder, BitConverter.GetBytes(b.Index));
            dataBuilder = AddBytesToList(dataBuilder, b.previousHash);
            dataBuilder = AddBytesToList(dataBuilder, BitConverter.GetBytes(b.DataSize));
            foreach (PixelWrapper.PixTX ptx in b.Data) { dataBuilder = AddBytesToList(dataBuilder, PixelWrapper.PixTxToBytes(ptx)); }
            dataBuilder = AddBytesToList(dataBuilder, BitConverter.GetBytes(b.TimeStamp));
            dataBuilder = AddBytesToList(dataBuilder, MinerTokenToBytes(b.minerToken));
            dataBuilder = AddBytesToList(dataBuilder, b.HashTarget);
            byte[] sha = ComputeSHA256(ListToByteArray(dataBuilder));
            sha = ComputeSHA256(sha); //< double hash function to avoid collision or anniversary attack 
            if (!sha.SequenceEqual(b.Hash)) { Console.WriteLine("wrong merkle root"); return new Tuple<bool, List<PixelWrapper.UPO>>(false, vUPO); }
            // now checking nonce
            dataBuilder = new List<byte>();
            dataBuilder = AddBytesToList(dataBuilder, BitConverter.GetBytes(b.Nonce));
            dataBuilder = AddBytesToList(dataBuilder, b.Hash);
            byte[] hash = ComputeSHA256(ListToByteArray(dataBuilder));
            if (!isNonceGolden(hash, b.HashTarget)) { Console.WriteLine("wrong nonce"); return new Tuple<bool, List<PixelWrapper.UPO>>(false, vUPO); }

            return new Tuple<bool, List<PixelWrapper.UPO>>(true, vUPO);
        }
        public static uint GetMiningReward(uint index) //< Get Current Mining Reward from Index. 
        {
            // The Reward given to the first block miner is 50, the volume is halved every 210,000 blocks(about 4 years)
            uint Reward = NATIVE_REWARD;
            while (index >= REWARD_DIVIDER_CLOCK)
            {
                index -= REWARD_DIVIDER_CLOCK;
                Reward /= 2;
            }
            return Reward;

        }
        public static uint GetCurrencyVolume(uint index)
        {
            uint sum = 0;
            for (uint i = 1; i < index + 1; i++) //< genesis dont produce currency. so we start at 1. 
            {
                sum += GetMiningReward(i);
            }
            return sum;
        }
        
       
        public static void StartMining(byte[] pKey, ulong mUTXOP, uint NTIMES)
        {
            
            // THIS IS A BASIC MINING STRATEGY (NOT OPTIMIZED) -> 
            /*
             * WE REFUSE FORK THAT HAVE TX LOCKTIME PURISHING IN 5000 (s) about 1hour and half.
             * WE GET THE LONGEST FORK. IF NOT. JUST MINE FROM OFFICIAL... 
             * WE ALSO REFUSE TX THAT HAVE LOCKTIME PURISHING IN 5000 in ptx file.
             */
            uint unixTimestamp = (uint)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            Block prev = GetBlockAtIndex(RequestLatestBlockIndex(true));
            if (prev == null) {   return; }
            
            string[] forkfiles = Directory.GetFiles(_folderPath + "fork");
            uint bestIndex = RequestLatestBlockIndex(true);
            Console.WriteLine(bestIndex);
            string longestForkPath = "";
            if ( bestIndex != 0)
            {
                longestForkPath = GetLatestBlockChainFilePath();
            }
            else
            {
                longestForkPath = _folderPath + "blockchain\\0";
            }
            Console.WriteLine(longestForkPath);
            foreach (string s in forkfiles)
            {
               uint latestIndex = RequestLatestBlockIndexInFile(s);
               if (latestIndex > bestIndex)
               {
                  bestIndex = latestIndex;
                  longestForkPath = s;
               }
            }
            prev = GetBlockAtIndexInFile(bestIndex, longestForkPath);
            if (prev == null) { Console.WriteLine("prev was null "); return; }
            Console.WriteLine("mining from fork:" + longestForkPath);

            for (uint a = 0; a < NTIMES; a++)
            {
                // so now we have a good prev block to start mining but first we will need TX in ptx file. 
                // just get max to 500 TX ... 
                List<PixelWrapper.PixTX> ptx = PixelWrapper.GetPackOfPixTXInfile(_folderPath + "ptx", 0, 500); 
                MineBlock(ptx, prev, pKey, mUTXOP);
                prev = GetBlockAtIndexInFile(prev.Index + 1, _folderPath + "winblock");
                if (prev == null) { Console.WriteLine("err here"); return; }
            }
        }
        public static void MineBlock(List<PixelWrapper.PixTX> TXS, Block prevBlock, byte[] pKey, ulong mUTXOP)
        {
            // Merkle root is build like this : index + ph + datasize + tx + timestamp + minertoken + hashtarget
            uint unixTimestamp = (uint)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            List<byte> dataBuilder = new List<byte>();
            dataBuilder = AddBytesToList(dataBuilder, BitConverter.GetBytes(prevBlock.Index + 1));
            dataBuilder = AddBytesToList(dataBuilder, prevBlock.Hash);
            dataBuilder = AddBytesToList(dataBuilder, BitConverter.GetBytes((uint)TXS.Count));
            uint sum = 0;
            foreach (PixelWrapper.PixTX TX in TXS) { dataBuilder = AddBytesToList(dataBuilder, PixelWrapper.PixTxToBytes(TX)); sum += TX.TxFee; }
            dataBuilder = AddBytesToList(dataBuilder, BitConverter.GetBytes(unixTimestamp));
            uint currentMiningReward = GetMiningReward(prevBlock.Index + 1);
            uint MR = sum + currentMiningReward;
            MinerToken MT = new MinerToken(pKey, mUTXOP, MR);
            dataBuilder = AddBytesToList(dataBuilder, MinerTokenToBytes(MT));
            byte[] HASH_TARGET;
            if (isNewTargetRequired(prevBlock.Index + 1)) //< will compute with the new hash target 
            {
                Block earlier;

                if (RequestLatestBlockIndex(true) >= prevBlock.Index + 1 - TARGET_CLOCK)
                {
                    earlier = GetBlockAtIndex(prevBlock.Index + 1 - TARGET_CLOCK);
                    if (earlier == null) { return; }
                }
                else
                {
                    earlier = GetBlockAtIndexInFile(prevBlock.Index + 1 - TARGET_CLOCK, _folderPath + "winblock");
                    if (earlier == null) { return; }
                }
                HASH_TARGET = ComputeHashTargetB(prevBlock, earlier);
            }
            else
            {
                HASH_TARGET = prevBlock.HashTarget;
            }
            dataBuilder = AddBytesToList(dataBuilder, HASH_TARGET);

            byte[] sha = ComputeSHA256(ListToByteArray(dataBuilder));
            sha = ComputeSHA256(sha); //< double hash function to avoid collision or anniversary attack 
            uint nonce = 0; // on va dire que c'est un random uint entre 0 et uint.maxvalue
            while (true)
            {
                List<byte> Databuilder = new List<byte>();
                Databuilder = AddBytesToList(Databuilder, BitConverter.GetBytes(nonce));
                Databuilder = AddBytesToList(Databuilder, sha);
                byte[] hash = ListToByteArray(Databuilder);
                hash = ComputeSHA256(hash);
                if (isNonceGolden(hash, HASH_TARGET))
                {
                    Console.WriteLine("[CONGRATS] YOU MINED A BLOCK!!");
                    Block WinnerBlock = new Block(prevBlock.Index + 1, sha, prevBlock.Hash, TXS, unixTimestamp, MT, HASH_TARGET, nonce);
                    PrintBlockData(WinnerBlock);
                    // arduino.SendTick("2");
                    if (File.Exists(_folderPath + "winblock"))
                    {
                        OverWriteBytesInFile(0, _folderPath + "winblock", BitConverter.GetBytes(WinnerBlock.Index));
                        AppendBytesToFile(_folderPath + "winblock", BlockToBytes(WinnerBlock));

                    }
                    else
                    {
                        File.WriteAllBytes(_folderPath + "winblock", BitConverter.GetBytes(WinnerBlock.Index));
                        AppendBytesToFile(_folderPath + "winblock", BlockToBytes(WinnerBlock));
                    }

                    PixelWrapper.UpdatePendingTXFile(WinnerBlock);
                    return;

                }
                else
                {
                    nonce++;
                }
            }
        }
        public static bool isNonceGolden(byte[] hash, byte[] hashtarget)
        {
            BigInteger myHash = BytesToUint256(hash);
            BigInteger target = BytesToUint256(hashtarget);

            if (myHash.CompareTo(target) == -1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
       
        //------------------------ BLOCKCHAIN ---------------------

        
        public static Block GetBlockAtIndexInFile(uint pointer, string filePath) // Return a null Block if CANT BE BE FOUND
        {
            uint byteOffset = 4;
            // Console.WriteLine("want to get " + filePath);
            uint fileLength = (uint)new FileInfo(filePath).Length;
            if (fileLength < 76) { return null; }
            while (true)
            {
                if (BitConverter.ToUInt32(GetBytesFromFile(byteOffset, 4, filePath), 0) == pointer)
                {
                    byteOffset += 68;
                    uint dsb = BitConverter.ToUInt32(GetBytesFromFile(byteOffset, 4, filePath), 0); // this is  datasize ...
                    uint dtbyteslength = 0 ; 
                    for (uint i = 0; i < dsb; i++)
                    {
                        dtbyteslength += 544;
                        if (fileLength < byteOffset + dtbyteslength + 4) { return null; }
                        uint ExtractionSize = BitConverter.ToUInt32(GetBytesFromFile(byteOffset + dtbyteslength, 4, filePath), 0);
                        dtbyteslength += 4 + (ExtractionSize * 72) + 520;
                    }
                    byteOffset -= 68;
                    if (fileLength < byteOffset + 72 + dtbyteslength + 84) { Console.WriteLine("byte missing in file"); return null; }
                    byte[] bytes = GetBytesFromFile(byteOffset, 72 + dtbyteslength + 84, filePath);
                    return BytesToBlock(bytes);
                }
                byteOffset += 68; 
                uint ds = BitConverter.ToUInt32(GetBytesFromFile(byteOffset, 4, filePath), 0);
                uint dtbyteslengthB = 0;
                for (uint i = 0; i < ds; i++)
                {
                    dtbyteslengthB += 544;
                    if (fileLength < byteOffset + dtbyteslengthB + 4) { return null; }
                    uint ExtractionSize = BitConverter.ToUInt32(GetBytesFromFile(byteOffset + dtbyteslengthB, 4, filePath), 0);
                    dtbyteslengthB += 4 + (ExtractionSize * 72) + 520;
                }
                byteOffset -= 68;
                byteOffset += 72 + dtbyteslengthB + 84;
                if (fileLength < byteOffset + 72) { return null; }
            }
        }
        public static Tuple<uint[], string> GetBlockPointerAtIndex(uint pointer) // Return a null Block if CANT BE BE FOUND
        {

            string[] files = Directory.GetFiles(_folderPath + "blockchain");
            List<uint> flist = new List<uint>();
            foreach (string s in files) { flist.Add(Convert.ToUInt32(Path.GetFileName(s))); }
            flist.Sort();

            string filePath = "";
            foreach (uint a in flist)
            {
                uint lastIndex = RequestLatestBlockIndexInFile(_folderPath + "blockchain\\" + a.ToString());
                if (lastIndex >= pointer)
                {
                    filePath = _folderPath + "blockchain\\" + a.ToString();
                    break;
                }
            }
            if (filePath.Length == 0) { return null; }
            uint byteOffset = 4;
            uint fileLength = (uint)new FileInfo(filePath).Length;
            if (fileLength < 76) { return null; }
            while (true)
            {
                if (BitConverter.ToUInt32(GetBytesFromFile(byteOffset, 4, filePath), 0) == pointer)
                {
                    byteOffset += 68;
                    uint dsb = BitConverter.ToUInt32(GetBytesFromFile(byteOffset, 4, filePath), 0); // this is  datasize ...
                    uint dtbyteslength = 0;
                    for (uint i = 0; i < dsb; i++)
                    {
                        dtbyteslength += 544;
                        if (fileLength < byteOffset + dtbyteslength + 4) { return null; }
                        uint ExtractionSize = BitConverter.ToUInt32(GetBytesFromFile(byteOffset + dtbyteslength, 4, filePath), 0);
                        dtbyteslength += 4 + (ExtractionSize * 72) + 520;
                    }
                    byteOffset -= 68;
                    if (fileLength < byteOffset + 72 + dtbyteslength + 84) { Console.WriteLine("byte missing in file"); return null; }
                    return new Tuple<uint[], string>(new uint[2] { byteOffset, byteOffset + 72 + dtbyteslength + 84 }, filePath);
                }
                byteOffset += 68;
                uint ds = BitConverter.ToUInt32(GetBytesFromFile(byteOffset, 4, filePath), 0);
                uint dtbyteslengthB = 0;
                for (uint i = 0; i < ds; i++)
                {
                    dtbyteslengthB += 544;
                    if (fileLength < byteOffset + dtbyteslengthB + 4) { return null; }
                    uint ExtractionSize = BitConverter.ToUInt32(GetBytesFromFile(byteOffset + dtbyteslengthB, 4, filePath), 0);
                    dtbyteslengthB += 4 + (ExtractionSize * 72) + 520;
                }
                byteOffset -= 68;
                byteOffset += 72 + dtbyteslengthB + 84;
                if (fileLength < byteOffset + 72) { return null; }
            }
        }
        public static Block GetBlockAtIndex(uint pointer) //< --- return a specific block at index. Fork NOT Included! Return a null Block if CANT BE BE FOUND
        {

            string[] files = Directory.GetFiles(_folderPath + "blockchain");
            List<uint> flist = new List<uint>();
            foreach (string s in files) { flist.Add(Convert.ToUInt32(Path.GetFileName(s))); }
            flist.Sort();

            string filePath = "";
            foreach (uint a in flist)
            {
                uint lastIndex = RequestLatestBlockIndexInFile(_folderPath + "blockchain\\" + a.ToString());
                if (lastIndex >= pointer)
                {
                    filePath = _folderPath + "blockchain\\" + a.ToString();
                    break;
                }
            }
            if (filePath.Length == 0) { return null; }
            return GetBlockAtIndexInFile(pointer, filePath); 
        }

        public static string GetLatestBlockChainFilePath() //<---- return latest official blockchain file
        {

            string[] files = Directory.GetFiles(_folderPath + "blockchain");
            List<uint> flist = new List<uint>();
            foreach (string s in files) { flist.Add(Convert.ToUInt32(Path.GetFileName(s))); }
            flist.Sort();
            return _folderPath + "blockchain\\" + flist[flist.Count - 1].ToString();
        }
        public static string GetIndexBlockChainFilePath(uint pointer) //<---- return a filepath Official Only !
        {

            string[] files = Directory.GetFiles(_folderPath + "blockchain");
            List<uint> flist = new List<uint>();
            foreach (string s in files) { flist.Add(Convert.ToUInt32(Path.GetFileName(s))); }
            flist.Sort();

            string filePath = "";
            foreach (uint a in flist)
            {
                uint lastIndex = RequestLatestBlockIndexInFile(_folderPath + "blockchain\\" + a.ToString());
                if (lastIndex >= pointer)
                {
                    filePath = _folderPath + "blockchain\\" + a.ToString();
                    break;
                }
            }
            return filePath;
        }
        public static uint RequestLatestBlockIndex(bool onlyOfficial) // can do an error
        {
            if (onlyOfficial)
            {
                string s = GetLatestBlockChainFilePath();
                if (new FileInfo(s).Length < 4) { Console.WriteLine("file wrong format"); return uint.MaxValue; }
                return BitConverter.ToUInt32(GetBytesFromFile(0, 4, s), 0);
            }
            else
            {
                string[] files = Directory.GetFiles(_folderPath + "fork");
                string sp = GetLatestBlockChainFilePath();
                if (new FileInfo(sp).Length < 4) { Console.WriteLine("file wrong format"); return uint.MaxValue; }
                uint highest_BlockIndex = BitConverter.ToUInt32(GetBytesFromFile(0, 4, sp), 0); //< create an infinite loop .... 
                foreach (string s in files)
                {
                    if (new FileInfo(s).Length < 4) { Console.WriteLine("file wrong format"); return uint.MaxValue; }
                    uint currentIndex = BitConverter.ToUInt32(GetBytesFromFile(0, 4, s), 0);
                    if (currentIndex > highest_BlockIndex)
                    {
                        highest_BlockIndex = currentIndex;
                    }
                }
                return highest_BlockIndex;
            }

        }
        public static uint RequestLatestBlockIndexInFile(string _filePath) // can do an error
        {
            if (new FileInfo(_filePath).Length < 4) { Console.WriteLine("file wrong format"); return uint.MaxValue; }
            uint currentIndex = BitConverter.ToUInt32(GetBytesFromFile(0, 4, _filePath), 0);
            return currentIndex;
        }
        public static string RequestLatestIndexFilePath()
        {
            string[] files = Directory.GetFiles(_folderPath + "fork");
            uint highest_BlockIndex = RequestLatestBlockIndex(false);
            string fPath = GetLatestBlockChainFilePath();
            foreach (string s in files)
            {
                if (new FileInfo(s).Length < 4) { Console.WriteLine("file wrong format"); return ""; }
                uint currentIndex = BitConverter.ToUInt32(GetBytesFromFile(0, 4, s), 0);
                if (currentIndex > highest_BlockIndex)
                {
                    highest_BlockIndex = currentIndex;
                    fPath = s;
                }
            }
            return fPath;
        }
        public static string GetNewForkFilePath()
        {
            string[] files = Directory.GetFiles(_folderPath + "fork");
            return _folderPath + "fork\\" + files.Length.ToString();
        }
        public static string GetNewBlockChainFilePath()
        {
            string[] files = Directory.GetFiles(_folderPath + "blockchain");
            return _folderPath + "blockchain\\" + files.Length.ToString();
        }

        //------------------------ CURRENCY ALGO ----------------------
        public static uint GetFee(PixelWrapper.UPO upo)
        {
            uint fee = 2;
            return fee;
        }



        //------------------------ WALLET     ---------------------


        public static void GenerateNewPairKey() // better to use offline & on another device. 
        {
            if (File.Exists(_folderPath + "privateKey") || File.Exists(_folderPath + "publicKey"))
            {
                Console.WriteLine("Already existing RSA key files has been found in app folder. Please move them or rename them. RSA Key Gen has been aborted");
                return;
            }
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(4096);
            byte[] _privateKey = rsa.ExportCspBlob(true);
            byte[] _publicKey = rsa.ExportCspBlob(false);
            File.WriteAllBytes(_folderPath + "privateKey", rsa.ExportCspBlob(true));
            File.WriteAllBytes(_folderPath + "publicKey", rsa.ExportCspBlob(false));
            rsa.Clear();
            Console.WriteLine("RSA public and private keys successfully created and saved in app folder! ");
            Process.Start(_folderPath + "QRMYKEYS.exe");
            Console.WriteLine("QR Code of your assymetric keys will be generated... ");
            // Console.WriteLine("Please (4 security) use QRMYKEYS, print output and delete key files ");

        }
        /*
        public static void SetUpTx(string _sprkey, string _spukey, uint sutxop, uint amount, string _rpukey, uint rutxop, uint fee, uint locktime)
        {
            //newtx sprkey: spukey: sutxop: amount: rpukey: rutxop: fee:
            byte[] _MyPublicKey = File.ReadAllBytes(_spukey);
            byte[] sUTXOPointer = BitConverter.GetBytes(sutxop);
            byte[] Coinamount = BitConverter.GetBytes(amount);
            byte[] _hashOthPublicKey = ComputeSHA256(File.ReadAllBytes(_rpukey));
            byte[] rUTXOPointer = BitConverter.GetBytes(rutxop);
            byte[] FEE = BitConverter.GetBytes(fee);
            byte[] LockTime = BitConverter.GetBytes(locktime);
            if (sutxop == 0) { Console.WriteLine("bad pointer"); return; }
            UTXO utxo = GetOfficialUTXOAtPointer(sutxop);
            if (utxo == null) { Console.WriteLine("bad pointer"); return; }
            if (!utxo.HashKey.SequenceEqual(ComputeSHA256(_MyPublicKey))) { Console.WriteLine("bad pointer"); return; }
            uint newtou = utxo.TokenOfUniqueness + 1;
            byte[] TOU = BitConverter.GetBytes(newtou);
            bool needDust = false;
            if (rutxop != 0)
            {
                UTXO oUTXO = GetOfficialUTXOAtPointer(rutxop);
                if (oUTXO == null) { Console.WriteLine("bad pointer"); return; }
            }
            else { needDust = true; }
            if (GetFee(utxo, needDust) > fee)
            {
                Console.WriteLine("insuffisiant fee");
                return;
            }
            if (fee + amount > utxo.Sold) { Console.WriteLine("insuffisiant sold"); return; }
            //           if ( TX.TxFee < GetFee(sUTXO, dustNeeded)) { Console.WriteLine("Invalid Fee"); return false;  }
            // uint sum = TX.Amount + TX.TxFee;
            CreateTxFile(_sprkey, _MyPublicKey, Coinamount, LockTime, sUTXOPointer, rUTXOPointer, TOU, FEE, _hashOthPublicKey);

        }


        public static void CreateTxFile(string _MyPrivateKey, byte[] _MyPublicKey, byte[] Coinamount, byte[] LockTime, byte[] sUTXOPointer, byte[] rUTXOPointer, byte[] TOU, byte[] FEES, byte[] _hashOthPublicKey)
        {

            RSACryptoServiceProvider _MyPrRsa = new RSACryptoServiceProvider();
            try
            {
                _MyPrRsa.ImportCspBlob(File.ReadAllBytes(_MyPrivateKey));
                List<byte> DataBuilder = new List<byte>();
                
                DataBuilder = AddBytesToList(DataBuilder, _MyPublicKey);
                DataBuilder = AddBytesToList(DataBuilder, Coinamount);
                DataBuilder = AddBytesToList(DataBuilder, _hashOthPublicKey);
                DataBuilder = AddBytesToList(DataBuilder, LockTime);
                DataBuilder = AddBytesToList(DataBuilder, sUTXOPointer);
                DataBuilder = AddBytesToList(DataBuilder, rUTXOPointer);
                DataBuilder = AddBytesToList(DataBuilder, TOU);
                DataBuilder = AddBytesToList(DataBuilder, FEES);

                // + sign

                byte[] UnsignedData = ListToByteArray(DataBuilder);
                UnsignedData = ComputeSHA256(UnsignedData); //< 1 : hash data
                byte[] Signature = _MyPrRsa.SignHash(UnsignedData, CryptoConfig.MapNameToOID("SHA256")); //< 2 : produce signature
                DataBuilder = AddBytesToList(DataBuilder, Signature); //< add the signature to the bytearray...
                byte[] SignedData = ListToByteArray(DataBuilder);

                if (!VerifyTransactionDataSignature(SignedData))
                {
                    Console.Write("Wrong Signature. Bad Input");
                    return;

                }

                string fileName = _folderPath + "TX" + BitConverter.ToUInt32(TOU, 0).ToString();
                File.WriteAllBytes(fileName, SignedData);
                Console.WriteLine("Tx" + BitConverter.ToUInt32(TOU, 0) + "file was successfully generated at roots. Signature :" + SHAToHex(Signature, false));
                _MyPrRsa.Clear();
                return;
            }
            catch (CryptographicException e)
            {
                Console.WriteLine(e.Message);
                _MyPrRsa.Clear();
                return;
            }

        }
        */
        public static bool VerifyTransactionDataSignature(PixelWrapper.PixTX ptx)
        {
            byte[] dataBytes = PixelWrapper.PixTxToBytes(ptx);
            if (dataBytes.Length != 548 + (ptx.ExtractionSize * 72) + 520) { return false; }
            byte[] mPKey = new byte[532]; 
            byte[] msg = new byte[548+(ptx.ExtractionSize*72)];
            byte[] signature = new byte[512];
            for (int i = 0; i < 532; i++) { mPKey[i] = dataBytes[i]; }
            for (int i = 0; i < 548 + (ptx.ExtractionSize * 72); i++) { msg[i] = dataBytes[i]; }
            for (int i = 548 + ((int)ptx.ExtractionSize * 72); i < dataBytes.Length; i++) { signature[i - 548 + ((int)ptx.ExtractionSize * 72)] = dataBytes[i]; }

            RSACryptoServiceProvider _MyPuRsa = new RSACryptoServiceProvider();
            try
            {
                _MyPuRsa.ImportCspBlob(mPKey);
                bool success = _MyPuRsa.VerifyData(msg, CryptoConfig.MapNameToOID("SHA256"), signature);
                if (success)
                {
                    _MyPuRsa.Clear();
                    return true;
                }
                else
                {
                    _MyPuRsa.Clear();
                    return false;
                }


            }
            catch (CryptographicException e)
            {
                Console.WriteLine(e.Message);
                _MyPuRsa.Clear();
                return false;
            }
        }
    }


}
