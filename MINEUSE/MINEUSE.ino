// ok our blockchain can't exceed 4GB because of FAT32 format ok
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
   utxo file : 
   same like first 4 bytes are currency volume then -> public key(32) -> token of uniquess(4) -> sold(4)
  */
  /*BECAUSE TX ARE NOT FIXED SIZE : WE SERIALIZE TX AT THE END OF THE BLOCK !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! */
  // to avoid huge ram usage : we should hash block like this : hash the index. then hash the previous hash with the index hash ... etc. 
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
  // append to the blockchain file 
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
void GetNumberOfTXAtBlockPointer( byte *buff, String FileName, uint32_t bpointer ){ // max tx per block
  
  SFILE = SD.open("blockchain", FILE_READ);
  SFILE.seek(bpointer + 68 );
  SFILE.read(buff,1);
  SFILE.close();
}
// --------> NEED GET TX -> spkey(0) rpkey(32) sutxop(64) rutxop(68) coin(72) fee(76) timer(80) token sign (84)

void GetSenderPublicKeyAtBlockPointer(byte *buff, byte txnumb, String FileName, uint32_t bpointer){
  // START OFFSET IS 113
   SFILE = SD.open(FileName, FILE_READ);
   SFILE.seek(113 +  (txnumb*148) );
   SFILE.read(buff,32);
   SFILE.close();
}
void GetReceiverPublicKeyAtBlockPointer(byte *buff, byte txnumb, String FileName, uint32_t bpointer){
  // START OFFSET IS 113
   SFILE = SD.open(FileName, FILE_READ);
   SFILE.seek(113 + 32 + (txnumb*148) );
   SFILE.read(buff,32);
   SFILE.close();
}
void GetSenderUTXOPAtBlockPointer(byte *buff, byte txnumb, String FileName, uint32_t bpointer){
  // START OFFSET IS 113
   SFILE = SD.open(FileName, FILE_READ);
   SFILE.seek(113 + 64 + (txnumb*148) );
   SFILE.read(buff,4);
   SFILE.close();
}
void GetReceiverUTXOPAtBlockPointer(byte *buff, byte txnumb, String FileName, uint32_t bpointer){
  // START OFFSET IS 113
   SFILE = SD.open(FileName, FILE_READ);
   SFILE.seek(113 + 68 + (txnumb*148) );
   SFILE.read(buff,4);
   SFILE.close();
}
void GetTxAmountAtBlockPointer(byte *buff, byte txnumb, String FileName, uint32_t bpointer){
  // START OFFSET IS 113
   SFILE = SD.open(FileName, FILE_READ);
   SFILE.seek(113 + 72 + (txnumb*148) );
   SFILE.read(buff,4);
   SFILE.close();
}
void GetFeeAtBlockPointer(byte *buff, byte txnumb, String FileName, uint32_t bpointer){
  // START OFFSET IS 113
   SFILE = SD.open(FileName, FILE_READ);
   SFILE.seek(113 + 76 + (txnumb*148) );
   SFILE.read(buff,4);
   SFILE.close();
}
void GetPurishmentTimeAtBlockPointer(byte *buff, byte txnumb, String FileName, uint32_t bpointer){
  // START OFFSET IS 113
   SFILE = SD.open(FileName, FILE_READ);
   SFILE.seek(113 + 80 + (txnumb*148) );
   SFILE.read(buff,4);
   SFILE.close();
}
void GetTXSignatureAtBlockPointer(byte *buff, byte txnumb, String FileName, uint32_t bpointer){
  // START OFFSET IS 113
   SFILE = SD.open(FileName, FILE_READ);
   SFILE.seek(113 + 84 +  (txnumb*148) );
   SFILE.read(buff,64);
   SFILE.close();
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
///////////////////// UTXO GET
void GetPublicKeyAtUtxoPointer( byte *buff, uint32_t bpointer ){ 
  
  SFILE = SD.open("utxos", FILE_READ);
  SFILE.seek(bpointer );
  SFILE.read(buff,32);
  SFILE.close();
}
void GetTokenOfUniquenessAtUtxoPointer( byte *buff, uint32_t bpointer ){ 
  
  SFILE = SD.open("utxos", FILE_READ);
  SFILE.seek(bpointer + 32 );
  SFILE.read(buff,4);
  SFILE.close();
}
void GetSoldAtUtxoPointer( byte *buff, uint32_t bpointer ){ 
  
  SFILE = SD.open("utxos", FILE_READ);
  SFILE.seek(bpointer + 36 );
  SFILE.read(buff,4);
  SFILE.close();
}

uint32_t GetKeyPointerInTemporaryUTXOS(byte *a) {
  
  SFILE = SD.open("temputxos", FILE_READ);
  // get the number of utxos ... 
  byte uintbuff[4];
  byte hashbuff[32]; 
  SFILE.read(uintbuff,4);
  uint32_t tuxon = BytesToUint(uintbuff); 
  uint32_t byteOffset = 4; 
  for (uint32_t i = 0 ; i < tuxon; i++ ){
    SFILE.seek(byteOffset);
    SFILE.read(hashbuff,32);
    if ( isHashesEqual(hashbuff, a) ) { 
      SFILE.seek(byteOffset+32);
      SFILE.read(uintbuff,4);
      return BytesToUint(uintbuff); 
      }
    byteOffset += 36; // reminder pkey + temporary pointer
  }
  return 0;
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
  // preparing some stuff here
  byte hashbuff[32]; 
  byte uintbuff[4];
  byte leaf[64]; 
  uint8_t *merkleroot;

  SFILE = SD.open("fork/w", FILE_WRITE); 
   
  uint32_t u = RequestLatestBlockIndex();
  // get is pointer
  GetBlockPointerInFile(u, uintbuff, "blockchain", true, 0 ); // we dont need to provide startingindex when official
  uint32_t lastblockPointer = BytesToUint(uintbuff);
  
  u++; // inc to get new index
  
  // writing  header data  [ starting index(uint32) -> number of block(uint32) ->  blockpointers (uint32 * nb) ]
   SFILE.seek(EOF);
   UINT32toBytes(u,uintbuff);
   SFILE.write(uintbuff, 4);
   SFILE.seek(EOF);
   UINT32toBytes(1,uintbuff);
   SFILE.write(uintbuff, 4);
   SFILE.seek(EOF);
   UINT32toBytes(12,uintbuff); // 12 cause always start at 12 ( we dont mine from fork for the moment )
   SFILE.write(uintbuff, 4);
   
  //writing Index
  UINT32toBytes(u,uintbuff);
  SFILE.seek(EOF);
  SFILE.write(uintbuff, 4);
  // crafting the merkle root : INDEX
  sha.write(uintbuff,4);
  merkleroot = sha.result(); 

  // create zero 32 bytes ( to prepare merkle root )
  SFILE.seek(EOF);
  SFILE.write(hashbuff, 32);
  
  //writing prev hash 
  GetHashAtBlockPointer( hashbuff, "blockchain",lastblockPointer ); 
  SFILE.seek(EOF);
  SFILE.write(hashbuff, 32);
  // crafting the merkle root : Previous hash
  for (byte i = 0; i < 32; i++ ) { leaf[i] = merkleroot[i]; } // update leaf side A
  for (byte i = 32; i < 64; i++ ) { leaf[i] = hashbuff[i-32]; } // update leaf side B
  sha.write(leaf,64);
  merkleroot = sha.result(); // save it to phash

  //writing tx numb (0) 
  UINT32toBytes(0,uintbuff);
  SFILE.seek(EOF);
  SFILE.write(uintbuff, 4);
  // crafting the merkle root : tx numb
  for (byte i = 0; i < 32; i++ ) { leaf[i] = merkleroot[i]; } // update leaf side A
  for (byte i = 32; i < 64; i++ ) { if ( i < 36 ) { leaf[i] = uintbuff[i-32];} else { leaf[i] = 0; } } // update leaf side B
  sha.write(leaf,64);
  merkleroot = sha.result(); // save it to phash

  //writing timestamp (0) 
  UINT32toBytes(0,uintbuff);
  SFILE.seek(EOF);
  SFILE.write(uintbuff, 4);
  // crafting the merkle root : timestamp
  for (byte i = 0; i < 32; i++ ) { leaf[i] = merkleroot[i]; } // update leaf side A
  for (byte i = 32; i < 64; i++ ) { if ( i < 36 ) { leaf[i] = uintbuff[i-32];} else { leaf[i] = 0; } } // update leaf side B
  sha.write(leaf,64);
  merkleroot = sha.result(); // save it to phash

  //writing miner token public key
  SFILE.seek(EOF);
  SFILE.write(pkey, 32);
  // crafting the merkle root : miner token public key
  for (byte i = 0; i < 32; i++ ) { leaf[i] = merkleroot[i]; } // update leaf side A
  for (byte i = 32; i < 64; i++ ) { leaf[i] = pkey[i-32]; } // update leaf side B
  sha.write(leaf,64);
  merkleroot = sha.result(); // save it to phash

 //writing miner utxop 
  UINT32toBytes(utxop,uintbuff);
  SFILE.seek(EOF);
  SFILE.write(uintbuff, 4);
  // crafting the merkle root :  miner utxop
  for (byte i = 0; i < 32; i++ ) { leaf[i] = merkleroot[i]; } // update leaf side A
  for (byte i = 32; i < 64; i++ ) { if ( i < 36 ) { leaf[i] = uintbuff[i-32];} else { leaf[i] = 0; } } // update leaf side B
  sha.write(leaf,64);
  merkleroot = sha.result(); // save it to phash

  // writing recolt
  UINT32toBytes(GetMiningReward(u),uintbuff);
  SFILE.seek(EOF);
  SFILE.write(uintbuff, 4);
  // crafting the merkle root :  miner recolt
  for (byte i = 0; i < 32; i++ ) { leaf[i] = merkleroot[i]; } // update leaf side A
  for (byte i = 32; i < 64; i++ ) { if ( i < 36 ) { leaf[i] = uintbuff[i-32];} else { leaf[i] = 0; } } // update leaf side B
  sha.write(leaf,64);
  merkleroot = sha.result(); // save it to phash

  // end : the root
  SFILE.seek(16);
  SFILE.write(merkleroot, 32);
  
  
  SFILE.close();
}

// validation proccess
bool ValidateBlocksFile( String FileName ) {
  // [1] first verify if block length and starting index. 

  // preparing local variable needed 
  byte uintbuff[4];
  
  GetStartingIndexOfBlocksFile(uintbuff,FileName);
  uint32_t startingIndex = BytesToUint(uintbuff);
  GetBlockLengthOfBlocksFile(uintbuff,FileName);
  uint32_t blockslength = BytesToUint(uintbuff);

  // [2] verify if block continue the chain and no genesis delivered
  uint32_t lastOfficialIndex = RequestLatestBlockIndex(); 
  if ( lastOfficialIndex >= startingIndex + blockslength - 1 || startingIndex == 0 ) return false; 

  //[3] Generate a temporary utxos 
  // number of utxo(4)-> list of (public key and pointer ) -> sold and tou 

  //[4] verify first block
  if ( !VerifyBlock( startingIndex, FileName, true ) ) {return false; }
  
  
  // [5] validate the others // only if needed ( file is more than one block )
  if ( blockslength > 1 ) {
    for (uint32_t i = startingIndex+1; i < startingIndex + blockslength; i++ ) 
    {
      if ( !VerifyBlock( i, FileName, false ) ) { return false; }
    }
  }
  
  
  return true;
  // does this should be in another function ???? idk 
  // delete temporary utxos after this function is called if no win distance or result is false
  // [5] check if fork win. 

  // [6] update files. (if fork win : update utxos from temporary utxo, append blocks.) 

}

bool VerifyBlock(uint32_t startingIndex, String FileName, bool isfirstblock ) {

  // preparing local variable needed 
  byte uintbuff[4];
  byte hashbuffA[32]; 
  byte hashbuffB[32]; 
  
  // [3] validate the first block of the file
  // [3a] verify previous hash --> we only need pointer  in blockchain file if isfirstblock is true
  GetBlockPointerInFile(startingIndex, uintbuff, FileName, false, startingIndex);
  uint32_t bpointerA = BytesToUint(uintbuff); // pointer of block to verify 
  GetPreviousHashAtBlockPointer( hashbuffA, FileName, bpointerA );
  if ( isfirstblock ) {
    GetBlockPointerInFile(startingIndex - 1, uintbuff, "blockchain", true, 0);
  }
  else{
    GetBlockPointerInFile(startingIndex - 1, uintbuff, FileName, false, startingIndex - 1);
  }
  
  uint32_t bpointerB = BytesToUint(uintbuff);
  if ( isfirstblock ) {
    
     GetHashAtBlockPointer( hashbuffB, "blockchain", bpointerB );
  }
  else{
     GetHashAtBlockPointer( hashbuffB, FileName, bpointerB );
  }
 
  if ( !isHashesEqual(hashbuffB,hashbuffA)) return false; 
  // [3b] verify timestamp 
  GetTimeStampAtBlockPointer(uintbuff, FileName, bpointerA ); 
  // then TODO...
  // [3c] verify miner utxo pointer
  GetMinerUTXOPAtBlockPointer( uintbuff, FileName, bpointerA );
  uint32_t pointer = BytesToUint(uintbuff);
  if ( pointer > 0 ) {
    GetPublicKeyAtUtxoPointer(hashbuffA, pointer); 
    GetMinerKeyAtBlockPointer(hashbuffB, FileName, bpointerA);
    if ( !isHashesEqual(hashbuffB,hashbuffA)) return false; 
  }
  // [3d] verify txs // total amount is compute there
  // update the temp utxo set of the miner ... 
  
  uint32_t recompense; // this  is sum at every tx ( amount + fee );
  GetNumberOfTXAtBlockPointer( uintbuff, FileName, bpointerA );
  uint32_t txn = BytesToUint(uintbuff);
  
    // verify every tx.
    
  for ( byte i = 0 ; i < txn ; i++ ) {
       
    
    
    // [3d a] check purishment time
    GetPurishmentTimeAtBlockPointer(uintbuff, i, FileName, bpointerA);
    // TODO
    // [3d a] check if sender pointer is correct

    // [3d b] check if dust is needed ( fee will be higher if so ) 

    // if no dust check if  receiver pointer is correct
    
     // check if dust needed. check if sold is ok for amount + fee. check if utxo pointer are correct ... 
     
    // [3d c] check if fee is higher than minimum
    
    // check if we need the validation from official utxos or on temporary utxos. 
    uint32_t tempPointer = GetKeyPointerInTemporaryUTXOS(pkey);
     // [3d d] check if sold is suffisant

    // [3d f] check if tou is correct 
    
    if ( tempPointer > 0 ) {
      
    }
    else{
      
    }
   
    // check the signature
    // update the temp utxo set ... 
    
    // sum recompense and return true
     
  }
  // [3e] verify miner recolt correctness
  GetMiningRecoltAtBlockPointer( uintbuff, FileName, bpointerA ) ;
  uint32_t recolt = BytesToUint(uintbuff);
  if ( recolt > recompense ) return false; 
  
  // [3d] verify merkle roots  
  byte leaf[64]; 
  uint8_t *merkleroot;

  // index
  UINT32toBytes(startingIndex,uintbuff);
  sha.write(uintbuff,4);
  merkleroot = sha.result(); // save it to phash

  // phash
  for (byte i = 0; i < 32; i++ ) { leaf[i] = merkleroot[i]; } // update leaf side A
  GetPreviousHashAtBlockPointer( hashbuffA, FileName, bpointerA );
  for (byte i = 32; i < 64; i++ ) { leaf[i] = hashbuffA[i-32]; } // update leaf side B
  sha.write(leaf,64);
  merkleroot = sha.result(); // save it to phash

  // txnumb
  for (byte i = 0; i < 32; i++ ) { leaf[i] = merkleroot[i]; } // update leaf side A
  GetNumberOfTXAtBlockPointer( uintbuff, FileName, bpointerA );
 // txn is define before
  for (byte i = 32; i < 64; i++ ) { if ( i < 36 ) { leaf[i] = uintbuff[i-32];} else { leaf[i] = 0; } } // update leaf side B ( zeroing the last 28 bytes is needed for better comprehension)
  sha.write(leaf,64);
  merkleroot = sha.result(); // save it to phash

  // timestamp
  for (byte i = 0; i < 32; i++ ) { leaf[i] = merkleroot[i]; } // update leaf side A
  GetTimeStampAtBlockPointer( uintbuff, FileName, bpointerA );
  for (byte i = 32; i < 64; i++ ) { if ( i < 36 ) { leaf[i] = uintbuff[i-32];} else { leaf[i] = 0; } } // update leaf side B
  sha.write(leaf,64);
  merkleroot = sha.result(); // save it to phash

  // miner key
  for (byte i = 0; i < 32; i++ ) { leaf[i] = merkleroot[i]; } // update leaf side A
  GetMinerKeyAtBlockPointer( hashbuffA, FileName, bpointerA );
  for (byte i = 32; i < 64; i++ ) { leaf[i] = hashbuffA[i-32]; } // update leaf side B
  sha.write(leaf,64);
  merkleroot = sha.result(); // save it to phash

  // miner utxo pointer
  for (byte i = 0; i < 32; i++ ) { leaf[i] = merkleroot[i]; } // update leaf side A
  GetMinerUTXOPAtBlockPointer( uintbuff, FileName, bpointerA );
  for (byte i = 32; i < 64; i++ ) { if ( i < 36 ) { leaf[i] = uintbuff[i-32];} else { leaf[i] = 0; } } // update leaf side B
  sha.write(leaf,64);
  merkleroot = sha.result(); // save it to phash

  // miner recolt 
  for (byte i = 0; i < 32; i++ ) { leaf[i] = merkleroot[i]; } // update leaf side A
  GetMiningRecoltAtBlockPointer( uintbuff, FileName, bpointerA );
  for (byte i = 32; i < 64; i++ ) { if ( i < 36 ) { leaf[i] = uintbuff[i-32];} else { leaf[i] = 0; } } // update leaf side B
  sha.write(leaf,64);
  merkleroot = sha.result(); // save it to phash

  // txs ??
  for (byte  i = 0 ; i < txn ; i++ ) { } // TODO

  // now verify
  GetHashAtBlockPointer( hashbuffB, FileName, bpointerA );
  if ( !isHashesEqual(hashbuffB,merkleroot)) return false; 

  
  return true;
}
void loop() {


}
