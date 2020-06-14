﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BlubLib.DotNetty.Handlers.MessageHandling;
using BlubLib.Security.Cryptography;
using Dapper.FastCrud;
using ExpressMapper.Extensions;
using Netsphere.Database.Auth;
using Netsphere.Database.Game;
using Netsphere.Network.Data.Chat;
using Netsphere.Network.Data.Game;
using Netsphere.Network.Message.Game;
using ProudNet.Handlers;
using Netsphere.Resource;
using Serilog;
using Serilog.Core;

namespace Netsphere.Network.Services
{
    internal class AuthService : ProudMessageHandler
    {
        private static readonly Version s_version = new Version(0, 8, 32, 63353);
        // ReSharper disable once InconsistentNaming
        private static readonly ILogger Logger = Log.ForContext(Constants.SourceContextPropertyName, nameof(AuthService));

        [MessageHandler(typeof(LoginRequestReqMessage))]
        public async Task LoginHandler(GameSession session, LoginRequestReqMessage message)
        {
            Logger.ForAccount(message.AccountId, message.Username)
                .Information("Login from {remoteEndPoint}", session.RemoteEndPoint);

            //if (message.Version != s_version)
            //{
            //    Logger.ForAccount(message.AccountId, message.Username)
            //        .Error("Invalid client version {version}", message.Version);
            //
            //    session.SendAsync(new LoginReguestAckMessage(GameLoginResult.WrongVersion));
            //    return;
            //}

            if (GameServer.Instance.PlayerManager.Count >= Config.Instance.PlayerLimit)
            {
                Logger.ForAccount(message.AccountId, message.Username)
                    .Error("Server is full");

                session.SendAsync(new LoginReguestAckMessage(GameLoginResult.ServerFull));
                return;
            }

            #region Validate Login

            AccountDto accountDto;
            using (var db = AuthDatabase.Open())
            {
                accountDto = (await db.FindAsync<AccountDto>(statement => statement
                            .Include<BanDto>(join => join.LeftOuterJoin())
                            .Where($"{nameof(AccountDto.Id):C} = @Id")
                            .WithParameters(new { Id = message.AccountId })))
                    .FirstOrDefault();
            }

            if (accountDto == null)
            {
                Logger.ForAccount(message.AccountId, message.Username)
                    .Error("Wrong login");

                session.SendAsync(new LoginReguestAckMessage(GameLoginResult.SessionTimeout));
                return;
            }

            uint inputSessionId;
            if (!uint.TryParse(message.SessionId, out inputSessionId))
            {
                Logger.ForAccount(message.AccountId, message.Username)
                    .Error("Wrong login");

                session.SendAsync(new LoginReguestAckMessage(GameLoginResult.SessionTimeout));
                return;
            }

            var sessionId = Hash.GetUInt32<CRC32>($"<{accountDto.Username}+{accountDto.Password}>");
            if (sessionId != inputSessionId)
            {
                Logger.ForAccount(message.AccountId, message.Username)
                    .Error("Wrong sessionid(1)");

                session.SendAsync(new LoginReguestAckMessage(GameLoginResult.SessionTimeout));
                return;
            }
            string datetime = $"{DateTimeOffset.Now.DateTime}";
            var authsessionId = Hash.GetString<CRC32>($"<{accountDto.Username}+{sessionId}+{message.Datetime}>");
            if (authsessionId != message.AuthToken)
            {
                Logger.ForAccount(message.AccountId, message.Username)
                    .Error("Wrong sessionid(2)");
            
                session.SendAsync(new LoginReguestAckMessage(GameLoginResult.SessionTimeout));
                return;
            }
            
            var newsessionId = Hash.GetString<CRC32>($"<{authsessionId}+{sessionId}>");
            if (newsessionId != message.newToken)
            {
                Logger.ForAccount(message.AccountId, message.Username)
                    .Error("Wrong sessionid(3)");
            
                session.SendAsync(new LoginReguestAckMessage(GameLoginResult.SessionTimeout));
                return;
            }


            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            var ban = accountDto.Bans.FirstOrDefault(b => b.Date + (b.Duration ?? 0) > now);
            if (ban != null)
            {
                var unbanDate = DateTimeOffset.FromUnixTimeSeconds(ban.Date + (ban.Duration ?? 0));
                Logger.ForAccount(message.AccountId, message.Username)
                    .Error("Banned until {unbanDate}", unbanDate);

                session.SendAsync(new LoginReguestAckMessage(GameLoginResult.SessionTimeout));
                return;
            }

            var account = new Account(accountDto);

            #endregion

            if (account.SecurityLevel < Config.Instance.SecurityLevel)
            {
                Logger.ForAccount(account)
                    .Error("No permission to enter this server({securityLevel} or above required)", Config.Instance.SecurityLevel);

                session.SendAsync(new LoginReguestAckMessage((GameLoginResult)9));
                return;
            }

            if (message.KickConnection)
            {
                Logger.ForAccount(account)
                    .Information("Kicking old connection");

                var oldPlr = GameServer.Instance.PlayerManager.Get(account.Id);
                oldPlr?.Disconnect();
            }

            if (GameServer.Instance.PlayerManager.Contains(account.Id))
            {
                Logger.ForAccount(account)
                    .Error("Already online");

                session.SendAsync(new LoginReguestAckMessage(GameLoginResult.TerminateOtherConnection));
                return;
            }

            using (var db = GameDatabase.Open())
            {
                var plrDto = (await db.FindAsync<PlayerDto>(statement => statement
                        .Include<PlayerCharacterDto>(join => join.LeftOuterJoin())
                        .Include<PlayerDenyDto>(join => join.LeftOuterJoin())
                        .Include<PlayerItemDto>(join => join.LeftOuterJoin())
                        .Include<PlayerMailDto>(join => join.LeftOuterJoin())
                        .Include<PlayerSettingDto>(join => join.LeftOuterJoin())
                        .Where($"{nameof(PlayerDto.Id):C} = @Id")
                        .WithParameters(new { Id = message.AccountId })))
                .FirstOrDefault();

                if (plrDto == null)
                {
                    // first time connecting to this server
                    var expTable = GameServer.Instance.ResourceCache.GetExperience();
                    Experience expValue;
                    if (!expTable.TryGetValue(Config.Instance.Game.StartLevel, out expValue))
                    {
                        expValue = new Experience();
                        expValue.TotalExperience = 0;
                        Logger.Warning("Given start level is not found in the experience table");
                    }

                    plrDto = new PlayerDto
                    {
                        Id = (int)account.Id,
                        Level = Config.Instance.Game.StartLevel,
                        PEN = Config.Instance.Game.StartPEN,
                        AP = Config.Instance.Game.StartAP,
                        Coins1 = Config.Instance.Game.StartCoins1,
                        Coins2 = Config.Instance.Game.StartCoins2,
                        TotalExperience = (int)expValue.TotalExperience
                    };

                    await db.InsertAsync(plrDto);
                }

                session.Player = new Player(session, account, plrDto);
            }

            if (GameServer.Instance.PlayerManager.Contains(session.Player))
            {
                session.Player = null;
                Logger.ForAccount(account)
                    .Error("Already online");

                session.SendAsync(new LoginReguestAckMessage(GameLoginResult.TerminateOtherConnection));
                return;
            }

            GameServer.Instance.PlayerManager.Add(session.Player);

            Logger.ForAccount(account)
                .Information("Login success");

            var result = string.IsNullOrWhiteSpace(account.Nickname)
                ? GameLoginResult.ChooseNickname
                : GameLoginResult.OK;
            if (result == GameLoginResult.ChooseNickname)
            {
                session.Player.Account.Nickname = session.Player.Account.Username;
                session.Player.CharacterManager.CreateFirst(0, 0, 0, 0, 0, 0);
                using (var db = AuthDatabase.Open())
                {
                    var mapping = OrmConfiguration
                        .GetDefaultEntityMapping<AccountDto>()
                        .Clone()
                        .UpdatePropertiesExcluding(prop => prop.IsExcludedFromUpdates = true, nameof(AccountDto.Nickname));

                    await db.UpdateAsync(new AccountDto { Id = (int)session.Player.Account.Id, Nickname = session.Player.Account.Username },
                                statement => statement.WithEntityMappingOverride(mapping));
                }
                Logger.ForAccount(account)
                    .Information($"Created Account for {session.Player.Account.Username}");
            }
            else if (!session.Player.CharacterManager.CheckChars())
            {
                session.Player.CharacterManager.CreateFirst(0, 0, 0, 0, 0, 0);
            }


            await session.SendAsync(new LoginReguestAckMessage(0, session.Player.Account.Id));

            if (!string.IsNullOrWhiteSpace(account.Nickname))
                await LoginAsync(session);
        }

