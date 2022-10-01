using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using Bencodex;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Explorer.GraphTypes;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Helper;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using Serilog;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActionQuery : ObjectGraphType
    {
        private static readonly Codec Codec = new Codec();

        public ActionQuery(StandaloneContext standaloneContext)
        {
            Field<ByteStringType>(
                name: "stake",
                arguments: new QueryArguments(new QueryArgument<BigIntGraphType>
                {
                    Name = "amount",
                    Description = "An amount to stake.",
                }),
                resolve: context =>
                    Codec.Encode(
                    ((NCAction)new Stake(context.GetArgument<BigInteger>("amount"))).PlainValue));

            Field<ByteStringType>(
                name: "claimStakeReward",
                arguments: new QueryArguments(
                    new QueryArgument<AddressType>
                    {
                        Name = "avatarAddress",
                        Description = "The avatar address to receive staking rewards."
                    }),
                resolve: context =>
                    Codec.Encode(
                        ((NCAction)new ClaimStakeReward(
                            context.GetArgument<Address>("avatarAddress"))).PlainValue));
            Field<NonNullGraphType<ByteStringType>>(
                name: "migrateMonsterCollection",
                arguments: new QueryArguments(
                    new QueryArgument<AddressType>
                    {
                        Name = "avatarAddress",
                        Description = "The avatar address to receive monster collection rewards."
                    }),
                resolve: context =>
                    Codec.Encode(
                        ((NCAction)new MigrateMonsterCollection(
                            context.GetArgument<Address>("avatarAddress"))).PlainValue));
            Field<ByteStringType>(
                name: "grinding",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Address of avatar.",
                    },
                    new QueryArgument<NonNullGraphType<ListGraphType<GuidGraphType>>>
                    {
                        Name = "equipmentIds",
                        Description = "List of equipment ItemId.",
                    },
                    new QueryArgument<BooleanGraphType>
                    {
                        Name = "chargeAp",
                        Description = "Flag to Charge Action Point.",
                    }
                ),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var equipmentIds = context.GetArgument<List<Guid>>("equipmentIds");
                    var chargeAp = context.GetArgument<bool>("chargeAp");
                    NCAction action = new Grinding
                    {
                        AvatarAddress = avatarAddress,
                        EquipmentIds = equipmentIds,
                        ChargeAp = chargeAp,
                    };
                    return Codec.Encode(action.PlainValue);
                });
            Field<ByteStringType>(
                name: "unlockEquipmentRecipe",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Address of avatar.",
                    },
                    new QueryArgument<NonNullGraphType<ListGraphType<IntGraphType>>>
                    {
                        Name = "recipeIds",
                        Description = "List of EquipmentRecipeSheet row ids to unlock.",
                    }
                ),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var recipeIds = context.GetArgument<List<int>>("recipeIds");
                    NCAction action = new UnlockEquipmentRecipe
                    {
                        AvatarAddress = avatarAddress,
                        RecipeIds = recipeIds,
                    };
                    return Codec.Encode(action.PlainValue);
                });
            Field<ByteStringType>(
                name: "unlockWorld",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Address of avatar.",
                    },
                    new QueryArgument<NonNullGraphType<ListGraphType<IntGraphType>>>
                    {
                        Name = "worldIds",
                        Description = "List of WorldUnlockSheet row world_id_to_unlock.",
                    }
                ),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var worldIds = context.GetArgument<List<int>>("worldIds");
                    NCAction action = new UnlockWorld
                    {
                        AvatarAddress = avatarAddress,
                        WorldIds = worldIds,
                    };
                    return Codec.Encode(action.PlainValue);
                });
            Field<ByteStringType>(
                name: "hackAndSlashAction",
                description: "Start stage to get material.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Avatar address."
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "worldId",
                        Description = "World ID containing the stage ID."
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "stageId",
                        Description = "Stage ID."
                    },
                    new QueryArgument<ListGraphType<GuidGraphType>>
                    {
                        Name = "costumeIds",
                        Description = "List of costume id for equip."
                    },
                    new QueryArgument<ListGraphType<GuidGraphType>>
                    {
                        Name = "equipmentIds",
                        Description = "List of equipment id for equip."
                    },
                    new QueryArgument<ListGraphType<GuidGraphType>>
                    {
                        Name = "consumableIds",
                        Description = "List of consumable id for use."
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "playCount",
                        Description = "Number of times to play the level, defaults to 1.",
                        DefaultValue = 1
                    },
                    new QueryArgument<IntGraphType>
                    {
                        Name = "buffId",
                        Description = "id of the buff to use, must already be available.",
                        DefaultValue = 1
                    }
                ),
                resolve: context =>
                {
                    Address avatarAddress = context.GetArgument<Address>("avatarAddress");
                    int worldId = context.GetArgument<int>("worldId");
                    int stageId = context.GetArgument<int>("stageId");
                    Address rankingMapAddress = context.GetArgument<Address>("rankingMapAddress");
                    List<Guid> costumeIds = context.GetArgument<List<Guid>>("costumeIds") ?? new List<Guid>();
                    List<Guid> equipmentIds = context.GetArgument<List<Guid>>("equipmentIds") ?? new List<Guid>();
                    List<Guid> consumableIds = context.GetArgument<List<Guid>>("consumableIds") ?? new List<Guid>();
                    var playCount = context.GetArgument<int>("playCount");
                    var buffId = context.GetArgument<int?>("buffId");

                    NCAction action = new HackAndSlash
                    {
                        AvatarAddress = avatarAddress,
                        WorldId = worldId,
                        StageId = stageId,
                        Costumes = costumeIds,
                        Equipments = equipmentIds,
                        Foods = consumableIds,
                        PlayCount = playCount,
                        StageBuffId = buffId,
                    };
                    return Codec.Encode(action.PlainValue);
                });
            Field<ByteStringType>(
                name: "transferAsset",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Description = "Address of sender.",
                        Name = "sender",
                    },
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Description = "Address of recipient.",
                        Name = "recipient",
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Description = "A string value to be transferred.",
                        Name = "amount",
                    },
                    new QueryArgument<NonNullGraphType<CurrencyEnumType>>
                    {
                        Description = "A currency type to be transferred.",
                        Name = "currency",
                    },
                    new QueryArgument<StringGraphType>
                    {
                        Description = "A 80-max length string to note.",
                        Name = "memo",
                    }
                ),
                resolve: context =>
                {
                    var sender = context.GetArgument<Address>("sender");
                    var recipient = context.GetArgument<Address>("recipient");
                    Currency currency = context.GetArgument<CurrencyEnum>("currency") switch
                    {
                        CurrencyEnum.NCG => new GoldCurrencyState(
                            (Dictionary)standaloneContext.BlockChain!.GetState(GoldCurrencyState.Address)
                        ).Currency,
                        CurrencyEnum.CRYSTAL => CrystalCalculator.CRYSTAL,
                        _ => throw new ExecutionError("Unsupported Currency type.")
                    };
                    var amount = FungibleAssetValue.Parse(currency, context.GetArgument<string>("amount"));
                    var memo = context.GetArgument<string?>("memo");
                    NCAction action = new TransferAsset(sender, recipient, amount, memo);
                    return Codec.Encode(action.PlainValue);
                });
            Field<NonNullGraphType<ByteStringType>>(
                name: "patchTableSheet",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Description = "name of table sheet.",
                        Name = "tableName",
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Description = "table data.",
                        Name = "tableCsv",
                    }
                ),
                resolve: context =>
                {
                    var tableName = context.GetArgument<string>("tableName");
                    var tableCsv = context.GetArgument<string>("tableCsv");
                    try
                    {
                        var _ = typeof(ISheet).Assembly
                            .GetTypes()
                            .First(type => type.Namespace is { } @namespace &&
                                           @namespace.StartsWith($"{nameof(Nekoyume)}.{nameof(Nekoyume.TableData)}") &&
                                           !type.IsAbstract &&
                                           typeof(ISheet).IsAssignableFrom(type) &&
                                           type.Name == tableName);
                    }
                    catch (Exception)
                    {
                        throw new ExecutionError("Invalid tableName.");
                    }

                    // TODO validate row data.
                    NCAction action = new PatchTableSheet
                    {
                        TableName = tableName,
                        TableCsv = tableCsv
                    };
                    return Codec.Encode(action.PlainValue);
                }
            );
            Field<NonNullGraphType<ByteStringType>>(
                name: "raid",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Description = "address of avatar state.",
                        Name = "avatarAddress",
                    },
                    new QueryArgument<ListGraphType<GuidGraphType>>
                    {
                        Description = "list of equipment id.",
                        DefaultValue = new List<Guid>(),
                        Name = "equipmentIds",
                    },
                    new QueryArgument<ListGraphType<GuidGraphType>>
                    {
                        Description = "list of costume id.",
                        DefaultValue = new List<Guid>(),
                        Name = "costumeIds",
                    },
                    new QueryArgument<ListGraphType<GuidGraphType>>
                    {
                        Description = "list of food id.",
                        DefaultValue = new List<Guid>(),
                        Name = "foodIds",
                    },
                    new QueryArgument<BooleanGraphType>
                    {
                        Description = "refill ticket by NCG.",
                        DefaultValue = false,
                        Name = "payNcg",
                    }
                ),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var equipmentIds = context.GetArgument<List<Guid>>("equipmentIds");
                    var costumeIds = context.GetArgument<List<Guid>>("costumeIds");
                    var foodIds = context.GetArgument<List<Guid>>("foodIds");
                    var payNcg = context.GetArgument<bool>("payNcg");

                    NCAction action = new Raid
                    {
                        AvatarAddress = avatarAddress,
                        EquipmentIds = equipmentIds,
                        CostumeIds = costumeIds,
                        FoodIds = foodIds,
                        PayNcg = payNcg
                    };
                    return Codec.Encode(action.PlainValue);
                }
            );
            Field<NonNullGraphType<ByteStringType>>(
                "claimRaidReward",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "address of avatar state to receive reward."
                    }
                ),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");

                    NCAction action = new ClaimRaidReward(avatarAddress);
                    return Codec.Encode(action.PlainValue);
                }
            );
            Field<NonNullGraphType<ByteStringType>>(
                "claimWorldBossKillReward",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "address of avatar state to receive reward."
                    }
                ),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");

                    NCAction action = new ClaimWordBossKillReward
                    {
                        AvatarAddress = avatarAddress,
                    };
                    return Codec.Encode(action.PlainValue);
                }
            );
            Field<NonNullGraphType<ByteStringType>>(
                "prepareRewardAssets",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "rewardPoolAddress",
                        Description = "address of reward pool for charge reward."
                    },
                    new QueryArgument<NonNullGraphType<ListGraphType<NonNullGraphType<FungibleAssetValueInputType>>>>
                    {
                        Name = "assets",
                        Description = "list of FungibleAssetValue for charge reward."
                    }
                ),
                resolve: context =>
                {
                    var assets = context.GetArgument<List<FungibleAssetValue>>("assets");
                    var rewardPoolAddress = context.GetArgument<Address>("rewardPoolAddress");

                    NCAction action = new PrepareRewardAssets
                    {
                        Assets = assets,
                        RewardPoolAddress = rewardPoolAddress,
                    };
                    return Codec.Encode(action.PlainValue);
                }
            );
        }
    }
}
