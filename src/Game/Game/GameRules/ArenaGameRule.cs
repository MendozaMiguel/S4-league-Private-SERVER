﻿using System;
using System.IO;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace Netsphere.Game.GameRules
{
    internal class ArenaGameRule : GameRuleBase
    {
        public override GameRule GameRule => GameRule.Arena;
        public override Briefing Briefing { get; }

        public ArenaGameRule(Room room)
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
            var teamMgr = Room.TeamManager;
            teamMgr.Add(Team.Alpha, (uint)(Room.Options.PlayerLimit / 2), (uint)(Room.Options.Spectator / 2));
            teamMgr.Add(Team.Beta, (uint)(Room.Options.PlayerLimit / 2), (uint)(Room.Options.Spectator / 2));

            base.Initialize();
        }

        public override void Cleanup()
        {
            var teamMgr = Room.TeamManager;
            teamMgr.Remove(Team.Alpha);
            teamMgr.Remove(Team.Beta);

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
                // Still have enough players?
                var min = teamMgr.Values.Min(team =>
                team.Values.Count(plr =>
                    plr.RoomInfo.State != PlayerState.Lobby &&
                    plr.RoomInfo.State != PlayerState.Spectating));
                if (min == 0 && !Room.Options.IsFriendly)
                    StateMachine.Fire(GameRuleStateTrigger.StartResult);

                var isFirstHalf = StateMachine.IsInState(GameRuleState.FirstHalf);
                var isSecondHalf = StateMachine.IsInState(GameRuleState.SecondHalf);
                if (isFirstHalf || isSecondHalf)
                {
                    var scoreLimit = isFirstHalf ? Room.Options.ScoreLimit / 2 : Room.Options.ScoreLimit;
                    var trigger = isFirstHalf ? GameRuleStateTrigger.StartHalfTime : GameRuleStateTrigger.StartResult;

                    // Did we reach ScoreLimit?
                    if (teamMgr.Values.Any(team => team.Score >= scoreLimit))
                        StateMachine.Fire(trigger);

                    // Did we reach round limit?
                    var roundTimeLimit = TimeSpan.FromMilliseconds(Room.Options.TimeLimit.TotalMilliseconds / 2);
                    if (RoundTime >= roundTimeLimit)
                        StateMachine.Fire(trigger);
                }
            }
        }

        public override PlayerRecord GetPlayerRecord(Player plr)
        {
            return new ArenaPlayerRecord(plr);
        }

        public override void OnScoreKill(Player killer, Player assist, Player target, AttackAttribute attackAttribute, LongPeerId ScoreTarget = null, LongPeerId ScoreKiller = null, LongPeerId ScoreAssist = null)
        {
            killer.RoomInfo.Team.Score += 1;
            base.OnScoreKill(killer, assist, target, attackAttribute);
        }

        public override void OnScoreHeal(Player plr, LongPeerId Target = null)
        {
            GetRecord(plr).HealAssists++;
            base.OnScoreHeal(plr);
        }

        private bool CanPrepareGame()
        {
            if (!StateMachine.IsInState(GameRuleState.Waiting))
                return false;

            var teams = Room.TeamManager.Values.ToArray();
            if (Room.Options.IsFriendly)
                return true;
            if (teams.Any(team => team.Count == 0)) // Do we have enough players?
                return false;

            // Is atleast one player per team ready?
            return teams.All(team => team.Players.Any(plr => plr.RoomInfo.IsReady || Room.Master == plr));
        }

        private bool CanStartGame()
        {
            return true;
        }

        private static ArenaPlayerRecord GetRecord(Player plr)
        {
            return (ArenaPlayerRecord)plr.RoomInfo.Stats;
        }
    }

    internal class ArenaPlayerRecord : PlayerRecord
    {
        public override uint TotalScore => GetTotalScore();

        public int HealAssists { get; set; }
        public int Unk { get; set; }
        public int Deaths2 { get; set; }
        public int Deaths3 { get; set; }

        public ArenaPlayerRecord(Player plr)
            : base(plr)
        { }

        public override void Serialize(BinaryWriter w, bool isResult)
        {
            base.Serialize(w, isResult);

            w.Write(Kills);
            w.Write(KillAssists);
            w.Write(HealAssists);
            w.Write(Deaths);
            w.Write(Unk);
            w.Write(Deaths2);
            w.Write(Deaths3);
        }

        public override void Reset()
        {
            base.Reset();

            HealAssists = 0;
            Unk = 0;
            Deaths2 = 0;
            Deaths3 = 0;
        }

        private uint GetTotalScore()
        {
            return (uint)(Kills * 2 + KillAssists + HealAssists * 2);
        }

        public override uint GetExpGain(out uint bonusExp)
        {
            base.GetExpGain(out bonusExp);

            var config = Config.Instance.Game.ArenaExpRates;
            var place = 1;

            var plrs = Player.Room.TeamManager.Players
                .Where(plr => plr.RoomInfo.State == PlayerState.Waiting &&
                    plr.RoomInfo.Mode == PlayerGameMode.Normal)
                .ToArray();

            foreach (var plr in plrs.OrderByDescending(plr => plr.RoomInfo.Stats.TotalScore))
            {
                if (plr == Player)
                    break;

                place++;
                if (place > 3)
                    break;
            }

            var rankingBonus = 0f;
            switch (place)
            {
                case 1:
                    rankingBonus = config.FirstPlaceBonus;
                    break;

                case 2:
                    rankingBonus = config.SecondPlaceBonus;
                    break;

                case 3:
                    rankingBonus = config.ThirdPlaceBonus;
                    break;
            }

            return (uint)(TotalScore * config.ScoreFactor +
                rankingBonus +
                plrs.Length * config.PlayerCountFactor +
                Player.RoomInfo.PlayTime.TotalMinutes * config.ExpPerMin);
        }
    }
}
