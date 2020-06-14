using System;
using System.Linq;
using Netsphere.Network.Data.GameRule;
using Netsphere.Network.Message.GameRule;
using Stateless;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace

namespace Netsphere.Game.GameRules
{
    internal abstract class GameRuleBase
    {
        private static readonly TimeSpan PreHalfTimeWaitTime = TimeSpan.FromSeconds(9);
        private static readonly TimeSpan PreResultWaitTime = TimeSpan.FromSeconds(9);
        private static readonly TimeSpan HalfTimeWaitTime = TimeSpan.FromSeconds(24);
        private static readonly TimeSpan ResultWaitTime = TimeSpan.FromSeconds(14);

        public abstract GameRule GameRule { get; }
        public Room Room { get; }
        public abstract Briefing Briefing { get; }
        public StateMachine<GameRuleState, GameRuleStateTrigger> StateMachine { get; }

        public TimeSpan GameTime { get; private set; }
        public TimeSpan RoundTime { get; private set; }

        protected GameRuleBase(Room room)
        {
            Room = room;
            StateMachine = new StateMachine<GameRuleState, GameRuleStateTrigger>(GameRuleState.Waiting);
            StateMachine.OnTransitioned(StateMachine_OnTransition);
        }

        public virtual void Initialize()
        { }

        public virtual void Cleanup()
        { }

        public virtual void Reload()
        { }

        public virtual void Update(TimeSpan delta)
        {
            RoundTime += delta;
            Room.RoundTime = RoundTime;

            if (StateMachine.IsInState(GameRuleState.Playing))
            {
                GameTime += delta;

                foreach (var plr in Room.TeamManager.PlayersPlaying)
                {
                    plr.RoomInfo.PlayTime += delta;
                    plr.RoomInfo.CharacterPlayTime[plr.CharacterManager.CurrentSlot] += delta;
                }
                
                //foreach (Player dead in Room.TeamManager.Players.Where(plr => plr.RoomInfo.State == PlayerState.Dead))
                //{
                //    int maxdeadtime = 10;
                //    int deadsince = (int)((RoundTime - dead.RoomInfo.DeadTime).TotalSeconds);
                //    if (deadsince >= maxdeadtime)
                //    {
                //        dead.RoomInfo.State = PlayerState.Alive;
                //        dead.Session.SendAsync(new GameEventMessageAckMessage(GameEventMessage.ResetRound, 2, 0, 0, ""));
                //        dead.RoomInfo.DeadTime = TimeSpan.Zero;
                //    }
                //    else
                //    {
                //        int timeleft = (int)(maxdeadtime - (RoundTime - dead.RoomInfo.DeadTime).TotalSeconds + 1);
                //        dead.Session.SendAsync(new GameEventMessageAckMessage((GameEventMessage)21, 2, 0, (ushort)timeleft, timeleft.ToString()));
                //    }
                //}
            }

            #region HalfTime

            if (StateMachine.IsInState(GameRuleState.EnteringHalfTime))
            {
                foreach (Player dead in Room.TeamManager.Players.Where(plr => plr.RoomInfo.State == PlayerState.Dead))
                {
                    dead.RoomInfo.State = PlayerState.Alive;
                    dead.RoomInfo.DeadTime = TimeSpan.Zero;
                }
                if(RoundTime >= PreHalfTimeWaitTime)
                {
                    RoundTime = TimeSpan.Zero;
                    StateMachine.Fire(GameRuleStateTrigger.StartHalfTime);
                }
                else
                {
                    Room.Broadcast(new GameEventMessageAckMessage(GameEventMessage.HalfTimeIn, 2, 0, 0,
                        ((int)(PreHalfTimeWaitTime - RoundTime).TotalSeconds + 1).ToString()));
                }
            }

            if (StateMachine.IsInState(GameRuleState.HalfTime))
            {
                if (RoundTime >= HalfTimeWaitTime)
                    StateMachine.Fire(GameRuleStateTrigger.StartSecondHalf);
            }

            #endregion

            #region Result

            if (StateMachine.IsInState(GameRuleState.EnteringResult))
            {
                foreach (Player dead in Room.TeamManager.Players.Where(plr => plr.RoomInfo.State == PlayerState.Dead))
                {
                    dead.RoomInfo.State = PlayerState.Alive;
                    dead.RoomInfo.DeadTime = TimeSpan.Zero;
                }
                if (RoundTime >= PreResultWaitTime)
                {
                    RoundTime = TimeSpan.Zero;
                    StateMachine.Fire(GameRuleStateTrigger.StartResult);
                }
                else
                {
                    Room.Broadcast(new GameEventMessageAckMessage(GameEventMessage.ResultIn, 3, 0, 0,
                        (int)(PreResultWaitTime - RoundTime).TotalSeconds + 1 + " second(s)"));
                }
            }

            if (StateMachine.IsInState(GameRuleState.Result))
            {
                if (RoundTime >= ResultWaitTime)
                {
                    StateMachine.Fire(GameRuleStateTrigger.EndGame);
                }
            }

            #endregion
        }

