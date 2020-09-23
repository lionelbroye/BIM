using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Xml;

namespace firstchain
{
    class SHOM
    {
        // MAX 1 MOIS DINTERVALLE PAR REQUETE
        // 

        public class SHOMData
        {
            ////{"idstation":22,"idsource":1,"value":0.6337,"timestamp":"2020/09/23 10:37:20"}]}
            public uint idstation { get; }
            public uint idsource { get; }
            public float value { get; }
            public DateTime timestamp { get; }
            public SHOMData ( uint station, uint source ,float val, DateTime ts)
            {
                this.idstation = station;
                this.idsource = source;
                this.value = val;
                this.timestamp = ts;
            
            }

            public void print()
            {
                Console.WriteLine("id de la station = " + this.idstation);
                Console.WriteLine("hauteur de l'eau = " + this.value);
                Console.WriteLine("Timestamp        = " + this.timestamp.ToString());

            }
        }
        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }
        public static List<SHOMData> GetShomDatafromPeriod(int station, uint tsStart, uint tsEnd)
        {
            //-----------------------------------------------------------> { WARNING }ask multiple time the shit if sup than one month or less that one day ...  


            DateTime dateStart = UnixTimeStampToDateTime(tsStart);
            dateStart = dateStart.AddMinutes(-15);
            DateTime dateEnd = UnixTimeStampToDateTime(tsEnd);
            string datestartstr = dateStart.Year.ToString() + "-" + dateStart.Month.ToString() + "-" + dateStart.Day.ToString();
            string dateendstr = dateEnd.Year.ToString() + "-" + dateEnd.Month.ToString() + "-" + dateEnd.Day.ToString();
            string sURL;
            sURL = "https://services.data.shom.fr/maregraphie/observation/json/" + station.ToString() + "?sources=1&dtStart=" + datestartstr + "&dtEnd=" + dateendstr;

            WebRequest wrGETURL;
            wrGETURL = WebRequest.Create(sURL);

            WebProxy myProxy = new WebProxy("myproxy", 80);
            myProxy.BypassProxyOnLocal = true;

            wrGETURL.Proxy = WebProxy.GetDefaultProxy();

            Stream objStream;
            try
            {
                objStream = wrGETURL.GetResponse().GetResponseStream();
            }
            catch (Exception e)
            {
                Console.WriteLine("Server not responding with current argument.");
                return new List<SHOMData>();
            }


            StreamReader objReader = new StreamReader(objStream);
            string dtreceived = "";
            string sLine = "";

            while (sLine != null)
            {
                sLine = objReader.ReadLine();
                if (sLine != null)
                    dtreceived = sLine;
            }

            return GetAllSHOMData(dtreceived.ToCharArray());

        }
        public static void TestSHOMData(int station, DateTime dateStart, DateTime dateEnd)
        {
            // annee-mois-jour
            Console.WriteLine(dateStart.Year.ToString());
            Console.WriteLine(dateStart.Month.ToString());
            Console.WriteLine(dateStart.Day.ToString());
            string datestartstr = dateStart.Year.ToString() + "-" + dateStart.Month.ToString() + "-" + dateStart.Day.ToString();
            string dateendstr = dateEnd.Year.ToString() + "-" + dateEnd.Month.ToString() + "-" + dateEnd.Day.ToString();
            string sURL;
            sURL = "https://services.data.shom.fr/maregraphie/observation/json/"+station.ToString()+"?sources=1&dtStart="+datestartstr+"&dtEnd=" + dateendstr;

            WebRequest wrGETURL;
            wrGETURL = WebRequest.Create(sURL);

            WebProxy myProxy = new WebProxy("myproxy", 80);
            myProxy.BypassProxyOnLocal = true;

            wrGETURL.Proxy = WebProxy.GetDefaultProxy();
            
            Stream objStream;
            try
            {
                objStream = wrGETURL.GetResponse().GetResponseStream();
            }
            catch ( Exception e)
            {
                Console.WriteLine("Server not responding with current argument.");
                return; 
            }
            

            StreamReader objReader = new StreamReader(objStream);
            string dtreceived = "" ; 
            string sLine = "";

            while (sLine != null)
            {
                sLine = objReader.ReadLine();
                if (sLine != null)
                    dtreceived = sLine;
            }
            

            // ----------------------------------------> ANOTHER EXAMPLE OF THE LAST DATA
                Console.WriteLine("_______________________________");
                Console.WriteLine("     Latest data received      ");
                Console.WriteLine("_______________________________");
                SHOMData lastresult = GetLastData(dtreceived.ToCharArray());
                lastresult.print();
                // 11/15/20 ... lol ... 
                Console.WriteLine("_______________________________");
                Console.WriteLine("   Specific data at 11:15:00   ");
                Console.WriteLine("_______________________________");
                DateTime datedesired = new DateTime(dateStart.Year, dateStart.Month, dateStart.Day, 11, 15, 0);
                SHOMData searchresult = GetDataAtSpecificTime(datedesired, GetAllSHOMData(dtreceived.ToCharArray()));
                if ( searchresult != null)
                {
                    searchresult.print();
                }
                else
                {
                    Console.WriteLine("not found...");
                }
                Console.WriteLine("_______________________________");
                Console.WriteLine("   Times where water was between 0.55 and 0.60    ");
                Console.WriteLine("_______________________________");
                List<SHOMData> shoms = GetTimeStampWaterLevel(GetAllSHOMData(dtreceived.ToCharArray()), 0.55f, 0.60f);
                if ( shoms.Count > 0)
                {
                    Console.WriteLine(shoms[0].timestamp.ToString() + " to " + shoms[shoms.Count - 1].timestamp.ToString());
                }
                else
                {
                    Console.WriteLine("water never between those value today :!");
                }
                Console.WriteLine("_______________________________");
                Console.WriteLine("   Lowest and highest water value from this request   ");
                Console.WriteLine("_______________________________");
                float[] result = GetLowestandHighestWaterLevel(GetAllSHOMData(dtreceived.ToCharArray()));
                Console.WriteLine(" niveau de l'eau le plus bas durant cette periode  :  " + result[0].ToString() + " m. ");
                Console.WriteLine(" niveau de l'eau le plus haut durant cette periode :  " + result[1].ToString() + " m. ");
        }
        public static float[] GetLowestandHighestWaterLevel(List<SHOMData> shoms)
        {
            float lowest = float.MaxValue;
            float highest = 0f;
            foreach (SHOMData shom in shoms)
            {
                if (shom.value < lowest )
                {
                    lowest = shom.value; 
                }
                if (shom.value > highest)
                {
                    highest = shom.value;
                }
            }
            return new float[2] { lowest, highest };
        }
        public static List<SHOMData> GetTimeStampWaterLevel(List<SHOMData> shoms, float minValue, float maxValue)
        {
            List<SHOMData> result = new List<SHOMData>();
            foreach ( SHOMData shom in shoms)
            {
                if ( shom.value >= minValue && shom.value <= maxValue)
                {
                    result.Add(shom);
                }
            }
            return result;
        }
        public static SHOMData StringToSHOMData(string result)
        {
            //{"idstation":22,"idsource":1,"value":0.6337,"timestamp":"2020/09/23 10:37:20"}]}


            int idstation = 0;
            int idsource = 0;
            float value = 0;
            string[] parser = result.Split(',');
            if ( parser.Length != 3) { return null;  }
            int.TryParse(parser[0].Replace("{\"idstation\":", ""), out idstation);
            int.TryParse(parser[1].Replace("\"idsource\":", ""), out idsource);
            string fparsing = parser[2].Replace('.', ',');
            float.TryParse(fparsing.Replace("\"value\":", ""), out value);
            // parse the space --- >  
            string tsparsing = parser[3].Replace("\"timestamp\":\"", "");
            tsparsing = tsparsing.Replace("\"}]}", "");
            // now i have 2020/09/23 10:37:20
            string[] YMD = tsparsing.Split(' ')[0].Split('/');
            int year = 0;
            int month = 0;
            int day = 0;
            int.TryParse(YMD[0], out year);
            int.TryParse(YMD[1], out month);
            int.TryParse(YMD[2], out day);
            DateTime timestamp = new DateTime(year, month, day);
            string[] HMS = tsparsing.Split(' ')[1].Split(':');
            int hour = 0;
            int minute = 0;
            int second = 0;
            int.TryParse(HMS[0], out hour);
            int.TryParse(HMS[1], out minute);
            int.TryParse(HMS[2], out second);
            timestamp = timestamp.AddHours(hour);
            timestamp = timestamp.AddMinutes(minute);
            timestamp = timestamp.AddSeconds(second);
            return new SHOMData((uint)idstation, (uint)idsource, value, timestamp);
        }

