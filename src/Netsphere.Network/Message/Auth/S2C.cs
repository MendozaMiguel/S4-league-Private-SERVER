using System;
using BlubLib.Serialization;
using Netsphere.Network.Data.Auth;
using Netsphere.Network.Serializers;
using ProudNet.Serialization.Serializers;

namespace Netsphere.Network.Message.Auth
{
    [BlubContract]
    public class LoginKRAckMessage : IAuthMessage
    {
        [BlubMember(0)]
        public ulong AccountId { get; set; }

        [BlubMember(1)]
        public uint SessionId { get; set; }

        [BlubMember(2, typeof(StringSerializer))]
        public string Unk1 { get; set; }

        [BlubMember(3, typeof(StringSerializer))]
        public string SessionId2 { get; set; }

        [BlubMember(4)]
        public AuthLoginResult Result { get; set; }

        [BlubMember(5, typeof(StringSerializer))]
        public string Unk2 { get; set; }

        [BlubMember(6, typeof(StringSerializer))]
        public string BannedUntil { get; set; }

        public LoginKRAckMessage()
        {
            Unk1 = "";
            SessionId2 = "";
            Unk2 = "";
            BannedUntil = "";
        }

        public LoginKRAckMessage(DateTimeOffset bannedUntil)
            : this()
        {
            Result = AuthLoginResult.Banned;
            BannedUntil = bannedUntil.ToString("yyyyMMddHHmmss");
        }

        public LoginKRAckMessage(AuthLoginResult result)
            : this()
        {
            Result = result;
        }

        public LoginKRAckMessage(AuthLoginResult result, ulong accountId, uint sessionId)
            : this()
        {
            Result = result;
            AccountId = accountId;
            SessionId = (uint)accountId;
            SessionId2 = sessionId.ToString();
        }
    }

    [BlubContract]
    public class LoginEUAckMessage : IAuthMessage
    {
        [BlubMember(0)]
        public ulong AccountId { get; set; }

        [BlubMember(1)]
        public uint SessionId { get; set; }

        [BlubMember(2, typeof(StringSerializer))]
        public string Unk1 { get; set; }

        [BlubMember(3, typeof(StringSerializer))]
        public string SessionId2 { get; set; }

        [BlubMember(4)]
        public AuthLoginResult Result { get; set; }

        [BlubMember(5, typeof(StringSerializer))]
        public string Unk2 { get; set; }

        [BlubMember(6, typeof(StringSerializer))]
        public string BannedUntil { get; set; }

        [BlubMember(7, typeof(StringSerializer))]
        public string mUnknow04 { get; set; }

        [BlubMember(8, typeof(StringSerializer))]
        public string AuthToken { get; set; }

        [BlubMember(9, typeof(StringSerializer))]
        public string NewToken { get; set; }

        [BlubMember(10, typeof(StringSerializer))]
        public string Datetime { get; set; }

        public LoginEUAckMessage()
        {
            Unk1 = "";
            SessionId2 = "";
            Unk2 = "";
            BannedUntil = "";
        }

        public LoginEUAckMessage(DateTimeOffset bannedUntil)
            : this()
        {
            Result = AuthLoginResult.Banned;
            BannedUntil = bannedUntil.ToString("yyyyMMddHHmmss");
        }

        public LoginEUAckMessage(AuthLoginResult result)
            : this()
        {
            Result = result;
        }

        public LoginEUAckMessage(AuthLoginResult result, ulong accountId, uint sessionId, string authsession, string newsession, string datetime)
            : this()
        {
            Result = result;
            AccountId = accountId;
            SessionId = (uint)accountId;
            SessionId2 = sessionId.ToString();
            AuthToken = authsession;
            NewToken = newsession;
            Datetime = datetime;
        }
    }

    [BlubContract]
    public class ServerListAckMessage : IAuthMessage
    {
        [BlubMember(0, typeof(ArrayWithIntPrefixSerializer))]
        public ServerInfoDto[] ServerList { get; set; }

        public ServerListAckMessage()
            : this(Array.Empty<ServerInfoDto>())
        { }

        public ServerListAckMessage(ServerInfoDto[] serverList)
        {
            ServerList = serverList;
        }
    }

    [BlubContract]
    public class OptionVersionCheckAckMessage : IAuthMessage
    {
        [BlubMember(0, typeof(ArrayWithScalarSerializer))]
        public byte[] Data { get; set; }

        public OptionVersionCheckAckMessage()
        {
            Data = Array.Empty<byte>();
        }

        public OptionVersionCheckAckMessage(byte[] data)
        {
            Data = data;
        }
    }

    [BlubContract]
    public class GameDataAckMessage : IAuthMessage
    {
        [BlubMember(0, typeof(ArraySerializer))]
        public byte[] Data { get; set; }

        public GameDataAckMessage()
        {
            Data = Array.Empty<byte>();
        }

        public GameDataAckMessage(byte[] data)
        {
            Data = data;
        }
    }

}
