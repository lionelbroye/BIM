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
        // Object used in our blockchain

        public class Block
        {
            public uint Index { get; } // 4 o
            public byte[] Hash { get; } // 32 o
            public byte[] previousHash { get; } // 32 o
            public uint DataSize { get; } // 4 o
            public List<Tx> Data { get; } // 1100 o * datasize
            public uint TimeStamp { get; } // 4 o
            public MinerToken minerToken { get; } // 40 o
            public byte[] HashTarget { get; } // 32 o
            public uint Nonce { get; } // 4 o 

            public Block(uint index, byte[] hash, byte[] ph, List<Tx> data, uint ts, MinerToken mt, byte[] hashtarget, uint nonce)
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
        public class Tx
        {
            public byte[] sPKey { get; }
            public List<byte> Symbols = new List<byte>();

            public uint Amount { get; }
            public byte[] rHashKey { get; }
            public uint LockTime { get; }
            public uint sUTXOP { get; }
            public uint rUTXOP { get; }
            public uint TokenOfUniqueness { get; }
            public uint TxFee { get; }
            public byte[] Signature { get; }


            public Tx(byte[] spk, uint amount, byte[] rpk, uint locktime, uint spkP, uint rpkP, uint TOU, uint Fee, byte[] sign)
            {
                this.sPKey = spk;
                this.Amount = amount;
                this.rHashKey = rpk;
                this.LockTime = locktime;
                this.sUTXOP = spkP;
                this.rUTXOP = rpkP;
                this.TokenOfUniqueness = TOU;
                this.TxFee = Fee;
                this.Signature = sign;
            }
        }
        public class UTXO
        {
            public byte[] HashKey { get; }
            public uint TokenOfUniqueness { get; }
            public uint Sold { get; }

            public UTXO(byte[] pKey, uint sold, uint TOU)
            {
                this.HashKey = pKey;
                this.Sold = sold;
                this.TokenOfUniqueness = TOU;
            }

        }
        public class MinerToken
        {
            public byte[] MinerPKEY { get; }
            public uint mUTXOP { get; }
            public uint MiningReward { get; }

            public MinerToken(byte[] hashpkey, uint utxoP, uint reward)
            {
                this.MinerPKEY = hashpkey;
                this.mUTXOP = utxoP;
                this.MiningReward = reward;
            }
        }
    }
}
