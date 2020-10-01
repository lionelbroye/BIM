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
        // Bytes manipulation for array of bytes building

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

        // unsigned 256 bit Integer constructor and bytes manipulation

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
            if (bytes.Length != 32) { Print("bytes to uint256 wrong format!"); return new BigInteger(0); }
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
                Print("uint 256 wrong format.");
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

        // All bytes manipulation for all Main Object in the blockchain program ( see ObjectStructure.cs )

        public static UTXO BytesToUTXO(byte[] bytes) // CAN RESULT NULL.
        {
            if (bytes.Length != 40) { return null; }

            uint byteOffset = 0;
            byte[] pKey = new byte[32];
            byte[] sold = new byte[4];
            byte[] TOU = new byte[4];
            for (uint i = byteOffset; i < byteOffset + 32; i++)
            {
                pKey[i - byteOffset] = bytes[i];
            }
            byteOffset += 32;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                sold[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                TOU[i - byteOffset] = bytes[i];
            }

            return new UTXO(pKey, BitConverter.ToUInt32(sold, 0), BitConverter.ToUInt32(TOU, 0));
        }
        public static byte[] UTXOToBytes(UTXO utxo)
        {
            List<byte> Databuilder = new List<byte>();
            Databuilder = AddBytesToList(Databuilder, utxo.HashKey);
            Databuilder = AddBytesToList(Databuilder, BitConverter.GetBytes(utxo.Sold));
            Databuilder = AddBytesToList(Databuilder, BitConverter.GetBytes(utxo.TokenOfUniqueness));
            return ListToByteArray(Databuilder);
        }
        public static Tx BytesToTx(byte[] bytes) // CAN RESULT NULL
        {
            if (bytes.Length != 1100) { return null; }


            uint byteOffset = 0;
            byte[] sPkey = new byte[532];
            byte[] Amount = new byte[4];
            byte[] rHashKey = new byte[32];
            byte[] LockTime = new byte[4];
            byte[] sUTXOP = new byte[4];
            byte[] rUTXOP = new byte[4];
            byte[] TOU = new byte[4];
            byte[] Fee = new byte[4];
            byte[] Sign = new byte[512];

            for (uint i = byteOffset; i < byteOffset + 532; i++)
            {
                sPkey[i - byteOffset] = bytes[i];
            }
            byteOffset += 532;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                Amount[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 32; i++)
            {
                rHashKey[i - byteOffset] = bytes[i];
            }
            byteOffset += 32;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                LockTime[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                sUTXOP[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                rUTXOP[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                TOU[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                Fee[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 512; i++)
            {
                Sign[i - byteOffset] = bytes[i];
            }

            return new Tx(sPkey, BitConverter.ToUInt32(Amount, 0), rHashKey, BitConverter.ToUInt32(LockTime, 0), BitConverter.ToUInt32(sUTXOP, 0),
                BitConverter.ToUInt32(rUTXOP, 0), BitConverter.ToUInt32(TOU, 0), BitConverter.ToUInt32(Fee, 0), Sign);

        }
        public static byte[] TxToBytes(Tx trans)
        {

            List<byte> Databuilder = new List<byte>();
            Databuilder = AddBytesToList(Databuilder, trans.sPKey);
            Databuilder = AddBytesToList(Databuilder, BitConverter.GetBytes(trans.Amount));
            Databuilder = AddBytesToList(Databuilder, trans.rHashKey);
            Databuilder = AddBytesToList(Databuilder, BitConverter.GetBytes(trans.LockTime));
            Databuilder = AddBytesToList(Databuilder, BitConverter.GetBytes(trans.sUTXOP));
            Databuilder = AddBytesToList(Databuilder, BitConverter.GetBytes(trans.rUTXOP));
            Databuilder = AddBytesToList(Databuilder, BitConverter.GetBytes(trans.TokenOfUniqueness));
            Databuilder = AddBytesToList(Databuilder, BitConverter.GetBytes(trans.TxFee));
            Databuilder = AddBytesToList(Databuilder, trans.Signature);
            return ListToByteArray(Databuilder);
        }
        public static MinerToken BytesToMinerToken(byte[] bytes) // CAN RESULT NULL
        {
            if (bytes.Length != 40) { return null; }
            byte[] hashKey = new byte[32];
            byte[] utxoP = new byte[4];
            byte[] reward = new byte[4];
            uint byteOffset = 0;
            for (uint i = byteOffset; i < byteOffset + 32; i++)
            {
                hashKey[i - byteOffset] = bytes[i];
            }
            byteOffset += 32;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                utxoP[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                reward[i - byteOffset] = bytes[i];
            }
            return new MinerToken(hashKey, BitConverter.ToUInt32(utxoP, 0), BitConverter.ToUInt32(reward, 0));
        }
        public static byte[] MinerTokenToBytes(MinerToken mt)
        {
            List<byte> Databuilder = new List<byte>();
            Databuilder = AddBytesToList(Databuilder, mt.MinerPKEY);
            Databuilder = AddBytesToList(Databuilder, BitConverter.GetBytes(mt.mUTXOP));
            Databuilder = AddBytesToList(Databuilder, BitConverter.GetBytes(mt.MiningReward));
            return ListToByteArray(Databuilder);
        }
        public static Block BytesToBlock(byte[] bytes) // CAN RESULT NULL
        {
            if (bytes.Length < 152) { return null; }

            byte[] index = new byte[4];
            byte[] hash = new byte[32];
            byte[] phash = new byte[32];
            byte[] ds = new byte[4];
            List<Tx> dt = new List<Tx>();
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

            if (bytes.Length != 72 + (BitConverter.ToUInt32(ds, 0) * 1100) + 80) { return null; }
            for (uint i = 0; i < BitConverter.ToUInt32(ds, 0); i++)
            {
                byte[] txBytes = new byte[1100];
                for (uint n = byteOffset; n < byteOffset + 1100; n++)
                {
                    txBytes[n - byteOffset] = bytes[n]; //  out of range exception 
                }
                byteOffset += 1100;
                Tx TX = BytesToTx(txBytes);
                if (TX == null) { return null; }
                dt.Add(TX);
            }

            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                ts[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            byte[] mbytes = new byte[40];
            for (uint i = byteOffset; i < byteOffset + 40; i++)
            {
                mbytes[i - byteOffset] = bytes[i];
            }
            byteOffset += 40;
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
            foreach (Tx trans in b.Data) { DataBuilder = AddBytesToList(DataBuilder, TxToBytes(trans)); }
            DataBuilder = AddBytesToList(DataBuilder, BitConverter.GetBytes(b.TimeStamp));
            DataBuilder = AddBytesToList(DataBuilder, MinerTokenToBytes(b.minerToken));
            DataBuilder = AddBytesToList(DataBuilder, b.HashTarget);
            DataBuilder = AddBytesToList(DataBuilder, BitConverter.GetBytes(b.Nonce));

            return ListToByteArray(DataBuilder);
        }
    }
}
