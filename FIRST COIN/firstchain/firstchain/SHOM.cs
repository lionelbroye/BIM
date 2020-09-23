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
            Console.WriteLine(dtreceived);

            // ----------------------------------------> GET LAST DATA

            PrintLastData(dtreceived.ToCharArray());
          

        }

        public static void PrintLastData(char[] data)
        {
            Console.WriteLine("_______________________________");
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
            Console.WriteLine("     Latest data received      ");
            Console.WriteLine("_______________________________");
            Console.WriteLine(result);
        }

    }
}
