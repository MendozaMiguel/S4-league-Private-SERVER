using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BlubLib.Collections.Concurrent;
using BlubLib.Threading.Tasks;
using ExpressMapper.Extensions;
using Netsphere.Game.Systems;
using Netsphere.Network;
using Netsphere.Network.Data.Chat;
using Netsphere.Network.Data.Game;
using Netsphere.Network.Data.GameRule;
using Netsphere.Network.Message.Chat;
using Netsphere.Network.Message.Game;
using Netsphere.Network.Message.GameRule;
using ProudNet;
using Serilog;
using Serilog.Core;
using System.Net;

namespace Netsphere
{
    internal class Room
    {
        // ReSharper disable once InconsistentNaming
        private static readonly ILogger Logger = Log.ForContext(Constants.SourceContextPropertyName, nameof(Room));
        private readonly AsyncLock _slotIdSync = new AsyncLock();

        private readonly ConcurrentDictionary<ulong, Player> _players = new ConcurrentDictionary<ulong, Player>();
        private readonly ConcurrentDictionary<ulong, object> _kickedPlayers = new ConcurrentDictionary<ulong, object>();
        private readonly TimeSpan _hostUpdateTime = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _changingRulesTime = TimeSpan.FromSeconds(5);
        private const uint PingDifferenceForChange = 20;

        private TimeSpan _hostUpdateTimer;
        private TimeSpan _changingRulesTimer;

        public RoomManager RoomManager { get; }
        public uint Id { get; }
        public RoomCreationOptions Options { get; }
        public DateTime TimeCreated { get; }

        public TeamManager TeamManager { get; }
        public GameRuleManager GameRuleManager { get; }
        public GameState GameState { get; set; } = GameState.Waiting;
        public GameTimeState SubGameState { get; set; } = GameTimeState.FirstHalf;
        public TimeSpan RoundTime { get; set; } = TimeSpan.Zero;

        public IReadOnlyDictionary<ulong, Player> Players => _players;

        public Player Master { get; private set; }
        public Player Host { get; private set; }
        public Player Creator { get; private set; }

        public P2PGroup Group { get; }

        public bool IsChangingRules { get; private set; }

        #region Events

        public event EventHandler<RoomPlayerEventArgs> PlayerJoining;
        public event EventHandler<RoomPlayerEventArgs> PlayerJoined;
        public event EventHandler<RoomPlayerEventArgs> PlayerLeft;
        public event EventHandler StateChanged;

        internal virtual byte GetFMBurnModeInfo()
        {
            byte FMBurnMode = 0;
            if (this.Options.IsFriendly && this.Options.IsBurning)
            {
                FMBurnMode = 3;
            }
            else if (this.Options.IsBurning)
            {
                FMBurnMode = 2;
            }
            else if (this.Options.IsFriendly)
            {
                FMBurnMode = 1;
            }
            else if(!this.Options.IsFriendly && !this.Options.IsBurning)
            {
                FMBurnMode = 0;
            }
            return FMBurnMode;
        }

        internal virtual void OnPlayerJoining(RoomPlayerEventArgs e)
        {
            PlayerJoining?.Invoke(this, e);
            
            RoomDto roomDto = new RoomDto();
            roomDto.RoomId = (byte)this.Id;
            roomDto.PlayerCount = (byte)this.Players.Count;
            roomDto.PlayerLimit = this.Options.PlayerLimit;
            roomDto.State = (byte)this.GameRuleManager.GameRule.StateMachine.State;
            roomDto.State2 = (byte)this.GameRuleManager.GameRule.StateMachine.State;
            roomDto.GameRule = (int)this.Options.GameRule;
            roomDto.Map = (byte)this.Options.MapID;
            roomDto.WeaponLimit = this.Options.ItemLimit;
            roomDto.Name = this.Options.Name;
            roomDto.Password = this.Options.Password;
            roomDto.FMBURNMode = GetFMBurnModeInfo();

            RoomManager.Channel.Broadcast(new RoomChangeRoomInfoAck2Message(roomDto));
            //RoomManager.Channel.Broadcast(new RoomChangeRoomInfoAckMessage(this.Map<Room, RoomDto>()));
            //RoomManager.Channel.Broadcast(new SUserDataAckMessage(e.Player.Map<Player, UserDataDto>()));
        }

        internal virtual void OnPlayerJoined(RoomPlayerEventArgs e)
        {
            PlayerJoined?.Invoke(this, e);
            //RoomManager.Channel.Broadcast(new SUserDataAckMessage(e.Player.Map<Player, UserDataDto>()));
        }

