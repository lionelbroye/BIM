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
        public static uint BLOCKCHAIN_FILE_CHUNK = 10000000;
        public static string _folderPath = "";
       
        // Files Initialisation and Verification Methods 

        public static void CheckFilesAtRoot() // check if file are in folder to process
        {
            _folderPath = AppDomain.CurrentDomain.BaseDirectory;
            Print(_folderPath);

            if (!File.Exists(_folderPath + "genesis"))
            {
                CreateGenesis();
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
            if (!Directory.Exists(_folderPath + "blockchain"))
            {
                Directory.CreateDirectory(_folderPath + "blockchain");
                File.WriteAllBytes(_folderPath + "blockchain/0", new byte[4]);
                AppendBytesToFile(_folderPath + "blockchain/0", File.ReadAllBytes(_folderPath + "genesis"));
                File.WriteAllBytes(_folderPath + "blockchain/1", new byte[4]);
            }
            if (!File.Exists(_folderPath + "utxos"))
            {
                File.WriteAllBytes(_folderPath + "utxos", new byte[4]);
            }
            if (!File.Exists(_folderPath + "ptx"))
            {
                File.Create(_folderPath + "ptx");
            }
            if (!Directory.Exists(_folderPath + "fork"))
            {
                Directory.CreateDirectory(_folderPath + "fork");
            }

            CURRENT_UTXO_SIZE = (uint)new FileInfo(_folderPath + "utxos").Length;
            UpdateHashTarget();
        }
        public static void CreateGenesis()
        {
            byte[] gen = Convert.FromBase64String("im a a genesis block");
            for (int i = 0; i < 10; i++)
            {
                gen = ComputeSHA256(gen);
            }
            BigInteger b1 = BytesToUint256(MAXIMUM_TARGET);
            byte[] firsttarget = Uint256ToByteArray(b1); //< firsttarget is correctly write! 
            Block Genesis = new Block(0, gen, gen, new List<Tx>(), FIRST_UNIX_TIMESTAMP, new MinerToken(new byte[32], 0, 0), firsttarget, 0);

            byte[] bytes = BlockToBytes(Genesis);
            File.WriteAllBytes(_folderPath + "genesis", bytes);
        }
        public static void ClearAllFiles() // CLEAR ALL FILES ( GENESIS, UTXO SET, PENDING TRANSACTION, FORKS, AND BLOCKCHAIN FILES. 
        {
            _folderPath = AppDomain.CurrentDomain.BaseDirectory;
            if (File.Exists(_folderPath + "genesis"))
            {
                File.Delete(_folderPath + "genesis");
            }
            if (File.Exists(_folderPath + "ptx"))
            {
                File.Delete(_folderPath + "ptx");
            }
            if (File.Exists(_folderPath + "utxos"))
            {
                File.Delete(_folderPath + "utxos");
            }
            if (Directory.Exists(_folderPath + "fork"))
            {
                Directory.Delete(_folderPath + "fork", true);
            }
            if (Directory.Exists(_folderPath + "blockchain"))
            {
                Directory.Delete(_folderPath + "blockchain", true);
            }
            Print("All files have been cleared.");
        }
        public static void VerifyFiles()
        {
            // we first verify every blockchain file
            string[] files = Directory.GetFiles(_folderPath + "blockchain");
            foreach (string s in files)
            {
                if (!isHeaderCorrectInBlockFile(s))
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
            uint fLenght = (uint)new FileInfo(_folderPath + "utxos").Length;
            if (fLenght < 4) { Print("utxo set file corrupted"); }
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
        }
        public static bool isHeaderCorrectInBlockFile(string _filePath) //< verify header correctness of a block file
        {
            uint latestIndex = RequestLatestBlockIndexInFile(_filePath);
            Block b = GetBlockAtIndexInFile(latestIndex, _filePath);
            if (b == null) { Print("no block at index specify by header :" + latestIndex); return false; }
            return true;
        }

        // File writing and reading Methods

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
                }
            }
        }
        public static void AppendBytesToFile(string _filePath, byte[] bytes)
        {

            using (FileStream f = new FileStream(_filePath, FileMode.Append))
            {
                f.Write(bytes, 0, bytes.Length);
            }

        }
        public static void TruncateFile(string _filePath, uint length) // can result an error. cant use file get length. 
        {
            FileInfo fi = new FileInfo(_filePath);
            FileStream fs = new FileStream(_filePath, FileMode.Open);

            fs.SetLength(fi.Length - length);
            fs.Close();
        }

        // Fatal error when writing file Handler

        public static void FatalErrorHandler(uint err, string msg = "")
        {

            if (ValidYesOrNo("[FATAL ERROR] Should reinit chain and close app : " + msg)) { ClearAllFiles(); Environment.Exit(0); }

        }
    }
}