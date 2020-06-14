using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using BlubLib.DotNetty.Handlers.MessageHandling;
using ExpressMapper.Extensions;
using Netsphere.Network.Data.Game;
using Netsphere.Network.Message.Chat;
using Netsphere.Network.Message.Game;
using ProudNet.Handlers;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using BlubLib;

namespace Netsphere.Network.Services
{
    internal class ChannelService : ProudMessageHandler
    {
        // ReSharper disable once InconsistentNaming
        private static readonly ILogger Logger = Log.ForContext(Constants.SourceContextPropertyName, nameof(ChannelService));

        [MessageHandler(typeof(ChannelInfoReqMessage))]
        public void ChannelInfoReq(GameSession session, ChannelInfoReqMessage message)
        {
            if (session.Player.Channel == null)
            {
                try
                {
                    GameServer.Instance.ChannelManager[0].Join(session.Player, true);
                }
                catch (Exception ex)
                {

                }
            }
            switch (message.Request)
            {
                case ChannelInfoRequest.ChannelList:

                    ChannelInfoDto[] channels = GameServer.Instance.ChannelManager.Select(c => c.Map<Channel, ChannelInfoDto>()).ToArray();
                    channels = channels.Skip(1).ToArray();

                    foreach (ChannelInfoDto channel in channels)
                    {
                        if (channel.Name.Contains("Clan"))
                        {
                            channel.IsClanChannel = true;
                        }
                    }
                    session.SendAsync(new ChannelListInfoAckMessage(channels));
                    break;

                case ChannelInfoRequest.RoomList:
                case ChannelInfoRequest.RoomList2:
                    if (session.Player.Channel == null)
                        return;
                    List<RoomDto> roomlist = new List<RoomDto>();

                    foreach (Room room in session.Player.Channel.RoomManager)
                    {
                        RoomDto temproom = new RoomDto();
                        temproom.RoomId = (byte)room.Id;
                        temproom.PlayerCount = (byte)room.Players.Count;
                        temproom.PlayerLimit = room.Options.PlayerLimit;
                        temproom.State = (byte)room.GameRuleManager.GameRule.StateMachine.State;
                        temproom.State2 = (byte)room.GameRuleManager.GameRule.StateMachine.State;
                        temproom.GameRule = (int)room.Options.GameRule;
                        temproom.Map = (byte)room.Options.MapID;
                        temproom.WeaponLimit = room.Options.ItemLimit;
                        temproom.Name = room.Options.Name;
                        temproom.Password = room.Options.Password;
                        temproom.FMBURNMode = room.GetFMBurnModeInfo();
                        roomlist.Add(temproom);
                    }

                    RoomDto[] rooms = roomlist.ToArray();
                    session.SendAsync(new RoomListInfoAck2Message(rooms));

                    // RoomDto[] rooms = new RoomDto[51];

                    //for (int i = 0; i < 51; i++)
                    //{
                    //    rooms[i] = new RoomDto();
                    //    rooms[i].RoomId = (byte)i;
                    //    rooms[i].PlayerCount = 1;
                    //    rooms[i].PlayerLimit = 12;
                    //    rooms[i].State = (byte)0;
                    //    rooms[i].State2 = 0;
                    //    rooms[i].GameRule = (int)GameRule.Touchdown;
                    //    rooms[i].Map = 66;
                    //    rooms[i].WeaponLimit = 0;
                    //    rooms[i].Name = $"Testroom{i}";
                    //    rooms[i].Password = "";
                    //    //rooms[i].Unk1 = 0;
                    //    //rooms[i].Unk2 = 0;
                    //}

                    //int count = 0;
                    //foreach (Room room in session.Player.Channel.RoomManager)
                    //{
                    //    count++;
                    //}
                    //RoomDto[] rooms = new RoomDto[count];
                    //
                    //for (uint i = 0; i <= session.Player.Channel.RoomManager.Count; i++)
                    //{
                    //    rooms[i] = new RoomDto();
                    //    rooms[i].RoomId = (byte)session.Player.Channel.RoomManager[i].Id;
                    //    rooms[i].PlayerCount = (byte)session.Player.Channel.RoomManager[i].Players.Count;
                    //    rooms[i].PlayerLimit = session.Player.Channel.RoomManager[i].Options.PlayerLimit;
                    //    rooms[i].State = (byte)0;
                    //    rooms[i].GameRule = (int)session.Player.Channel.RoomManager[i].Options.GameRule;
                    //    rooms[i].Map = session.Player.Channel.RoomManager[i].Options.MapID;
                    //    rooms[i].WeaponLimit = session.Player.Channel.RoomManager[i].Options.ItemLimit;
                    //    rooms[i].Name = session.Player.Channel.RoomManager[i].Options.Name;
                    //    rooms[i].Password = session.Player.Channel.RoomManager[i].Options.Password;
                    //}
                    //session.SendAsync(new RoomListInfoAckMessage(session.Player.Channel.RoomManager.Select(r => r.Map<Room, RoomDto>()).ToArray()));

                    break;

                default:
                    Logger.ForAccount(session)
                        .Error("Invalid request {request}", message.Request);
                    break;
            }
        }