        [MessageHandler(typeof(NickCheckReqMessage))]
        public async Task CheckNickHandler(GameSession session, NickCheckReqMessage message)
        {
            if (session.Player == null || !string.IsNullOrWhiteSpace(session.Player.Account.Nickname))
            {
                session.CloseAsync();
                return;
            }

            Logger.ForAccount(session)
                .Information("Checking nickname {nickname}", message.Nickname);

            var available = await IsNickAvailableAsync(message.Nickname);
            if (!available)
            {
                Logger.ForAccount(session)
                    .Information("Nickname not available: {nickname}", message.Nickname);
            }

            session.SendAsync(new NickCheckAckMessage(!available));
        }

        //[MessageHandler(typeof(CCreateNickReqMessage))]
        //public async Task CreateNickHandler(GameSession session, CCreateNickReqMessage message)
        //{
        //    if (session.Player == null || !string.IsNullOrWhiteSpace(session.Player.Account.Nickname))
        //    {
        //        session.CloseAsync();
        //        return;
        //    }

        //    Logger.ForAccount(session)
        //        .Information("Creating nickname {nickname}", message.Nickname);

        //    if (!await IsNickAvailableAsync(message.Nickname))
        //    {
        //        Logger.ForAccount(session)
        //            .Error("Nickname not available: {nickname}", message.Nickname);

