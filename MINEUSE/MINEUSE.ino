
/* HERE SOME SPECIFIC FONCTION TO WRITE/READ CREATE FILE ON SD CARD 
if (SD.exists("filename")) : check if a file exist
SD.remove("filename") : remove a file
SD.open("filename", FILE_WRITE); create a file
SD.mkdir(filename) create a directory ( parse with '/')
SD.rmdir(filename) : remove a directory ( should delete file first

file.flush() 
then file.close()

append some bytes : 
outputFile.seek(EOF);
outputFile.println("Appended to the EOF");

file.seek(bytepos); // go to some specific byte position 
file.read(buffer,size); // full the buffer to get bytes 
file.size() unsigned long : get length of the file

*/

/*
sha test 
#include "sha256.h"

Sha256 sha; 


void setup(){
  
  Serial.begin(9600);
  uint8_t *hash; // same as byte ( unsigned 8 bit ... 
  sha.init();
  byte test [520]; 
  for (int i = 0 ; i < 520; i++ ) 
  {
   test[i] = 255; 
  }
  sha.write(test,520);
  //sha.print(("abs");
  hash = sha.result();
  delay(1000);
  printHash(hash);
  
}

void printHash(uint8_t* hash) {
 
  for (int i=0; i<32; i++) {
    Serial.print("0123456789abcdef"[hash[i]>>4]);
    Serial.print("0123456789abcdef"[hash[i]&0xf]);
  }
  Serial.println();
  
}
*/


      //------------------------------------------------- GENERAL  OBJECT STRUCT -----------------------------------------
  /* block struct : using SHA256 (32o for every hash). using Ed25519 (elliptic curve modulo 2^255 - 19) 32o private. 32o public. 64o sign
  :: index -> hash -> previoushash -> txnumb (max numb for atmega328p needed)-> txs -> timestamp??? -> MinerToken -> HT??? -> Nonce ???
  4     -> 32   -> 32           -> 1                                    -> ?*152 ->  4???       ->  40   ->  ???  -> ???
  */
  
#include <SPI.h>
#include <SD.h>
#include "sha256.h"

/*crypto*/
Sha256 sha; 

/*SD Stuff*/
Sd2Card card;
SdVolume volume;
File SFILE; 
const int chipSelect = 10;


void setup() {

  Serial.begin(9600);
  if (!InitializeSD()) return; 
  ClearAllFiles();
  CheckFilesAtRoot();
  Test_PrintGenesisUTXOPAndMiningReward();
}

bool InitializeSD(){

  if (!SD.begin(10)) {
    Serial.println("initialization failed!");
    return false;
  }
  return true;

}
void CheckFilesAtRoot(){

  // create 
  if (!SD.exists("genesis")){
    Serial.println("creating genesis...");
    CreateGenesis();
    if ( SD.exists("genesis")){
      Serial.println("genesis file OK ");
    }
  }
  else{
    Serial.println("genesis file OK");
  }
}

void ClearAllFiles(){
  if (SD.exists("genesis")){
      SD.remove("genesis");
  }
  
}
uint32_t BytesToUint(byte *arr ){
   uint32_t foo;
 // big endian
  foo = (uint32_t) arr[0] << 24;
  foo |=  (uint32_t) arr[1] << 16;
  foo |= (uint32_t) arr[2] << 8;
  foo |= (uint32_t) arr[3];
  return foo; 
}

void UINT32toBytes(uint32_t v, byte *a ){ // we dont need a return or something. we already pass a pointer in argument. THIS IS LITTLE ENDIAN
  a[0] = v >> 24;
  a[1] = v >> 16;
  a[2] = v >>  8;
  a[3] = v;
}

void Test_PrintGenesisUTXOPAndMiningReward(){


  SFILE = SD.open("genesis", FILE_WRITE); // genesis file is 113 o
  unsigned long fsize = SFILE.size();
  SFILE.seek(109); // go to last 4 bytes... 
  byte uintbuff[4];
  SFILE.read(uintbuff,4);
  SFILE.close();
  uint32_t reward = BytesToUint(uintbuff); 
  Serial.println("GENESIS SIZE = " + String(fsize) + " and miner reward is " + String(reward));
  
}

void CreateGenesis(){
  /* the c# 
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
  */
  uint8_t *hash; // same as byte ( unsigned 8 bit ...
  byte gen [255];
  byte i; // iterator
  for ( i = 0 ; i < 255 ; i++ ){
    gen[i] = i; 
  }
  // hash the stuff
  sha.write(gen,520);
  hash = sha.result();
  /* block struct : using SHA256 (32o for every hash). using Ed25519 (elliptic curve modulo 2^255 - 19) 32o private. 32o public. 64o sign
  :: index -> hash -> previoushash -> txnumb (max numb for atmega328p needed)-> txs -> timestamp??? -> MinerToken -> HT??? -> Nonce ???
  4     -> 32   -> 32           -> 1                                    -> ?*152 ->  4???       ->  40   ->  ???  -> ???
  */
  // genesis size : 113 o 
  byte genesis[113]; 
  byte uintbuff[4];
  byte byteOffset = 0; 
  for (i = 0 ; i < 4; i++ ) { genesis[byteOffset] = 0; byteOffset++;}
  for (i = 0 ; i < 32; i++ ) { genesis[byteOffset] = (byte)hash[i]; byteOffset++;} // hash
  for (i = 0 ; i < 32; i++ ) { genesis[byteOffset] = (byte)hash[i]; byteOffset++;} // prev hash
  genesis[byteOffset] = 0; byteOffset++; 
  UINT32toBytes(0, uintbuff ); // should do some zero here 
  for (i = 0 ; i < 4; i++ ) { genesis[byteOffset] = 0; byteOffset++;}// timeStamp? : equal to 0 probably
  // raw minertoken ... 
 
 
  // pkey of genesis is full of zero
  for (i = 0 ; i < 32; i++ ) { genesis[byteOffset] = 0; byteOffset++;}
  // reminder first 4 byte of utxo set is equal to currency volume. so first pointer start at 4.
  UINT32toBytes(4, uintbuff ); 

  for (i = 0 ; i < 4; i++ ) { genesis[byteOffset] = uintbuff[i]; byteOffset++;}// utxop
  // mining reward is always equal to 50 
  UINT32toBytes(50, uintbuff );
  for (i = 0 ; i < 4; i++ ) { genesis[byteOffset] = uintbuff[i]; byteOffset++;}// utxop
  Serial.println(byteOffset);
  // append all those bytes to genesis file on sd card
  SFILE = SD.open("genesis", FILE_WRITE); 
  SFILE.seek(EOF);
  SFILE.write(genesis, 113);
  SFILE.close();
}
void loop() {


}
