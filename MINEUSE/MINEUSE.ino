// ok our blockchain can't exceed 4GB because of FAT32 format 
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
  :: index -> hash -> previoushash -> txnumb (max numb for atmega328p needed)-> txs -> timestamp??? -> MinerToken ->X HT??? -> Nonce ???
  4     -> 32   -> 32           -> 4                                    -> ?*152 ->  4???       ->  40   ->  ???  ->X not implemented *2 ???
  */
  // new stuff : block file are fixed size. there is one blockchain file which can get bigger. But every block are fixed size. Tx limit per block
  // is 4000 
  // there is a new file. the "blockpointers" file. every new block, a new uint is written in it. it contains the pointer of the block newly wriiten.
  // virtualizing utxo for fork check. (fork file have their custom utxo set now it is struct like this : 
  /*fork chain file :  
   starting index(uint32) -> number of block(uint32) ->  blockpointers (uint32 * nb) -> blocks .... 
  */
  /*BECAUSE TX ARE NOT FIXED SIZE : WE SERIALIZE TX AT THE END OF THE BLOCK !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! */
     // CAS 1 : 
    // ONE BLOCK PER TIDE. ONLY ACCEPTING ONE BLOCK PER TIDE. 
    // WHO IS THE MINER ? we need to define a rare event. we could use signature ( private blockchain ) 
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
  Initchain();
  CheckFilesAtRoot();
  Test_PrintGenesisUTXOPAndMiningReward();
  byte pkey[32]; // full of zero :: should be equal to Genesis pkey
  while (1){
    Mine(pkey,4); 
  }
}

bool InitializeSD(){

  if (!SD.begin(10)) {
    Serial.println("initialization failed!");
    return false;
  }
  return true;

}
/*
void printDirectory(File *dir, int numTabs) { // will return the number of gile 

  while (true) {

    File entry =  dir.openNextFile();

    if (! entry) {
      // no more files
      break;
    }
    Serial.print(entry.name());
    entry.close();
  }
}
*/
void CheckFilesAtRoot(){
  
   // blockchain folder check 
  if (!SD.exists("blockchain")){
      CreateGenesis();
  }
  if (!SD.exists("blockpointers")){
     SFILE = SD.open("blockpointers", FILE_WRITE); 
    SFILE.seek(EOF);
    byte uintbuff[4];
    UINT32toBytes(0, uintbuff ); // writting pointer of the genesis
    SFILE.write(uintbuff, 4);
    SFILE.close();
  }
  // utxo set file check 
  if (!SD.exists("utxos")){
    Serial.println("creating utxo set...");
    SFILE = SD.open("utxos", FILE_WRITE); 
    SFILE.seek(EOF);
    byte uintbuff[4];
    UINT32toBytes(50, uintbuff ); // currency volume is 50. cause genesis produce first 50 coin?
    SFILE.write(uintbuff, 4);
    SFILE.close();
  }
  else{
    Serial.println("utxo set file OK");
  }
 
  // fork chain folder check 
  if (!SD.exists("fork")){
    SD.mkdir("fork"); 
  }
  // ptx
  if (!SD.exists("ptx")){
    Serial.println("creating pending transaction file...");
    SFILE = SD.open("ptx", FILE_WRITE); 
    SFILE.close();
  }
  else{
    Serial.println("pending transaction file OK");
  }
}

