using BlubLib.DotNetty.Handlers.MessageHandling;
using Netsphere.Network.Data.Club;
using Netsphere.Network.Message.Club;
using Netsphere.Network.Message.Game;
using ProudNet.Handlers;
using Serilog;
using Serilog.Core;

namespace Netsphere.Network.Services
{
    internal class ClubService : ProudMessageHandler
    {
        // ReSharper disable once InconsistentNaming
        private static readonly ILogger Logger = Log.ForContext(Constants.SourceContextPropertyName, nameof(ClubService));
        
        [MessageHandler(typeof(ClubSearchReqMessage))]
        public void CClubAddressReq(GameSession session, ClubSearchReqMessage message)
        {
            ClubSearchInfoDto[] testclans = new ClubSearchInfoDto[0];
            //testclans[0] = new ClubSearchInfoDto { Id = 1, Unk1 = "test1", Unk2 = "test2", Unk3 = "test3", Unk4 = 0, Unk5 = 0, Unk6 = 0, Unk7="test4", Unk8=0, Unk9=0, Unk10=0, Unk11="test5" };

            session.SendAsync(new ClubSearchAckMessage(0, testclans));
        }


        [MessageHandler(typeof(ClubNameCheckReqMessage))]
        public void ClubNameCheckReq(GameSession session, ClubNameCheckReqMessage message)
        {
            session.SendAsync(new ClubNameCheckAckMessage(0));
        }

        [MessageHandler(typeof(ClubCreateReqMessage))]
        public void ClubCreateReq(GameSession session, ClubCreateReqMessage message)
        {
            session.SendAsync(new ClubCreateAckMessage(0));
        }

        [MessageHandler(typeof(ClubCreateReq2Message))]
        public void ClubCreateReq2(GameSession session, ClubCreateReq2Message message)
        {
            session.SendAsync(new ClubCreateAck2Message(0));
        }
        [MessageHandler(typeof(ClubAddressReqMessage))]
        public void CClubAddressReq(GameSession session, ClubAddressReqMessage message)
        {
            Logger.ForAccount(session)
                .Debug("ClubAddressReq: {message}", message);

            session.SendAsync(new ClubAddressAckMessage("Kappa", 123));
        }

        [MessageHandler(typeof(ClubClubMemberInfoReq2Message))]
        public void ClubClubMemberInfoReq2(ChatSession session, ClubClubMemberInfoReq2Message message)
        {
            if(session.GameSession!= null)
                session.GameSession.SendAsync(new ServerResultAckMessage(ServerResult.ServerError));
        }

        //[MessageHandler(typeof(ClubInfoReqMessage))]
        //public void CClubInfoReq()
        //{
        //    //session.Send(new ClubInfoAckMessage(new PlayerClubInfoDto
        //    //{
        //    //    Unk1 = 0,
        //    //    Unk2 = 0,
        //    //    Unk3 = 0,
        //    //    Unk4 = 0,
        //    //    Unk5 = 0,
        //    //    Unk6 = 0,
        //    //    Unk7 = "",
        //    //    Unk8 = "",
        //    //    Unk9 = "Name?",
        //    //    ModeratorName = "Moderator",
        //    //    Unk11 = "",
        //    //    Unk12 = "",
        //    //    Unk13 = "",
        //    //}));
        //}
    }
}
