using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using BlubLib.DotNetty.Handlers.MessageHandling;
using Netsphere.Game.GameRules;
using Netsphere.Network.Data.GameRule;
using Netsphere.Network.Message.Game;
using Netsphere.Network.Message.GameRule;
using Newtonsoft.Json;
using ProudNet.Handlers;
using Serilog;
using Serilog.Core;
using Netsphere.Network.Data.Game;
using ExpressMapper.Extensions;

namespace Netsphere.Network.Services
{
    internal class RoomService : ProudMessageHandler
    {
        // ReSharper disable once InconsistentNaming
        private static readonly ILogger Logger = Log.ForContext(Constants.SourceContextPropertyName, nameof(RoomService));

        [MessageHandler(typeof(RoomEnterPlayerReqMessage))]
        public void CEnterPlayerReq(GameSession session)
        {
            var plr = session.Player;
            if(plr.Room==null)
                return;

            plr.Room.ChangeMasterIfNeeded(plr);
            plr.Room.ChangeHostIfNeeded(plr);
            plr.Room.BroadcastBriefing();
            session.SendAsync(new GameChangeStateAckMessage(plr.Room.GameState));
            session.SendAsync(new GameChangeSubStateAckMessage(plr.Room.SubGameState));
            plr.Room.Broadcast(new RoomEnterPlayerForBookNameTagsAckMessage() { AccountId = (long)plr.Account.Id, Team = plr.RoomInfo.Team.Team, PlayerGameMode = plr.RoomInfo.Mode, Exp = plr.TotalExperience, Nickname = plr.Account.Nickname });
            foreach (Player playr in plr.Room.Players.Values.Where(p => p != plr))
            {
                plr.Session.SendAsync(new RoomEnterPlayerForBookNameTagsAckMessage() { AccountId = (long)playr.Account.Id, Team = playr.RoomInfo.Team.Team, PlayerGameMode = playr.RoomInfo.Mode, Exp = playr.TotalExperience, Nickname = playr.Account.Nickname });
            }

            //plr.Room.Broadcast(new RoomEnterPlayerInfoListForNameTagAckMessage());
            //plr.Room.Broadcast(new RoomEnterPlayerInfoAckMessage(plr.Map<Player, RoomPlayerDto>()));
        }

