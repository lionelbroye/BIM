using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace firstchain
{
    class pixelidea
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
        public class PixTX
        {
            public byte[] sPKey { get; } // ma clé publique [512 octets]
            public uint APP { get; } // le pointeur de ma clé publique dans l'annuaire des pixels [ 4 octets ]
            public uint TokenOfUniqueness { get; } // token d'unicité [ 4 octets ] 
            public uint ExtractionSize { get;  } // nombre d'extraction dans ma transaction [ 4 octets ] DOIT ETRE SUPERIEUR A 0 ! 
            public List<PixExtract> data { get; } // mes extractions ( liste d'extractions ) c'est ce qui formera mon texte ou mon image ;D
            public uint TxFee { get; } // frais de transaction [ 4 octets ] 
            public uint Value { get;  } // valeur appliqué  à l'ensemble des pixels - après la transaction. equation. [4 octets]
            public byte[] Signature { get; } // signature de tout ce qui précede avec ma clé privé. [512 octets]

            public PixTX(byte[] pkey, uint app, uint tou, List<PixExtract> dt, uint fee, uint value, byte[] sign )
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

        public class PixExtract
        {
            public byte[] hPKey { get; } // hash de la clé publique du possesseur de ces pixels [32 octets]
            public uint APP { get; } // le pointeur de la clé publique dans l'annuaire des pixels [ 4 octets ]
            public Vector[] ExtractionZone { get; } // list de 2 vecteurs des points d'extraction [ 16 octets ]
            public Vector Offset { get;  } // [8 octets] // offset
            public uint BlocIndex { get;  } // [4 octets] // bloc mentionné pour l'extraction
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
