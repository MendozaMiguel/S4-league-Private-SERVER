using System;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using static RmiMessage;
using Netsphere.Database.Auth;
using Netsphere;
using Serilog.Core;
using Dapper.FastCrud;
using System.Linq;
using System.Security.Cryptography;
using BlubLib;
using DotNetty.Common.Utilities;
using Serilog;
using System.Net.Sockets;

public class LoginServerHandler : ChannelHandlerAdapter
{
    private static readonly short Magic = 0x5713;
    private static readonly ILogger Logger = Log.ForContext(Constants.SourceContextPropertyName, "LoginServer");

    public override void ChannelActive(IChannelHandlerContext context)
    {
        base.ChannelActive(context);
        RmiMessage firstMessage = new RmiMessage();
        firstMessage.Write(MessageType.Notify);
        firstMessage.Write("<region>EU-S4L</region>");
        SendA(context, firstMessage);
    }
    
    public override void ChannelRead(IChannelHandlerContext context, object messageData)
    {
        var buffer = messageData as IByteBuffer;
        byte[] data = buffer.ToArray();

        RmiMessage __msg = new RmiMessage(data, data.Length);
        short magic = 0;
        ByteArray _message = new ByteArray();

        if (__msg.Read(ref magic)
            && magic == Magic
            && __msg.Read(ref _message))
        {
            
            RmiMessage message = new RmiMessage(_message);
            MessageType coreID = 0;
            if (message.Read(ref coreID))
            {
                switch (coreID)
                {
                    case MessageType.Rmi:
                        {
                            short RmiID = 0;
                            if (message.Read(ref RmiID))
                            {
                                switch (RmiID)
                                {
                                    case 15:
                                        {
                                            AccountDto account;
                                            string Username = "";
                                            string Password = "";
                                            bool register = false;

                                            if (message.Read(ref Username)
                                            && message.Read(ref Password)
                                            && message.Read(ref register))
                                            {
                                                using (var db = AuthDatabase.Open())
                                                {
                                                    Logger.Information("Authentication login from {endpoint}", context.Channel.RemoteAddress.ToString());

                                                    if (Username.Length > 5 && Password.Length > 5)
                                                    {
                                                        var result = db.Find<AccountDto>(statement => statement
                                                               .Where($"{nameof(AccountDto.Username):C} = @{nameof(Username)}")
                                                               .Include<BanDto>(join => join.LeftOuterJoin())
                                                               .WithParameters(new { Username }));

                                                        account = result.FirstOrDefault();

                                                        if (account == null)
                                                        {
                                                            account = new AccountDto { Username = Username };

                                                            var newSalt = new byte[24];
                                                            using (var csprng = new RNGCryptoServiceProvider())
                                                                csprng.GetBytes(newSalt);

                                                            var hash = new byte[24];
                                                            using (var pbkdf2 = new Rfc2898DeriveBytes(Password, newSalt, 24000))
                                                                hash = pbkdf2.GetBytes(24);

                                                            account.Password = Convert.ToBase64String(hash);

                                                            account.Salt = Convert.ToBase64String(newSalt);

                                                            db.InsertAsync(account);
                                                        }

                                                        var salt = Convert.FromBase64String(account.Salt);

                                                        var passwordGuess = new byte[24];
                                                        using (var pbkdf2 = new Rfc2898DeriveBytes(Password, salt, 24000))
                                                            passwordGuess = pbkdf2.GetBytes(24);

                                                        var actualPassword = Convert.FromBase64String(account.Password);

                                                        uint difference = (uint)passwordGuess.Length ^ (uint)actualPassword.Length;

                                                        for (var i = 0; i < passwordGuess.Length && i < actualPassword.Length; i++)
                                                        {
                                                            difference |= (uint)(passwordGuess[i] ^ actualPassword[i]);
                                                        }

                                                        if (difference != 0 || string.IsNullOrWhiteSpace(account.Password))
                                                        {
                                                            Logger.Error("Wrong authentication credentials for {username} / {endpoint}", Username, context.Channel.RemoteAddress.ToString());
                                                            RmiMessage ack = new RmiMessage();
                                                            ack.Write(false);
                                                            ack.Write("Login failed");
                                                            RmiSend(context, 16, ack);
                                                        }
                                                        else
                                                        {
                                                            account.LoginToken = AuthHash.GetHash256($"{context.Channel.RemoteAddress.ToString()}-{account.Username}-{account.Password}").ToLower();
                                                            account.LastLogin = $"{DateTimeOffset.Now}";
                                                            account.AuthToken = "";
                                                            account.newToken = "";
                                                            db.UpdateAsync(account);

                                                            RmiMessage ack = new RmiMessage();
                                                            ack.Write(true);
                                                            ack.Write(account.LoginToken);
                                                            RmiSend(context, 16, ack);
                                                            Logger.Information("Authentication success for {username}", Username);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        Logger.Error("Wrong authentication credentials for {username} / {endpoint}", Username, context.Channel.RemoteAddress.ToString());
                                                        RmiMessage ack = new RmiMessage();
                                                        ack.Write(false);
                                                        ack.Write("Invalid length of username/password");
                                                        RmiSend(context, 16, ack);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                Logger.Error("Wrong login for {endpoint}", context.Channel.RemoteAddress.ToString());
                                                RmiMessage ack = new RmiMessage();
                                                ack.Write(false);
                                                ack.Write("Invalid loginpacket");
                                                RmiSend(context, 16, ack);
                                            }
                                            break;
                                        }
                                    case 17:
                                        context.CloseAsync();
                                        break;
                                    default:
                                        Logger.Error("Received unknown rmiID{rmi} from {endpoint}", RmiID, context.Channel.RemoteAddress.ToString());
                                        break;
                                }
                            }
                            break;
                        }
                    case MessageType.Notify:
                        {
                            string info = "";
                            message.Read(ref info);
                            Logger.Information("Received info! -> {received}", info);
                            break;
                        }
                    default:
                        Logger.Error("Received unknown coreID{coreid} from {endpoint}", coreID, context.Channel.RemoteAddress.ToString());
                        break;
                }
            }
        }
        else
        {
            Logger.Error("Received invalid packetstruct from {endpoint}", context.Channel.RemoteAddress.ToString());
            context.CloseAsync();
        }
    }
    
    public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
    {
#if DEBUG
        Logger.Error("Exception: " + exception);
#endif
        context.CloseAsync();
    }

    public void RmiSend(IChannelHandlerContext ctx, short RmiID, RmiMessage message)
    {
        RmiMessage rmiframe = new RmiMessage();
        rmiframe.Write(MessageType.Rmi);
        rmiframe.Write(RmiID);
        rmiframe.Write(message);
        SendA(ctx, rmiframe);
    }

    public void SendA(IChannelHandlerContext ctx, RmiMessage data)
    {
        RmiMessage coreframe = new RmiMessage();
        coreframe.Write(Magic);
        coreframe.WriteScalar(data.Length);
        coreframe.Write(data);

        IByteBuffer buffer = Unpooled.Buffer(coreframe.Length);
        buffer.WriteBytes(coreframe.Buffer);
        ctx.WriteAndFlushAsync(buffer);
    }
}
