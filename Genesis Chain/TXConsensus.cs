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
        // consensus methods for validating a transaction

        public static void ProccessTempTXforPending(string _filePath, bool needPropagate)
        {
            // we first check if length can be divide by 1100 .. 
            FileInfo f = new FileInfo(_filePath);
            int fl = (int)f.Length;
            if (fl % 1100 != 0 || fl < 1100) { File.Delete(_filePath); return; }
            // can be very large ... so we have have to chunk every txs ... into split part of max 500 tx 4 the RAM alloc
            uint chunkcounter = 0;
            uint byteOffset = 0;

            // we only accept pending tx that avec 

            List<Tx> txs = new List<Tx>();
            while (byteOffset < fl)
            {
                Tx Trans = BytesToTx(GetBytesFromFile(byteOffset, 1100, _filePath));
                if (Trans == null) { File.Delete(_filePath); return; }
                txs.Add(Trans);
                byteOffset += 1100;
                chunkcounter++;
                if (chunkcounter > 500 || byteOffset == fl)
                {
                    chunkcounter = 0;
                    foreach (Tx TX in txs)
                    {
                        // we should do some virtualisation here ... ...
                        if (isTxValidforPending(TX, GetOfficialUTXOAtPointer(TX.sUTXOP)))
                        {
                            AppendBytesToFile(_folderPath + "ptx", TxToBytes(TX));
                            // NT.SendFile(_folderPath + "ptx", 2); //< Send our PTX File
                        }

                    }

                    txs = new List<Tx>();

                }
            }
            if (needPropagate)
            {
                BroadcastQueue.Add(new BroadcastInfo(1, 2, _folderPath + "ptx"));
            }
            File.Delete(_filePath);
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
        public static bool isTxValidforPending(Tx TX, UTXO sUTXO) //< this only verify tx validity with current official utxo set. ( need to verify validity for 
        {
            bool dustNeeded = false;
            if (!VerifyTransactionDataSignature(TxToBytes(TX))) { Print("Invalid Signature"); return false; }
            if (sUTXO == null) { Print("Invalid UTXO POINTER : utxo not found " + TX.sUTXOP); return false; }
            if (!ComputeSHA256(TX.sPKey).SequenceEqual(sUTXO.HashKey)) { Print("Invalid UTXO POINTER"); return false; }
            if (TX.rUTXOP >= 4)
            {
                UTXO rUTXO = GetOfficialUTXOAtPointer(TX.rUTXOP);
                if (rUTXO == null) { Print("Invalid UTXO POINTER : " + TX.rUTXOP); return false; }
                if (!TX.rHashKey.SequenceEqual(rUTXO.HashKey)) { Print("Invalid UTXO POINTER" + +TX.rUTXOP); return false; }
            }
            else { dustNeeded = true; }
            if (TX.TokenOfUniqueness != sUTXO.TokenOfUniqueness + 1) { Print("Invalid Token"); return false; }  //< this should be compared to a virtual UTXO
            if (TX.TxFee < GetFee(sUTXO, dustNeeded)) { Print("Invalid Fee"); return false; }
            uint unixTimestamp = (uint)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            if (TX.LockTime < unixTimestamp) { Print("Invalid Timestamp"); return false; } //< should Be COMPARED TO UNIX TIME! 
            Int64 sold64 = Convert.ToInt64(sUTXO.Sold);
            uint sum = TX.Amount + TX.TxFee;
            Int64 sum64 = Convert.ToInt64(sum);
            if (sold64 - sum64 < 0) { return false; }
            return true;
        }
        public static bool VerifyTransactionDataSignature(byte[] dataBytes)
        {
            if (dataBytes.Length != 1100) { return false; }

            byte[] mPKey = new byte[532];
            byte[] msg = new byte[588];
            byte[] signature = new byte[512];
            for (int i = 0; i < 532; i++) { mPKey[i] = dataBytes[i]; }
            for (int i = 0; i < 588; i++) { msg[i] = dataBytes[i]; }
            for (int i = 588; i < 1100; i++) { signature[i - 588] = dataBytes[i]; }

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
                Print(e.Message);
                _MyPuRsa.Clear();
                return false;
            }
        }
        public static uint GetFee(UTXO utxo, bool needDust)
        {
            uint fee = 2;
            if (needDust) { fee += 5; }
            // we should aso use utxo Token of uniqueness and sold to compute the fee. also maybe currency volume
            return fee;
        }
        
        // virtualisation of UTXO 's methods when proccessing multiple transactions

        public static UTXO GetDownGradedVirtualUTXO(uint index, UTXO utxo) //< get a virtual instance of a specific UTXO at specific time of the official chain
        {
            for (uint i = RequestLatestBlockIndex(false); i >= index; i--)
            {
                if (i == uint.MaxValue) { break; }
                Block b = GetBlockAtIndex(i);
                if (b == null) { Print("[missing block] Downgrade UTXO aborted "); return null; }
                utxo = UpdateVirtualUTXOWithFullBlock(b, utxo, true);
            }
            return utxo;
        }
        public static UTXO UpdateVirtualUTXOWithFullBlock(Block b, UTXO utxo, bool reverse) //< get a virtual instance of a specific UTXO updated from a block
        {
            uint nSold = utxo.Sold;
            uint nToken = utxo.TokenOfUniqueness;
            if (!reverse)
            {
                foreach (Tx TX in b.Data)
                {
                    if (ComputeSHA256(TX.sPKey).SequenceEqual(utxo.HashKey))
                    {
                        nSold -= TX.Amount + TX.TxFee;
                        nToken = TX.TokenOfUniqueness;
                    }
                    if (TX.rHashKey.SequenceEqual(utxo.HashKey))
                    {
                        nSold += TX.Amount;
                    }
                }
                if (b.minerToken.mUTXOP != 0)
                {
                    UTXO mUTXO = GetOfficialUTXOAtPointer(b.minerToken.mUTXOP);
                    if (mUTXO != null)
                    {
                        if (utxo.HashKey.SequenceEqual(mUTXO.HashKey))
                        {
                            nSold += b.minerToken.MiningReward;
                        }
                    }

                }

            }
            else
            {
                if (b.minerToken.mUTXOP != 0)
                {
                    UTXO mUTXO = GetOfficialUTXOAtPointer(b.minerToken.mUTXOP);
                    if (mUTXO != null)
                    {
                        if (utxo.HashKey.SequenceEqual(mUTXO.HashKey))
                        {
                            nSold -= b.minerToken.MiningReward;
                        }
                    }
                }

                for (int i = b.Data.Count - 1; i >= 0; i--)
                {
                    if (i == uint.MaxValue) { break; }
                    Tx TX = b.Data[i];
                    if (ComputeSHA256(TX.sPKey).SequenceEqual(utxo.HashKey))
                    {

                        nSold += TX.Amount + TX.TxFee;
                        nToken = TX.TokenOfUniqueness;
                    }
                    if (TX.rHashKey.SequenceEqual(utxo.HashKey))
                    {

                        nSold -= TX.Amount;

                    }
                }
            }

            return new UTXO(utxo.HashKey, nSold, nToken);

        }
        public static UTXO UpdateVirtualUTXO(Tx TX, UTXO utxo, bool reverse) //< get a virtual instance of a specific UTXO updated from a an unique TX
        {
            uint nSold = utxo.Sold;
            uint nToken = utxo.TokenOfUniqueness;
            if (ComputeSHA256(TX.sPKey).SequenceEqual(utxo.HashKey))
            {
                if (!reverse)
                {
                    nSold -= TX.Amount + TX.TxFee;
                    nToken = TX.TokenOfUniqueness;
                }
                else
                {
                    nSold += TX.Amount + TX.TxFee;
                    nToken = TX.TokenOfUniqueness;
                }
            }
            if (TX.rHashKey.SequenceEqual(utxo.HashKey))
            {
                if (!reverse)
                {
                    nSold += TX.Amount;
                }
                else
                {
                    nSold -= TX.Amount;
                }

            }
            return new UTXO(utxo.HashKey, nSold, nToken);

        }

    }
}
