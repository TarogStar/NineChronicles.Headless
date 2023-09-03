#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Action.State;
using NineChronicles.Headless.Utils;
using Libplanet.Types.Consensus;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class StateContext : IAccountStateDelta
    {
        public StateContext(
            IAccountState accountState,
            long? blockIndex)
        {
            AccountState = accountState;
            BlockIndex = blockIndex;
            CurrencyFactory = new CurrencyFactory(() => accountState);
            FungibleAssetValueFactory = new FungibleAssetValueFactory(CurrencyFactory);
        }

        public IAccountState AccountState { get; }

        public long? BlockIndex { get; }

        public static ChampionArenaInfo[] AddRank(ChampionArenaInfo[] tuples)
        {

            if (tuples.Length == 0)
            {
                return new ChampionArenaInfo[0];
            }

            var orderedTuples = tuples
                .OrderByDescending(tuple => tuple.Score)
                .ThenBy(tuple => tuple.AvatarAddress)
                .ToArray();

            var result = new List<ChampionArenaInfo>();
            var trunk = new List<ChampionArenaInfo>();
            int? currentScore = null;
            var currentRank = 1;
            for (var i = 0; i < orderedTuples.Length; i++)
            {
                var tuple = orderedTuples[i];
                if (!currentScore.HasValue)
                {
                    currentScore = tuple.Score;
                    trunk.Add(tuple);
                    continue;
                }

                if (currentScore.Value == tuple.Score)
                {
                    trunk.Add(tuple);
                    currentRank++;
                    if (i < orderedTuples.Length - 1)
                    {
                        continue;
                    }

                    foreach (var tupleInTrunk in trunk)
                    {
                        tupleInTrunk.Rank = currentRank;
                        result.Add(tupleInTrunk);
                    }

                    trunk.Clear();

                    continue;
                }

                foreach (var tupleInTrunk in trunk)
                {
                    tupleInTrunk.Rank = currentRank;
                    result.Add(tupleInTrunk);
                }

                trunk.Clear();
                if (i < orderedTuples.Length - 1)
                {
                    trunk.Add(tuple);
                    currentScore = tuple.Score;
                    currentRank++;
                    continue;
                }
                tuple.Rank = currentRank;
                result.Add(tuple);
            }

            return result.ToArray();
        }
        public IImmutableSet<Address> UpdatedAddresses => throw new System.NotImplementedException();

        public IImmutableSet<Address> StateUpdatedAddresses => throw new System.NotImplementedException();

        public IImmutableDictionary<Address, IImmutableSet<Currency>> UpdatedFungibleAssets => throw new System.NotImplementedException();

        public IImmutableSet<Currency> TotalSupplyUpdatedCurrencies => throw new System.NotImplementedException();

        public CurrencyFactory CurrencyFactory { get; }

        public FungibleAssetValueFactory FungibleAssetValueFactory { get; }

        IAccountDelta IAccountStateDelta.Delta => throw new System.NotImplementedException();

        IImmutableSet<(Address, Currency)> IAccountStateDelta.TotalUpdatedFungibleAssets => throw new System.NotImplementedException();

        public IValue? GetState(Address address) => AccountState.GetState(address);

        public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses) => AccountState.GetStates(addresses);

        public FungibleAssetValue GetBalance(Address address, Currency currency) => AccountState.GetBalance(address, currency);

        public IAccountStateDelta SetState(Address address, IValue state)
        {
            throw new System.NotImplementedException();
        }

        public IAccountStateDelta MintAsset(Address recipient, FungibleAssetValue value)
        {
            throw new System.NotImplementedException();
        }

        public IAccountStateDelta TransferAsset(Address sender, Address recipient, FungibleAssetValue value, bool allowNegativeBalance = false)
        {
            throw new System.NotImplementedException();
        }

        public IAccountStateDelta BurnAsset(Address owner, FungibleAssetValue value)
        {
            throw new System.NotImplementedException();
        }

        public IAccountStateDelta TotalUpdateFungibleAssets(Address owner, FungibleAssetValue value)
        {
            throw new System.NotImplementedException();
        }

        public FungibleAssetValue GetTotalSupply(Currency currency)
        {
            throw new System.NotImplementedException();
        }

        IAccountStateDelta IAccountStateDelta.MintAsset(IActionContext context, Address recipient, FungibleAssetValue value)
        {
            throw new System.NotImplementedException();
        }

        IAccountStateDelta IAccountStateDelta.TransferAsset(IActionContext context, Address sender, Address recipient, FungibleAssetValue value, bool allowNegativeBalance)
        {
            throw new System.NotImplementedException();
        }

        IAccountStateDelta IAccountStateDelta.BurnAsset(IActionContext context, Address owner, FungibleAssetValue value)
        {
            throw new System.NotImplementedException();
        }

        IAccountStateDelta IAccountStateDelta.SetValidator(Validator validator)
        {
            throw new System.NotImplementedException();
        }

        ValidatorSet IAccountState.GetValidatorSet()
        {
            throw new System.NotImplementedException();
        }
    }
}