        public abstract PlayerRecord GetPlayerRecord(Player plr);

        #region Scores

        public void Respawn(Player victim)
        {
            victim.Session.SendAsync(new InGamePlayerResponseOfDeathAckMessage());
            //victim.RoomInfo.State = PlayerState.Dead;
            //victim.RoomInfo.DeadTime = GameTime;
        }
        
        public virtual void OnScoreKill(Player killer, Player assist, Player target, AttackAttribute attackAttribute, LongPeerId ScoreTarget = null, LongPeerId ScoreKiller = null, LongPeerId ScoreAssist = null)
        {
            killer.RoomInfo.Stats.Kills++;
            target.RoomInfo.Stats.Deaths++;

            Respawn(target);
            if (assist != null)
            {
                assist.RoomInfo.Stats.KillAssists++;

                Room.Broadcast(
                    new ScoreKillAssistAckMessage(new ScoreAssistDto(killer.RoomInfo.PeerId, assist.RoomInfo.PeerId,
                        target.RoomInfo.PeerId, attackAttribute)));
            }
            else
            {
                Room.Broadcast(
                    new ScoreKillAckMessage(new ScoreDto(killer.RoomInfo.PeerId, target.RoomInfo.PeerId,
                        attackAttribute)));
            }
        }

        public virtual void OnScoreTeamKill(Player killer, Player target, AttackAttribute attackAttribute, LongPeerId Killer = null, LongPeerId Target = null)
        {
            target.RoomInfo.Stats.Deaths++;

            Respawn(target);
            Room.Broadcast(
                new ScoreTeamKillAckMessage(new Score2Dto(killer.RoomInfo.PeerId, target.RoomInfo.PeerId,
                    attackAttribute)));
        }

        public virtual void OnScoreHeal(Player plr, LongPeerId Target = null)
        {
            Room.Broadcast(new ScoreHealAssistAckMessage(plr.RoomInfo.PeerId));
        }

        public virtual void OnScoreSuicide(Player plr, LongPeerId Target = null)
        {
            Respawn(plr);
            plr.RoomInfo.Stats.Deaths++;
            Room.Broadcast(new ScoreSuicideAckMessage(plr.RoomInfo.PeerId, AttackAttribute.KillOneSelf));
        }

        #endregion

