using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Security;
using MDPlayer;

namespace MDPlayerx64.MDServer
{
    //参考
    //https://jp-seemore.com/sys/17648/
    //https://tomosoft.jp/design/?p=6699

    public class MDServer
    {
        ManualResetEvent SocketEvent = new ManualResetEvent(false);
        IPEndPoint ipEndPoint;
        Socket sock = null;
        Thread tMain;
        bool discon = false;
        Action<string> remoteCallback;

        public MDServer(int port,Action<string> remoteCallback)
        {
            this.remoteCallback = remoteCallback;
            ipEndPoint = new IPEndPoint(IPAddress.Any, port);
        }

        public void init()
        {
            Debug.WriteLine("init ThreadID:" + Thread.CurrentThread.ManagedThreadId);
            Debug.WriteLine("サーバー起動中・・・");
            tMain = new Thread(new ThreadStart(Round));
            tMain.Start();
        }

        private void Round()
        {
            while (true)
            {
                using (sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    sock.Bind(ipEndPoint);
                    sock.Listen(10);

                    try
                    {
                        Debug.WriteLine("Round ThreadID:" + Thread.CurrentThread.ManagedThreadId);
                        while (true)
                        {
                            SocketEvent.Reset();
                            sock.BeginAccept(new AsyncCallback(OnConnectRequest), sock);
                            SocketEvent.WaitOne();
                        }
                    }
                    catch (Exception ex)
                    {

                    }
                }
                if (discon) break;
            }
        }

        private void OnConnectRequest(IAsyncResult ar)
        {
            try
            {
                Debug.WriteLine("OnConnectRequest ThreadID:" + Thread.CurrentThread.ManagedThreadId);
                SocketEvent.Set();
                Socket listener = (Socket)ar.AsyncState;
                if (listener == null) return;
                Socket handler = listener.EndAccept(ar);
                EndPoint ep = handler.RemoteEndPoint;
                if (ep != null) Debug.WriteLine(ep.ToString() + " joined");
                StateObject state = new()
                {
                    WorkSocket = handler
                };
                handler.BeginReceive(state.buffer, 0, StateObject.BUFFER_SIZE, 0, new AsyncCallback(ReadCallback), state);
            }
            catch
            {
                ;
            }
        }

        private void ReadCallback(IAsyncResult ar)
        {
            try
            {
                Debug.WriteLine("ReadCallback ThreadID:" + Thread.CurrentThread.ManagedThreadId);
                StateObject state = (StateObject)ar.AsyncState;
                if (state == null) return;
                Socket handler = state.WorkSocket;
                if (!handler.Connected) return;
                int ReadSize = handler.EndReceive(ar);
                if (ReadSize < 1)
                {
                    EndPoint ep = handler.RemoteEndPoint;
                    if (ep != null) Debug.WriteLine(ep.ToString() + " disconnected");
                    return;
                }
                byte[] bb = new byte[ReadSize];
                Array.Copy(state.buffer, bb, ReadSize);
                string msg = Encoding.UTF8.GetString(bb);
                switch (msg)
                {
                    case "CC":
                        msg = Audio.GetCounter().ToString();
                        break;
                    case "TC":
                        msg = Audio.GetTotalCounter().ToString();
                        break;
                    case "LC":
                        msg = Audio.GetLoopCounter().ToString();
                        break;
                    default:
                        remoteCallback(msg);
                        break;
                }
                Debug.WriteLine(msg);
                bb = Encoding.UTF8.GetBytes(msg);
                handler.BeginSend(bb, 0, bb.Length, 0, new AsyncCallback(WriteCallback), state);
            }
            catch (Exception)
            {
            }
        }

        private void WriteCallback(IAsyncResult ar)
        {
            if (ar == null) return;
            Debug.WriteLine("WriteCallback ThreadID:" + Thread.CurrentThread.ManagedThreadId);
            StateObject? state = (StateObject?)ar.AsyncState;
            if (state == null) return;
            Socket handler = state.WorkSocket;
            handler?.EndSend(ar);
            Debug.WriteLine("送信完了");
            handler?.BeginReceive(state.buffer, 0, StateObject.BUFFER_SIZE, 0, new AsyncCallback(ReadCallback), state);
        }

        public void disConnect()
        {
            Debug.WriteLine("disConnect ThreadID:" + Thread.CurrentThread.ManagedThreadId);
            discon = true;
            sock?.Close();
        }

    }
}
