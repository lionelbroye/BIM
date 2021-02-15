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
        // Some General Functions for Blockchain Explorer.... 

        public static uint GetNumberOfBlocksMinedSinceLastLowTide()
        {
            List<Tuple<float, Block>> r = GetBlocksMinedSinceNumberOfTides(1);
            if (r == null)
                return 0;
            return (uint)r.Count; 
        }

        // start time and range ?
        public static List<Tuple<float, uint>> GetTidalValuesInRangeOfTides(uint _tidesNumber = 1, uint _startTime = 0, uint _secondOffset = 0 )
        {
            // the format is highest tidal timestamp -> lowest tidal timestamp ... -> repeat etc. 
            
            if (_startTime == 0)
                _startTime = (uint)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            if (_tidesNumber < 1)
                _tidesNumber = 1;

            List<Tuple<float, uint>> result = new List<Tuple<float, uint>>();
            
            uint unixTimestamp = _startTime + _secondOffset;
            uint startTimeStamp = unixTimestamp;

            uint tideCounter = 0;
            float lastwater_level = 0;
            int movement = 0;

            result.Add(new Tuple<float, uint>(Tidal.GetTidalAtSpecificTime(unixTimestamp), unixTimestamp));

            for (uint i = 0; i < 500; i++) // la ya 8 heure 
            {
                float water_level = Tidal.GetTidalAtSpecificTime(unixTimestamp);
                
                if (lastwater_level < water_level)
                { 
                    if (movement == -1) // now sea level will start to increment
                    {
                        tideCounter++;
                        result.Add(new Tuple<float, uint>(Tidal.GetTidalAtSpecificTime(unixTimestamp + 300), unixTimestamp + 300));
                    }
                        
                    if (tideCounter == _tidesNumber)
                    {
                         break;
                    }
                    movement = 1;
                }
                
                if (lastwater_level > water_level)
                {
                    if (movement == 1)
                    {
                        // now sea level will start to decrement
                        // so this is a high peak 
                        result.Add( new Tuple<float, uint>( Tidal.GetTidalAtSpecificTime(unixTimestamp + 300), unixTimestamp + 300));
                    }
                    movement = -1;
                }
                    

                lastwater_level = water_level;
                unixTimestamp -= 300; // get the water every five minutes 
            }
            Console.WriteLine(tideCounter);
            return result; 
        }
        // Get Every Block mine since a tides and range times 
        public static List<Tuple<float, Block>> GetBlocksMinedSinceNumberOfTides(uint _tidesNumber = 1,  uint _startTime = 0, uint _secondOffset  = 0 )
        {

            List<Tuple<float, Block>> result = new List<Tuple<float, Block>>();
            if (_startTime == 0)
                _startTime = (uint)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            if (_tidesNumber < 1)
                _tidesNumber = 1;
            

            uint unixTimestamp = _startTime + _secondOffset;
            uint startTimeStamp = unixTimestamp;
            uint endTimestamp = 0;

            uint tideCounter = 0;
            float lastwater_level = 0;
            int movement = 0;
            

            for (uint i = 0; i < 500; i++) // la ya 8 heure 
            {
                float water_level = Tidal.GetTidalAtSpecificTime(unixTimestamp);

                if (lastwater_level < water_level)
                {
                    if (movement == -1) // now sea level will start to increment
                    {
                        tideCounter++;
                    }

                    if (tideCounter == _tidesNumber)
                    {
                        endTimestamp = unixTimestamp; break;
                    }
                    movement = 1;
                }

                if (lastwater_level > water_level)
                {
                  
                    movement = -1;
                }


                lastwater_level = water_level;
                unixTimestamp -= 300; // get the water every five minutes 
            }

            Block latestBlock = GetBlockAtIndex(RequestLatestBlockIndex(true));
            if (latestBlock.TimeStamp < endTimestamp)
                return null;

            result.Add(new Tuple<float, Block>( Tidal.GetTidalAtSpecificTime(latestBlock.TimeStamp), latestBlock )); 

            for (uint i = latestBlock.Index; i >= 2; i --)
            {
                Block b = GetBlockAtIndex(i);
                if ( b.TimeStamp < endTimestamp) { break;  }
                else
                {
                    result.Add(new Tuple<float, Block>(Tidal.GetTidalAtSpecificTime(b.TimeStamp), b));
                }

            }
            return result;
           
        }

        // Get Blocks in file

        public static Block GetBlockAtIndexInFile(uint pointer, string filePath) // Return a null Block if CANT BE BE FOUND
        {
            if ( arduino.HEADPOSITION > pointer)
            {
                for (uint i = 0; i < arduino.HEADPOSITION - pointer; i++)
                {
                    arduino.SendTick("1"); // go left for horloge 1
                }
            }
            else
            {
                for (uint i = 0; i < pointer - arduino.HEADPOSITION ; i++)
                {
                    arduino.SendTick("2");  // go right for horloge 2
                }

            }
            arduino.HEADPOSITION = pointer;
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
            if (arduino.HEADPOSITION > pointer)
            {
                for (uint i = 0; i < arduino.HEADPOSITION - pointer; i++)
                {
                    arduino.SendTick("3");
                }
            }
            else
            {
                for (uint i = 0; i < pointer - arduino.HEADPOSITION; i++)
                {
                    arduino.SendTick("2");
                }

            }
            arduino.HEADPOSITION = pointer;
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
