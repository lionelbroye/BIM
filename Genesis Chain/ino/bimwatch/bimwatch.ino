
#include <Adafruit_GFX.h>    // Core graphics library
#include <Adafruit_TFTLCD.h> // Hardware-specific library
#include <TouchScreen.h>

#if defined(__SAM3X8E__)
    #undef __FlashStringHelper::F(string_literal)
    #define F(string_literal) string_literal
#endif


#define YP A1  // must be an analog pin, use "An" notation!
#define XM A2  // must be an analog pin, use "An" notation!
#define YM 7   // can be a digital pin
#define XP 6   // can be a digital pin

#define TS_MINX 150
#define TS_MINY 120
#define TS_MAXX 920
#define TS_MAXY 940


TouchScreen ts = TouchScreen(XP, YP, XM, YM, 300);
//TouchScreen ts = TouchScreen(YP, XP, YM, XM, 300);
#define LCD_CS A3
#define LCD_CD A2
#define LCD_WR A1
#define LCD_RD A0
// optional
#define LCD_RESET A4

// Assign human-readable names to some common 16-bit color values:
#define	BLACK   0x0000
#define	BLUE    0x001F
#define	RED     0xF800
#define	GREEN   0x07E0
#define CYAN    0x07FF
#define MAGENTA 0xF81F
#define YELLOW  0xFFE0
#define WHITE   0xFFFF

Adafruit_TFTLCD tft(LCD_CS, LCD_CD, LCD_WR, LCD_RD, LCD_RESET);

int currentcolor;

bool _receiving = false;
byte data_x[120];
byte data_y[120];
int touchcounter = 0;
int notouchcounter = 0;

byte buff[1000]; // max data that can be retain
byte buff_c;

byte PAGE_X = 0;
byte PAGE_Y = 0;

void setup(void) {
  
  Serial.begin(9600);
  uint16_t identifier = 0x9325;
  tft.begin(identifier);

  pinMode(13, OUTPUT);
   tft.setRotation(0);
  currentcolor = WHITE;
  tft.fillScreen(BLACK);
  delay(1000);
  B_B(false);
  

  
}

#define MINPRESSURE 0
#define MAXPRESSURE 1000

void GraphicTest(){

  for (int i = 0 ; i < 20 ; i ++ ) {
tft.setRotation(i);
  tft.fillScreen(BLACK);
// some exemple here! 
  tft.setCursor(0, 0);
  tft.setTextColor(WHITE);  tft.setTextSize(10);
  tft.println("Hello BIMER!");

  tft.drawLine(0, 0, 50,50, WHITE);
 tft.drawRect(0, 0, 30, 30, WHITE);
  tft.fillCircle(50, 50, 50, BLUE);
  tft.drawCircle(60, 60, 70, WHITE);
  tft.drawTriangle( 50,50,60,60,70,70,WHITE);
    delay(1000);
  }
  
}

void H_C(bool _onlydata){ // Hash and Sea. Hash and See.

 if (!_onlydata){
  tft.fillScreen(BLACK);
  // print the moon CYCLE
  byte diff = 5; 
  byte pos = 50; 
  for (int i = 0 ; i < 5 ; i++ ) {
    tft.fillCircle(pos, 50, 20, WHITE);
    tft.fillCircle(pos+diff, 50, 20, BLACK);
    pos += 35; 
    diff += 6;
  }
  
 }
  // the hash will be contains in the first 64 bytes
  tft.setCursor(0, 0);
  tft.setTextColor(WHITE);  tft.setTextSize(5);
  byte stuff[64];
  for (int i = 0 ; i < 64 ; i++ ) stuff[i] = buff[i];
  String myString = String((char *)stuff);
  tft.println(myString);

}

