using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;

namespace firstchain
{
    class network
    {
        /*
     
         ALSO WE ABSOLUTELY NEED TO THROW CLIENT WHEN ANY EXCEPTION COMES OUT! 
         ALSO WE NEED TO REFRESH OUR PEER LIST IF WE LOSE CONTACT WITH SERV. 

         */ 

        static uint BUFFER_CHUNK = 1000;

        public class Server
        {
            TcpListener server = null;
            public Server(string ip, int port)
            {
                IPAddress localAddr = IPAddress.Parse(ip);
                server = new TcpListener(IPAddress.Any, port);
                server.Start();
                StartListener();
            }
            public void StartListener()
            {
                try
                {
                    while (true)
                    {
                        Console.WriteLine("Waiting for connections...");
                        TcpClient client = server.AcceptTcpClient();
                        Console.WriteLine("New client connected!");
                        Thread t = new Thread(new ParameterizedThreadStart(HandleDeivce));
                        t.Start(client);
                    }
                }
                catch (SocketException e)
                {
                    Console.WriteLine("SocketException: {0}", e);
                    server.Stop();
                }
            }

           
            public void HandleDeivce(Object obj)
            {
                TcpClient client = (TcpClient)obj;
                var stream = client.GetStream();
 
                byte[] bytes = new byte[BUFFER_CHUNK]; //< we can change the buffer ! 
                uint current_packet_id = 0;
                byte data_flag = 0;
                uint file_length = 0;
                uint byteOffset = 0;
                uint pNumb = 0; //< packet number
                byte[] hash_checksum = new byte[32];
                int i;
                try
                {
                    while ((i = stream.Read(bytes, 0, bytes.Length)) != 0) //< ca process seulement lorsque je recoit qqchose
                    {
                        /*
                            FIRST PACKET STRUCTURE :
                            (4 bytes) PACKET ID ( empreinte UNIX - ) 
                            (1 byte) WHAT IS THE DATA -> A TX, A BLOCK  
                            (4 bytes) FULL FILE LENGTH -> INFO ABOUT THE GENERAL LENGTH OF THE RCVED FILE. 
                            (32 bytes) HASH CHEKSUM OF FILE (if the hash is wrong we not process it! ) 
                            (...) data
                            ALL OTHER PACKET : 
                            (4 bytes) PACKET ID
                            (...) data

                         */
                        uint pId = BitConverter.ToUInt32(new byte[4] { bytes[0], bytes[1], bytes[2], bytes[3] }, 0);
                        if ( pId != current_packet_id)
                        {
                            // clean files containing pID in dll folder... 
                            if ( pId != 0)
                            {
                                string[] files = Directory.GetFiles(Program._folderPath + "net");
                                foreach (string s in files)
                                {
                                    if (s.Contains(current_packet_id.ToString()))
                                    {
                                        File.Delete(s);
                                    }
                                }
                            }
                          
                            current_packet_id = pId;
                            data_flag = bytes[4];
                            file_length = BitConverter.ToUInt32(new byte[4] { bytes[5], bytes[6], bytes[7], bytes[8] }, 0);
                            hash_checksum = new byte[32];
                            byteOffset = 0;
                            pNumb = 0;
                            for (int n = 9; n < 41; n++)
                            {
                                hash_checksum[n - 9] = bytes[n];
                            }
                            if ( file_length < BUFFER_CHUNK - 41) // REMINDER : 41 bytes is the length of the header of the first packet
                            {
                                byte[] bwrite = new byte[file_length];
                                for (int n = 41; n < file_length + 41; n++)
                                {
                                    bwrite[n-41] = bytes[n];
                                }
                                File.WriteAllBytes(Program._folderPath + "net\\" + current_packet_id.ToString() + "_" + pNumb.ToString(), bwrite);
                                Console.WriteLine("file downloaded!");
                                switch (data_flag)
                                {
                                    case 1:
                                        Program.PendingDLBlocks.Add(current_packet_id);
                                        break;
                                    case 2:
                                        Program.PendingDLTXs.Add(current_packet_id);
                                        break;
                                }
                                current_packet_id = 0; // init cpaket id to avoid erasing it! 
                            }
                            else
                            {
                                // we need to get byte of this header ... 
                                byte[] bwrite = new byte[BUFFER_CHUNK - 41];
                                for (int n = 41; n < BUFFER_CHUNK ; n++)
                                {
                                    bwrite[n - 41] = bytes[n];
                                }
                              
                                File.WriteAllBytes(Program._folderPath + "net\\" + current_packet_id.ToString() + "_" + pNumb.ToString(), bwrite);
                                byteOffset += BUFFER_CHUNK - 41;
                                Console.WriteLine("downloading status : " + byteOffset +"/" + file_length); // OK ! 
                            }
                            
                        }
                        else
                        {
                            pNumb++;
                            // OVERWRITE EVERY BYTE EXCEPT IF BYTEOFFSET+
                            if (file_length < byteOffset + BUFFER_CHUNK - 4 ) // Reminder : header 4 bytes for other packet
                            {
                                // WE ARE MISSING 4 BYTES HERE!!!!
                                byte[] bwrite = new byte[BUFFER_CHUNK - (byteOffset + BUFFER_CHUNK - file_length)]; 
                                for (int n = 4; n < 4+BUFFER_CHUNK - (byteOffset + BUFFER_CHUNK - file_length); n++)
                                {
                                    bwrite[n - 4] = bytes[n];
                                }
                                File.WriteAllBytes(Program._folderPath + "net\\" + current_packet_id.ToString() + "_" + pNumb.ToString(), bwrite);
                                byteOffset += (uint)bwrite.Length;
                                Console.WriteLine("downloading status :" + byteOffset + "/" + file_length + " " + bwrite.Length); // OK !
                                Console.WriteLine("file downloaded!");
                                switch (data_flag)
                                {
                                    case 1:
                                        Program.PendingDLBlocks.Add(current_packet_id);
                                        break;
                                    case 2:
                                        Program.PendingDLTXs.Add(current_packet_id);
                                        break;
                                }
                                
                                
                                current_packet_id = 0; // init cpaket id to avoid erasing it! 
                            }
                            else
                            {
                                byte[] bwrite = new byte[BUFFER_CHUNK - 4];
                                for (int n = 4; n < bytes.Length; n++)
                                {
                                    bwrite[n - 4] = bytes[n];
                                }
                                File.WriteAllBytes(Program._folderPath + "net\\" + current_packet_id.ToString() + "_" + pNumb.ToString(), bwrite);
                                byteOffset += BUFFER_CHUNK -4 ;
                                Console.WriteLine("downloading status :" + byteOffset + "/" + file_length + " " + bwrite.Length); // OK !
                            }
               
                        }
                        
                        stream.Write(bytes, 0, bytes.Length); // just a ping back... It is always important...  
                       
                    }
                }
                catch (Exception e)
                {
                    if (!client.Connected)
                    {
                        Console.WriteLine("peer disconnect");
                    }
                    else
                    {
                        Console.WriteLine(e.ToString());
                    }
                    client.Close();

                }
            }
        }
       