        [MessageHandler(typeof(RoomMakeReq2Message))]
        public void CMakeRoomReq2(GameSession session, RoomMakeReq2Message message)
        {
            var plr = session.Player;

            if (!plr.Channel.RoomManager.GameRuleFactory.Contains((GameRule)message.GameRule))
            {
                Logger.ForAccount(plr)
                    .Error("Game rule {gameRule} does not exist", (GameRule)message.GameRule);
                session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return;
            }

            if((GameRule)message.GameRule != GameRule.Practice && (GameRule)message.GameRule != GameRule.CombatTrainingTD && (GameRule)message.GameRule != GameRule.CombatTrainingDM)
            {
                var map = GameServer.Instance.ResourceCache.GetMaps().GetValueOrDefault(message.Map_ID);
                if (map == null)
                {
                    Logger.ForAccount(plr)
                        .Error("Map {map} does not exist", message.Map_ID);
                    session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                    return;
                }
                if (!map.GameRules.Contains((GameRule)message.GameRule))
                {
                    Logger.ForAccount(plr)
                        .Error("Map {mapId}({mapName}) is not available for game rule {gameRule}", map.Id, map.Name, (GameRule)message.GameRule);

                    session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                    return;
                }
            }
            else
            {
                message.Player_Limit = 16;
            }
            MatchKey matchkey = new MatchKey();
            
            if ((GameRule)message.GameRule == GameRule.CombatTrainingDM || (GameRule)message.GameRule == GameRule.CombatTrainingTD || (GameRule)message.GameRule == GameRule.Practice)
                message.FMBURNMode = 1;

            bool isfriendly = false;
            bool isbalanced = true;
            bool isburning = false;
            switch(message.FMBURNMode)
            {
                case 0:
                    isbalanced = true;
                    isfriendly = false;
                    break;
                case 1:
                    isbalanced = isfriendly = true;
                    break;
                case 2:
                    isbalanced = true;
                    isfriendly = false;
                    isburning = true;
                    break;
                case 3:
                    isburning = true;
                    isbalanced = isfriendly = true;
                    break;
            }

            var room = plr.Channel.RoomManager.Create(new RoomCreationOptions
            {
                Name = message.rName,
                GameRule = (GameRule)message.GameRule,
                PlayerLimit = message.Player_Limit,
                TimeLimit = TimeSpan.FromMinutes(message.Time),
                ScoreLimit = (ushort)message.Points,
                Password = message.rPassword,
                IsFriendly = isfriendly,
                IsBalanced = isbalanced,
                IsBurning = isburning,
                MapID = message.Map_ID,
                MinLevel = 0,
                MaxLevel = 100,
                ItemLimit = 0,
                IsNoIntrusion = false,
                ServerEndPoint = new IPEndPoint(IPAddress.Parse(Config.Instance.IP), Config.Instance.RelayListener.Port),
                Creator = plr,
            }, RelayServer.Instance.P2PGroupManager.Create(true));
            try
            {
                room.Join(plr);
            }
            catch (RoomAccessDeniedException)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.CantEnterRoom));
            }
            catch (RoomLimitReachedException)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.CantEnterRoom));
            }
            catch (RoomException)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
            }
            catch(Exception ex)
            {
                Logger.Error(ex.ToString());
                session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
            }
            return;
        }


        [MessageHandler(typeof(InGamePlayerResponseReqMessage))]
        public void InGamePlayerResponseReq(GameSession session, InGamePlayerResponseReqMessage message)
        {
        }

        [MessageHandler(typeof(RoomMakeReqMessage))]
        public void CMakeRoomReq(GameSession session, RoomMakeReqMessage message)
        {
            session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToCreateRoom));
            return;
        }

        [MessageHandler(typeof(RoomEnterReqMessage))]
        public void CGameRoomEnterReq(GameSession session, RoomEnterReqMessage message)
        {
            var plr = session.Player;
            var room = plr.Channel.RoomManager[message.RoomId];
            if (room == null)
            {
                Logger.ForAccount(plr)
                    .Error("Room {roomId} in channel {channelId} not found", message.RoomId, plr.Channel.Id);
                session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
                return;
            }

            if (room.IsChangingRules)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.RoomChangingRules));
                return;
            }

            if (!string.IsNullOrEmpty(room.Options.Password) && !room.Options.Password.Equals(message.Password))
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.PasswordError));
                return;
            }

            try
            {
                room.Join(plr);
            }
            catch (RoomAccessDeniedException)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.CantEnterRoom));
            }
            catch (RoomLimitReachedException)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.CantEnterRoom));
            }
            catch (RoomException)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
            }
            return;
        }

        [MessageHandler(typeof(RoomLeaveReqMessage))]
        public void CJoinTunnelInfoReq(GameSession session)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;
            plr.Room.Leave(plr);
        }

        [MessageHandler(typeof(RoomTeamChangeReqMessage))]
        public void CChangeTeamReq(GameSession session, RoomTeamChangeReqMessage message)
        {
            var plr = session.Player;

            if (plr.Room == null)
                return;
            try
            {
                plr.Room.TeamManager.ChangeTeam(plr, message.Team);
            }
            catch (RoomException ex)
            {
                Logger.ForAccount(plr)
                    .Error(ex, "Failed to change team to {team}", message.Team);
            }
        }

        [MessageHandler(typeof(RoomPlayModeChangeReqMessage))]
        public void CPlayerGameModeChangeReq(GameSession session, RoomPlayModeChangeReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;
            try
            {
                plr.Room.TeamManager.ChangeMode(plr, message.Mode);
            }
            catch (RoomException ex)
            {
                Logger.ForAccount(plr)
                    .Error(ex, "Failed to change mode to {mode}", message.Mode);
            }
        }


        [MessageHandler(typeof(GameLoadingSuccessReqMessage))]
        public void CLoadingSucceeded(GameSession session)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;
            plr.RoomInfo.hasLoaded = true;
            plr.Room.Broadcast(new RoomGameEndLoadingAckMessage() { Unk = (long)plr.Account.Id});
            if (plr.Room.GameState != GameState.Playing)
            {
                var attachedmembers = plr.Room.Players.Where(member => (member.Value.RoomInfo.IsReady || member.Value == plr.Room.Master)).ToDictionary(member => member.Key, member => member.Value);
                int membercount = attachedmembers.Count;
                int loadedmembercount = 0;
                foreach (Player member in plr.Room.Players.Values)
                {
                    if (member.RoomInfo.hasLoaded)
                    {
                        loadedmembercount++;
                    }
                }
                var stateMachine = plr.Room.GameRuleManager.GameRule.StateMachine;
                if (loadedmembercount == membercount)
                {
                    if (stateMachine.CanFire(GameRuleStateTrigger.StartGame))
                        stateMachine.Fire(GameRuleStateTrigger.StartGame);
                }
            }
            else
            {
                session.SendAsync(new GameRefreshGameRuleInfoAckMessage(plr.Room.GameState, plr.Room.SubGameState, (int)(plr.Room.RoundTime.TotalSeconds)));
                session.SendAsync(new RoomGameStartAckMessage());
            }
        }
        [MessageHandler(typeof(RoomIntrudeRoundReq2Message))]
        public void CIntrudeRoundReq2(GameSession session)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;
            if (plr.Room.GameRuleManager.GameRule.StateMachine.State != GameRuleState.Waiting)
            {
                plr.Session.SendAsync(new RoomGameLoadingAckMessage());
            }
            else
            {
                plr.RoomInfo.IsReady = !plr.RoomInfo.IsReady;
                plr.Room.Broadcast(new RoomReadyRoundAckMessage(plr.Account.Id, plr.RoomInfo.IsReady));
            }
        }
        
        [MessageHandler(typeof(RoomBeginRoundReq2Message))]
        public void CBeginRoundReq2(GameSession session)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;
            var stateMachine = plr.Room.GameRuleManager.GameRule.StateMachine;

            if (stateMachine.CanFire(GameRuleStateTrigger.StartPrepare))
                stateMachine.Fire(GameRuleStateTrigger.StartPrepare);
            else
                session.SendAsync(new GameEventMessageAckMessage(GameEventMessage.CantStartGame, 0, 0, 0, ""));
        }

        [MessageHandler(typeof(RoomReadyRoundReq2Message))]
        public void CReadyRoundReq2(GameSession session)
        {
            var plr = session.Player;

            if (plr.Room == null)
                return;
            if (plr.Room.GameRuleManager.GameRule.StateMachine.State != GameRuleState.Waiting)
            {
                plr.Session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
            }
            else
            {
                plr.RoomInfo.IsReady = !plr.RoomInfo.IsReady;
                plr.Room.Broadcast(new RoomReadyRoundAckMessage(plr.Account.Id, plr.RoomInfo.IsReady));
            }
        }
        //[MessageHandler(typeof(RoomBeginRoundReqMessage))]
        //public void CBeginRoundReq(GameSession session)
        //{
        //    var plr = session.Player;
        //    if (plr.Room == null)
        //        return;
        //    var stateMachine = plr.Room.GameRuleManager.GameRule.StateMachine;
        //
        //    if (stateMachine.CanFire(GameRuleStateTrigger.StartGame))
        //        stateMachine.Fire(GameRuleStateTrigger.StartGame);
        //    else
        //        session.SendAsync(new GameEventMessageAckMessage(GameEventMessage.CantStartGame, 0, 0, 0, ""));
        //}
        
        //[MessageHandler(typeof(RoomReadyRoundReqMessage))]
        //public void CReadyRoundReq(GameSession session)
        //{
        //    var plr = session.Player;
        //
        //    if (plr.Room == null)
        //        return;
        //    if (plr.Room.GameRuleManager.GameRule.StateMachine.State != GameRuleState.Waiting)
        //    {
        //        var stateMachine = plr.Room.GameRuleManager.GameRule.StateMachine;
        //
        //        stateMachine.Fire(GameRuleStateTrigger.StartGame);
        //    }
        //    else
        //    {
        //        plr.RoomInfo.IsReady = !plr.RoomInfo.IsReady;
        //        plr.Room.Broadcast(new RoomReadyRoundAckMessage(plr.Account.Id, plr.RoomInfo.IsReady));
        //    }
        //}

        [MessageHandler(typeof(GameEventMessageReqMessage))]
        public void CEventMessageReq(GameSession session, GameEventMessageReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;

            plr.Room.Broadcast(new GameEventMessageAckMessage(message.Event, session.Player.Account.Id, message.Unk1, message.Value, ""));
            //if (message.Event == GameEventMessage.BallReset && plr == plr.Room.Host)
            //{
            //    plr.Room.Broadcast(new GameEventMessageAckMessage(GameEventMessage.BallReset, 0, 0, 0, ""));
            //    return;
            //}

            //if (message.Event != GameEventMessage.StartGame)
            //    return;

            if (plr.Room.GameRuleManager.GameRule.StateMachine.IsInState(GameRuleState.Playing) && plr.RoomInfo.State == PlayerState.Lobby)
            {
                plr.RoomInfo.State = plr.RoomInfo.Mode == PlayerGameMode.Normal
                    ? PlayerState.Alive
                    : PlayerState.Spectating;

                plr.Room.BroadcastBriefing();
            }
        }

        //[MessageHandler(typeof(CItemsChangeReqMessage))]
        //public void CItemsChangeReq(GameSession session, CItemsChangeReqMessage message)
        //{
        //    var plr = session.Player;

        //    Logger.ForAccount(session)
        //       .Debug("Item sync - {unk1}", message.Unk1);

        //    if (message.Unk2.Length > 0)
        //    {
        //        Logger.ForAccount(session)
        //            .Warning("{unk2}", message.Unk2);
        //    }

        //    var @char = plr.CharacterManager.CurrentCharacter;
        //    var unk1 = new ChangeItemsUnkDto
        //    {
        //        AccountId = plr.Account.Id,
        //        Skills = @char.Skills.GetItems().Select(item => item?.ItemNumber ?? 0).ToArray(),
        //        Weapons = @char.Weapons.GetItems().Select(item => item?.ItemNumber ?? 0).ToArray(),
        //        Unk4 = message.Unk1.Unk4,
        //        Unk5 = message.Unk1.Unk5,
        //        Unk6 = message.Unk1.Unk6,
        //        HP = plr.GetMaxHP(),
        //        Unk8 = message.Unk1.Unk8
        //    };

        //    plr.Room.Broadcast(new RoomChangeItemAckMessage(unk1, message.Unk2));
        //}

        //[MessageHandler(typeof(GameAvatarChangeReqMessage))]
        //public void CAvatarChangeReq(GameSession session, GameAvatarChangeReqMessage message)
        //{
        //    var plr = session.Player;

        //    Logger.ForAccount(session)
        //        .Debug("Avatar sync - {unk1}", message.Unk1);

        //    if (message.Unk2.Length > 0)
        //    {
        //        Logger.ForAccount(session)
        //            .Warning("{unk2}", message.Unk2);
        //    }

        //    var @char = plr.CharacterManager.CurrentCharacter;
        //    var unk1 = new ChangeAvatarUnk1Dto
        //    {
        //        AccountId = plr.Account.Id,
        //        Skills = @char.Skills.GetItems().Select(item => item?.ItemNumber ?? 0).ToArray(),
        //        Weapons = @char.Weapons.GetItems().Select(item => item?.ItemNumber ?? 0).ToArray(),
        //        Costumes = new ItemNumber[(int)CostumeSlot.Max],
        //        Unk5 = message.Unk1.Unk5,
        //        Unk6 = message.Unk1.Unk6,
        //        Unk7 = message.Unk1.Unk7,
        //        Unk8 = message.Unk1.Unk8,
        //        Gender = plr.CharacterManager.CurrentCharacter.Gender,
        //        HP = plr.GetMaxHP(),
        //        Unk11 = message.Unk1.Unk11
        //    };

        //    // If no item equipped use the default item the character was created with
        //    for (CostumeSlot slot = 0; slot < CostumeSlot.Max; slot++)
        //    {
        //        var item = plr.CharacterManager.CurrentCharacter.Costumes.GetItem(slot)?.ItemNumber ?? 0;
        //        switch (slot)
        //        {
        //            case CostumeSlot.Hair:
        //                if (item == 0)
        //                    item = @char.Hair.ItemNumber;
        //                break;

        //            case CostumeSlot.Face:
        //                if (item == 0)
        //                    item = @char.Face.ItemNumber;
        //                break;

        //            case CostumeSlot.Shirt:
        //                if (item == 0)
        //                    item = @char.Shirt.ItemNumber;
        //                break;

        //            case CostumeSlot.Pants:
        //                if (item == 0)
        //                    item = @char.Pants.ItemNumber;
        //                break;

        //            case CostumeSlot.Gloves:
        //                if (item == 0)
        //                    item = @char.Gloves.ItemNumber;
        //                break;

        //            case CostumeSlot.Shoes:
        //                if (item == 0)
        //                    item = @char.Shoes.ItemNumber;
        //                break;
        //        }
        //        unk1.Costumes[(int)slot] = item;
        //    }

        //    plr.Room.Broadcast(new GameAvatarChangeAckMessage(unk1, message.Unk2));
        //}

        //[MessageHandler(typeof(RoomChangeRuleNotifyReqMessage))]
        //public void CChangeRuleNotifyReq(GameSession session, RoomChangeRuleNotifyReqMessage message)
        //{
        //    session.Player.Room.ChangeRules(message.Settings);
        //}

        //[MessageHandler(typeof(RoomLeaveReguestReqMessage))]
        //public void CLeavePlayerRequestReq(GameSession session, RoomLeaveReguestReqMessage message)
        //{
        //    var plr = session.Player;
        //    var room = plr.Room;

        //    switch (message.Reason)
        //    {
        //        case RoomLeaveReason.Kicked:
        //            // Only the master can kick people and kick is only allowed in the lobby
        //            if (room.Master != plr &&
        //                !room.GameRuleManager.GameRule.StateMachine.IsInState(GameRuleState.Waiting))
        //                return;
        //            break;

        //        case RoomLeaveReason.AFK:
        //            // The client kicks itself when afk is detected
        //            if (message.AccountId != plr.Account.Id)
        //                return;
        //            break;

        //        default:
        //            // Dont allow any other reasons for now
        //            return;
        //    }

        //    var targetPlr = room.Players.GetValueOrDefault(message.AccountId);
        //    if (targetPlr == null)
        //        return;

        //    room.Leave(targetPlr, message.Reason);
        //}

        #region Scores

        [MessageHandler(typeof(ScoreKillReqMessage))]
        public void CScoreKillReq(GameSession session, ScoreKillReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;

            if(plr.Room.Options.GameRule == GameRule.Practice)
            {
                plr.Room.Broadcast(new ScoreKillAckMessage(message.Score));
                plr.Session.SendAsync(new InGamePlayerResponseOfDeathAckMessage());
                return;
            }

            plr.RoomInfo.PeerId = message.Score.Target;

            var room = plr.Room;
            var killer = room.Players.GetValueOrDefault(message.Score.Killer.AccountId);
            //if (killer == null)
            //    return;
            if (killer != null)
             killer.RoomInfo.PeerId = message.Score.Killer;

            room.GameRuleManager.GameRule.OnScoreKill(killer, null, plr, message.Score.Weapon, message.Score.Target, message.Score.Killer, null);
        }

        [MessageHandler(typeof(ScoreKillAssistReqMessage))]
        public void CScoreKillAssistReq(GameSession session, ScoreKillAssistReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;
            plr.RoomInfo.PeerId = message.Score.Target;

            var room = plr.Room;
            var assist = room.Players.GetValueOrDefault(message.Score.Assist.AccountId);
            //if (assist == null)
            //    return;
            if (assist != null)
                assist.RoomInfo.PeerId = message.Score.Assist;

            var killer = room.Players.GetValueOrDefault(message.Score.Killer.AccountId);
            //if (killer == null)
            //    return;
            if (killer != null)
                killer.RoomInfo.PeerId = message.Score.Killer;

            room.GameRuleManager.GameRule.OnScoreKill(killer, assist, plr, message.Score.Weapon, message.Score.Target, message.Score.Killer, message.Score.Assist);
        }

        [MessageHandler(typeof(ScoreOffenseReqMessage))]
        public void CScoreOffenseReq(GameSession session, ScoreOffenseReqMessage message)
        {
            var plr = session.Player;

            if (plr.Room == null)
                return;
            
            plr.RoomInfo.PeerId = message.Score.Target;

            var room = plr.Room;
            var killer = room.Players.GetValueOrDefault(message.Score.Killer.AccountId);
            //if (killer == null)
            //    return;
            if (killer != null)
                killer.RoomInfo.PeerId = message.Score.Killer;

            if (room.Options.GameRule == GameRule.Touchdown)
                ((TouchdownGameRule)room.GameRuleManager.GameRule).OnScoreOffense(killer, null, plr, message.Score.Weapon/*, message.Score.Target, message.Score.Killer, null*/);
            else if(room.Options.GameRule == GameRule.CombatTrainingTD)
                ((TouchdownTrainingGameRule)room.GameRuleManager.GameRule).OnScoreOffense(killer, null, plr, message.Score.Weapon, message.Score.Target, message.Score.Killer, null);
        }

        [MessageHandler(typeof(ScoreOffenseAssistReqMessage))]
        public void CScoreOffenseAssistReq(GameSession session, ScoreOffenseAssistReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;
            plr.RoomInfo.PeerId = message.Score.Target;

            var room = plr.Room;
            var assist = room.Players.GetValueOrDefault(message.Score.Assist.AccountId);

            if (assist != null)
                assist.RoomInfo.PeerId = message.Score.Assist;

            var killer = room.Players.GetValueOrDefault(message.Score.Killer.AccountId);

            if (killer != null)
                killer.RoomInfo.PeerId = message.Score.Killer;

            if (room.Options.GameRule == GameRule.Touchdown)
                ((TouchdownGameRule)room.GameRuleManager.GameRule).OnScoreOffense(killer, null, plr, message.Score.Weapon/*, message.Score.Target, message.Score.Killer, message.Score.Assist*/);
            else if (room.Options.GameRule == GameRule.CombatTrainingTD)
                ((TouchdownTrainingGameRule)room.GameRuleManager.GameRule).OnScoreOffense(killer, null, plr, message.Score.Weapon, message.Score.Target, message.Score.Killer, message.Score.Assist);
        }

        [MessageHandler(typeof(ScoreDefenseReqMessage))]
        public void CScoreDefenseReq(GameSession session, ScoreDefenseReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;
            plr.RoomInfo.PeerId = message.Score.Target;

            var room = plr.Room;
            var killer = room.Players.GetValueOrDefault(message.Score.Killer.AccountId);

            if (killer != null)
                killer.RoomInfo.PeerId = message.Score.Killer;

            if (room.Options.GameRule == GameRule.Touchdown)
                ((TouchdownGameRule)room.GameRuleManager.GameRule).OnScoreDefense(killer, null, plr, message.Score.Weapon/*, message.Score.Weapon, message.Score.Target, message.Score.Killer, null*/);
            else if (room.Options.GameRule == GameRule.CombatTrainingTD)
                ((TouchdownTrainingGameRule)room.GameRuleManager.GameRule).OnScoreDefense(killer, null, plr, message.Score.Weapon, message.Score.Target, message.Score.Killer, null);
        }

        [MessageHandler(typeof(ScoreDefenseAssistReqMessage))]
        public void CScoreDefenseAssistReq(GameSession session, ScoreDefenseAssistReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;
            plr.RoomInfo.PeerId = message.Score.Target;

            var room = plr.Room;
            var assist = room.Players.GetValueOrDefault(message.Score.Assist.AccountId);

            if (assist != null)
                assist.RoomInfo.PeerId = message.Score.Assist;

            var killer = room.Players.GetValueOrDefault(message.Score.Killer.AccountId);
            
            if (killer != null)
                killer.RoomInfo.PeerId = message.Score.Killer;

            if (room.Options.GameRule == GameRule.Touchdown)
                ((TouchdownGameRule)room.GameRuleManager.GameRule).OnScoreDefense(killer, assist, plr, message.Score.Weapon /*, message.Score.Weapon, message.Score.Target, message.Score.Killer, message.Score.Assist*/);
            else if (room.Options.GameRule == GameRule.CombatTrainingTD)
                ((TouchdownTrainingGameRule)room.GameRuleManager.GameRule).OnScoreDefense(killer, assist, plr, message.Score.Weapon, message.Score.Target, message.Score.Killer, message.Score.Assist);
        }

        [MessageHandler(typeof(ScoreTeamKillReqMessage))]
        public void CScoreTeamKillReq(GameSession session, ScoreTeamKillReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;
            plr.RoomInfo.PeerId = message.Score.Target;

            var room = plr.Room;
            var killer = room.Players.GetValueOrDefault(message.Score.Killer.AccountId);
            
            if (killer != null)
                killer.RoomInfo.PeerId = message.Score.Killer;

            room.GameRuleManager.GameRule.OnScoreKill(killer, null, plr, message.Score.Weapon, message.Score.Target, message.Score.Killer, null);
        }

        [MessageHandler(typeof(ScoreHealAssistReqMessage))]
        public void CScoreHealAssistReq(GameSession session, ScoreHealAssistReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;
            plr.RoomInfo.PeerId = message.Id;

            var room = plr.Room;
            room.GameRuleManager.GameRule.OnScoreHeal(plr, message.Id);
        }

        [MessageHandler(typeof(ScoreSuicideReqMessage))]
        public void CScoreSuicideReq(GameSession session, ScoreSuicideReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;
            plr.RoomInfo.PeerId = message.Id;

            var room = plr.Room;
            room.GameRuleManager.GameRule.OnScoreSuicide(plr, message.Id);
        }

        [MessageHandler(typeof(ScoreReboundReqMessage))]
        public void CScoreReboundReq(GameSession session, ScoreReboundReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;
            var room = plr.Room;

            Player newPlr = null;
            Player oldPlr = null;

            if (message.NewId != 0)
                newPlr = room.Players.GetValueOrDefault(message.NewId.AccountId);

            if (message.OldId != 0)
                oldPlr = room.Players.GetValueOrDefault(message.OldId.AccountId);

            if (newPlr != null)
                newPlr.RoomInfo.PeerId = message.NewId;

            if (oldPlr != null)
                oldPlr.RoomInfo.PeerId = message.OldId;

            if (room.Options.GameRule == GameRule.Touchdown)
                ((TouchdownGameRule)room.GameRuleManager.GameRule).OnScoreRebound(newPlr, oldPlr);
            else if (room.Options.GameRule == GameRule.CombatTrainingTD)
                ((TouchdownTrainingGameRule)room.GameRuleManager.GameRule).OnScoreRebound(newPlr, oldPlr, message.NewId, message.OldId);
        }

        [MessageHandler(typeof(ScoreGoalReqMessage))]
        public void CScoreGoalReq(GameSession session, ScoreGoalReqMessage message)
        {
            var plr = session.Player;
            if (plr.Room == null)
                return;
            var room = plr.Room;

            var target = room.Players.GetValueOrDefault(message.PeerId.AccountId);
            if (target == null)
                return;
            target.RoomInfo.PeerId = message.PeerId;

            if (room.Options.GameRule == GameRule.Touchdown)
                ((TouchdownGameRule)room.GameRuleManager.GameRule).OnScoreGoal(target);
            else if (room.Options.GameRule == GameRule.CombatTrainingTD)
                ((TouchdownTrainingGameRule)room.GameRuleManager.GameRule).OnScoreGoal(target, message.PeerId);
        }

        #endregion
    }
}
