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
        public static bool PRINT_INFO = true;
        public static bool STOP_MINING = false;


        // Command methods and proccess

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
                if (argument == "setsecure")
                {
                    string s = argument.Replace("setsecure", "");
                    s = s.Replace(" ", "");
                    if ( s == "1")
                    {
                        SECURE_MODE = true;
                        Console.WriteLine("Secure mode set to true.");
                    }
                    if (s == "0")
                    {
                        SECURE_MODE = true;
                        Console.WriteLine("Secure mode set to false.");
                    }

                    
                    argumentFound = true;
                }
                if (argument.Contains("gettidal"))
                {
                    uint unixTimestamp = (uint)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                    Console.WriteLine("Water height from harmonics at St-Nazaire : " + Tidal.GetTidalAtSpecificTime(unixTimestamp) + "meters" );
                    argumentFound = true;

                }
                if (argument == "getcominfo")
                {
                    arduino.GetPortReady();
                    argumentFound = true;
                }
                if (argument.Contains("setarduino"))
                {
                    string s = argument.Replace("setarduino", "");
                    s = s.Replace(" ", "");
                    arduino.Initialize(s);
                    argumentFound = true;

                }
                if (argument.Contains("setseapow"))
                {
                    string s = argument.Replace("setseapow", "");
                    s = s.Replace(" ", "");
                    uint val = 0;
                    if (uint.TryParse(s, out val))
                    {
                        if ( ValidYesOrNo("[Warning] If you change SEA_FORCE parameters, you should do it on all the network nodes..."))
                        {
                            float old_val = SEA_FORCE;
                            SEA_FORCE = val;
                            Console.WriteLine("Sea force successfully changed from " + old_val + " to " + SEA_FORCE);
                        }
                    }
                    else
                    {
                        Print("bad arguments ");
                    }
                    argumentFound = true;

                }
                if (argument.Contains("setport"))
                {
                    string s = argument.Replace("setport", "");
                    s = s.Replace(" ", "");
                    int port = 37;
                    if (int.TryParse(s, out port))
                    {
                        ACTUAL_PORT = port;
                        Console.WriteLine("new port for data request is " + ACTUAL_PORT);
                        argumentFound = true;
                    }
                    else
                    {
                        Print("bad arguments ");
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
                if (argument.Contains("readblockascii"))
                {
                    string s = argument.Replace("readblockascii", "");
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
                            PrintBlockInASCII(b);
                        }
                        argumentFound = true;
                    }
                    else
                    {
                        Console.WriteLine("invalid argument");
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
                if (argument.Contains("getutxo"))
                {
                    string s = argument.Replace("getutxo", "");
                    s = s.Replace(" ", "");
                    uint index = 0;
                    bool success = uint.TryParse(s, out index);
                    if (success)
                    {
                        UTXO utxo = GetOfficialUTXOAtPointer(index);
                        if (utxo == null)
                        {
                            Console.WriteLine("UTXO of this public Key not existing in UTXO Set ");
                        }
                        else
                        {
                            Console.WriteLine("UTXO Hash    :" + SHAToHex(utxo.HashKey, false));
                            Console.WriteLine("UTXO Sold    :" + utxo.Sold);
                            Console.WriteLine("UTXO Token   :" + utxo.TokenOfUniqueness);
                        }
                        argumentFound = true;
                    }
                }
                if (argument.Contains("getutxop"))
                {
                    string path = argument.Replace("location", AppDomain.CurrentDomain.BaseDirectory.Remove(AppDomain.CurrentDomain.BaseDirectory.Length - 1));
                    path = getfilePath(path.ToCharArray());
                    if (path.Length == 0) { Console.WriteLine("invalid argument. Please set path of your public key file in quote. "); }
                    else
                    {
                        Console.WriteLine(path);
                        if (File.Exists(path))
                        {
                            byte[] pkeyHASH = ComputeSHA256(File.ReadAllBytes(path));
                            Console.WriteLine("result for hash key : " + SHAToHex(pkeyHASH, false));
                            uint myUTXOP = GetUTXOPointer(pkeyHASH);
                            UTXO myUTXO = GetOfficialUTXOAtPointer(myUTXOP);
                            if (myUTXO != null)
                            {
                                Console.WriteLine("UTXO Hash    :" + SHAToHex(pkeyHASH, false));
                                Console.WriteLine("UTXO Pointer :" + myUTXOP);
                                Console.WriteLine("UTXO Sold    :" + myUTXO.Sold);
                                Console.WriteLine("UTXO Token   :" + myUTXO.TokenOfUniqueness);
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
                    argumentFound = true;
                    string pkeyArgs = GetStringAfterArgs(argument, "pkey:", '\"');
                    string utxopArgs = GetStringAfterArgs(argument, "utxop:");
                    string minlockArgs = GetStringAfterArgs(argument, "minlock:");
                    string ntimeArgs = GetStringAfterArgs(argument, "ntime:");
                    string path = "";
                    uint utxopointer = 0;
                    if (pkeyArgs.Length != 0)
                    {
                        path = pkeyArgs.Replace("location", AppDomain.CurrentDomain.BaseDirectory.Remove(AppDomain.CurrentDomain.BaseDirectory.Length - 1));
                        path = getfilePath(path.ToCharArray());
                    }
                    else
                    {
                        // automatically detect if there is a key in folder 
                        string[] files = Directory.GetFiles(_folderPath, "publicKey", SearchOption.AllDirectories);
                        if (files.Length > 0)
                        {
                            if (files.Length > 1)
                            {
                                Console.WriteLine("Multiple public keys have been found! ");
                                if (ValidYesOrNo("This will automatically use  " + files[0] + "..."))
                                {
                                    path = files[0];
                                }
                            }
                            else
                            {
                                Console.WriteLine("Will use key " + files[0]);
                                path = files[0];
                            }
                        }
                        else
                        {
                            Console.WriteLine("No public key found in directories and subdirectories. Please set a path in argument.");
                        }
                    }

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
                            uint parsePointer = 0;
                            if (!uint.TryParse(utxopArgs, out parsePointer) && Continue)
                            {
                                if (!ValidYesOrNo("[WARNING] Incorrect UTXO pointer") && Continue) { Continue = false; }
                            }
                            else
                            {
                                UTXO utxo = GetOfficialUTXOAtPointer(parsePointer);
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
                            uint mnlock = 5000;
                            uint nTime = 0;
                            if (minlockArgs.Length != 0)
                            {
                                uint.TryParse(minlockArgs, out mnlock);
                            }
                            if (ntimeArgs.Length != 0)
                            {
                                uint.TryParse(ntimeArgs, out nTime);
                            }

                            MYMINERPKEY = pkeyHASH;
                            MYUTXOPOINTER = utxopointer;
                            MAXLOCKTIMESETTING = mnlock;
                            NTIMES = nTime;
                            MININGENABLED = true;
                            argumentFound = true;
                        }

                    }
                    else
                    {
                        Console.WriteLine("Path : " + path + " does not exist. Please set path of your public key file. ");
                    }
                }
                if (argument.Contains("newtx"))
                {
                    //newtx sprkey: spukey: sutxop: amount: rpukey: rutxop: fee: lock: ( offset in sec since now we want to create the tx)
                    while (argument.Contains("location"))
                    {
                        argument = argument.Replace("location", AppDomain.CurrentDomain.BaseDirectory.Remove(AppDomain.CurrentDomain.BaseDirectory.Length - 1));
                    }
                    //string path = pkeyArgs.Replace("location", AppDomain.CurrentDomain.BaseDirectory);
                    //path = getfilePath(path.ToCharArray());
                    string sprkeyArgs = getfilePath(GetStringAfterArgs(argument, "sprkey:", '\"').ToCharArray()); //< i use get path
                    string spukeyArgs = getfilePath(GetStringAfterArgs(argument, "spukey:", '\"').ToCharArray());
                    string sutxopArgs = GetStringAfterArgs(argument, "sutxop:"); //<
                    string amountArgs = GetStringAfterArgs(argument, "amount:");
                    string rpukeyArgs = getfilePath(GetStringAfterArgs(argument, "rpukey:", '\"').ToCharArray());
                    string feeArgs = GetStringAfterArgs(argument, "fee:");
                    string locktimeArgs = GetStringAfterArgs(argument, "lock:");
                    if (sprkeyArgs.Length == 0 || spukeyArgs.Length == 0 || sutxopArgs.Length == 0 || amountArgs.Length == 0 || rpukeyArgs.Length == 0
                        || feeArgs.Length == 0 || locktimeArgs.Length == 0)
                    {
                        Console.WriteLine("missing argument. see getcmdinfo. ");
                    }
                    else
                    {
                        uint sutxop = 0;
                        uint amount = 0;
                        uint rutxop = 0;
                        uint fee = 0;
                        uint locktime = 0;
                        if (!File.Exists(sprkeyArgs) || !File.Exists(spukeyArgs) || !uint.TryParse(sutxopArgs, out sutxop) || !uint.TryParse(amountArgs, out amount)
                            || !File.Exists(rpukeyArgs)  || !uint.TryParse(feeArgs, out fee) || !uint.TryParse(locktimeArgs, out locktime))
                        {
                            Console.WriteLine("invalid argument. see getcmdinfo. ");
                        }
                        else
                        {
                            SetUpTx(sprkeyArgs, spukeyArgs, sutxop, amount, rpukeyArgs, rutxop, fee, locktime);
                            argumentFound = true;
                        }
                    }

                }
                if (argument.Contains("hideapoeminatransaction"))
                {
                    //newtx sprkey: spukey: sutxop: amount: rpukey: rutxop: fee: lock:
                    while (argument.Contains("location"))
                    {
                        argument = argument.Replace("location", AppDomain.CurrentDomain.BaseDirectory.Remove(AppDomain.CurrentDomain.BaseDirectory.Length - 1));
                    }
                    //string path = pkeyArgs.Replace("location", AppDomain.CurrentDomain.BaseDirectory);
                    //path = getfilePath(path.ToCharArray());
                    string sprkeyArgs = getfilePath(GetStringAfterArgs(argument, "sprkey:", '\"').ToCharArray()); //< i use get path
                    string spukeyArgs = getfilePath(GetStringAfterArgs(argument, "spukey:", '\"').ToCharArray());
                    string sutxopArgs = GetStringAfterArgs(argument, "sutxop:"); //<
                    string poem = getfilePath(GetStringAfterArgs(argument, "poem:", '\"').ToCharArray());
                    //string rpukeyArgs = getfilePath(GetStringAfterArgs(argument, "rpukey:", '\"').ToCharArray());
                   // string rutxopArgs = GetStringAfterArgs(argument, "rutxop:");
                    string feeArgs = GetStringAfterArgs(argument, "fee:");
                    string locktimeArgs = GetStringAfterArgs(argument, "lock:");
                    if (sprkeyArgs.Length == 0 || spukeyArgs.Length == 0 || sutxopArgs.Length == 0 
                        || feeArgs.Length == 0 || locktimeArgs.Length == 0 || poem.Length == 0)
                    {
                        Console.WriteLine("missing argument. see getcmdinfo. ");
                    }
                    else
                    {
                        uint sutxop = 0;
                        uint rutxop = 0;
                        uint fee = 0;
                        uint locktime = 0;
                        if (!File.Exists(sprkeyArgs) || !File.Exists(spukeyArgs) || !uint.TryParse(sutxopArgs, out sutxop) 
                             || !uint.TryParse(feeArgs, out fee) || !uint.TryParse(locktimeArgs, out locktime))
                        {
                            Console.WriteLine("invalid argument. see getcmdinfo. ");
                        }
                        else
                        {
                            //SetUpTx(sprkeyArgs, spukeyArgs, sutxop, amount, rpukeyArgs, rutxop, fee, locktime);
                            Console.WriteLine("poem is " + poem);
                            XYPoem.BuildandHidePoemsInPublicKeyHash(sprkeyArgs, spukeyArgs, sutxop, 0, poem, rutxop, fee, locktime);
                            argumentFound = true;
                        }
                    }

                }
                if (argument.Contains("reqtx"))
                {
                    argument = argument.Replace("reqtx", "");
                    argument = argument.Replace("location", AppDomain.CurrentDomain.BaseDirectory.Remove(AppDomain.CurrentDomain.BaseDirectory.Length - 1)); // enlever le dernier / ici ! 
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
        public static void MagicKey()
        {

            while (true)
            {
                if (!Console.KeyAvailable)
                {
                    ConsoleKey Key = Console.ReadKey(true).Key;
                    if (Key == ConsoleKey.F1)
                    {
                        STOP_MINING = true;
                        Console.WriteLine("Mining stopped!");
                    }
                    if (Key == ConsoleKey.F3)
                    {
                        PRINT_INFO = !PRINT_INFO;
                        if (PRINT_INFO)
                        {
                            Console.WriteLine("Get Info stream enabled");
                        }
                        else
                        {
                            Console.WriteLine("Get Info stream disabled");
                        }
                    }
                }

            }

        }
        public static string GetStringAfterArgs(string searchstring, string arg, char specificdelimiter = ' ')
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
        public static bool ValidYesOrNo(string warning)
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
        public static string getfilePath(char[] searchstring)
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
        
        // Basic blocks, Transaction content printing in UI

        public static void Print(string msg)
        {
            Console.WriteLine(msg);
        }
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
            Console.WriteLine("Create Transaction -> newtx sprkey:# spukey:# sutxop:# amount:# rpukey:# rutxop:# fee:# lock:#");
            Console.WriteLine("Add Pending Transaction  -> reqtx #");
            Console.WriteLine("Connect to nodes         -> netconnect");
            Console.WriteLine("Set Broadcast parameter  -> setbrcparam max:# fbc:#");
            Console.WriteLine("Get Water Height (meter) -> gettidal");
            Console.WriteLine("Get COM Port available   -> getcominfo");
            Console.WriteLine("Connect to Arduino       -> setarduino #(port name)");
            Console.WriteLine("Print this               -> getcmdinfo");

            Console.WriteLine("Print/Hide Info          -> [F11]");
            Console.WriteLine("Abort Mining Proccess    -> [ANY KEY]");

            Console.WriteLine("");
        }
        public static void PrintBlockData(Block b)
        {

            Console.WriteLine("-----------------");
            Console.WriteLine("index         : " + b.Index);
            Console.WriteLine("merkle root   : " + SHAToHex(b.Hash, false));
            Console.WriteLine("previous hash : " + SHAToHex(b.previousHash, false));
            Console.WriteLine("#TX           : " + b.DataSize);
            foreach (Tx TXS in b.Data)
            {
                PrintTXData(TXS);
            }
            Console.WriteLine("hash target   : " + SHAToHex(b.HashTarget, false));
            Console.WriteLine("nonce         : " + b.Nonce);
            Console.WriteLine("");
        }
        public static void PrintBlockInASCII(Block b)
        {
            byte[] bytes = BlockToBytes(b);
            string s = BitConverter.ToString(bytes);
            Console.WriteLine(s);

        }
        public static void PrintTXData(Tx TX)
        {
            Console.WriteLine("-----------------");
            Console.WriteLine("sender           : " + SHAToHex(ComputeSHA256(TX.sPKey), false));
            Console.WriteLine("receiver         : " + SHAToHex(TX.rHashKey, false));
            Console.WriteLine("amount           : " + TX.Amount);
            Console.WriteLine("fee              : " + TX.TxFee);
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

    }
}
