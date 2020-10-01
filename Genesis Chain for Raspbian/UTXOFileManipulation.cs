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
        // This includes every methods for modifying UTXO file (chain state infos...) and transaction file

        public static uint CURRENT_UTXO_SIZE = 0;

        // General UTXO File downgrading or upgrading or building

        public static void UpgradeUTXOSet(Block b) //< Apply when changing Official Blockchain Only. OverWriting UTXO depend of previous transaction. Produce dust.
        {
            foreach (Tx TX in b.Data)
            {
                UTXO utxo = GetOfficialUTXOAtPointer(TX.sUTXOP);
                if (utxo == null) { FatalErrorHandler(0, "no utxo found during upgrade utxo set"); return; } // FATAL ERROR 
                utxo = UpdateVirtualUTXO(TX, utxo, false);
                OverWriteUTXOAtPointer(TX.sUTXOP, utxo);
                if (TX.rUTXOP != 0)
                {
                    utxo = GetOfficialUTXOAtPointer(TX.rUTXOP);
                    if (utxo == null) { FatalErrorHandler(0, "no utxo found during upgrade utxo set"); return; } // FATAL ERROR
                    utxo = UpdateVirtualUTXO(TX, utxo, false);
                    OverWriteUTXOAtPointer(TX.rUTXOP, utxo);
                }
                else
                {
                    utxo = new UTXO(TX.rHashKey, TX.Amount, 0);
                    AddDust(utxo);

                }
            }
            if (b.minerToken.mUTXOP != 0)
            {
                UTXO utxo = GetOfficialUTXOAtPointer(b.minerToken.mUTXOP);
                if (utxo == null) { FatalErrorHandler(0, "no utxo found during upgrade utxo set"); return; } // FATAL ERROR
                uint mSold = utxo.Sold + b.minerToken.MiningReward;
                uint mTOU = utxo.TokenOfUniqueness + 1;
                OverWriteUTXOAtPointer(b.minerToken.mUTXOP, new UTXO(b.minerToken.MinerPKEY, mSold, mTOU));
            }
            else
            {
                UTXO utxo = new UTXO(b.minerToken.MinerPKEY, b.minerToken.MiningReward, 0);
                AddDust(utxo);
            }
            // overwrite currency_volume header. 
            uint actual_volume = BitConverter.ToUInt32(GetBytesFromFile(0, 4, _folderPath + "utxos"), 0);
            actual_volume += GetMiningReward(b.Index);
            OverWriteBytesInFile(0, _folderPath + "utxos", BitConverter.GetBytes(actual_volume));
        }
        public static void DownGradeUTXOSet(uint index) //< Apply when changing Official Blockchain Only. OverWriting UTXO depend of previous transaction. Compute dust.
        {
            uint DustCount = 0;
            for (uint i = RequestLatestBlockIndex(true); i > index; i--)
            {
                if (i == uint.MaxValue) { break; }
                Block b = GetBlockAtIndex(i);
                if (b == null) { FatalErrorHandler(0, "no block found during downgrade utxo set"); return; } // FATAL ERROR
                if (b.minerToken.mUTXOP != 0)
                {
                    UTXO utxo = GetOfficialUTXOAtPointer(b.minerToken.mUTXOP);
                    if (utxo == null) { FatalErrorHandler(0, "no utxo found during downgrade utxo set"); return; } // FATAL ERROR
                    uint mSold = utxo.Sold - b.minerToken.MiningReward;
                    uint mTOU = utxo.TokenOfUniqueness - 1;
                    OverWriteUTXOAtPointer(b.minerToken.mUTXOP, new UTXO(b.minerToken.MinerPKEY, mSold, mTOU));
                }
                else
                {
                    DustCount++;
                }
                for (int a = b.Data.Count - 1; a >= 0; a--)
                {
                    if (a == uint.MaxValue) { break; }
                    Tx TX = b.Data[a];
                    UTXO utxo = GetOfficialUTXOAtPointer(TX.sUTXOP);
                    if (utxo == null) { FatalErrorHandler(0, "no utxo found during downgrade utxo set"); return; } // FATAL ERROR
                    utxo = UpdateVirtualUTXO(TX, utxo, true);
                    OverWriteUTXOAtPointer(TX.sUTXOP, utxo);
                    if (TX.rUTXOP != 0)
                    {
                        utxo = GetOfficialUTXOAtPointer(TX.rUTXOP);
                        if (utxo == null) { FatalErrorHandler(0, "no utxo found during downgrade utxo set"); return; } // FATAL ERROR
                        utxo = UpdateVirtualUTXO(TX, utxo, true);
                        OverWriteUTXOAtPointer(TX.rUTXOP, utxo);
                    }
                    else
                    {
                        DustCount++;
                    }
                }

            }
            if (!RemoveDust(DustCount)) { FatalErrorHandler(0, "bad dust removing during downgrade utxo set"); return; } // FATAL ERROR
            OverWriteBytesInFile(0, _folderPath + "utxos", BitConverter.GetBytes(GetCurrencyVolume(index)));
        }
        public static bool OverWriteUTXOAtPointer(uint pointer, UTXO towrite)
        { // CAN RETURN FALSE

            if (pointer < 4 || pointer > CURRENT_UTXO_SIZE - 40) { return false; }
            byte[] bytes = UTXOToBytes(towrite);
            OverWriteBytesInFile(pointer, _folderPath + "utxos", bytes);
            return true;
        }
        public static void AddDust(UTXO utxo)
        {

            using (FileStream f = new FileStream(_folderPath + "utxos", FileMode.Append))
            {
                byte[] bytes = UTXOToBytes(utxo);
                f.Write(bytes, 0, bytes.Length);
            }
            CURRENT_UTXO_SIZE += 40;
        }
        public static bool RemoveDust(uint nTime)
        { //< CAN RETURN FALSE

            uint DustsLength = nTime * 40;
            if (CURRENT_UTXO_SIZE < DustsLength + 4) { return false; }
            FileStream fs = new FileStream(_folderPath + "utxos", FileMode.Open);
            fs.SetLength(CURRENT_UTXO_SIZE - DustsLength);
            fs.Close();
            CURRENT_UTXO_SIZE -= DustsLength;
            return true;
        }
        public static void BuildUTXOSet()
        {
            string fPath = _folderPath + "utxos";
            File.WriteAllBytes(_folderPath + "utxos", new byte[4]);
            uint lastIndex = RequestLatestBlockIndex(true);
            for (uint i = 1; i < lastIndex + 1; i++)
            {
                Block b = GetBlockAtIndex(i);
                if (b == null) { FatalErrorHandler(0); return; } // FATAL ERROR
                UpgradeUTXOSet(b);
            }
        }

        // General TX File downgrading or upgrading

        public static void UpdatePendingTXFile(Block b) // delete all TX in pending if they are include in the block ( just with verifying pkey & tou)
        {
            uint byteOffset = 0;
            FileInfo f = new FileInfo(_folderPath + "ptx");
            int fl = (int)f.Length;
            while (byteOffset < fl)
            {
                Tx TX = BytesToTx(GetBytesFromFile(byteOffset, 1100, _folderPath + "ptx"));
                if (TX == null) { FatalErrorHandler(0, "no tx found in data during update pending tx file"); return; } // FATAL ERROR
                foreach (Tx BTX in b.Data)
                {
                    if (BTX == null) { FatalErrorHandler(0, "no tx found in builder during update pending tx file"); return; } // FATAL ERROR
                    if (TX.sPKey.SequenceEqual(BTX.sPKey) && TX.TokenOfUniqueness == BTX.TokenOfUniqueness)
                    {
                        // flip bytes then truncate
                        byte[] lastTX = GetBytesFromFile((uint)fl - 1100, 1100, _folderPath + "ptx");
                        OverWriteBytesInFile(byteOffset, _folderPath + "ptx", lastTX);
                        TruncateFile(_folderPath + "ptx", 1100);
                        f = new FileInfo(_folderPath + "ptx");
                        fl = (int)f.Length;
                        break;
                    }
                }

                byteOffset += 1100;

            }
        }
        public static void UpdatePendingTXFileB(string _filePath)
        {
            uint firstTempIndex = BitConverter.ToUInt32(GetBytesFromFile(4, 8, _filePath), 0);
            uint latestTempIndex = BitConverter.ToUInt32(GetBytesFromFile(0, 4, _filePath), 0);
            for (uint i = firstTempIndex; i < latestTempIndex + 1; i++)
            {
                Block b = GetBlockAtIndexInFile(i, _filePath);
                if (b == null) { return; }
                UpdatePendingTXFile(b);
            }
        }
        public static void CleanOldPendingTX(bool onlyForks)// remove out-to-date locktime TX in Forks + PTX file if needed. 
        {
            string[] forkfiles = Directory.GetFiles(_folderPath + "fork");
            List<string> forkdel = new List<string>();
            uint unixTimestamp = (uint)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            foreach (string s in forkfiles)
            {
                uint firstIndex = BitConverter.ToUInt32(GetBytesFromFile(4, 8, s), 0);
                uint latestIndex = BitConverter.ToUInt32(GetBytesFromFile(0, 4, s), 0);
                for (uint i = firstIndex; i < latestIndex + 1; i++)
                {
                    Block b = GetBlockAtIndexInFile(i, s);
                    if (b == null) { FatalErrorHandler(0, "no block found during cleaning old pending tx file"); return; } // FATAL ERROR
                    foreach (Tx TX in b.Data)
                    {
                        if (TX.LockTime < unixTimestamp && !forkdel.Contains(s))
                        {
                            forkdel.Add(s);
                        }
                    }
                }
            }
            foreach (string s in forkdel)
            {
                File.Delete(s);
            }
            if (!onlyForks)
            {
                uint byteOffset = 0;
                FileInfo f = new FileInfo(_folderPath + "ptx"); // clean out to date ptx and also if there are include
                int fl = (int)f.Length;
                while (byteOffset < fl)
                {
                    Tx TX = BytesToTx(GetBytesFromFile(byteOffset, 1100, _folderPath + "ptx"));
                    if (TX == null) { FatalErrorHandler(0, "no tx found in data during cleaning pending tx file"); return; } // FATAL ERROR
                    bool _del = false;
                    if (TX.LockTime < unixTimestamp) { _del = true; }
                    if (_del)
                    {
                        // flip bytes then truncate
                        byte[] lastTX = GetBytesFromFile((uint)fl - 1100, 1100, _folderPath + "ptx"); // FATAL ERROR
                        OverWriteBytesInFile(byteOffset, _folderPath + "ptx", lastTX);
                        TruncateFile(_folderPath + "ptx", 1100);
                        f = new FileInfo(_folderPath + "ptx");
                        fl = (int)f.Length;

                    }

                    byteOffset += 1100;

                }
            }
        }

        // Get UTXO in file

        public static uint GetUTXOPointer(byte[] pKey) // return a pointer from the SHA256 pKey UTXO in the UTXO Set. They are
        {
            uint byteOffset = 4;
            while (true)
            {
                if (byteOffset >= CURRENT_UTXO_SIZE) { return 0; }
                UTXO utxo = BytesToUTXO(GetBytesFromFile(byteOffset, 40, _folderPath + "utxos"));
                if (utxo.HashKey.SequenceEqual(pKey))
                {
                    return byteOffset;
                }
                byteOffset += 40;
            }

        }
        public static UTXO GetOfficialUTXOAtPointer(uint pointer) // CAN RETURN NULL
        {
            if (pointer > CURRENT_UTXO_SIZE - 40 || pointer < 4) { return null; }
            return BytesToUTXO(GetBytesFromFile(pointer, 40, _folderPath + "utxos"));

        }

        // Transaction Creation Methods

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
            if (sutxop == 0) { Print("bad pointer"); return; }
            UTXO utxo = GetOfficialUTXOAtPointer(sutxop);
            if (utxo == null) { Print("bad pointer"); return; }
            if (!utxo.HashKey.SequenceEqual(ComputeSHA256(_MyPublicKey))) { Print("bad pointer"); return; }
            uint newtou = utxo.TokenOfUniqueness + 1;
            byte[] TOU = BitConverter.GetBytes(newtou);
            bool needDust = false;
            if (rutxop != 0)
            {
                UTXO oUTXO = GetOfficialUTXOAtPointer(rutxop);
                if (oUTXO == null) { Print("bad pointer"); return; }
            }
            else { needDust = true; }
            if (GetFee(utxo, needDust) > fee)
            {
                Print("insuffisiant fee");
                return;
            }
            if (fee + amount > utxo.Sold) { Print("insuffisiant sold"); return; }
            //           if ( TX.TxFee < GetFee(sUTXO, dustNeeded)) { Print("Invalid Fee"); return false;  }
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
                /*
                public Tx (byte[] spk, uint amount, byte[] rpk, uint locktime, uint spkP, uint rpkP, uint TOU, uint Fee, byte[] sign )
             */
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
                Print("Tx" + BitConverter.ToUInt32(TOU, 0) + "file was successfully generated at roots. Signature :" + SHAToHex(Signature, false));
                _MyPrRsa.Clear();
                return;
            }
            catch (CryptographicException e)
            {
                Print(e.Message);
                _MyPrRsa.Clear();
                return;
            }

        }
    }
}