void Initchain(){
  
   if (SD.exists("blockchain")){
    Serial.println("deleting blockchain file...");
    //SFILE = SD.remove("blockchain", FILE_WRITE); 
  }
  if (SD.exists("ptx")){
    Serial.println("deleting pending transaction file...");
    //SFILE = SD.remove("ptx", FILE_WRITE); 
  }
  if (SD.exists("utxos")){
    Serial.println("deleting utxo set file...");
    //SFILE = SD.remove("utxos", FILE_WRITE); 
  }
  if (SD.exists("blockpointers")){
    Serial.println("deleting blockpointers file...");
    //SFILE = SD.remove("blockpointers", FILE_WRITE); 
  }
  // deleting all fork before remove the dir 
  // deleting all net before remove the dir 
  
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


  SFILE = SD.open("blockchain", FILE_WRITE); // genesis file is 113 o
  unsigned long fsize = SFILE.size();
  SFILE.seek(109); // go to last 4 bytes... 
  byte uintbuff[4];
  SFILE.read(uintbuff,4);
  SFILE.close();
  uint32_t reward = BytesToUint(uintbuff); 
  Serial.println("BLOCKCHAIN SIZE = " + String(fsize) + " and miner reward is " + String(reward));
  
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
  sha.write(gen,255);
  hash = sha.result();
  /* block struct : using SHA256 (32o for every hash). using Ed25519 (elliptic curve modulo 2^255 - 19) 32o private. 32o public. 64o sign
  :: index -> hash -> previoushash -> txnumb (max numb for atmega328p needed)-> txs -> timestamp??? -> MinerToken -> HT??? -> Nonce ???
  4     -> 32   -> 32           -> 1                                    -> ?*152 ->  4???       ->  40   ->  ???  -> ???
  */
  // genesis size : 113 o 
  byte genesis[113]; 
  byte uintbuff[4];
  byte byteOffset = 0; 
  for (i = 0 ; i < 4; i++ ) { genesis[byteOffset] = 0; byteOffset++;} // index
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
  // append all those bytes to blockchain/0 file on sd card
  SFILE = SD.open("blockchain", FILE_WRITE); 
  SFILE.seek(EOF);
  SFILE.write(genesis, 113);
  SFILE.close();
}
//------------ GETTING POINTER AND INDEX ::::: WARNING SHOULD ALSO SUPPORT FORKS FILES 
uint32_t RequestLatestBlockIndex(){
  
  // latest blockIndex is equal to blockpointers size / 4 
  SFILE = SD.open("blockchain", FILE_WRITE); // genesis file is 113 o
  unsigned long fsize = SFILE.size();
  fsize /= 4 ;
  if ( fsize >  1) fsize --; // give the index starting from 0
  return (uint32_t) fsize;  
}

void GetBlockPointerInFile(uint32_t index, byte *buff, String FileName, bool official, uint32_t startingindex){
  
  if ( official ) {
    
      SFILE = SD.open("blockpointers", FILE_READ); 
      SFILE.seek(index*4);  
      SFILE.read(buff,4);
      SFILE.close();
  }
  else{
    
    SFILE = SD.open(FileName, FILE_READ); 
    SFILE.seek(8+(index-startingindex)*4); 
    SFILE.read(buff,4);
    SFILE.close();
  }
  
}

