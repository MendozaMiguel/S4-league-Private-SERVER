using BlubLib.Serialization;
using BlubLib.Serialization.Serializers;
using ProudNet.Serialization.Serializers;

namespace Netsphere.Network.Data.Game
{
    [BlubContract]
    public class RoomDto
    {
        [BlubMember(0)]
        public byte RoomId { get; set; }

        [BlubMember(1)]
        public byte State { get; set; }

        [BlubMember(2)]
        public int GameRule { get; set; }

        [BlubMember(3)]
        public byte Map { get; set; }

        [BlubMember(4)]
        public byte PlayerCount { get; set; }

        [BlubMember(5)]
        public byte PlayerLimit { get; set; }

        [BlubMember(6)]
        public int WeaponLimit { get; set; }

        [BlubMember(7, typeof(StringSerializer))]
        public string Password { get; set; }

        [BlubMember(8, typeof(StringSerializer))]
        public string Name { get; set; }

        [BlubMember(9)]
        public int Spectator { get; set; }

        [BlubMember(10)]
        public short State2 { get; set; }

        [BlubMember(11)]
        public short FMBURNMode { get; set; }

        [BlubMember(12)]
        public long mUnknow02 { get; set; }

        [BlubMember(13)]
        public short mUnknow03 { get; set; }



        //[BlubMember(12)]
        //public long Unk2 { get; set; } = 0;
        //
        //[BlubMember(13)]
        //public short Unk3 { get; set; } = 0;

        public RoomDto()
        {
            Name = "";
            Password = "";
        }
    }
}