        //        session.SendAsync(new NickCheckAckMessage(false));
        //        return;
        //    }

        //    session.Player.Account.Nickname = message.Nickname;
        //    using (var db = AuthDatabase.Open())
        //    {
        //        var mapping = OrmConfiguration
        //            .GetDefaultEntityMapping<AccountDto>()
        //            .Clone()
        //            .UpdatePropertiesExcluding(prop => prop.IsExcludedFromUpdates = true, nameof(AccountDto.Nickname));

        //        await db.UpdateAsync(new AccountDto { Id = (int)session.Player.Account.Id, Nickname = message.Nickname },
        //                    statement => statement.WithEntityMappingOverride(mapping));

        //    }
        //    //session.Send(new SCreateNickAckMessage { Nickname = msg.Nickname });
        //    await session.SendAsync(new ServerResultAckMessage(ServerResult.CreateNicknameSuccess));

        //    Logger.ForAccount(session)
        //        .Information("Created nickname {nickname}", message.Nickname);

        //    await LoginAsync(session);
        //}

        private static async Task LoginAsync(GameSession session)
        {
            var plr = session.Player;

            await session.SendAsync(new ItemInventoryInfoAckMessage
            {
                Items = plr.Inventory.Select(i => i.Map<PlayerItem, ItemDto>()).ToArray()
            });

            // Todo random shop
            //await session.SendAsync(new SRandomShopChanceInfoAckMessage { Progress = 10000 });
            await session.SendAsync(new CharacterCurrentSlotInfoAckMessage
            {
                ActiveCharacter = plr.CharacterManager.CurrentSlot,
                CharacterCount = (byte)plr.CharacterManager.Count,
                MaxSlots = 3
            });

            foreach (var @char in plr.CharacterManager)
            {
                await session.SendAsync(new CharacterCurrentInfoAckMessage
                {
                    Slot = @char.Slot,
                    Style = new CharacterStyle(@char.Gender, @char.Hair.Variation, @char.Face.Variation, @char.Shirt.Variation, @char.Pants.Variation, @char.Slot)
                });


                //var weps = @char.Weapons.GetItems().Select(i => i?.Id ?? 0).ToArray();
                //var skills = new[] { @char.Skills.GetItem(SkillSlot.Skill)?.Id ?? 0 };
                //var cloths = @char.Costumes.GetItems().Select(i => i?.Id ?? 0).ToArray();
                //
                //
                //List<CharacterItemBuffer> _Weapons = new List<CharacterItemBuffer>();
                //List<CharacterItemBuffer> _Skills = new List<CharacterItemBuffer>();
                //List<CharacterItemBuffer> _Clothes = new List<CharacterItemBuffer>();
                //
                //int foreachcount = 0;
                //foreach (var item in weps)
                //{
                //    _Weapons.Add(new CharacterItemBuffer { slotid = (byte)foreachcount, itemid = item });
                //    foreachcount++;
                //}
                //foreachcount = 0;
                //foreach (var item in skills)
                //{
                //    _Skills.Add(new CharacterItemBuffer { slotid = (byte)foreachcount, itemid = item });
                //    foreachcount++;
                //}
                //foreachcount = 0;
                //foreach (var item in cloths)
                //{
                //    _Clothes.Add(new CharacterItemBuffer { slotid = (byte)foreachcount, itemid = item });
                //    foreachcount++;
                //}

                var message = new CharacterCurrentItemInfoAckMessage
                {
                    Slot = @char.Slot,

                    Weapons = @char.Weapons.GetItems().Select(i => i?.Id ?? 0).ToArray(),
                    Skills = new[] { @char.Skills.GetItem(SkillSlot.Skill)?.Id ?? 0 },
                    Clothes = @char.Costumes.GetItems().Select(i => i?.Id ?? 0).ToArray(),
                };


                //var weapons = @char.Weapons.GetItems().Select(i => i?.Id ?? 0).ToArray();
                //Array.Copy(weapons, 0, message.Weapons, 6, weapons.Length);

                await session.SendAsync(message);
            }

            await session.SendAsync(new MoneyRefreshCashInfoAckMessage
            {
                PEN = plr.PEN,
                AP = plr.AP
            });
            await session.SendAsync(new MoenyRefreshCoinInfoAckMessage
            {
                ArcadeCoins = plr.Coins1,
                BuffCoins = plr.Coins2
            });
            await session.SendAsync(new ServerResultAckMessage(ServerResult.WelcomeToS4World));
            await session.SendAsync(new PlayerAccountInfoAckMessage(plr.Map<Player, PlayerAccountInfoDto>()));

            //ItemDto testitem = new ItemDto
            //{
            //    Id = 570,
            //    @ItemNumber = 6000001,
            //    PriceType = ItemPriceType.None,
            //    PeriodType = ItemPeriodType.None,
            //    Period =0,
            //    Color = 0,
            //    ExpireTime = DateTimeOffset.Now,
            //    Durability = 800,
            //    Effects = new ItemEffectDto[] { },
            //    EnchantMP = 0,
            //    EnchantLevel = 0,
            //};
            //session.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Add, testitem));



            //await session.SendAsync(new ServerResultAckMessage(ServerResult.WelcomeToS4World2));

            if (plr.Inventory.Count == 0)
            {
                IEnumerable<StartItemDto> startItems;
                using (var db = GameDatabase.Open())
                {
                    startItems = await db.FindAsync<StartItemDto>(statement => statement
                            .Where($"{nameof(StartItemDto.RequiredSecurityLevel):C} <= @{nameof(plr.Account.SecurityLevel)}")
                            .WithParameters(new { plr.Account.SecurityLevel }));
                }

                foreach (var startItem in startItems)
                {
                    var shop = GameServer.Instance.ResourceCache.GetShop();
                    var item = shop.Items.Values.First(group => group.GetItemInfo(startItem.ShopItemInfoId) != null);
                    var itemInfo = item.GetItemInfo(startItem.ShopItemInfoId);
                    var effect = itemInfo.EffectGroup.GetEffect(startItem.ShopEffectId);

                    if (itemInfo == null)
                    {
                        Logger.Warning("Cant find ShopItemInfo for Start item {startItemId} - Forgot to reload the cache?", startItem.Id);
                        continue;
                    }

                    var price = itemInfo.PriceGroup.GetPrice(startItem.ShopPriceId);
                    if (price == null)
                    {
                        Logger.Warning("Cant find ShopPrice for Start item {startItemId} - Forgot to reload the cache?", startItem.Id);
                        continue;
                    }

                    var color = startItem.Color;
                    if (color > item.ColorGroup)
                    {
                        Logger.Warning("Start item {startItemId} has an invalid color {color}", startItem.Id, color);
                        color = 0;
                    }

                    var count = startItem.Count;
                    if (count > 0 && item.ItemNumber.Category <= ItemCategory.Skill)
                    {
                        Logger.Warning("Start item {startItemId} cant have stacks(quantity={count})", startItem.Id, count);
                        count = 0;
                    }

                    if (count < 0)
                        count = 0;

                    plr.Inventory.Create(itemInfo, price, color, effect.Effect, (uint)count);
                }
            }

            //session.Send(new ItemEquipBoostItemInfoAckMessage());
            //session.Send(new ItemClearInvalidEquipItemAckMessage());
        }