        protected virtual void OnPlayerLeft(RoomPlayerEventArgs e)
        {
            PlayerLeft?.Invoke(this, e);

            RoomDto roomDto = new RoomDto();
            roomDto.RoomId = (byte)this.Id;
            roomDto.PlayerCount = (byte)this.Players.Count;
            roomDto.PlayerLimit = this.Options.PlayerLimit;
            roomDto.State = (byte)this.GameRuleManager.GameRule.StateMachine.State;
            roomDto.State2 = (byte)this.GameRuleManager.GameRule.StateMachine.State;
            roomDto.GameRule = (int)this.Options.GameRule;
            roomDto.Map = (byte)this.Options.MapID;
            roomDto.WeaponLimit = this.Options.ItemLimit;
            roomDto.Name = this.Options.Name;
            roomDto.Password = this.Options.Password;
            roomDto.FMBURNMode = GetFMBurnModeInfo();
            RoomManager.Channel.Broadcast(new RoomChangeRoomInfoAck2Message(roomDto));
            //RoomManager.Channel.Broadcast(new RoomChangeRoomInfoAckMessage(this.Map<Room, RoomDto>()));
            //RoomManager.Channel.Broadcast(new SUserDataAckMessage(e.Player.Map<Player, UserDataDto>()));
        }

        protected virtual void OnStateChanged()
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
            RoomDto roomDto = new RoomDto();
            roomDto.RoomId = (byte)this.Id;
            roomDto.PlayerCount = (byte)this.Players.Count;
            roomDto.PlayerLimit = this.Options.PlayerLimit;
            roomDto.State = (byte)this.GameRuleManager.GameRule.StateMachine.State;
            roomDto.State2 = (byte)this.GameRuleManager.GameRule.StateMachine.State;
            roomDto.GameRule = (int)this.Options.GameRule;
            roomDto.Map = (byte)this.Options.MapID;
            roomDto.WeaponLimit = this.Options.ItemLimit;
            roomDto.Name = this.Options.Name;
            roomDto.Password = this.Options.Password;
            roomDto.FMBURNMode = GetFMBurnModeInfo();

            RoomManager.Channel.Broadcast(new RoomChangeRoomInfoAck2Message(roomDto));
            //RoomManager.Channel.Broadcast(new RoomChangeRoomInfoAckMessage(this.Map<Room, RoomDto>()));
        }

        #endregion

        public Room(RoomManager roomManager, uint id, RoomCreationOptions options, P2PGroup group, Player creator)
        {
            RoomManager = roomManager;
            Id = id;
            Options = options;
            TimeCreated = DateTime.Now;
            TeamManager = new TeamManager(this);
            GameRuleManager = new GameRuleManager(this);
            Group = group;
            Creator = creator;
            TeamManager.TeamChanged += TeamManager_TeamChanged;

            GameRuleManager.GameRuleChanged += GameRuleManager_OnGameRuleChanged;
            GameRuleManager.MapInfo = GameServer.Instance.ResourceCache.GetMaps()[options.MapID];
            GameRuleManager.GameRule = RoomManager.GameRuleFactory.Get(Options.GameRule, this);
        }

        public void Update(TimeSpan delta)
        {
            if (Players.Count == 0)
                return;

            if (Host != null)
            {
                _hostUpdateTimer += delta;
                if (_hostUpdateTimer >= _hostUpdateTime)
                {
                    var lowest = GetPlayerWithLowestPing();
                    if (Host != lowest)
                    {
                        var diff = Math.Abs(Host.Session.UnreliablePing - lowest.Session.UnreliablePing);
                        if (diff >= PingDifferenceForChange)
                            ChangeHostIfNeeded(lowest, true);
                    }

                    _hostUpdateTimer = TimeSpan.Zero;
                }
            }

            if (IsChangingRules)
            {
                _changingRulesTimer += delta;
                if (_changingRulesTimer >= _changingRulesTime)
                {
                    GameRuleManager.MapInfo = GameServer.Instance.ResourceCache.GetMaps()[Options.MapID];
                    GameRuleManager.GameRule = RoomManager.GameRuleFactory.Get(Options.GameRule, this);
                    Broadcast(new RoomChangeRuleAckMessage(Options.Map<RoomCreationOptions, ChangeRuleDto>()));
                    IsChangingRules = false;
                }
            }

            GameRuleManager.Update(delta);
        }

