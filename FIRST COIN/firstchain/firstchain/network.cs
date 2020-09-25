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
         * __________________________________ NETWORK TOPOLOGY_____________________________
         *                              A COMPLETE GRAPH TOPOLOGY 
         *                              

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
                        if (pId != current_packet_id && pId > 0 ) // pID
                        {
                            /*
                            // clean files containing pID in dll folder... 
                            if (pId != 0)
                            {
                                string[] files = Directory.GetFiles(Program._folderPath + "net");
                                foreach (string s in files)
                                {
                                    if (s.Contains(current_packet_id.ToString()))
                                    {
                                        File.Delete(s);
                                    }
                                }
                                Console.WriteLine("will delete files with " + pId.ToString());
                            }
                            */
                            Program.dlRemovalQueue.Add(pId); 
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
                            if (file_length < BUFFER_CHUNK - 41) // REMINDER : 41 bytes is the length of the header of the first packet
                            {
                                byte[] bwrite = new byte[file_length];
                                for (int n = 41; n < file_length + 41; n++)
                                {
                                    bwrite[n - 41] = bytes[n];
                                }
                                File.WriteAllBytes(Program._folderPath + "net\\" + current_packet_id.ToString() + "_" + pNumb.ToString(), bwrite);
                                Console.WriteLine("file downloaded! --->  " + pId);
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
                                for (int n = 41; n < BUFFER_CHUNK; n++)
                                {
                                    bwrite[n - 41] = bytes[n];
                                }

                                File.WriteAllBytes(Program._folderPath + "net\\" + current_packet_id.ToString() + "_" + pNumb.ToString(), bwrite);
                                byteOffset += BUFFER_CHUNK - 41;
                                Console.WriteLine("downloading status : " + byteOffset + "/" + file_length); // OK ! 
                            }

                        }
                        else
                        {
                            pNumb++;
                            // OVERWRITE EVERY BYTE EXCEPT IF BYTEOFFSET+
                            if (file_length < byteOffset + BUFFER_CHUNK - 4) // Reminder : header 4 bytes for other packet
                            {
                                // WE ARE MISSING 4 BYTES HERE!!!!
                                byte[] bwrite = new byte[BUFFER_CHUNK - (byteOffset + BUFFER_CHUNK - file_length)];
                                for (int n = 4; n < 4 + BUFFER_CHUNK - (byteOffset + BUFFER_CHUNK - file_length); n++)
                                {
                                    bwrite[n - 4] = bytes[n];
                                }
                                File.WriteAllBytes(Program._folderPath + "net\\" + current_packet_id.ToString() + "_" + pNumb.ToString(), bwrite);
                                byteOffset += (uint)bwrite.Length;
                                Console.WriteLine("downloading status :" + byteOffset + "/" + file_length + " " + bwrite.Length); // OK !
                                Console.WriteLine("file downloaded!--->  " + pId);
                                switch (data_flag)
                                {
                                    case 1:
                                        if (!Program.PendingDLBlocks.Contains(pId))
                                        {
                                            Program.PendingDLBlocks.Add(current_packet_id);
                                        }
                                        
                                        break;
                                    case 2:
                                        if (!Program.PendingDLTXs.Contains(pId))
                                        {

                                            Program.PendingDLTXs.Add(current_packet_id);
                                        }
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
                                byteOffset += BUFFER_CHUNK - 4;
                                Console.WriteLine("downloading status :" + byteOffset + "/" + file_length + " " + bwrite.Length); // OK !
                            }

                        }

                        stream.Write(bytes, 0, bytes.Length); // probably a bad stuff here ... 

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
                    Console.WriteLine("peer removed");
                    client.Close();

                }
            }
        }

        public List<ExtendedPeer> mPeers; //< list of all of my peers! 
        
        public class ExtendedPeer
        {
            public TcpClient Peer { get; set; }
            public bool currentlySending { get; set; }
            public string IP { get; }
            public int Port { get; }
            public ExtendedPeer(TcpClient client, string ip, int port)
            {
                this.Peer = client;
                this.IP = ip;
                this.Port = port;
                currentlySending = false;
            }
        }

        public void Initialize()
        {
            mPeers= new List<ExtendedPeer>();
            string[] netinfos = File.ReadAllLines(AppDomain.CurrentDomain.BaseDirectory + "\\peers.txt");
            if (netinfos.Length < 1)
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

            mPeers.Add(new ExtendedPeer(client,server,port));

        }
        public void SendData(byte[] data, int peerIndex) //<  send data to a peer! 
        {
            try
            {

                //clients[peerIndex].ReceiveTimeout = 5000;

                NetworkStream stream = mPeers[peerIndex].Peer.GetStream();
                stream.ReadTimeout = 5000;
                stream.Write(data, 0, data.Length);
                stream.Read(data, 0, data.Length); // this could create issue ! 
            }
            catch (Exception e)
            {

                if (!mPeers[peerIndex].Peer.Connected)
                {
                    if (!Program.peerRemovalQueue.Contains(mPeers[peerIndex].IP))
                    {
                        Program.peerRemovalQueue.Add(mPeers[peerIndex].IP); 
                    }
                   
                    /*
                    mPeers[peerIndex].Peer.Close();
                    // try to reconnect to him!
                    string ip = mPeers[peerIndex].IP;
                    Int32 Port = mPeers[peerIndex].Port;
                    mPeers.RemoveAt(peerIndex); // we should not remove that peer like this ( find a different way ) ... 
                    // we should use a removalpeerqueue that only trigger when no uploading! 
                    
                    new Thread(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;
                        Connect(ip, Port); 
                    }).Start();
                   

                    Console.WriteLine("peers removed due to inactivity!"); 
                    */
                    // we should try to reconnect to him ... 
                }
                else
                {
                    // Console.WriteLine(e.ToString());
                }
            }
        }

        public void SendFile(string _fPath, byte header, int i) // this is kind of awful
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
            
            mPeers[i].currentlySending = true;
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
                mPeers[i].currentlySending = false;
                return;
            }
            else
            {
                DataBuilder = Program.AddBytesToList(DataBuilder, Program.GetBytesFromFile(byteOffset, BUFFER_CHUNK - 41, _fPath));
                SendData(Program.ListToByteArray(DataBuilder), i);
                byteOffset += BUFFER_CHUNK - 41; 
            }
            //byte[] data;
            while (byteOffset < fLength )
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
            mPeers[i].currentlySending = false;

        }

        public void BroadcastFile(string _fPath, byte header) //< WILL SEND ANY FILE... ( like a ptx, a unique tx, a fork, a specific bloc file etc. ) 
        {

            for (int i = 0; i < mPeers.Count; i++)
            {
                int index = i;
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    SendFile(_fPath, header, index);
                }).Start();
            }


        }

        public Tuple<uint[], byte[]> GetChunkBytesinBlockchainFiles(uint chunksize, uint byteOffset, uint fileOffset) //< this will help ! :3
        {
            // chunk size is the number of byte we need to extract. byteOffset is pointer of byte at start. when reset it equals 4 cause we dont include header, fileoffset is file starting searching...
            // we return a tuple like this . item 1 : byte[2]{byteOffsetatend,fileOffsetatend} item2 : byte array of length chunksize! 
            Console.WriteLine("chinksize = " + chunksize);
            List<byte> DataBuilder = new List<byte>();
            string filePath = Program._folderPath + "blockchain\\" + fileOffset.ToString();
            uint currentFileLength = (uint)new FileInfo(filePath).Length;
            while (DataBuilder.Count < chunksize)
            {

                uint chunk = chunksize;
                if (byteOffset + chunk > currentFileLength)
                {
                    chunk = chunk - (byteOffset + chunk - currentFileLength);

                }
                DataBuilder = Program.AddBytesToList(DataBuilder, Program.GetBytesFromFile(byteOffset, chunk, filePath));
                Console.WriteLine("retrieving data : " + DataBuilder.Count + "/" + chunksize + " " + chunk);
                if (chunk != chunksize)
                {
                    fileOffset++;
                    byteOffset = 4;
                    filePath = Program._folderPath + "blockchain\\" + fileOffset.ToString();
                    currentFileLength = (uint)new FileInfo(filePath).Length;
                }
                else
                {
                    byteOffset += chunk;
                }

            }

            return new Tuple<uint[], byte[]>(new uint[2] { byteOffset, fileOffset }, Program.ListToByteArray(DataBuilder));
        }

        public void SendBlockChain(uint indexStart, uint offsetStart, string fileStart, uint indexEnd, uint offsetEnd, string fileEnd, int i)
        {
            // Nous avons besoin d'avoir ces differentes arguments : uint start (index fo block start), uint end ( index of last block ) 
            // nous avons pour cela avoir besoin de startOffset du fichier contenant le 1er bloc et du endOffset contenant la fin du dernier bloc . 
            // de cette manière nous pouvons alors determiner le filelength! et nous pourrons tranquillement broadcaster la data avec les chunks!  
            Console.WriteLine(offsetStart + " " + offsetEnd);
            mPeers[i].currentlySending = true;
            uint unixTimestamp = (uint)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds; // add a random ( like our 
            byte header = 1;
            // get the length 
            uint fLength = 0;
            uint fsInt = uint.Parse(fileStart.Replace(Program._folderPath + "blockchain\\", ""));
            uint feInt = uint.Parse(fileEnd.Replace(Program._folderPath + "blockchain\\", ""));
            fLength += (uint)new FileInfo(fileStart).Length - offsetStart;
            for (uint n = fsInt + 1; n < feInt; n++) // can cause eventually an error ! 
            {
                fLength += (uint)new FileInfo(Program._folderPath + "blockchain\\" + n.ToString()).Length - 4;
            }

            fLength += (uint)new FileInfo(fileEnd).Length - offsetEnd;
            fLength += 4; // we will include header! 
            byte[] checksum = new byte[32]; // we dont compute the hash here. It was a bad idea... too slow.. 

            List<byte> DataBuilder = new List<byte>();
            DataBuilder = Program.AddBytesToList(DataBuilder, BitConverter.GetBytes(unixTimestamp));
            DataBuilder = Program.AddBytesToList(DataBuilder, new byte[1] { header });
            DataBuilder = Program.AddBytesToList(DataBuilder, BitConverter.GetBytes(fLength));
            DataBuilder = Program.AddBytesToList(DataBuilder, checksum);
            DataBuilder = Program.AddBytesToList(DataBuilder, BitConverter.GetBytes(indexEnd)); // we include a custom header
            uint fileOffset = fsInt;
            uint byteOffsetB = 4;
            uint byteOffset = 4;
            if (fLength < BUFFER_CHUNK - 45) // REMINDER : 41 bytes is the length of the header of the first packet
            {
                Tuple<uint[], byte[]> chunkedData = GetChunkBytesinBlockchainFiles(fLength - 4, offsetStart, fileOffset);
                DataBuilder = Program.AddBytesToList(DataBuilder, chunkedData.Item2);
                SendData(Program.ListToByteArray(DataBuilder), i);
                mPeers[i].currentlySending = false;
                return;
            }
            else
            {
                Tuple<uint[], byte[]> chunkedData = GetChunkBytesinBlockchainFiles(BUFFER_CHUNK - 45, offsetStart, fileOffset);
                DataBuilder = Program.AddBytesToList(DataBuilder, chunkedData.Item2);
                fileOffset = chunkedData.Item1[1];
                byteOffsetB = chunkedData.Item1[0];
                SendData(Program.ListToByteArray(DataBuilder), i);
                byteOffset += BUFFER_CHUNK - 45;  // probably need to see if we write 41 and not 45 ... 
                Console.WriteLine("uploading status : " + byteOffset + "/" + fLength);
            }
            while (byteOffset < fLength)
            {
                DataBuilder = new List<byte>();
                DataBuilder = Program.AddBytesToList(DataBuilder, BitConverter.GetBytes(unixTimestamp));
                uint chunk = BUFFER_CHUNK - 4;
                if (byteOffset + chunk > fLength)
                {
                    chunk = chunk - (byteOffset + chunk - fLength);
                }
                Tuple<uint[], byte[]> chunkedData = GetChunkBytesinBlockchainFiles(chunk, byteOffsetB, fileOffset);
                DataBuilder = Program.AddBytesToList(DataBuilder, chunkedData.Item2);
                fileOffset = chunkedData.Item1[1];
                byteOffsetB = chunkedData.Item1[0];
                // we absolutely need to get some extra info from this function like the latest file Offset and the latestoffsetStart! 

                SendData(Program.ListToByteArray(DataBuilder), i);
                byteOffset += chunk;
                Console.WriteLine("uploading status : " + byteOffset + "/" + fLength + " " + chunk);
            }
            mPeers[i].currentlySending = false;

        }
        public void BroadcastBlockchain(uint start, uint end) //< WILL SEND A LIST OF BLOCKS FROM INDEX START TO INDEX END 
        {
            Tuple<uint[], string> sInfo = Program.GetBlockPointerAtIndex(start);
            Tuple<uint[], string> eInfo = Program.GetBlockPointerAtIndex(end);
            if (sInfo == null || eInfo == null) { Console.WriteLine("unable to broadcast blockchain beacause of bad pointer"); return; }
            if (end < start) { return; }
            for (int i = 0; i < mPeers.Count; i++)
            {
                int index = i;
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    SendBlockChain(start, sInfo.Item1[0], sInfo.Item2, end, eInfo.Item1[1], eInfo.Item2, index);
                }).Start();
            }


        }
    
    }

}


