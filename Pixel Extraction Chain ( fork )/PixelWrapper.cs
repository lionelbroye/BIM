using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO; 

namespace firstchain
{
    class PixelWrapper 
    {
        /*
         PROBLEME ACTUEL LE MINING REWARD NEST PAS MIS A JOUR....

         */
        /*
         *  En admettant que le monochrome est de taille 50x50 pix alors 2500 pixels serait en circulation sur le marché. 
         *  Le nombre de pixel sera a definir dans le genesis ( dans la methode CreateGenesis() de Program.cs
         *  
        ----------------------------------STRUCTURE DE L'ANNUAIRE DES PIXELS -----------------------------------
        hash clé publique (32 octets) 
        token d'unicité (4 octets )
        sold ( coin ) 
        nombre de pixel en possession [4 octets] // peut aller de 0 à 2500 ... 
        Liste de pixel en possession sous cette forme

           [position x du pixel, position y du pixel]  [bloc dans lequel il est contenu, numéro de transaction dans lequel il a été acquis][prix equation]

                exemple : | [0,0][2,10][1]
                          | [10,2][2,10][1]
                          | [25,5][2,10][1]
                          | [10,2][3,5][1]

        */
       

        public static UInt64 LATEST_UPO_INDEX = 0; //????
        public class Vector
        {
            public Int32 X { get; set; }
            public Int32 Y { get; set; }

            public Vector(Int32 x = 0, Int32 y = 0)
            {
                this.X = x;
                this.Y = y;
            }
        }
       
        //------------------------------------------- NEW TYPE OF TRANSACTION ------------------------------------
        public class PixTX // minimum size : 1064 octets
        {
            public byte[] sPKey { get; } // ma clé publique [532 octets]
            public UInt64 Index { get; } // le pointeur de ma clé publique dans l'annuaire des pixels [ 8 octets ]
            public uint TokenOfUniqueness { get; } // token d'unicité [ 4 octets ] 
            public uint ExtractionSize { get; } // nombre d'extraction dans ma transaction [ 4 octets ] DOIT ETRE SUPERIEUR A 0 ! 
            public List<PixExtract> data { get; } // mes extractions ( liste d'extractions ) c'est ce qui formera mon texte ou mon image ;D ( 68 octets per extractions )
            public uint TxFee { get; } // frais de transaction [ 4 octets ] 
            public uint Value { get; } // valeur appliqué  à l'ensemble des pixels - après la transaction. equation. [4 octets]
            public byte[] Signature { get; } // signature de tout ce qui précede avec ma clé privé. [512 octets]

            public PixTX(byte[] pkey, UInt64 index, uint tou, List<PixExtract> dt, uint fee, uint value, byte[] sign)
            {
                this.sPKey = pkey;
                this.Index = index;
                this.TokenOfUniqueness = tou;
                this.ExtractionSize = (uint)dt.Count;
                this.data = dt;
                this.TxFee = fee;
                this.Value = value;
                this.Signature = sign;
            }
        }
        public class PixExtract // size : 68 octets
        {
            public byte[] hPKey { get; } // hash de la clé publique du possesseur de ces pixels [32 octets]
            public UInt64 Index { get; } // le pointeur de la clé publique dans l'annuaire des pixels [ 8 octets ]
            public Vector[] ExtractionZone { get; } // list de 2 vecteurs des points d'extraction [ 16 octets ]
            public Vector Offset { get; } // [8 octets] // offset
            public uint BlocIndex { get; } // [4 octets] // bloc mentionné pour l'extraction
            public uint BlocTX { get; } // [4 octets] // pixel transaction mentionné lors de l'acquisition

            public PixExtract(byte[] pkey, UInt64 index, Vector[] zone, Vector off, uint bindex, uint btx)
            {
                this.hPKey = pkey;
                this.Index = index;
                this.ExtractionZone = zone;
                this.Offset = off;
                this.BlocIndex = bindex;
                this.BlocTX = btx;
            }
        }

        // ------------------------------  Main Method Pixel Protocol --------------------
        public static List<Pixel> GetAllPixelsFromExtraction(PixExtract pex ,List<UPO> vUPOs)
        {
            // get the upo from pex hashbey 
            List<Pixel> exPixels = new List<Pixel>();
            UPO upo = null;
            bool _found = false;
            foreach( UPO vupo in vUPOs)
            {
                if ( vupo.HashKey.SequenceEqual(pex.hPKey))
                {
                    upo = vupo;
                    _found = true;
                    break; 
                }
            }
            if ( !_found)
            {
                upo = GetOfficialUPO(pex.Index, pex.hPKey);
            }
            if (pex.ExtractionZone[0].X < pex.ExtractionZone[1].X) { return exPixels; }
            if (pex.ExtractionZone[0].Y < pex.ExtractionZone[1].Y) { return exPixels; }

            for (int x = pex.ExtractionZone[0].X; x <= pex.ExtractionZone[1].X; x++)
            {
                for (int y = pex.ExtractionZone[0].Y; y <= pex.ExtractionZone[1].Y; y++)
                {
                    foreach(Pixel pix in upo.Pixels)
                    {
                        if ( pix.BlocIndex == pex.Index && pix.PixTXIndex == pex.BlocTX && pix.position.X == x && pix.position.Y == y)
                        {
                            // you can't add multiple times the same pixel so ... 
                            if (!exPixels.Contains(pix))
                            {
                                exPixels.Add(pix);
                            }
                        }
                    }
                }
            }
            return exPixels;
        }
        public static uint GetValueFromPixels(List<Pixel> pixs)
        {

            return 0; 
        }
        public static Pixel UpdatePixel(Pixel pix, uint newval, uint BlockID, uint TXID, Vector offset)
        {
            return new Pixel(new Vector(pix.position.X + offset.X, pix.position.Y + offset.Y ), BlockID, TXID, newval);
        }

        // UTXO etait l'acronyme pour Unspent transaction Output (Montant non depensé apres transaction ) .
        //              Nous changeons cette denomination pour notre chaine de bloc comme tel : 
        //-------------------> UPO acronyme de Unexcavated Pixel Ouput ( Pixel non extrait apres transaction ). 

