using System;
using Netsphere.Game;
using Netsphere.Game.Systems;

namespace Netsphere
{
    internal class PlayerLocationInfo
    {
        public int channelid { get; set; } = 0;
        public bool invisible { get; set; } = false;

        public PlayerLocationInfo()
        {

        }
        public PlayerLocationInfo(int _id)
        {
            channelid = _id;
        }
    }
}
