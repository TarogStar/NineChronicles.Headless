using System.Collections.Immutable;
using System.Numerics;
using Bencodex;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Consensus;

namespace Libplanet.Extensions.ActionEvaluatorCommonComponents;

public class AccountStateDelta : IAccountStateDelta, IValidatorSupportStateDelta
{
    private IImmutableDictionary<Address, IValue> _states;
    private IImmutableDictionary<(Address, Currency), BigInteger> _balances;
    private IImmutableDictionary<Currency, BigInteger> _totalSupplies;
    private ValidatorSet? _validatorSet;

    public IImmutableSet<Address> UpdatedAddresses => _states.Keys.ToImmutableHashSet();

    public IImmutableSet<Address> StateUpdatedAddresses => _states.Keys.ToImmutableHashSet();

#pragma warning disable LAA1002
    public IImmutableDictionary<Address, IImmutableSet<Currency>> UpdatedFungibleAssets =>
        _balances.GroupBy(kv => kv.Key.Item1).ToImmutableDictionary(
            g => g.Key,
            g => (IImmutableSet<Currency>)g.Select(kv => kv.Key.Item2).ToImmutableHashSet()
        );
#pragma warning restore LAA1002

    public IImmutableSet<Currency> TotalSupplyUpdatedCurrencies =>
        _totalSupplies.Keys.ToImmutableHashSet();

    public AccountStateGetter StateGetter { get; set; }

    public AccountBalanceGetter BalanceGetter { get; set; }

    public TotalSupplyGetter TotalSupplyGetter { get; set; }

    public ValidatorSetGetter ValidatorSetGetter { get; set; }


    public AccountStateDelta()
        : this(
            ImmutableDictionary<Address, IValue>.Empty,
            ImmutableDictionary<(Address, Currency), BigInteger>.Empty,
            ImmutableDictionary<Currency, BigInteger>.Empty,
            null)
    {
    }

    public AccountStateDelta(
        IImmutableDictionary<Address, IValue> states,
        IImmutableDictionary<(Address, Currency), BigInteger> balances,
        IImmutableDictionary<Currency, BigInteger> totalSupplies,
        ValidatorSet? validatorSet
    )
    {
        _states = states;
        _balances = balances;
        _totalSupplies = totalSupplies;
        _validatorSet = validatorSet;
    }

    public AccountStateDelta(Dictionary states, List balances, Dictionary totalSupplies, IValue validatorSet)
    {
        // This assumes `states` consists of only Binary keys:
        _states = states.ToImmutableDictionary(
            kv => new Address(kv.Key),
            kv => kv.Value
        );

        _balances = balances.Cast<Dictionary>().ToImmutableDictionary(
            record => (new Address(((Binary)record["address"]).ByteArray),
                new Currency((Dictionary)record["currency"])),
            record => (BigInteger)(Integer)record["amount"]
        );

        // This assumes `totalSupplies` consists of only Binary keys:
        _totalSupplies = totalSupplies.ToImmutableDictionary(
            kv => new Currency(new Codec().Decode((Binary)kv.Key)),
            kv => (BigInteger)(Integer)kv.Value
        );

        _validatorSet = new ValidatorSet(validatorSet);
    }

    public AccountStateDelta(IValue serialized)
        : this(
            (Dictionary)((Dictionary)serialized)["states"],
            (List)((Dictionary)serialized)["balances"],
            (Dictionary)((Dictionary)serialized)["totalSupplies"],
            ((Dictionary)serialized)["validatorSet"]
        )
    {
    }

    public AccountStateDelta(byte[] bytes)
        : this((Dictionary)new Codec().Decode(bytes))
    {
    }

    public IValue? GetState(Address address) =>
        _states.ContainsKey(address)
            ? _states[address]
            : StateGetter(new[] { address })[0];