        public class UPO  // min size = 44 octets | max size = ( for 50x50: 50044 / 500*500 : 250044 octets / 1000*1000 : 1 000 044 otctes
                          // nombre de pkey pour un annuaire de frag ( fixé a environ 10Millions d'octets
        {
            public byte[] HashKey { get; } //32 o
            public uint TokenOfUniqueness { get; } // 4 o 
            public uint Sold { get; } // 4 o 
            public uint pixelsSize { get;  } // 4o 
            public List<Pixel> Pixels { get; } // * 20 octets par Pixel

            public UPO(byte[] key, uint tou, uint sold, List<Pixel> pixs)
            {
                this.HashKey = key;
                this.TokenOfUniqueness = tou;
                this.Sold = sold;
                this.pixelsSize = (uint)pixs.Count; 
                this.Pixels = pixs;
            }
        }

        public class Pixel
        {
            public Vector position { get; } // 8 octets
            public uint BlocIndex { get; } // 4 octets
            public uint PixTXIndex { get; } // 4 octets
            public uint Value { get; } // 4 octets
            public Pixel(Vector pos, uint bi, uint txi, uint val)
            {
                this.position = pos;
                this.BlocIndex = bi;
                this.PixTXIndex = txi;
                this.Value = val;
            }
        }
        