        [MessageHandler(typeof(ChannelEnterReqMessage))]
        public void CChannelEnterReq(GameSession session, ChannelEnterReqMessage message)
        {
            var channel = GameServer.Instance.ChannelManager[message.Channel];
            if (channel == null)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.NonExistingChannel));
                return;
            }

            session.Player.Channel?.Leave(session.Player, true);
            try
            {
                channel.Join(session.Player);
            }
            catch (ChannelLimitReachedException)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.ChannelLimitReached));
            }
        }


        [MessageHandler(typeof(ChannelLeaveReqMessage))]
        public void CChannelLeaveReq(GameSession session)
        {
            session.Player.Channel?.Leave(session.Player);
            GameServer.Instance.ChannelManager[0].Join(session.Player);
        }

        [MessageHandler(typeof(MessageChatReqMessage))]
        public void CChatMessageReq(ChatSession session, MessageChatReqMessage message)
        {
            switch (message.ChatType)
            {
                case ChatType.Channel:
                    session.Player.Channel.SendChatMessage(session.Player, message.Message);
                    break;

                case ChatType.Club:
                    // ToDo Change this when clans are implemented
                    session.SendAsync(new MessageChatAckMessage(ChatType.Club, session.Player.Account.Id, session.Player.Account.Nickname, message.Message));
                    break;

                default:
                    Logger.ForAccount(session)
                        .Warning("Invalid chat type {chatType}", message.ChatType);
                    break;
            }
        }

        ulong fakeitemid = 5115000;
        [MessageHandler(typeof(MessageWhisperChatReqMessage))]
        public void CWhisperChatMessageReq(ChatSession session, MessageWhisperChatReqMessage message)
        {
            var toPlr = GameServer.Instance.PlayerManager.Get(message.ToNickname);
            if (message.ToNickname.ToLower() != "server")
            {
                // ToDo Is there an answer for this case?
                if (toPlr == null)
                {
                    session.Player.ChatSession.SendAsync(new MessageChatAckMessage(ChatType.Channel, session.Player.Account.Id, "SYSTEM", $"{message.ToNickname} is not online"));
                    return;
                }

                // ToDo Is there an answer for this case?
                if (toPlr.DenyManager.Contains(session.Player.Account.Id))
                {
                    session.Player.ChatSession.SendAsync(new MessageChatAckMessage(ChatType.Channel, session.Player.Account.Id, "SYSTEM", $"{message.ToNickname} is ignoring you"));
                    return;
                }
                toPlr.ChatSession.SendAsync(new MessageWhisperChatAckMessage(0, toPlr.Account.Nickname,
                    session.Player.Account.Id, session.Player.Account.Nickname, message.Message));
            }
            else
            {
                string[] command = message.Message.Split(new string[] { " " }, StringSplitOptions.None);
                Array.Resize<string>(ref command, 1024);
                if (command[0] == "/ping")
                {
                    session.Player.ChatSession.SendAsync(new MessageChatAckMessage(ChatType.Channel, session.Player.Account.Id, "SYSTEM", $"{session.UnreliablePing}"));
                }
                else if (command[0] == "/additem")
                {
                    if(command[1].Length > 0)
                    {
                        fakeitemid++;
                        ItemDto item = new ItemDto();
                        item.ItemNumber = new ItemNumber(long.Parse(command[1]));
                        item.Id = fakeitemid;
                        item.PeriodType = ItemPeriodType.None;
                        item.Period = 1;
                        item.Durability = 2400;

                        session.GameSession.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update, item));
                        session.Player.ChatSession.SendAsync(new MessageChatAckMessage(ChatType.Channel, session.Player.Account.Id, "SYSTEM", $"Added {command[1]}, ID: {fakeitemid}"));
                    }
                }
                else
                {
                    var args = message.Message.GetArgs();
                    if (!GameServer.Instance.CommandManager.Execute(session.Player, args))
                        session.Player.ChatSession.SendAsync(new MessageChatAckMessage(ChatType.Channel, session.Player.Account.Id, "SYSTEM", $"Unknown command! Try to contact the server administrators"));
                }


            }
        }

        [MessageHandler(typeof(RoomQuickStartReqMessage))]
        public Task CQuickStartReq(GameSession session, RoomQuickStartReqMessage message)
        {
            //ToDo - Logic
            return session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
        }

        [MessageHandler(typeof(TaskReguestReqMessage))]
        public Task TaskRequestReq(GameSession session, TaskReguestReqMessage message)
        {
            //ToDo - Logic
            return session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
        }
    }
}