        private static async Task<bool> IsNickAvailableAsync(string nickname)
        {
            var minLength = Config.Instance.Game.NickRestrictions.MinLength;
            var maxLength = Config.Instance.Game.NickRestrictions.MaxLength;
            var whitespace = Config.Instance.Game.NickRestrictions.WhitespaceAllowed;
            var ascii = Config.Instance.Game.NickRestrictions.AsciiOnly;

            if (string.IsNullOrWhiteSpace(nickname) || (!whitespace && nickname.Contains(" ")) ||
                nickname.Length < minLength || nickname.Length > maxLength ||
                (ascii && Encoding.UTF8.GetByteCount(nickname) != nickname.Length))
            {
                return false;
            }

            // check for repeating chars example: (AAAHello, HeLLLLo)
            var maxRepeat = Config.Instance.Game.NickRestrictions.MaxRepeat;
            if (maxRepeat > 0)
            {
                var counter = 1;
                var current = nickname[0];
                for (var i = 1; i < nickname.Length; i++)
                {
                    if (current != nickname[i])
                    {
                        if (counter > maxRepeat) return false;
                        counter = 0;
                        current = nickname[i];
                    }
                    counter++;
                }
            }

            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            using (var db = AuthDatabase.Open())
            {
                var nickExists = (await db.FindAsync<AccountDto>(statement => statement
                            .Where($"{nameof(AccountDto.Nickname):C} = @{nameof(nickname)}")
                            .WithParameters(new { nickname })))
                    .Any();

                var nickReserved = (await db.FindAsync<NicknameHistoryDto>(statement => statement
                            .Where($"{nameof(NicknameHistoryDto.Nickname):C} = @{nameof(nickname)} AND ({nameof(NicknameHistoryDto.ExpireDate):C} = -1 OR {nameof(NicknameHistoryDto.ExpireDate):C} > @{nameof(now)})")
                            .WithParameters(new { nickname, now })))
                    .Any();
                return !nickExists && !nickReserved;
            }
        }

