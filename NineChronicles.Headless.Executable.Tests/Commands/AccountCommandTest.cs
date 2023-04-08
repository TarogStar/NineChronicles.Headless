using System;
using System.IO;
using Bencodex;
using Libplanet;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.RocksDBStore;
using Libplanet.Store;
using Nekoyume.BlockChain.Policy;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.Store;
using NineChronicles.Headless.Executable.Tests.IO;
using Serilog.Core;
using Xunit;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;
using Lib9cUtils = Lib9c.DevExtensions.Utils;

namespace NineChronicles.Headless.Executable.Tests.Commands
{
    public class AccountCommandTest
    {
        private readonly StringIOConsole _console;
        private readonly Codec _codec = new Codec();
        private readonly AccountCommand _command;

        private readonly string _storePath;

        public AccountCommandTest()
        {
            _console = new StringIOConsole();
            _command = new AccountCommand(_console);
            _storePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        }

        [Theory]
        // [InlineData(StoreType.Default)]  // Balance() loads loads only RocksDB 
        [InlineData(StoreType.RocksDb)]
        public void Balance(StoreType storeType)
        {
            IStore store = storeType.CreateStore(_storePath);
            var statesPath = Path.Combine(_storePath, "states");
            Address targetAddress = new PrivateKey().ToAddress();
            int targetCurrency = 10000; // 100 NCG
            Block<NCAction> genesisBlock = GenesisHelper.MineGenesisBlock(targetAddress, targetCurrency);
            var stateKeyValueStore = new RocksDBKeyValueStore(statesPath);
            var stateStore = new TrieStateStore(stateKeyValueStore);
            IStagePolicy<NCAction> stagePolicy = new VolatileStagePolicy<NCAction>();
            IBlockPolicy<NCAction> blockPolicy = new BlockPolicySource(Logger.None).GetPolicy();
            BlockChain<NCAction> chain = BlockChain<NCAction>.Create(
                blockPolicy,
                stagePolicy,
                store,
                stateStore,
                genesisBlock);
            Guid chainId = chain.Id;
            chain.ExecuteActions(chain.Tip);
            store.Dispose();
            stateStore.Dispose();

            _command.Balance(false, _storePath, chainId: chainId, address: targetAddress.ToString());
            string[] result = _console.Out.ToString().Trim().Split("\t");
            Assert.Equal(targetAddress.ToString(), result[0]);
            // NCG recognizes the last two digits after the decimal point.
            Assert.Equal(targetCurrency / 100, Int32.Parse(result[1].Split(" ")[0]));
        }
    }
}
