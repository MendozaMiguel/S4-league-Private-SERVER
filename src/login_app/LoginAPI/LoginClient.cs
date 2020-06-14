using S4L_Login;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using static RmiMessage;

public class StateObject
{
    public Socket workSocket = null;
    public const int BufferSize = 1024;
    public byte[] buffer = new byte[BufferSize];
}


class LoginClient
{
    internal enum HostID : int
    {
        NONE,
        SERVER,
        LAST,
    }
    
    public static ManualResetEvent allDone = new ManualResetEvent(false);

    public const short Magic = 0x5713;

    public static Socket socket;

    public static bool connected = false;

    public LoginClient()
    {
    }

    public static void UpdateLabel(LoginWindow win, string msg)
    {
        win.UpdateLabel(msg);
    }

    public static void Connect(IPEndPoint localEndPoint)
    {
        Program.LoginWindow.UpdateLabel("Connecting...");
        Socket sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        sck.LingerState.Enabled = false;
        connected = false;
        try
        {
            sck.BeginConnect(localEndPoint, ConnectCallback, sck);
            socket = sck;
            Thread timer = new Thread(() => {
                Thread.Sleep(5000);
                if (!connected)
                {
                    sck.Close();
                    Program.LoginWindow.Reset();
                    Program.LoginWindow.UpdateLabel("Authentication Timeout.");
                }
            });
            timer.Start();
        }
        catch (Exception e)
        {
#if DEBUG
            /*Program.LoginWindow.UpdateLabel($"Error -> {e.ToString()}");
            throw new Exception(e);*/
#endif
        }
    }
    
    private static ManualResetEvent connectDone =
        new ManualResetEvent(false);
    private static ManualResetEvent sendDone =
        new ManualResetEvent(false);
    private static ManualResetEvent receiveDone =
        new ManualResetEvent(false);
    
    private static String response = String.Empty;
    
    private static void ConnectCallback(IAsyncResult ar)
    {
        try
        {
            Socket client = (Socket)ar.AsyncState;
            
            client.EndConnect(ar);
            Program.LoginWindow.UpdateLabel("Connected.");
            
            connectDone.Set();
            StateObject state = new StateObject();
            state.workSocket = client;
            
            client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReceiveCallback), state);
        }
        catch (Exception e)
        {
#if DEBUG
            /*Program.LoginWindow.UpdateLabel($"Error -> {e.ToString()}");
            throw new Exception(e);*/
#endif
        }
    }

    private static void Receive(Socket client)
    {
        try
        {
            StateObject state = new StateObject();
            state.workSocket = client;
            client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReceiveCallback), state);
        }
        catch (Exception e)
        {
#if DEBUG
            /*Program.LoginWindow.UpdateLabel($"Error -> {e.ToString()}");
            throw new Exception(e);*/
#endif
        }
    }

    private static void ReceiveCallback(IAsyncResult ar)
    {
        try
        {
            StateObject state = (StateObject)ar.AsyncState;
            Socket client = state.workSocket;
            int bytesRead = client.EndReceive(ar);

            if (bytesRead > 0)
            {
                RmiMessage __msg = new RmiMessage(state.buffer, bytesRead);
                short magic = 0;
                ByteArray packet = new RmiMessage();
                if (__msg.Read(ref magic)
                    && magic == Magic
                    && __msg.Read(ref packet))
                {
                    MessageType coreID = 0;
                    RmiMessage message = new RmiMessage(packet);
                    message.Read(ref coreID);
                    switch (coreID)
                    {
                        case MessageType.Notify:
                            {
                                string info = "";
                                message.Read(ref info);
                                if(info.Contains("<region>") && info.Contains("</region>"))
                                {
                                    info = info.Replace("<region>", "");
                                    info = info.Replace("</region>", "");
                                    if (info == "EU-S4L")
                                    {
                                        RmiMessage _msg = new RmiMessage();
                                        _msg.Write(Program.LoginWindow.GetUsername());
                                        _msg.Write(Program.LoginWindow.GetPassword());
                                        _msg.Write(false);
                                        RmiSend(client, 15, _msg);
                                        Program.LoginWindow.UpdateLabel($"Authenticating..");
                                    }
                                    else
                                    {
                                        Program.LoginWindow.UpdateLabel($"Error");
                                        Program.LoginWindow.UpdateErrorLabel($"Wrong region/server");
                                    }
                                }
                                break;
                            }
                        case MessageType.Rmi:
                            {
                                short RmiID = 0;
                                if(!message.Read(ref RmiID))
                                {
                                    Program.LoginWindow.UpdateLabel($"Received corrupted Rmi message.");
                                }
                                else
                                {
                                    switch(RmiID)
                                    {
                                        case 16:
                                            {
                                                bool success = false;
                                                if(message.Read(ref success) && success)
                                                {
                                                    receiveDone.Set();
                                                    string code = "";
                                                    message.Read(ref code);
                                                    //Program.LoginWindow.UpdateLabel($"Authentication succeeded. \ncode={code}");
                                                    Program.LoginWindow.UpdateLabel($"Authentication succeeded.");
                                                    Program.LoginWindow.Ready(code);
                                                    connected = true;
                                                    RmiMessage _msg = new RmiMessage();
                                                    RmiSend(client, 17, _msg);
                                                    client.Disconnect(false);
                                                    client.Close();
                                                }
                                                else
                                                {
                                                    receiveDone.Set();
                                                    string errcode = "";
                                                    message.Read(ref errcode);
                                                    Program.LoginWindow.Reset();
                                                    Program.LoginWindow.UpdateErrorLabel($"Failed: {errcode}");
                                                    connected = true;
                                                    client.Disconnect(false);
                                                    client.Close();
                                                }
                                            }
                                            break;
                                        default:
                                            Program.LoginWindow.UpdateLabel($"Received unknown RmiID. {RmiID}");
                                            break;
                                    }
                                }
                                break;
                            }
                        case MessageType.Encrypted:
                            {
                                break;
                            }
                        default:
                            {
                                break;
                            }
                    }
                    if(!connected)
                        client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
                }
                else
                {
                    receiveDone.Set();
                    client.Disconnect(true);
                }
            }
        }
        catch (Exception e)
        {
#if DEBUG
            /*Program.LoginWindow.UpdateLabel($"Error -> {e.ToString()}");
            throw new Exception(e);*/
#endif
        }
    }
    private static void RmiSend(Socket handler, short RmiID, RmiMessage msg)
    {
        RmiMessage rmiframe = new RmiMessage();
        rmiframe.Write(MessageType.Rmi);
        rmiframe.Write(RmiID);
        rmiframe.Write(msg);
        Send(handler, rmiframe);
    }

    private static void Send(Socket handler, RmiMessage data)
    {
        try
        {
            RmiMessage coreframe = new RmiMessage();
            coreframe.Write(Magic);
            coreframe.WriteScalar(data.Length);
            coreframe.Write(data);
            handler.BeginSend(coreframe._buffer, 0, coreframe._writeoffset, 0,
                new AsyncCallback(SendCallback), handler);
        }
        catch (Exception ex)
        {
#if DEBUG
            /*Program.LoginWindow.UpdateLabel($"Error -> {e.ToString()}");
            throw new Exception(e);*/
#endif
        }
    }

    private static void SendCallback(IAsyncResult ar)
    {
        try
        {
            Socket client = (Socket)ar.AsyncState;
            sendDone.Set();
        }
        catch (Exception e)
        {
#if DEBUG
            /*Program.LoginWindow.UpdateLabel($"Error -> {e.ToString()}");
            throw new Exception(e);*/
#endif
        }
    }
}