        public List<TcpClient> clients; //< list of all of my peers!  

        public void Initialize()
        {
            clients = new List<TcpClient>();
            string[] netinfos = File.ReadAllLines(AppDomain.CurrentDomain.BaseDirectory + "\\peers.txt");
            if ( netinfos.Length < 1)
            {
                Console.WriteLine("Please put net info !");
                return; 
            }
            Thread t = new Thread(delegate ()
            {
                // replace the IP with your system IP Address...
                Server myserver = new Server(netinfos[0].Split(':')[0], int.Parse(netinfos[0].Split(':')[1]));
            });
            t.Start();

            Console.WriteLine("Server Started...!");

            for (int i = 1; i < netinfos.Length; i++)
            {
                string ip = netinfos[i].Split(':')[0];
                Int32 Port = int.Parse(netinfos[i].Split(':')[1]);
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    Connect(ip, Port); // ICI QUELQUECHOSE A REGLER!!! 
                }).Start();
           }
          
           
        }
        public void SendData(byte[] data, int peerIndex) //<  send data to a peer! 
        {
            try
            {
                
                //clients[peerIndex].ReceiveTimeout = 5000;
               
                NetworkStream stream = clients[peerIndex].GetStream();
                stream.ReadTimeout = 5000;
                stream.Write(data, 0, data.Length);
                stream.Read(data, 0, data.Length); // this could create issue ! 
            }
            catch ( Exception e)
            {
               
                if (!clients[peerIndex].Connected)
                {
                    clients[peerIndex].Close();
                    clients.RemoveAt(peerIndex);
                    Console.WriteLine("peers removed due to inactivity!");
                }
                else
                {
                   // Console.WriteLine(e.ToString());
                }
            }
        }

        public void SendFile (string _fPath, byte header, int i) // this is kind of awful
        {

            /*
                            FIRST PACKET STRUCTURE :
                            (4 bytes) PACKET ID ( empreinte UNIX - ) 
                            (1 byte) WHAT IS THE DATA -> A TX, A BLOCK  
                            (4 bytes) FULL FILE LENGTH -> INFO ABOUT THE GENERAL LENGTH OF THE RCVED FILE. 
                            (32 bytes) HASH CHEKSUM OF FILE (if the hash is wrong we not process it! ) 
                            (...) data
                            ALL OTHER PACKET : 
                            (4 bytes) PACKET ID
                            (...) data

            */
            uint unixTimestamp = (uint)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            uint fLength = (uint)new FileInfo(_fPath).Length; // THIS CAN CAUSE A PROB ! 
            byte[] checksum = Program.ComputeSHA256(File.ReadAllBytes(_fPath)); // can take some time! 
            // REMINDER : 41 bytes is the length of the header of the first packet
            uint byteOffset = 0;
            
            List<byte> DataBuilder = new List<byte>();
            DataBuilder = Program.AddBytesToList(DataBuilder, BitConverter.GetBytes(unixTimestamp));
            DataBuilder = Program.AddBytesToList(DataBuilder, new byte[1] { header });
            DataBuilder = Program.AddBytesToList(DataBuilder, BitConverter.GetBytes(fLength));
            DataBuilder = Program.AddBytesToList(DataBuilder, checksum);
            // dbbytes is header of the first packet! 
            if (fLength < BUFFER_CHUNK - 41) // REMINDER : 41 bytes is the length of the header of the first packet
            {
                DataBuilder = Program.AddBytesToList(DataBuilder, File.ReadAllBytes(_fPath));
                SendData(Program.ListToByteArray(DataBuilder), i);
                return;
            }
            else
            {
                DataBuilder = Program.AddBytesToList(DataBuilder, Program.GetBytesFromFile(byteOffset, BUFFER_CHUNK - 41, _fPath));
                SendData(Program.ListToByteArray(DataBuilder), i);
                byteOffset += BUFFER_CHUNK - 41;  // OK JUSQUE LA
            }
            //byte[] data;
            while ( byteOffset < fLength)
            {
                DataBuilder = new List<byte>();
                DataBuilder = Program.AddBytesToList(DataBuilder, BitConverter.GetBytes(unixTimestamp));
                uint chunk = BUFFER_CHUNK - 4;
                if (byteOffset + chunk > fLength)
                {
                    chunk = chunk - (byteOffset + chunk - fLength);

                }
                DataBuilder = Program.AddBytesToList(DataBuilder, Program.GetBytesFromFile(byteOffset, chunk, _fPath));
                SendData(Program.ListToByteArray(DataBuilder), i);
                byteOffset += chunk;
                Console.WriteLine("uploading status : " + byteOffset + "/" + fLength + " " + chunk);
            }

           
        }

        public void ThreadingFile(string _fPath, byte header) //< WILL SEND FULL BLOCKCHAIN 
        {

            for (int i = 0;  i < clients.Count; i++) // Should be MULTITHREADED ! 
            {
                int index = i;
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    SendFile(_fPath, header, index); 
                }).Start();
            }
          

        }
        public void Connect(String server, Int32 Port)
        {
            Int32 port = Port;
            TcpClient client;  
            while (true)
            {
                try
                {
                    client = new TcpClient(server, port);
                    break;
                }
                catch (Exception e)
                {
                    Thread.Sleep(500); // ATTEMPT THE SERVER ! 
                }
            }
 
            clients.Add(client);
           
        }
    }

}