        public static byte[] PixExtractToBytes(PixExtract pext)
        {
            if (pext == null) { return new byte[0]; }
            List<byte> Databuilder = new List<byte>();
            Databuilder = Program.AddBytesToList(Databuilder, pext.hPKey);
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(pext.Index));
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(pext.ExtractionZone[0].X));
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(pext.ExtractionZone[0].Y));
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(pext.ExtractionZone[1].X));
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(pext.ExtractionZone[1].Y));
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(pext.Offset.X));
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(pext.Offset.Y));
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(pext.BlocIndex));
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(pext.BlocTX));
            return Program.ListToByteArray(Databuilder);
        }

        public static PixExtract BytesToPixExtract(byte[] bytes)
        {
            if (bytes.Length != 68) { return null; }
            byte[] hPKey = new byte[32];
            byte[] APP = new byte[8];
            byte[] ExtractionZone_AX = new byte[4];
            byte[] ExtractionZone_AY = new byte[4];
            byte[] ExtractionZone_BX = new byte[4];
            byte[] ExtractionZone_BY = new byte[4];
            byte[] Offset_X = new byte[4];
            byte[] Offset_Y = new byte[4];
            byte[] BlocIndex = new byte[4];
            byte[] BlocTX = new byte[4];
            uint byteOffset = 0;
            for (uint i = byteOffset; i < byteOffset + 32; i++)
            {
                hPKey[i - byteOffset] = bytes[i];
            }
            byteOffset += 32;
            for (uint i = byteOffset; i < byteOffset + 8; i++)
            {
                APP[i - byteOffset] = bytes[i];
            }
            byteOffset += 8;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                ExtractionZone_AX[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                ExtractionZone_AY[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                ExtractionZone_BX[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                ExtractionZone_BY[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                Offset_X[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                Offset_Y[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                BlocIndex[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                BlocTX[i - byteOffset] = bytes[i];
            }

            return new PixExtract(hPKey,
                BitConverter.ToUInt32(APP, 0),
                new Vector[2] { new Vector(BitConverter.ToInt32(ExtractionZone_AX, 0), BitConverter.ToInt32(ExtractionZone_AY, 0)),
                                new Vector(BitConverter.ToInt32(ExtractionZone_BX, 0), BitConverter.ToInt32(ExtractionZone_BY, 0))},
                new Vector(BitConverter.ToInt32(Offset_X, 0), BitConverter.ToInt32(Offset_Y, 0)),
                BitConverter.ToUInt32(BlocIndex, 0),
                BitConverter.ToUInt32(BlocTX, 0)
                ); 
        }
     
        public static PixTX BytesToPixTX(byte[] bytes)
        {

            // we should secure the stuff...

            byte[] sPKey = new byte[532];
            byte[] APP = new byte[8];
            byte[] TokenOfUniqueness = new byte[4];
            byte[] ExtractionSize = new byte[4];
            List<PixExtract> data = new List<PixExtract>();  
            byte[] TxFee = new byte[4];
            byte[] Value = new byte[4];
            byte[] Signature = new byte[512];
            uint byteOffset = 0;
            for (uint i = byteOffset; i < byteOffset + 532; i++)
            {
                sPKey[i - byteOffset] = bytes[i];
            }
            byteOffset += 532;
            for (uint i = byteOffset; i < byteOffset + 8; i++)
            {
                APP[i - byteOffset] = bytes[i];
            }
            byteOffset += 8;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                TokenOfUniqueness[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                ExtractionSize[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;

            for (uint i = 0; i < BitConverter.ToUInt32(ExtractionSize,0); i++)
            {
                byte[] dtBytes = new byte[72];
                for (uint n = byteOffset; n < byteOffset + 72; n++)
                {
                    dtBytes[n - byteOffset] = bytes[n]; 
                }
                byteOffset += 72;
                PixExtract pex = BytesToPixExtract(dtBytes);
                if (pex== null) { return null; }
                data.Add(pex);
            }
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                TxFee[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                Value[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 512; i++)
            {
                Signature[i - byteOffset] = bytes[i];
            }
            return new PixTX(sPKey, BitConverter.ToUInt32(APP, 0), BitConverter.ToUInt32(TokenOfUniqueness, 0), data,
                BitConverter.ToUInt32(TxFee, 0), BitConverter.ToUInt32(Value, 0), Signature);
        }

       
        public static byte[] PixTxToBytes(PixTX ptx)
        {
            if (ptx == null) { return new byte[0]; }
            List<byte> Databuilder = new List<byte>();
            Databuilder = Program.AddBytesToList(Databuilder, ptx.sPKey);
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(ptx.Index));
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(ptx.TokenOfUniqueness));
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(ptx.ExtractionSize));
            foreach(PixExtract pex in ptx.data) { Databuilder = Program.AddBytesToList(Databuilder, PixExtractToBytes(pex)); }
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(ptx.TxFee));
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(ptx.Value));
            Databuilder = Program.AddBytesToList(Databuilder, ptx.Signature);
            return Program.ListToByteArray(Databuilder);
        }
        // bytesToPixTX
        public static Pixel BytesToPixel(byte[] bytes)
        {
            if (bytes.Length != 20) { return null; }
            byte[] posx = new byte[4];
            byte[] posy = new byte[4];
            byte[] bi = new byte[4];
            byte[] txi = new byte[4];
            byte[] val = new byte[4];
            uint byteOffset = 0; 
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                posx[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                posy[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                bi[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                txi[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                val[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;

            return new Pixel(new Vector(BitConverter.ToInt32(posx, 0), BitConverter.ToInt32(posy, 0)), BitConverter.ToUInt32(bi, 0),
                BitConverter.ToUInt32(txi, 0), BitConverter.ToUInt32(val, 0)); 
        }

        public static byte[] PixelToBytes(Pixel pix)
        {
            if ( pix == null) { return new byte[0];  }
            List<byte> Databuilder = new List<byte>();
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(pix.position.X));
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(pix.position.Y));
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(pix.BlocIndex));
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(pix.PixTXIndex));
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(pix.Value));
            return Program.ListToByteArray(Databuilder);
        }
        
        public static UPO BytesToUPO(byte[] bytes)
        {
            // we should secure the stuff...
            byte[] HashKey = new byte[32];
            byte[] TokenOfUniqueness = new byte[4];
            byte[] Sold = new byte[4];
            byte[] pixelsSize = new byte[4];
            List<Pixel> Pixels = new List<Pixel>();
            uint byteOffset = 0;
            for (uint i = byteOffset; i < byteOffset + 32; i++)
            {
                HashKey[i - byteOffset] = bytes[i];
            }
            byteOffset += 32;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                TokenOfUniqueness[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                Sold[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                pixelsSize[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
            Console.WriteLine("pixelsize found in file : " + BitConverter.ToUInt32(pixelsSize, 0)); 
            for (uint i = 0; i < BitConverter.ToUInt32(pixelsSize, 0); i++)
            {
                byte[] pixelBytes = new byte[20];
                for (uint n = byteOffset; n < byteOffset + 20; n++)
                {
                    pixelBytes [n - byteOffset] = bytes[n]; //  out of range exception 
                }
                byteOffset += 20;
                Pixel pix = BytesToPixel(pixelBytes);
                if (pix == null) { Console.WriteLine("oh shit"); return null; }
                Pixels.Add(pix);
            }
            return new UPO(HashKey, BitConverter.ToUInt32(TokenOfUniqueness, 0), BitConverter.ToUInt32(Sold, 0), Pixels); 
        }

        public static byte[] UPOToBytes(UPO upo)
        {
            if (upo == null) { return new byte[0]; }
            List<byte> Databuilder = new List<byte>();
            Databuilder = Program.AddBytesToList(Databuilder, upo.HashKey);
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(upo.TokenOfUniqueness));
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(upo.Sold));
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(upo.pixelsSize));
            foreach ( Pixel pix in upo.Pixels)
            {
                Databuilder = Program.AddBytesToList(Databuilder, PixelToBytes(pix));
            }

            return Program.ListToByteArray(Databuilder); 
        }

        //------------------ File PTX manipulation

        public static PixTX GetPixTXatIndexInFile(uint index, string filePath)
        {
            //string filePath = Program._folderPath + "ptx";
            uint fileLength = (uint)new FileInfo(filePath).Length;
            uint readCounter = 0;
            uint byteOffset = 0;
            while ( byteOffset < fileLength)
            {
                if ( fileLength < byteOffset + 548) { return null;  }
                if ( readCounter == index)
                {
                    byteOffset += 544;
                    uint datasizeB = BitConverter.ToUInt32(Program.GetBytesFromFile(byteOffset, 4, filePath), 0);
                    byteOffset -= 544;
                    if (fileLength < byteOffset + 548 + (datasizeB * 72) + 520) { return null; }
                    return BytesToPixTX(Program.GetBytesFromFile(byteOffset, 548 + (datasizeB * 72) + 520, filePath)); 
                }
                byteOffset += 544;
                uint datasize =  BitConverter.ToUInt32(Program.GetBytesFromFile(byteOffset, 4, filePath),0);
                byteOffset += 4 + (datasize * 72) + 520;
                readCounter++;
            }
            return null; 
        } 

        public static List<PixTX> GetPackOfPixTXInfile(string filePath, uint startindex = 0, uint endindex = uint.MaxValue)
        {
            List<PixTX> result = new List<PixTX>();
            for (uint i = startindex; i < endindex; i++)
            {
                PixTX ptx = GetPixTXatIndexInFile(i, filePath);
                if ( ptx != null)
                {
                    result.Add(ptx);
                }
                else
                {
                    break; 
                }
            }
            return result;
        }
      
        public static void RemovePTXinPTXFile(PixTX ptx)
        {
            string filePath = Program._folderPath + "ptx";
            uint fileLength = (uint)new FileInfo(filePath).Length;
            uint byteOffset = 0;
            uint ptxCounter = 0;
            while (byteOffset < fileLength)
            {
                if (fileLength < byteOffset + 548) { return; }
                byte[] currentHashkey = Program.GetBytesFromFile(byteOffset, 32, filePath);
                uint currentTOU = BitConverter.ToUInt32(Program.GetBytesFromFile(byteOffset + 540, 4, filePath), 0); 
                if (currentHashkey.SequenceEqual(ptx.sPKey) && currentTOU == ptx.TokenOfUniqueness)
                {
                    byteOffset += 544;
                    uint datasizeB = BitConverter.ToUInt32(Program.GetBytesFromFile(byteOffset, 4, filePath), 0);
                    byteOffset -= 544;
                    if (fileLength < byteOffset + 548 + (datasizeB * 72) + 520) { Console.WriteLine("ptx can't be erased"); return ; }
                    // split the file like the upo set ... // we can get the counter here... 
                    byte[] _toSave = Program.GetBytesFromFile(byteOffset + 548 + (datasizeB * 72) + 520, fileLength - (byteOffset + 548 + (datasizeB * 72) + 520), filePath);
                    FileStream fs = new FileStream(filePath, FileMode.Open);
                    fs.SetLength(byteOffset);
                    fs.Close();
                    
                    Program.AppendBytesToFile(filePath, _toSave);
                    Console.WriteLine("ptx erased with success");
                    return;
                }
                byteOffset += 544;
                uint datasize = BitConverter.ToUInt32(Program.GetBytesFromFile(byteOffset, 4, filePath), 0);
                byteOffset += 4 + (datasize * 72) + 520;
                ptxCounter++;
            }
            
        }
        public static void AddPTXinPTXFile(PixTX ptx)
        {
            Program.AppendBytesToFile(Program._folderPath + "ptx", PixTxToBytes(ptx));
        }

        public static void UpdatePendingTXFileB(string _filePath)
        {
            uint firstTempIndex = BitConverter.ToUInt32(Program.GetBytesFromFile(4, 8, _filePath), 0);
            uint latestTempIndex = BitConverter.ToUInt32(Program.GetBytesFromFile(0, 4, _filePath), 0);
            for (uint i = firstTempIndex; i < latestTempIndex + 1; i++)
            {
                Program.Block b = Program.GetBlockAtIndexInFile(i, _filePath);
                if (b == null) { return; }
                UpdatePendingTXFile(b);
            }
        }
        public static void UpdatePendingTXFile(Program.Block b) // delete all TX in pending if they are include in the block ( just with verifying pkey & tou)
        {
            List<PixTX> allptx = GetPackOfPixTXInfile(Program._folderPath + "ptx");
            foreach ( PixTX TX in b.Data)
            {
                foreach ( PixTX txinfile in allptx)
                {
                    if ( txinfile.sPKey.SequenceEqual(TX.sPKey) && txinfile.TokenOfUniqueness == TX.TokenOfUniqueness)
                    {
                        RemovePTXinPTXFile(txinfile);
                    }
                }
            }
        }

        public static void ProccessTempTXforPending(string _filePath, bool needPropagate)
        {
            List<PixTX> allptx = GetPackOfPixTXInfile(_filePath);

            foreach ( PixTX ptx in allptx)
            {
                if (!isPixTxValid(ptx, new List<UPO>()))
                {
                    Console.WriteLine("ptx received is not valid.");
                    File.Delete(_filePath);
                    return;
                }
                else
                {
                    // add it to our ptx file only if max size of ptx allowed
                    byte[] toAdd = PixTxToBytes(ptx);
                    uint fileLenght = (uint)new FileInfo(Program._folderPath + "ptx").Length; 
                    if (  fileLenght + toAdd.Length  < Program.PTX_MAX_SIZE)
                    {
                        Program.AppendBytesToFile(Program._folderPath + "ptx", toAdd);
                    }
                    else
                    {
                        Console.WriteLine("can't add more ptx actually.");
                        File.Delete(_filePath);
                        break;
                    }
                    
                }
            }

            if (needPropagate)
            {
               Program.BroadcastQueue.Add(new Program.BroadcastInfo(1, 2, Program._folderPath + "ptx")); // we should upload the tx... 
            }
            File.Delete(_filePath);
        }

        /*
         * 
         */
        //------------------ File UPO manipulation
        public static UPO GetOfficialUPO_B(UInt64 index) // pkey has to be the hash of public key
        {
            // every pixel annuaire have an indication about the last index inside it (in 8 bytes values ) -
            string[] files = Directory.GetFiles(Program._folderPath + "pixelstate");
            string filePath = "";
            uint readCounter = 0;
            foreach (string s in files)
            {
                UInt64 lastIndex = BitConverter.ToUInt64(Program.GetBytesFromFile(0, 8, s), 0);
                if (lastIndex >= index)
                {
                    filePath = s;
                    break;
                }
            }

            if (filePath.Length == 0) { Console.WriteLine("unable to find a pointer for you ");  return null; }
            uint fileLength = (uint)new FileInfo(filePath).Length;
            uint byteOffset = 8; // first 8 bytes header is last Index in annuaire
            while (true)
            {
                if (byteOffset >= fileLength) { Console.WriteLine("nothing here");  return null; }
                Console.WriteLine(readCounter);
                if (readCounter == index)
                {
                    byteOffset += 40; // jump to pixelsSize data... 
                    if (byteOffset + 4 >= fileLength) { Console.WriteLine("unable to get the pixel size data..." + byteOffset + " " + fileLength); return null; } // big prob
                    uint psi = BitConverter.ToUInt32(Program.GetBytesFromFile(byteOffset, 4, filePath), 0);
                    byteOffset -= 40;
                    return BytesToUPO(Program.GetBytesFromFile(byteOffset, 44 + (psi * 20), filePath));
                }
                byteOffset += 40; // jump to pixelsSize data... 
                if (byteOffset + 4 >= fileLength) {  return null; }
                uint ps = BitConverter.ToUInt32(Program.GetBytesFromFile(byteOffset, 4, filePath), 0);
                byteOffset += 4 + (ps * 20);
                readCounter++;
            }
        }

        public static UPO GetOfficialUPO(UInt64 index, byte[] pKey) // pkey has to be the hash of public key
        {
            
            // every pixel annuaire have an indication about the last index inside it (in 8 bytes values ) -
            string[] files = Directory.GetFiles(Program._folderPath + "pixelstate");
            string filePath = "";
            uint readCounter = 0;
            foreach (string s in files)
            {
                UInt64 lastIndex = BitConverter.ToUInt64(Program.GetBytesFromFile(0, 8, s), 0);
                if (lastIndex >= index)
                {
                    filePath = s;
                    break;
                }
            }
         
            if ( filePath.Length == 0) { Console.WriteLine("no file found "); return null;  }
            uint fileLength = (uint) new FileInfo(filePath).Length; 
            uint byteOffset = 8; // first 8 bytes header is last Index in annuaire
            while (true)
            {
                if (byteOffset >= fileLength) { return null; }

                byte[] currentPKEY = Program.GetBytesFromFile(byteOffset, 32, filePath);
                if (currentPKEY.SequenceEqual(pKey))
                {
                    byteOffset += 40; // jump to pixelsSize data... 
                    if (byteOffset + 4 >= fileLength) { return null; }
                    uint psi = BitConverter.ToUInt32(Program.GetBytesFromFile(byteOffset, 4, filePath), 0);
                    byteOffset -= 40;
                    Console.WriteLine("found at " + readCounter);
                    return BytesToUPO(Program.GetBytesFromFile(byteOffset, 44 + (psi * 20), filePath)); 
                }
                byteOffset += 40; // jump to pixelsSize data... 
                if (byteOffset + 4 >= fileLength) {  return null; }
                uint ps = BitConverter.ToUInt32(Program.GetBytesFromFile(byteOffset, 4, filePath), 0);
                byteOffset += 4 + (ps * 20);
                readCounter++;
            }
            
        }
        
        public static UInt64 GetUPOIndex(byte[] pKey)
        {
            string[] files = Directory.GetFiles(Program._folderPath + "pixelstate");
            ulong readCounter = 0;
            foreach ( string s in files)
            {
                uint fileLength = (uint)new FileInfo(s).Length;
                uint byteOffset = 8;

                while (fileLength >= byteOffset)
                {
                    byte[] currentPKEY = Program.GetBytesFromFile(byteOffset, 32, s);
                    if (currentPKEY.SequenceEqual(pKey))
                    {
                        return readCounter; // wtf??
                    }
                    byteOffset += 40; // jump to pixelsSize data... 
                    if (byteOffset + 4 >= fileLength) { return 0; }
                    uint ps = BitConverter.ToUInt32(Program.GetBytesFromFile(byteOffset, 4, s), 0);
                    byteOffset += 4 + (ps * 20);
                    readCounter++;
                }
            }
            return 0; 

        }

        public static string GetLatestUPOfilePath()
        {
            Console.WriteLine(Program._folderPath);
            string[] files = Directory.GetFiles(Program._folderPath + "pixelstate");
            List<uint> flist = new List<uint>();
            foreach (string s in files) { flist.Add(Convert.ToUInt32(Path.GetFileName(s))); }
            flist.Sort();
            return Program._folderPath + "pixelstate\\" + flist[flist.Count - 1].ToString();
        }

        public static string GetNewUPOfilePath()
        {
            string[] files = Directory.GetFiles(Program._folderPath + "pixelstate");
            List<uint> flist = new List<uint>();
            foreach (string s in files) { flist.Add(Convert.ToUInt32(Path.GetFileName(s))); }
            flist.Sort();
            return Program._folderPath + "pixelstate\\" + flist[flist.Count].ToString();
        }
        
        public static ulong GetLastUPOIndex()
        {
            string filePath = GetLatestUPOfilePath();
            UInt64 latestIndex = BitConverter.ToUInt64(Program.GetBytesFromFile(0, 8, filePath), 0);
            return latestIndex;
        }

        // ------------------------ VIRTUALISATION DES UPO ---------------------------
        
        public static UPO UpdateVirtualUPOWithFullBlock(Program.Block b, UPO upo, bool reverse, List<UPO> vUPOs)
        {
            uint nSold = upo.Sold;
            uint nToken = upo.TokenOfUniqueness;
            List<Pixel> nPixs = upo.Pixels;
            uint ptxCount = 0; 
            if ( !reverse)
            {
                foreach ( PixTX ptx in b.Data)
                {
                    foreach (PixExtract pex in ptx.data)
                    {
                        if (pex.hPKey.SequenceEqual(upo.HashKey))
                        {

                             List<Pixel> pixs = GetAllPixelsFromExtraction(pex, vUPOs);
                             nSold += GetValueFromPixels(pixs);
                             foreach (Pixel pix in pixs)
                             {
                                 for (int i = nPixs.Count - 1; i >= 0; i++)
                                 {
                                     if (nPixs[i].Equals(pix))
                                     {
                                         nPixs.RemoveAt(i);
                                     }
                                 }
                             }
                            

                        }
                    }
                    //
                    if (Program.ComputeSHA256(ptx.sPKey).SequenceEqual(upo.HashKey))
                    {
                            // substract sold by all extract amount + tx fee
                            // add all pixels extracted to upo pixels... 
                            foreach (PixExtract pex in ptx.data)
                            {
                                List<Pixel> pixs = GetAllPixelsFromExtraction(pex, vUPOs);
                                nSold -= GetValueFromPixels(pixs);
                                foreach (Pixel pix in pixs)
                                {
                                    nPixs.Add(UpdatePixel(pix, ptx.Value, b.Index, ptxCount, pex.Offset)); // need trans numb... 
                                }
                            }
                            nSold -= ptx.TxFee;
                            nToken = upo.TokenOfUniqueness;
                    }

                    ptxCount++;  
                }
                if (b.minerToken.MinerPKEY.SequenceEqual(upo.HashKey))
                {
                    nSold += b.minerToken.MiningReward;
                }
            }
            else
            {
                foreach (PixTX ptx in b.Data)
                {
                    foreach (PixExtract pex in ptx.data)
                    {
                        if (pex.hPKey.SequenceEqual(upo.HashKey))
                        {
                            // lower the amount and get back the pixs . 
                            List<Pixel> pixs = GetAllPixelsFromExtraction(pex, vUPOs);
                            nSold -= GetValueFromPixels(pixs);
                            foreach (Pixel pix in pixs)
                            {
                                nPixs.Add(UpdatePixel(pix, ptx.Value, b.Index, ptxCount, pex.Offset)); // 
                            }
                        }
                    }
                    //
                    if (Program.ComputeSHA256(ptx.sPKey).SequenceEqual(upo.HashKey))
                    {
                        foreach (PixExtract pex in ptx.data)
                        {
                            List<Pixel> pixs = GetAllPixelsFromExtraction(pex, vUPOs);
                            nSold += GetValueFromPixels(pixs);
                            foreach (Pixel pix in pixs)
                            {
                                for (int i = nPixs.Count - 1; i >= 0; i++)
                                {
                                    if (nPixs[i].Equals(pix))
                                    {
                                        nPixs.RemoveAt(i);
                                    }
                                }
                            }

                        }
                        nSold += ptx.TxFee;
                        nToken = upo.TokenOfUniqueness;
                    }
                    ptxCount++;

                }
                if (b.minerToken.MinerPKEY.SequenceEqual(upo.HashKey))
                {
                    nSold -= b.minerToken.MiningReward;
                }
            }
            return new UPO(upo.HashKey, nToken, nSold, nPixs);
        }
        public static List<UPO> UpdateUPOCacheListWithMinerToken(List<UPO> cache, Program.MinerToken mt) // just update cache list
        {

            for ( int i = 0; i < cache.Count; i++) 
            {
                if (mt.MinerPKEY.SequenceEqual(cache[i].HashKey))
                {
                    cache[i] = new UPO(cache[i].HashKey, cache[i].TokenOfUniqueness, cache[i].Sold + mt.MiningReward, cache[i].Pixels);
                }
            }
          
            return cache;
        }
        public static List<UPO> UpdateUPOCacheListWithFullBlock(List<UPO> cache, Program.Block b)
        { 
            bool _minerfound = false;

            foreach (UPO upo in cache)
            {
                if (b.minerToken.MinerPKEY.SequenceEqual(upo.HashKey))
                {
                    _minerfound = true;
                    Console.WriteLine("miner was found");
                    break;
                }
            }
            if (!_minerfound)
            {
                if ( b.minerToken.mUPOIndex != 0)
                {
                    UPO mupo = GetOfficialUPO(b.minerToken.mUPOIndex, b.minerToken.MinerPKEY); 
                    if (mupo == null) { Console.WriteLine("BIG SHIIT HERE" + b.minerToken.mUPOIndex);}
                    cache.Add(GetOfficialUPO(b.minerToken.mUPOIndex,b.minerToken.MinerPKEY));
                    
                }
                else
                {
                    cache.Add(new UPO(b.minerToken.MinerPKEY, 0, b.minerToken.MiningReward, new List<Pixel>()));
                }
            }
            foreach (PixTX ptx in b.Data)
            {
                bool _ptxUpofound = false;
                foreach ( UPO upo in cache)
                {
                    if (ptx.sPKey.SequenceEqual(upo.HashKey))
                    {
                        _ptxUpofound = true;
                    }
                }
                if (!_ptxUpofound)
                {
                    cache.Add(GetOfficialUPO(ptx.Index, ptx.sPKey));
                }
                foreach ( PixExtract pex in ptx.data)
                {
                    bool _pexUPOfound = false;
                    foreach (UPO upo in cache)
                    {
                        if (pex.hPKey.SequenceEqual(upo.HashKey))
                        {
                            _pexUPOfound = true;
                        }
                    }
                    if (!_pexUPOfound)
                    {
                        cache.Add(GetOfficialUPO(pex.Index, pex.hPKey));
                    }
                }
                
            }
            return cache;

        }

        public static List<UPO> GetVirtualUPOCacheFromFile(string _filePath, uint startindex ,uint endindex)
        {
            List<UPO> cache = new List<UPO>();
            uint latestOfficialIndex = Program.RequestLatestBlockIndex(true);
            bool needDowngrade = false;
            if ( latestOfficialIndex < startindex) { needDowngrade = true; }
            if ( !needDowngrade ) // for this purpose we know we have to procceed from a fork file.  
            {
                uint firstIndex = BitConverter.ToUInt32(Program.GetBytesFromFile(4,8,_filePath),0);
                for (uint i = firstIndex; i < endindex + 1; i++)
                {
                    Program.Block b = Program.GetBlockAtIndexInFile(i, _filePath);
                    cache = UpdateUPOCacheListWithFullBlock(cache, b); 
                    
                    for (int a = 0; a < cache.Count; a++)
                    {
                        cache[a] = UpdateVirtualUPOWithFullBlock(b, cache[a], false, cache);
                    }
             
                }
            }
            else
            {
                // first downgrade all utxo ... 
                for (uint i = latestOfficialIndex; i >= startindex; i++ )
                {
                    Program.Block b = Program.GetBlockAtIndex(i);
                    cache = UpdateUPOCacheListWithFullBlock(cache, b);
                    for (int a = 0; a < cache.Count; a++)
                    {
                        cache[a] = UpdateVirtualUPOWithFullBlock(b, cache[a], true, cache);
                    }
                }
                if ( endindex > startindex)
                {
                    for (uint i = latestOfficialIndex + 1; i <= endindex; i++)
                    {
                        Program.Block b = Program.GetBlockAtIndexInFile(i, _filePath);
                        cache = UpdateUPOCacheListWithFullBlock(cache, b);
                        for (int a = 0; a < cache.Count; a++)
                        {
                            cache[a] = UpdateVirtualUPOWithFullBlock(b, cache[a], false, cache);
                        }

                    }
                }
                
            } 
            return cache;
        }
        public static List<UPO> UpdateUPOCacheWithPixelTX(List<UPO> cache, PixTX ptx, uint blockindex, uint ptxindex)
        {
            foreach ( UPO upo in cache)
            {
                UpdateVirtualUPO(ptx, upo, false, blockindex, ptxindex, cache);
            }
            return cache;
        }
        public static UPO UpdateVirtualUPO(PixTX ptx, UPO upo, bool reverse, uint blocIndex, uint ptxIndex, List<UPO> cache = null ) 
        {
            // erase every pixel possession if ptx 
            uint nSold = upo.Sold;
            uint nToken = upo.TokenOfUniqueness;
            List<Pixel> nPixs = upo.Pixels;

            foreach (PixExtract pex in ptx.data)
            {
                if (pex.hPKey.SequenceEqual(upo.HashKey))
                {
                    if (!reverse)
                    {
                        // add the amount and remove the pix 
                        List<Pixel> pixs = GetAllPixelsFromExtraction(pex, cache);
                        nSold += GetValueFromPixels(pixs);
                        foreach (Pixel pix in pixs)
                        {
                            for (int i = nPixs.Count - 1; i >= 0; i++)
                            {
                                if (nPixs[i].Equals(pix))
                                {
                                    nPixs.RemoveAt(i);
                                }
                            }
                        }
                    }
                    else
                    {
                        // lower the amount and get back the pixs . 
                        List<Pixel> pixs = GetAllPixelsFromExtraction(pex, cache);
                        nSold -= GetValueFromPixels(pixs);
                        foreach (Pixel pix in pixs)
                        {
                            nPixs.Add(UpdatePixel(pix, ptx.Value, blocIndex, ptxIndex, pex.Offset)); // need bIndex and ptx index
                        }
                    }

                }
            }

            if (Program.ComputeSHA256(ptx.sPKey).SequenceEqual(upo.HashKey))
            {
                if (!reverse)
                {
                    // substract sold by all extract amount + tx fee
                    // add all pixels extracted to upo pixels... 
                    foreach(PixExtract pex in ptx.data)
                    {
                        List<Pixel> pixs = GetAllPixelsFromExtraction(pex,cache);
                        nSold -= GetValueFromPixels(pixs);
                        foreach( Pixel pix in pixs)
                        {
                            nPixs.Add(UpdatePixel(pix, ptx.Value, blocIndex, ptxIndex, pex.Offset));
                        }
                    }
                    nSold -= ptx.TxFee; 
                    nToken = upo.TokenOfUniqueness;
                }
                else
                {
                    // add sold by all extract amount + tx fee
                    // remove all pixels extracted to upo pixels... 
                    foreach (PixExtract pex in ptx.data)
                    {
                        List<Pixel> pixs = GetAllPixelsFromExtraction(pex, cache);
                        nSold += GetValueFromPixels(pixs);
                        foreach (Pixel pix in pixs)
                        {
                            for (int i = nPixs.Count - 1; i >= 0; i++)
                            {
                                if (nPixs[i].Equals(pix))
                                {
                                    nPixs.RemoveAt(i);
                                }
                            }
                        }
                       
                    }
                    nSold += ptx.TxFee;
                    nToken = upo.TokenOfUniqueness;
                }
            }


            
            return new UPO(upo.HashKey, nToken, nSold, nPixs);

        }

        public static UPO GetDownGradedVirtualUPO(uint index, UPO upo, List<UPO> vUPOs) //< get a virtual instance of a specific UTXO at specific time of the official chain
        {
            for (uint i = Program.RequestLatestBlockIndex(false); i >= index; i--)
            {
                if (i == uint.MaxValue) { break; }
                Program.Block b = Program.GetBlockAtIndex(i);
                if (b == null) { Console.WriteLine("[missing block] Downgrade UTXO aborted "); return null; }
                upo = UpdateVirtualUPOWithFullBlock(b, upo, true, vUPOs);
            }
            return upo;
        }
        public static void UpgradeUPOSet(Program.Block b) //< Apply when changing Official Blockchain Only. OverWriting UTXO depend of previous transaction. Produce dust.
        {
            uint ptxCount = 0;
            foreach (PixTX ptx in b.Data)
            {
                UPO upo = GetOfficialUPO(ptx.Index, Program.ComputeSHA256(ptx.sPKey));
                if (upo == null) { Program.FatalErrorHandler(0, "no upo found during upgrade upo set"); return; } // FATAL ERROR 
                upo = UpdateVirtualUPO(ptx, upo, false, b.Index, ptxCount);
                UpdateOfficialUPO(upo, ptx.Index, Program.ComputeSHA256(ptx.sPKey));
                
                // now update for each extract
                foreach ( PixExtract pex in ptx.data)
                {
                    upo = GetOfficialUPO(pex.Index, pex.hPKey);
                    if (upo == null) { Program.FatalErrorHandler(0, "no upo found during upgrade upo set"); return; } // FATAL ERROR
                    upo = UpdateVirtualUPO(ptx, upo, false, b.Index, ptxCount);
                    UpdateOfficialUPO(upo, pex.Index, pex.hPKey);
                }
                ptxCount++;
            }
            if (b.minerToken.mUPOIndex != 0)
            {
                UPO upo = GetOfficialUPO(b.minerToken.mUPOIndex, b.minerToken.MinerPKEY);
           
                if (upo == null) { Program.FatalErrorHandler(0, "no upo found during upgrade upo set"); return; } // FATAL ERROR
                Console.WriteLine(b.minerToken.mUPOIndex + "curr sold : " + upo.Sold);
                uint mSold = upo.Sold + b.minerToken.MiningReward;
                uint mTOU = upo.TokenOfUniqueness ;
                UpdateOfficialUPO(new UPO(upo.HashKey, mTOU, mSold, upo.Pixels), b.minerToken.mUPOIndex, b.minerToken.MinerPKEY);
            }
            else
            {
                // create an empty UPO
                UPO upo = new UPO(b.minerToken.MinerPKEY, 0, b.minerToken.MiningReward, new List<Pixel>());
                AddDust(upo);
            }
        }
        public static void DownGradeUPOset(uint index) //< Apply when changing Official Blockchain Only. OverWriting UTXO depend of previous transaction. Compute dust.
        {
            uint DustCount = 0; 
            for (uint i = Program.RequestLatestBlockIndex(true); i > index; i--)
            {
                if (i == uint.MaxValue) { break; }
                Program.Block b = Program.GetBlockAtIndex(i);
                if (b == null) { Program.FatalErrorHandler(0, "no block found during downgrade upo set"); return; } // FATAL ERROR
                if (b.minerToken.mUPOIndex != 0)
                {
                    UPO upo = GetOfficialUPO(b.minerToken.mUPOIndex, b.minerToken.MinerPKEY);
                    if (upo == null) { Program.FatalErrorHandler(0, "no upo found during downgrade upo set"); return; } // FATAL ERROR
                    uint mSold = upo.Sold - b.minerToken.MiningReward;
                    uint mTOU = upo.TokenOfUniqueness - 1;
                    UpdateOfficialUPO(new UPO(upo.HashKey, mTOU, mSold, upo.Pixels), b.minerToken.mUPOIndex, b.minerToken.MinerPKEY);
                }
                else
                {
                    DustCount++;
                }
                for (int a = b.Data.Count - 1; a >= 0; a--)
                {
                    if (a == uint.MaxValue) { break; }
                    PixTX ptx = b.Data[a];
                    UPO upo = GetOfficialUPO(ptx.Index,ptx.sPKey);
                    if (upo == null) { Program.FatalErrorHandler(0, "no utxo found during downgrade upo set"); return; } // FATAL ERROR
                    upo = UpdateVirtualUPO(ptx, upo, true, b.Index, (uint)a);
                    UpdateOfficialUPO(upo, ptx.Index, ptx.sPKey);

                    for ( int y = ptx.data.Count-1; y >= 0; y--)
                    {
                        upo = GetOfficialUPO(ptx.data[y].Index, ptx.data[y].hPKey);
                        if (upo == null) { Program.FatalErrorHandler(0, "no upo found during downgrade upo set"); return; } // FATAL ERROR
                        upo = UpdateVirtualUPO(ptx, upo, true, b.Index, (uint)a);
                        UpdateOfficialUPO(upo, ptx.data[y].Index, ptx.data[y].hPKey);
                    }
                    
                }

            }

            if (!RemoveDust(DustCount)) { Program.FatalErrorHandler(0, "bad dust removing during downgrade upo set"); return; } // FATAL ERROR
        }

        // ------------------------ UPDATE DES UPO ---------------------------
        public static bool UpdateOfficialUPO(UPO toUpdate, ulong index, byte[] pKey  ) // Overwrite UPO in upo annuaire
        {
            // every pixel annuaire have an indication about the last index inside it (in 8 bytes values ) -
            string[] files = Directory.GetFiles(Program._folderPath + "pixelstate");
            string filePath = "";
            foreach (string s in files)
            {
                UInt64 lastIndex = BitConverter.ToUInt64(Program.GetBytesFromFile(0, 8, s), 0);
                if (lastIndex >= index)
                {
                    filePath = s;
                    break;
                }
            }

            if (filePath.Length == 0) { return false; }
            
            uint fileLength = (uint)new FileInfo(filePath).Length; // possibly do shit 
            
            uint byteOffset = 8;
            while (true)
            {
                if (byteOffset >= fileLength) { return false; }

                byte[] currentPKEY = Program.GetBytesFromFile(byteOffset, 32, filePath);
                if (currentPKEY.SequenceEqual(pKey))
                {
                    byteOffset += 40; // jump to pixelsSize data... 
                    if (byteOffset + 4 >= fileLength) { return false; }
                    uint psi = BitConverter.ToUInt32(Program.GetBytesFromFile(byteOffset, 4, filePath), 0);
                    byteOffset -= 40;
                    uint currentupoSize = 44 + (psi * 20);

                    // seems to create error ... 
                    byte[] _toSave = Program.GetBytesFromFile(byteOffset + currentupoSize, fileLength - (byteOffset + currentupoSize), filePath); 
                    // truncate our file... 
                    FileStream fs = new FileStream(filePath, FileMode.Open);
                    fs.SetLength(byteOffset);
                    fs.Close();
                    // save the rest && add the new upo in byte array
                    List<byte> DataBuilder = new List<byte>();
                    DataBuilder = Program.AddBytesToList(DataBuilder, UPOToBytes(toUpdate));
                    DataBuilder = Program.AddBytesToList(DataBuilder, _toSave);
                  
                    // append
                    Program.AppendBytesToFile(filePath, Program.ListToByteArray(DataBuilder));
                   

                    return true;
                }
                byteOffset += 40; // jump to pixelsSize data... 
                if (byteOffset + 4 >= fileLength) { return false; }
                uint ps = BitConverter.ToUInt32(Program.GetBytesFromFile(byteOffset, 4, filePath), 0);
                byteOffset += 4 + (ps * 20);

            }
        }
        public static void AddDust(UPO upo)
        {
            LATEST_UPO_INDEX++;
            byte[] bytes = UPOToBytes(upo);
            string filePath = GetLatestUPOfilePath();
            Console.WriteLine(filePath);
            Console.WriteLine("upo size : "+ bytes.Length);
            uint latestlength = (uint) new FileInfo(filePath).Length; 
            if ( latestlength + bytes.Length > Program.BLOCKCHAIN_FILE_CHUNK)
            {
                filePath = GetNewUPOfilePath();
                File.WriteAllBytes(filePath, BitConverter.GetBytes(LATEST_UPO_INDEX));
            }
            else
            {
                Program.OverWriteBytesInFile(0, filePath, BitConverter.GetBytes(LATEST_UPO_INDEX));
            }
            using (FileStream f = new FileStream(filePath, FileMode.Append)) 
            {
                f.Write(bytes, 0, bytes.Length);
            }
            Console.WriteLine("Update UPO -> " +LATEST_UPO_INDEX);
        }
        public static bool RemoveDust(uint nTime)
        {
            int DustsLength = (int)nTime * 44;
            while (true)
            {
                string filePath = GetLatestUPOfilePath();
                FileInfo f = new FileInfo(filePath);
                if (DustsLength > f.Length)
                {
                    File.Delete(filePath);
                    DustsLength -= (int)f.Length;
                    DustsLength += 8; // padding header
                }
                else
                {
                    Program.TruncateFile(filePath, (uint)DustsLength);
                    break;
                }
            }

            LATEST_UPO_INDEX -= nTime;
            Console.WriteLine("reducing upo index " + nTime);
            Program.OverWriteBytesInFile(0, GetLatestUPOfilePath(), BitConverter.GetBytes(LATEST_UPO_INDEX));
            return true;
        }

        // ------------------------ UPO CONSENSUS ---------------------------
      
        public static bool isPixTxValid(PixTX ptx, List<UPO> UPOcache)
       {
            // we use UPOCache if it exists ... 
            UPO upo = null;
            bool _found = false;
            foreach (UPO vupo in UPOcache)
            {
                if (vupo.HashKey.SequenceEqual(ptx.sPKey))
                {
                    upo = vupo;
                    _found = true;
                    break;
                }
            }
            if (!_found)
            {
                upo = GetOfficialUPO(ptx.Index, ptx.sPKey);
            }
            // [1] verify transaction signature
            if (!Program.VerifyTransactionDataSignature(ptx)) { Console.WriteLine("Invalid Signature"); return false; }
            // [2] verify UPO existing
            if (upo == null) { Console.WriteLine("Invalid UPO POINTER : utxo not found " + ptx.Index); return false; }
            uint sumpixvalue = 0;
            // [3] verify every extraction [ sum amount ]
            foreach(PixExtract pex in ptx.data)
            {
                if ( pex.ExtractionZone.Length != 2) { Console.WriteLine("Invalid Extraction Zone format!"); return false; }
                List<Pixel> pixs = GetAllPixelsFromExtraction(pex, UPOcache);
                sumpixvalue += GetValueFromPixels(pixs);
            }
            if (ptx.TxFee < Program.GetFee(upo)) { Console.WriteLine("Invalid Fee"); return false; }
            Int64 sold64 = Convert.ToInt64(upo.Sold);
            uint sum = sumpixvalue + ptx.TxFee;
            Int64 sum64 = Convert.ToInt64(sum);
            if (sold64 - sum64 < 0) { Console.WriteLine("not enough sold");  return false; }
            // [3] verify token of Uniqueness
            if (ptx.TokenOfUniqueness != upo.TokenOfUniqueness + 1) { Console.WriteLine("Invalid Token"); return false; }

            return true;
        }
    }
}
