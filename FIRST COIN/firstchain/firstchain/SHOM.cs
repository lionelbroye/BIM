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


        public class SHOMData
        {
            ////{"idstation":22,"idsource":1,"value":0.6337,"timestamp":"2020/09/23 10:37:20"}]}
            public int idstation { get; }
            public int idsource { get; }
            public float value { get; }
            public DateTime timestamp { get; }
            public SHOMData ( int station, int source ,float val, DateTime ts)
            {
                this.idstation = station;
                this.idsource = source;
                this.value = val;
                this.timestamp = ts;
            
            }
        }

        public static void GetSHOMData(int station, DateTime dateStart, DateTime dateEnd)
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
           // Console.WriteLine(dtreceived);

            // ----------------------------------------> GET LAST DATA

       
            Console.WriteLine("_______________________________");
            Console.WriteLine("     Latest data received      ");
            Console.WriteLine("_______________________________");
            SHOMData lastresult = GetLastData(dtreceived.ToCharArray());
            Console.WriteLine("id de la station = " + lastresult.idstation);
            Console.WriteLine("hauteur de l'eau = " + lastresult.value);
            Console.WriteLine("Timestamp        = " + lastresult.timestamp.ToString());


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

            //{"idstation":22,"idsource":1,"value":0.6337,"timestamp":"2020/09/23 10:37:20"}]}

            
            int idstation = 0;
            int idsource = 0;
            float value = 0;
            string[] parser = result.Split(',');
            int.TryParse(parser[0].Replace("{\"idstation\":", ""), out idstation);
            int.TryParse(parser[1].Replace("\"idsource\":", ""), out idsource);
            string fparsing = parser[2].Replace('.', ',');
            float.TryParse(fparsing.Replace("\"value\":", ""), out value);
            // parse the space --- >  
            string tsparsing = parser[3].Replace("\"timestamp\":\"", "");
            tsparsing = tsparsing.Replace("\"}]}", "");
            // now i have 2020/09/23 10:37:20
            string[] YMD = tsparsing.Split(' ')[0].Split('/') ;
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
            return new SHOMData(idstation, idsource, value, timestamp);
        }

    }
}
