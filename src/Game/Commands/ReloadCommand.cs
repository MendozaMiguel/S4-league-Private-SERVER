using System;
using System.Collections.Generic;
using System.Text;
using Netsphere.Network;
using Netsphere.Resource;
using ProudNet;
using Netsphere.Network.Message.Game;
using System.IO;
using BlubLib.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Netsphere.Commands
{
    internal class ReloadCommand : ICommand
    {
        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }

        public ReloadCommand()
        {
            Name = "reload";
            AllowConsole = true;
            Permission = SecurityLevel.Developer;
            SubCommands = new ICommand[] {new ShopCommand(), new ReqBoxCommand(), new RoomMassKickCommand()};
        }

        public bool Execute(GameServer server, Player plr, string[] args)
        {
            return true;
        }

        public string Help()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Name);
            foreach (var cmd in SubCommands)
            {
                sb.Append("\t");
                sb.AppendLine(cmd.Help());
            }
            return sb.ToString();
        }

        private class ShopCommand : ICommand
        {
            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }

            public ShopCommand()
            {
                Name = "shop";
                AllowConsole = true;
                Permission = SecurityLevel.Developer;
                SubCommands = new ICommand[] {};
            }

            public bool Execute(GameServer server, Player plr, string[] args)
            {
                Task.Factory.StartNew(() =>
                {
                    var message = "Reloading shop, server may lag for a short period of time...";

                    if (plr == null)
                        Console.WriteLine(message);
                    else
                        plr.SendConsoleMessage(S4Color.Green + message);

                    server.BroadcastNotice(message);

                    server.ResourceCache.Clear(ResourceCacheType.Shop);
                    var shop = server.ResourceCache.GetShop();
                    var version = shop.Version;
                    server.Broadcast(new NewShopUpdateCheckAckMessage
                    {
                        Date01 = version,
                        Date02 = version,
                        Date03 = version,
                        Date04 = version,
                        Unk = 0
                    });
                    #region NewShopPrice

                    using (var w = new BinaryWriter(new MemoryStream()))
                    {
                        w.Serialize(shop.Prices.Values.ToArray());

                        server.Broadcast(new NewShopUpdataInfoAckMessage
                        {
                            Type = ShopResourceType.NewShopPrice,
                            Data = w.ToArray(),
                            Date = version
                        });
                    }

                    #endregion

                    #region NewShopEffect

                    using (var w = new BinaryWriter(new MemoryStream()))
                    {
                        w.Serialize(shop.Effects.Values.ToArray());

                        server.Broadcast(new NewShopUpdataInfoAckMessage
                        {
                            Type = ShopResourceType.NewShopEffect,
                            Data = w.ToArray(),
                            Date = version
                        });
                    }

                    #endregion

                    #region NewShopItem

                    using (var w = new BinaryWriter(new MemoryStream()))
                    {
                        w.Serialize(shop.Items.Values.ToArray());

                        server.Broadcast(new NewShopUpdataInfoAckMessage
                        {
                            Type = ShopResourceType.NewShopItem,
                            Data = w.ToArray(),
                            Date = version
                        });
                    }

                    #endregion

                    // ToDo
                    using (var w = new BinaryWriter(new MemoryStream()))
                    {
                        w.Write(0);

                        server.Broadcast(new NewShopUpdataInfoAckMessage
                        {
                            Type = ShopResourceType.NewShopUniqueItem,
                            Data = w.ToArray(),
                            Date = version
                        });
                    }

                    server.Broadcast(new NewShopUpdateEndAckMessage());
                    message = "Shop reload completed";
                    server.BroadcastNotice(message);
                    if (plr == null)
                        Console.WriteLine(message);
                    else
                        plr.SendConsoleMessage(S4Color.Green + message);
                });
                return true;
            }

            public string Help()
            {
                return Name;
            }
        }

        private class ReqBoxCommand : ICommand
        {
            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }

            public ReqBoxCommand()
            {
                Name = "reqs";
                AllowConsole = true;
                Permission = SecurityLevel.Developer;
                SubCommands = new ICommand[] { };
            }

            public bool Execute(GameServer server, Player plr, string[] args)
            {
                var message = "Trying to fix stuck request boxes..";

                if (plr == null)
                    Console.WriteLine(message);
                else
                    plr.SendConsoleMessage(S4Color.Green + message);

                GameServer.Instance.Broadcast(new Network.Message.Game.ServerResultAckMessage(ServerResult.ServerError));

                message = "Done trying to fix stuck request boxes.";
                //server.BroadcastNotice(message);
                if (plr == null)
                    Console.WriteLine(message);
                else
                    plr.SendConsoleMessage(S4Color.Green + message);

                return true;
            }

            public string Help()
            {
                return Name;
            }
        }

        private class RoomMassKickCommand : ICommand
        {
            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }

            public RoomMassKickCommand()
            {
                Name = "rooms";
                AllowConsole = true;
                Permission = SecurityLevel.Developer;
                SubCommands = new ICommand[] { };
            }

            public bool Execute(GameServer server, Player plr, string[] args)
            {
                var message = "Kicking all players..";

                if (plr == null)
                    Console.WriteLine(message);
                else
                    plr.SendConsoleMessage(S4Color.Green + message);
                
                foreach(ProudSession sess in GameServer.Instance.Sessions.Values)
                {
                    GameSession session = (GameSession)sess;
                    if(session.Player != null && session.Player.Room != null)
                    {
                        session.Player.Room.Leave(session.Player);
                    }
                }

                message = "Done kicking all players from all rooms.";
                //server.BroadcastNotice(message);
                if (plr == null)
                    Console.WriteLine(message);
                else
                    plr.SendConsoleMessage(S4Color.Green + message);

                return true;
            }

            public string Help()
            {
                return Name;
            }
        }
    }
}