void GetStartingIndexOfBlocksFile(byte *buff, String FileName){
  
     SFILE = SD.open(FileName, FILE_READ); 
     SFILE.read(buff,4);
     SFILE.close();
}
void GetBlockLengthOfBlocksFile(byte *buff, String FileName){
  
     SFILE = SD.open(FileName, FILE_READ);
     SFILE.seek(4); 
     SFILE.read(buff,4);
     SFILE.close();
}
//--------------
// some verif for blocks ( because we cant load in ram ... ) 
void GetHashAtBlockPointer( byte *buff, String FileName, uint32_t bpointer ){ 
 
  SFILE = SD.open(FileName, FILE_READ);
  SFILE.seek(bpointer + 4 );
  SFILE.read(buff,32);
  SFILE.close();
}
void GetPreviousHashAtBlockPointer( byte *buff, String FileName, uint32_t bpointer ){ 
  
  SFILE = SD.open("blockchain", FILE_READ);
  SFILE.seek(bpointer + 36 );
  SFILE.read(buff,32);
  SFILE.close();
}
void GetNumberOfTXAtBlockPointer( byte *buff, String FileName, uint32_t bpointer ){ 
  
  SFILE = SD.open("blockchain", FILE_READ);
  SFILE.seek(bpointer + 68 );
  SFILE.read(buff,1);
  SFILE.close();
}
// --------> NEED GET TX -> spkey rpkey sutxop rutxop coin fee token sign
void GetTXAtBlockPointer( byte *buff, byte txnumb, String FileName, uint32_t bpointer) { // always at the end of the block so txnumb * 144

  
}
void GetTimeStampAtBlockPointer( byte *buff, String FileName, uint32_t bpointer ){ 
 
  SFILE = SD.open("blockchain", FILE_READ);
  SFILE.seek(bpointer + 69 );
  SFILE.read(buff,4);
  SFILE.close();
}
void GetMinerKeyAtBlockPointer( byte *buff, String FileName, uint32_t bpointer ){ 
  
  SFILE = SD.open("blockchain", FILE_READ);
  SFILE.seek(bpointer + 73 ); 
  SFILE.read(buff,32);
  SFILE.close();
}
void GetMinerUTXOPAtBlockPointer( byte *buff, String FileName, uint32_t bpointer ){ 
  
  SFILE = SD.open("blockchain", FILE_READ);
  SFILE.seek(bpointer + 105 );
  SFILE.read(buff,4);
  SFILE.close();
}
void GetMiningRecoltAtBlockPointer( byte *buff, String FileName, uint32_t bpointer ){ 
  
  SFILE = SD.open("blockchain", FILE_READ);
  SFILE.seek(bpointer + 109);
  SFILE.read(buff,4);
  SFILE.close();
}

bool isHashesEqual(byte *a, byte *b){
  for (byte i = 0 ; i < 32; i++ ) {
    if ( a[i] != b[i] ){
      return false;   
    }
  }
  return true;
}
uint32_t GetMiningReward(uint32_t Index){
  uint32_t Reward = 50; // NATIVE REWARD
  while (Index >= 210000) // REWARD_DIVIDER_CLOCK
  {
        Index -= 210000;
        Reward /= 2;
   }
   return Reward;
  
}
void Mine(byte *pkey, uint32_t utxop){
  
  // preparing the next block 
  // get latest index + 1 
  // get prev hash 
  //:: index -> hash -> previoushash -> txnumb (max numb for atmega328p needed)-> txs -> timestamp??? -> MinerToken -> HT??? -> Nonce ???
  // miner token : pkey -> utxop -> mining reward ( dont fuck with tx for the moment ) 
  byte nextblock[81];
  byte uintbuff[4];
  byte i; // iterator
  uint32_t u = RequestLatestBlockIndex();
  // get last block pointer
  GetBlockPointerInFile(u, uintbuff, "blockchain", true, 0 ); // we dont need to provide startingindex when official
  uint32_t lastblockPointer = BytesToUint(uintbuff);
  // get his hash for prev hash 
  byte hash[32];  
  GetHashAtBlockPointer( hash, "blockchain",lastblockPointer ); 
  u++; // inc to get new index
  byte byteOffset = 0;
  UINT32toBytes(u, uintbuff );
  
  for (i = 0 ; i < 4; i++ ) { nextblock[byteOffset] = uintbuff[i]; byteOffset++;}
  // current hash is done at the end
  //byteOffset += 32;  ------------------------------------------------------------------------ !!!!!!
  for (i = 0 ; i < 32; i++ ) { nextblock[byteOffset] = hash[i]; byteOffset++;} // the prevhash
  nextblock[byteOffset] = 0; byteOffset++; // 0 for the tx numb because we dont give a shit
  UINT32toBytes(0, uintbuff ); //zeroing timestamp because its not clear for bimer
  for (i = 0 ; i < 4; i++ ) { nextblock[byteOffset] = uintbuff[i]; byteOffset++;}// timeStamp? : equal to 0 probably
  //--> miner token 
  for (i = 0 ; i < 32; i++ ) { nextblock[byteOffset] = pkey[i]; byteOffset++;} // pkey 
  UINT32toBytes(utxop,uintbuff); 
  for (i = 0 ; i < 4; i++ ) { nextblock[byteOffset] = uintbuff[i]; byteOffset++;} // utxo pointer 
  // coin reward 
   UINT32toBytes(GetMiningReward(u),uintbuff);  // we do 50 
  for (i = 0 ; i < 4; i++ ) { nextblock[byteOffset] = uintbuff[i]; byteOffset++;} // mining  reward 
  // now do the hash of all of this 
  uint8_t *merkleroot; // same as byte ( unsigned 8 bit ...
  sha.write(nextblock,81);
  merkleroot = sha.result();
  // rebuild it 
  byte winnerblock[113];
  byteOffset = 0;
  for (i = 0 ; i < 4; i++ ) { winnerblock[byteOffset] = nextblock[i]; byteOffset++;}
  for (i = 0 ; i < 32; i++ ) { winnerblock[byteOffset] = merkleroot[i]; } // no inc byteoffset here
  for (i = 0 ; i < 77; i++ ) { winnerblock[byteOffset] = nextblock[byteOffset]; byteOffset++;}
  
   // append the winner block ( need to update it with ProcessBlock fonction
  SFILE = SD.open("blockchain", FILE_WRITE); 
  SFILE.seek(EOF);
  SFILE.write(winnerblock, 113);
  SFILE.close();
}

