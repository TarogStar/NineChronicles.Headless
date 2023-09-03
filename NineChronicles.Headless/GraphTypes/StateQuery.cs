#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Lib9c.Model.Order;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Explorer.GraphTypes;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Arena;
using Nekoyume.Battle;
using Nekoyume.Extensions;
using Nekoyume.Model;
using Nekoyume.Model.Arena;
using Nekoyume.Model.EnumType;
using Nekoyume.Model.Item;
using Nekoyume.Model.Skill;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using Nekoyume.TableData.Crystal;
using NineChronicles.Headless.GraphTypes.Abstractions;
using NineChronicles.Headless.GraphTypes.States;
using NineChronicles.Headless.GraphTypes.States.Models;
using NineChronicles.Headless.GraphTypes.States.Models.Item.Enum;
using NineChronicles.Headless.GraphTypes.States.Models.Table;


namespace NineChronicles.Headless.GraphTypes
{
    public partial class StateQuery : ObjectGraphType<StateContext>
    {
        public StateQuery()
        {
            Name = "StateQuery";

            AvatarStateType.AvatarStateContext? GetAvatarState(StateContext context, Address address)
            {
                try
                {
                    return new AvatarStateType.AvatarStateContext(
                        context.AccountState.GetAvatarState(address),
                        context.AccountState,
                        context.BlockIndex);
                }
                catch (InvalidAddressException)
                {
                    return null;
                }
            }

            Field<AvatarStateType>(
                name: "avatar",
                description: "State for avatar.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "avatarAddress",
                    Description = "Address of avatar."
                }),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("avatarAddress");
                    return GetAvatarState(context.Source, address)
                        ?? throw new InvalidOperationException($"The state {address} doesn't exists");
                });
            Field<NonNullGraphType<ListGraphType<AvatarStateType>>>(
                name: "avatars",
                description: "Avatar states having some order as addresses",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ListGraphType<NonNullGraphType<AddressType>>>>
                    {
                        Name = "addresses",
                        Description = "Addresses of avatars to query."
                    }
                ),
                resolve: context =>
                {
                    return context.GetArgument<List<Address>>("addresses")
                        .AsParallel()
                        .AsOrdered()
                        .Select(address => GetAvatarState(context.Source, address));
                }
            );
            Field<RankingMapStateType>(
                name: "rankingMap",
                description: "State for avatar EXP record.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "index",
                        Description = "RankingMapState index. 0 ~ 99"
                    }),
                resolve: context =>
                {
                    var index = context.GetArgument<int>("index");
                    if (context.Source.GetState(RankingState.Derive(index)) is { } state)
                    {
                        return new RankingMapState((Dictionary)state);
                    }

                    return null;
                });
            Field<ShopStateType>(
                name: "shop",
                description: "State for shop.",
                deprecationReason: "Shop is migrated to ShardedShop and not using now. Use shardedShop() instead.",
                resolve: context => context.Source.GetState(Addresses.Shop) is { } state
                    ? new ShopState((Dictionary)state)
                    : null);
            Field<ShardedShopStateV2Type>(
                name: "shardedShop",
                description: "State for sharded shop.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ItemSubTypeEnumType>>
                    {
                        Name = "itemSubType",
                        Description = "ItemSubType for shard. see from https://github.com/planetarium/lib9c/blob/main/Lib9c/Model/Item/ItemType.cs#L13"
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "nonce",
                        Description = "Nonce for shard. It's not considered if itemSubtype is kind of costume or title. 0 ~ 15"
                    }),
                resolve: context =>
                {
                    var subType = context.GetArgument<ItemSubType>("itemSubType");
                    var nonce = context.GetArgument<int>("nonce").ToString("X").ToLower();

                    if (context.Source.GetState(ShardedShopStateV2.DeriveAddress(subType, nonce)) is { } state)
                    {
                        return new ShardedShopStateV2((Dictionary)state);
                    }

                    return null;
                });
            Field<WeeklyArenaStateType>(
                name: "weeklyArena",
                description: "State for weekly arena.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "index",
                        Description = "WeeklyArenaState index. It increases every 56,000 blocks."
                    }),
                resolve: context =>
                {
                    var index = context.GetArgument<int>("index");
                    var arenaAddress = WeeklyArenaState.DeriveAddress(index);
                    if (context.Source.GetState(arenaAddress) is { } state)
                    {
                        var arenastate = new WeeklyArenaState((Dictionary)state);
                        if (arenastate.OrderedArenaInfos.Count == 0)
                        {
                            var listAddress = arenaAddress.Derive("address_list");
                            if (context.Source.GetState(listAddress) is List rawList)
                            {
                                var addressList = rawList.ToList(StateExtensions.ToAddress);
                                var arenaInfos = new List<ArenaInfo>();
                                foreach (var address in addressList)
                                {
                                    var infoAddress = arenaAddress.Derive(address.ToByteArray());
                                    if (context.Source.GetState(infoAddress) is Dictionary rawInfo)
                                    {
                                        var info = new ArenaInfo(rawInfo);
                                        arenaInfos.Add(info);
                                    }
                                }
#pragma warning disable CS0618 // Type or member is obsolete
                                arenastate.OrderedArenaInfos.AddRange(arenaInfos.OrderByDescending(a => a.Score)
                                    .ThenBy(a => a.CombatPoint));
#pragma warning restore CS0618 // Type or member is obsolete
                            }
                        }

                        return arenastate;
                    }

                    return null;
                });
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<ArenaInformationType>>>>(
                name: "arenaInformation",
                description: "List of arena information of requested arena and avatar list",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "championshipId",
                        Description = "Championship ID to get arena information"
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "round",
                        Description = "Round of championship to get arena information"
                    },
                    new QueryArgument<NonNullGraphType<ListGraphType<NonNullGraphType<AddressType>>>>
                    {
                        Name = "avatarAddresses",
                        Description = "List of avatar address to get arena information"
                    }
                ),
                resolve: context =>
                {
                    var championshipId = context.GetArgument<int>("championshipId");
                    var round = context.GetArgument<int>("round");
                    return context.GetArgument<List<Address>>("avatarAddresses").AsParallel().AsOrdered().Select(
                        address =>
                        {
                            var infoAddr = ArenaInformation.DeriveAddress(address, championshipId, round);
                            var scoreAddr = ArenaScore.DeriveAddress(address, championshipId, round);

                            return (
                                address,
                                new ArenaInformation((List)context.Source.GetState(infoAddr)!),
                                new ArenaScore((List)context.Source.GetState(scoreAddr)!)
                            );
                        }
                    );
                }
            );
            Field<AgentStateType>(
                name: "agent",
                description: "State for agent.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "address",
                    Description = "Address of agent."
                }),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("address");
                    if (context.Source.GetState(address) is Dictionary state)
                    {
                        return new AgentStateType.AgentStateContext(
                            new AgentState(state),
                            context.Source.AccountState,
                            context.Source.BlockIndex
                        );
                    }

                    return null;
                }
            );

            StakeStateType.StakeStateContext? GetStakeState(StateContext ctx, Address agentAddress)
            {
                if (ctx.GetState(StakeState.DeriveAddress(agentAddress)) is Dictionary state)
                {
                    return new StakeStateType.StakeStateContext(
                        new StakeState(state),
                        ctx.AccountState,
                        ctx.BlockIndex
                    );
                }

                return null;
            }

            Field<StakeStateType>(
                name: nameof(StakeState),
                description: "State for staking.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "address",
                    Description = "Address of agent who staked."
                }),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("address");
                    return GetStakeState(context.Source, address);
                }
            );

            Field<NonNullGraphType<ListGraphType<StakeStateType>>>(
                name: "StakeStates",
                description: "Staking states having same order as addresses",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ListGraphType<AddressType>>>
                    {
                        Name = "addresses",
                        Description = "Addresses of agent who staked."
                    }
                ),
                resolve: context =>
                {
                    return context.GetArgument<List<Address>>("addresses")
                        .AsParallel()
                        .AsOrdered()
                        .Select(address => GetStakeState(context.Source, address));
                }
            );

            Field<MonsterCollectionStateType>(
                nameof(MonsterCollectionState),
                description: "State for monster collection.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "agentAddress",
                        Description = "Address of agent."
                    }
                ),
                resolve: context =>
                {
                    var agentAddress = context.GetArgument<Address>("agentAddress");
                    if (!(context.Source.GetState(agentAddress) is Dictionary value))
                    {
                        return null;
                    }
                    var agentState = new AgentState(value);
                    var deriveAddress = MonsterCollectionState.DeriveAddress(agentAddress, agentState.MonsterCollectionRound);
                    if (context.Source.GetState(deriveAddress) is Dictionary state)
                    {
                        return new MonsterCollectionState(state);
                    }

                    return null;
                }
            );

            Field<MonsterCollectionSheetType>(
                nameof(MonsterCollectionSheet),
                resolve: context =>
                {
                    var sheetAddress = Addresses.GetSheetAddress<MonsterCollectionSheet>();
                    var rewardSheetAddress = Addresses.GetSheetAddress<MonsterCollectionRewardSheet>();
                    IReadOnlyList<IValue?> values = context.Source.GetStates(new[]
                    {
                        sheetAddress,
                        rewardSheetAddress,
                    });
                    if (values[0] is Text ss &&
                        values[1] is Text srs)
                    {
                        var monsterCollectionSheet = new MonsterCollectionSheet();
                        monsterCollectionSheet.Set(ss);
                        var monsterCollectionRewardSheet = new MonsterCollectionRewardSheet();
                        monsterCollectionRewardSheet.Set(srs);
                        return (monsterCollectionSheet, monsterCollectionRewardSheet);
                    }

                    return null;
                }
            );

            Field<StakeRewardsType>(
                "stakeRewards",
                resolve: context =>
                {
                    StakeRegularRewardSheet stakeRegularRewardSheet;
                    StakeRegularFixedRewardSheet stakeRegularFixedRewardSheet;

                    if (context.Source.BlockIndex < StakeState.StakeRewardSheetV2Index)
                    {
                        stakeRegularRewardSheet = new StakeRegularRewardSheet();
                        stakeRegularRewardSheet.Set(ClaimStakeReward.V1.StakeRegularRewardSheetCsv);
                        stakeRegularFixedRewardSheet = new StakeRegularFixedRewardSheet();
                        stakeRegularFixedRewardSheet.Set(ClaimStakeReward.V1.StakeRegularFixedRewardSheetCsv);
                    }
                    else
                    {
                        IReadOnlyList<IValue?> values = context.Source.GetStates(new[]
                        {
                            Addresses.GetSheetAddress<StakeRegularRewardSheet>(),
                            Addresses.GetSheetAddress<StakeRegularFixedRewardSheet>()
                        });

                        if (!(values[0] is Text sv && values[1] is Text fsv))
                        {
                            return null;
                        }

                        stakeRegularRewardSheet = new StakeRegularRewardSheet();
                        stakeRegularRewardSheet.Set(sv);
                        stakeRegularFixedRewardSheet = new StakeRegularFixedRewardSheet();
                        stakeRegularFixedRewardSheet.Set(fsv);
                    }

                    return (stakeRegularRewardSheet, stakeRegularFixedRewardSheet);
                }
            );
            Field<CrystalMonsterCollectionMultiplierSheetType>(
                name: nameof(CrystalMonsterCollectionMultiplierSheet),
                resolve: context =>
                {
                    var sheetAddress = Addresses.GetSheetAddress<CrystalMonsterCollectionMultiplierSheet>();
                    IValue? sheetValue = context.Source.GetState(sheetAddress);
                    if (sheetValue is Text sv)
                    {
                        var crystalMonsterCollectionMultiplierSheet = new CrystalMonsterCollectionMultiplierSheet();
                        crystalMonsterCollectionMultiplierSheet.Set(sv);
                        return crystalMonsterCollectionMultiplierSheet;
                    }

                    return null;
                });

            Field<ListGraphType<IntGraphType>>(
                "unlockedRecipeIds",
                description: "List of unlocked equipment recipe sheet row ids.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "avatarAddress",
                    Description = "Address of avatar."
                }),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var address = avatarAddress.Derive("recipe_ids");
                    IReadOnlyList<IValue?> values = context.Source.AccountState.GetStates(new[] { address });
                    if (values[0] is List rawRecipeIds)
                    {
                        return rawRecipeIds.ToList(StateExtensions.ToInteger);
                    }

                    return null;
                }
            );

            Field<ListGraphType<IntGraphType>>(
                "unlockedWorldIds",
                description: "List of unlocked world sheet row ids.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "avatarAddress",
                    Description = "Address of avatar."
                }),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var address = avatarAddress.Derive("world_ids");
                    IReadOnlyList<IValue?> values = context.Source.AccountState.GetStates(new[] { address });
                    if (values[0] is List rawWorldIds)
                    {
                        return rawWorldIds.ToList(StateExtensions.ToInteger);
                    }

                    return null;
                }
            );

            Field<RaiderStateType>(
                name: "raiderState",
                description: "world boss season user information.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "raiderAddress",
                        Description = "address of world boss season."
                    }
                ),
                resolve: context =>
                {
                    var raiderAddress = context.GetArgument<Address>("raiderAddress");
                    if (context.Source.GetState(raiderAddress) is List list)
                    {
                        return new RaiderState(list);
                    }

                    return null;
                }
            );

            Field<NonNullGraphType<IntGraphType>>(
                "raidId",
                description: "world boss season id by block index.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<LongGraphType>>
                    {
                        Name = "blockIndex"
                    },
                    new QueryArgument<BooleanGraphType>
                    {
                        Name = "prev",
                        Description = "find previous raid id.",
                        DefaultValue = false
                    }
                ),
                resolve: context =>
                {
                    var blockIndex = context.GetArgument<long>("blockIndex");
                    var prev = context.GetArgument<bool>("prev");
                    var sheet = new WorldBossListSheet();
                    var address = Addresses.GetSheetAddress<WorldBossListSheet>();
                    if (context.Source.GetState(address) is Text text)
                    {
                        sheet.Set(text);
                    }

                    return prev
                        ? sheet.FindPreviousRaidIdByBlockIndex(blockIndex)
                        : sheet.FindRaidIdByBlockIndex(blockIndex);
                }
            );

            Field<WorldBossStateType>(
                "worldBossState",
                description: "world boss season boss information.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "bossAddress"
                }),
                resolve: context =>
                {
                    var bossAddress = context.GetArgument<Address>("bossAddress");
                    if (context.Source.GetState(bossAddress) is List list)
                    {
                        return new WorldBossState(list);
                    }

                    return null;
                }
            );
            Field<CombinationSlotStateType>(
                "CombinationSlot",
                description: "Allows you to see crafting slot data.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Address of avatar."
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "slot",
                        Description = "Slot index 0-3"
                    }
                ),
                resolve: context =>
                {
                    var agentAddress = context.GetArgument<Address>("avatarAddress");
                    var index = context.GetArgument<int>("slot");

                    if(index < 0 || index > 3)
                    {
                        throw new Exception("Invalid Slot Index");
                    }
                    var deriveAddress = CombinationSlotState.DeriveAddress(agentAddress, index);
                    if (context.Source.GetState(deriveAddress) is Dictionary state)
                    {
                        return new CombinationSlotState(state);
                    }

                    return null;
                }
            );
            Field<ChampionshipArenaStateType>(
                name: "championshipArena",
                description: "State for championShip arena.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "championshipid",
                        Description = "Championship Id, increases each season"
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "round",
                        Description = "The round number"
                    }),
                resolve: context =>
                {
                    try {
                        var championshipId = context.GetArgument<int>("championshipid");
                        var round = context.GetArgument<int>("round");
                        var sheets = context.Source.GetSheets(sheetTypes: new[]
                        {
                            typeof(ArenaSheet),
                            typeof(CharacterSheet),
                            typeof(CostumeStatSheet),
                            typeof(RuneListSheet),
                            typeof(RuneOptionSheet)
                        });
                        var arenaSheet = sheets.FirstOrDefault().Value.sheet as ArenaSheet;
                        if (arenaSheet == null || !arenaSheet.TryGetValue(championshipId, out var arenaRow))
                        {
                            throw new SheetRowNotFoundException(nameof(ArenaSheet),
                                $"championship Id : {championshipId}");
                        }
                        if (!arenaRow.TryGetRound(round, out var roundData))
                        {
                            throw new RoundNotFoundException(
                                $"[{nameof(BattleArena)}] ChampionshipId({arenaRow.ChampionshipId}) - round({round})");
                        }

                        var arenaParticipantsAdr =
                            ArenaParticipants.DeriveAddress(roundData.ChampionshipId, roundData.Round);
                        if (!context.Source.TryGetArenaParticipants(arenaParticipantsAdr, out var arenaParticipants))
                        {
                            throw new ArenaParticipantsNotFoundException(
                                $"[{nameof(BattleArena)}] ChampionshipId({roundData.ChampionshipId}) - round({roundData.Round})");
                        }
                        var championshipInfo = new ChampionshipArenaState();
                        championshipInfo.StartIndex = roundData.StartBlockIndex;
                        championshipInfo.EndIndex = roundData.EndBlockIndex;
                        championshipInfo.Address = arenaParticipantsAdr;
                        List<ChampionArenaInfo> arenaInformations = new List<ChampionArenaInfo>();
                        var gameConfigState = context.Source.GetGameConfigState();
                        var interval = gameConfigState.DailyArenaInterval;
                        var currentTicketResetCount = ArenaHelper.GetCurrentTicketResetCount(
                                        context.Source.BlockIndex!.Value, roundData.StartBlockIndex, interval);
                        foreach (var participant in arenaParticipants.AvatarAddresses)
                        {
                            
                            var arenaInformationAdr =
                                ArenaInformation.DeriveAddress(participant, roundData.ChampionshipId, roundData.Round);
                            if (!context.Source.TryGetArenaInformation(arenaInformationAdr, out var arenaInformation))
                            {
                                continue;
                            }
                            var arenaScoreAdr =
                                    ArenaScore.DeriveAddress(participant, roundData.ChampionshipId, roundData.Round);
                            if (!context.Source.TryGetArenaScore(arenaScoreAdr, out var arenaScore))
                            {
                                continue;
                            }
                            var ticket = arenaInformation.Ticket;
                            if (ticket == 0 && arenaInformation.TicketResetCount < currentTicketResetCount)
                            {
                                ticket = 8;
                            }
                            var avatar = context.Source.GetAvatarStateV2(participant);
                            var arenaAvatarStateAdr = ArenaAvatarState.DeriveAddress(participant);
                            var arenaAvatarState = context.Source.GetArenaAvatarState(arenaAvatarStateAdr, avatar);

                            var characterSheet = sheets.GetSheet<CharacterSheet>();
                            if (!characterSheet.TryGetValue(avatar.characterId, out var characterRow))
                            {
                                throw new SheetRowNotFoundException("CharacterSheet", avatar.characterId);
                            }
                            var costumeStatSheet = sheets.GetSheet<CostumeStatSheet>();



                            var runeSlotStateAddress = RuneSlotState.DeriveAddress(participant, BattleType.Arena);
                            var runeSlotState = context.Source.TryGetState(runeSlotStateAddress, out List rawRuneSlotState)
                                ? new RuneSlotState(rawRuneSlotState)
                                : new RuneSlotState(BattleType.Raid);
                            var runeListSheet = sheets.GetSheet<RuneListSheet>();
                            var runeStates = new List<RuneState>();
                            var runeSlotInfoList = runeSlotState.GetRuneSlot();

                            foreach (var address in runeSlotInfoList.Select(info => RuneState.DeriveAddress(participant, info.RuneId)))
                            {
                                if (context.Source.TryGetState(address, out List rawRuneState))
                                {
                                    runeStates.Add(new RuneState(rawRuneState));
                               }
                            }

                            var runeOptionSheet = sheets.GetSheet<RuneOptionSheet>();
                            var runeOptions = new List<RuneOptionSheet.Row.RuneOptionInfo>();
                            foreach (var runeState in runeStates)
                            {
                                if (!runeOptionSheet.TryGetValue(runeState.RuneId, out var optionRow))
                                {
                                    throw new SheetRowNotFoundException("RuneOptionSheet", runeState.RuneId);
                                }

                                if (!optionRow.LevelOptionMap.TryGetValue(runeState.Level, out var option))
                                {
                                    throw new SheetRowNotFoundException("RuneOptionSheet", runeState.Level);
                                }

                                runeOptions.Add(option);
                            }

                            var avatarEquipments = avatar.inventory.Equipments;

                            var avatarCostumes = avatar.inventory.Costumes;

                            List<Equipment> arenaEquipementList = avatarEquipments.Where(f=>arenaAvatarState.Equipments.Contains(f.ItemId)).Select(n => n).ToList();
                            List<Costume> arenaCostumeList = avatarCostumes.Where(f=>arenaAvatarState.Costumes.Contains(f.ItemId)).Select(n => n).ToList();

                            var cp = Nekoyume.Battle.CPHelper.TotalCP(
                                arenaEquipementList, arenaCostumeList,
                                runeOptions, avatar.level,
                                characterRow, costumeStatSheet
                            );

                            var arenaInfo = new ChampionArenaInfo
                            {
                                AvatarAddress = participant,
                                AgentAddress = avatar.agentAddress,
                                AvatarName = avatar.name,
                                Win = arenaInformation.Win,
                                Ticket = ticket,
                                Lose = arenaInformation.Lose,
                                Score = arenaScore.Score,
                                PurchasedTicketCount = arenaInformation.PurchasedTicketCount,
                                TicketResetCount = arenaInformation.TicketResetCount,
                                Active = true,
                                CP = cp,
                                Equipment = arenaAvatarState.Equipments,
                                Costumes = arenaAvatarState.Costumes
                            };
                            arenaInformations.Add(arenaInfo);
                        }

                        var ranks = StateContext.AddRank(arenaInformations.ToArray());
                        foreach (var rank in ranks)
                        {
                            var info = arenaInformations.First(a => a.AvatarAddress == rank.AvatarAddress);
                            if (info != null)
                            {
                                info.Rank = rank.Rank;
                            }
                        }
                        var orderInfos = arenaInformations.OrderBy(a => a.Rank).ToList();
                        championshipInfo.OrderedArenaInfos = orderInfos;
                        return championshipInfo;
                    }
                    catch(Exception ex){
                        Console.WriteLine("Arena Exception = " + ex.Message);
                        return null;
                    }                   
            });
            Field<CombinationCrystalStateType>(
                "CraftCrystalCheck",
                description: "Allows you to pull data",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Address of avatar."
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "recipeId",
                        Description = "recipeid"
                    }
                ),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var recipeId = context.GetArgument<int>("recipeId");

                    CombinationCrystalState combinationCrystalState = new CombinationCrystalState();

                    Dictionary<Type, (Address, ISheet)> sheets = context.Source.GetSheets(sheetTypes: new[]
                    {
                        typeof(CrystalHammerPointSheet),
                    });

                    var existHammerPointSheet = sheets.TryGetSheet(out CrystalHammerPointSheet hammerPointSheet);
                    var hammerPointAddress = Addresses.GetHammerPointStateAddress(avatarAddress, recipeId);
                    var hammerPointState = new HammerPointState(hammerPointAddress, recipeId);
                    CrystalHammerPointSheet.Row? hammerPointRow = null;
                    if (existHammerPointSheet)
                    {
                        if (context.Source.TryGetState(hammerPointAddress, out List serialized))
                        {
                            hammerPointState =
                                new HammerPointState(hammerPointAddress, serialized);
                        }

                        // Validate HammerPointSheet by recipeId
                        if (!hammerPointSheet.TryGetValue(recipeId, out hammerPointRow))
                        {
                            throw new Exception("Bad Data");
                        }

                        combinationCrystalState.CrystalCost = hammerPointRow.CRYSTAL;
                        combinationCrystalState.RecipeId = recipeId;
                        combinationCrystalState.MaxPoint = hammerPointRow.MaxPoint;
                        combinationCrystalState.CurrentPoint = hammerPointState.HammerPoint;
                    }
                    return combinationCrystalState;
                }
            );
            Field<ListGraphType<CombinationSlotStateTypeExtended>>(
                "CombinationSlotNEW",
                description: "Allows you to pull data ",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Address of avatar."
                    }
                ),
                resolve: context =>
                {
                    try
                    {
                        var avatarAddress = context.GetArgument<Address>("avatarAddress");
                        List<CombinationSlotStateExtended> combinationSlotDataList = new List<CombinationSlotStateExtended>();
                        for(int slotIndex = 0; slotIndex < 4; slotIndex++)
                        {
                            var deriveAddress = CombinationSlotState.DeriveAddress(avatarAddress, slotIndex);
                            if (context.Source.GetState(deriveAddress) is Dictionary state)
                            {
                                CombinationSlotStateExtended combinationSlotData = new CombinationSlotStateExtended();

                                var newCombSlotState = new CombinationSlotState(state);
                                var states = newCombSlotState.Result;
                                if(states is not null && newCombSlotState.UnlockBlockIndex > context.Source.BlockIndex)
                                {
                                    combinationSlotData.SlotIndex = slotIndex;
                                    combinationSlotData.ItemGUID = states.itemUsable.ItemId;
                                    combinationSlotData.Stars = states.itemUsable.GetOptionCount();
                                    combinationSlotData.Spell = states.itemUsable.Skills.Count();
                                    combinationSlotData.UnlockBlockIndex = newCombSlotState.UnlockBlockIndex;
                                    combinationSlotDataList.Add(combinationSlotData);
                                }
                            }     
                        }
                        return combinationSlotDataList;
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(ex.StackTrace);
                        return null;
                    }
                }
            );
            Field<GatchaStateType>(
                "gachaBuff",
                description: "Allows you to pull data ",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Address of avatar."
                    }
                ),
                resolve: context =>
                {
                    try
                    {
                        var avatarAddress = context.GetArgument<Address>("avatarAddress");
                        var gachaStateAddress = Addresses.GetSkillStateAddressFromAvatarAddress(avatarAddress);

                        // Invalid Avatar address, or does not have GachaState.
                        if (!context.Source.TryGetState(gachaStateAddress, out List rawGachaState))
                        {
                            throw new FailedLoadStateException(
                                $"Can't find {nameof(CrystalRandomSkillState)}. Gacha state address:{gachaStateAddress}");
                        }

                        var gachaState = new CrystalRandomSkillState(gachaStateAddress, rawGachaState);
                        var stageBuffSheet = context.Source.GetSheet<CrystalStageBuffGachaSheet>();

                        GatchaState gatchaStateResult = new GatchaState();

                        gatchaStateResult.CurrentStarCount = gachaState.StarCount;
                        gatchaStateResult.StageId = gachaState.StageId;
                        gatchaStateResult.RequiredStarCount = stageBuffSheet[gachaState.StageId].MaxStar;

                        return gatchaStateResult;
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(ex.StackTrace);
                        return null;
                    }
                }
            );
            Field<WorldBossKillRewardRecordType>(
                "worldBossKillRewardRecord",
                description: "user boss kill reward record by world boss season.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "worldBossKillRewardRecordAddress"
                }),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("worldBossKillRewardRecordAddress");
                    if (context.Source.GetState(address) is List list)
                    {
                        return new WorldBossKillRewardRecord(list);
                    }
                    return null;
                }
            );
            Field<NonNullGraphType<FungibleAssetValueWithCurrencyType>>("balance",
                description: "asset balance by currency.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "address"
                    },
                    new QueryArgument<NonNullGraphType<CurrencyInputType>>
                    {
                        Name = "currency"
                    }
                ),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("address");
                    var currency = context.GetArgument<Currency>("currency");
                    return context.Source.GetBalance(address, currency);
                }
            );

            Field<ListGraphType<NonNullGraphType<AddressType>>>(
                "raiderList",
                description: "raider address list by world boss season.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "raiderListAddress"
                }),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("raiderListAddress");
                    if (context.Source.GetState(address) is List list)
                    {
                        return list.ToList(StateExtensions.ToAddress);
                    }
                    return null;
                }
            );
            
            Field<RuneStateType>(
                "RuneSlot",
                description: "Grab Rune Slot Data",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Address of agent."
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "runeId",
                        Description = "Rune ID"
                    }
                ),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var runeId = context.GetArgument<int>("runeId");

                    var deriveAddress = RuneState.DeriveAddress(avatarAddress, runeId);
                    if (context.Source.TryGetState(deriveAddress, out List runes))
                    {
                        return new RuneState(runes);
                    }

                    return null;
                });
            Field<NonNullGraphType<MeadPledgeType>>(
                "pledge",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "agentAddress"
                }),
                resolve: context =>
                {
                    var agentAddress = context.GetArgument<Address>("agentAddress");
                    var pledgeAddress = agentAddress.GetPledgeAddress();
                    Address? address = null;
                    bool approved = false;
                    int mead = 0;
                    if (context.Source.GetState(pledgeAddress) is List l)
                    {
                        address = l[0].ToAddress();
                        approved = l[1].ToBoolean();
                        mead = l[2].ToInteger();
                    }

                    return (address, approved, mead);
                }
            );

            RegisterGarages();
        }
    }
}
