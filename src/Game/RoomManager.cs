using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using BlubLib.Collections.Concurrent;
using BlubLib.Threading.Tasks;
using ExpressMapper.Extensions;
using Netsphere.Game;
using Netsphere.Network.Data.Game;
using Netsphere.Network.Message.Game;
using ProudNet;

// ReSharper disable once CheckNamespace
namespace Netsphere
{
    internal class RoomManager : IReadOnlyCollection<Room>
    {
        private readonly ConcurrentDictionary<uint, Room> _rooms = new ConcurrentDictionary<uint, Room>();
        private readonly AsyncLock _sync = new AsyncLock();

        public Channel Channel { get; }
        public GameRuleFactory GameRuleFactory { get; }

        #region Events


        #endregion

        public RoomManager(Channel channel)
        {
            Channel = channel;
            GameRuleFactory = new GameRuleFactory();
        }

        public void Update(TimeSpan delta)
        {
            foreach (var room in _rooms.Values)
                room.Update(delta);
        }

        public Room Get(uint id)
        {
            Room room;
            _rooms.TryGetValue(id, out room);
            return room;
        }

        public Room Create(RoomCreationOptions options, P2PGroup p2pGroup)
        {
            using (_sync.Lock())
            {
                uint id = 1;
                while (true)
                {
                    if (!_rooms.ContainsKey(id))
                        break;
                    id++;
                }

                var room = new Room(this, id, options, p2pGroup, options.Creator);
                _rooms.TryAdd(id, room);
                RoomDto roomDto = new RoomDto();
                roomDto.RoomId = (byte)room.Id;
                roomDto.PlayerCount = (byte)room.Players.Count;
                roomDto.PlayerLimit = room.Options.PlayerLimit;
                roomDto.State = (byte)room.GameRuleManager.GameRule.StateMachine.State;
                roomDto.State2 = (byte)room.GameRuleManager.GameRule.StateMachine.State;
                roomDto.GameRule = (int)room.Options.GameRule;
                roomDto.Map = (byte)room.Options.MapID;
                roomDto.WeaponLimit = room.Options.ItemLimit;
                roomDto.Name = room.Options.Name;
                roomDto.Password = room.Options.Password;
                roomDto.FMBURNMode = room.GetFMBurnModeInfo();
                Channel.Broadcast(new RoomDeployAck2Message(roomDto));
                
                return room;
            }
        }

        public void Remove(Room room)
        {
            if (room.Players.Count > 0)
                throw new RoomException("Players are still in this room");

            _rooms.Remove(room.Id);
            Channel.Broadcast(new RoomDisposeAckMessage(room.Id));
        }

        #region IReadOnlyCollection

        public int Count => _rooms.Count;

        public Room this[uint id] => Get(id);

        public IEnumerator<Room> GetEnumerator()
        {
            return _rooms.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}