void DrawCube(byte x, byte y, byte s){

  tft.drawRect(x, y, s, s, WHITE);
  tft.drawRect(x+s, y+s, s, s, WHITE);
  tft.drawLine(x, x+s, x+s,y+s, WHITE);
  tft.drawLine(120, 160, 160,200, WHITE);
  tft.drawLine(120, 100, 160,140, WHITE);
  tft.drawLine(60, 160, 100,200, WHITE);
}
void B_B(bool _onlydata){
   tft.fillScreen(0x29);
 
// draw the moon inside
  tft.fillCircle(115, 150, 20, WHITE);
  tft.fillCircle(127, 150, 20, BLACK);
// draw the cube
  tft.drawRect(60, 100, 60, 60, WHITE);
  tft.drawRect(100, 140, 60, 60, WHITE);
  tft.drawLine(60, 100, 100,140, WHITE);
  tft.drawLine(120, 160, 160,200, WHITE);
  tft.drawLine(120, 100, 160,140, WHITE);
  tft.drawLine(60, 160, 100,200, WHITE);
    
  

// number of block
 tft.setCursor(0, 0);
  tft.setTextColor(BLUE);  tft.setTextSize(3);
  tft.println("BLOCKCHAIN SIZE");
  tft.setCursor(50, 50);
  tft.setTextColor(WHITE);  tft.setTextSize(2);
  byte stuff[64];
  for (int i = 0 ; i < 64 ; i++ ) stuff[i] = buff[i];
  String s = String((char *)stuff);
  s+= " blocks";
  tft.println(s);
 
}
float BytesToFloat32(byte a, byte b, byte c, byte d ) {

  byte incoming[4]={a,b,c,d};
  return *( (float*) incoming );
}
uint32_t BytesToUintB(byte a, byte b, byte c, byte d ){
   uint32_t foo;
 // big endian
  foo = (uint32_t) d << 24;
  foo |=  (uint32_t) c << 16;
  foo |= (uint32_t) b << 8;
  foo |= (uint32_t) a;
  return foo; 
}
uint32_t BytesToUint(byte a, byte b, byte c, byte d ){
   byte incoming[4]={a,b,c,d};
  return *( (uint32_t*) incoming );
}

void T_B(bool _onlydata){ // -_-_-_-_-_-_--_-_-_- LOT OF OPTIMISATIONS NEEDED HERE!!!! -_-_-_-_-_-_-_-_-
  tft.fillScreen(BLACK);
  if ( !_onlydata ) {

  
  }
     // draw the moon inside
  tft.fillCircle(115, 150, 20, WHITE);
  tft.fillCircle(127, 150, 20, BLACK);
// draw the cube
  tft.drawRect(60, 100, 60, 60, WHITE);
  tft.drawRect(100, 140, 60, 60, WHITE);
  tft.drawLine(60, 100, 100,140, WHITE);
  tft.drawLine(120, 160, 160,200, WHITE);
  tft.drawLine(120, 100, 160,140, WHITE);
  tft.drawLine(60, 160, 100,200, WHITE);
  // value from 0.0 to like 10.0. we wiil get a precision to *10 
  tft.setCursor(0, 0);
  tft.setTextColor(WHITE);  tft.setTextSize(2);

  //  // 7 * [4-4] ( float-uint)
  //  //uint32_t val = BytesToUint(buff[0],buff[1],buff[2],buff[3]); 
  //     float f = BytesToFloat32(buff[0],buff[1],buff[2],buff[3]); 

  String s;
  uint32_t ts;
  float water_value; 
  uint32_t lastW = 0;
  uint32_t lastT = 0;
  for (int i = 0 ; i < 7; i++ ) {
     
     water_value =  BytesToFloat32(buff[i*8],buff[(i*8)+1],buff[(i*8)+2],buff[(i*8)+3]); 
     ts =  BytesToUintB(buff[(i*8)+4],buff[(i*8)+5],buff[(i*8)+6],buff[(i*8)+7]); // this is time offset in second
  
      tft.setCursor(0, i*30);
      s = String(water_value);
       tft.println(s);
       tft.setCursor(180, i*30);
      s = String(ts);
      tft.println(s);
      // some shitty graphics 
      
      uint32_t test = (uint32_t) (water_value * 100);
      //Serial.println(String(i) + " " + ts);
      //tft.fillCircle(i*35, test, 10, WHITE);
      tft.fillCircle(lastT + (ts/8), test, 10, WHITE);
      if ( i > 0 ) {
       // tft.drawLine(i*35, test, (i-1)*35, lastW, WHITE);
       tft.drawLine(lastT+ (ts/8), test, lastT , lastW, WHITE);
      }
      lastW = test;
      lastT += (ts/8);
       //Serial.println(String(water_value));
       
  }
  uint32_t blockNumb = 0;
  uint32_t TxNumb = 0;

  blockNumb = BytesToUintB(buff[56],buff[57],buff[58],buff[59]);
  TxNumb =BytesToUintB(buff[60],buff[61],buff[62],buff[63]);
  //Serial.println(String(blockNumb) + " " + String(TxNumb));
 tft.setTextSize(2);
 tft.setTextColor(BLUE);
 tft.setCursor(0, 230);
 s = "BLOCK MINED : ";
 s += String(blockNumb);
 tft.println(s);
 tft.setCursor(0, 250);
 s = "TX MINED : ";
 s += String(TxNumb);
 tft.println(s);
 
}
void Test_Parsing(){ // this is ok here ... 

  tft.fillScreen(BLUE);
   tft.setTextSize(2);
 tft.setTextColor(WHITE);
  uint32_t val = 0;
  String s;
   for (byte i = 0 ; i < 10; i++ ) { // receiving an array of 10 uint 11 + 11 ++ 11 +1 1 +1 1 -- - and over 
    
     val =  BytesToUintB(buff[i*4],buff[(i*4)+1],buff[(i*4)+2],buff[(i*4)+3]); 
      tft.setCursor(0, i*30);
      s = String(val);
       tft.println(s);
  }
}

