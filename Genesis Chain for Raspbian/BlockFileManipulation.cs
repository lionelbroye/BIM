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
        // General Blockchain File downgrading or upgrading ( Official and Forks )

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
                UpdatePendingTXFile(b);
            }
            OverWriteBytesInFile(0, newForkPath, BitConverter.GetBytes(LastIndex));
            Print("will delete " + _path2);
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
            Print("Blockchain Downgraded at " + pointer);
        }
        public static void AddBlocksToOfficialChain(string filePath, bool needPropagate)
        {
            // GET THE LATEST BLOCKCHAIN FILE PATH.->
            string blockchainPath = GetLatestBlockChainFilePath();

            uint firstTempIndex = BitConverter.ToUInt32(GetBytesFromFile(4, 8, filePath), 0);
            uint latestTempIndex = RequestLatestBlockIndexInFile(filePath);
            //Print(latestTempIndex);
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
                UpgradeUTXOSet(b);
                UpdatePendingTXFile(b);
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
            Print("will delete " + filePath);
            File.Delete(filePath);
            Print("Blockchain updated!");
            //arduino.SendTick("1");

        }

        // Get Blocks in file

        public static Block GetBlockAtIndexInFile(uint pointer, string filePath) // Return a null Block if CANT BE BE FOUND
        {
            uint byteOffset = 4;
            // Print("want to get " + filePath);
            uint fileLength = (uint)new FileInfo(filePath).Length;
            if (fileLength < 76) { return null; }
            while (true)
            {
                if (BitConverter.ToUInt32(GetBytesFromFile(byteOffset, 4, filePath), 0) == pointer)
                {
                    byteOffset += 68;
                    uint dsb = BitConverter.ToUInt32(GetBytesFromFile(byteOffset, 4, filePath), 0);
                    // Print(dsb); //< this is called twice i dont fucking know why ! 
                    byteOffset -= 68;
                    if (fileLength < byteOffset + 72 + (dsb * 1100) + 80) { Print("byte missing in file"); return null; }
                    byte[] bytes = GetBytesFromFile(byteOffset, 72 + (dsb * 1100) + 80, filePath);
                    return BytesToBlock(bytes);

                }
                byteOffset += 68;
                uint ds = BitConverter.ToUInt32(GetBytesFromFile(byteOffset, 4, filePath), 0);
                byteOffset -= 68;
                byteOffset += 72 + (ds * 1100) + 80;
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
                uint lastIndex = RequestLatestBlockIndexInFile(_folderPath + "blockchain/" + a.ToString());
                if (lastIndex >= pointer)
                {
                    filePath = _folderPath + "blockchain/" + a.ToString();
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
                    uint dsb = BitConverter.ToUInt32(GetBytesFromFile(byteOffset, 4, filePath), 0);
                    byteOffset -= 68;
                    if (fileLength < byteOffset + 72 + (dsb * 1100) + 80) { return null; }
                    return BytesToBlock(GetBytesFromFile(byteOffset, 72 + (dsb * 1100) + 80, filePath));
                }
                byteOffset += 68;
                uint ds = BitConverter.ToUInt32(GetBytesFromFile(byteOffset, 4, filePath), 0);
                byteOffset -= 68;
                byteOffset += 72 + (ds * 1100) + 80;
                if (fileLength < byteOffset + 72) { return null; }
            }

        }

        // Get Blockchain File information

        public static Tuple<uint[], string> GetBlockPointerAtIndex(uint pointer) //< will return a Tuple  
        {

            string[] files = Directory.GetFiles(_folderPath + "blockchain");
            List<uint> flist = new List<uint>();
            foreach (string s in files) { flist.Add(Convert.ToUInt32(Path.GetFileName(s))); }
            flist.Sort();

            string filePath = "";
            foreach (uint a in flist)
            {
                uint lastIndex = RequestLatestBlockIndexInFile(_folderPath + "blockchain/" + a.ToString());
                if (lastIndex >= pointer)
                {
                    filePath = _folderPath + "blockchain/" + a.ToString();
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
                    uint dsb = BitConverter.ToUInt32(GetBytesFromFile(byteOffset, 4, filePath), 0);
                    byteOffset -= 68;
                    if (fileLength < byteOffset + 72 + (dsb * 1100) + 80) { return null; }

                    return new Tuple<uint[], string>(new uint[2] { byteOffset, byteOffset + 72 + (dsb * 1100) + 80 }, filePath);

                }
                byteOffset += 68;
                uint ds = BitConverter.ToUInt32(GetBytesFromFile(byteOffset, 4, filePath), 0);
                byteOffset -= 68;
                byteOffset += 72 + (ds * 1100) + 80;
                if (fileLength < byteOffset + 72) { return null; }
            }

        }
        public static string GetLatestBlockChainFilePath() //<---- return latest official blockchain file
        {

            string[] files = Directory.GetFiles(_folderPath + "blockchain");
            List<uint> flist = new List<uint>();
            foreach (string s in files) { flist.Add(Convert.ToUInt32(Path.GetFileName(s))); }
            flist.Sort();
            return _folderPath + "blockchain/" + flist[flist.Count - 1].ToString();
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
                uint lastIndex = RequestLatestBlockIndexInFile(_folderPath + "blockchain/" + a.ToString());
                if (lastIndex >= pointer)
                {
                    filePath = _folderPath + "blockchain/" + a.ToString();
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
                if (new FileInfo(s).Length < 4) { Print("file wrong format"); return uint.MaxValue; }
                return BitConverter.ToUInt32(GetBytesFromFile(0, 4, s), 0);
            }
            else
            {
                string[] files = Directory.GetFiles(_folderPath + "fork");
                string sp = GetLatestBlockChainFilePath();
                if (new FileInfo(sp).Length < 4) { Print("file wrong format"); return uint.MaxValue; }
                uint highest_BlockIndex = BitConverter.ToUInt32(GetBytesFromFile(0, 4, sp), 0); //< create an infinite loop .... 
                foreach (string s in files)
                {
                    if (new FileInfo(s).Length < 4) { Print("file wrong format"); return uint.MaxValue; }
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
            if (new FileInfo(_filePath).Length < 4) { Print("file wrong format"); return uint.MaxValue; }
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
                if (new FileInfo(s).Length < 4) { Print("file wrong format"); return ""; }
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
            return _folderPath + "fork/" + files.Length.ToString();
        }
        public static string GetNewBlockChainFilePath()
        {
            string[] files = Directory.GetFiles(_folderPath + "blockchain");
            return _folderPath + "blockchain/" + files.Length.ToString();
        }
    }
}