        [MessageHandler(typeof(Message.Chat.LoginReqMessage))]
        public async Task Chat_LoginHandler(ChatServer server, ChatSession session, Message.Chat.LoginReqMessage message)
        {
            Logger.ForAccount(message.AccountId, "")
                .Information("Login from {remoteEndPoint}", session.RemoteEndPoint);

            uint sessionId;
            if (!uint.TryParse(message.SessionId, out sessionId))
            {
                Logger.ForAccount(message.AccountId, "")
                    .Error("Invalid sessionId");
                session.SendAsync(new Message.Chat.LoginAckMessage(2));
                return;
            }

            var plr = GameServer.Instance.PlayerManager[message.AccountId];
            if (plr == null)
            {
                Logger.ForAccount(message.AccountId, "")
                    .Error("Login failed");
                session.SendAsync(new Message.Chat.LoginAckMessage(3));
                return;
            }

            if (plr.ChatSession != null)
            {
                Logger.ForAccount(session)
                    .Error("Already online");
                session.SendAsync(new Message.Chat.LoginAckMessage(4));
                return;
            }

            session.GameSession = plr.Session;
            plr.ChatSession = session;

            Logger.ForAccount(session)
                .Information("Login success");
            session.SendAsync(new Message.Chat.LoginAckMessage(0));
            session.SendAsync(new Message.Chat.DenyListAckMessage(plr.DenyManager.Select(d => d.Map<Deny, DenyDto>()).ToArray()));
        }