void UpdatePage(int dirX, int dirY, bool load)
{
  if ( dirX == -1 && PAGE_X == 0 ) return;
  if ( dirY == -1 && PAGE_Y == 0 ) return;
  PAGE_X += dirX; 
  //PAGE_Y += dirY;
  if ( dirX > 1  ) dirX = 1;
  if (load) LoadPage(false);
}

void LoadPage(bool _onlydata){

// here the shit;
  if ( PAGE_X == 0 && PAGE_Y == 0 ) {
    if ( !_onlydata) {Serial.println("RBC"); }
    B_B(_onlydata); return;
  }
  if ( PAGE_X == 1 && PAGE_Y == 0 ) {
    if ( !_onlydata) {Serial.println("RLH"); }
    H_C(_onlydata); return;
  }
  if ( PAGE_X == 2 && PAGE_Y == 0 ) {
    if ( !_onlydata) {Serial.println("RCC"); }
    T_B(_onlydata); return;
  }
  if ( PAGE_X == 3 && PAGE_Y == 0 ) {
    if ( !_onlydata) {Serial.println("TTT"); }
    Test_Parsing(); return;
  }

}

void DetectDir(){
    // minimum de 3 points obligé.
  // get the lowest x and his index pos same with highest and stuff

  byte lX = 255 ;  
  byte hX = 0;
  byte lY = 255 ;
  byte hY = 0;
   byte lXI = 0;
  byte hXI = 0;
   byte lYI = 0;
  byte hYI = 0;

  byte pCount = 0; 
  for (int i = 1; i < 120; i++ ){
      if ( data_x[i] != 0 && data_y[i] != 0 ) {
        pCount ++ ;
        if ( data_x[i] < lX ) {
            lX = data_x[i];
            lXI = i;
        }
        if ( data_x[i] > hX ) {
            hX = data_x[i];
            hXI = i;
        }
        if ( data_y[i] < lY ) {
            lY = data_y[i];
            lYI = i;
        }
        if ( data_x[i] > hY ) {
            hY = data_y[i];
            hYI = i;
        }
        
      }
  }
  byte threshold = 50; 
  if ( pCount > 2 ) { // minimum 3 we said
      byte distX = hX - lX;
      byte distY = hY- lY;

      if ( distX > threshold && lXI < hXI ) { // vers la droite
      UpdatePage(1,0, true);
      ResetStuff();
  return;
      }
      if ( distX > threshold && lXI > hXI ) { // vers la gauhce
      UpdatePage(-1,0, true);
      ResetStuff();
return;
      }
      if ( distY > threshold && lYI < hYI ) { // vers le bas
      ResetStuff();
return;
      }
      if ( distY > threshold &&  lYI > hYI ) {// vers le haut
      ResetStuff();
return;
      }
      
  }
  
}

void ResetStuff(){
       touchcounter = 0;
  notouchcounter = 0; 
  for (int i = 0 ; i < 120 ; i++ ) {
    data_x[i] = 0; 
    data_y[i] = 0;
  }

}

void ReceiveCOM(){
  
  if ( Serial.available()> 0 ) {  
    
      byte incomingByte = Serial.read();
        
      if ( incomingByte > -1 ) {
          _receiving = true;
          buff[buff_c] = incomingByte;
          buff_c++;
          if ( buff_c > 999 ) {
              buff_c = 0;   
          }
        
      }
  }  
  else{
    if ( _receiving == true ) {
      _receiving = false; 
        LoadPage(true); 
    }
    buff_c = 0; 
    for (int i = 0 ; i < 999; i++ ){
        buff[i] = 0;
    }
  }
}

void loop()
{
  digitalWrite(13, HIGH);
  TSPoint p = ts.getPoint();
  digitalWrite(13, LOW);
  pinMode(XM, OUTPUT);
  pinMode(YP, OUTPUT);
//DRAW : 
  if (p.z > MINPRESSURE && p.z < MAXPRESSURE) {
  notouchcounter = 0;
  
    // scale from 0->1023 to tft.width
  p.x = map(p.x, TS_MINX, TS_MAXX, 0, tft.width());
  p.y = map(p.y, TS_MINY, TS_MAXY, tft.height(), 0);
  p.x = p.x + 25;
  p.y = p.y - 30;
  tft.drawPixel(p.y, p.x, currentcolor);
  data_x[touchcounter] = p.x;
  data_y[touchcounter] = p.y;
  touchcounter ++;
  DetectDir();
}
else{
  notouchcounter++;
}


if ( notouchcounter > 50 ) {
  ResetStuff();
}

ReceiveCOM(); 

delay(20);
 
}