    public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses) =>
        addresses.Select(GetState).ToArray();

    public IAccountStateDelta SetState(Address address, IValue state) =>
        new AccountStateDelta(_states.SetItem(address, state), _balances, _totalSupplies, _validatorSet)
        {
            StateGetter = StateGetter,
            BalanceGetter = BalanceGetter,
            ValidatorSetGetter = ValidatorSetGetter,
            TotalSupplyGetter = TotalSupplyGetter,
        };

    public FungibleAssetValue GetBalance(Address address, Currency currency)
    {
        if (!_balances.TryGetValue((address, currency), out BigInteger rawValue))
        {
            return BalanceGetter(address, currency);
        }

        return FungibleAssetValue.FromRawValue(currency, rawValue);
    }

    public FungibleAssetValue GetTotalSupply(Currency currency)
    {
        if (!currency.TotalSupplyTrackable)
        {
            var msg =
                $"The total supply value of the currency {currency} is not trackable"
                + " because it is a legacy untracked currency which might have been"
                + " established before the introduction of total supply tracking support.";
            throw new TotalSupplyNotTrackableException(msg, currency);
        }

        // Return dirty state if it exists.
        if (_totalSupplies.TryGetValue(currency, out var totalSupplyValue))
        {
            return FungibleAssetValue.FromRawValue(currency, totalSupplyValue);
        }

        return TotalSupplyGetter(currency);
    }

    public IAccountStateDelta MintAsset(Address recipient, FungibleAssetValue value)
    {
        // FIXME: 트랜잭션 서명자를 알아내 currency.AllowsToMint() 확인해서 CurrencyPermissionException
        // 던지는 처리를 해야하는데 여기서 트랜잭션 서명자를 무슨 수로 가져올지 잘 모르겠음.

        var currency = value.Currency;

        if (value <= currency * 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        var nextAmount = GetBalance(recipient, value.Currency) + value;

        if (currency.TotalSupplyTrackable)
        {
            var currentTotalSupply = GetTotalSupply(currency);
            if (currency.MaximumSupply < currentTotalSupply + value)
            {
                var msg = $"The amount {value} attempted to be minted added to the current"
                          + $" total supply of {currentTotalSupply} exceeds the"
                          + $" maximum allowed supply of {currency.MaximumSupply}.";
                throw new SupplyOverflowException(msg, value);
            }

            return new AccountStateDelta(
                _states,
                _balances.SetItem(
                    (recipient, value.Currency),
                    nextAmount.RawValue
                ),
                _totalSupplies.SetItem(currency, (currentTotalSupply + value).RawValue),
                _validatorSet
            )
            {
                StateGetter = StateGetter,
                BalanceGetter = BalanceGetter,
                ValidatorSetGetter = ValidatorSetGetter,
                TotalSupplyGetter = TotalSupplyGetter,
            };
        }

        return new AccountStateDelta(
            _states,
            _balances.SetItem(
                (recipient, value.Currency),
                nextAmount.RawValue
            ),
            _totalSupplies,
            _validatorSet
        )
        {
            StateGetter = StateGetter,
            BalanceGetter = BalanceGetter,
            ValidatorSetGetter = ValidatorSetGetter,
            TotalSupplyGetter = TotalSupplyGetter,
        };
    }

    public IAccountStateDelta TransferAsset(
        Address sender,
        Address recipient,
        FungibleAssetValue value,
        bool allowNegativeBalance = false)
    {
        if (value.Sign <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        FungibleAssetValue senderBalance = GetBalance(sender, value.Currency);
        if (senderBalance < value)
        {
            throw new InsufficientBalanceException(
                $"There is no sufficient balance for {sender}: {senderBalance} < {value}",
                sender,
                senderBalance
            );
        }

        Currency currency = value.Currency;
        FungibleAssetValue senderRemains = senderBalance - value;
        FungibleAssetValue recipientRemains = GetBalance(recipient, currency) + value;
        var balances = _balances
            .SetItem((sender, currency), senderRemains.RawValue)
            .SetItem((recipient, currency), recipientRemains.RawValue);
        return new AccountStateDelta(_states, balances, _totalSupplies, _validatorSet)
        {
            StateGetter = StateGetter,
            BalanceGetter = BalanceGetter,
            ValidatorSetGetter = ValidatorSetGetter,
            TotalSupplyGetter = TotalSupplyGetter,
        };
    }

    public IAccountStateDelta BurnAsset(Address owner, FungibleAssetValue value)
    {
        // FIXME: 트랜잭션 서명자를 알아내 currency.AllowsToMint() 확인해서 CurrencyPermissionException
        // 던지는 처리를 해야하는데 여기서 트랜잭션 서명자를 무슨 수로 가져올지 잘 모르겠음.

        var currency = value.Currency;

        if (value <= currency * 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        FungibleAssetValue balance = GetBalance(owner, currency);
        if (balance < value)
        {
            throw new InsufficientBalanceException(
                $"There is no sufficient balance for {owner}: {balance} < {value}",
                owner,
                value
            );
        }

        FungibleAssetValue nextValue = balance - value;
        return new AccountStateDelta(
            _states,
            _balances.SetItem(
                (owner, currency),
                nextValue.RawValue
            ),
            currency.TotalSupplyTrackable
                ? _totalSupplies.SetItem(
                    currency,
                    (GetTotalSupply(currency) - value).RawValue)
                : _totalSupplies,
            _validatorSet
        )
        {
            StateGetter = StateGetter,
            BalanceGetter = BalanceGetter,
            ValidatorSetGetter = ValidatorSetGetter,
            TotalSupplyGetter = TotalSupplyGetter,
        };
    }

    public ValidatorSet GetValidatorSet()
    {
        return _validatorSet ?? ValidatorSetGetter();
    }

    public IAccountStateDelta SetValidator(Validator validator)
    {
        return new AccountStateDelta(
            _states,
            _balances,
            _totalSupplies,
            GetValidatorSet().Update(validator)
        )
        {
            StateGetter = StateGetter,
            BalanceGetter = BalanceGetter,
            ValidatorSetGetter = ValidatorSetGetter,
            TotalSupplyGetter = TotalSupplyGetter,
        };
    }
}
