using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using BlubLib.DotNetty.Handlers.MessageHandling;
using BlubLib.Security.Cryptography;
using Dapper.FastCrud;
using Netsphere.Database.Auth;
using Netsphere.Network.Message.Auth;
using ProudNet;
using ProudNet.Handlers;
using Serilog;
using Serilog.Core;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Netsphere.Network.Service
{
    internal class AuthService : ProudMessageHandler
    {
        // ReSharper disable once InconsistentNaming
        private static readonly ILogger Logger = Log.ForContext(Constants.SourceContextPropertyName, nameof(AuthService));

        [MessageHandler(typeof(LoginEUReqMessage))]
        public async Task EULoginHandler(ProudSession session, LoginEUReqMessage message)
        {
            var ip = session.RemoteEndPoint.Address.ToString();

            AccountDto account = new AccountDto();
            using (var db = AuthDatabase.Open())
            {
                if (message.token != "")
                {
                    Logger.Information($"Login from {ip}");

                    var result = await db.FindAsync<AccountDto>(statement => statement
                            .Where($"{nameof(AccountDto.LoginToken):C} = @{nameof(message.token)}")
                            .Include<BanDto>(join => join.LeftOuterJoin())
                            .WithParameters(new { message.token }));
                    account = result.FirstOrDefault();

                    if (account != null)
                    {
                        if ((DateTimeOffset.Now - DateTimeOffset.Parse(account.LastLogin)).Minutes >= 5)
                        {

                            session.SendAsync(new LoginEUAckMessage(AuthLoginResult.Failed2));
                            Logger.Error("Wrong login for {ip}", ip);
                            return;
                        }
                    }
                    else
                    {
                        session.SendAsync(new LoginEUAckMessage(AuthLoginResult.Failed2));
                        Logger.Error("Wrong login for {ip}", ip);
                        return;
                    }
                }
                else if (message.AuthToken != "" && message.NewToken != "")
                {
                    Logger.Information("Session login from {ip}", ip);

                    var result = await db.FindAsync<AccountDto>(statement => statement
                            .Where($"{nameof(AccountDto.AuthToken):C} = @{nameof(message.AuthToken)}")
                            .Include<BanDto>(join => join.LeftOuterJoin())
                            .WithParameters(new { message.AuthToken }));
                    account = result.FirstOrDefault();

                    if (account != null)
                    {
                        if (account.AuthToken != message.AuthToken && account.newToken != message.NewToken)
                        {
                            session.SendAsync(new LoginEUAckMessage(AuthLoginResult.Failed2));
                            Logger.Error("Wrong session login for {ip} ({AuthToken}, {newToken})", ip, account.AuthToken, account.newToken);
                            return;
                        }
                    }
                    else
                    {
                        session.SendAsync(new LoginEUAckMessage(AuthLoginResult.Failed2));
                        Logger.Error("Wrong session login for {ip}", ip);
                        return;
                    }
                }

                var now = DateTimeOffset.Now.ToUnixTimeSeconds();
                var ban = account.Bans.FirstOrDefault(b => b.Date + (b.Duration ?? 0) > now);
                if (ban != null)
                {
                    var unbanDate = DateTimeOffset.FromUnixTimeSeconds(ban.Date + (ban.Duration ?? 0));
                    Logger.Error("{user} is banned until {until}", message.Username, unbanDate);
                    session.SendAsync(new LoginEUAckMessage(unbanDate));
                    return;
                }

                Logger.Information("Login success for {user}", account.Username);

                var entry = new LoginHistoryDto
                {
                    AccountId = account.Id,
                    Date = DateTimeOffset.Now.ToUnixTimeSeconds(),
                    IP = ip
                };
                await db.InsertAsync(entry);
            }


            string datetime = $"{DateTimeOffset.Now.DateTime}";
            var sessionId = Hash.GetUInt32<CRC32>($"<{account.Username}+{account.Password}>");
            var authsessionId = Hash.GetString<CRC32>($"<{account.Username}+{sessionId}+{datetime}>");
            var newsessionId = Hash.GetString<CRC32>($"<{authsessionId}+{sessionId}>");

            using (var db = AuthDatabase.Open())
            {
                account.LoginToken = "";
                account.AuthToken = authsessionId;
                account.newToken = newsessionId;
                await db.UpdateAsync(account);
            }
            session.SendAsync(new LoginEUAckMessage(AuthLoginResult.OK, (ulong)account.Id, sessionId, authsessionId, newsessionId, datetime));

        }

        [MessageHandler(typeof(ServerListReqMessage))]
        public void ServerListHandler(AuthServer server, ProudSession session)
        {
            session.SendAsync(new ServerListAckMessage(server.ServerManager.ToArray()));
        }

        static byte[] HexStringToByteArray(string hexString)
        {
            hexString = hexString.Replace("-", ""); // remove '-' symbols

            byte[] result = new byte[hexString.Length / 2];

            for (int i = 0; i < hexString.Length; i += 2)
            {
                result[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16); // base 16
            }

            return result;
        }

        [MessageHandler(typeof(GameDataReqMessage))]
        public void DataHandler(AuthServer server, ProudSession session)
        {
            string file1 = System.IO.File.ReadAllText(@"XBNFILE_1");
            string file2 = System.IO.File.ReadAllText(@"XBNFILE_2");
            string file3 = System.IO.File.ReadAllText(@"XBNFILE_3");
            string file4 = System.IO.File.ReadAllText(@"XBNFILE_4");
            string file5 = System.IO.File.ReadAllText(@"XBNFILE_5");
            string file6 = System.IO.File.ReadAllText(@"XBNFILE_6");
            string file7 = System.IO.File.ReadAllText(@"XBNFILE_7");
            string file8 = System.IO.File.ReadAllText(@"XBNFILE_8");
            string file9 = System.IO.File.ReadAllText(@"XBNFILE_9");
            string file10 = System.IO.File.ReadAllText(@"XBNFILE_10");
            string file11 = System.IO.File.ReadAllText(@"XBNFILE_11");
            string file12 = System.IO.File.ReadAllText(@"XBNFILE_12");
            string file13 = System.IO.File.ReadAllText(@"XBNFILE_13");
            string file14 = System.IO.File.ReadAllText(@"XBNFILE_14");
            string file15 = System.IO.File.ReadAllText(@"XBNFILE_15");


            session.SendAsync(new GameDataAckMessage(HexStringToByteArray(file1)), SendOptions.ReliableCompress);
            session.SendAsync(new GameDataAckMessage(HexStringToByteArray(file2)), SendOptions.ReliableCompress);
            session.SendAsync(new GameDataAckMessage(HexStringToByteArray(file3)), SendOptions.ReliableCompress);
            session.SendAsync(new GameDataAckMessage(HexStringToByteArray(file4)), SendOptions.ReliableCompress);
            session.SendAsync(new GameDataAckMessage(HexStringToByteArray(file5)), SendOptions.ReliableCompress);
            session.SendAsync(new GameDataAckMessage(HexStringToByteArray(file6)), SendOptions.ReliableCompress);
            session.SendAsync(new GameDataAckMessage(HexStringToByteArray(file7)), SendOptions.ReliableCompress);
            session.SendAsync(new GameDataAckMessage(HexStringToByteArray(file8)), SendOptions.ReliableCompress);
            session.SendAsync(new GameDataAckMessage(HexStringToByteArray(file9)), SendOptions.ReliableCompress);
            session.SendAsync(new GameDataAckMessage(HexStringToByteArray(file10)), SendOptions.ReliableCompress);
            session.SendAsync(new GameDataAckMessage(HexStringToByteArray(file11)), SendOptions.ReliableCompress);
            session.SendAsync(new GameDataAckMessage(HexStringToByteArray(file12)), SendOptions.ReliableCompress);
            session.SendAsync(new GameDataAckMessage(HexStringToByteArray(file13)), SendOptions.ReliableCompress);
            session.SendAsync(new GameDataAckMessage(HexStringToByteArray(file14)), SendOptions.ReliableCompress);
            session.SendAsync(new GameDataAckMessage(HexStringToByteArray(file15)), SendOptions.ReliableCompress);
        }
    }
}
