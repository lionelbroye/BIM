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

        // POW Blockchain traditionnal parameters

        public static uint WINNING_RUN_DISTANCE = 6; // LONGEST CHAIN WIN RULES DISTANCE
        public static uint MAX_RETROGRADE = 10; // WHAT IS THE MAXIMUM OF DOWNGRADING BLOCKCHAIN FOR A LIGHT FORK.
        public static uint TARGET_CLOCK = 42; // 2016. NEW HASH TARGET REQUIRED EVERY TARGET_CLOCKth BLOCK
        public static uint TIMESTAMP_TARGET = 11; // TIMESTAMP BLOCK SHOULD BE HIGHER THAN MEDIAN OF LAST TIMESTAMP_TARGETth BLOCK
        public static uint TARGET_TIME = 40320; // 1209600 . number of seconds a block should be mined 10 *  ---> WE SHOULD GET ONE BLOCK EVERY 10S . this is working !!! 
        public static uint TARGET_DIVIDER_BOUNDARIES = 4; // 4. LIMIT OF NEW TARGET BOUNDARIES (QUARTER + AND QUARTER - )
        public static uint FIRST_UNIX_TIMESTAMP = 1598981949;
        public static uint NATIVE_REWARD = 50; // COIN GIVE TO FIRST REWARD_DIVIDER_CLOCKth BLOCK
        public static uint REWARD_DIVIDER_CLOCK = 210000; // NUMBER OF BLOCK BEFORE NATIVE REWARD SHOULD BE HALFED
        public static byte[] MAXIMUM_TARGET = StringToByteArray("00000000FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"); //< MAX HASH TARGET. MINIMUM DIFFICULTY.

        // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~ Our Current Sea Consensus Parameters ~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public static float SEA_MINLEVEL = 0.1f;
        public static float SEA_MAXLEVEL = 10f;
        public static float SEA_FORCE = 8f; //< change sea force as you need. 10 should be a strong impact! below 5f it is not suffisant i guess. 8f is ok!
        public static int ACTUAL_PORT = 22;
        public static uint SEA_UINT256_PRECISION = 100000000;
        //if you go above 8f in sea force. add some 0 to this ( one by one ) or it can result target hash equal to 0 :'(
        // if you put sea_uint256_precision above 2,147,483,647 ( cause of . it will result an error. 



    }
}