        public void Join(Player plr)
        {
            if (plr.Room != null)
                throw new RoomException("Player is already inside a room");

            if (_players.Count >= Options.PlayerLimit)
                throw new RoomLimitReachedException();

            if (_kickedPlayers.ContainsKey(plr.Account.Id))
                throw new RoomAccessDeniedException();
            
            using (_slotIdSync.Lock())
            {
                byte id = 3;
                while (Players.Values.Any(p => p.RoomInfo.Slot == id))
                    id++;

                plr.RoomInfo.Slot = id;
            }
            
            if(plr.Channel != null)
            {
                plr.LocationInfo = new PlayerLocationInfo(plr.Channel.Id);
                plr.LocationInfo.invisible = true;
                plr.Channel.Broadcast(new ChannelLeavePlayerAckMessage(plr.Account.Id));
            }

            plr.RoomInfo.Reset();
            plr.RoomInfo.State = PlayerState.Lobby;
            plr.RoomInfo.Mode = PlayerGameMode.Normal;
            plr.RoomInfo.Stats = GameRuleManager.GameRule.GetPlayerRecord(plr);
            TeamManager.Join(plr);
            
            _players.TryAdd(plr.Account.Id, plr);
            plr.Room = this;
            plr.RoomInfo.IsConnecting = true;
            
            plr.Session.SendAsync(new RoomEnterRoomInfoAck2Message()
            {
                RoomID = this.Id,
                GameRule = this.Options.GameRule,
                MapID = (byte)this.Options.MapID,
                PlayerLimit = this.Options.PlayerLimit,
                GameTimeState = this.SubGameState,
                GameState = this.GameState,
                TimeLimit = (uint)this.Options.TimeLimit.TotalMilliseconds,
                mUnknow01 = 0,
                Time_Sync = (uint)this.GameRuleManager.GameRule.RoundTime.TotalMilliseconds,
                Score_Limit = this.Options.ScoreLimit,
                mUnknow02 = 0,
                IP = new IPEndPoint(IPAddress.Parse(Config.Instance.IP), Config.Instance.RelayListener.Port),
                Spectator = (byte)this.Options.Spectator,
                mUnknow03 = 0,
                FMBURNMode = GetFMBurnModeInfo(),
                mUnknow04 = (ulong)Creator.Account.Id,
            });

            plr.Session.SendAsync(new ItemClearEsperChipAckMessage() { Unk = new ClearEsperChipDto[] { } });
            plr.Session.SendAsync(new ItemClearInvalidEquipItemAckMessage() { Items = new InvalidateItemInfoDto[] { } });
            plr.Session.SendAsync(new RoomCurrentCharacterSlotAckMessage(0, plr.RoomInfo.Slot));
            Broadcast(new RoomPlayerInfoListForEnterPlayerAckMessage(_players.Values.Select(p => p.Map<Player, RoomPlayerDto>()).ToArray()));
            OnPlayerJoining(new RoomPlayerEventArgs(plr));
        }

        public void Leave(Player plr, RoomLeaveReason roomLeaveReason = RoomLeaveReason.Left)
        {
            if (plr.Room != this)
                return;
            try
            {
                if (plr.RelaySession?.HostId != null)
                    Group?.Leave(plr.RelaySession.HostId);
                Broadcast(new RoomLeavePlayerAckMessage(plr.Account.Id, plr.Account.Nickname, roomLeaveReason));

                if (roomLeaveReason == RoomLeaveReason.Kicked ||
                    roomLeaveReason == RoomLeaveReason.ModeratorKick ||
                    roomLeaveReason == RoomLeaveReason.VoteKick)
                    _kickedPlayers.TryAdd(plr.Account.Id, null);

                plr.LocationInfo.invisible = false;
                uint curchannelid = (uint)plr.Channel.Id;
                plr.Channel.Leave(plr, true);
                GameServer.Instance.ChannelManager[curchannelid].Join(plr);

                plr.RoomInfo.PeerId = 0;
                plr.RoomInfo.Team.Leave(plr);
                _players.Remove(plr.Account.Id);
                plr.Room = null;
                plr.Session.SendAsync(new Network.Message.Game.RoomLeavePlayerInfoAckMessage(plr.Account.Id));
                
                OnPlayerLeft(new RoomPlayerEventArgs(plr));

                if (_players.Count > 0)
                {
                    if (Master == plr)
                        ChangeMasterIfNeeded(GetPlayerWithLowestPing(), true);

                    if (Host == plr)
                        ChangeHostIfNeeded(GetPlayerWithLowestPing(), true);
                }
                else
                {
                    RoomManager.Remove(this);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.StackTrace);
                plr.Session.SendAsync(new Network.Message.Game.RoomLeavePlayerInfoAckMessage(plr.Account.Id));

                if (_players.Count == 1)
                    plr.Channel.RoomManager.Remove(this);
                _players.Remove(plr.Account.Id);
                return;
            }
        }

        public uint GetLatency()
        {
            // ToDo add this to config
            var good = 30;
            var bad = 190;

            var players = TeamManager.SelectMany(t => t.Value.Values).ToArray();
            var total = players.Sum(plr => plr.Session.UnreliablePing) / players.Length;

            if (total <= good)
                return 100;
            if (total >= bad)
                return 0;

            var result = (uint)(100f * total / bad);
            return 100 - result;
        }

