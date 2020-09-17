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
         THERE IS TWO BIG GENERAL ISSUE HERE.FLAG IS NOT WORKING PROPERLY. we need another way. 
         like first 12 bytes of a packets are info about (WHAT IS THE FILE) (
         ALSO WE ABSOLUTELY NEED TO THROW CLIENT WHEN ANY EXCEPTION COMES OUT! 
         ALSO WE NEED TO REFRESH OUR PEER LIST IF WE LOSE CONTACT WITH SERV. 
         */ 

        static uint BUFFER_CHUNK = 1000;
        static uint BUFFER_HEAD = 256; 

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
                        Console.WriteLine("Waiting for a connection...");
                        TcpClient client = server.AcceptTcpClient();
                        Console.WriteLine("Connected!");
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

                uint _flag = 0; 
                byte[] bytes = new byte[BUFFER_HEAD]; //< we can change the buffer ! 
                string fileNameHeader = "";

                uint FILE_LENGTH = 0;
                uint CURRENT_BYTE = 0;
                int i;
                stream.ReadTimeout = 15000;
                try
                {
                    while ((i = stream.Read(bytes, 0, bytes.Length)) != 0) //< ca process seulement lorsque je recoit qqchose
                    {

                        bool _pass = false;
                        if (_flag == 0)
                        {
                            Random r = new Random();
                            r.Next(0, int.MaxValue);
                            fileNameHeader = r.ToString();
                            uint header = BitConverter.ToUInt32(bytes, 0);
                            if (header == 1)
                            {
                                _flag = 1;
                                _pass = true;
                                FILE_LENGTH = 0;
                                CURRENT_BYTE = 0;
                                Console.WriteLine("Will receive Blocks file.");
                            }
                            if (header == 2)
                            {
                                _flag = 3;
                                _pass = true;
                                FILE_LENGTH = 0;
                                CURRENT_BYTE = 0;
                                Console.WriteLine("Will receive TX file.");
                            }
                        }
                        if (!_pass)
                        {
                            if (_flag == 1 || _flag == 3)
                            {
                                FILE_LENGTH = BitConverter.ToUInt32(bytes, 0);
                                Console.WriteLine("Bytes needed : " + FILE_LENGTH);
                                if (FILE_LENGTH < BUFFER_CHUNK)
                                {
                                    bytes = new byte[FILE_LENGTH];
                                }
                                else
                                {
                                    bytes = new byte[BUFFER_CHUNK];
                                }

                                _pass = true;

                                if (_flag == 1)
                                {
                                    _flag = 2;
                                }
                                if (_flag == 3)
                                {
                                    _flag = 4;
                                }
                            }
                        }
                        if (_flag == 2 && !_pass)
                        {
                            string namefile = fileNameHeader;

                            if (CURRENT_BYTE == 0)
                            {
                                if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\" + namefile))
                                {
                                    // File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\" + namefile);
                                }
                                else
                                {
                                    // File.Create(AppDomain.CurrentDomain.BaseDirectory + "\\" + namefile);
                                }
                            }
                            Console.WriteLine("Writing " + bytes.Length + " bytes at " + CURRENT_BYTE);
                            //while ( File.)
                            // Program.AppendBytesToFile(AppDomain.CurrentDomain.BaseDirectory + "\\" + namefile, bytes); //< GET AN ERROR HERE !!! CAUSE WE USE THREADING...
                            CURRENT_BYTE += (uint)bytes.Length;

                            if (FILE_LENGTH < CURRENT_BYTE + BUFFER_CHUNK) // we verify if file length is ok 
                            {
                                //  chunk = chunk - (byteOffset + chunk - fLength);
                                bytes = new byte[BUFFER_CHUNK - (CURRENT_BYTE + BUFFER_CHUNK - FILE_LENGTH)];
                            }
                            else
                            {
                                bytes = new byte[BUFFER_CHUNK];
                            }
                            if (CURRENT_BYTE == FILE_LENGTH)
                            {
                                Console.WriteLine("Download Done");
                                // Program.ProccessTempBlocks(AppDomain.CurrentDomain.BaseDirectory + "\\" + namefile);
                                _flag = 0;
                            }

                        }

                        //i = 0;
                        stream.Write(bytes, 0, bytes.Length);

                    }
                }
                catch (Exception e)
                {
                    if (!client.Connected)
                    {
                        Console.WriteLine("peer disconnect");
                        client.Close();
                        return;
                    }
                    else
                    {
                        Console.WriteLine(e.ToString());
                        Console.WriteLine("thread should be init");

                        // Thread t = new Thread(new ParameterizedThreadStart(HandleDeivce));
                        // t.Start(client);
                    }

                }
                /*
                while ( true)
               {

               }
               */
            }
        }

        public List<TcpClient> clients; //< list of all of my peers!  

        public void Initialize()
        {
            clients = new List<TcpClient>();
            string[] netinfos = File.ReadAllLines(AppDomain.CurrentDomain.BaseDirectory + "\\peers.txt");
            Int32 Port = int.Parse(netinfos[0]);
            if ( netinfos.Length < 1)
            {
                Console.WriteLine("Please put net info !");
                return; 
            }
            Thread t = new Thread(delegate ()
            {
                // replace the IP with your system IP Address...
                Server myserver = new Server(netinfos[1], Port);
            });
            t.Start();

            Console.WriteLine("Server Started...!");

            for (int i = 2; i < netinfos.Length; i++)
            {
                string ip = netinfos[i];
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

            //  Console.WriteLine("client connecté = " + clients[peerIndex].Connected);
            clients[peerIndex].ReceiveTimeout = 5000;
            // stream.ReadTimeout = 5000;
           

                NetworkStream stream = clients[peerIndex].GetStream();
                // Send the message to the connected TcpServer. 
                stream.Write(data, 0, data.Length);
                stream.Read(data, 0, data.Length);
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
            //< should use a specific wait 
        }

        public void SendFile (string _fPath, byte header, int i)
        {
            byte[] data = new byte[1] { header };
            SendData(data, i); //< SEND HEADER
            uint fLength = (uint)new FileInfo(_fPath).Length;
            data = BitConverter.GetBytes(fLength);
            SendData(data, i); //< SEND FILELENGTH INFO
            uint byteOffset = 0;
            while (byteOffset < fLength)
            {
                uint chunk = BUFFER_CHUNK;
                if (byteOffset + chunk > fLength)
                {
                    chunk = chunk - (byteOffset + chunk - fLength);

                }

                data = Program.GetBytesFromFile(byteOffset, chunk, _fPath);


                SendData(data, i); //< SEND FILE CHUNK BY CHUNK ! 
                byteOffset += chunk;
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

