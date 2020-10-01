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
        public static byte[] CURRENT_HASH_TARGET;

        // Main methods for blocks validation

        public static void ProccessTempBlocks(string _filePath, bool needPropagate) // MAIN FUNCTION TO VALID BLOCK
        {
            Print("STARTING PROCESSING BLOCKS FOR " + _filePath);
            FileInfo f = new FileInfo(_filePath);
            if (f.Length < 8) { Console.WriteLine("[BLOCKS REFUSED] bad file length!"); File.Delete(_filePath); return; }
            if (!isHeaderCorrectInBlockFile(_filePath)) { Console.WriteLine("[BLOCKS REFUSED] header incorrect!"); File.Delete(_filePath); return; }

            uint firstTempIndex = BitConverter.ToUInt32(GetBytesFromFile(4, 8, _filePath), 0);
            uint latestTempIndex = BitConverter.ToUInt32(GetBytesFromFile(0, 4, _filePath), 0);

            Print(firstTempIndex + " " + latestTempIndex);
            uint latestOfficialIndex = RequestLatestBlockIndex(true);

            bool HardFork = false;
            Print(((int)(latestOfficialIndex - MAX_RETROGRADE)).ToString());
            if ((int)firstTempIndex <= (int)(latestOfficialIndex - MAX_RETROGRADE)) // create a uint max value error ! 
            {
                if (firstTempIndex == 0) { Console.WriteLine("[BLOCKS REFUSED] no genesis allowed!"); File.Delete(_filePath); return; }
                if (latestTempIndex < latestOfficialIndex + WINNING_RUN_DISTANCE) { Console.WriteLine("[BLOCKS REFUSED] Not Winning dist!"); File.Delete(_filePath); return; }
                HardFork = true;
            }
            else
            {
                uint latestIndex = RequestLatestBlockIndex(false);
                if (firstTempIndex > latestIndex + 1) { Console.WriteLine("[BLOCKS REFUSED] Can't proccess blocks. "); File.Delete(_filePath); return; }
                // we check if we have a fork that contains specific index to mesure
                if (latestTempIndex < latestOfficialIndex + 1) { Console.WriteLine("[BLOCKS REFUSED] Can't proccess blocks. "); File.Delete(_filePath); return; }
            }

            //--------- get the shom data during validation process ... 

            //----- we should load the shit . but like every 30 blocs or something (depending of distance in time between those blocks )
            Block bfirst = GetBlockAtIndexInFile(firstTempIndex, _filePath);
            Block blast = GetBlockAtIndexInFile(latestTempIndex, _filePath);
            if (bfirst == null || blast == null) { Console.WriteLine("header incorrect!"); File.Delete(_filePath); return; }
            List<SHOM.SHOMData> shoms = SHOM.GetShomDatafromPeriod(ACTUAL_PORT, bfirst.TimeStamp, blast.TimeStamp);
            //-----------

            if (HardFork)
            {
                Print("hard forking");
                Block currentBlockReading = GetBlockAtIndexInFile(firstTempIndex, _filePath);
                Block previousBlock = GetBlockAtIndex(firstTempIndex - 1);
                if (currentBlockReading == null || previousBlock == null) { Console.WriteLine("[BLOCKS REFUSED] block null "); File.Delete(_filePath); return; }
                List<uint> timestamps = new List<uint>();
                uint TC = 0;
                for (uint i = firstTempIndex - 1; i >= 0; i--)
                {
                    if (i == uint.MaxValue) { break; }
                    Block b = GetBlockAtIndex(i);
                    if (b == null) { Console.WriteLine("[BLOCKS REFUSED] block null "); File.Delete(_filePath); return; }
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
                        if (earlierBlock == null) { Console.WriteLine("[BLOCKS REFUSED] block null "); File.Delete(_filePath); return; }
                    }
                    else
                    {
                        earlierBlock = GetBlockAtIndex(previousBlock.Index + 1 - TARGET_CLOCK);//  need also an update
                        if (earlierBlock == null) { Console.WriteLine("[BLOCKS REFUSED] block null "); File.Delete(_filePath); return; }

                    }
                    tempTarget = ComputeHashTargetB(previousBlock, earlierBlock);
                }
                else
                {
                    tempTarget = previousBlock.HashTarget;
                }
                List<UTXO> vUTXO = new List<UTXO>(); //< we will need temp file to avoid stackoverflow ( dont load this in RAM! )
                while (true)
                {
                    // APPLY THE SEA
                    SHOM.SHOMData shom = SHOM.GetLastDataBeforeSpecificTime(SHOM.UnixTimeStampToDateTime(currentBlockReading.TimeStamp), shoms);
                    if (shom == null)
                    {
                        Console.WriteLine("No SHOM found... "); File.Delete(_filePath);
                        return;
                    }

                    //----
                    byte[] reqtarget = ApplyTheSeaToTheCryptoPuzzle(tempTarget, shom);
                    Tuple<bool, List<UTXO>> bV = IsBlockValid(currentBlockReading, previousBlock, MINTIMESTAMP, tempTarget, reqtarget, vUTXO);

                    if (!bV.Item1)
                    {
                        File.Delete(_filePath);
                        Console.WriteLine("[BLOCKS REFUSED] block not valid ");
                        return;
                    }
                    vUTXO = bV.Item2;

                    if (currentBlockReading.Index == latestTempIndex)
                    {
                        DownGradeUTXOSet(firstTempIndex - 1);
                        DowngradeOfficialChain(firstTempIndex - 1);
                        AddBlocksToOfficialChain(_filePath, needPropagate);
                        return;
                    }
                    previousBlock = currentBlockReading; //*
                    currentBlockReading = GetBlockAtIndexInFile(currentBlockReading.Index + 1, _filePath); //* 
                    if (currentBlockReading == null) { Console.WriteLine("[BLOCKS REFUSED] block not valid "); File.Delete(_filePath); return; }

                    timestamps.RemoveAt(0);
                    timestamps.Add(previousBlock.TimeStamp);
                    MINTIMESTAMP = GetTimeStampRequirementB(timestamps);

                    if (isNewTargetRequired(currentBlockReading.Index))
                    {
                        Block earlierBlock;
                        if (previousBlock.Index + 1 <= TARGET_CLOCK)
                        {
                            earlierBlock = GetBlockAtIndex(0);
                            if (earlierBlock == null) { Console.WriteLine("[BLOCKS REFUSED] block not valid "); File.Delete(_filePath); return; }
                        }
                        else
                        {
                            if (previousBlock.Index + 1 - TARGET_CLOCK > latestOfficialIndex)
                            {
                                earlierBlock = GetBlockAtIndexInFile(previousBlock.Index + 1 - TARGET_CLOCK, _filePath);
                                if (earlierBlock == null) { Console.WriteLine("[BLOCKS REFUSED] block not valid "); File.Delete(_filePath); return; }
                            }
                            else
                            {
                                earlierBlock = GetBlockAtIndex(previousBlock.Index + 1 - TARGET_CLOCK);
                                if (earlierBlock == null) { Console.WriteLine("[BLOCKS REFUSED] block not valid "); File.Delete(_filePath); return; }
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
                if (currentBlockReading == null) { File.Delete(_filePath); Console.WriteLine("[BLOCKS REFUSED] block not valid "); return; }

                string[] forkfiles = Directory.GetFiles(_folderPath + "fork");
                uint latestIndex = RequestLatestBlockIndex(false);
                // 
                string _pathToGetPreviousBlock = "";
                if (firstTempIndex != latestOfficialIndex + 1) // searching A fork : if firsttempindex(1) is not latestofficialindex  (0) + 1
                {

                    Print("called");
                    latestIndex = RequestLatestBlockIndex(false);
                    if (latestIndex > firstTempIndex - 1)
                    {
                        _pathToGetPreviousBlock = GetIndexBlockChainFilePath(firstTempIndex - 1);
                        if (_pathToGetPreviousBlock == "")
                        {
                            File.Delete(_filePath); Console.WriteLine("[BLOCK REFUSED ]Can't find a fork to process those blocks. temp index : " + firstTempIndex); return;
                        }
                    }
                    else
                    {
                        // check if we can find a fork with this specific block file ... goes here when lightfork ... 
                        string forkpath = FindMatchingFork(currentBlockReading);
                        if (forkpath.Length == 0) { Console.WriteLine("[BLOCK REFUSED ]Can't find a fork to process those blocks. temp index : " + firstTempIndex); return; }
                        else { _pathToGetPreviousBlock = forkpath; }
                        Block bb = GetBlockAtIndexInFile(latestTempIndex, _filePath);
                        if (bb == null) { Console.WriteLine("[BLOCKS REFUSED] block not valid "); File.Delete(_filePath); return; }
                        if (isForkAlreadyExisting(bb)) { Console.WriteLine("[BLOCKS REFUSED] block already exist "); File.Delete(_filePath); return; }
                    }
                }
                else
                {
                    //_pathToGetPreviousBlock = GetLatestBlockChainFilePath(); // this is shit ... 
                    _pathToGetPreviousBlock = GetIndexBlockChainFilePath(firstTempIndex - 1);
                }
                Print(_pathToGetPreviousBlock);
                Block previousBlock = GetBlockAtIndexInFile(firstTempIndex - 1, _pathToGetPreviousBlock);

                if (previousBlock == null) { File.Delete(_filePath); Console.WriteLine("[BLOCKS REFUSED] block null "); return; }

                List<uint> timestamps = new List<uint>();
                uint TC = 0;
                for (uint i = firstTempIndex - 1; i >= 0; i--)
                {
                    if (i == uint.MaxValue) { break; }
                    if (i > latestOfficialIndex)
                    {
                        Block b = GetBlockAtIndexInFile(i, _pathToGetPreviousBlock);
                        if (b == null) { Console.WriteLine("[BLOCKS REFUSED] block null "); File.Delete(_filePath); return; }
                        timestamps.Add(b.TimeStamp);
                    }
                    else
                    {
                        Block b = GetBlockAtIndex(i);
                        if (b == null) { Console.WriteLine("[BLOCKS REFUSED] block null "); File.Delete(_filePath); return; }
                        timestamps.Add(GetBlockAtIndex(i).TimeStamp);
                    }
                    TC++;
                    if (TC >= TIMESTAMP_TARGET)
                    {
                        break;
                    }
                }
                uint MINTIMESTAMP = GetTimeStampRequirementB(timestamps);
                Print(MINTIMESTAMP.ToString());
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
                            if (earlierBlock == null) { Console.WriteLine("[BLOCKS REFUSED] block null "); File.Delete(_filePath); return; }
                        }
                        else
                        {
                            earlierBlock = GetBlockAtIndexInFile(previousBlock.Index + 1 - TARGET_CLOCK, _pathToGetPreviousBlock); // it means that we have an index shit...
                            if (earlierBlock == null) { Console.WriteLine("[BLOCKS REFUSED] block null "); File.Delete(_filePath); return; }
                        }

                    }
                    else
                    {
                        earlierBlock = GetBlockAtIndex(previousBlock.Index + 1 - TARGET_CLOCK);
                        if (earlierBlock == null) { Console.WriteLine("[BLOCKS REFUSED] block null "); File.Delete(_filePath); return; }

                    }



                    tempTarget = ComputeHashTargetB(previousBlock, earlierBlock);
                    Print((previousBlock.Index + 1).ToString());


                }
                else
                {
                    tempTarget = previousBlock.HashTarget;
                }
                List<UTXO> vUTXO = new List<UTXO>();  //< can go up to 1.2mb (including retrograde) in ram so it is ok... 
                // we should update this vUTXO with every block of a fork if fork is needed... also
                if (_pathToGetPreviousBlock != GetLatestBlockChainFilePath()) // we absolutely not need to compute retrograde here we just update the fork
                {
                    uint firstforkIndex = BitConverter.ToUInt32(GetBytesFromFile(4, 8, _pathToGetPreviousBlock), 0);
                    for (uint i = firstforkIndex; i < previousBlock.Index + 1; i++)
                    {
                        Block b = GetBlockAtIndexInFile(i, _pathToGetPreviousBlock);
                        if (b == null) { File.Delete(_filePath); return; }

                        foreach (Tx TXS in b.Data)
                        {
                            bool _sFound = false;
                            int sIndex = 0;
                            bool _rFound = false;
                            int rIndex = 0;
                            UTXO rutxo = null;

                            for (int a = 0; a < vUTXO.Count; a++)
                            {
                                if (vUTXO[a].HashKey.SequenceEqual(ComputeSHA256(TXS.sPKey)))
                                {
                                    _sFound = true;
                                    sIndex = a;
                                }
                                if (vUTXO[a].HashKey.SequenceEqual(TXS.rHashKey))
                                {
                                    _rFound = true;
                                    rIndex = a;
                                    rutxo = vUTXO[a];
                                }

                            }
                            if (!_sFound)
                            {
                                vUTXO.Add(UpdateVirtualUTXOWithFullBlock(b, GetOfficialUTXOAtPointer(TXS.sUTXOP), false));
                            }
                            else
                            {
                                vUTXO[sIndex] = UpdateVirtualUTXOWithFullBlock(b, vUTXO[sIndex], false);
                            }
                            if (!_rFound)
                            {
                                rutxo = GetOfficialUTXOAtPointer(TXS.rUTXOP);
                                if (rutxo != null)
                                {
                                    vUTXO.Add(UpdateVirtualUTXOWithFullBlock(b, GetOfficialUTXOAtPointer(TXS.rUTXOP), false));
                                }

                            }
                            else
                            {
                                vUTXO[rIndex] = UpdateVirtualUTXOWithFullBlock(b, rutxo, false);
                            }
                        }
                        bool _mFound = false;
                        int mIndex = 0;
                        for (int a = 0; a < vUTXO.Count; a++)
                        {
                            if (b.minerToken.MinerPKEY.SequenceEqual(vUTXO[a].HashKey))
                            {
                                _mFound = true;
                                mIndex = a;
                            }

                        }

                        if (!_mFound)
                        {
                            UTXO mutxo = GetOfficialUTXOAtPointer(b.minerToken.mUTXOP);
                            if (mutxo != null)
                            {
                                vUTXO.Add(UpdateVirtualUTXOWithFullBlock(b, mutxo, false));
                            }

                        }
                        else
                        {
                            vUTXO[mIndex] = UpdateVirtualUTXOWithFullBlock(b, vUTXO[mIndex], false);
                        }
                    }
                }

                while (true)
                {
                    // APPLY THE SEA
                    SHOM.SHOMData shom = SHOM.GetLastDataBeforeSpecificTime(SHOM.UnixTimeStampToDateTime(currentBlockReading.TimeStamp), shoms);
                    if (shom == null)
                    {
                        Console.WriteLine("No SHOM found... "); File.Delete(_filePath);
                        return;
                    }
                    byte[] reqtarget = ApplyTheSeaToTheCryptoPuzzle(tempTarget, shom);
                    //----
                    Tuple<bool, List<UTXO>> bV = IsBlockValid(currentBlockReading, previousBlock, MINTIMESTAMP, tempTarget, reqtarget, vUTXO);

                    if (!bV.Item1)
                    {
                        File.Delete(_filePath);
                        return;
                    }
                    vUTXO = bV.Item2; // vutxo are update! 
                    if (currentBlockReading.Index == latestTempIndex)
                    {

                        if (_pathToGetPreviousBlock == GetLatestBlockChainFilePath())
                        {
                            string newPath = GetNewForkFilePath();
                            File.Move(_filePath, newPath);
                            Print("new fork added");
                            UpdatePendingTXFileB(newPath);
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
                            Print("fork has been append.");
                            return;
                        }

                    }
                    previousBlock = currentBlockReading; //*
                    currentBlockReading = GetBlockAtIndexInFile(currentBlockReading.Index + 1, _filePath); //*
                    if (currentBlockReading == null) { File.Delete(_filePath); Print("wrong index specified 3"); return; }

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
                                if (earlierBlock == null) { Print("[missing block]"); File.Delete(_filePath); return; }
                            }
                            else
                            {
                                earlierBlock = GetBlockAtIndexInFile(previousBlock.Index + 1 - TARGET_CLOCK, _pathToGetPreviousBlock); // it means that we have an index shit...
                                if (earlierBlock == null) { Print("[missing block]"); File.Delete(_filePath); return; }
                            }

                        }
                        else
                        {
                            earlierBlock = GetBlockAtIndex(previousBlock.Index + 1 - TARGET_CLOCK);
                            if (earlierBlock == null) { Print("[missing block]"); File.Delete(_filePath); return; }

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
        public static Tuple<bool, List<UTXO>> IsBlockValid(Block b, Block prevb, uint MIN_TIME_STAMP, byte[] HASHTARGET, byte[] reqtarget, List<UTXO> vUTXO)
        {
            uint latestIndex = RequestLatestBlockIndex(true);
            if (!b.previousHash.SequenceEqual(prevb.Hash)) { Print("wrong previous hash"); return new Tuple<bool, List<UTXO>>(false, vUTXO); }
            uint unixTimestamp = (uint)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            if (b.TimeStamp < MIN_TIME_STAMP || b.TimeStamp > unixTimestamp) { Print("wrong time stamp"); return new Tuple<bool, List<UTXO>>(false, vUTXO); }
            if (!b.HashTarget.SequenceEqual(HASHTARGET)) { Print("wrong HASH TARGET"); return new Tuple<bool, List<UTXO>>(false, vUTXO); }
            uint sumFEE = 0;
            //------------------------------------ > WORKING VIRTUALIZING UTXO
            foreach (Tx TX in b.Data)
            {

                bool _sFound = false;
                bool _rFound = false;
                int sIndex = 0;
                UTXO sutxo = null;
                int rIndex = 0;
                UTXO rutxo = null;
                for (int i = 0; i < vUTXO.Count; i++)
                {
                    if (!_sFound)
                    {
                        if (vUTXO[i].HashKey.SequenceEqual(ComputeSHA256(TX.sPKey)))
                        {
                            sutxo = vUTXO[i];
                            sIndex = i;
                            _sFound = true;
                        }
                    }
                    if (!_rFound)
                    {
                        if (vUTXO[i].HashKey.SequenceEqual(TX.rHashKey))
                        {
                            rutxo = vUTXO[i];
                            rIndex = i;
                            _rFound = true;
                        }
                    }

                }
                if (!_sFound)
                {
                    sutxo = GetOfficialUTXOAtPointer(TX.sUTXOP);
                    if (sutxo == null) { return new Tuple<bool, List<UTXO>>(false, vUTXO); }
                    if (b.Index <= latestIndex) { sutxo = GetDownGradedVirtualUTXO(b.Index, sutxo); }
                }
                if (!_rFound)
                {
                    rutxo = GetOfficialUTXOAtPointer(TX.rUTXOP);
                    if (rutxo != null && b.Index <= latestIndex) { rutxo = GetDownGradedVirtualUTXO(b.Index, rutxo); }
                }
                if (!isTxValidforPending(TX, sutxo)) { Print("wrong TX"); return new Tuple<bool, List<UTXO>>(false, vUTXO); }

                sumFEE += TX.TxFee;
                UTXO nsUTXO = UpdateVirtualUTXO(TX, sutxo, false);


                if (_sFound)
                {
                    vUTXO[sIndex] = nsUTXO;
                }
                else
                {
                    vUTXO.Add(nsUTXO);
                }
                if (rutxo != null)
                {
                    UTXO nrUTXO = UpdateVirtualUTXO(TX, rutxo, false);
                    if (_rFound)
                    {
                        vUTXO[rIndex] = nrUTXO;
                    }
                    else
                    {
                        vUTXO.Add(nrUTXO);
                    }
                }

            }
            bool _mFound = false;
            int mIndex = 0;
            UTXO mutxo = null;

            for (int i = 0; i < vUTXO.Count; i++)
            {

                if (vUTXO[i].HashKey.SequenceEqual(b.minerToken.MinerPKEY))
                {
                    mutxo = vUTXO[i];
                    mIndex = i;
                    _mFound = true;
                    break;
                }

            }
            if (!_mFound)
            {
                mutxo = GetOfficialUTXOAtPointer(b.minerToken.mUTXOP);
                if (mutxo != null && b.Index <= latestIndex) { mutxo = GetDownGradedVirtualUTXO(b.Index, mutxo); }
            }

            if (mutxo != null)
            {
                UTXO nmUTXO = UpdateVirtualUTXOWithFullBlock(b, mutxo, false);
                if (_mFound)
                {
                    vUTXO[mIndex] = nmUTXO;
                }
                else
                {
                    vUTXO.Add(nmUTXO);
                }
            }


            //------------------------------------ > <
            if (b.minerToken.mUTXOP != 0)
            {
                UTXO mUTXO = GetOfficialUTXOAtPointer(b.minerToken.mUTXOP);
                if (mUTXO == null) { Print("wrong UTXO pointer"); return new Tuple<bool, List<UTXO>>(false, vUTXO); }
                else { if (!mUTXO.HashKey.SequenceEqual(b.minerToken.MinerPKEY)) { Print("wrong UTXO pointer"); return new Tuple<bool, List<UTXO>>(false, vUTXO); } }
            }
            if (b.minerToken.MiningReward != GetMiningReward(b.Index) + sumFEE) { Print("wrong mining reward"); return new Tuple<bool, List<UTXO>>(false, vUTXO); }
            // now verify Merkle Root Correctness. 
            List<byte> dataBuilder = new List<byte>();
            dataBuilder = AddBytesToList(dataBuilder, BitConverter.GetBytes(b.Index));
            dataBuilder = AddBytesToList(dataBuilder, b.previousHash);
            dataBuilder = AddBytesToList(dataBuilder, BitConverter.GetBytes(b.DataSize));
            foreach (Tx TX in b.Data) { dataBuilder = AddBytesToList(dataBuilder, TxToBytes(TX)); }
            dataBuilder = AddBytesToList(dataBuilder, BitConverter.GetBytes(b.TimeStamp));
            dataBuilder = AddBytesToList(dataBuilder, MinerTokenToBytes(b.minerToken));
            dataBuilder = AddBytesToList(dataBuilder, b.HashTarget);
            byte[] sha = ComputeSHA256(ListToByteArray(dataBuilder));
            sha = ComputeSHA256(sha); //< double hash function to avoid collision or anniversary attack 
            if (!sha.SequenceEqual(b.Hash)) { Print("wrong merkle root"); return new Tuple<bool, List<UTXO>>(false, vUTXO); }
            // now checking nonce
            dataBuilder = new List<byte>();
            dataBuilder = AddBytesToList(dataBuilder, BitConverter.GetBytes(b.Nonce));
            dataBuilder = AddBytesToList(dataBuilder, b.Hash);
            byte[] hash = ComputeSHA256(ListToByteArray(dataBuilder));
            if (!isNonceGolden(hash, reqtarget)) { Print("wrong nonce"); return new Tuple<bool, List<UTXO>>(false, vUTXO); }

            return new Tuple<bool, List<UTXO>>(true, vUTXO);
        }

        // Fork and Winning distance protocol verification

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
            Print("winning dist" + WINNING_RUN_DISTANCE);
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
                        DownGradeUTXOSet(firstTempIndex - 1);
                    }
                    AddBlocksToOfficialChain(s, needPropagate);
                    _found = true;
                    // clear all forks
                    // Print("called a");
                    break;
                }
            }
            if (_found)
            {
                //Print("called b");
                foreach (string s in forkfiles)
                {
                    File.Delete(s);
                }
            }


        }
        
        // Hashcash Proof of work consensus methods for block validation

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
            Print("new target decimal is : " + b1.ToString());
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
            Print("new target decimal is : " + b1.ToString());
            return Uint256ToByteArray(b1);
        }
        public static byte[] ApplyTheSeaToTheCryptoPuzzle(byte[] Current_Target, SHOM.SHOMData shom)
        {
            // apply the water height to the difficulty of cryptographic puzzle ! 
            if (shom == null) { return Current_Target; }
            float currentwaterlevel = shom.value;
            float midpoint = (SEA_MAXLEVEL + SEA_MINLEVEL) / 2;
            float prct = currentwaterlevel / midpoint;
            prct = (float)Math.Pow(prct, SEA_FORCE);
            uint PRECISION = SEA_UINT256_PRECISION; // using this current precision could be more 0. will see. like 1000 is like 0.001 but we need more like 1000000
            prct *= PRECISION;
            uint prctInt = (uint)prct; // should do something if precision is higher value than max float precision
            Console.WriteLine("median = " + midpoint); // i get minimum int value. 
            Console.WriteLine("prct = " + prct); // i get minimum int value.
            BigInteger b1 = BytesToUint256(Current_Target);
            Console.WriteLine("current target = " + b1); // i get minimum int value. 
            BigInteger mult = new BigInteger(prctInt);
            BigInteger prec = new BigInteger(PRECISION);
            Console.WriteLine("mult = " + mult); // i get minimum int value.
            Console.WriteLine("precison = " + prec); // i get minimum int value.

            b1 = BigInteger.Multiply(b1, mult);
            b1 = BigInteger.Divide(b1, prec);
            Console.WriteLine("without lowering >");
            Console.WriteLine(b1); // ca a diminué 
            BigInteger maxtarget = BytesToUint256(MAXIMUM_TARGET);
            if (b1.CompareTo(maxtarget) == 1)
            {
                b1 = BytesToUint256(MAXIMUM_TARGET);
            }

            Console.WriteLine("with lowering -->");
            Console.WriteLine(b1); // ca a diminué 
            shom.print();

            return Uint256ToByteArray(b1);
        }
      
        // No use

        public static bool isBlockChainValid() //< THIS WILL COMPLETELY REBUILD AN UTXO SET. 
        {
            Print("method work in progress actually. please wait update of this program");
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

        
    }
}