        private void StateMachine_OnTransition(StateMachine<GameRuleState, GameRuleStateTrigger>.Transition transition)
        {
            RoundTime = TimeSpan.Zero;

            switch (transition.Trigger)
            {
                case GameRuleStateTrigger.StartPrepare:
                    {
                        foreach ( // ToDo Use one of the Player properties
                            var plr in
                                Room.TeamManager.Values.SelectMany(
                                    team =>
                                        team.Values.Where(
                                            plr =>
                                                Room.Master == plr ||
                                                plr.RoomInfo.Mode == PlayerGameMode.Spectate)))
                        {
                            plr.RoomInfo.Reset();
                            plr.RoomInfo.State = plr.RoomInfo.Mode == PlayerGameMode.Normal
                                ? PlayerState.Alive
                                : PlayerState.Spectating;

                            foreach (Player member in plr.Room.Players.Values)
                            {
                                if (member.RoomInfo.IsReady || member == Room.Master)
                                {
                                    member.Session.SendAsync(new RoomGameLoadingAckMessage());
                                }
                            }
                            Room.BroadcastBriefing();
                        }
                    }
                    break;
                default:
                    break;
            }
            switch (transition.Destination)
            {
                case GameRuleState.FirstHalf:
                    GameTime = TimeSpan.Zero;
                    foreach (var team in Room.TeamManager.Values)
                        team.Score = 0;
                    foreach (Player member in Room.Players.Values.Where(i => i.RoomInfo.hasLoaded))
                    {
                        member.Session.SendAsync(new RoomBeginRoundAckMessage());
                        member.Session.SendAsync(new RoomGameStartAckMessage());
                    }

                    Room.BroadcastBriefing();
                    Room.GameState = GameState.Playing;
                    Room.Broadcast(new GameChangeStateAckMessage(Room.GameState));
                    Room.Broadcast(new GameChangeSubStateAckMessage(Room.SubGameState));
                    break;

                case GameRuleState.HalfTime:
                    {
                        Room.SubGameState = GameTimeState.HalfTime;
                        Room.Broadcast(new GameChangeSubStateAckMessage(Room.SubGameState));
                    }
                    break;

                case GameRuleState.SecondHalf:
                    {
                        Room.SubGameState = GameTimeState.SecondHalf;
                        Room.Broadcast(new GameChangeSubStateAckMessage(Room.SubGameState));
                    }
                    break;
                case GameRuleState.Result:
                    foreach (var plr in Room.TeamManager.PlayersPlaying)
                    {
                        foreach (var @char in plr.CharacterManager)
                        {
                            var loss = (int)plr.RoomInfo.CharacterPlayTime[@char.Slot].TotalMinutes *
                                       Config.Instance.Game.DurabilityLossPerMinute;
                            loss += (int)plr.RoomInfo.Stats.Deaths * Config.Instance.Game.DurabilityLossPerDeath;
                    
                            foreach (var item in @char.Weapons.GetItems().Where(item => item != null && item.Durability != -1))
                                item.LoseDurabilityAsync(loss).Wait();
                    
                            foreach (var item in @char.Costumes.GetItems().Where(item => item != null && item.Durability != -1))
                                item.LoseDurabilityAsync(loss).Wait();
                    
                            foreach (var item in @char.Skills.GetItems().Where(item => item != null && item.Durability != -1))
                                item.LoseDurabilityAsync(loss).Wait();
                        }
                    }

                    foreach (var plr in Room.TeamManager.Players.Where(plr => plr.RoomInfo.State != PlayerState.Lobby))
                        plr.RoomInfo.State = PlayerState.Waiting;

                    Room.GameState = GameState.Result;
                    Room.Broadcast(new GameChangeStateAckMessage(Room.GameState));
                    Room.BroadcastBriefing(true);
                    break;
                case GameRuleState.Waiting:
                    GameTime = TimeSpan.Zero;
                    foreach (var team in Room.TeamManager.Values)
                        team.Score = 0;

                    foreach (var plr in Room.TeamManager.Players.Where(plr => plr.RoomInfo.State != PlayerState.Lobby))
                    {
                        plr.RoomInfo.Reset();
                        plr.RoomInfo.State = PlayerState.Lobby;
                    }
                    Room.SubGameState = GameTimeState.FirstHalf;
                    Room.GameState = GameState.Waiting;
                    Room.Broadcast(new GameChangeStateAckMessage(Room.GameState));
                    Room.BroadcastBriefing();
                    break;
            }
        }
    }
}
