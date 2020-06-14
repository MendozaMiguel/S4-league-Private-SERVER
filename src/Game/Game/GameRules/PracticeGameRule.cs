using System;
using System.IO;
using System.Linq;
using Netsphere.Network.Message.GameRule;
using Netsphere.Network.Data.GameRule;

// ReSharper disable once CheckNamespace
namespace Netsphere.Game.GameRules
{
    internal class PracticeGameRule : GameRuleBase
    {
        private const uint PlayersNeededToStart = 1;
        
        public override GameRule GameRule => GameRule.Practice;
        public override Briefing Briefing { get; }
        

        public PracticeGameRule(Room room)
            : base(room)
        {
            Briefing = new Briefing(this);

            StateMachine.Configure(GameRuleState.Waiting)
                .PermitIf(GameRuleStateTrigger.StartPrepare, GameRuleState.Prepare, CanPrepareGame);

            StateMachine.Configure(GameRuleState.Prepare)
                .PermitIf(GameRuleStateTrigger.StartGame, GameRuleState.FirstHalf, CanStartGame);

            StateMachine.Configure(GameRuleState.FirstHalf)
                .SubstateOf(GameRuleState.Playing)
                .Permit(GameRuleStateTrigger.StartHalfTime, GameRuleState.EnteringHalfTime)
                .Permit(GameRuleStateTrigger.StartResult, GameRuleState.EnteringResult);

            StateMachine.Configure(GameRuleState.EnteringHalfTime)
                .SubstateOf(GameRuleState.Playing)
                .Permit(GameRuleStateTrigger.StartHalfTime, GameRuleState.HalfTime)
                .Permit(GameRuleStateTrigger.StartResult, GameRuleState.EnteringResult);

            StateMachine.Configure(GameRuleState.HalfTime)
                .SubstateOf(GameRuleState.Playing)
                .Permit(GameRuleStateTrigger.StartSecondHalf, GameRuleState.SecondHalf)
                .Permit(GameRuleStateTrigger.StartResult, GameRuleState.EnteringResult);

            StateMachine.Configure(GameRuleState.SecondHalf)
                .SubstateOf(GameRuleState.Playing)
                .Permit(GameRuleStateTrigger.StartResult, GameRuleState.EnteringResult);

            StateMachine.Configure(GameRuleState.EnteringResult)
                .SubstateOf(GameRuleState.Playing)
                .Permit(GameRuleStateTrigger.StartResult, GameRuleState.Result);

            StateMachine.Configure(GameRuleState.Result)
                .SubstateOf(GameRuleState.Playing)
                .Permit(GameRuleStateTrigger.EndGame, GameRuleState.Waiting);
        }

        public override void Initialize()
        {
            Room.TeamManager.Add(Team.Alpha, (uint)(Room.Options.PlayerLimit), (uint)0);

            base.Initialize();
        }

        public override void Cleanup()
        {
            Room.TeamManager.Remove(Team.Alpha);

            base.Cleanup();
        }

        public override void Update(TimeSpan delta)
        {
            base.Update(delta);

            var teamMgr = Room.TeamManager;

            if (StateMachine.IsInState(GameRuleState.Playing) &&
                !StateMachine.IsInState(GameRuleState.EnteringResult) &&
                !StateMachine.IsInState(GameRuleState.Result))
            {
                if (StateMachine.IsInState(GameRuleState.FirstHalf))
                {
                    // Still have enough players?
                    if (teamMgr.PlayersPlaying.Count() < PlayersNeededToStart)
                        StateMachine.Fire(GameRuleStateTrigger.StartResult);

                    // Did we reach ScoreLimit?
                    if (teamMgr.PlayersPlaying.Any(plr => plr.RoomInfo.Stats.TotalScore >= Room.Options.ScoreLimit))
                        StateMachine.Fire(GameRuleStateTrigger.StartResult);

                    // Did we reach round limit?
                    var roundTimeLimit = TimeSpan.FromMilliseconds(Room.Options.TimeLimit.TotalMilliseconds);
                    if (RoundTime >= roundTimeLimit)
                        StateMachine.Fire(GameRuleStateTrigger.StartResult);
                }
            }
        }

        public override PlayerRecord GetPlayerRecord(Player plr)
        {
            return new PracticePlayerRecord(plr);
        }

        public override void OnScoreKill(Player killer, Player assist, Player target, AttackAttribute attackAttribute, LongPeerId ScoreTarget = null, LongPeerId ScoreKiller = null, LongPeerId ScoreAssist = null)
        {
            Respawn(Room.Creator);
            if (ScoreAssist != null)
            {

                Room.Broadcast(
                    new ScoreKillAssistAckMessage(new ScoreAssistDto(ScoreKiller, ScoreAssist,
                        ScoreTarget, attackAttribute)));
            }
            else
            {
                Room.Broadcast(
                    new ScoreKillAckMessage(new ScoreDto(ScoreKiller, ScoreTarget,
                        attackAttribute)));
            }
            return;
        }
        
        private bool CanPrepareGame()
        {
            if (!StateMachine.IsInState(GameRuleState.Waiting))
                return false;
            
            return true;
        }
        private bool CanStartGame()
        {
            return true;
        }

        private static PracticePlayerRecord GetRecord(Player plr)
        {
            return (PracticePlayerRecord)plr.RoomInfo.Stats;
        }
    }

    internal class PracticePlayerRecord : PlayerRecord
    {
        public override uint TotalScore => GetTotalScore();

        public uint BonusKills { get; set; }
        public uint BonusKillAssists { get; set; }
        public int Unk5 { get; set; }
        public int Unk6 { get; set; }
        public int Unk7 { get; set; } // Increases kill score
        public int Unk8 { get; set; } // increases kill assist score
        public int Unk9 { get; set; }
        public int Unk10 { get; set; }
        public int Unk11 { get; set; }

        public PracticePlayerRecord(Player plr)
            : base(plr)
        { }

        public override void Serialize(BinaryWriter w, bool isResult)
        {
            base.Serialize(w, isResult);

            w.Write(Kills);
            w.Write(Kills);
            w.Write(Kills);
            w.Write(Kills);
            w.Write(Kills);
            w.Write(Kills);
            w.Write(Kills);
            w.Write(Kills);
            w.Write(Kills);
            w.Write(Kills);
            w.Write(Kills);
        }

        public override void Reset()
        {
            base.Reset();

            KillAssists = 0;
            BonusKills = 0;
            BonusKillAssists = 0;
            Unk5 = 0;
            Unk6 = 0;
            Unk7 = 0;
            Unk8 = 0;
            Unk9 = 0;
            Unk10 = 0;
            Unk11 = 0;
        }

        private uint GetTotalScore()
        {
            return Kills * 2 +
                KillAssists +
                BonusKills * 5 +
                BonusKillAssists;
        }
    }
}
