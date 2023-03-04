using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Headless.Hosting;
using Xunit;

namespace Libplanet.Headless.Tests.Hosting
{
    public class LibplanetNodeServiceTest
    {
        [Fact]
        public void Constructor()
        {
            var genesisBlock = BlockChain<DummyAction>.MakeGenesisBlock();

            IActionTypeLoader actionTypeLoader = new StaticActionTypeLoader(
                Assembly.GetEntryAssembly() is { } entryAssembly
                    ? new[] { typeof(DummyAction).Assembly, entryAssembly }
                    : new[] { typeof(DummyAction).Assembly },
                typeof(DummyAction)
            );

            var service = new LibplanetNodeService<DummyAction>(
                new LibplanetNodeServiceProperties<DummyAction>()
                {
                    AppProtocolVersion = new AppProtocolVersion(),
                    GenesisBlock = genesisBlock,
                    SwarmPrivateKey = new PrivateKey(),
                    StoreStatesCacheSize = 2,
                    StorePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
                    Host = IPAddress.Loopback.ToString(),
                    IceServers = new List<IceServer>(),
                },
                blockPolicy: new BlockPolicy<DummyAction>(),
                stagePolicy: new VolatileStagePolicy<DummyAction>(),
                renderers: null,
                minerLoopAction: (chain, swarm, pk, ct) => Task.CompletedTask,
                preloadProgress: null,
                exceptionHandlerAction: (code, msg) => throw new Exception($"{code}, {msg}"),
                preloadStatusHandlerAction: isPreloadStart => { },
                actionTypeLoader: actionTypeLoader
            );

            Assert.NotNull(service);
        }

        [Fact]
        public void PropertiesMustContainGenesisBlockOrPath()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                IActionTypeLoader actionTypeLoader = new StaticActionTypeLoader(
                    Assembly.GetEntryAssembly() is { } entryAssembly
                        ? new[] { typeof(DummyAction).Assembly, entryAssembly }
                        : new[] { typeof(DummyAction).Assembly },
                    typeof(DummyAction)
                );

                var service = new LibplanetNodeService<DummyAction>(
                    new LibplanetNodeServiceProperties<DummyAction>()
                    {
                        AppProtocolVersion = new AppProtocolVersion(),
                        SwarmPrivateKey = new PrivateKey(),
                        StoreStatesCacheSize = 2,
                        Host = IPAddress.Loopback.ToString(),
                        IceServers = new List<IceServer>(),
                    },
                    blockPolicy: new BlockPolicy<DummyAction>(),
                    stagePolicy: new VolatileStagePolicy<DummyAction>(),
                    renderers: null,
                    minerLoopAction: (chain, swarm, pk, ct) => Task.CompletedTask,
                    preloadProgress: null,
                    exceptionHandlerAction: (code, msg) => throw new Exception($"{code}, {msg}"),
                    preloadStatusHandlerAction: isPreloadStart => { },
                    actionTypeLoader: actionTypeLoader
                );
            });
        }

        private class DummyAction : IAction
        {
            IValue IAction.PlainValue => Dictionary.Empty;

            IAccountStateDelta IAction.Execute(IActionContext context)
            {
                return context.PreviousStates;
            }

            void IAction.LoadPlainValue(IValue plainValue)
            {
            }
        }
    }
}