        public static List<SHOMData> GetAllSHOMData(char[] data)
        {
            List<SHOMData> resultdata = new List<SHOMData>();
            int latestOpenedBrackets = 0;
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == '{')
                {
                    latestOpenedBrackets = i;
                    List<char> stringBuild = new List<char>();
                    int bracketcount = 0;
                    for (int n = latestOpenedBrackets; n < data.Length; n++)
                    {
                        if (data[n] == '}')
                        {
                            bracketcount++;
                        }
                        if (bracketcount == 2)
                        {
                            break;
                        }
                        stringBuild.Add(data[n]);
                    }
                    string result = "";
                    foreach (char c in stringBuild)
                    {

                        result += c;
                    }
                    SHOMData sd = StringToSHOMData(result);
                    resultdata.Add(sd);

                }
            }
            return resultdata;
        }
        public static SHOMData GetDataAtSpecificTime(DateTime dt, List<SHOMData> resultdata)
        {
            foreach( SHOMData shom in resultdata)
            {
                int cp = DateTime.Compare(dt, shom.timestamp);
                if ( cp == 0)
                {
                    return shom;
                }
            }

            return null;
        }
        public static SHOMData GetLastDataBeforeSpecificTime(DateTime dt, List<SHOMData> resultdata)
        {
            SHOMData result = null; 
            foreach (SHOMData shom in resultdata)
            {
                int cp = DateTime.Compare(dt, shom.timestamp);
                if (cp < 0)
                {
                    result = shom; 
                }
            }

            return result;
        }

        public static SHOMData GetLastData(char[] data)
        {
            int latestOpenedBrackets = 0;
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == '{')
                {
                    latestOpenedBrackets = i;
                }
            }
            List<char> stringBuild = new List<char>();
            for (int i = latestOpenedBrackets; i < data.Length; i++)
            {
                stringBuild.Add(data[i]);
            }
            string result = "";
            foreach ( char c in stringBuild)
            {
                
                result += c; 
            }
            SHOMData sd = StringToSHOMData(result);
            return sd;
           
        }

    }
}
