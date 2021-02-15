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
        // Current Miner Info for Miner Signature

        public static byte[] MYMINERPKEY = new byte[32];
        public static uint MYUTXOPOINTER = 0;

        // Mining Settings

        public static uint MAXLOCKTIMESETTING = 10000;
        public static uint NTIMES = 0;
        public static bool MININGENABLED = false;
        public static uint MINING_CLOCK_LIMIT_MULTIPLIER = 2; // multiplier of max attempted second before rebuild mining proccess
        // Mining Proccess

        public static string StartMining(byte[] pKey, uint mUTXOP, uint MAXLOCKTIME, uint NTIMES)
        {

            CleanOldPendingTX(false);

            // THIS IS A BASIC MINING STRATEGY (NOT OPTIMIZED) -> 
            /*
             * WE REFUSE FORK THAT HAVE TX LOCKTIME PURISHING IN 5000 (s) about 1hour and half.
             * WE GET THE LONGEST FORK. IF NOT. JUST MINE FROM OFFICIAL... 
             * WE ALSO REFUSE TX THAT HAVE LOCKTIME PURISHING IN 5000 in ptx file.
             */
            uint unixTimestamp = (uint)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            Block prev = GetBlockAtIndex(RequestLatestBlockIndex(true));
            if (prev == null) { return ""; }
            Print(prev.Index.ToString());
            string[] forkfiles = Directory.GetFiles(_folderPath + "fork");
            List<string> okfiles = new List<string>();
            foreach (string s in forkfiles)
            {
                uint latestIndex = RequestLatestBlockIndexInFile(s);
                uint startIndex = BitConverter.ToUInt32(GetBytesFromFile(4, 8, s), 0);
                bool OK = true;
                for (uint i = startIndex; i < latestIndex + 1; i++)
                {
                    Block b = GetBlockAtIndexInFile(i, s);
                    if (b == null) { return ""; }
                    foreach (Tx TX in b.Data)
                    {
                        if (TX.LockTime < unixTimestamp + MAXLOCKTIME)
                        {
                            OK = false;
                        }
                    }
                }
                if (OK) { okfiles.Add(s); }
            }
            if (okfiles.Count > 0)
            {
                uint bestIndex = 0;
                string longestForkPath = "";
                foreach (string s in okfiles)
                {
                    uint latestIndex = RequestLatestBlockIndexInFile(s);
                    if (latestIndex > bestIndex)
                    {
                        bestIndex = latestIndex;
                        longestForkPath = s;
                    }
                }
                prev = GetBlockAtIndexInFile(bestIndex, longestForkPath);
                if (prev == null) { return ""; }
                Print("mining from fork:" + longestForkPath);
            }
            TimeSpan testdatetime = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1));
            string elapsed = testdatetime.Days.ToString() + testdatetime.Hours.ToString() + testdatetime.Minutes.ToString() + testdatetime.Seconds.ToString() + testdatetime.Milliseconds.ToString();
            byte[] sb = Encoding.ASCII.GetBytes(elapsed);
            sb = ComputeSHA256(sb);
            string WinBlockPath = _folderPath + "net/" + SHAToHex(sb, false);

            uint clock_limit = TARGET_TIME / TARGET_CLOCK;
            clock_limit *= MINING_CLOCK_LIMIT_MULTIPLIER;
            // clock_limit should also get tidal prevision to adjust himself

            for (uint a = 0; a < NTIMES; a++)
            {
                CleanOldPendingTX(false);
                // so now we have a good prev block to start mining but first we will need TX in ptx file. 
                List<Tx> txs = new List<Tx>();
                uint fl = (uint)new FileInfo(_folderPath + "ptx").Length;
                uint byteOffset = 0;
                while (byteOffset < fl)
                {
                    Tx TX = BytesToTx(GetBytesFromFile(byteOffset, 1100, _folderPath + "ptx"));
                    if (TX == null) { Console.WriteLine("TX was null during mining proccess."); return ""; }
                  
                    if (TX.LockTime > unixTimestamp + MAXLOCKTIME)
                    {
                        txs.Add(TX);

                    }
                    byteOffset += 1100;
                }
                // now just get TX that has the highest EXTRA FEE ! 4 da money :>)
                List<Tx> finalTX = new List<Tx>();
                txs = txs.OrderBy(x => x.Amount).ToList();
                for (int i = 0; i < txs.Count; i++)
                {
                    if (i == 500) { break; }
                    finalTX.Add(txs[i]);
                    Console.WriteLine("TX ADDED");
                }
                WinBlockPath = MineBlock(finalTX, prev, pKey, mUTXOP, WinBlockPath, clock_limit);
                if (WinBlockPath.Length == 0) { Console.WriteLine("return path of mineblock() was empty!"); return ""; }
                prev = GetBlockAtIndexInFile(prev.Index + 1, WinBlockPath);
                if (prev == null) { Console.WriteLine("previous block was null!"); return ""; }
            }
            return WinBlockPath;
        }
        public static string MineBlock(List<Tx> TXS, Block prevBlock, byte[] pKey, uint mUTXOP, string WinBlockPath, uint clock_limit)
        {
            // Merkle root is build like this : index + ph + datasize + tx + timestamp + minertoken + hashtarget
            uint unixTimestamp = (uint)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            List<byte> dataBuilder = new List<byte>();
            dataBuilder = AddBytesToList(dataBuilder, BitConverter.GetBytes(prevBlock.Index + 1));
            dataBuilder = AddBytesToList(dataBuilder, prevBlock.Hash);
            dataBuilder = AddBytesToList(dataBuilder, BitConverter.GetBytes((uint)TXS.Count));
            uint sum = 0;
            foreach (Tx TX in TXS) { dataBuilder = AddBytesToList(dataBuilder, TxToBytes(TX)); sum += TX.TxFee; }
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
                    if (earlier == null) { Console.WriteLine("previous block was null during mining proccess."); return ""; }
                }
                else
                {
                    earlier = GetBlockAtIndexInFile(prevBlock.Index + 1 - TARGET_CLOCK, WinBlockPath);
                    if (earlier == null) { Console.WriteLine("previous block was null during mining proccess."); return ""; }
                }
                HASH_TARGET = ComputeHashTargetB(prevBlock, earlier);
            }
            else
            {
                HASH_TARGET = prevBlock.HashTarget;
            }
 
            dataBuilder = AddBytesToList(dataBuilder, HASH_TARGET);
            byte[] CLEAN_HASH_TARGET = HASH_TARGET;
            float water_level = Tidal.GetTidalAtSpecificTime(unixTimestamp);
            HASH_TARGET = ApplyTheSeaToTheCryptoPuzzle(HASH_TARGET, water_level);


            byte[] sha = ComputeSHA256(ListToByteArray(dataBuilder));
            sha = ComputeSHA256(sha); //< double hash function to avoid collision or anniversary attack
            byte[] nonceByte = new byte[4];
            Random rd = new Random();
            rd.NextBytes(nonceByte);
            uint nonce = BitConverter.ToUInt32(nonceByte, 0);
            //uint nonce = 0; // on va dire que c'est un random uint entre 0 et uint.maxvalue
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Stopwatch sw2 = new Stopwatch();
            sw2.Start();
            uint trycounter = 0;
            while (true)
            {
                List<byte> Databuilder = new List<byte>();
                Databuilder = AddBytesToList(Databuilder, BitConverter.GetBytes(nonce));
                Databuilder = AddBytesToList(Databuilder, sha);
                byte[] hash = ListToByteArray(Databuilder);
                hash = ComputeSHA256(hash);
                if (isNonceGolden(hash, HASH_TARGET))
                {
                    Print("[CONGRATS] YOU MINED A BLOCK!!");
                    Block WinnerBlock = new Block(prevBlock.Index + 1, sha, prevBlock.Hash, TXS, unixTimestamp, MT, CLEAN_HASH_TARGET, nonce); // reapply hash to the stuff
                    PrintBlockData(WinnerBlock);
                    Console.WriteLine("will write at " + WinBlockPath);
                    arduino.SendTick("5"); // go sing the coucou
                    arduino.HashToClock(hash);
                    if (File.Exists(WinBlockPath))
                    {
                        Console.WriteLine("will write a new fi le at " + WinBlockPath);
                        OverWriteBytesInFile(0, WinBlockPath, BitConverter.GetBytes(WinnerBlock.Index));
                        AppendBytesToFile(WinBlockPath, BlockToBytes(WinnerBlock));

                    }
                    else
                    {
                        Console.WriteLine("append bytes at " + WinBlockPath);
                        File.WriteAllBytes(WinBlockPath, BitConverter.GetBytes(WinnerBlock.Index));
                        AppendBytesToFile(WinBlockPath, BlockToBytes(WinnerBlock));
                    }
                    Console.WriteLine("shoud have write at " + WinBlockPath);
                    UpdatePendingTXFile(WinnerBlock);
                    return WinBlockPath;

                }
                else
                {
                    if ( sw.Elapsed.Seconds > 5)
                    {
                        Console.WriteLine("______________________________________");
                        Console.WriteLine("           [Mining report]        ");
                        Console.WriteLine("total attempts      : " + trycounter);
                        Console.WriteLine("last hash produced  :" + SHAToHex(hash,false));
                        Console.WriteLine("index               : " + (prevBlock.Index + 1).ToString());
                        Console.WriteLine("previous hash       : " + SHAToHex(prevBlock.Hash, false));
                        Console.WriteLine("hash target         : " + SHAToHex(HASH_TARGET, false));
                        Console.WriteLine("water level         : " + water_level);
                        Console.WriteLine("______________________________________");
                        sw.Restart();
                    }
                    if ( sw2.Elapsed.Seconds > clock_limit)
                    {
                        Console.WriteLine("Mining time limit reached. Mining aborted for this specific new block. ");
                        return "";
                    }
                    if (nonce == uint.MaxValue)
                    {
                        rd.NextBytes(nonceByte);
                        nonce = BitConverter.ToUInt32(nonceByte, 0);
                    }
                    trycounter++;
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
    }
}
