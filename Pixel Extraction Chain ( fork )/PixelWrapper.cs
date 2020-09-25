using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace firstchain
{
    class PixelWrapper
    {
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
            public uint APP { get; } // le pointeur de ma clé publique dans l'annuaire des pixels [ 4 octets ]
            public uint TokenOfUniqueness { get; } // token d'unicité [ 4 octets ] 
            public uint ExtractionSize { get; } // nombre d'extraction dans ma transaction [ 4 octets ] DOIT ETRE SUPERIEUR A 0 ! 
            public List<PixExtract> data { get; } // mes extractions ( liste d'extractions ) c'est ce qui formera mon texte ou mon image ;D ( 68 octets per extractions )
            public uint TxFee { get; } // frais de transaction [ 4 octets ] 
            public uint Value { get; } // valeur appliqué  à l'ensemble des pixels - après la transaction. equation. [4 octets]
            public byte[] Signature { get; } // signature de tout ce qui précede avec ma clé privé. [512 octets]

            public PixTX(byte[] pkey, uint app, uint tou, List<PixExtract> dt, uint fee, uint value, byte[] sign)
            {
                this.sPKey = pkey;
                this.APP = app;
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
            public uint APP { get; } // le pointeur de la clé publique dans l'annuaire des pixels [ 4 octets ]
            public Vector[] ExtractionZone { get; } // list de 2 vecteurs des points d'extraction [ 16 octets ]
            public Vector Offset { get; } // [8 octets] // offset
            public uint BlocIndex { get; } // [4 octets] // bloc mentionné pour l'extraction
            public uint BlocTX { get; } // [4 octets] // pixel transaction mentionné lors de l'acquisition

            public PixExtract(byte[] pkey, uint app, Vector[] zone, Vector off, uint bindex, uint btx)
            {
                this.hPKey = pkey;
                this.APP = app;
                this.ExtractionZone = zone;
                this.Offset = off;
                this.BlocIndex = bindex;
                this.BlocTX = btx;
            }
        }

        // UTXO etait l'acronyme pour Unspent transaction Output (Montant non depensé apres transaction ) .
        //              Nous changeons cette denomination pour notre chaine de bloc comme tel : 
        //-------------------> UPO acronyme de Unexcavated Pixel Ouput ( Pixel non extrait restant apres transaction ). 

        public class UPO
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

        public static uint CURRENT_UPOS_SIZE = 0;

        
        public static byte[] PixExtractToBytes(PixExtract pext)
        {
            if (pext == null) { return new byte[0]; }
            List<byte> Databuilder = new List<byte>();
            Databuilder = Program.AddBytesToList(Databuilder, pext.hPKey);
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(pext.APP));
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
            byte[] APP = new byte[4];
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
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                APP[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
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
            byteOffset += 4;

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
            byte[] sPKey = new byte[532];
            byte[] APP = new byte[4];
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
            for (uint i = byteOffset; i < byteOffset + 4; i++)
            {
                APP[i - byteOffset] = bytes[i];
            }
            byteOffset += 4;
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
                byte[] dtBytes = new byte[68];
                for (uint n = byteOffset; n < byteOffset + 68; n++)
                {
                    dtBytes[n - byteOffset] = bytes[n]; //  out of range exception 
                }
                byteOffset += 68;
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
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(ptx.APP));
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
            for (uint i = 0; i < BitConverter.ToUInt32(pixelsSize, 0); i++)
            {
                byte[] pixelBytes = new byte[20];
                for (uint n = byteOffset; n < byteOffset + 20; n++)
                {
                    pixelBytes [n - byteOffset] = bytes[n]; //  out of range exception 
                }
                byteOffset += 20;
                Pixel pix = BytesToPixel(pixelBytes);
                if (pix == null) { return null; }
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
            Databuilder = Program.AddBytesToList(Databuilder, BitConverter.GetBytes(upo.pixelsSize));
            foreach ( Pixel pix in upo.Pixels)
            {
                Databuilder = Program.AddBytesToList(Databuilder, PixelToBytes(pix));
            }

            return Program.ListToByteArray(Databuilder); 
        }

   
       

        // need a upotobytes
        public static uint GetUPOPointer(byte[] pKey)
        {

            uint byteOffset = 4;
            string filePath = Program._folderPath + "upos";
            if (byteOffset >= CURRENT_UPOS_SIZE) { return 0; }
            while (true)
            {
                if (byteOffset >= CURRENT_UPOS_SIZE) { return 0; }

                byte[] currentPKEY = Program.GetBytesFromFile(byteOffset, 32, filePath);
                if (currentPKEY.SequenceEqual(pKey))
                {
                    return byteOffset;
                }
                byteOffset += 40; // jump to pixelsSize data... 
                if (byteOffset + 4  >= CURRENT_UPOS_SIZE) { return 0; }
                uint ps = BitConverter.ToUInt32(Program.GetBytesFromFile(byteOffset, 4, filePath), 0);
                byteOffset += 4 + (ps * 20);
               
            }

        }
       
        public static UPO GetOfficialUPOAtPointer(uint pointer)
        {
            // get first pixel size... 
            string filePath = Program._folderPath + "upos";
            if ( pointer + 44  > CURRENT_UPOS_SIZE) { return null;  } // check if we can get the stuff
            uint ps = BitConverter.ToUInt32(Program.GetBytesFromFile(pointer+40, 4, filePath), 0);
            if ( pointer + 44 + (ps*20 ) > CURRENT_UPOS_SIZE ) { return null; }
            return BytesToUPO(Program.GetBytesFromFile(pointer, 44 + (ps * 20), filePath)); 
        }
       
        public static uint GetPixExtractionValue(PixExtract pex)
        {
            
            return 0;
        }

        /*
         *  En admettant que le monochrome est de taille 50x50 pix alors 2500 pixels serait en circulation sur le marché. 
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
         
        exemple brut ->

        a871c47a7f48a12b38a994e48a9659fab5d6376f3dbce37559bcb617efe8662d
        1
        600
        2 <* en lisant ce nombre. je sais jusqu'où je dois lire les infos de cette clé publique
        0
        0
        2
        10
        1
        10
        2
        2
        10
        1
        *< mon pc c'est que l'information de cette clé publique se termine ici grace au 2 plus haut. 2 = lire 2*20 octets...


        */
    }
}
