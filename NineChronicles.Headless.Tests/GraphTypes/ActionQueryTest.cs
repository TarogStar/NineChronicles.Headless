using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using GraphQL.Execution;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Crypto;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Helper;
using Nekoyume.Model;
using Nekoyume.Model.EnumType;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class ActionQueryTest
    {
        private readonly Codec _codec;
        private readonly StandaloneContext _standaloneContext;
        private readonly PrivateKey _activationCodeSeed;
        private readonly ActivationKey _activationKey;
        private readonly byte[] _nonce;

        public ActionQueryTest()
        {
            _codec = new Codec();
            _activationCodeSeed = new PrivateKey();
            _nonce = new byte[16];
            new Random().NextBytes(_nonce);
            (_activationKey, PendingActivationState pending) = ActivationKey.Create(_activationCodeSeed, _nonce);
            var minerPrivateKey = new PrivateKey();
            var initializeStates = new InitializeStates(
                rankingState: new RankingState0(),
                shopState: new ShopState(),
                gameConfigState: new GameConfigState(),
                redeemCodeState: new RedeemCodeState(Bencodex.Types.Dictionary.Empty
                    .Add("address", RedeemCodeState.Address.Serialize())
                    .Add("map", Bencodex.Types.Dictionary.Empty)
                ),
                adminAddressState: new AdminState(new PrivateKey().ToAddress(), 1500000),
                activatedAccountsState: new ActivatedAccountsState(),
#pragma warning disable CS0618
                // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
                goldCurrencyState:
                new GoldCurrencyState(Currency.Legacy("NCG", 2, minerPrivateKey.ToAddress())),
#pragma warning restore CS0618
                goldDistributions: Array.Empty<GoldDistribution>(),
                tableSheets: new Dictionary<string, string>(),
                pendingActivationStates: new[] { pending }
            );
            _standaloneContext = CreateStandaloneContext(initializeStates, minerPrivateKey);
        }

        [Theory]
        [ClassData(typeof(StakeFixture))]
        public async Task Stake(BigInteger amount)
        {
            string query = $@"
            {{
                stake(amount: {amount})
            }}";

            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            NCAction action = new Stake(amount);
            var expected = new Dictionary<string, object>()
            {
                ["stake"] = ByteUtil.Hex(_codec.Encode(action.PlainValue)),
            };
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["stake"]));
            var expectedPlainValue = _codec.Decode(ByteUtil.ParseHex((string)expected["stake"]));
            Assert.IsType<Dictionary>(plainValue);
            var dictionary = (Dictionary)plainValue;
            Assert.IsType<Stake>(DeserializeNCAction(dictionary).InnerAction);
            var actualAmount = ((Dictionary)dictionary["values"])["am"].ToBigInteger();
            var expectedAmount = ((Dictionary)((Dictionary)expectedPlainValue)["values"])["am"].ToBigInteger();
            Assert.Equal(expectedAmount, actualAmount);
        }

        [Fact]
        public async Task ClaimStakeReward()
        {
            var avatarAddress = new PrivateKey().ToAddress();
            string query = $@"
            {{
                claimStakeReward(avatarAddress: ""{avatarAddress.ToString()}"")
            }}";

            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["claimStakeReward"]));
            Assert.IsType<Dictionary>(plainValue);
            var dictionary = (Dictionary)plainValue;
            Assert.IsAssignableFrom<IClaimStakeReward>(DeserializeNCAction(dictionary).InnerAction);
        }

        [Fact]
        public async Task MigrateMonsterCollection()
        {
            var avatarAddress = new PrivateKey().ToAddress();
            string query = $@"
            {{
                migrateMonsterCollection(avatarAddress: ""{avatarAddress.ToString()}"")
            }}";

            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["migrateMonsterCollection"]));
            var dictionary = Assert.IsType<Dictionary>(plainValue);
            var action = Assert.IsType<MigrateMonsterCollection>(DeserializeNCAction(dictionary).InnerAction);
            Assert.Equal(avatarAddress, action.AvatarAddress);
        }

        private class StakeFixture : IEnumerable<object[]>
        {
            private readonly List<object[]> _data = new List<object[]>
            {
                new object[]
                {
                    new BigInteger(1),
                },
                new object[]
                {
                    new BigInteger(100),
                },
            };

            public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => _data.GetEnumerator();
        }
        [Theory]
        [InlineData("false", false)]
        [InlineData("true", true)]
        [InlineData(null, false)]
        public async Task Grinding(string chargeApValue, bool chargeAp)
        {
            var avatarAddress = new PrivateKey().ToAddress();
            var equipmentId = Guid.NewGuid();
            string queryArgs = $"avatarAddress: \"{avatarAddress.ToString()}\", equipmentIds: [{string.Format($"\"{equipmentId}\"")}]";
            if (!string.IsNullOrEmpty(chargeApValue))
            {
                queryArgs += $", chargeAp: {chargeApValue}";
            }
            string query = $@"
            {{
                grinding({queryArgs})
            }}";

            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["grinding"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<Grinding>(polymorphicAction.InnerAction);

            Assert.Equal(avatarAddress, action.AvatarAddress);
            Assert.Single(action.EquipmentIds);
            Assert.Equal(equipmentId, action.EquipmentIds.First());
            Assert.Equal(chargeAp, action.ChargeAp);
        }

        [Fact]
        public async Task UnlockEquipmentRecipe()
        {
            var avatarAddress = new PrivateKey().ToAddress();
            string query = $@"
            {{
                unlockEquipmentRecipe(avatarAddress: ""{avatarAddress.ToString()}"", recipeIds: [2, 3])
            }}";

            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["unlockEquipmentRecipe"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<UnlockEquipmentRecipe>(polymorphicAction.InnerAction);

            Assert.Equal(avatarAddress, action.AvatarAddress);
            Assert.Equal(
                new List<int>
                {
                    2,
                    3,
                },
                action.RecipeIds
            );
        }

        [Fact]
        public async Task UnlockWorld()
        {
            var avatarAddress = new PrivateKey().ToAddress();
            string query = $@"
            {{
                unlockWorld(avatarAddress: ""{avatarAddress.ToString()}"", worldIds: [2, 3])
            }}";

            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["unlockWorld"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<UnlockWorld>(polymorphicAction.InnerAction);

            Assert.Equal(avatarAddress, action.AvatarAddress);
            Assert.Equal(
                new List<int>
                {
                    2,
                    3,
                },
                action.WorldIds
            );
        }

        [Theory]
        [InlineData("NCG", true)]
        [InlineData("NCG", false)]
        [InlineData("CRYSTAL", true)]
        [InlineData("CRYSTAL", false)]
        public async Task TransferAsset(string currencyType, bool memo)
        {
            var recipient = new PrivateKey().ToAddress();
            var sender = new PrivateKey().ToAddress();
            var args = $"recipient: \"{recipient}\", sender: \"{sender}\", amount: \"17.5\", currency: {currencyType}";
            if (memo)
            {
                args += ", memo: \"memo\"";
            }
            var query = $"{{ transferAsset({args}) }}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["transferAsset"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<TransferAsset>(polymorphicAction.InnerAction);
            var rawState = _standaloneContext.BlockChain!.GetState(Addresses.GoldCurrency);
            var goldCurrencyState = new GoldCurrencyState((Dictionary)rawState);
            Currency currency = currencyType == "NCG" ? goldCurrencyState.Currency : CrystalCalculator.CRYSTAL;

            Assert.Equal(recipient, action.Recipient);
            Assert.Equal(sender, action.Sender);
            Assert.Equal(FungibleAssetValue.Parse(currency, "17.5"), action.Amount);
            if (memo)
            {
                Assert.Equal("memo", action.Memo);
            }
            else
            {
                Assert.Null(action.Memo);
            }
        }

        [Fact]
        public async Task PatchTableSheet()
        {
            var tableName = nameof(ArenaSheet);
            var csv = @"
            id,round,arena_type,start_block_index,end_block_index,required_medal_count,entrance_fee,ticket_price,additional_ticket_price
            1,1,OffSeason,1,2,0,0,5,2
            1,2,Season,3,4,0,100,50,20
            1,3,OffSeason,5,1005284,0,0,5,2
            1,4,Season,1005285,1019684,0,100,50,20
            1,5,OffSeason,1019685,1026884,0,0,5,2
            1,6,Season,1026885,1034084,0,10000,50,20
            1,7,OffSeason,1034085,1041284,0,0,5,2
            1,8,Championship,1041285,1062884,20,100000,50,20
            2,1,OffSeason,1062885,200000000,0,0,5,2
            2,2,Season,200000001,200000002,0,100,50,20
            2,3,OffSeason,200000003,200000004,0,0,5,2
            2,4,Season,200000005,200000006,0,100,50,20
            2,5,OffSeason,200000007,200000008,0,0,5,2
            2,6,Season,200000009,200000010,0,10000,50,20
            2,7,OffSeason,200000011,200000012,0,0,5,2
            2,8,Championship,200000013,200000014,20,100000,50,20
            ";
            var query = $"{{ patchTableSheet(tableName: \"{tableName}\", tableCsv: \"\"\"{csv}\"\"\") }}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["patchTableSheet"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<PatchTableSheet>(polymorphicAction.InnerAction);

            Assert.Equal(tableName, action.TableName);

            // FIXME parameterize sheet type.
            var sheet = new ArenaSheet();
            sheet.Set(action.TableCsv);
            var row = sheet.First!;
            var round = row.Round.First();

            Assert.Equal(1, row.ChampionshipId);
            Assert.Equal(1, round.Round);
            Assert.Equal(ArenaType.OffSeason, round.ArenaType);
            Assert.Equal(1, round.StartBlockIndex);
            Assert.Equal(2, round.EndBlockIndex);
            Assert.Equal(0, round.RequiredMedalCount);
            Assert.Equal(0, round.EntranceFee);
            Assert.Equal(5, round.TicketPrice);
            Assert.Equal(2, round.AdditionalTicketPrice);
        }

        [Fact]
        public async Task PatchTableSheet_Invalid_TableName()
        {
            var tableName = "Sheet";
            var csv = "id";
            var query = $"{{ patchTableSheet(tableName: \"{tableName}\", tableCsv: \"\"\"{csv}\"\"\") }}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var error = queryResult.Errors!.Single();
            Assert.Contains("Invalid tableName.", error.Message);
        }

        [Theory]
        [InlineData(true, false, false, false, false)]
        [InlineData(false, true, false, false, false)]
        [InlineData(false, false, true, false, false)]
        [InlineData(false, false, false, true, false)]
        [InlineData(false, false, false, false, true)]
        public async Task Raid(bool equipment, bool costume, bool food, bool payNcg, bool rune)
        {
            var avatarAddress = new PrivateKey().ToAddress();
            var args = $"avatarAddress: \"{avatarAddress}\"";
            var guid = Guid.NewGuid();
            if (equipment)
            {
                args += $", equipmentIds: [\"{guid}\"]";
            }

            if (costume)
            {
                args += $", costumeIds: [\"{guid}\"]";
            }

            if (food)
            {
                args += $", foodIds: [\"{guid}\"]";
            }

            if (payNcg)
            {
                args += ", payNcg: true";
            }

            if (rune)
            {
                args += ", runeSlotInfos: [{ slotIndex: 1, runeId: 2 }]";
            }

            var query = $"{{ raid({args}) }}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["raid"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<Raid>(polymorphicAction.InnerAction);

            Assert.Equal(avatarAddress, action.AvatarAddress);
            if (equipment)
            {
                var equipmentId = Assert.Single(action.EquipmentIds);
                Assert.Equal(guid, equipmentId);
            }
            else
            {
                Assert.Empty(action.EquipmentIds);
            }

            if (costume)
            {
                var costumeId = Assert.Single(action.CostumeIds);
                Assert.Equal(guid, costumeId);
            }
            else
            {
                Assert.Empty(action.CostumeIds);
            }

            if (food)
            {
                var foodId = Assert.Single(action.FoodIds);
                Assert.Equal(guid, foodId);
            }
            else
            {
                Assert.Empty(action.FoodIds);
            }

            if (rune)
            {
                var runeSlotInfo = Assert.Single(action.RuneInfos);
                Assert.Equal(1, runeSlotInfo.SlotIndex);
                Assert.Equal(2, runeSlotInfo.RuneId);
            }
            else
            {
                Assert.Empty(action.RuneInfos);
            }

            Assert.Equal(payNcg, action.PayNcg);
        }

        [Fact]
        public async Task ClaimRaidReward()
        {
            var avatarAddress = new PrivateKey().ToAddress();
            var query = $"{{ claimRaidReward(avatarAddress: \"{avatarAddress}\") }}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["claimRaidReward"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<ClaimRaidReward>(polymorphicAction.InnerAction);

            Assert.Equal(avatarAddress, action.AvatarAddress);
        }

        [Fact]
        public async Task ClaimWorldBossKillReward()
        {
            var avatarAddress = new PrivateKey().ToAddress();
            var query = $"{{ claimWorldBossKillReward(avatarAddress: \"{avatarAddress}\") }}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["claimWorldBossKillReward"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<ClaimWordBossKillReward>(polymorphicAction.InnerAction);

            Assert.Equal(avatarAddress, action.AvatarAddress);
        }

        [Theory]
        [InlineData(true, 2)]
        [InlineData(false, 1)]
        public async Task PrepareRewardAssets(bool mintersExist, int expectedCount)
        {
            var rewardPoolAddress = new PrivateKey().ToAddress();
            var assets = "{quantity: 100, decimalPlaces: 0, ticker: \"CRYSTAL\"}";
            if (mintersExist)
            {
                assets += $", {{quantity: 100, decimalPlaces: 2, ticker: \"NCG\", minters: [\"{rewardPoolAddress}\"]}}";
            }
            var query = $"{{ prepareRewardAssets(rewardPoolAddress: \"{rewardPoolAddress}\", assets: [{assets}]) }}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["prepareRewardAssets"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<PrepareRewardAssets>(polymorphicAction.InnerAction);

            Assert.Equal(rewardPoolAddress, action.RewardPoolAddress);
            Assert.Equal(expectedCount, action.Assets.Count);

            var crystal = action.Assets.First(r => r.Currency.Ticker == "CRYSTAL");
            Assert.Equal(100, crystal.MajorUnit);
            Assert.Equal(0, crystal.Currency.DecimalPlaces);
            Assert.Null(crystal.Currency.Minters);

            if (mintersExist)
            {
                var ncg = action.Assets.First(r => r.Currency.Ticker == "NCG");
                Assert.Equal(100, ncg.MajorUnit);
                Assert.Equal(2, ncg.Currency.DecimalPlaces);
                var minter = Assert.Single(ncg.Currency.Minters!);
                Assert.Equal(rewardPoolAddress, minter);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TransferAssets(bool exc)
        {
            var sender = new PrivateKey().ToAddress();
            var recipients =
                $"{{ recipient: \"{sender}\", amount: {{ quantity: 100, decimalPlaces: 18, ticker: \"CRYSTAL\" }} }}, {{ recipient: \"{sender}\", amount: {{ quantity: 100, decimalPlaces: 0, ticker: \"RUNE_FENRIR1\" }} }}";
            if (exc)
            {
                var count = 0;
                while (count < Nekoyume.Action.TransferAssets.RecipientsCapacity)
                {
                    recipients += $", {{ recipient: \"{sender}\", amount: {{ quantity: 100, decimalPlaces: 18, ticker: \"CRYSTAL\" }} }}, {{ recipient: \"{sender}\", amount: {{ quantity: 100, decimalPlaces: 0, ticker: \"RUNE_FENRIR1\" }} }}";
                    count++;
                }
            }
            var query = $"{{ transferAssets(sender: \"{sender}\", recipients: [{recipients}]) }}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);

            if (exc)
            {
                var error = Assert.Single(queryResult.Errors!);
                Assert.Contains($"recipients must be less than or equal {Nekoyume.Action.TransferAssets.RecipientsCapacity}.", error.Message);
            }
            else
            {
                Assert.Null(queryResult.Errors);
                var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
                var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["transferAssets"]));
                Assert.IsType<Dictionary>(plainValue);
                var polymorphicAction = DeserializeNCAction(plainValue);
                var action = Assert.IsType<TransferAssets>(polymorphicAction.InnerAction);

                Assert.Equal(sender, action.Sender);
                Assert.Equal(2, action.Recipients.Count);
                Assert.All(action.Recipients, recipient => Assert.Equal(sender, recipient.recipient));
                Assert.All(action.Recipients, recipient => Assert.Equal(100, recipient.amount.MajorUnit));
                Assert.All(action.Recipients, recipient => Assert.Null(recipient.amount.Currency.Minters));
                foreach (var (ticker, decimalPlaces) in new[] { ("CRYSTAL", 18), ("RUNE_FENRIR1", 0) })
                {
                    var recipient = action.Recipients.First(r => r.amount.Currency.Ticker == ticker);
                    Assert.Equal(decimalPlaces, recipient.amount.Currency.DecimalPlaces);
                }
            }
        }

        [Fact]
        public async Task ActivateAccount()
        {
            var activationCode = _activationKey.Encode();
            var signature = _activationKey.PrivateKey.Sign(_nonce);

            var query = $"{{ activateAccount(activationCode: \"{activationCode}\") }}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);

            Assert.Null(queryResult.Errors);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["activateAccount"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<ActivateAccount>(polymorphicAction.InnerAction);

            Assert.Equal(signature, action.Signature);
        }

        [Theory]
        [InlineData(-1, "ab", null, null, null, null, false)]
        [InlineData(0, "ab", null, null, null, null, true)]
        [InlineData(2, "ab", null, null, null, null, true)]
        [InlineData(3, "ab", null, null, null, null, false)]
        [InlineData(1, "", null, null, null, null, false)]
        [InlineData(1, "a", null, null, null, null, false)]
        [InlineData(1, "ab", null, null, null, null, true)]
        [InlineData(1, "12345678901234567890", null, null, null, null, true)]
        [InlineData(1, "123456789012345678901", null, null, null, null, false)]
        [InlineData(1, "ab", 1, null, null, null, true)]
        [InlineData(1, "ab", null, 1, null, null, true)]
        [InlineData(1, "ab", null, null, 1, null, true)]
        [InlineData(1, "ab", null, null, null, 1, true)]
        [InlineData(1, "ab", 1, 1, 1, 1, true)]
        public async Task CreateAvatar(
            int index,
            string name,
            int? hair,
            int? lens,
            int? ear,
            int? tail,
            bool errorsShouldBeNull)
        {
            var sb = new StringBuilder();
            sb.Append($"{{ createAvatar(index: {index}, name: \"{name}\"");
            if (hair.HasValue)
            {
                sb.Append($", hair: {hair}");
            }

            if (lens.HasValue)
            {
                sb.Append($", lens: {lens}");
            }

            if (ear.HasValue)
            {
                sb.Append($", ear: {ear}");
            }

            if (tail.HasValue)
            {
                sb.Append($", tail: {tail}");
            }

            sb.Append(") }");
            var query = sb.ToString();
            var queryResult = await ExecuteQueryAsync<ActionQuery>(
                query,
                standaloneContext: _standaloneContext);
            if (!errorsShouldBeNull)
            {
                Assert.NotNull(queryResult.Errors);
                return;
            }

            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["createAvatar"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<CreateAvatar>(polymorphicAction.InnerAction);
            Assert.Equal(index, action.index);
            Assert.Equal(name, action.name);
            Assert.Equal(hair ?? 0, action.hair);
            Assert.Equal(lens ?? 0, action.lens);
            Assert.Equal(ear ?? 0, action.ear);
            Assert.Equal(tail ?? 0, action.tail);
        }

        [Theory]
        [InlineData(0, 1, true)] // Actually this cannot be executed, but can build a query.
        [InlineData(1001, 1, true)]
        [InlineData(1001, null, true)]
        [InlineData(1001, -1, false)]
        public async Task RuneEnhancement(int runeId, int? tryCount, bool isSuccessCase)
        {
            var avatarAddress = new PrivateKey().ToAddress();
            var args = $"avatarAddress: \"{avatarAddress}\", runeId: {runeId}";
            if (tryCount is not null)
            {
                args += $" tryCount: {tryCount}";
            }

            var query = $"{{runeEnhancement({args})}}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            if (!isSuccessCase)
            {
                Assert.NotNull(queryResult.Errors);
                return;
            }

            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["runeEnhancement"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<RuneEnhancement>(polymorphicAction.InnerAction);
            Assert.Equal(avatarAddress, action.AvatarAddress);
            Assert.Equal(runeId, action.RuneId);
            Assert.Equal(tryCount ?? 1, action.TryCount);
        }

        [Theory]
        [InlineData(false, false, false, false, false)]
        [InlineData(true, false, false, false, false)]
        [InlineData(true, true, false, false, false)]
        [InlineData(true, true, true, false, false)]
        [InlineData(true, true, true, true, false)]
        [InlineData(true, true, true, true, true)]
        public async Task HackAndSlash(bool useCostume, bool useEquipment, bool useFood, bool useRune, bool useBuff)
        {
            var avatarAddress = new PrivateKey().ToAddress();
            var worldId = 1;
            var stageId = 1;
            var costume = Guid.NewGuid();
            var equipment = Guid.NewGuid();
            var food = Guid.NewGuid();
            var runeInfo = new RuneSlotInfo(0, 10001);
            var stageBuffId = 1;

            var args = $"avatarAddress: \"{avatarAddress}\", worldId: {worldId}, stageId: {stageId}";
            if (useCostume)
            {
                args += $", costumeIds: [\"{costume}\"]";
            }

            if (useEquipment)
            {
                args += $", equipmentIds: [\"{equipment}\"]";
            }

            if (useFood)
            {
                args += $", consumableIds: [\"{food}\"]";
            }

            if (useRune)
            {
                args += $", runeSlotInfos: [{{slotIndex: {runeInfo.SlotIndex}, runeId: {runeInfo.RuneId}}}]";
            }

            if (useBuff)
            {
                args += $", stageBuffId: {stageBuffId}";
            }

            var query = $"{{hackAndSlash({args})}}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            Assert.Null(queryResult.Errors);

            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["hackAndSlash"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<HackAndSlash>(polymorphicAction.InnerAction);
            Assert.Equal(avatarAddress, action.AvatarAddress);
            Assert.Equal(worldId, action.WorldId);
            Assert.Equal(stageId, action.StageId);
            if (useCostume)
            {
                Assert.Equal(costume, action.Costumes.First());
            }

            if (useEquipment)
            {
                Assert.Equal(equipment, action.Equipments.First());
            }

            if (useFood)
            {
                Assert.Equal(food, action.Foods.First());
            }

            if (useRune)
            {
                Assert.Equal(runeInfo.SlotIndex, action.RuneInfos.First().SlotIndex);
                Assert.Equal(runeInfo.RuneId, action.RuneInfos.First().RuneId);
            }

            if (useBuff)
            {
                Assert.Equal(stageBuffId, action.StageBuffId);
            }
        }

        [Theory]
        [InlineData(false, false, false, false)]
        [InlineData(true, false, false, false)]
        [InlineData(true, true, false, false)]
        [InlineData(true, true, true, false)]
        [InlineData(true, true, true, true)]
        public async Task HackAndSlashSweep(bool useCostume, bool useEquipment, bool useRune, bool useApStone)
        {
            var avatarAddress = new PrivateKey().ToAddress();
            var worldId = 1;
            var stageId = 1;
            var costume = Guid.NewGuid();
            var equipment = Guid.NewGuid();
            var runeInfo = new RuneSlotInfo(0, 10001);
            var actionPoint = 120;
            var apStoneCount = 1;

            var args = @$"
avatarAddress: ""{avatarAddress}"", 
worldId: {worldId}, 
stageId: {stageId},
actionPoint: {actionPoint},
";
            if (useApStone)
            {
                args += $", apStoneCount: {apStoneCount}";
            }

            if (useCostume)
            {
                args += $", costumeIds: [\"{costume}\"]";
            }

            if (useEquipment)
            {
                args += $", equipmentIds: [\"{equipment}\"]";
            }

            if (useRune)
            {
                args += $", runeSlotInfos: [{{slotIndex: {runeInfo.SlotIndex}, runeId: {runeInfo.RuneId}}}]";
            }

            var query = $"{{hackAndSlashSweep({args})}}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            Assert.Null(queryResult.Errors);

            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["hackAndSlashSweep"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<HackAndSlashSweep>(polymorphicAction.InnerAction);
            Assert.Equal(avatarAddress, action.avatarAddress);
            Assert.Equal(worldId, action.worldId);
            Assert.Equal(stageId, action.stageId);
            Assert.Equal(actionPoint, action.actionPoint);
            Assert.Equal(useApStone ? apStoneCount : 0, action.apStoneCount);
            if (useCostume)
            {
                Assert.Equal(costume, action.costumes.First());
            }

            if (useEquipment)
            {
                Assert.Equal(equipment, action.equipments.First());
            }

            if (useRune)
            {
                Assert.Equal(runeInfo.SlotIndex, action.runeInfos.First().SlotIndex);
                Assert.Equal(runeInfo.RuneId, action.runeInfos.First().RuneId);
            }
        }

        [Fact]
        public async Task DailyReward()
        {
            var avatarAddress = new PrivateKey().ToAddress();
            var query = $"{{dailyReward(avatarAddress: \"{avatarAddress}\")}}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            Assert.Null(queryResult.Errors);

            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["dailyReward"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<DailyReward>(polymorphicAction.InnerAction);
            Assert.Equal(avatarAddress, action.avatarAddress);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CombinationEquipment(bool useSubRecipe)
        {
            var avatarAddress = new PrivateKey().ToAddress();
            var slotIndex = 0;
            var recipeId = 1;
            var subRecipeId = 10;
            var payByCrystalValue = "false";
            var payByCrystal = false;
            var useHammerPointValue = "false";
            var useHammerPoint = false;

            var args =
                $"avatarAddress: \"{avatarAddress}\", slotIndex: {slotIndex}, recipeId: {recipeId}, payByCrystal: {payByCrystalValue}, useHammerPoint: {useHammerPointValue}";
            if (useSubRecipe)
            {
                args += $", subRecipeId: {subRecipeId}";
            }

            var query = $"{{combinationEquipment({args})}}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            Assert.Null(queryResult.Errors);

            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["combinationEquipment"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<CombinationEquipment>(polymorphicAction.InnerAction);
            Assert.Equal(avatarAddress, action.avatarAddress);
            Assert.Equal(slotIndex, action.slotIndex);
            Assert.Equal(recipeId, action.recipeId);
            Assert.Equal(payByCrystal, action.payByCrystal);
            Assert.Equal(useHammerPoint, action.useHammerPoint);
            if (useSubRecipe)
            {
                Assert.Equal(subRecipeId, action.subRecipeId);
            }
            else
            {
                Assert.Null(action.subRecipeId);
            }
        }

        [Fact]
        public async Task ItemEnhantement()
        {
            var avatarAddress = new PrivateKey().ToAddress();
            var slotIndex = 0;
            var itemId = Guid.NewGuid();
            var materialId = Guid.NewGuid();

            var query = $"{{itemEnhancement(avatarAddress: \"{avatarAddress}\", slotIndex: {slotIndex}, " +
                        $"itemId: \"{itemId}\", materialId: \"{materialId}\")}}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            Assert.Null(queryResult.Errors);

            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["itemEnhancement"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<ItemEnhancement>(polymorphicAction.InnerAction);
            Assert.Equal(avatarAddress, action.avatarAddress);
            Assert.Equal(slotIndex, action.slotIndex);
            Assert.Equal(itemId, action.itemId);
            Assert.Equal(materialId, action.materialId);
        }

        [Fact]
        public async Task RapidCombination()
        {
            var avatarAddress = new PrivateKey().ToAddress();
            var slotIndex = 0;

            var query = $"{{rapidCombination(avatarAddress: \"{avatarAddress}\", slotIndex: {slotIndex})}}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            Assert.Null(queryResult.Errors);

            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["rapidCombination"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<RapidCombination>(polymorphicAction.InnerAction);
            Assert.Equal(avatarAddress, action.avatarAddress);
            Assert.Equal(slotIndex, action.slotIndex);
        }

        [Fact]
        public async Task CombinationConsumable()
        {
            var avatarAddress = new PrivateKey().ToAddress();
            var slotIndex = 0;
            var recipeId = 1;

            var query =
                $"{{combinationConsumable(avatarAddress: \"{avatarAddress}\", slotIndex: {slotIndex}, recipeId: {recipeId})}}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            Assert.Null(queryResult.Errors);

            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["combinationConsumable"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<CombinationConsumable>(polymorphicAction.InnerAction);
            Assert.Equal(avatarAddress, action.avatarAddress);
            Assert.Equal(slotIndex, action.slotIndex);
            Assert.Equal(recipeId, action.recipeId);
        }
    }
}
