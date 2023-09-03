#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Lib9c.Abstractions;
using Libplanet.Action;
using Libplanet.Action.State;
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
using NineChronicles.Headless.GraphTypes.States.Models.Item.Enum;
using NineChronicles.Headless.GraphTypes.States.Models.Table;
using Nekoyume.TableData.Pet;
using DecimalMath;
using Nekoyume.Helper;
using Nekoyume.Model.Stat;
using Humanizer;

namespace NineChronicles.Headless.GraphTypes
{
    public class SimultionQuery : ObjectGraphType<StateContext>
    {
        public SimultionQuery()
        {
            Name = "SimultionQuery";
            
            Field<StageResultInfoStateType>(
                name: "stagePercentageCalculator",
                description: "State for championShip arena.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Avatar address."
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "stageId",
                        Description = "ID of stage"
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "worldId",
                        Description = "ID of World"
                    },
                    new QueryArgument<ListGraphType<GuidGraphType>>
                    {
                        Description = "list of food id.",
                        DefaultValue = new List<Guid>(),
                        Name = "foodIds",
                    }
                ),
                resolve: context =>
                {
                    Address myAvatarAddress = context.GetArgument<Address>("avatarAddress");
                    int StageId = context.GetArgument<int>("stageId");
                    int WorldId = context.GetArgument<int>("worldId");
                    var Foods = context.GetArgument<List<Guid>>("foodIds");
                    int? StageBuffId = 1;
                    //sheets
                    var sheets = context.Source.GetSheets(
                    containQuestSheet: true,
                    containSimulatorSheets: true,
                    sheetTypes: new[]
                    {
                        typeof(WorldSheet),
                        typeof(StageSheet),
                        typeof(StageWaveSheet),
                        typeof(EnemySkillSheet),
                        typeof(CostumeStatSheet),
                        typeof(SkillSheet),
                        typeof(QuestRewardSheet),
                        typeof(QuestItemRewardSheet),
                        typeof(EquipmentItemRecipeSheet),
                        typeof(WorldUnlockSheet),
                        typeof(MaterialItemSheet),
                        typeof(ItemRequirementSheet),
                        typeof(EquipmentItemRecipeSheet),
                        typeof(EquipmentItemSubRecipeSheetV2),
                        typeof(EquipmentItemOptionSheet),
                        typeof(CrystalStageBuffGachaSheet),
                        typeof(CrystalRandomBuffSheet),
                        typeof(StakeActionPointCoefficientSheet),
                        typeof(RuneListSheet),
                    });
                    var materialItemSheet = sheets.GetSheet<MaterialItemSheet>();
                    var characterSheet = sheets.GetSheet<CharacterSheet>();
                    if (!sheets.GetSheet<StageSheet>().TryGetValue(StageId, out var stageRow))
                    {
                        throw new SheetRowNotFoundException(nameof(StageSheet), StageId);
                    }

                    //MyAvatar  
                    var myAvatar = context.Source.GetAvatarStateV2(myAvatarAddress);

                    if (!characterSheet.TryGetValue(myAvatar.characterId, out var characterRow))
                    {
                        throw new SheetRowNotFoundException("CharacterSheet", myAvatar.characterId);
                    }              
                    var myArenaAvatarStateAdr = ArenaAvatarState.DeriveAddress(myAvatarAddress);
                    var myArenaAvatarState = context.Source.GetArenaAvatarState(myArenaAvatarStateAdr, myAvatar);

                    var myAvatarEquipments = myAvatar.inventory.Equipments;
                    var myAvatarCostumes = myAvatar.inventory.Costumes;

                    List<Guid> myArenaEquipementList = myAvatarEquipments.Where(f=>f.equipped).Select(n => n.ItemId).ToList();
                    List<Guid> myArenaCostumeList = myAvatarCostumes.Where(f=>f.equipped).Select(n => n.ItemId).ToList();

                    var myRuneSlotStateAddress = RuneSlotState.DeriveAddress(myAvatarAddress, BattleType.Adventure);
                    var myRuneSlotState = context.Source.TryGetState(myRuneSlotStateAddress, out List myRawRuneSlotState)
                        ? new RuneSlotState(myRawRuneSlotState)
                        : new RuneSlotState(BattleType.Arena);

                    var myRuneStates = new List<RuneState>();
                    var myRuneSlotInfos = myRuneSlotState.GetEquippedRuneSlotInfos();
                    foreach (var address in myRuneSlotInfos.Select(info => RuneState.DeriveAddress(myAvatarAddress, info.RuneId)))
                    {
                        if (context.Source.TryGetState(address, out List rawRuneState))
                        {
                            myRuneStates.Add(new RuneState(rawRuneState));
                        }
                    }

                    //Crystal Buffs//
                    var skillStateAddress = Addresses.GetSkillStateAddressFromAvatarAddress(myAvatarAddress);
                    var isNotClearedStage = !myAvatar.worldInformation.IsStageCleared(StageId);
                    var skillsOnWaveStart = new List<Skill>();
                    CrystalRandomSkillState? skillState = null;
                    skillState = context.Source.TryGetState<List>(skillStateAddress, out var serialized)
                        ? new CrystalRandomSkillState(skillStateAddress, serialized)
                        : new CrystalRandomSkillState(skillStateAddress, StageId);
                    if (skillState.SkillIds.Any())
                    {
                        var crystalRandomBuffSheet = sheets.GetSheet<CrystalRandomBuffSheet>();
                        var skillSheet = sheets.GetSheet<SkillSheet>();
                        int selectedId;
                        if (StageBuffId.HasValue && skillState.SkillIds.Contains(StageBuffId.Value))
                        {
                            selectedId = StageBuffId.Value;
                        }
                        else
                        {
                            selectedId = skillState.GetHighestRankSkill(crystalRandomBuffSheet);
                        }
                        var skill = CrystalRandomSkillState.GetSkill(
                            selectedId,
                            crystalRandomBuffSheet,
                            skillSheet);
                        skillsOnWaveStart.Add(skill);
                    }
                    Random rnd  =new Random();

                    var simulatorSheets = sheets.GetSimulatorSheets();

                    int Wave0 = 0;
                    int Wave1 = 0;
                    int Wave2 = 0;
                    int Wave3 = 0;

                    for (var i = 0; i < 50; i++)
                    {
                        LocalRandom random = new LocalRandom(rnd.Next());
                        var simulator = new StageSimulator(
                            random,
                            myAvatar,
                            i == 0 ? Foods : new List<Guid>(),
                            myRuneStates,
                            i == 0 ? skillsOnWaveStart : new List<Skill>(),
                            WorldId,
                            StageId,
                            stageRow,
                            sheets.GetSheet<StageWaveSheet>()[StageId],
                            myAvatar.worldInformation.IsStageCleared(StageId),
                            StageRewardExpHelper.GetExp(myAvatar.level, StageId),
                            simulatorSheets,
                            sheets.GetSheet<EnemySkillSheet>(),
                            sheets.GetSheet<CostumeStatSheet>(),
                            StageSimulator.GetWaveRewards(random, stageRow, materialItemSheet));

                        simulator.Simulate();

                        switch(simulator.Log.clearedWaveNumber)
                        {
                            case 1:
                                Wave1++;
                                break;
                            case 2:
                                Wave2++;
                                break;
                            case 3:
                                Wave3++;
                                break;
                            default:
                                Wave0++;
                                break;
                        }
                    }

                    var StageResult = new StageResultInfo
                    {
                        AvatarAddress = myAvatarAddress,
                        Stage = StageId,
                        Wave0 = Math.Round(((double)Wave0 / 50) * 100, 2),
                        Wave1 = Math.Round(((double)Wave1 / 50) * 100, 2),
                        Wave2 = Math.Round(((double)Wave2 / 50) * 100, 2),
                        Wave3 = Math.Round(((double)Wave3 / 50) * 100, 2)
                    };
                    return StageResult;
                });
            Field<NonNullGraphType<ArenaSimulationStateType>>(
                name: "arenaPercentageCalculator",
                description: "State for championShip arena.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Avatar address."
                    },
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "enemyAvatarAddress",
                        Description = "Enemy Avatar address."
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "simulationCount",
                        Description = "Amount of simulations, between 1 and 1000"
                    }
                ),
                resolve: context =>
                {
                    Address myAvatarAddress = context.GetArgument<Address>("avatarAddress");
                    Address enemyAvatarAddress = context.GetArgument<Address>("enemyAvatarAddress");
                    int simulationCount = context.GetArgument<int>("simulationCount");

                    var sheets = context.Source.GetSheets(sheetTypes: new[]
                    {
                        typeof(ArenaSheet),
                        typeof(CostumeStatSheet),
                        typeof(ItemRequirementSheet),
                        typeof(EquipmentItemRecipeSheet),
                        typeof(EquipmentItemSubRecipeSheetV2),
                        typeof(EquipmentItemOptionSheet),
                        typeof(RuneListSheet),
                        typeof(MaterialItemSheet),
                        typeof(SkillSheet),
                        typeof(SkillBuffSheet),
                        typeof(StatBuffSheet),
                        typeof(SkillActionBuffSheet),
                        typeof(ActionBuffSheet),
                        typeof(CharacterSheet),
                        typeof(CharacterLevelSheet),
                        typeof(EquipmentItemSetEffectSheet),
                        typeof(WeeklyArenaRewardSheet),
                        typeof(RuneOptionSheet),
                    });

                    if(simulationCount < 1 || simulationCount > 1000)
                    {
                        throw new Exception("arenaPercentageCalculator - Invalid simulationCount");
                    }

                    var myAvatar = context.Source.GetAvatarStateV2(myAvatarAddress);
                    var enemyAvatar = context.Source.GetAvatarStateV2(enemyAvatarAddress);

                    //sheets
                    var arenaSheets = sheets.GetArenaSimulatorSheets();
                    var characterSheet = sheets.GetSheet<CharacterSheet>();

                    if (!characterSheet.TryGetValue(myAvatar.characterId, out var characterRow) || !characterSheet.TryGetValue(enemyAvatar.characterId, out var characterRow2))
                    {
                        throw new SheetRowNotFoundException("CharacterSheet", myAvatar.characterId);
                    }

                    //MyAvatar                
                    var myArenaAvatarStateAdr = ArenaAvatarState.DeriveAddress(myAvatarAddress);
                    var myArenaAvatarState = context.Source.GetArenaAvatarState(myArenaAvatarStateAdr, myAvatar);
                    var myAvatarEquipments = myAvatar.inventory.Equipments;
                    var myAvatarCostumes = myAvatar.inventory.Costumes;
                    List<Guid> myArenaEquipementList = myAvatarEquipments.Where(f=>myArenaAvatarState.Equipments.Contains(f.ItemId)).Select(n => n.ItemId).ToList();
                    List<Guid> myArenaCostumeList = myAvatarCostumes.Where(f=>myArenaAvatarState.Costumes.Contains(f.ItemId)).Select(n => n.ItemId).ToList();

                    var myRuneSlotStateAddress = RuneSlotState.DeriveAddress(myAvatarAddress, BattleType.Arena);
                    var myRuneSlotState = context.Source.TryGetState(myRuneSlotStateAddress, out List myRawRuneSlotState)
                        ? new RuneSlotState(myRawRuneSlotState)
                        : new RuneSlotState(BattleType.Arena);

                    var myRuneStates = new List<RuneState>();
                    var myRuneSlotInfos = myRuneSlotState.GetEquippedRuneSlotInfos();
                    foreach (var address in myRuneSlotInfos.Select(info => RuneState.DeriveAddress(myAvatarAddress, info.RuneId)))
                    {
                        if (context.Source.TryGetState(address, out List rawRuneState))
                        {
                            myRuneStates.Add(new RuneState(rawRuneState));
                        }
                    }

                    //Enemy
                    var enemyArenaAvatarStateAdr = ArenaAvatarState.DeriveAddress(enemyAvatarAddress);
                    var enemyArenaAvatarState = context.Source.GetArenaAvatarState(enemyArenaAvatarStateAdr, enemyAvatar);
                    var enemyAvatarEquipments = enemyAvatar.inventory.Equipments;
                    var enemyAvatarCostumes = enemyAvatar.inventory.Costumes;
                    List<Guid> enemyArenaEquipementList = enemyAvatarEquipments.Where(f=>enemyArenaAvatarState.Equipments.Contains(f.ItemId)).Select(n => n.ItemId).ToList();
                    List<Guid> enemyArenaCostumeList = enemyAvatarCostumes.Where(f=>enemyArenaAvatarState.Costumes.Contains(f.ItemId)).Select(n => n.ItemId).ToList();

                    var enemyRuneSlotStateAddress = RuneSlotState.DeriveAddress(enemyAvatarAddress, BattleType.Arena);
                    var enemyRuneSlotState = context.Source.TryGetState(enemyRuneSlotStateAddress, out List enemyRawRuneSlotState)
                        ? new RuneSlotState(enemyRawRuneSlotState)
                        : new RuneSlotState(BattleType.Arena);

                    var enemyRuneStates = new List<RuneState>();
                    var enemyRuneSlotInfos = enemyRuneSlotState.GetEquippedRuneSlotInfos();
                    foreach (var address in enemyRuneSlotInfos.Select(info => RuneState.DeriveAddress(enemyAvatarAddress, info.RuneId)))
                    {
                        if (context.Source.TryGetState(address, out List rawRuneState))
                        {
                            enemyRuneStates.Add(new RuneState(rawRuneState));
                        }
                    }

                    var myArenaPlayerDigest = new ArenaPlayerDigest(
                        myAvatar,
                        myArenaEquipementList,
                        myArenaCostumeList,
                        myRuneStates);

                    var enemyArenaPlayerDigest = new ArenaPlayerDigest(
                        enemyAvatar,
                        enemyArenaEquipementList,
                        enemyArenaCostumeList,
                        enemyRuneStates);

                    Random rnd  =new Random();          

                    int win = 0;
                    int loss = 0;

                    List<ArenaSimulationResult> arenaResultsList = new List<ArenaSimulationResult>();
                    ArenaSimulationState arenaSimulationState = new ArenaSimulationState();
                    arenaSimulationState.blockIndex = context.Source.BlockIndex;
                    
                    for (var i = 0; i < simulationCount; i++)
                    {
                        ArenaSimulationResult arenaResult = new ArenaSimulationResult();
                        arenaResult.seed = rnd.Next();
                        LocalRandom iRandom = new LocalRandom(arenaResult.seed);
                        var simulator = new ArenaSimulator(iRandom);
                        var log = simulator.Simulate(
                            myArenaPlayerDigest,
                            enemyArenaPlayerDigest,
                            arenaSheets);
                        if(log.Result.ToString() == "Win")
                        {
                            arenaResult.win = true;
                            win++;
                        }
                        else
                        {
                            loss++;
                            arenaResult.win = false;
                        }
                        arenaResultsList.Add(arenaResult);
                    }
                    arenaSimulationState.winPercentage = Math.Round(((decimal)win / simulationCount) * 100m, 2);
                    arenaSimulationState.result = arenaResultsList;
                    return arenaSimulationState;
                });
            Field<NonNullGraphType<CombinationSimulationStateType>>(
               name: "combinationSimulator",
               description: "State for championShip arena.",
               arguments: new QueryArguments(
                   new QueryArgument<NonNullGraphType<AddressType>>
                   {
                       Name = "agentAdress",
                       Description = "Avatar address."
                   },
                   new QueryArgument<NonNullGraphType<AddressType>>
                   {
                       Name = "avatarAddress",
                       Description = "Avatar address."
                   },
                   new QueryArgument<NonNullGraphType<StringGraphType>>
                   {
                       Name = "recipeId",
                       Description = "Enemy Avatar address."
                   },
                   new QueryArgument<NonNullGraphType<StringGraphType>>
                   {
                       Name = "subRecipeId",
                       Description = "Enemy Avatar address."
                   },
                   new QueryArgument<NonNullGraphType<IntGraphType>>
                   {
                       Name = "simulationCount",
                       Description = "Amount of simulations, between 1 and 1000"
                   }
               ),
               resolve: context =>
               {
                   Address agentAdress = context.GetArgument<Address>("agentAdress");
                   Address avatarAddress = context.GetArgument<Address>("avatarAddress");
                   int recipeId = context.GetArgument<int>("recipeId");
                   int subRecipeId = context.GetArgument<int>("subRecipeId");
                   int simulationCount = context.GetArgument<int>("simulationCount");

                   var states = context.Source;


                   Dictionary<Type, (Address, ISheet)> sheets = states.GetSheets(sheetTypes: new[]
                    {
                        typeof(EquipmentItemRecipeSheet),
                        typeof(EquipmentItemSheet),
                        typeof(MaterialItemSheet),
                        typeof(EquipmentItemSubRecipeSheetV2),
                        typeof(EquipmentItemOptionSheet),
                        typeof(SkillSheet),
                        typeof(CrystalMaterialCostSheet),
                        typeof(CrystalFluctuationSheet),
                        typeof(CrystalHammerPointSheet),
                        typeof(ConsumableItemRecipeSheet),
                    });

                   if (!states.TryGetAgentAvatarStatesV2(agentAdress, avatarAddress, out var agentState,
                        out var avatarState, out _))
                   {
                       throw new Exception("arenaPercentageCalculator - Invalid simulationCount");
                   }

                   // Validate RecipeId
                   var equipmentItemRecipeSheet = sheets.GetSheet<EquipmentItemRecipeSheet>();
                   if (!equipmentItemRecipeSheet.TryGetValue(recipeId, out var recipeRow))
                   {
                       throw new Exception("arenaPercentageCalculator - Invalid simulationCount");
                   }
                   // ~Validate RecipeId

                   // Validate Recipe ResultEquipmentId
                   var equipmentItemSheet = sheets.GetSheet<EquipmentItemSheet>();
                   if (!equipmentItemSheet.TryGetValue(recipeRow.ResultEquipmentId, out var equipmentRow))
                   {
                       throw new Exception("arenaPercentageCalculator - Invalid simulationCount");
                   }
                   // ~Validate Recipe ResultEquipmentId

                   // Validate Recipe Material
                   var materialItemSheet = sheets.GetSheet<MaterialItemSheet>();
                   if (!materialItemSheet.TryGetValue(recipeRow.MaterialId, out var materialRow))
                   {
                       throw new Exception("arenaPercentageCalculator - Invalid simulationCount");
                   }

                   var requiredFungibleItems = new Dictionary<int, int>();

                   if (requiredFungibleItems.ContainsKey(materialRow.Id))
                   {
                       requiredFungibleItems[materialRow.Id] += recipeRow.MaterialCount;
                   }
                   else
                   {
                       requiredFungibleItems[materialRow.Id] = recipeRow.MaterialCount;
                   }

                   // Validate Recipe Unlocked.
                   if (equipmentItemRecipeSheet[recipeId].CRYSTAL != 0)
                   {
                       var unlockedRecipeIdsAddress = avatarAddress.Derive("recipe_ids");
                       if (!states.TryGetState(unlockedRecipeIdsAddress, out List rawIds))
                       {
                           throw new FailedLoadStateException("can't find UnlockedRecipeList.");
                       }

                       var unlockedIds = rawIds.ToList(StateExtensions.ToInteger);
                       if (!unlockedIds.Contains(recipeId))
                       {
                           throw new InvalidRecipeIdException($"unlock {recipeId} first.");
                       }

                       if (!avatarState.worldInformation.IsStageCleared(recipeRow.UnlockStage))
                       {
                           avatarState.worldInformation.TryGetLastClearedStageId(out var current);
                           throw new FailedLoadStateException("can't find UnlockedRecipeList.");
                       }
                   }
                   // ~Validate Recipe Unlocked

                   // Validate SubRecipeId
                   EquipmentItemSubRecipeSheetV2.Row? subRecipeRow = null;
                   if (subRecipeId > 0)
                   {
                       if (!recipeRow.SubRecipeIds.Contains(subRecipeId))
                       {
                           throw new Exception("arenaPercentageCalculator - Invalid simulationCount");
                       }

                       var equipmentItemSubRecipeSheetV2 = sheets.GetSheet<EquipmentItemSubRecipeSheetV2>();
                       if (!equipmentItemSubRecipeSheetV2.TryGetValue(subRecipeId, out subRecipeRow))
                       {
                           throw new Exception("arenaPercentageCalculator - Invalid simulationCount");
                       }

                       // Validate SubRecipe Material
                       for (var i = subRecipeRow.Materials.Count; i > 0; i--)
                       {
                           var materialInfo = subRecipeRow.Materials[i - 1];
                           if (!materialItemSheet.TryGetValue(materialInfo.Id, out materialRow))
                           {
                               throw new Exception("arenaPercentageCalculator - Invalid simulationCount");
                           }

                           if (requiredFungibleItems.ContainsKey(materialRow.Id))
                           {
                               requiredFungibleItems[materialRow.Id] += materialInfo.Count;
                           }
                           else
                           {
                               requiredFungibleItems[materialRow.Id] = materialInfo.Count;
                           }
                       }
                   }

                   var existHammerPointSheet =
                       sheets.TryGetSheet(out CrystalHammerPointSheet hammerPointSheet);
                   var hammerPointAddress =
                       Addresses.GetHammerPointStateAddress(avatarAddress, recipeId);
                   var hammerPointState = new HammerPointState(hammerPointAddress, recipeId);
                   CrystalHammerPointSheet.Row? hammerPointRow = null;
                   if (existHammerPointSheet)
                   {
                       if (states.TryGetState(hammerPointAddress, out List serialized))
                       {
                           hammerPointState =
                               new HammerPointState(hammerPointAddress, serialized);
                       }

                       // Validate HammerPointSheet by recipeId
                       if (!hammerPointSheet.TryGetValue(recipeId, out hammerPointRow))
                       {
                           throw new Exception("arenaPercentageCalculator - Invalid simulationCount");
                       }
                   }
                   long endBlockIndex = 99999999999;
                   //var isMimisbrunnrSubRecipe = subRecipeRow?.IsMimisbrunnrSubRecipe ??
                   //    subRecipeId.HasValue && recipeRow.SubRecipeIds[2] == subRecipeId.Value;
                   var petOptionSheet = states.GetSheet<PetOptionSheet>();
                   //bool useHammerPoint = false;
                   //if (useHammerPoint)
                   //{
                   //    if (!existHammerPointSheet)
                   //    {
                   //        throw new FailedLoadSheetException(typeof(CrystalHammerPointSheet));
                   //    }

                   //    states = UseAssetsBySuperCraft(
                   //        states,
                   //        context,
                   //        hammerPointRow,
                   //        hammerPointState);
                   //}
                   //else
                   //{
                   //    states = UseAssetsByNormalCombination(
                   //        states,
                   //        context,
                   //        avatarState,
                   //        hammerPointState,
                   //        petState,
                   //        sheets,
                   //        materialItemSheet,
                   //        hammerPointSheet,
                   //        petOptionSheet,
                   //        recipeRow,
                   //        subRecipeRow,
                   //        requiredFungibleItems,
                   //        addressesHex);
                   //}
                   PetState? petState = null;

                   List<CombinationSimulationResult> combinationResultsList = new List<CombinationSimulationResult>();
                   CombinationSimulationState combinationSimulationState = new CombinationSimulationState();
                   combinationSimulationState.blockIndex = context.Source.BlockIndex;

                   int oneStar = 0;
                   int twoStar = 0;
                   int threeStar = 0;
                   int fourStar = 0;
                   int spell = 0;
                
                   for(int i = 0; i < simulationCount; i++)
                   {
                       CombinationSimulationResult combinationResult = new CombinationSimulationResult();
                       // Create Equipment
                       var equipment = (Equipment)ItemFactory.CreateItemUsable(
                            equipmentRow,
                            Guid.NewGuid(),
                            endBlockIndex,
                            madeWithMimisbrunnrRecipe: false
                       );
                       Random random = new Random();
                       int seed = random.Next(1, 1000000);
                       LocalRandom random1 = new LocalRandom(seed);

                       if (!(subRecipeRow is null))
                       {
                           AddAndUnlockOption(
                               agentState,
                               petState,
                               equipment,
                               random1,
                               subRecipeRow,
                               sheets.GetSheet<EquipmentItemOptionSheet>(),
                               petOptionSheet,
                               sheets.GetSheet<SkillSheet>()
                           );
                           endBlockIndex = 99999999;
                       }

                       if(equipment.Skills.Any())
                       {
                          spell++;
                          combinationResult.spellChance = equipment.Skills[0].Chance;
                          combinationResult.spellPower = equipment.Skills[0].Power;
                       }

                       switch(equipment.optionCountFromCombination)
                       {
                           case 1:
                              oneStar++;
                              break;

                           case 2:
                              twoStar++;
                              break;

                           case 3:
                              threeStar++;
                              break;

                           case 4:
                              fourStar++;
                              combinationResult.spellChance = equipment.Skills[0].Chance;
                              combinationResult.spellPower = equipment.Skills[0].Power;
                              break;
                       }

                       combinationResult.seed = seed;
                       combinationResult.starCount = equipment.optionCountFromCombination;
                       combinationResultsList.Add(combinationResult);
                   }
                   combinationSimulationState.oneStarPercentage = Math.Round(((decimal)oneStar / simulationCount) * 100m, 2);
                   combinationSimulationState.twoStarPercentage = Math.Round(((decimal)twoStar / simulationCount) * 100m, 2);
                   combinationSimulationState.threeStarPercentage = Math.Round(((decimal)threeStar / simulationCount) * 100m, 2);
                   combinationSimulationState.fourStarPercentage = Math.Round(((decimal)fourStar / simulationCount) * 100m, 2);
                   combinationSimulationState.spellPercentage = Math.Round(((decimal)spell / simulationCount) * 100m, 2);
                   combinationSimulationState.result = combinationResultsList;
                   return combinationSimulationState;
               });
        }

        public static void AddAndUnlockOption(
             AgentState agentState,
             PetState? petState,
             Equipment equipment,
             IRandom random,
             EquipmentItemSubRecipeSheetV2.Row subRecipe,
             EquipmentItemOptionSheet optionSheet,
             PetOptionSheet petOptionSheet,
             SkillSheet skillSheet
         )
        {
            foreach (var optionInfo in subRecipe.Options
                .OrderByDescending(e => e.Ratio)
                .ThenBy(e => e.RequiredBlockIndex)
                .ThenBy(e => e.Id))
            {
                if (!optionSheet.TryGetValue(optionInfo.Id, out var optionRow))
                {
                    continue;
                }

                var value = random.Next(1, GameConfig.MaximumProbability + 1);
                var ratio = optionInfo.Ratio;

                // Apply pet bonus if possible
                if (!(petState is null))
                {
                    ratio = PetHelper.GetBonusOptionProbability(
                        ratio,
                        petState,
                        petOptionSheet);
                }

                if (value > ratio)
                {
                    continue;
                }

                if (optionRow.StatType != StatType.NONE)
                {
                    var stat = CombinationEquipment5.GetStat(optionRow, random);
                    equipment.StatsMap.AddStatAdditionalValue(stat.StatType, stat.BaseValue);
                    equipment.Update(equipment.RequiredBlockIndex + optionInfo.RequiredBlockIndex);
                    equipment.optionCountFromCombination++;
                    agentState.unlockedOptions.Add(optionRow.Id);
                }
                else
                {
                    var skill = CombinationEquipment.GetSkill(optionRow, skillSheet, random);
                    if (!(skill is null))
                    {
                        equipment.Skills.Add(skill);
                        equipment.Update(equipment.RequiredBlockIndex + optionInfo.RequiredBlockIndex);
                        equipment.optionCountFromCombination++;
                        agentState.unlockedOptions.Add(optionRow.Id);
                    }
                }
            }
        }
    }
}