// validation proccess
void ValidateBlocksFile( String FileName ) {
  // [1] first verify if block length and starting index. 

  // preparing local variable needed 
  byte uintbuff[4];
  byte hashbuffA[32]; // will need to hash buffer for comparing hash
  byte hashbuffB[32]; 
  
  GetStartingIndexOfBlocksFile(uintbuff,FileName);
  uint32_t startingIndex = BytesToUint(uintbuff);
  GetBlockLengthOfBlocksFile(uintbuff,FileName);
  uint32_t blockslength = BytesToUint(uintbuff);

  // [2] verify if block continue the chain and no genesis delivered
  uint32_t lastOfficialIndex = RequestLatestBlockIndex(); 
  if ( lastOfficialIndex >= startingIndex + blockslength - 1 || startingIndex == 0 ) return ; 

  // [3] validate the first block of the file
  // [3a] verify previous hash
  GetBlockPointerInFile(startingIndex, uintbuff, FileName, false, startingIndex);
  uint32_t bpointerA = BytesToUint(uintbuff);
  GetPreviousHashAtBlockPointer( hashbuffA, FileName, bpointerA );
  GetBlockPointerInFile(startingIndex - 1, uintbuff, "blockchain", true, 0);
  uint32_t bpointerB = BytesToUint(uintbuff);
  GetHashAtBlockPointer( hashbuffB, "blockchain", bpointerB );
  if ( !isHashesEqual(hashbuffB,hashbuffA)) return; 
  // [3b] verify timestamp 
  // TODO....
  // [3c] verify miner utxo pointer
  // TODO ....
  // [3d] verify txs
  // TODO ....
  // [3e] verify merkle roots
  
  // [4] validate the others
  for (uint32_t i = startingIndex+1; i < startingIndex + blockslength; i++ ) 
  {
    // [4a] verify previous hash
    GetBlockPointerInFile(i, uintbuff, FileName, false, startingIndex);
    uint32_t bpointerA = BytesToUint(uintbuff);
    GetPreviousHashAtBlockPointer( hashbuffA, FileName, bpointerA );
    GetBlockPointerInFile(i - 1, uintbuff, "blockchain", true, 0);
    uint32_t bpointerB = BytesToUint(uintbuff);
    GetHashAtBlockPointer( hashbuffB, "blockchain", bpointerB );
    if ( !isHashesEqual(hashbuffB,hashbuffA)) return; 
    
  }
  // [5] check if fork win. 

  // [6] update files. 

}
void loop() {


}