        public void SetCreator(Player plr)
        {
            Master = plr;
            Host = plr;
            return;
        }

        public void ChangeMasterIfNeeded(Player plr, bool force = false)
        {
            
            if ((plr.Room != this || plr.Room.Players.Count == 1 || plr.Room.Master == null) || force && Master != plr)
            {
                Master = plr;
            }
            Broadcast(new RoomChangeMasterAckMessage(Master.Account.Id));
            return;
        }

        public void ChangeHostIfNeeded(Player plr, bool force = false)
        {
            if ((plr.Room != this || Host == null || plr.Room.Players.Count == 1) || force && Host != plr)
            {
                // TODO Add Room extension?
                Logger.Debug("<Room {roomId}> Changing host to {nickname} - Ping:{ping} ms", Id, plr.Account.Nickname, plr.Session.UnreliablePing);
                Host = plr;
            }
            Broadcast(new RoomChangeRefereeAckMessage(Host.Account.Id));
            return;
        }

        public void ChangeRules(ChangeRuleDto options)
        {
            if (IsChangingRules)
                return;

            //if (!RoomManager.GameRuleFactory.Contains(options.MatchKey.GameRule))
            //{
            //    Logger.ForAccount(Master)
            //        .Error("Game rule {gameRule} does not exist", options.MatchKey.GameRule);
            //    Master.Session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
            //    return;
            //}

            //var map = GameServer.Instance.ResourceCache.GetMaps().GetValueOrDefault(options.MatchKey.Map);
            //if (map == null)
            //{
            //    Logger.ForAccount(Master)
            //        .Error("Map {map} does not exist", options.MatchKey.Map);
            //    Master.Session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
            //    return;
            //}

            //if (!map.GameRules.Contains(options.MatchKey.GameRule))
            //{
            //    Logger.ForAccount(Master)
            //        .Error("Map {mapId}({mapName}) is not available for game rule {gameRule}",
            //            map.Id, map.Name, options.MatchKey.GameRule);
            //    Master.Session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
            //    return;
            //}

            // ToDo check if current player count is not above the new player limit

            //_changingRulesTimer = TimeSpan.Zero;
            //IsChangingRules = true;
            //Options.Name = options.Name;
            //Options.MatchKey = options.MatchKey;
            //Options.TimeLimit = options.TimeLimit;
            //Options.ScoreLimit = options.ScoreLimit;
            //Options.Password = options.Password;
            //Options.IsFriendly = options.IsFriendly;
            //Options.IsBalanced = options.IsBalanced;
            //Options.ItemLimit = options.ItemLimit;
            //Options.IsNoIntrusion = options.IsNoIntrusion;

            //Broadcast(new RoomChangeRuleNotifyAckMessage(Options.Map<RoomCreationOptions, ChangeRuleDto>()));
        }

        private Player GetPlayerWithLowestPing()
        {
            return _players.Values.Aggregate((lowestPlayer, player) => (lowestPlayer == null || player.Session.UnreliablePing < lowestPlayer.Session.UnreliablePing ? player : lowestPlayer));
        }

        private void TeamManager_TeamChanged(object sender, TeamChangedEventArgs e)
        {
            //RoomManager.Channel.Broadcast(new SUserDataAckMessage(e.Player.Map<Player, UserDataDto>()));
        }

        private void GameRuleManager_OnGameRuleChanged(object sender, EventArgs e)
        {
            GameRuleManager.GameRule.StateMachine.OnTransitioned(t => OnStateChanged());

            foreach (var plr in Players.Values)
            {
                plr.RoomInfo.Stats = GameRuleManager.GameRule.GetPlayerRecord(plr);
                TeamManager.Join(plr);
            }
            BroadcastBriefing();
        }

        #region Broadcast

        public void Broadcast(IGameMessage message)
        {
            foreach (var plr in _players.Values)
                plr.Session.SendAsync(message);
        }

        public void Broadcast(IGameRuleMessage message)
        {
            foreach (var plr in _players.Values)
                plr.Session.SendAsync(message);
        }

        public void Broadcast(IChatMessage message)
        {
            foreach (var plr in _players.Values)
                plr.Session.SendAsync(message);
        }

        public void BroadcastBriefing(bool isResult = false)
        {
            var gameRule = GameRuleManager.GameRule;
            //var isResult = gameRule.StateMachine.IsInState(GameRuleState.Result);
            Broadcast(new GameBriefingInfoAckMessage(isResult, false, gameRule.Briefing.ToArray(isResult)));
        }
        
        #endregion
    }
}