        [MessageHandler(typeof(Message.Relay.CRequestLoginMessage))]
        public async Task Relay_LoginHandler(RelayServer server, RelaySession session, Message.Relay.CRequestLoginMessage message)
        {
            var ip = session.RemoteEndPoint;
            Logger.ForAccount(message.AccountId, "")
                .Information("Login from {remoteAddress}", ip);

            var plr = GameServer.Instance.PlayerManager[message.AccountId];
            if (plr == null)
            {
                Logger.ForAccount(message.AccountId, "")
                    .Error("Login failed");
                session.SendAsync(new Message.Relay.SNotifyLoginResultMessage(1));
                return;
            }

            if (plr.RelaySession != null && plr.RelaySession.IsConnected)
            {
                Logger.ForAccount(session)
                    .Error("Already online");
                session.SendAsync(new Message.Relay.SNotifyLoginResultMessage(2));
                return;
            }

            var gameIp = plr.Session.RemoteEndPoint;
            if (!gameIp.Address.Equals(ip.Address))
            {
                Logger.ForAccount(message.AccountId, "")
                    .Error("Suspicious login");
                session.SendAsync(new Message.Relay.SNotifyLoginResultMessage(3));
                return;
            }

            if (plr.Room == null || plr.Room?.Id != message.RoomLocation.RoomId)
            {
                Logger.ForAccount(message.AccountId, "")
                    .Error($"Suspicious login(Not in a room/Invalid room id) (given id:{message.RoomLocation.RoomId})");
                session.SendAsync(new Message.Relay.SNotifyLoginResultMessage(4));
                return;
            }

            session.GameSession = plr.Session;
            plr.RelaySession = session;

            Logger.ForAccount(session)
                .Information("Login success");

            await session.SendAsync(new Message.Relay.SEnterLoginPlayerMessage(plr.RoomInfo.Slot, plr.Account.Id, plr.Account.Nickname));
            foreach (var p in plr.Room.Players.Values.Where(p => p.RelaySession?.P2PGroup != null /*&& p.Account.Id != plr.Account.Id*/))
            {
                await p.RelaySession.SendAsync(new Message.Relay.SEnterLoginPlayerMessage(plr.RoomInfo.Slot, plr.Account.Id, plr.Account.Nickname));
                await session.SendAsync(new Message.Relay.SEnterLoginPlayerMessage(p.RoomInfo.Slot, p.Account.Id, p.Account.Nickname));
            }

            plr.Room.Group.Join(session.HostId);
            await session.SendAsync(new Message.Relay.SNotifyLoginResultMessage(0));

            plr.RoomInfo.IsConnecting = false;
            plr.Room.OnPlayerJoined(new RoomPlayerEventArgs(plr));
        }
    }
}

